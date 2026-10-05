using UnityEngine;
using System.Globalization;

// ============================================================================
// CONFIGURACIÓN DE EJECUCIÓN PARA PRUEBAS DE VERIFICACIÓN
//
// Permite reproducir las condiciones exactas del entrenamiento SIN ML-Agents,
// para comprobar que la simulación se comporta igual que a velocidad normal.
//
// En un ejecutable se controla por línea de comandos (todos opcionales):
//   -escalaTiempo 20    Time.timeScale
//   -capturaFps 300     Time.captureFramerate: fija el tiempo de juego que
//                       avanza cada fotograma (escala / captura)
//   -sinAgente          desactiva los agentes: prueba de la IA enemiga sola
//   -duracion 300       segundos de JUEGO tras los que se cierra solo
//
// En el editor se usan los valores del Inspector si "aplicarEnEditor" está marcado.
//
// Sin argumentos (y sin la casilla marcada) NO HACE NADA: durante el
// entrenamiento es ML-Agents quien fija la velocidad, y no debemos interferir.
// ============================================================================

public class ConfiguracionEjecucion : MonoBehaviour
{
    [Header("Pruebas en el editor")]
    public bool aplicarEnEditor = false;
    public float escalaTiempo = 20f;
    public int capturaFps = 300;
    public bool sinAgente = true;
    public float duracion = 300f; // segundos de juego; 0 = sin límite

    private float cerrarEn = 0f;
    private string resumenConfiguracion = null;

    void Awake()
    {
        float escala = escalaTiempo;
        int captura = capturaFps;
        bool quitarAgente = sinAgente;
        float dur = duracion;
        bool hayArgumentos = false;

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string siguiente = (i + 1 < args.Length) ? args[i + 1] : null;
            switch (args[i])
            {
                case "-escalaTiempo":
                    if (float.TryParse(siguiente, NumberStyles.Float, CultureInfo.InvariantCulture, out float e)) { escala = e; hayArgumentos = true; }
                    break;
                case "-capturaFps":
                    if (int.TryParse(siguiente, out int c)) { captura = c; hayArgumentos = true; }
                    break;
                case "-sinAgente":
                    quitarAgente = true; hayArgumentos = true;
                    break;
                case "-duracion":
                    if (float.TryParse(siguiente, NumberStyles.Float, CultureInfo.InvariantCulture, out float d)) { dur = d; hayArgumentos = true; }
                    break;
            }
        }

        bool aplicar = Application.isEditor ? aplicarEnEditor : hayArgumentos;
        if (!aplicar) return; // entrenamiento normal: manda ML-Agents

        Time.timeScale = escala;
        Time.captureFramerate = captura;

        if (quitarAgente)
        {
            foreach (AgenteRTS a in FindObjectsByType<AgenteRTS>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                a.enabled = false;
        }

        if (dur > 0f) cerrarEn = dur;

        float paso = (captura > 0) ? escala / captura : -1f;
        resumenConfiguracion = $"CONFIG escala {escala}x | captura {captura} fps | paso {paso:0.000} s por fotograma | " +
                               $"agente {(quitarAgente ? "DESACTIVADO" : "activo")} | duración {(dur > 0 ? dur + " s" : "sin límite")}";
    }

    // Se escribe en Start y no en Awake: en Awake el registro todavía no ha
    // empezado a escuchar la consola, y el mensaje se perdía.
    void Start()
    {
        if (resumenConfiguracion != null) Debug.Log(resumenConfiguracion);
    }

    void Update()
    {
        if (cerrarEn <= 0f || Time.time < cerrarEn) return;

        Debug.Log($"CONFIG fin de la prueba a los {Time.time:0} s de juego ({Time.realtimeSinceStartup:0} s reales)");
        cerrarEn = 0f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}