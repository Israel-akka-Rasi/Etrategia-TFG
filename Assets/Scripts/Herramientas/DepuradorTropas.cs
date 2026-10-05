using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ============================================================================
// DEPURADOR DE TROPAS
// Escribe en un ARCHIVO propio (Logs/depuracion_FECHA.txt, junto a Assets/)
// todo lo necesario para saber por qué una unidad no hace lo que debería.
// En la consola solo aparece una línea al empezar, con la ruta del archivo.
//
// Qué registra (solo el área visible):
//   ORDEN        cada orden nueva que recibe una unidad de combate TUYA,
//                con su estado en ese momento.
//   COMPROBACION 1,5 s después de cada orden: cuánto se ha movido y su estado.
//                Marca "NO SE HA MOVIDO" si no ha avanzado ni está golpeando.
//   ATASCO       unidad de combate (de cualquier bando) quieta 2 s teniendo
//                algo que hacer: ruta pendiente o enemigo sin golpear.
//   RECUPERADA   cuando una unidad atascada vuelve a moverse o a golpear.
//   RESUMEN      cada 10 s: cuántas unidades de cada bando se mueven, golpean,
//                están atascadas o llevan más de 1 s "calculando ruta".
//   OBSTACULOS   cada 10 s: tamaño de la malla y NavMeshObstacle con Carve.
// En el ejecutable sin gráficos (entrenamiento) se desactiva solo.
//   F4           volcado del estado completo de las unidades seleccionadas.
//   F3           muestra/oculta la ficha en pantalla (desactivada por defecto).
//
// Otros scripts pueden escribir aquí con DepuradorTropas.Anotar("...").
// Ponlo en cualquier objeto de la escena (por ejemplo, LogSalida).
// ============================================================================

public class DepuradorTropas : MonoBehaviour
{
    [Header("Archivo")]
    public bool escribirArchivo = true;

    [Header("Controles")]
    public KeyCode teclaFicha = KeyCode.F3;
    public KeyCode teclaVolcado = KeyCode.F4;
    public bool mostrarFicha = false;

    [Header("Vigilancia")]
    public float intervaloRevision = 0.25f;
    public float segundosQuietaParaAvisar = 2f;
    public float segundosComprobacionOrden = 1.5f;
    public float intervaloResumen = 10f;
    public float movimientoMinimo = 0.2f;

    // --- Archivo ---
    private static DepuradorTropas instancia;
    private StreamWriter escritor;
    private float temporizadorVolcado;

    // --- Seguimiento por unidad ---
    private class Seguimiento
    {
        public Vector3 ultimaPosicion;
        public float tiempoQuieta;
        public bool atascada;
        public float inicioAtasco;
        public string ultimaOrden = null;
        public Vector3 posicionAlOrdenar;
        public float comprobarEn = -1f;
        public float tiempoCalculando;
    }
    private readonly Dictionary<Tropa, Seguimiento> seguimientos = new Dictionary<Tropa, Seguimiento>();

    private Movimiento movimiento;
    private float temporizadorRevision;
    private float temporizadorResumen;

    // ------------------------------------------------------------------
    // ARCHIVO
    // ------------------------------------------------------------------
    void OnEnable()
    {
        // En el ejecutable de entrenamiento (sin gráficos) no hace falta y
        // gastaría tiempo de CPU: se desactiva solo.
        if (Application.isBatchMode) { enabled = false; return; }

        instancia = this;
        if (!escribirArchivo) return;

        string carpeta = Path.Combine(Application.dataPath, "..", "Logs");
        Directory.CreateDirectory(carpeta);
        string ruta = Path.GetFullPath(Path.Combine(carpeta,
            $"depuracion_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt"));

        escritor = new StreamWriter(ruta, false, Encoding.UTF8);
        escritor.WriteLine("=== DEPURACIÓN DE TROPAS ===");
        escritor.WriteLine($"Inicio: {System.DateTime.Now}");
        escritor.WriteLine("Formato: [tiempo de juego] TIPO unidad: detalle");
        escritor.WriteLine();
        escritor.Flush();

        Debug.Log($"DepuradorTropas: escribiendo en {ruta}");
    }

    void OnDisable()
    {
        if (instancia == this) instancia = null;
        if (escritor == null) return;
        escritor.WriteLine();
        escritor.WriteLine($"=== FIN · {System.DateTime.Now} ===");
        escritor.Close();
        escritor = null;
    }

    void Escribir(string linea)
    {
        if (escritor == null) return;
        escritor.WriteLine($"[{Time.time,7:F1}s] {linea}");
    }

