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
    // Al agotarse el nodo, busca solo el siguiente más cercano que pueda trabajar.
    public bool buscarOtroAlAgotarse = true;
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
    }

    // Localiza (y cachea) el castillo de nuestro equipo
    BaseMilitar MiCastillo()
    {
        if (miCastillo != null) return miCastillo;
        if (miTropa == null) return null;

        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == miTropa.equipo) { miCastillo = b; break; }
        }
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
        if (!PuedeTrabajar(tipo)) return null;

        Recurso[] todos = FindObjectsOfType<Recurso>();
        Recurso mejor = null;
        float distanciaMinima = radioBusquedaNodo;

        foreach (Recurso r in todos)
        {
            if (r == null || r.tipo != tipo || !r.EstaDisponible()) continue;

            float d = Vector3.Distance(transform.position, r.transform.position);
            if (d < distanciaMinima)
            {
                distanciaMinima = d;
                mejor = r;
            }
        }
        return mejor;
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