using UnityEngine;

// Controla el ciclo de partida/episodio. De momento (Parte A) solo sabe REINICIAR:
// vaciar el mundo (tropas y edificios construidos) y restaurar el estado de las bases.
// En la Parte B lo conectaremos al final de partida y al re-arranque de las IAs.
public class GestorPartida : MonoBehaviour
{
    public static GestorPartida Instancia;

    [Header("Modo")]
    public bool modoEntrenamiento = false;          // true = reinicia solo, sin pausar ni UI (para entrenar)
    public bool regenerarMapaCadaEpisodio = false;  // false = mapa fijo (rápido). TODO: rama de regenerar mapa.

    [Header("Contenedor de objetos dinámicos")]
    // Las tropas y los edificios construidos nacen DENTRO de este objeto.
    // Al reiniciar lo vaciamos de golpe. Mantenlo en posición (0,0,0) y escala 1.
    public Transform contenedorDinamico;

    [Header("Límite de Episodio (entrenamiento)")]
    // Ninguna partida puede durar para siempre. Si se agota el tiempo, se declara
    // empate y se reinicia. Es imprescindible al entrenar: un agente que aún no
    // sabe atacar dejaría el episodio colgado indefinidamente.
    public bool limitarDuracion = true;
    public float segundosMaximosPartida = 420f; // 7 minutos de tiempo de juego

    [Header("Pruebas")]
    public KeyCode teclaReinicio = KeyCode.R; // Para disparar el reinicio a mano y comprobarlo

    private int episodios = 0; // Cuántas partidas se han jugado (útil luego para el entrenamiento)
    private float tiempoPartida = 0f;

    // ---- Resultado de la partida, a la espera de que alguien lo recoja ----
    // Sin esto, al caer un castillo se reiniciaba en el MISMO frame y el agente
    // nunca llegaba a ver el final: se perdía la recompensa de victoria/derrota,
    // que es la señal más importante del aprendizaje.
    public bool ResultadoPendiente { get; private set; }
    public int EquipoDerrotado { get; private set; } = -1; // -1 = empate

    // Marca el resultado como ya recogido (lo llama el entorno al reiniciar)
    public void ConsumirResultado()
    {
        ResultadoPendiente = false;
        EquipoDerrotado = -1;
    }

    // ¿Hay un agente de aprendizaje gobernando esta partida?
    bool HayAgente()
    {
        AreaPartida area = MiArea();
        return area != null && area.Entorno() != null;
    }

    // TRUE mientras se está reiniciando. Las tropas destruidas durante el reinicio
    // NO deben descontar población, porque las bases ya la han restaurado a su valor
    // inicial (Destroy() de Unity se resuelve al final del frame, no al instante).
    public bool EstaReiniciando { get; private set; }

    // Área (partida) que gestiona este objeto
    private AreaPartida miArea;
    public AreaPartida MiArea()
    {
        if (miArea == null) miArea = AreaPartida.Resolver(this);
        return miArea;
    }

    void Awake()
    {
        // Ya NO es un singleton único: hay un gestor por partida. 'Instancia'
        // se mantiene apuntando al del área visible, por comodidad del código
        // de interfaz y de las herramientas de depuración.
        AreaPartida area = MiArea();
        if (Instancia == null || (area != null && area.esAreaVisible)) Instancia = this;

        // Si no nos han asignado contenedor, usamos el del área (y si el área
        // tampoco tiene, ella acabará heredando el nuestro: siempre uno común).
        if (contenedorDinamico == null && area != null) contenedorDinamico = area.contenedorDinamico;
    }

    void Update()
    {
        // Atajo de desarrollo: pulsar R reinicia la partida (lo quitaremos en la Parte B)
        // La tecla solo actúa sobre el área que estamos viendo
        if (Input.GetKeyDown(teclaReinicio) && MiArea() != null && MiArea().esAreaVisible)
        {
            ReiniciarPartida();
        }

        VigilarDuracion();
    }