    // Para que otros scripts dejen constancia en este mismo archivo
    public static void Anotar(string mensaje)
    {
        if (instancia != null && instancia.escritor != null) instancia.Escribir(mensaje);
        else Debug.LogWarning(mensaje);
    }

    // ------------------------------------------------------------------
    // BUCLE
    // ------------------------------------------------------------------
    void Update()
    {
        if (Input.GetKeyDown(teclaFicha)) mostrarFicha = !mostrarFicha;
        if (Input.GetKeyDown(teclaVolcado)) VolcarSeleccionadas();

        temporizadorRevision += Time.deltaTime;
        if (temporizadorRevision >= intervaloRevision)
        {
            float paso = temporizadorRevision;
            temporizadorRevision = 0f;
            Revisar(paso);
        }

        temporizadorResumen += Time.deltaTime;
        if (temporizadorResumen >= intervaloResumen)
        {
            temporizadorResumen = 0f;
            EscribirResumen();
        }

        // Volcado a disco periódico, por si Unity se cierra de golpe
        temporizadorVolcado += Time.unscaledDeltaTime;
        if (temporizadorVolcado >= 2f && escritor != null)
        {
            temporizadorVolcado = 0f;
            escritor.Flush();
        }
    }

    void Revisar(float paso)
    {
        AreaPartida area = AreaPartida.AreaVisible();
        if (area == null) return;

        foreach (Tropa t in area.Tropas())
        {
            if (t == null || t.EsRecolector()) continue;

            Seguimiento s;
            if (!seguimientos.TryGetValue(t, out s))
            {
                s = new Seguimiento { ultimaPosicion = t.transform.position };
                seguimientos[t] = s;
            }

            Combate c = t.GetComponent<Combate>();
            NavMeshAgent a = t.GetComponent<NavMeshAgent>();

            // Tiempo seguido "calculando ruta"
            bool calculando = a != null && a.isActiveAndEnabled && a.isOnNavMesh && a.pathPending;
            s.tiempoCalculando = calculando ? s.tiempoCalculando + paso : 0f;

            // --- Órdenes nuevas (solo las tuyas) ---
            if (t.equipo == 0 && c != null)
            {
                string orden = LineaOrden(c);
                if (orden != s.ultimaOrden)
                {
                    if (s.ultimaOrden != null)
                        Escribir($"ORDEN {Id(t)}: {orden}\n    {Describir(t, s).Replace("\n", "\n    ")}");
                    s.ultimaOrden = orden;
                    s.posicionAlOrdenar = t.transform.position;
                    s.comprobarEn = Time.time + segundosComprobacionOrden;
                }

                if (s.comprobarEn > 0f && Time.time >= s.comprobarEn)
                {
                    s.comprobarEn = -1f;
                    float movidoOrden = Vector3.Distance(t.transform.position, s.posicionAlOrdenar);
                    bool golpeando = c.SegundosDesdeUltimoGolpe() < segundosComprobacionOrden;
                    string marca = (movidoOrden < 0.3f && !golpeando) ? "  <-- NO SE HA MOVIDO" : "";
                    Escribir($"COMPROBACION {Id(t)}: movido {movidoOrden:F1} m en {segundosComprobacionOrden:F1} s{marca}\n    {Describir(t, s).Replace("\n", "\n    ")}");
                }
            }

            // --- Atascos (ambos bandos) ---
            float movido = Vector3.Distance(t.transform.position, s.ultimaPosicion);
            s.ultimaPosicion = t.transform.position;
            string motivo = MotivoParaEstarOcupada(t, c, a);

            if (movido >= movimientoMinimo * (paso / 0.5f) || motivo == null)
            {
                if (s.atascada)
                    Escribir($"RECUPERADA {Id(t)} tras {Time.time - s.inicioAtasco:F1} s");
                s.tiempoQuieta = 0f;
                s.atascada = false;
                continue;
            }

            s.tiempoQuieta += paso;
            if (s.tiempoQuieta >= segundosQuietaParaAvisar && !s.atascada)
            {
                s.atascada = true;
                s.inicioAtasco = Time.time - s.tiempoQuieta;
                Escribir($"ATASCO {Id(t)} (equipo {t.equipo}): {motivo}\n    {Describir(t, s).Replace("\n", "\n    ")}");
            }
        }

        // Limpiar las que ya no existen
        List<Tropa> muertas = null;
        foreach (Tropa t in seguimientos.Keys)
            if (t == null) (muertas ??= new List<Tropa>()).Add(t);
        if (muertas != null) foreach (Tropa t in muertas) seguimientos.Remove(t);
    }

