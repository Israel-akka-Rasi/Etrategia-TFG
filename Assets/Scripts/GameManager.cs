using UnityEngine;
using TMPro; // Necesario para el texto de la pantalla

public class GameManager : MonoBehaviour
{
    [Header("Interfaz de Fin de Partida")]
    public GameObject panelFinJuego;
    public TextMeshProUGUI textoResultado;

    void Start()
    {
        // Al empezar la partida, escondemos el cartel y nos aseguramos de que el tiempo corra
        panelFinJuego.SetActive(false);
        Time.timeScale = 1f; 
    }

    // Esta función la llamará el castillo justo antes de explotar
    public void TerminarPartida(int equipoDerrotado)
    {
        panelFinJuego.SetActive(true); // Mostramos el cartel

        if (equipoDerrotado == 1) // Si muere el equipo 1 (Rojo)
        {
            textoResultado.text = "¡VICTORIA!";
            textoResultado.color = Color.green;
        }
        else // Si muere el equipo 0 (Azul / Tú)
        {
            textoResultado.text = "¡DERROTA!";
            textoResultado.color = Color.red;
        }

        // ¡Magia! Detenemos el tiempo del juego por completo
        Time.timeScale = 0f; 
    }
}