using UnityEngine;
using UnityEngine.AI;

public class Recolector : MonoBehaviour
{
    [Header("Permisos")]
    public bool puedeMadera;
    public bool puedePiedra;
    public bool puedeOro;

    [Header("Configuración")]
    public float tiempoEntreRecoleccion = 1.5f;
    public float distanciaDeTrabajo = 3.5f;
    public float distanciaEntrega = 3.5f;

    [Header("Carga")]
    // Recoge hasta llenar la mochila y entonces la lleva al castillo.
    // Mismo modelo que usa la IA: así ambos bandos rinden igual.
    public int capacidadCarga = 10;

    [Header("Indicador Visual de Carga")]
    // Objeto que se enciende cuando la tropa lleva recursos encima.
    // Arrastra aquí un icono/saco colocado sobre la cabeza de la unidad.
    public GameObject indicadorCarga;

    [Header("Automatismo")]
    // Al nacer, buscar trabajo por su cuenta. DESACTIVADO por defecto: el jugador
    // decide a qué recurso manda cada unidad. El agente entrenado no lo necesita
    // porque dispone de la acción "Asignar recolectores" para los que estén ociosos.
    public bool trabajarAlNacer = false;
    // Al agotarse el nodo, busca solo el siguiente más cercano que pueda trabajar.
    public bool buscarOtroAlAgotarse = true;
    // Radio para la búsqueda AUTOMÁTICA (al agotarse el nodo actual): limitado a
    // propósito, para que no crucen el mapa entero por su cuenta.
    public float radioBusquedaNodo = 40f;

    private float temporizador;
    private Recurso recursoObjetivo;
    private NavMeshAgent agente;
    private Tropa miTropa;
    private BaseMilitar miCastillo;

    private int cargaActual = 0;
    private bool entregando = false;              // true = volviendo al castillo
    private Recurso.TipoRecurso tipoTransportado;

    void Awake()
    {
        // En Awake y no en Start: así las referencias existen aunque le den
        // una orden en el mismo frame en que nace la unidad.
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Start()
    {
        ActualizarIndicador();
        if (trabajarAlNacer) BuscarTrabajo();
    }

    // Se pone a trabajar en el nodo disponible más cercano que sepa explotar.
    // Es una ORDEN EXPRESA, así que busca por TODO el mapa: si limitáramos la
    // distancia, un minero cuya mina está en la otra punta se quedaría parado.
    // Devuelve true si ha encontrado algo que hacer.
    public bool BuscarTrabajo()
    {
        if (recursoObjetivo != null || entregando) return true; // ya está ocupado

        Recurso destino = null;
        if (puedeMadera) destino = BuscarNodoMasCercano(Recurso.TipoRecurso.Madera, Mathf.Infinity);
        if (destino == null && puedePiedra) destino = BuscarNodoMasCercano(Recurso.TipoRecurso.Piedra, Mathf.Infinity);
        if (destino == null && puedeOro) destino = BuscarNodoMasCercano(Recurso.TipoRecurso.Oro, Mathf.Infinity);

        if (destino == null)
        {
            Debug.Log($"{gameObject.name}: no encuentro ningún nodo disponible de mi tipo en todo el mapa.");
            return false;
        }

        OrdenarRecoleccion(destino);
        return true;
    }

    // ¿Está parado sin hacer nada?
    public bool EstaOcioso()
    {
        return recursoObjetivo == null && !entregando;
    }

    // Localiza (y cachea) el castillo de nuestro equipo
    BaseMilitar MiCastillo()
    {
        if (miCastillo != null) return miCastillo;
        if (miTropa == null) return null;

        // Solo el castillo de MI partida: con varias áreas entrenando a la vez,
        // una búsqueda global podría devolvernos el castillo de otra partida.
        AreaPartida area = miTropa.MiArea();
        if (area != null) miCastillo = area.BaseDeEquipo(miTropa.equipo);

        return miCastillo;
    }

    void Update()
    {
        // --- VIAJE DE VUELTA: llevamos la carga al castillo ---
        if (entregando)
        {
            GestionarEntrega();
            return;
        }

        if (recursoObjetivo == null) return;

        // Nodo agotado: si llevamos algo, lo entregamos; si no, buscamos otro
        if (!recursoObjetivo.EstaDisponible())
        {
            if (cargaActual > 0)
            {
                IniciarEntrega();
                return;
            }

            Recurso siguiente = buscarOtroAlAgotarse ? BuscarNodoMasCercano(recursoObjetivo.tipo) : null;
            if (siguiente != null)
            {
                recursoObjetivo = siguiente;
                temporizador = 0f;
                Debug.Log(gameObject.name + ": nodo agotado, voy al siguiente.");
            }
            else
            {
                DetenerTrabajo();
            }
            return;
        }

        // --- EXTRACCIÓN ---
        float distancia = Vector3.Distance(transform.position, recursoObjetivo.transform.position);

        if (distancia <= distanciaDeTrabajo)
        {
            agente.isStopped = true;
            temporizador += Time.deltaTime;

            if (temporizador >= tiempoEntreRecoleccion)
            {
                Extraer();
                temporizador = 0f;
            }
        }
        else
        {
            agente.isStopped = false;
            agente.SetDestination(recursoObjetivo.transform.position);
        }
    }

    // Saca una tanda del nodo y la mete en la mochila
    void Extraer()
    {
        if (recursoObjetivo == null || !recursoObjetivo.EstaDisponible()) return;

        int cantidad = recursoObjetivo.cantidadPorTanda;
        int hueco = capacidadCarga - cargaActual;
        if (cantidad > hueco) cantidad = hueco;
        if (cantidad <= 0) { IniciarEntrega(); return; }

        recursoObjetivo.SerExtraido(cantidad);

        tipoTransportado = recursoObjetivo.tipo;
        cargaActual += cantidad;
        ActualizarIndicador();

        // Mochila llena: a llevarla al castillo
        if (cargaActual >= capacidadCarga)
        {
            IniciarEntrega();
        }
    }

    void IniciarEntrega()
    {
        BaseMilitar castillo = MiCastillo();
        if (castillo == null) return;

        entregando = true;
        temporizador = 0f;

        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = false;
            agente.SetDestination(castillo.transform.position);
        }
    }

