using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum EstadoRecolector
{
    Buscando,
    CaminandoAlRecurso,
    Trabajando,
    VolviendoBase
}

public class RecolectorIA : MonoBehaviour
{
    [Header("Qué recolecta")]
    // Configurable por prefab: el leñador rojo = Madera, el minero rojo = Piedra, etc.
    public Recurso.TipoRecurso tipoRecurso = Recurso.TipoRecurso.Madera;

    [Header("Ajustes de Recolección")]
    public float tiempoRecoleccion = 2f;
    public int capacidadCarga = 10;          // Cuánto trae en cada viaje
    public float radioBusquedaNavMesh = 3f;  // Punto caminable junto al recurso (no el tronco/roca)

    // Distancia EN LÍNEA RECTA a la que se da por llegado al recurso o a la base.
    // Es la misma estrategia que usa el recolector del jugador, y es robusta a
    // simulaciones aceleradas: con la escala de tiempo alta, cada fotograma avanza
    // mucho tiempo de juego, el agente se pasa de su punto exacto de parada y la
    // comprobación del NavMesh no llega a detectar nunca la llegada.
    public float radioLlegada = 3.5f;
    // Distancia al punto final de su ruta a la que se da por llegado
    public float margenFinalRuta = 1.5f;

    // Distancia AL CENTRO DEL CASTILLO a la que se entrega la carga. Es mayor que
    // la del árbol porque el castillo es un edificio grande: medir con el mismo
    // margen que un árbol hacía que el recolector llegara a la pared sin cumplirlo.
    public float radioEntrega = 5f;

    // Los recolectores no van todos al mismo punto del castillo, sino a uno
    // distinto a su alrededor. Si van al mismo, se amontonan y se empujan entre
    // ellos, y a velocidades altas ese forcejeo les impide llegar a entregar.
    public float radioReparto = 3f;

    [Header("Diagnóstico")]
    // Cada cierto tiempo de juego informa de en qué ha gastado el tiempo.
    // Sirve para localizar en qué fase del ciclo se atasca la recolección.
    public bool informeDeTiempos = true;
    public float segundosEntreInformes = 60f;

    [Header("Ventaja de la IA")]
    public float bonoDificultad = 1.2f; // 1.2 = 20% más rápido

    private EstadoRecolector estadoActual;
    private NavMeshAgent agente;
    private Transform objetivoActual;
    private Recurso recursoObjetivo;
    private BaseMilitar miBase;
    private int equipo;
    private int recursosLlevados = 0;

    // ====================================================================
    // ASIGNACIÓN DE BASE
    // La base que crea a esta tropa la llama para decirle a quién pertenece.
    // Así, aunque haya varias bases rojas, cada tropa entrega a la SUYA.
    // ====================================================================
    public void AsignarBase(BaseMilitar baseAsignada)
    {
        miBase = baseAsignada;
        if (miBase != null) equipo = miBase.equipo;
    }

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        agente.speed *= bonoDificultad;

        // Leemos nuestro equipo del componente Tropa (por si no nos asignaron base)
        Tropa miTropa = GetComponent<Tropa>();
        if (miTropa != null) equipo = miTropa.equipo;

        // PLAN B: si nadie nos asignó base, buscamos la MÁS CERCANA de nuestro equipo.
        // Al nacer estamos pegados a nuestra base, así que será la correcta.
        if (miBase == null)
        {
            BuscarBaseMasCercana();
        }

