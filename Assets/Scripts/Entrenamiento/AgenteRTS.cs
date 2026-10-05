using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

// ============================================================================
// AGENTE DE APRENDIZAJE POR REFUERZO
// Puente entre ML-Agents y el EntornoRL del juego. Es deliberadamente delgado:
// toda la lógica (qué observa, qué puede hacer y cuánto vale) vive en EntornoRL,
// que además funciona sin ML-Agents y se validó por separado. Así el algoritmo
// de aprendizaje es intercambiable sin tocar el juego.
//
//   OnEpisodeBegin    -> reinicia la partida
//   CollectObservations -> entrega el vector de estado
//   WriteDiscreteActionMask -> bloquea las acciones imposibles en este momento
//   OnActionReceived  -> ejecuta la macroacción y cobra la recompensa
//
// CONFIGURACIÓN en Behavior Parameters (componente que acompaña a este):
//   Vector Observation > Space Size .......... 22
//   Actions > Discrete Branches .............. 1
//   Actions > Branch 0 Size .................. 20
// ============================================================================

public class AgenteRTS : Agent
{
    [Header("Entorno")]
    // Si se deja vacío se busca en la misma área de partida
    public EntornoRL entorno;

    [Header("Ritmo de Decisión")]
    // Cada cuánto tiempo DE JUEGO toma una decisión. Con 1 segundo, una partida
    // de seis minutos son unos 360 pasos. Usamos temporizador propio en lugar
    // del DecisionRequester para que el ritmo no dependa de la física y coincida
    // con el que se usó al validar el entorno.
    public float segundosEntreDecisiones = 1f;

    [Header("Máscara de Acciones")]
    // Antes de cada decisión se bloquean las acciones imposibles (sin recursos,
    // sin población, sin ejército...) y la red solo elige entre las posibles.
    // Desactivarlo reproduce los entrenamientos anteriores al entorno v5.
    public bool usarMascara = true;

    [Header("Diagnóstico")]
    // Escribe una línea por partida con el resultado y el recuento de acciones.
    // Empieza por "AgenteRL" para poder filtrarla en RegistroLog.
    public bool registrarResumenEnConsola = true;

    private float temporizador;
    private bool episodioListo = false;
    private bool primerEpisodio = true;

    // Claves de TensorBoard, calculadas una sola vez
    private static string[] nombresAcciones;
    private static string[] clavesFrecuencia;
    private static string[] clavesInvalidez;

    public override void Initialize()
    {
        LocalizarEntorno();
    }

    void LocalizarEntorno()
    {
        if (entorno != null) return;

        AreaPartida area = AreaPartida.Resolver(this);
        if (area != null) entorno = area.Entorno();

        if (entorno == null)
            Debug.LogError("AgenteRTS: no encuentro el EntornoRL de mi área.");
    }

    // ------------------------------------------------------------------
    // INICIO DE EPISODIO
    // Lo llama ML-Agents al empezar y después de cada EndEpisode()
    // ------------------------------------------------------------------
    public override void OnEpisodeBegin()
    {
        LocalizarEntorno();
        if (entorno == null) return;

        // El PRIMER episodio no se reinicia: la escena acaba de cargarse y el
        // mapa todavía se está generando. Reiniciar aquí pillaría al juego a
        // medio construir. A partir del segundo sí, porque venimos de EndEpisode.
        if (primerEpisodio) primerEpisodio = false;
        else entorno.ReiniciarEpisodio();

        temporizador = 0f;
        episodioListo = true;
    }

    // ------------------------------------------------------------------
    // OBSERVACIONES
    // El vector lo construye el entorno; aquí solo se entrega.
    // ------------------------------------------------------------------
    public override void CollectObservations(VectorSensor sensor)
    {
        if (entorno == null)
        {
            // Si algo va mal, entregamos ceros para no romper el tamaño esperado
            for (int i = 0; i < EntornoRL.NUM_OBSERVACIONES; i++) sensor.AddObservation(0f);
            return;
        }

        float[] observaciones = entorno.ObtenerObservaciones();
        foreach (float valor in observaciones) sensor.AddObservation(valor);
    }

