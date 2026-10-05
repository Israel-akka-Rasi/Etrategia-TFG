using UnityEngine;

// ============================================================================
// PANEL DE PRUEBA DEL ENTORNO RL
// Herramienta de desarrollo: permite disparar cada macroacción a mano y ver
// en tiempo real las observaciones y la recompensa. Sirve para verificar que
// el entorno funciona ANTES de conectar ML-Agents.
//
// También sirve para VER JUGAR a un modelo entrenado: muestra la última acción
// que ha elegido el agente y el recuento de acciones de la partida en curso.
//
// Se dibuja con OnGUI, así que no hay que montar nada en el Canvas.
// Desactiva este script para las pruebas de juego normal.
// ============================================================================

public class PanelPruebaRL : MonoBehaviour
{
    [Header("Visibilidad")]
    public bool mostrarPanel = true;
    public KeyCode teclaAlternar = KeyCode.F1;

    [Header("Agente Aleatorio (prueba de estrés)")]
    // Ejecuta acciones al azar. Útil para comprobar que ninguna combinación
    // rompe el juego y que los episodios siempre terminan.
    public bool agenteAleatorio = false;
    public float segundosEntreAcciones = 1f;

    private EntornoRL entorno;
    private AreaPartida areaObservada; // la elegida con los atajos 1..4 de la cámara

    // Entorno del área que estamos observando (por defecto, la visible)
    EntornoRL Entorno()
    {
        if (entorno != null) return entorno;

        AreaPartida area = areaObservada != null ? areaObservada : AreaPartida.AreaVisible();
        if (area != null) entorno = area.Entorno();
        if (entorno == null) entorno = FindObjectOfType<EntornoRL>();

        return entorno;
    }

    // Cambia el área que muestra el panel (lo llaman los atajos de la cámara)
    public void ObservarArea(AreaPartida area)
    {
        if (area == null) return;
        areaObservada = area;
        entorno = area.Entorno();
        ReiniciarContadores();
    }

    // Recuento por acción, para saber cuáles funcionan y cuáles fallan siempre
    private int[] vecesLanzada;
    private int[] vecesValida;

    private float temporizador;
    private float recompensaTotalEpisodio = 0f;
    private int pasosEpisodio = 0;
    private string ultimaAccion = "-";
    private Vector2 scroll;

    void Update()
    {
        if (Input.GetKeyDown(teclaAlternar)) mostrarPanel = !mostrarPanel;

        if (!agenteAleatorio || Entorno() == null) return;

        temporizador += Time.deltaTime;
        if (temporizador < segundosEntreAcciones) return;
        temporizador = 0f;

        int n = Entorno().NumeroDeAcciones();
        Lanzar(Random.Range(0, n));

        if (Entorno().PartidaTerminada())
        {
            RegistrarResumenEpisodio();
            ReiniciarContadores();
            Entorno().ReiniciarEpisodio();
        }
    }

    void Lanzar(int accion)
    {
        if (Entorno() == null) return;

        int n = Entorno().NumeroDeAcciones();
        if (vecesLanzada == null || vecesLanzada.Length != n)
        {
            vecesLanzada = new int[n];
            vecesValida = new int[n];
        }

        bool ok = Entorno().EjecutarAccion(accion);
        ultimaAccion = Entorno().ultimoResultado;

        if (accion >= 0 && accion < n)
        {
            vecesLanzada[accion]++;
            if (ok) vecesValida[accion]++;
        }

        // Consumimos la recompensa igual que haría un agente en cada step
        recompensaTotalEpisodio += Entorno().ConsumirRecompensa();
        pasosEpisodio++;
    }

    // Vuelca un resumen legible del episodio: resultado, recompensa y qué tal
    // se ha portado cada acción. Es mucho más útil que registrar miles de líneas.
    void RegistrarResumenEpisodio()
    {
        string resultado = Entorno().AgenteHaGanado() ? "VICTORIA"
                         : (Entorno().HuboEmpate() ? "empate" : "DERROTA");

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Episodio terminado ({resultado}). Recompensa: {recompensaTotalEpisodio:F3} en {pasosEpisodio} pasos.");

        if (vecesLanzada != null)
        {
            string[] nombres = System.Enum.GetNames(typeof(AccionRL));
            sb.AppendLine("   RESUMEN de acciones (válidas / lanzadas):");

            for (int i = 0; i < vecesLanzada.Length; i++)
            {
                if (vecesLanzada[i] == 0) continue;

                // Marcamos las que NUNCA funcionaron: son las sospechosas
                string aviso = (vecesValida[i] == 0) ? "   <-- nunca válida" : "";
                string nombre = (i < nombres.Length) ? nombres[i] : ("accion" + i);
                sb.AppendLine($"   {i,2}. {nombre}: {vecesValida[i]}/{vecesLanzada[i]}{aviso}");
            }
        }

        Debug.Log(sb.ToString());
    }

