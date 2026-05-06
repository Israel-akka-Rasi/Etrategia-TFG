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
    private Transform objetivo; 
    private NavMeshAgent agente;
    private Tropa miTropa;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Update()
    {
        // --- 1. EL SEGURO ANTIBUGS DEFINITIVO ---
        // Si no tenemos objetivo (o el objetivo acaba de morir y Unity lo ha destruido)
        if (objetivo == null)
        {
            // ¡Quitamos el freno de mano si estaba puesto!
            if (agente != null && agente.isOnNavMesh && agente.isStopped)
            {
                agente.isStopped = false; 
            }
            
            // Como estamos libres, activamos el radar para buscar pelea
            BuscarEnemigosCercanos();
            return; // Cortamos aquí para que no ejecute el resto
        }

        // Si el objetivo existe pero ha sido ocultado/desactivado por algún motivo
        if (!objetivo.gameObject.activeInHierarchy)
        {
            CancelarAtaque();
            return;
        }

        // --- 2. LÓGICA DE COMBATE ---
        float distancia = Vector3.Distance(transform.position, objetivo.position);

        if (distancia <= rangoAtaque)
        {
            agente.isStopped = true; // Ponemos el freno de mano para golpear firmemente
            temporizador += Time.deltaTime;

            if (temporizador >= tiempoEntreAtaques)
            {
                DarGolpe();
                temporizador = 0f;
            }
        }
        else
        {
            agente.isStopped = false; // Quitamos el freno para poder perseguir
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

    public void OrdenarAtaque(Transform nuevoObjetivo)
    {
        objetivo = nuevoObjetivo;
        temporizador = tiempoEntreAtaques; // Así el primer golpe lo da nada más llegar
    }

    public void OrdenarAtaque(Tropa tropaObjetivo)
    {
        OrdenarAtaque(tropaObjetivo.transform);
    }

    public bool TieneObjetivo()
    {
        return objetivo != null;
    }
    
    public void CancelarAtaque()
    {
        objetivo = null;
        
        // Al cancelar, aseguramos que el motor vuelva a encenderse
        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = false; 
        }
    }
}