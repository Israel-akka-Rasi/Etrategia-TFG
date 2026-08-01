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

    [Header("Automatismo")]
    // Al agotarse el nodo, busca solo el siguiente más cercano que pueda trabajar.
    // Sin esto habría que re-ordenarlo a mano cada vez que se acaba un árbol.
    public bool buscarOtroAlAgotarse = true;
    public float radioBusquedaNodo = 40f;

    private float temporizador;
    private Recurso recursoObjetivo;
    private NavMeshAgent agente;
    private Tropa miTropa;

    void Awake()
    {
        // En Awake y no en Start: así las referencias existen aunque le den
        // una orden en el mismo frame en que nace la unidad.
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Update()
    {
        if (recursoObjetivo == null) return;

        // Si el nodo se ha agotado, buscamos otro igual (o paramos si no hay)
        if (!recursoObjetivo.EstaDisponible())
        {
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
    }

    public void DetenerTrabajo()
    {
        recursoObjetivo = null;
        temporizador = 0f;

        // ¡CLAVE! Mientras talaba le pusimos el freno de mano (isStopped = true).
        // Si no lo soltamos aquí, la unidad se queda clavada y no obedece
        // ninguna orden de movimiento posterior.
        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = false;
        }
    }

    // ¿Está ahora mismo asignado a recolectar un nodo?
    public bool TieneRecurso()
    {
        return recursoObjetivo != null;
    }
}