    // ------------------------------------------------------------------
    // MÁSCARA
    // ML-Agents la pide justo antes de cada decisión. Las acciones bloqueadas
    // reciben probabilidad 0 y la red reparte la probabilidad entre el resto.
    // Sin ella, el agente usaba acciones imposibles como forma de "esperar"
    // (81 % de decisiones inválidas en ppo_v4_gamma998).
    // ------------------------------------------------------------------
    public override void WriteDiscreteActionMask(IDiscreteActionMask mascara)
    {
        if (!usarMascara || entorno == null) return;

        bool[] posibles = entorno.AccionesPosibles();
        for (int i = 0; i < posibles.Length; i++)
        {
            if (!posibles[i]) mascara.SetActionEnabled(0, i, false);
        }
    }

    // ------------------------------------------------------------------
    // ACCIÓN
    // Una macroacción discreta por decisión.
    // ------------------------------------------------------------------
    public override void OnActionReceived(ActionBuffers acciones)
    {
        if (entorno == null) return;

        int accion = acciones.DiscreteActions[0];
        entorno.EjecutarAccion(accion);

        // La recompensa la calcula el entorno por diferencia respecto al paso
        // anterior, e incluye la de victoria o derrota cuando corresponde.
        AddReward(entorno.ConsumirRecompensa());

        if (entorno.PartidaTerminada())
        {
            // Antes de cerrar, publicamos cómo ha ido la partida. Hay que hacerlo
            // aquí: al terminar el episodio se reinicia y el resultado se pierde.
            PublicarEstadisticas();

            episodioListo = false;
            EndEpisode(); // esto disparará OnEpisodeBegin()
        }
    }

    // ------------------------------------------------------------------
    // ESTADÍSTICAS DE JUEGO EN TENSORBOARD
    // Aparecen en su propia sección "Juego", junto a las de recompensa.
    // Se promedian en cada periodo de resumen, así que los valores 0/1 del
    // resultado se convierten en TASAS (p. ej. porcentaje de victorias).
    // ------------------------------------------------------------------
    void PublicarEstadisticas()
    {
        EntornoRL.ResumenEpisodio r = entorno.ObtenerResumen();
        StatsRecorder stats = Academy.Instance.StatsRecorder;

        stats.Add("Juego/TasaVictoria", r.victoria ? 1f : 0f);
        stats.Add("Juego/TasaEmpate", r.empate ? 1f : 0f);
        stats.Add("Juego/TasaRuina", r.ruina ? 1f : 0f);
        stats.Add("Juego/TasaDerrotaCombate", r.derrotaEnCombate ? 1f : 0f);

        stats.Add("Juego/RecursosIngresados", r.recursos);
        stats.Add("Juego/NivelMaximo", r.nivelMaximo);
        stats.Add("Juego/EdificiosConstruidos", r.edificios);
        stats.Add("Juego/UnidadesPerdidas", r.unidadesPerdidas);
        stats.Add("Juego/AtaquesLanzados", r.ataquesLanzados);
        stats.Add("Juego/DanoInfligidoCastillo", r.danoInfligidoFraccion);

        // --- Diagnóstico de la política ---
        stats.Add("Juego/TasaAccionesInvalidas", r.tasaInvalidas);
        stats.Add("Juego/RecolectoresMaximo", r.recolectoresMax);
        stats.Add("Juego/UnidadesMaximo", r.unidadesMax);
        stats.Add("Juego/DesfasePoblacion", r.desfasePoblacionMax); // debe ser 0
        stats.Add("Juego/FraccionConOciosos", r.fraccionConOciosos);

        if (r.decisiones > 0 && r.accionesLanzadas != null)
        {
            PrepararClaves(r.accionesLanzadas.Length);

            for (int i = 0; i < r.accionesLanzadas.Length; i++)
            {
                // Sección "Acciones": fracción de decisiones que fueron esta acción
                stats.Add(clavesFrecuencia[i], (float)r.accionesLanzadas[i] / r.decisiones);

                // Sección "AccionesInvalidas": de las veces que la eligió, cuántas
                // fallaron. Solo se anota si la eligió, para no promediar ceros falsos.
                if (r.accionesLanzadas[i] > 0)
                    stats.Add(clavesInvalidez[i], 1f - (float)r.accionesValidas[i] / r.accionesLanzadas[i]);
            }
        }

        if (registrarResumenEnConsola) Debug.Log(TextoResumen(r));
    }

