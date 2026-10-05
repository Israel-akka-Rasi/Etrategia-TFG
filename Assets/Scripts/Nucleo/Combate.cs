using UnityEngine;
using UnityEngine.AI;

// ============================================================================
// COMBATE
// Reglas (en orden de prioridad), revisadas cada 'intervaloBusqueda':
//   0. Recién ordenado MOVER: durante 'segundosDesenganche' ignora a los
//      enemigos para poder salir de un combate. Luego sigue su camino.
//   1. Si ya tiene un enemigo a distancia de golpe, lo golpea. Si el que
//      perseguía no está a su alcance pero otro sí, pelea con el de al lado:
//      una unidad nunca se queda quieta con enemigos pegados.
//   2. Enemigo concreto ordenado (clic derecho, IA, agente) a la vista: a por él.
//   3. Cualquier otra tropa enemiga a la vista: la más cercana.
//   4. Enemigo ordenado fuera de la vista: lo persigue.
//   5. Castillo a asediar.
//   6. Ociosa (sin orden de mover): castillo enemigo a la vista.
//   Sin nada que atacar y con orden de mover, retoma la marcha.
// ============================================================================

public class Combate : MonoBehaviour
{
    [Header("Estadísticas")]
    public int dano = 12;              // Caballero (línea base)
    public float rangoAtaque = 1.5f;   // 1.5 cuerpo a cuerpo | ~7 a distancia
    public float tiempoEntreAtaques = 1f;

    [Header("Inteligencia Artificial")]
    public float radioVision = 8f;     // SIEMPRE mayor que rangoAtaque

    [Header("Selección de Objetivo")]
    public float intervaloBusqueda = 0.3f;     // cada cuánto se revisa a quién atacar
    public float margenCambioObjetivo = 1f;    // solo cambia si el nuevo está claramente más cerca
    public float segundosDesenganche = 2f;     // tras una orden de mover, ignora enemigos este tiempo

    [Header("Optimización de ruta")]
    public float intervaloRecalculo = 0.25f; // Cada cuánto, como mucho, se recalcula la ruta al perseguir
    public float umbralMovimientoObjetivo = 1f; // Si el objetivo se mueve más de esto, recalculamos ya

    private NavMeshAgent agente;
    private Tropa miTropa;

    // --- Órdenes recibidas ---
    private Transform objetivoOrdenado;  // enemigo concreto que me han mandado atacar
    private Transform objetivoFinal;     // castillo a asediar
    private bool hayDestino;             // orden de mover pendiente
    private Vector3 destino;
    private bool reanudarMarcha;         // retomar el camino al acabar una pelea
    private float ignorarEnemigosHasta;

    // --- Objetivo inmediato ---
    private Transform objetivo;
    private float radioObjetivo;         // radio del agente enemigo (para medir al borde)
    private bool teniaObjetivo;          // para detectar que el objetivo ha muerto

