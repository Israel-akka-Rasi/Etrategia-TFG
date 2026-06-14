using UnityEngine;
using TMPro; // Necesario para el texto de la pantalla

public class GameManager : MonoBehaviour
{
    [Header("Interfaz de Fin de Partida")]
    public GameObject panelFinJuego;
    public TextMeshProUGUI textoResultado;

    // Evita que el final se gestione varias veces en la misma partida
    private bool partidaTerminada = false;

    void Start()
    {
        panelFinJuego.SetActive(false);
        Time.timeScale = 1f;
    }

    // La llama una base cuando es derrotada (su salud llega a 0).
    // equipoDerrotado = el equipo que ha PERDIDO.
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