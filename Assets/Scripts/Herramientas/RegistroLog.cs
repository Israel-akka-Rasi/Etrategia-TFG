using UnityEngine;
using System.IO;
using System.Text;

// ============================================================================
// REGISTRO DE LOG A ARCHIVO
// Vuelca los mensajes de consola a un .txt para poder revisarlos con calma
// (o pasárselos a alguien). Con filtro, para quedarse solo con lo interesante.
//
// USO: crea un GameObject vacío, ponle este script y dale a Play.
// El archivo aparece en la carpeta del proyecto, junto a Assets/.
// ============================================================================

public class RegistroLog : MonoBehaviour
{
    [Header("Archivo")]
    public string nombreArchivo = "log_partida.txt";
    public bool anadirFechaAlNombre = true;

    [Header("Filtro")]
    // Si está activo, solo se guardan las líneas que contengan alguno de los textos.
    // Déjalo desactivado para guardarlo TODO.
    public bool usarFiltro = true;
    public string[] textosDeInteres = {
        "IAEnemiga",
        "EL ENEMIGO",
        "EL JUGADOR",
        "ATAQUE FORZOSO",
        "Comandante"
    };

    [Header("Extras")]
    public bool incluirAvisosYErrores = true; // warnings y errores siempre interesan
    public KeyCode teclaGuardar = KeyCode.F2; // fuerza el volcado a disco

    private StreamWriter escritor;
    private string rutaCompleta;
    private int lineasEscritas = 0;
    private float temporizadorVolcado = 0f;

    void OnEnable()
    {
        AbrirArchivo();
        Application.logMessageReceived += RecibirMensaje;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= RecibirMensaje;
        CerrarArchivo();
    }

    void AbrirArchivo()
    {
        string nombre = nombreArchivo;
        if (anadirFechaAlNombre)
        {
            string sello = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sinExt = Path.GetFileNameWithoutExtension(nombreArchivo);
            string ext = Path.GetExtension(nombreArchivo);
            if (string.IsNullOrEmpty(ext)) ext = ".txt";
            nombre = $"{sinExt}_{sello}{ext}";
        }

        // Carpeta del proyecto (al lado de Assets), fácil de encontrar
        string carpeta = Path.Combine(Application.dataPath, "..", "Logs");
        Directory.CreateDirectory(carpeta);
        rutaCompleta = Path.GetFullPath(Path.Combine(carpeta, nombre));

        escritor = new StreamWriter(rutaCompleta, false, Encoding.UTF8);
        escritor.AutoFlush = false;

        escritor.WriteLine("=== REGISTRO DE PARTIDA ===");
        escritor.WriteLine($"Inicio: {System.DateTime.Now}");
        escritor.WriteLine($"Filtro activo: {usarFiltro}");
        escritor.WriteLine("Formato: [tiempo de juego] mensaje");
        escritor.WriteLine();
        escritor.Flush();

        Debug.Log($"RegistroLog: guardando en {rutaCompleta}");
    }

    void RecibirMensaje(string mensaje, string pila, LogType tipo)
    {
        if (escritor == null) return;

        bool esAvisoOError = (tipo == LogType.Warning || tipo == LogType.Error
                           || tipo == LogType.Exception || tipo == LogType.Assert);

        // Decidimos si esta línea nos interesa
        if (usarFiltro && !(incluirAvisosYErrores && esAvisoOError))
        {
            bool coincide = false;
            foreach (string texto in textosDeInteres)
            {
                if (!string.IsNullOrEmpty(texto) && mensaje.Contains(texto)) { coincide = true; break; }
            }
            if (!coincide) return;
        }

        string marca = tipo == LogType.Log ? "" : $"[{tipo}] ";
        escritor.WriteLine($"[{Time.time,7:F1}s] {marca}{mensaje}");
        lineasEscritas++;

        // Las excepciones sí llevan pila: ayuda mucho a localizar el fallo
        if (tipo == LogType.Exception || tipo == LogType.Error)
        {
            escritor.WriteLine(pila);
        }
    }

    void Update()
    {
        // Volcado periódico, para no perder nada si Unity se cierra de golpe
        temporizadorVolcado += Time.unscaledDeltaTime;
        if (temporizadorVolcado >= 2f)
        {
            temporizadorVolcado = 0f;
            if (escritor != null) escritor.Flush();
        }

        if (Input.GetKeyDown(teclaGuardar))
        {
            if (escritor != null) escritor.Flush();
            Debug.Log($"RegistroLog: {lineasEscritas} líneas guardadas en {rutaCompleta}");
        }
    }

    void CerrarArchivo()
    {
        if (escritor == null) return;

        escritor.WriteLine();
        escritor.WriteLine($"=== FIN === {lineasEscritas} líneas · {System.DateTime.Now}");
        escritor.Flush();
        escritor.Close();
        escritor = null;
    }
}