    private float temporizador;
    private float temporizadorBusqueda;
    private float temporizadorRuta;
    private float ultimoGolpe = -999f;   // para el depurador: ¿está golpeando de verdad?
    private Vector3 ultimoDestino;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        miTropa = GetComponent<Tropa>();
    }

    void Update()
    {
        LimpiarObjetivos();

        temporizadorBusqueda += Time.deltaTime;
        if (objetivo == null || temporizadorBusqueda >= intervaloBusqueda)
        {
            temporizadorBusqueda = 0f;
            ElegirObjetivo();
        }

        if (objetivo != null)
        {
            Pelear();
            return;
        }

        // --- Nada que atacar ---
        if (agente != null && agente.isOnNavMesh && agente.isStopped)
            NavegacionSegura.Frenar(agente, false);

        if (hayDestino)
        {
            if (reanudarMarcha)
            {
                NavegacionSegura.IrA(agente, destino, reanudar: true);
                reanudarMarcha = false;
            }
            else if (HaLlegado())
            {
                hayDestino = false;
            }
        }
    }

    // ========================================================================
    // ELECCIÓN DE OBJETIVO
    // ========================================================================
    void ElegirObjetivo()
    {
        if (miTropa == null) return;

        bool desenganchando = hayDestino && Time.time < ignorarEnemigosHasta;

        if (!desenganchando)
        {
            // 1. Enemigo pegado: nunca quedarse quieto con alguien al alcance
            if (objetivo != null && EsTropa(objetivo) && EnAlcance(objetivo, RadioDe(objetivo))) return;

            Transform alLado = TropaEnemigaMasCercana(true);
            if (alLado != null) { CambiarObjetivo(alLado); return; }

            // 2. Enemigo ordenado a la vista
            if (objetivoOrdenado != null && EnVision(objetivoOrdenado)) { CambiarObjetivo(objetivoOrdenado); return; }

            // 3. Tropa enemiga más cercana a la vista (sin saltar de una a otra por poco)
            Transform cercana = TropaEnemigaMasCercana(false);
            if (cercana != null)
            {
                bool actualVisible = objetivo != null && EsTropa(objetivo) && EnVision(objetivo);
                if (!actualVisible || Distancia(cercana) + margenCambioObjetivo < Distancia(objetivo))
                    CambiarObjetivo(cercana);
                return;
            }
        }

        // 4. Enemigo ordenado fuera de la vista: perseguirlo
        if (objetivoOrdenado != null && !desenganchando) { CambiarObjetivo(objetivoOrdenado); return; }

        // 5. Castillo a asediar
        if (objetivoFinal != null) { CambiarObjetivo(objetivoFinal); return; }

        // 6. Ociosa: defender atacando un castillo enemigo a la vista
        if (!hayDestino)
        {
            Transform baseCercana = BaseEnemigaALaVista();
            if (baseCercana != null) { CambiarObjetivo(baseCercana); return; }
        }

        CambiarObjetivo(null);
    }

    void CambiarObjetivo(Transform nuevo)
    {
        if (nuevo == objetivo) return;

        if (objetivo == null) temporizador = tiempoEntreAtaques; // primer golpe nada más llegar
        if (nuevo == null && hayDestino) reanudarMarcha = true;   // acabó la pelea: seguir el camino

        objetivo = nuevo;
        teniaObjetivo = nuevo != null;
        radioObjetivo = RadioDe(nuevo);
        temporizadorRuta = intervaloRecalculo; // recalcular la ruta ya
    }

    void LimpiarObjetivos()
    {
        if (objetivo != null && !objetivo.gameObject.activeInHierarchy) CambiarObjetivo(null);
        if (objetivoOrdenado != null && !objetivoOrdenado.gameObject.activeInHierarchy) objetivoOrdenado = null;

        // Objetivo destruido (Unity lo iguala a null): limpiamos y, si íbamos
        // de camino a algún sitio, retomamos la marcha.
        if (teniaObjetivo && objetivo == null)
        {
            objetivo = null;
            teniaObjetivo = false;
            radioObjetivo = 0f;
            if (hayDestino) reanudarMarcha = true;
        }
    }

    // ========================================================================
    // PELEA
    // ========================================================================
    void Pelear()
    {
        if (EnAlcance(objetivo, radioObjetivo))
        {
            NavegacionSegura.Frenar(agente, true); // Freno de mano para golpear firme
            temporizador += Time.deltaTime;

            if (temporizador >= tiempoEntreAtaques)
            {
                DarGolpe();
                temporizador = 0f;
            }
        }
        else
        {
            NavegacionSegura.Frenar(agente, false); // Soltamos el freno para perseguir

            temporizadorRuta += Time.deltaTime;
            bool objetivoSeMovio = Vector3.Distance(objetivo.position, ultimoDestino) > umbralMovimientoObjetivo;

            // Mientras se calcula la ruta anterior no pedimos otra: cada petición
            // nueva cancela la que está en curso y la unidad no llegaba a moverse.
            bool calculando = agente != null && agente.isOnNavMesh && agente.pathPending;

            if (!calculando && (objetivoSeMovio || temporizadorRuta >= intervaloRecalculo))
            {
                NavegacionSegura.IrA(agente, objetivo.position);
                ultimoDestino = objetivo.position;
                temporizadorRuta = 0f;
            }
        }
    }

    void DarGolpe()
    {
        ultimoGolpe = Time.time;
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

    // ========================================================================
    // AUXILIARES
    // ========================================================================
    bool EsTropa(Transform t) { return t != null && t.GetComponent<Tropa>() != null; }

    float Distancia(Transform t) { return Vector3.Distance(transform.position, t.position); }

    bool EnVision(Transform t) { return t != null && Distancia(t) <= radioVision; }

    // Se mide al BORDE de la otra unidad (su radio): en un grupo apelotonado los
    // cuerpos no dejan acercarse más y, midiendo al centro, se quedaban sin golpear.
    bool EnAlcance(Transform t, float radio) { return t != null && Distancia(t) <= rangoAtaque + radio; }

    float RadioDe(Transform t)
    {
        if (t == null) return 0f;
        if (t.GetComponent<Tropa>() == null) return 0f; // castillos: se mantiene la medida al centro
        NavMeshAgent a = t.GetComponent<NavMeshAgent>();
        return (a != null) ? a.radius : 0f;
    }

    // Tropa enemiga más cercana dentro de la vista (o solo las que están a distancia de golpe)
    Transform TropaEnemigaMasCercana(bool soloAlAlcance)
    {
        Collider[] detectados = Physics.OverlapSphere(transform.position, radioVision);
        Transform mejor = null;
        float minima = Mathf.Infinity;

        foreach (Collider col in detectados)
        {
            Tropa t = col.GetComponent<Tropa>();
            if (t == null || t.equipo == miTropa.equipo || !t.gameObject.activeInHierarchy) continue;
            if (soloAlAlcance && !EnAlcance(t.transform, RadioDe(t.transform))) continue;

            float d = Distancia(t.transform);
            if (d < minima) { minima = d; mejor = t.transform; }
        }
        return mejor;
    }

    Transform BaseEnemigaALaVista()
    {
        Collider[] detectados = Physics.OverlapSphere(transform.position, radioVision);
        foreach (Collider col in detectados)
        {
            BaseMilitar b = col.GetComponent<BaseMilitar>();
            if (b != null && b.equipo != miTropa.equipo) return b.transform;
        }
        return null;
    }

    bool HaLlegado()
    {
        if (agente == null || !agente.isOnNavMesh) return true;
        if (agente.pathPending) return false;
        if (!agente.hasPath) return true;
        return agente.remainingDistance <= agente.stoppingDistance + 0.3f;
    }

    // ========================================================================
    // ÓRDENES (misma interfaz que antes: Movimiento, IAEnemiga y EntornoRL no cambian)
    // ========================================================================

    // Atacar un objetivo concreto (clic derecho, IA, agente). Si es un castillo, se asedia.
    public void OrdenarAtaque(Transform nuevoObjetivo)
    {
        if (nuevoObjetivo == null) return;

        if (nuevoObjetivo.GetComponent<BaseMilitar>() != null)
        {
            OrdenarAsedio(nuevoObjetivo);
            return;
        }

        objetivoOrdenado = nuevoObjetivo;
        objetivoFinal = null;
        hayDestino = false;
        temporizadorBusqueda = intervaloBusqueda; // decidir ya
    }

    // Reacción al ser golpeado (la llama Tropa.RecibirDano). No borra las órdenes:
    // solo responde si en ese momento no estaba peleando con nadie.
    public void OrdenarAtaque(Tropa atacante)
    {
        if (atacante == null || objetivo != null) return;
        if (hayDestino && Time.time < ignorarEnemigosHasta) return; // retirándose
        CambiarObjetivo(atacante.transform);
    }

    // Asediar un castillo, luchando contra lo que salga al paso
    public void OrdenarAsedio(Transform castilloEnemigo)
    {
        objetivoFinal = castilloEnemigo;
        objetivoOrdenado = null;
        hayDestino = false;
        temporizadorBusqueda = intervaloBusqueda;
    }

    // Orden de MOVER. La llama Tropa.OrdenarMovimiento antes de fijar la ruta.
    public void FijarDestino(Vector3 nuevoDestino)
    {
        destino = nuevoDestino;
        hayDestino = true;
        reanudarMarcha = false;
        objetivoOrdenado = null;
        objetivoFinal = null;
        ignorarEnemigosHasta = Time.time + segundosDesenganche;

        objetivo = null;
        teniaObjetivo = false;
        radioObjetivo = 0f;
        if (agente != null) NavegacionSegura.Frenar(agente, false);
    }

    public bool TieneObjetivo() { return objetivo != null; }

    // ---- DEPURACIÓN: estado legible de la unidad (lo usa DepuradorTropas) ----
    public float SegundosDesdeUltimoGolpe() { return Time.time - ultimoGolpe; }

    public string Estado()
    {
        string orden;
        if (hayDestino)
            orden = $"MOVER a ({destino.x:F0}, {destino.z:F0})" +
                    (Time.time < ignorarEnemigosHasta ? " [desenganchando]" : "");
        else if (objetivoOrdenado != null) orden = $"ATACAR {objetivoOrdenado.name}";
        else if (objetivoFinal != null) orden = $"ASEDIAR {objetivoFinal.name}";
        else orden = "ninguna";

        string obj = (objetivo != null)
            ? $"{objetivo.name} a {Distancia(objetivo):F1} m (alcance {rangoAtaque + radioObjetivo:F1})"
            : "ninguno";

        string golpe = (ultimoGolpe < 0f) ? "nunca" : $"hace {SegundosDesdeUltimoGolpe():F1} s";
        string activo = enabled ? "" : " [Combate DESACTIVADO]";

        return $"Orden: {orden}{activo}\nObjetivo: {obj}\nÚltimo golpe: {golpe}";
    }

    public bool EstaAsediando() { return objetivoFinal != null; }

    public void CancelarAtaque()
    {
        objetivo = null;
        teniaObjetivo = false;
        radioObjetivo = 0f;
        objetivoOrdenado = null;
        objetivoFinal = null;
        hayDestino = false;
        reanudarMarcha = false;

        if (agente != null && agente.isOnNavMesh) agente.isStopped = false;
    }
}