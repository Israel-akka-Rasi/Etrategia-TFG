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

    private float temporizador;
    private Recurso recursoObjetivo;
    private NavMeshAgent agente;
    private Tropa miTropa;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Update()
    {
        if (recursoObjetivo == null) return;

        // Si el nodo se ha agotado, dejamos de trabajarlo
        if (!recursoObjetivo.EstaDisponible())
        {
            DetenerTrabajo();
            return;
        }

        float distancia = Vector3.Distance(transform.position, recursoObjetivo.transform.position);

        if (distancia <= distanciaDeTrabajo)
        {
            agente.isStopped = true;
            temporizador += Time.deltaTime;

            if (temporizador >= tiempoEntreRecoleccion)
            {
                EjecutarTrabajo();
                temporizador = 0;
            }
        }
        else
        {
            agente.isStopped = false;
            agente.SetDestination(recursoObjetivo.transform.position);
        }
    }

    void EjecutarTrabajo()
    {
        if (recursoObjetivo == null || !recursoObjetivo.EstaDisponible()) return;

        Debug.Log("¡" + gameObject.name + " está talando! + " + recursoObjetivo.cantidadPorTanda + " de " + recursoObjetivo.tipo);

        // Buscamos nuestra propia base y le entregamos
        BaseMilitar[] todasLasBases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in todasLasBases)
        {
            if (b.equipo == miTropa.equipo)
            {
                b.RecibirRecurso(recursoObjetivo.tipo.ToString(), recursoObjetivo.cantidadPorTanda);
                recursoObjetivo.SerExtraido(recursoObjetivo.cantidadPorTanda);
                break;
            }
        }
    }

    public void OrdenarRecoleccion(Recurso nuevoRecurso)
    {
        // Si nos mandan a trabajar, cancelamos cualquier pelea
        Combate scriptCombate = GetComponent<Combate>();
        if (scriptCombate != null) scriptCombate.CancelarAtaque();

        recursoObjetivo = nuevoRecurso;
    }

    public void DetenerTrabajo()
    {
        recursoObjetivo = null;
    }
}