    static void PrepararClaves(int n)
    {
        if (clavesFrecuencia != null && clavesFrecuencia.Length == n) return;

        nombresAcciones = System.Enum.GetNames(typeof(AccionRL));
        clavesFrecuencia = new string[n];
        clavesInvalidez = new string[n];

        for (int i = 0; i < n; i++)
        {
            string nombre = (i < nombresAcciones.Length) ? nombresAcciones[i] : ("Accion" + i);
            clavesFrecuencia[i] = $"Acciones/{i:00}_{nombre}";
            clavesInvalidez[i] = $"AccionesInvalidas/{i:00}_{nombre}";
        }
    }

    // Una sola línea por partida, fácil de buscar y de copiar a una tabla
    string TextoResumen(EntornoRL.ResumenEpisodio r)
    {
        string resultado = r.victoria ? "VICTORIA"
                         : r.empate ? "EMPATE"
                         : r.ruina ? "RUINA"
                         : "DERROTA";

        AreaPartida area = entorno.MiArea();
        string nombreArea = (area != null) ? area.name : "?";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"AgenteRL [{nombreArea}] {resultado} | {r.decisiones} pasos | ");
        sb.Append($"recompensa {GetCumulativeReward():F3} | inválidas {r.tasaInvalidas:P0} | ");
        sb.Append($"recolectores máx {r.recolectoresMax}, con ociosos {r.fraccionConOciosos:P0} | ");
        sb.Append($"recursos {r.recursos}, nivel {r.nivelMaximo}, edificios {r.edificios}, ");
        sb.Append($"ataques {r.ataquesLanzados}, bajas {r.unidadesPerdidas} | acciones (válidas/elegidas):");

        if (r.accionesLanzadas != null)
        {
            PrepararClaves(r.accionesLanzadas.Length);
            for (int i = 0; i < r.accionesLanzadas.Length; i++)
            {
                if (r.accionesLanzadas[i] == 0) continue;
                sb.Append($" {i}.{nombresAcciones[i]} {r.accionesValidas[i]}/{r.accionesLanzadas[i]};");
            }
        }
        sb.Append($" | control población: unidades máx {r.unidadesMax}, desfase {r.desfasePoblacionMax}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // MODO MANUAL
    // Se usa cuando el comportamiento está en Heuristic Only: permite probar
    // el agente sin entrenar. Elige al azar entre las acciones posibles (o
    // entre todas, si la máscara está desactivada).
    // ------------------------------------------------------------------
    public override void Heuristic(in ActionBuffers accionesFuera)
    {
        var discretas = accionesFuera.DiscreteActions;
        if (entorno == null) { discretas[0] = 0; return; }

        if (!usarMascara)
        {
            discretas[0] = Random.Range(0, entorno.NumeroDeAcciones());
            return;
        }

        bool[] posibles = entorno.AccionesPosibles();
        System.Collections.Generic.List<int> candidatas = new System.Collections.Generic.List<int>();
        for (int i = 0; i < posibles.Length; i++)
            if (posibles[i]) candidatas.Add(i);

        discretas[0] = candidatas[Random.Range(0, candidatas.Count)]; // NoHacerNada siempre está
    }

    // ------------------------------------------------------------------
    // RITMO
    // Pedimos decisión cada N segundos de juego. Al acelerar el tiempo durante
    // el entrenamiento, las decisiones se aceleran con él automáticamente.
    // ------------------------------------------------------------------
    void FixedUpdate()
    {
        if (!episodioListo || entorno == null) return;

        temporizador += Time.fixedDeltaTime;
        if (temporizador < segundosEntreDecisiones) return;

        temporizador = 0f;
        RequestDecision();
    }
}