    // Si la partida se eterniza (nadie ataca, ambos atascados), se corta en empate.
    void VigilarDuracion()
    {
        if (!limitarDuracion) return;
        if (Time.timeScale == 0f) return; // partida ya terminada y pausada

        tiempoPartida += Time.deltaTime;

        if (tiempoPartida >= segundosMaximosPartida)
        {
            Debug.LogWarning($"GestorPartida: tiempo agotado ({segundosMaximosPartida}s). EMPATE.");
            tiempoPartida = 0f;

            // Empate: nadie derrotado. Si hay agente, que lo observe él.
            if (!ResultadoPendiente)
            {
                ResultadoPendiente = true;
                EquipoDerrotado = -1;
            }
            if (HayAgente()) return;

            if (modoEntrenamiento)
            {
                ReiniciarPartida();
            }
            else if (MiArea() != null && MiArea().esAreaVisible)
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null) gm.TerminarPartidaPorTiempo();
            }
            else
            {
                ReiniciarPartida(); // áreas de fondo: siempre reinician solas
            }
        }
    }

    // Fin de partida de ESTA área. Lo llama la base al ser destruida.
    public void TerminarPartida(int equipoDerrotado)
    {
        if (ResultadoPendiente) return; // ya estaba resuelta

        AreaPartida area = MiArea();

        // Dejamos anotado el resultado SIEMPRE, antes de hacer nada más
        ResultadoPendiente = true;
        EquipoDerrotado = equipoDerrotado;

        // Si hay un agente al mando, NO reiniciamos: él tiene que poder observar
        // el final, cobrar su recompensa y decidir cuándo empieza el episodio nuevo.
        if (HayAgente()) return;

        // Sin agente: comportamiento de siempre
        if (modoEntrenamiento || area == null || !area.esAreaVisible)
        {
            ReiniciarPartida();
            return;
        }

        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null) gm.TerminarPartida(equipoDerrotado);
    }

    // Segundos transcurridos en la partida actual (lo usa el entorno de RL)
    public float TiempoPartida()
    {
        return tiempoPartida;
    }

    public void ReiniciarPartida()
    {
        episodios++;
        Debug.Log($"GestorPartida: reiniciando partida (episodio {episodios})...");

        EstaReiniciando = true;
        tiempoPartida = 0f;
        ConsumirResultado();

        // Si veníamos de un fin de partida pausado, reanudamos el tiempo
        if (Time.timeScale == 0f) Time.timeScale = 1f;

        // El cartel de fin de partida solo existe para el área visible
        if (MiArea() != null && MiArea().esAreaVisible)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null) gm.PrepararNuevaPartida();
        }

        if (regenerarMapaCadaEpisodio)
        {
            // TODO (futuro): destruir el mapa entero + bases y volver a generarlo con GeneradorTablero.
            Debug.LogWarning("GestorPartida: la regeneración de mapa aún no está implementada. Uso mapa fijo.");
        }

        // 1. Vaciar el contenedor dinámico. Pedimos el del ÁREA para asegurarnos
        //    de vaciar exactamente el mismo sitio donde nacen las tropas.
        Transform contenedor = (MiArea() != null) ? MiArea().Contenedor() : contenedorDinamico;

        if (contenedor != null)
        {
            for (int i = contenedor.childCount - 1; i >= 0; i--)
            {
                Destroy(contenedor.GetChild(i).gameObject);
            }
        }
        else
        {
            Debug.LogWarning("GestorPartida: no encuentro contenedor dinámico que vaciar.");
        }

        // A partir de aquí SOLO tocamos objetos de NUESTRA partida: con varias
        // áreas entrenando a la vez, reiniciar todo cortaría los episodios ajenos.
        AreaPartida area = MiArea();

        // 2. Restaurar el estado de cada base (vida, recursos, población, nivel)
        foreach (BaseMilitar b in area.Bases())
        {
            b.ReiniciarEstado();
        }

        // 2.5. Reabastecer los nodos de recurso (árboles, minas) a su estado lleno
        foreach (Recurso r in area.Recursos())
        {
            r.ReiniciarRecurso();
        }

        // 3. Volver a crear la tropa inicial (el Leñador gratis) de cada base
        foreach (BaseMilitar b in area.Bases())
        {
            b.SpawnTropaInicial();
        }

        // 4. Reiniciar el cerebro de cada IA enemiga de esta partida
        foreach (BaseMilitar b in area.Bases())
        {
            IAEnemiga c = b.GetComponent<IAEnemiga>();
            if (c != null) c.ReiniciarComandante();
        }

        // Las destrucciones pendientes se resuelven al final del frame; hasta entonces
        // mantenemos la bandera para que no descuenten población de la partida nueva.
        StartCoroutine(TerminarReinicio());
    }

    private System.Collections.IEnumerator TerminarReinicio()
    {
        yield return new WaitForEndOfFrame();
        EstaReiniciando = false;
    }
}