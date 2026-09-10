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

    [Header("Ventaja de la IA")]
    public float bonoDificultad = 1f; // 1.2 = 20% más rápido

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
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        float distanciaMinima = Mathf.Infinity;

        foreach (BaseMilitar b in bases)
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

    void Update()
    {
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
        Recurso[] recursos = FindObjectsOfType<Recurso>();
        float distanciaMinima = Mathf.Infinity;
        Recurso elegido = null;

        foreach (Recurso rec in recursos)
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
                agente.SetDestination(hit.position);
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

        if (!agente.pathPending && agente.remainingDistance <= agente.stoppingDistance)
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
        if (recursosLlevados > 0 && miBase != null)
        {
            agente.SetDestination(miBase.transform.position);
            estadoActual = EstadoRecolector.VolviendoBase;
        }
        else
        {
            estadoActual = EstadoRecolector.Buscando;
        }
    }

    void EntregarRecursos()
    {
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