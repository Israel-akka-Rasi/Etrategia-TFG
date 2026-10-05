using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Tipo de unidad. Se asigna UNA VEZ en cada prefab y evita tener que deducirlo
// de sus estadísticas (que es frágil: al reequilibrar números, las cuentas del
// entorno de RL empezarían a contar mal sin dar ningún error).
public enum TipoTropa
{
    Lenador,
    Minero,
    Tesorero,
    Caballero,
    Arquero,
    Gladiador
}

public class Tropa : MonoBehaviour
{
    [Header("Identidad")]
    public int equipo;
    public TipoTropa tipo = TipoTropa.Caballero; // ¡asignar en cada prefab!
    public int salud = 100;   // Caballero (línea base). Ver tabla de stats por tipo.
    public float saludMaxima; // Para calcular el porcentaje de la barra

    [Header("Visuales")]
    public GameObject indicadorSeleccion;
    public Image barraSalud;

    [Header("Interfaz")]
    public Sprite retratoTropa;

    private NavMeshAgent agente;

    // Castillo al que "pertenece" esta unidad (para devolverle el hueco de población)
    private BaseMilitar miCastillo;

    // Lo que costó reclutar esta unidad. Lo rellena quien la crea (castillo o
    // edificio) y sirve para devolver una parte al licenciarla.
    [System.NonSerialized] public int costeMaderaPagado;
    [System.NonSerialized] public int costePiedraPagado;
    [System.NonSerialized] public int costeOroPagado;

    // ¿Es una unidad de economía?
    public bool EsRecolector()
    {
        return tipo == TipoTropa.Lenador || tipo == TipoTropa.Minero || tipo == TipoTropa.Tesorero;
    }

    // Velocidad propia del prefab. Al moverse en grupo se baja a la del más lento
    // para que el ejército llegue junto, y se restaura al terminar el movimiento.
    private float velocidadOriginal;

    // Referencias a los otros sistemas para no interferir con ellos
    private Combate combate;
    private Recolector recolector;
    private RecolectorIA recolectorIA;

    // Medidor de progreso para detectar oscilación/atasco en movimientos libres
    private float remanenteAnterior = Mathf.Infinity;
    private float tiempoSinAvanzar = 0f;

    // Área (partida) a la que pertenece esta unidad
    private AreaPartida miArea;
    public AreaPartida MiArea()
    {
        if (miArea == null) miArea = AreaPartida.Resolver(this);
        return miArea;
    }

    // Corrige el área si se registró antes de tener padre (ver AreaPartida.Adoptar)
    public void CambiarArea(AreaPartida nueva)
    {
        if (nueva == null || nueva == miArea) return;
        if (miArea != null) miArea.Desregistrar(this);
        miArea = nueva;
        miArea.Registrar(this);
    }

    void Awake()
    {
        MiArea().Registrar(this);

        agente = GetComponent<NavMeshAgent>();
        saludMaxima = salud;

        combate = GetComponent<Combate>();
        recolector = GetComponent<Recolector>();
        recolectorIA = GetComponent<RecolectorIA>();

        if (agente != null) velocidadOriginal = agente.speed;
    }

    void Start()
    {
        // El castillo de nuestro equipo DENTRO de nuestra partida, para devolverle
        // el hueco de población al morir.
        AreaPartida area = MiArea();
        if (area != null) miCastillo = area.BaseDeEquipo(equipo);
    }

    // Al morir (o ser destruida por cualquier motivo) devolvemos el hueco de población.
    // Sin esto el contador solo sube y, al tocar techo, no se puede reclutar más
    // aunque te hayan matado media tropa.
    void OnDestroy()
    {
        if (miArea != null) miArea.Desregistrar(this);

        if (!Application.isPlaying) return;

        // Durante el reinicio de episodio no se descuenta: las bases ya han
        // restaurado su población inicial y descontar aquí la dejaría en negativo.
        // Durante el reinicio de NUESTRA partida no se descuenta población
        GestorPartida gestor = (miArea != null) ? miArea.Gestor() : null;
        if (gestor != null && gestor.EstaReiniciando) return;

        if (miCastillo != null) miCastillo.LiberarPoblacion();
    }

    void Update()
    {
        GestionarLlegada();
    }

    // --- MARCHA EN GRUPO ---
    public float VelocidadOriginal()
    {
        return velocidadOriginal;
    }

    // Marchar al ritmo del grupo (la del miembro más lento)
    public void FijarVelocidadGrupo(float velocidad)
    {
        if (agente != null) agente.speed = velocidad;
    }