    void ReiniciarContadores()
    {
        recompensaTotalEpisodio = 0f;
        pasosEpisodio = 0;

        if (vecesLanzada != null)
        {
            for (int i = 0; i < vecesLanzada.Length; i++) { vecesLanzada[i] = 0; vecesValida[i] = 0; }
        }
    }

    void OnGUI()
    {
        if (!mostrarPanel) return;

        // Si no encontramos el entorno, avisamos EN PANTALLA en lugar de
        // desaparecer sin más: una herramienta de depuración nunca debe
        // dejar de mostrarse en silencio.
        if (Entorno() == null)
        {
            GUILayout.BeginArea(new Rect(10, 10, 330, 90), GUI.skin.box);
            GUILayout.Label("<b>ENTORNO RL</b>", EstiloTitulo());
            GUILayout.Label("No encuentro ningún EntornoRL en la escena.");
            GUILayout.Label("Revisa que el objeto esté activo y que");
            GUILayout.Label("tenga el componente EntornoRL añadido.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 330, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        AreaPartida areaPanel = Entorno().MiArea();
        string idPanel = (areaPanel != null) ? areaPanel.idArea.ToString() : "?";
        GUILayout.Label($"<b>ENTORNO RL — ÁREA {idPanel}</b>", EstiloTitulo());
        GUILayout.Label($"F1 oculta este panel · 1-4 cambia de área");
        GUILayout.Space(6);

        // --- Lo que está haciendo el agente (útil al ver jugar un modelo) ---
        // ultimoResultado lo escribe el entorno en CADA acción, venga del
        // agente entrenado o de los botones de abajo.
        GUILayout.Label("<b>Agente</b>", EstiloTitulo());
        GUILayout.Label($"Última acción: {Entorno().ultimoResultado}");

        int[] lanzadas = Entorno().AccionesLanzadasEpisodio();
        int[] validas = Entorno().AccionesValidasEpisodio();
        string[] nombresAcc = System.Enum.GetNames(typeof(AccionRL));
        GUILayout.Label("Partida en curso (válidas / elegidas):");
        for (int i = 0; i < lanzadas.Length; i++)
        {
            if (lanzadas[i] == 0) continue;
            string nombre = (i < nombresAcc.Length) ? nombresAcc[i] : ("accion" + i);
            GUILayout.Label($"   {i,2}. {nombre}: {validas[i]}/{lanzadas[i]}");
        }
        GUILayout.Space(6);

        // --- Botones de acción ---
        GUILayout.Label("<b>Acciones</b>", EstiloTitulo());
        string[] nombres = System.Enum.GetNames(typeof(AccionRL));
        for (int i = 0; i < nombres.Length; i++)
        {
            if (GUILayout.Button($"{i}. {nombres[i]}")) Lanzar(i);
        }

        GUILayout.Space(8);
        GUILayout.Label($"Última (botones): {ultimaAccion}");

        // --- Recompensa ---
        GUILayout.Space(8);
        GUILayout.Label("<b>Recompensa</b>", EstiloTitulo());
        GUILayout.Label($"Acumulada episodio: {recompensaTotalEpisodio:F4}");
        GUILayout.Label($"Pendiente: {Entorno().RecompensaPendiente():F4}");
        GUILayout.Label($"Pasos: {pasosEpisodio}");

        // --- Observaciones ---
        GUILayout.Space(8);
        GUILayout.Label("<b>Observaciones</b>", EstiloTitulo());
        float[] obs = Entorno().ObtenerObservaciones();
        string[] etiquetas = {
            "madera","piedra","oro",
            "pobActual","pobMax","nivel",
            "lenadores","mineros","tesoreros","OCIOSOS","caballeros","arqueros","gladiadores",
            "campamento","galeria","gremio","coliseo",
            "saludPropia","saludEnemiga","tropasEnem","trabajEnem","tiempo"
        };

        for (int i = 0; i < obs.Length; i++)
        {
            string nombre = (i < etiquetas.Length) ? etiquetas[i] : "obs" + i;
            GUILayout.Label($"[{i}] {nombre}: {obs[i]:F3}");
        }

        // --- Episodio ---
        GUILayout.Space(8);
        GUILayout.Label("<b>Episodio</b>", EstiloTitulo());
        GUILayout.Label($"Terminado: {Entorno().PartidaTerminada()}");

        if (GUILayout.Button("Reiniciar episodio"))
        {
            ReiniciarContadores();
            Entorno().ReiniciarEpisodio();
        }

        agenteAleatorio = GUILayout.Toggle(agenteAleatorio, " Agente aleatorio");

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    GUIStyle EstiloTitulo()
    {
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.richText = true;
        return s;
    }
}