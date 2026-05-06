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
    private Transform objetivo; // Ahora guardamos un 'Transform' (puede ser tropa o castillo)
    private NavMeshAgent agente;
    private Tropa miTropa;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Update()
    {
        if (objetivo != null)
        {
            // Si el objetivo es destruido (desaparece de la jerarquía), limpiamos
            if (!objetivo.gameObject.activeInHierarchy)
            {
                objetivo = null;
                return;
            }

            // 1. Fase de Persecución y Ataque
            float distancia = Vector3.Distance(transform.position, objetivo.position);

            if (distancia <= rangoAtaque)
            {
                agente.isStopped = true; // Nos detenemos para dar el golpe
                temporizador += Time.deltaTime;

                if (temporizador >= tiempoEntreAtaques)
                {
                    DarGolpe();
                    temporizador = 0f;
                }
            }
            else
            {
                agente.isStopped = false;
                agente.SetDestination(objetivo.position); // Perseguimos
            }
        }
        else
        {
            // 2. Sistema de Radar (Aggro): Si no hay órdenes, buscamos pelea
            BuscarEnemigosCercanos();
        }
    }

    void DarGolpe()
    {
        // CASO A: ¿El objetivo es una Tropa?
        Tropa tropaEnemiga = objetivo.GetComponent<Tropa>();
        if (tropaEnemiga != null)
        {
            tropaEnemiga.RecibirDano(dano, miTropa);
            return; // Salimos de la función
        }

        // CASO B: ¿El objetivo es un Castillo?
        BaseMilitar baseEnemiga = objetivo.GetComponent<BaseMilitar>();
        if (baseEnemiga != null)
        {
            baseEnemiga.RecibirDano(dano);
            
            // ¡NUEVO DEBUG DE ASEDIO!
            Debug.Log("🔥 ¡" + gameObject.name + " está asediando la " + baseEnemiga.gameObject.name + "! (-" + dano + " HP)");
        }
    }

    void BuscarEnemigosCercanos()
    {
        Collider[] detectados = Physics.OverlapSphere(transform.position, radioVision);
        foreach (Collider col in detectados)
        {
            // Prioridad 1: Atacar a soldados enemigos que pasen cerca
            Tropa t = col.GetComponent<Tropa>();
            if (t != null && t.equipo != miTropa.equipo)
            {
                OrdenarAtaque(t.transform);
                return;
            }

            // Prioridad 2: Si no hay soldados, pero hay un castillo enemigo cerca, ¡A por él!
            BaseMilitar b = col.GetComponent<BaseMilitar>();
            if (b != null && b.equipo != miTropa.equipo)
            {
                OrdenarAtaque(b.transform);
                return;
            }
        }
    }

    // El jugador o el radar nos mandan un objetivo nuevo
    public void OrdenarAtaque(Transform nuevoObjetivo)
    {
        objetivo = nuevoObjetivo;
        temporizador = tiempoEntreAtaques; // Al llegar, damos el primer golpe inmediatamente
    }

    // Sobrecarga de seguridad para que tu script Tropa.cs no dé errores
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
        objetivo = null; // Olvidamos a quién estábamos persiguiendo
    }
}