    // ¿Tiene algo que hacer? Devuelve la razón, o null si está legítimamente parada
    string MotivoParaEstarOcupada(Tropa t, Combate c, NavMeshAgent a)
    {
        if (c != null && c.enabled && c.TieneObjetivo())
        {
            if (c.SegundosDesdeUltimoGolpe() < 2.5f) return null; // golpeando: estar quieta es normal
            return "tiene objetivo de combate pero NO golpea";
        }

        if (a != null && a.isActiveAndEnabled && a.isOnNavMesh && !a.isStopped
            && (a.pathPending || (a.hasPath && a.remainingDistance > a.stoppingDistance + 0.5f)))
            return a.pathPending ? "ruta pendiente de calcular" : "tiene ruta pero no avanza";

        return null;
    }

    // ------------------------------------------------------------------
    // RESUMEN PERIÓDICO
    // ------------------------------------------------------------------
    void EscribirResumen()
    {
        // Complejidad de la malla de navegación
        Escribir("OBSTACULOS " + CensoObstaculos());

        AreaPartida area = AreaPartida.AreaVisible();
        if (area == null) return;

        int[] total = new int[2], moviendo = new int[2], golpeando = new int[2];
        int[] atascadas = new int[2], calculandoLargo = new int[2];

        foreach (Tropa t in area.Tropas())
        {
            if (t == null || t.EsRecolector() || t.equipo < 0 || t.equipo > 1) continue;
            int e = t.equipo;
            total[e]++;

            NavMeshAgent a = t.GetComponent<NavMeshAgent>();
            Combate c = t.GetComponent<Combate>();
            if (a != null && a.isActiveAndEnabled && a.velocity.magnitude > 0.2f) moviendo[e]++;
            if (c != null && c.SegundosDesdeUltimoGolpe() < 2f) golpeando[e]++;

            Seguimiento s;
            if (seguimientos.TryGetValue(t, out s))
            {
                if (s.atascada) atascadas[e]++;
                if (s.tiempoCalculando > 1f) calculandoLargo[e]++;
            }
        }

        Escribir($"RESUMEN combate | tuyas: {total[0]} (moviéndose {moviendo[0]}, golpeando {golpeando[0]}, atascadas {atascadas[0]}, calculando ruta >1 s {calculandoLargo[0]})" +
                 $" | enemigas: {total[1]} (moviéndose {moviendo[1]}, golpeando {golpeando[1]}, atascadas {atascadas[1]}, calculando ruta >1 s {calculandoLargo[1]})");
    }

    // ------------------------------------------------------------------
    // CENSO DE NavMeshObstacle
    // Un obstáculo con "Carve" abre un agujero en la malla de navegación. Cada
    // vez que aparece, se mueve o se detiene, la malla cambia y Unity invalida y
    // recalcula las rutas de las unidades cercanas. Si los llevan las propias
    // tropas, en una batalla la malla cambia sin parar y las rutas no terminan.
    // ------------------------------------------------------------------
    private int triangulosMalla = -1;

    string CensoObstaculos()
    {
        NavMeshObstacle[] todos = FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None);
        int activos = 0, conCarve = 0, enUnidades = 0, soloQuietos = 0;
        Dictionary<string, int> porNombre = new Dictionary<string, int>();

        foreach (NavMeshObstacle o in todos)
        {
            if (o == null || !o.isActiveAndEnabled) continue;
            activos++;
            if (!o.carving) continue;
            conCarve++;
            if (o.carveOnlyStationary) soloQuietos++;

            bool esUnidad = o.GetComponentInParent<Tropa>() != null || o.GetComponent<NavMeshAgent>() != null;
            if (esUnidad) enUnidades++;

            string nombre = o.transform.root == o.transform ? o.name : o.transform.root.name + "/" + o.name;
            nombre = nombre.Replace("(Clone)", "");
            if (esUnidad) nombre = "[UNIDAD] " + o.name.Replace("(Clone)", "");
            porNombre[nombre] = porNombre.TryGetValue(nombre, out int n) ? n + 1 : 1;
        }

        List<KeyValuePair<string, int>> lista = new List<KeyValuePair<string, int>>(porNombre);
        lista.Sort((a, b) => b.Value.CompareTo(a.Value));
        StringBuilder ej = new StringBuilder();
        for (int i = 0; i < lista.Count && i < 6; i++) ej.Append($"{lista[i].Key} x{lista[i].Value}; ");

        // Tamaño de la malla de navegación (se calcula una sola vez: es costoso)
        if (triangulosMalla < 0)
            triangulosMalla = NavMesh.CalculateTriangulation().indices.Length / 3;

