using UnityEngine;
using UnityEngine.AI;

public class Combate : MonoBehaviour
{
    [Header("Estadísticas")]
    public int dano = 12;              // Caballero (línea base)
    public float rangoAtaque = 1.5f;   // 1.5 cuerpo a cuerpo | ~7 a distancia
    public float tiempoEntreAtaques = 1f;

    [Header("Inteligencia Artificial")]
    public float radioVision = 8f;     // SIEMPRE mayor que rangoAtaque

    private float temporizador;
    private Transform objetivo;        // Objetivo INMEDIATO (a quién golpeo ahora mismo)
    private Transform objetivoFinal;   // Objetivo FINAL del asedio (el castillo). Si es null, no hay asedio.
    private NavMeshAgent agente;
    private Tropa miTropa;

    [Header("Optimización de ruta")]
    public float intervaloRecalculo = 0.25f; // Cada cuánto, como mucho, se recalcula la ruta al perseguir
    public float umbralMovimientoObjetivo = 1f; // Si el objetivo se mueve más de esto, recalculamos ya

    private float temporizadorRuta;
    private Vector3 ultimoDestino;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Update()
    {
        // --- 1. SIN OBJETIVO INMEDIATO ---
        if (objetivo == null)
        {
            if (agente != null && agente.isOnNavMesh && agente.isStopped)
            {
                agente.isStopped = false;
            }

            if (objetivoFinal != null)
            {
                // Estamos en ASEDIO. Antes de seguir hacia el castillo, miramos si
                // hay una tropa enemiga cerca que limpiar. Si no, retomamos la marcha.
                Transform intruso = BuscarTropaEnemigaCercana();
                objetivo = (intruso != null) ? intruso : objetivoFinal;
            }
            else
            {
                // No hay asedio: estamos ociosos, así que defendemos buscando pelea cercana.
                BuscarEnemigosCercanos();
            }
            return;
        }

        // --- 2. EL OBJETIVO DESAPARECIÓ (desactivado, no destruido) ---
        if (!objetivo.gameObject.activeInHierarchy)
        {
            objetivo = null; // Dejamos que el bloque de arriba reanude/intercepte el próximo frame
            if (agente != null && agente.isOnNavMesh) agente.isStopped = false;
            return;
        }

        // --- 3. INTERCEPCIÓN DURANTE EL ASEDIO ---
        // Si vamos directos al castillo (objetivo == objetivoFinal) y aparece una tropa
        // enemiga dentro de la vista, la atacamos primero para asegurar la zona.
        if (objetivoFinal != null && objetivo == objetivoFinal)
        {
            Transform intruso = BuscarTropaEnemigaCercana();
            if (intruso != null) objetivo = intruso;
        }

        // --- 4. LÓGICA DE COMBATE ---
        float distancia = Vector3.Distance(transform.position, objetivo.position);

        if (distancia <= rangoAtaque)
        {
            agente.isStopped = true; // Freno de mano para golpear firme
            temporizador += Time.deltaTime;

            if (temporizador >= tiempoEntreAtaques)
            {
                DarGolpe();
                temporizador = 0f;
            }
        }
        else
        {
            agente.isStopped = false; // Soltamos el freno para perseguir

            // En vez de recalcular la ruta cada frame (caro y genera microtemblor),
            // lo hacemos solo de vez en cuando, o si el objetivo se ha movido bastante.
            temporizadorRuta += Time.deltaTime;

            bool objetivoSeMovio = Vector3.Distance(objetivo.position, ultimoDestino) > umbralMovimientoObjetivo;
            bool tocaRecalcular = temporizadorRuta >= intervaloRecalculo;

            if (objetivoSeMovio || tocaRecalcular)
            {
                agente.SetDestination(objetivo.position);
                ultimoDestino = objetivo.position;
                temporizadorRuta = 0f;
            }
        }
    }

    void DarGolpe()
    {
        Tropa tropaEnemiga = objetivo.GetComponent<Tropa>();
        if (tropaEnemiga != null)
        {
            tropaEnemiga.RecibirDano(dano, miTropa);
            return;
        }

        BaseMilitar baseEnemiga = objetivo.GetComponent<BaseMilitar>();
        if (baseEnemiga != null)
        {
            baseEnemiga.RecibirDano(dano);
            Debug.Log("🔥 ¡" + gameObject.name + " está asediando la " + baseEnemiga.gameObject.name + "! (-" + dano + " HP)");
        }
    }

    // Busca cualquier enemigo (tropa o base) y lo fija. Se usa para la defensa cuando estamos ociosos.
    void BuscarEnemigosCercanos()
    {
        Collider[] detectados = Physics.OverlapSphere(transform.position, radioVision);
        foreach (Collider col in detectados)
        {
            Tropa t = col.GetComponent<Tropa>();
            if (t != null && t.equipo != miTropa.equipo)
            {
                OrdenarAtaque(t.transform);
                return;
            }

            BaseMilitar b = col.GetComponent<BaseMilitar>();
            if (b != null && b.equipo != miTropa.equipo)
            {
                OrdenarAtaque(b.transform);
                return;
            }
        }
    }

    // Devuelve SOLO la tropa enemiga más cercana dentro de la vista (o null). Para interceptar en el asedio.
    Transform BuscarTropaEnemigaCercana()
    {
        Collider[] detectados = Physics.OverlapSphere(transform.position, radioVision);
        Transform masCercana = null;
        float distanciaMinima = Mathf.Infinity;

        foreach (Collider col in detectados)
        {
            Tropa t = col.GetComponent<Tropa>();
            if (t != null && t.equipo != miTropa.equipo)
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < distanciaMinima)
                {
                    distanciaMinima = d;
                    masCercana = t.transform;
                }
            }
        }

        return masCercana;
    }

    // --- ATAQUE PRECISO (clic del jugador o contraataque): ataca SOLO a ese objetivo ---
    public void OrdenarAtaque(Transform nuevoObjetivo)
    {
        objetivo = nuevoObjetivo;
        objetivoFinal = null; // sin asedio: no intercepta a terceros
        temporizador = tiempoEntreAtaques; // primer golpe nada más llegar
        temporizadorRuta = intervaloRecalculo; // recalcular la ruta ya, en el primer frame
    }

    public void OrdenarAtaque(Tropa tropaObjetivo)
    {
        OrdenarAtaque(tropaObjetivo.transform);
    }

    // --- ASEDIO (la IA): va directo al castillo pero interceptando enemigos por el camino ---
    public void OrdenarAsedio(Transform castilloEnemigo)
    {
        objetivoFinal = castilloEnemigo;
        objetivo = castilloEnemigo;
        temporizador = tiempoEntreAtaques;
        temporizadorRuta = intervaloRecalculo; // recalcular la ruta ya, en el primer frame
    }

    public bool TieneObjetivo()
    {
        return objetivo != null;
    }

    public bool EstaAsediando()
    {
        return objetivoFinal != null;
    }

    public void CancelarAtaque()
    {
        objetivo = null;
        objetivoFinal = null;

        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = false;
        }
    }
}