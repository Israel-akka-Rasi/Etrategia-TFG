using UnityEngine;
using TMPro; // Necesario para el texto de la pantalla

public class GameManager : MonoBehaviour
{
    [Header("Rendimiento del NavMesh")]
    // Cuántas iteraciones de cálculo de rutas hace Unity en CADA FOTOGRAMA.
    // Por defecto son 100. Es un límite por fotograma, no por segundo de juego:
    // al acelerar la simulación hay muchos menos fotogramas por cada segundo de
    // juego, y con varias partidas y decenas de unidades pidiendo rutas a la vez
    // el presupuesto se agota. Las unidades se quedan paradas esperando su ruta.
    public int iteracionesRutasPorFotograma = 2000;

    [Header("Interfaz de Fin de Partida")]
    public GameObject panelFinJuego;
    public TextMeshProUGUI textoResultado;

    // Evita que el final se gestione varias veces en la misma partida
    private bool partidaTerminada = false;

    void Awake()
    {
        UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame = iteracionesRutasPorFotograma;
        Debug.Log($"NavMesh: {UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame} iteraciones de rutas por fotograma.");
    }

    void Start()
    {
        panelFinJuego.SetActive(false);
        // Solo reanudamos si veníamos de una pausa. NO forzamos el 1: durante el
        // entrenamiento, ML-Agents fija la escala de tiempo (--time-scale) y
        // machacarla aquí dejaría el juego corriendo a velocidad normal.
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    // La llama una base cuando es derrotada (su salud llega a 0).
    // equipoDerrotado = el equipo que ha PERDIDO.
    // Final por tiempo agotado: no gana nadie
    public void TerminarPartidaPorTiempo()
    {
        if (partidaTerminada) return;
        partidaTerminada = true;

        if (panelFinJuego != null) panelFinJuego.SetActive(true);

        if (textoResultado != null)
        {
            textoResultado.text = "¡EMPATE!";
            textoResultado.color = Color.yellow;
        }

        Time.timeScale = 0f;
    }

    public void TerminarPartida(int equipoDerrotado)
    {
        if (partidaTerminada) return; // el final de esta partida ya se gestionó
        partidaTerminada = true;

        // --- MODO ENTRENAMIENTO: reiniciar al instante, sin cartel ni pausa ---
        if (GestorPartida.Instancia != null && GestorPartida.Instancia.modoEntrenamiento)
        {
            int ganador = (equipoDerrotado == 0) ? 1 : 0;
            Debug.Log($"Partida terminada: perdió el equipo {equipoDerrotado} (ganó el {ganador}). Reiniciando...");
            GestorPartida.Instancia.ReiniciarPartida();
            return;
        }

        // --- MODO JUGABLE: cartel de victoria/derrota y pausa ---
        panelFinJuego.SetActive(true);

        if (equipoDerrotado == 1) // Perdió el rojo => ganas tú
        {
            textoResultado.text = "¡VICTORIA!";
            textoResultado.color = Color.green;
        }
        else // Perdió el azul => pierdes tú
        {
            textoResultado.text = "¡DERROTA!";
            textoResultado.color = Color.red;
        }

        Time.timeScale = 0f; // Congelamos el juego
    }

    // La llama el GestorPartida al reiniciar: deja todo listo para una partida nueva.
    public void PrepararNuevaPartida()
    {
        partidaTerminada = false;
        if (panelFinJuego != null) panelFinJuego.SetActive(false);
    }
}