        return $"malla {triangulosMalla} triángulos | activos {activos}, con carve {conCarve} (en unidades {enUnidades}, solo quietos {soloQuietos}) | {ej}";
    }

    // ------------------------------------------------------------------
    // DESCRIPCIÓN DEL ESTADO
    // ------------------------------------------------------------------
    static string Id(Tropa t)
    {
        return $"{t.name.Replace("(Clone)", "")}#{Mathf.Abs(t.GetInstanceID()) % 10000}";
    }

    static string LineaOrden(Combate c)
    {
        string estado = c.Estado();
        int salto = estado.IndexOf('\n');
        string linea = (salto >= 0) ? estado.Substring(0, salto) : estado;
        // Quitamos la marca de desenganche para no contarla como orden nueva
        return linea.Replace(" [desenganchando]", "");
    }

    string Describir(Tropa t, Seguimiento s)
    {
        StringBuilder sb = new StringBuilder();

        Combate c = t.GetComponent<Combate>();
        sb.AppendLine(c != null ? c.Estado() : "(sin Combate)");

        NavMeshAgent a = t.GetComponent<NavMeshAgent>();
        if (a == null) sb.Append("Nav: SIN NavMeshAgent");
        else if (!a.isActiveAndEnabled) sb.Append("Nav: agente DESACTIVADO");
        else if (!a.isOnNavMesh) sb.Append("Nav: FUERA DE LA MALLA");
        else
        {
            string ruta = a.pathPending ? "calculando"
                        : a.hasPath ? a.pathStatus.ToString()
                        : "sin ruta";
            string llevaCalculando = (s != null && s.tiempoCalculando > 0f) ? $" (lleva {s.tiempoCalculando:F1} s)" : "";
            sb.Append($"Nav: frenado={(a.isStopped ? "SÍ" : "no")} | ruta={ruta}{llevaCalculando} | " +
                      $"falta={(a.hasPath ? a.remainingDistance.ToString("F1") : "-")} m | " +
                      $"vel={a.velocity.magnitude:F1} (máx {a.speed:F1}) | " +
                      $"pos=({t.transform.position.x:F0}, {t.transform.position.z:F0})");
        }

        NavMeshObstacle obs = t.GetComponent<NavMeshObstacle>();
        if (obs != null)
            sb.Append($"\nObstáculo: {(obs.isActiveAndEnabled ? "ACTIVO" : "inactivo")}, carve={(obs.carving ? "SÍ" : "no")}");

        return sb.ToString();
    }

    void VolcarSeleccionadas()
    {
        List<Tropa> sel = Seleccionadas();
        if (sel.Count == 0) { Escribir("VOLCADO: no hay tropas seleccionadas."); return; }

        Escribir($"VOLCADO de {sel.Count} unidades seleccionadas:");
        foreach (Tropa t in sel)
        {
            if (t == null) continue;
            Seguimiento s;
            seguimientos.TryGetValue(t, out s);
            Escribir($"  {Id(t)} (equipo {t.equipo}):\n    {Describir(t, s).Replace("\n", "\n    ")}");
        }
        if (escritor != null) escritor.Flush();
        Debug.Log($"DepuradorTropas: volcado de {sel.Count} unidades escrito en el archivo.");
    }

    List<Tropa> Seleccionadas()
    {
        if (movimiento == null) movimiento = FindObjectOfType<Movimiento>();
        return (movimiento != null) ? movimiento.tropasSeleccionadas : new List<Tropa>();
    }

    // ------------------------------------------------------------------
    // FICHA EN PANTALLA (opcional, F3)
    // ------------------------------------------------------------------
    private GUIStyle estilo;

    void OnGUI()
    {
        if (!mostrarFicha || Camera.main == null) return;

        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.box);
            estilo.alignment = TextAnchor.UpperLeft;
            estilo.fontSize = 11;
            estilo.wordWrap = false;
        }

        foreach (Tropa t in Seleccionadas())
        {
            if (t == null) continue;

            Vector3 p = Camera.main.WorldToScreenPoint(t.transform.position + Vector3.up * 2f);
            if (p.z < 0f) continue;

            Seguimiento s;
            seguimientos.TryGetValue(t, out s);
            string texto = $"{Id(t)}\n{Describir(t, s)}";
            Vector2 tam = estilo.CalcSize(new GUIContent(texto));
            GUI.Box(new Rect(p.x - tam.x / 2f, Screen.height - p.y - tam.y, tam.x, tam.y), texto, estilo);
        }
    }
}