    // Volver a su velocidad propia (al llegar o al recibir una orden individual)
    public void RestaurarVelocidad()
    {
        if (agente != null && velocidadOriginal > 0f) agente.speed = velocidadOriginal;
    }

    // Orden de movimiento "libre" (la da el jugador, el agente, la IA o un spawn)
    public void OrdenarMovimiento(Vector3 destinoFinal)
    {
        // Combate guarda el destino: así, si se cruza con enemigos por el camino,
        // pelea con ellos y después retoma la marcha hasta aquí.
        if (combate != null) combate.FijarDestino(destinoFinal);

        bool aceptada = NavegacionSegura.IrA(agente, destinoFinal);
        ReiniciarMedidor();

        // DEPURACIÓN: una orden rechazada se perdía en silencio
        if (!aceptada)
        {
            string motivo = (agente == null) ? "sin NavMeshAgent"
                          : !agente.isActiveAndEnabled ? "NavMeshAgent desactivado"
                          : !agente.isOnNavMesh ? "fuera de la malla de navegación"
                          : "destino no válido";
            DepuradorTropas.Anotar($"RECHAZADA {name}: orden de mover no aceptada ({motivo}).");
        }
    }

    // ====================================================================
    // GESTIÓN DE LLEGADA Y ANTI-OSCILACIÓN
    // Cuando la unidad va a un punto por una orden de movimiento, al llegar
    // (o si se queda oscilando sin avanzar en un hueco estrecho) paramos el
    // agente, para que no forcejee de lado a lado indefinidamente.
    // No actuamos cuando otro sistema controla a la unidad.
    // ====================================================================
    void GestionarLlegada()
    {
        if (agente == null || !agente.isOnNavMesh) return;

        // Si la unidad la controla otro sistema, no nos metemos:
        if (recolectorIA != null) return;                            // recolector autónomo de la IA
        if (combate != null && combate.TieneObjetivo()) return;      // está combatiendo
        if (recolector != null && recolector.TieneRecurso()) return; // está recolectando

        // Sin camino activo no hay nada que vigilar
        if (agente.isStopped || agente.pathPending || !agente.hasPath)
        {
            ReiniciarMedidor();
            return;
        }

        // ¿Ha llegado prácticamente al destino? -> paramos para no oscilar encima
        if (agente.remainingDistance <= agente.stoppingDistance + 0.15f)
        {
            agente.ResetPath();
            RestaurarVelocidad(); // fin de la marcha: recupera su ritmo propio
            ReiniciarMedidor();
            return;
        }

        // ¿Sigue acercándose al destino?
        if (agente.remainingDistance < remanenteAnterior - 0.05f)
        {
            remanenteAnterior = agente.remainingDistance; // sí avanza, todo bien
            tiempoSinAvanzar = 0f;
        }
        else
        {
            // No reduce distancia: está oscilando o atascado. Si persiste, lo soltamos.
            tiempoSinAvanzar += Time.deltaTime;
            if (tiempoSinAvanzar > 1f)
            {
                agente.ResetPath();
                RestaurarVelocidad();
                ReiniciarMedidor();
            }
        }
    }

    void ReiniciarMedidor()
    {
        remanenteAnterior = Mathf.Infinity;
        tiempoSinAvanzar = 0f;
    }

    public void ActivarSeleccion(bool estado)
    {
        if (indicadorSeleccion != null)
        {
            indicadorSeleccion.SetActive(estado);
        }
    }

    // Recibimos la cantidad de daño y quién nos lo ha hecho
    public void RecibirDano(int cantidad, Tropa atacante)
    {
        salud -= cantidad;

        if (barraSalud != null)
        {
            barraSalud.fillAmount = salud / saludMaxima;
        }

        Debug.Log("🩸 " + gameObject.name + " recibe daño. Salud: " + salud);

        if (salud <= 0)
        {
            Debug.Log("💀 " + gameObject.name + " ha caído en combate.");
            Destroy(gameObject);
        }
        else if (atacante != null) // Seguimos vivos y alguien nos pegó
        {
            // Instinto de supervivencia: si no estábamos atacando a nadie, contraatacamos.
            // Combate decide si responde: no lo hace si se está retirando por una
            // orden de mover, y no borra las órdenes que tuviera la unidad.
            if (combate != null && !combate.TieneObjetivo())
            {
                combate.OrdenarAtaque(atacante);
            }
        }
    }
}