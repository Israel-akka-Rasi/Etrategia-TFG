using UnityEngine;
using UnityEngine.AI;

public class Combate : MonoBehaviour
{
    [Header("Estadísticas")]
    public int dano = 1;
    public float rangoAtaque = 1.5f;
    public float tiempoEntreAtaques = 1f;

    [Header("Inteligencia Artificial")]
    public float radioVision = 5f;

    private float temporizador;
    private Transform objetivo;        // Objetivo INMEDIATO (a quién golpeo ahora mismo)
    private Transform objetivoFinal;   // Objetivo FINAL del asedio (el castillo). Si es null, no hay asedio.
    private NavMeshAgent agente;
    private Tropa miTropa;

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
            agente.SetDestination(objetivo.position);
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