        estadoActual = EstadoRecolector.Buscando;
    }

    void BuscarBaseMasCercana()
    {
        // Solo bases de MI partida (con varias áreas a la vez, una búsqueda
        // global podría mandarnos a entregar a la base de otra partida).
        AreaPartida area = AreaPartida.Resolver(this);
        if (area == null) return;

        float distanciaMinima = Mathf.Infinity;

        foreach (BaseMilitar b in area.Bases())
        {
            if (b.equipo != equipo) continue; // solo bases de nuestro bando

            float distancia = Vector3.Distance(transform.position, b.transform.position);
            if (distancia < distanciaMinima)
            {
                distanciaMinima = distancia;
                miBase = b;
            }
        }
    }

    // --- Detección de atasco ---
    // Si el punto de destino no es del todo alcanzable, el agente se queda
    // oscilando de lado a lado indefinidamente. Si detectamos que no avanza,
    // lo desatascamos y le buscamos otro objetivo.
    private float remanenteAnterior = Mathf.Infinity;
    private float tiempoSinAvanzar = 0f;
    private const float SEGUNDOS_PARA_ATASCO = 2.5f;

    void ReiniciarMedidorAtasco()
    {
        remanenteAnterior = Mathf.Infinity;
        tiempoSinAvanzar = 0f;
    }

    // Lleva la unidad al punto caminable más cercano si ha quedado fuera de la malla
    void RecolocarEnNavMesh()
    {
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            agente.Warp(hit.position);
            estadoActual = EstadoRecolector.Buscando; // retoma su ciclo desde el principio
        }
    }

    // Devuelve true si lleva demasiado tiempo sin acercarse a su destino
    bool EstaAtascado()
    {
        if (agente == null || !agente.isOnNavMesh) return false;
        if (agente.pathPending || !agente.hasPath) return false;

        if (agente.remainingDistance < remanenteAnterior - 0.05f)
        {
            remanenteAnterior = agente.remainingDistance;
            tiempoSinAvanzar = 0f;
            return false;
        }

        tiempoSinAvanzar += Time.deltaTime;
        return tiempoSinAvanzar > SEGUNDOS_PARA_ATASCO;
    }

    // --- Diagnóstico: cuánto tiempo pasa en cada estado ---
    private readonly float[] tiempoEnEstado = new float[4];
    private int entregasPeriodo = 0;
    private float relojInforme = 0f;

    void AcumularDiagnostico()
    {
        if (!informeDeTiempos) return;

        tiempoEnEstado[(int)estadoActual] += Time.deltaTime;
        relojInforme += Time.deltaTime;
        if (relojInforme < segundosEntreInformes) return;

        AreaPartida area = AreaPartida.Resolver(this);
        string etiqueta = (area != null) ? $"[A{area.idArea}] " : "";
        float total = relojInforme;
        Debug.Log($"{etiqueta}DIAG {name.Replace("(Clone)", "")}: " +
                  $"buscando {100 * tiempoEnEstado[0] / total:0}% | " +
                  $"camino al recurso {100 * tiempoEnEstado[1] / total:0}% | " +
                  $"trabajando {100 * tiempoEnEstado[2] / total:0}% | " +
                  $"volviendo {100 * tiempoEnEstado[3] / total:0}% | " +
                  $"entregas {entregasPeriodo} en {total:0} s");

        for (int i = 0; i < tiempoEnEstado.Length; i++) tiempoEnEstado[i] = 0f;
        entregasPeriodo = 0;
        relojInforme = 0f;
    }

    void Update()
    {
        AcumularDiagnostico();

        switch (estadoActual)
        {
            case EstadoRecolector.Buscando:
                BuscarRecursoMasCercano();
                break;

            case EstadoRecolector.CaminandoAlRecurso:
                ComprobarLlegada(objetivoActual, EstadoRecolector.Trabajando);
                break;

            case EstadoRecolector.VolviendoBase:
                ComprobarLlegada(miBase != null ? miBase.transform : null, EstadoRecolector.Buscando);
                break;

            // En "Trabajando" no hacemos nada en Update: lo gestiona la corrutina.
        }
    }

    void BuscarRecursoMasCercano()
    {
        // Solo nodos de MI partida
        AreaPartida area = AreaPartida.Resolver(this);
        if (area == null) return;

        float distanciaMinima = Mathf.Infinity;
        Recurso elegido = null;

        foreach (Recurso rec in area.Recursos())
        {
            // Solo nos interesan los recursos de NUESTRO tipo y que tengan reserva
            if (rec.tipo != tipoRecurso) continue;
            if (!rec.EstaDisponible()) continue;

            float distanciaReal = Vector3.Distance(transform.position, rec.transform.position);

            // Pequeño "ruido" para que no se amontonen todos en el mismo recurso
            float distanciaPercibida = distanciaReal + Random.Range(0f, 15f);

            if (distanciaPercibida < distanciaMinima)
            {
                distanciaMinima = distanciaPercibida;
                elegido = rec;
            }
        }

        if (elegido != null)
        {
            recursoObjetivo = elegido;
            objetivoActual = elegido.transform;

            // Buscamos un punto caminable cerca del recurso para no chocar contra él
            NavMeshHit hit;
            if (NavMesh.SamplePosition(objetivoActual.position, out hit, radioBusquedaNavMesh, NavMesh.AllAreas))
            {
                // Solo pasamos a caminar si la orden se ha podido dar de verdad
                if (NavegacionSegura.IrA(agente, hit.position))
                    estadoActual = EstadoRecolector.CaminandoAlRecurso;
            }
            // Si no hay punto válido, seguimos en "Buscando" y probamos con otro recurso.
        }
    }

    void ComprobarLlegada(Transform destino, EstadoRecolector siguienteEstado)
    {
        // Si el destino desapareció (otro recolector agotó el recurso, etc.), a buscar de nuevo
        if (destino == null)
        {
            estadoActual = EstadoRecolector.Buscando;
            return;
        }

        // Con reinicios frecuentes y varias partidas, el agente puede estar recién
        // creado o a punto de destruirse, fuera del NavMesh. Consultarle la distancia
        // en ese estado es lo que llenaba la consola de errores.
        if (agente == null || !agente.isActiveAndEnabled) return;

        // Si ha quedado fuera de la malla (por ejemplo, al nacer), lo recolocamos
        // en el punto caminable más cercano. Antes solo se ignoraba el caso para
        // evitar errores, pero la unidad se quedaba parada para siempre y dejaba
        // de aportar a la economía sin que nada lo indicara.
        if (!agente.isOnNavMesh)
        {
            RecolocarEnNavMesh();
            return;
        }

        // ¿Hemos llegado? Tres comprobaciones, basta con que se cumpla una:
        //  1) Cerca del FINAL DE SU RUTA. Es la clave: el recolector no va al centro
        //     del árbol sino a un punto caminable junto a él (hasta a 3 unidades).
        //     Medir contra el centro del árbol fallaba cuando ese punto caía en el
        //     lado opuesto: el agente se quedaba parado sin detectar su llegada.
        //  2) Cerca del objetivo en línea recta (robusto a la aceleración).
        //  3) La comprobación clásica del NavMesh.
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = destino.position;  b.y = 0f;

        bool cercaDelFinalDeRuta = false;
        if (agente.hasPath)
        {
            Vector3 fin = agente.pathEndPosition; fin.y = 0f;
            cercaDelFinalDeRuta = Vector3.Distance(a, fin) <= margenFinalRuta;
        }
        // El castillo es grande: se le aplica su propio radio de entrega
        float radio = (siguienteEstado == EstadoRecolector.Trabajando) ? radioLlegada : radioEntrega;
        bool cercaEnLineaRecta = Vector3.Distance(a, b) <= radio;
        bool llegadaNavMesh = !agente.pathPending && agente.remainingDistance <= agente.stoppingDistance;

        if (cercaDelFinalDeRuta || cercaEnLineaRecta || llegadaNavMesh)
        {
            ReiniciarMedidorAtasco();

            if (siguienteEstado == EstadoRecolector.Trabajando)
            {
                StartCoroutine(ProcesoRecoleccion());
            }
            else // Ha llegado a la base
            {
                EntregarRecursos();
            }
            return;
        }

        // No ha llegado: ¿está avanzando o se ha quedado bailando en el sitio?
        // (La llegada ya se ha comprobado arriba con margen, así que si seguimos
        //  aquí es que de verdad estamos lejos del objetivo.)
        if (EstaAtascado())
        {
            agente.ResetPath();
            ReiniciarMedidorAtasco();

            if (siguienteEstado == EstadoRecolector.Trabajando)
            {
                // El árbol/mina es inaccesible: descartamos y buscamos otro
                Debug.Log(gameObject.name + ": no puedo llegar al recurso, busco otro.");
                objetivoActual = null;
                estadoActual = EstadoRecolector.Buscando;
            }
            else
            {
                // No puede volver a la base: damos la entrega por hecha para no bloquearse
                Debug.Log(gameObject.name + ": no puedo volver a la base, reintento.");
                EntregarRecursos();
            }
        }
    }

    IEnumerator ProcesoRecoleccion()
    {
        estadoActual = EstadoRecolector.Trabajando;

        // La ventaja de dificultad también acelera la tala/minería
        yield return new WaitForSeconds(tiempoRecoleccion / bonoDificultad);

        // Si el recurso sigue teniendo reserva, cargamos y lo agotamos un poco
        if (recursoObjetivo != null && recursoObjetivo.EstaDisponible())
        {
            recursosLlevados = capacidadCarga;
            recursoObjetivo.SerExtraido(capacidadCarga);
        }
        else
        {
            recursosLlevados = 0;
        }

        // Si traemos algo, volvemos a la base; si el nodo estaba agotado, a buscar otro
        // La corrutina puede seguir viva tras un reinicio de episodio, cuando el
        // agente ya está destruido o fuera del NavMesh. Sin esta comprobación,
        // Unity lanza "SetDestination can only be called on an active agent".
        bool agenteUtil = agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh;

        if (recursosLlevados > 0 && miBase != null && agenteUtil)
        {
            NavegacionSegura.IrA(agente, PuntoDeEntrega());
            estadoActual = EstadoRecolector.VolviendoBase;
        }
        else
        {
            estadoActual = EstadoRecolector.Buscando;
        }
    }

    // Un punto caminable alrededor del castillo, distinto para cada viaje, para
    // repartir a los recolectores en lugar de amontonarlos en la misma puerta.
    Vector3 PuntoDeEntrega()
    {
        Vector3 centro = miBase.transform.position;
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 candidato = centro + new Vector3(dir.x, 0f, dir.y) * radioReparto;

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(candidato, out hit, radioReparto, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;
        return centro; // respaldo: el comportamiento de antes
    }

    void EntregarRecursos()
    {
        entregasPeriodo++;
        // Entregamos SIEMPRE a nuestra base asignada (no a la primera que pillemos).
        if (miBase != null && recursosLlevados > 0)
        {
            miBase.RecibirRecurso(tipoRecurso.ToString(), recursosLlevados);
            Debug.Log($"{gameObject.name} entrega {recursosLlevados} de {tipoRecurso} a {miBase.gameObject.name}. " +
                      $"(M:{miBase.madera} P:{miBase.piedra} O:{miBase.oro})");
        }

        recursosLlevados = 0;
        estadoActual = EstadoRecolector.Buscando;
    }
}