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

        float distancia = Vector3.Distance(transform.position, recursoObjetivo.transform.position);

        // USAMOS LA NUEVA VARIABLE DE DISTANCIA
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
        if (recursoObjetivo == null) return;

        Debug.Log("¡" + gameObject.name + " está talando! + " + recursoObjetivo.cantidadPorTanda + " de " + recursoObjetivo.tipo);

        // --- SOLUCIÓN: Buscar TODAS las bases y quedarnos con la nuestra ---
        BaseMilitar[] todasLasBases = FindObjectsOfType<BaseMilitar>(); 
        foreach (BaseMilitar b in todasLasBases)
        {
            if (b.equipo == miTropa.equipo)
            {
                b.RecibirRecurso(recursoObjetivo.tipo.ToString(), recursoObjetivo.cantidadPorTanda);
                recursoObjetivo.SerExtraido(recursoObjetivo.cantidadPorTanda);
                break; // Ya encontramos la nuestra, dejamos de buscar
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