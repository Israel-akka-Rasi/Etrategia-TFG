using UnityEngine;

// HERRAMIENTA DE DESARROLLO: acelera TODO el juego para probar más rápido
// (movimiento, recolección, decisiones de la IA, ataques... todo a la vez).
// Pulsa la tecla para ir cambiando de velocidad en bucle. Quítalo en la versión final.
//
// Cómo usar: crea un GameObject vacío en la escena (p. ej. "DEBUG_Velocidad")
// y añádele este script. En Play, pulsa T para acelerar.
public class ControlVelocidad : MonoBehaviour
{
    [Header("Configuración")]
    public KeyCode tecla = KeyCode.T;
    // Incluye 20x, que es la velocidad a la que se entrena: así se pueden
    // reproducir las condiciones del entrenamiento sin lanzar ML-Agents.
    public float[] velocidades = { 1f, 2f, 4f, 8f, 20f };

    private int indice = 0;

    void Update()
    {
        // No tocamos la velocidad si el juego está pausado (p. ej. fin de partida)
        if (Time.timeScale == 0f) return;

        if (Input.GetKeyDown(tecla))
        {
            indice = (indice + 1) % velocidades.Length;
            Time.timeScale = velocidades[indice];
            Debug.Log($"Velocidad del juego: x{velocidades[indice]}");
        }
    }
}