    void GestionarEntrega()
    {
        BaseMilitar castillo = MiCastillo();
        if (castillo == null) { entregando = false; return; }

        float distancia = Vector3.Distance(transform.position, castillo.transform.position);

        if (distancia > distanciaEntrega)
        {
            agente.isStopped = false;
            agente.SetDestination(castillo.transform.position);
            return;
        }

        // --- ENTREGA ---
        castillo.RecibirRecurso(tipoTransportado.ToString(), cargaActual);
        Debug.Log($"{gameObject.name} entrega {cargaActual} de {tipoTransportado} al castillo.");

        cargaActual = 0;
        entregando = false;
        ActualizarIndicador();

        // Volvemos al tajo: al mismo nodo si sigue vivo, o al más cercano
        if (recursoObjetivo == null || !recursoObjetivo.EstaDisponible())
        {
            recursoObjetivo = buscarOtroAlAgotarse ? BuscarNodoMasCercano(tipoTransportado) : null;
        }

        if (recursoObjetivo != null && agente.isOnNavMesh)
        {
            agente.isStopped = false;
            agente.SetDestination(recursoObjetivo.transform.position);
        }
    }

    // Enciende o apaga el icono de "llevo recursos"
    void ActualizarIndicador()
    {
        if (indicadorCarga != null)
        {
            indicadorCarga.SetActive(cargaActual > 0);
        }
    }

    // Busca el nodo disponible más cercano del mismo tipo que este recolector pueda trabajar
    Recurso BuscarNodoMasCercano(Recurso.TipoRecurso tipo)
    {
        return BuscarNodoMasCercano(tipo, radioBusquedaNodo);
    }

    Recurso BuscarNodoMasCercano(Recurso.TipoRecurso tipo, float radio)
    {
        if (!PuedeTrabajar(tipo)) return null;

        // Solo nodos de MI partida, disponibles y dentro del radio de búsqueda
        AreaPartida area = (miTropa != null) ? miTropa.MiArea() : AreaPartida.Resolver(this);
        if (area == null) return null;

        return area.RecursoMasCercano(tipo, transform.position, radio);
    }

    // ¿Tiene herramientas para este tipo de recurso?
    public bool PuedeTrabajar(Recurso.TipoRecurso tipo)
    {
        if (tipo == Recurso.TipoRecurso.Madera) return puedeMadera;
        if (tipo == Recurso.TipoRecurso.Piedra) return puedePiedra;
        if (tipo == Recurso.TipoRecurso.Oro) return puedeOro;
        return false;
    }

    public void OrdenarRecoleccion(Recurso nuevoRecurso)
    {
        // Si nos mandan a trabajar, cancelamos cualquier pelea
        Combate scriptCombate = GetComponent<Combate>();
        if (scriptCombate != null) scriptCombate.CancelarAtaque();

        recursoObjetivo = nuevoRecurso;
        entregando = false; // el jugador manda: vamos al nodo, ya entregaremos
        temporizador = 0f;
    }

    public void DetenerTrabajo()
    {
        recursoObjetivo = null;
        entregando = false;
        temporizador = 0f;

        // ¡CLAVE! Mientras trabajaba le pusimos el freno de mano (isStopped = true).
        // Si no lo soltamos aquí, la unidad se queda clavada y no obedece
        // ninguna orden de movimiento posterior.
        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = false;
        }
        // Ojo: la carga NO se pierde. Sigue en la mochila (y el icono encendido).
    }

    // Ocupado recolectando o entregando: Tropa no debe interferir en su movimiento
    public bool TieneRecurso()
    {
        return recursoObjetivo != null || entregando;
    }

    // Para la interfaz: cuánto lleva encima
    public int CargaActual() { return cargaActual; }
}