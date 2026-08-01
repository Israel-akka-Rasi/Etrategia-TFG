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

    // TRUE mientras se está reiniciando. Las tropas destruidas durante el reinicio
    // NO deben descontar población, porque las bases ya la han restaurado a su valor
    // inicial (Destroy() de Unity se resuelve al final del frame, no al instante).
    public bool EstaReiniciando { get; private set; }

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else { Destroy(gameObject); return; }
    }

    void Update()
    {
        // Atajo de desarrollo: pulsar R reinicia la partida (lo quitaremos en la Parte B)
        if (Input.GetKeyDown(teclaReinicio))
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
            Debug.LogWarning($"GestorPartida: tiempo agotado ({segundosMaximosPartida}s). EMPATE, reiniciando.");
            tiempoPartida = 0f;

            if (modoEntrenamiento)
            {
                ReiniciarPartida();
            }
            else
            {
                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null) gm.TerminarPartidaPorTiempo();
            }
        }
    }

    public void ReiniciarPartida()
    {
        episodios++;
        Debug.Log($"GestorPartida: reiniciando partida (episodio {episodios})...");

        EstaReiniciando = true;
        tiempoPartida = 0f;

        // Si veníamos de un fin de partida pausado, reanudamos el tiempo
        if (Time.timeScale == 0f) Time.timeScale = 1f;

        // Dejamos el GameManager listo para una partida nueva (oculta el cartel y resetea su flag)
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null) gm.PrepararNuevaPartida();

        if (regenerarMapaCadaEpisodio)
        {
            // TODO (futuro): destruir el mapa entero + bases y volver a generarlo con GeneradorTablero.
            Debug.LogWarning("GestorPartida: la regeneración de mapa aún no está implementada. Uso mapa fijo.");
        }

        // 1. Vaciar el contenedor dinámico: borra todas las tropas y edificios construidos de una vez
        if (contenedorDinamico != null)
        {
            for (int i = contenedorDinamico.childCount - 1; i >= 0; i--)
            {
                Destroy(contenedorDinamico.GetChild(i).gameObject);
            }
        }
        else
        {
            Debug.LogWarning("GestorPartida: no tengo asignado el contenedorDinamico en el Inspector.");
        }

        // 2. Restaurar el estado de cada base (vida, recursos, población, nivel)
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            b.ReiniciarEstado();
        }

        // 2.5. Reabastecer los nodos de recurso (árboles, minas) a su estado lleno
        Recurso[] recursos = FindObjectsOfType<Recurso>();
        foreach (Recurso r in recursos)
        {
            r.ReiniciarRecurso();
        }

        // 3. Volver a crear la tropa inicial (el Leñador gratis) de cada base
        foreach (BaseMilitar b in bases)
        {
            b.SpawnTropaInicial();
        }

        // 4. Reiniciar el cerebro de cada IA enemiga (vuelven a su fase de Economía)
        IAEnemiga[] comandantes = FindObjectsOfType<IAEnemiga>();
        foreach (IAEnemiga c in comandantes)
        {
            c.ReiniciarComandante();
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