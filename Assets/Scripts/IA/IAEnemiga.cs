using UnityEngine;
using System.Collections.Generic;

public enum FaseEstrategia
{
    Economia,
    Construccion,
    PreparacionMilitar,
    Expedicion,
    Asedio
}

// Dónde se coloca cada tipo de tropa en la formación de asalto.
public enum RolFormacion
{
    Frente,      // Delante (p. ej. gladiadores)
    Centro,      // En medio (p. ej. caballeros)
    Retaguardia  // Detrás (p. ej. arqueros)
}

// Cada entrada describe UN edificio que la IA construirá y cuántas tropas sacará de él.
// Se rellena desde el Inspector. Añadir un edificio = añadir una fila a la lista.
[System.Serializable]
public class PlanEdificio
{
    public string nombre = "Edificio";        // Solo para que te aclares en el Inspector
    public GameObject prefabEdificio;          // El prefab del edificio a construir
    public int tropasAReclutar = 2;            // Cuántas unidades saca de él
    public bool tropasSonMilitares = false;    // true = se unen al ejército de asedio; false = economía
    public RolFormacion rolFormacion = RolFormacion.Frente; // Dónde van sus tropas en el asalto (si son militares)

    // --- Estado en tiempo de ejecución (no se toca en el Inspector) ---
    [System.NonSerialized] public EdificioReclutamiento edificioConstruido;

    // Tropas VIVAS salidas de este edificio. Si mueren, salen de la lista y se reponen.
    [System.NonSerialized] public List<GameObject> tropasVivas = new List<GameObject>();
    // Máximo que llegó a tener: solo reponemos hasta aquí, para no adelantar fases.
    [System.NonSerialized] public int pico;

    // Cuenta las que siguen vivas, limpiando de paso las que Unity ya destruyó
    public int Vivas()
    {
        if (tropasVivas == null) tropasVivas = new List<GameObject>();
        tropasVivas.RemoveAll(t => t == null);
        return tropasVivas.Count;
    }

    public void Registrar(GameObject tropa)
    {
        if (tropasVivas == null) tropasVivas = new List<GameObject>();
        tropasVivas.Add(tropa);
        if (tropasVivas.Count > pico) pico = tropasVivas.Count;
    }
}

public class IAEnemiga : MonoBehaviour
{
    // ---- Trazas etiquetadas con el área ----
    // Con varias partidas en paralelo, sin esta etiqueta los mensajes de las
    // cuatro IAs se mezclarían en la consola y sería imposible seguir cada una.
    string EtiquetaArea()
    {
        AreaPartida area = AreaPartida.Resolver(this);
        return (area != null) ? $"[A{area.idArea}] " : "";
    }

    void LogIA(string mensaje) { Debug.Log(EtiquetaArea() + mensaje); }
    void AvisoIA(string mensaje) { Debug.LogWarning(EtiquetaArea() + mensaje); }

    private BaseMilitar miBase;

    [Header("Estrategia Actual")]
    public FaseEstrategia faseActual = FaseEstrategia.Economia;

    [Header("Condiciones de Cambio de Fase")]
    public int limiteRecolectores = 4; // Leñadores antes de empezar a construir
    public int cantidadCaballeros = 4; // Caballeros (básicos, del castillo) que forman la base del ejército

    [Header("Plan de Construcción")]
    // El orden de la lista es el orden en que los construye (de arriba a abajo).
    public List<PlanEdificio> planConstruccion = new List<PlanEdificio>();

    [Header("Guarnición (anti-rush)")]
    // Caballeros que la IA mantiene desde el PRINCIPIO para no estar indefensa.
    // NO son tropas extra: se descuentan de 'cantidadCaballeros', así que solo
    // adelanta parte de su ejército. Sin esto, atacarla pronto gana siempre,
    // y un agente entrenado descubriría ese atajo y no aprendería nada más.
    public int guarnicionMinima = 2;

    [Header("Reacción: Defensa de la Base")]
    // Si detecta enemigos cerca de casa, abandona lo que esté haciendo y vuelve a defender.
    public bool defenderLaBase = true;
    public float radioDefensaBase = 22f;   // Qué se considera "estar en mi territorio"
    public float radioLlamadaTropas = 60f; // Desde cuán lejos vuelven las tropas a defender

    [Header("Reacción: Retirada")]
    // Si pierde buena parte del ejército durante el asalto, se retira y rehace fuerzas
    // en vez de seguir mandando soldados a morir de uno en uno.
    public bool retirarseSiPierde = true;
    [Range(0f, 1f)] public float umbralRetirada = 0.4f; // % del ejército por debajo del cual se retira

    [Header("Variabilidad (para que no juegue siempre igual)")]
    // Pequeñas variaciones aleatorias por partida: hacen al rival menos predecible
    // y evitan que el agente aprenda a explotar UNA secuencia exacta.
    public bool comportamientoVariable = true;
    public int variacionExpediciones = 1;      // ± expediciones
    public int variacionTropasAtacar = 1;      // ± tropas mínimas para atacar
    [Range(0f, 0.5f)] public float variacionRitmo = 0.2f; // ± % en el tiempo entre decisiones

    [Header("Anti-bloqueo (¡clave para el entrenamiento!)")]
    // Si una fase se atasca (sin recursos, sin hueco, sin poder construir...),
    // la IA avanza igualmente en vez de quedarse esperando para siempre.
    public int ciclosMaximosPorFase = 20;
    // Ahorrar para un edificio caro NO es estar atascado. Mientras la IA esté
    // esperando recursos le damos mucho más margen antes de saltarse el edificio.
    public int ciclosMaximosAhorrando = 70;
    // Pase lo que pase, a partir de este momento ataca con lo que tenga.
    // Garantiza que la partida SIEMPRE se resuelve y nunca hay tablas eternas.
    public float segundosHastaAtaqueForzoso = 240f;
    // Si el asalto forzoso fracasa y rehace ejército, vuelve a lanzarse pasado este tiempo
    public float segundosEntreAtaquesForzosos = 60f;

    [Header("Dificultad (0 = igualdad total con el jugador)")]
    // Palanca única para calibrar al rival durante el entrenamiento.
    // Déjalo a 0 para partidas simétricas; súbelo para un rival más duro.
    // Varíalo entre episodios para que el agente no se aprenda a UN solo rival.
    public int maderaExtraInicio = 0;
    public int piedraExtraInicio = 0;
    public int oroExtraInicio = 0;

    [Header("Subida de Nivel")]
    public bool subirNivelAutomatico = true; // La IA mejora su castillo para ampliar población y desbloquear edificios
    public int nivelObjetivo = 5;            // Nivel objetivo (con población base 8: nivel 5 = 16 huecos)

    [Header("Tiempos")]
    public float segundosEntreDecisiones = 3f; // Bajo para pruebas (sube a 10-15 en la versión final)

    [Header("Expediciones (antes de atacar)")]
    public int numeroExpediciones = 3;     // Cuántas salidas de exploración hace antes del asalto
    public int tropasPorExpedicion = 2;    // Cuántas tropas salen en cada partida (el resto se queda en el castillo)
    public float radioExpedicion = 25f;    // Hasta dónde se alejan a explorar
    public int minTropasParaAtacar = 4;    // Ejército mínimo para lanzar el asalto

    [Header("Formación de Asalto")]
    public RolFormacion rolCaballeros = RolFormacion.Centro; // Dónde van los caballeros del castillo
    public float distanciaPreparacion = 12f; // A qué distancia de la base enemiga se reúnen antes de cargar
    public float separacionFilas = 3f;       // Distancia entre la fila de frente, centro y retaguardia
    public float espaciadoLateral = 1.5f;    // Separación entre tropas de una misma fila
    public int ciclosParaCargar = 2;         // Ciclos de decisión que pasan formándose antes de cargar

    [Header("Construcción Procedimental")]
    public float radioConstruccionMin = 3f;  // Distancia MÍNIMA: no pegar edificios al castillo
    public float radioConstruccion = 10f;    // Distancia MÁXIMA desde el castillo (antes 15)
    public int cantidadEdificiosMaximos = 6;
    public float distanciaEntreEdificios = 5f;
    [Space(5)]
    public LayerMask capaObstaculos;     // Árboles, rocas... (la MISMA capa que usas en SistemaConstruccion)
    public float radioLibre = 2.5f;      // Espacio despejado que se exige alrededor de cada edificio
    public float correccionGradosY = 0f; // Giro de los edificios. Si las puertas salen al revés, prueba 180

    // Coordenadas donde la IA puede levantar edificios
    private List<Vector3> puntosDeAnclaje = new List<Vector3>();
    private List<bool> anclajesOcupados = new List<bool>();

    // Progreso del plan
    private int planActual = 0; // En qué edificio de la lista vamos

    // Unidades VIVAS del castillo. Si mueren salen de la lista y se reponen.
    private List<GameObject> misLeñadores = new List<GameObject>();
    private List<GameObject> misCaballeros = new List<GameObject>();
    // Máximos alcanzados: solo reponemos hasta aquí, para no adelantar fases.
    private int picoLeñadores = 0;
    private int picoCaballeros = 0;

    // Cada miembro del ejército guarda su objeto y su rol de formación
    private class MiembroEjercito
    {
        public GameObject objeto;
        public RolFormacion rol;
        public MiembroEjercito(GameObject o, RolFormacion r) { objeto = o; rol = r; }
    }
    private List<MiembroEjercito> miEjercito = new List<MiembroEjercito>();

    // Estado de las expediciones
    private int expedicionesHechas = 0;
    private int indiceExpedicion = 0;            // Para rotar qué tropas salen cada vez
    private List<GameObject> partidaFuera = new List<GameObject>(); // Tropas que están explorando ahora

    // Estado del asalto en formación
    private bool enFormacion = false;
    private bool cargando = false;
    private int ciclosEnFormacion = 0;

    // Control de bloqueos
    private FaseEstrategia faseRegistrada;
    private int ciclosEnFase = 0;
    private float tiempoDePartida = 0f;
    private float proximoAtaqueForzoso = 240f;
    private bool ahorrandoParaEdificio = false; // true = solo le faltan recursos
    private int planVigilado = -1;              // edificio del que estamos midiendo paciencia
    private float saludEnemigaVigilada = -1f;   // para saber si el asalto progresa
    private float intervaloReal = 3f; // el ritmo realmente usado (con su variación)

    // Reacción: defensa y retirada
    [System.NonSerialized] public bool defendiendoAhora = false; // true mientras hay invasores
    private int picoEjercito = 0; // Mayor tamaño que llegó a tener el ejército

    // Valores "de esta partida" (los del Inspector con su variación aleatoria aplicada)
    private int expedicionesObjetivo;
    private int tropasMinimasAtacar;

    void Start()
    {
        miBase = GetComponent<BaseMilitar>();
        GenerarAnclajesAleatorios();

        if (miBase != null)
        {
            AplicarVentajaInicial();
            AplicarVariabilidad();
            Invoke("SpawnInicial", 1f);
        }
    }

    // Suma la ventaja configurada. Con todo a 0 la IA juega exactamente
    // con las mismas reglas y los mismos recursos que el jugador.
    void AplicarVentajaInicial()
    {
        if (miBase == null) return;
        if (maderaExtraInicio == 0 && piedraExtraInicio == 0 && oroExtraInicio == 0) return;

        miBase.madera += maderaExtraInicio;
        miBase.piedra += piedraExtraInicio;
        miBase.oro += oroExtraInicio;

        LogIA($"IAEnemiga: ventaja inicial aplicada (M:{maderaExtraInicio} P:{piedraExtraInicio} O:{oroExtraInicio}).");
    }

    // ====================================================================
    // REINICIO DEL COMANDANTE: vuelve al estado de arranque y re-lanza su ciclo.
    // Lo llama el GestorPartida al reiniciar la partida.
    // ====================================================================
    public void ReiniciarComandante()
    {
        if (miBase == null) miBase = GetComponent<BaseMilitar>();

        CancelInvoke(); // paramos los timers viejos (SpawnInicial y las decisiones)

        // Volvemos al principio de la estrategia
        faseActual = FaseEstrategia.Economia;
        planActual = 0;

        misLeñadores.Clear();
        misCaballeros.Clear();
        picoLeñadores = 0;
        picoCaballeros = 0;
        miEjercito.Clear();

        // Estado de expediciones y asalto
        expedicionesHechas = 0;
        indiceExpedicion = 0;
        partidaFuera.Clear();
        enFormacion = false;
        cargando = false;
        ciclosEnFormacion = 0;

        // Estado reactivo (defensa y retirada)
        defendiendoAhora = false;
        picoEjercito = 0;
        planVigilado = -1;
        saludEnemigaVigilada = -1f;
        ahorrandoParaEdificio = false;

        // Reseteamos el estado en tiempo de ejecución de cada entrada del plan
        foreach (PlanEdificio p in planConstruccion)
        {
            p.edificioConstruido = null; // los viejos ya no existen
            if (p.tropasVivas == null) p.tropasVivas = new List<GameObject>();
            p.tropasVivas.Clear();
            p.pico = 0;
        }

        // Liberamos todos los huecos de construcción (sus edificios fueron destruidos)
        for (int i = 0; i < anclajesOcupados.Count; i++) anclajesOcupados[i] = false;

        // Re-armamos el ciclo desde cero, con una personalidad nueva para este episodio
        AplicarVentajaInicial();
        AplicarVariabilidad();
        Invoke("SpawnInicial", 1f);
    }

    void SpawnInicial()
    {
        for (int i = 0; i < 2; i++)
        {
            GameObject leñador = miBase.ComprarLeñador();
            if (leñador != null) RegistrarLeñador(leñador);
        }
    }

    void TomarDecisionEstrategica()
    {
        // Contamos tiempo y cuánto llevamos en la misma fase (para detectar bloqueos)
        tiempoDePartida += intervaloReal;
        if (faseActual != faseRegistrada) { faseRegistrada = faseActual; ciclosEnFase = 0; }
        else ciclosEnFase++;

        // En construcción la paciencia se mide POR EDIFICIO, no por fase entera:
        // si no, levantar cuatro edificios agota el contador y se salta los últimos.
        if (faseActual == FaseEstrategia.Construccion && planActual != planVigilado)
        {
            planVigilado = planActual;
            ciclosEnFase = 0;
        }

        GestionarBloqueos();

        // El ejército reacciona a las invasiones, pero la ECONOMÍA NO SE DETIENE:
        // un jugador defiende Y sigue produciendo al mismo tiempo. Si esto cortara
        // el ciclo, bastaría con merodear cerca de su base para paralizarla entera.
        bool invasion = GestionarDefensa();

        // PRIORIDAD 2: reponer las bajas. Si el jugador le mata los leñadores o los
        // mineros, la IA los vuelve a fabricar en lugar de quedarse sin economía.
        // Se hace en TODAS las fases, no solo en la que los creó.
        if (ReponerBajas()) return;

        // PRIORIDAD 3: mantener la guarnición mínima. Desde el primer minuto tiene
        // un par de caballeros en casa, así que atacarla pronto ya no es gratis.
        if (GestionarGuarnicion()) return;

        // PRIORIDAD 4: invertir en subir de nivel hacia el objetivo (si puede pagarlo).
        // Esto hace crecer su población y le desbloquea edificios, como a un jugador.
        GestionarSubidaNivel();

        // Mientras defiende su casa no manda al ejército fuera (ni expedición ni asalto),
        // pero sí sigue con economía, construcción y reclutamiento.
        if (invasion && (faseActual == FaseEstrategia.Expedicion || faseActual == FaseEstrategia.Asedio))
            return;

        switch (faseActual)
        {
            case FaseEstrategia.Economia:
                LogicaEconomia();
                break;

            case FaseEstrategia.Construccion:
                LogicaConstruccion();
                break;

            case FaseEstrategia.PreparacionMilitar:
                LogicaPreparacion();
                break;

            case FaseEstrategia.Expedicion:
                LogicaExpedicion();
                break;

            case FaseEstrategia.Asedio:
                LogicaAsedio();
                break;
        }
    }

    // ====================================================================
    // SUBIDA DE NIVEL: la IA mejora su castillo igual que el jugador, lo que
    // amplía su población máxima y le desbloquea edificios de mayor nivel.
    // Es reactiva: sube cuando le hace falta y solo si puede pagarlo.
    // ====================================================================

    // ====================================================================
    // UNIDADES VIVAS Y REPOSICIÓN DE BAJAS
    // Los contadores no son "cuántas he fabricado" sino "cuántas siguen vivas".
    // Así, si el enemigo le mata trabajadores o soldados, la IA los repone en
    // lugar de quedarse tullida para el resto de la partida.
    // ====================================================================

    int LeñadoresVivos()
    {
        misLeñadores.RemoveAll(t => t == null);
        return misLeñadores.Count;
    }

    int CaballerosVivos()
    {
        misCaballeros.RemoveAll(t => t == null);
        return misCaballeros.Count;
    }

    void RegistrarLeñador(GameObject g)
    {
        if (g == null) return;
        misLeñadores.Add(g);
        if (misLeñadores.Count > picoLeñadores) picoLeñadores = misLeñadores.Count;
    }

    void RegistrarCaballero(GameObject g)
    {
        if (g == null) return;
        misCaballeros.Add(g);
        if (misCaballeros.Count > picoCaballeros) picoCaballeros = misCaballeros.Count;
        miEjercito.Add(new MiembroEjercito(g, rolCaballeros));
    }

    // Mantiene la guarnición mínima desde el primer minuto de partida.
    // Devuelve true si ha gastado el ciclo en reclutar un guardia.
    bool GestionarGuarnicion()
    {
        if (guarnicionMinima <= 0) return false;

        // Nunca más guardias que el total de caballeros previsto
        int objetivo = Mathf.Min(guarnicionMinima, cantidadCaballeros);
        if (CaballerosVivos() >= objetivo) return false;

        if (!HayHuecoPoblacion()) return false;

        GameObject guardia = miBase.ComprarCaballero();
        if (guardia == null) return false; // no le llega: seguirá con lo suyo

        RegistrarCaballero(guardia);
        LogIA($"IAEnemiga: guardia de la base reclutado ({CaballerosVivos()}/{objetivo}).");
        return true;
    }

    // Repone UNA baja por ciclo, con prioridad económica (sin madera no hay nada).
    // Solo repone hasta el máximo que llegó a tener, para no adelantarse a las fases.
    // Devuelve true si ha usado el ciclo en reponer.
    bool ReponerBajas()
    {
        if (miBase == null) return false;

        // 1. LEÑADORES: son la base de todo, la madera paga niveles, edificios y tropas
        int objetivoLeñadores = Mathf.Min(limiteRecolectores, picoLeñadores);
        if (LeñadoresVivos() < objetivoLeñadores)
        {
            if (!HayHuecoPoblacion()) return false;

            GameObject nuevo = miBase.ComprarLeñador();
            if (nuevo != null)
            {
                RegistrarLeñador(nuevo);
                LogIA($"IAEnemiga: repuesto LEÑADOR caído ({LeñadoresVivos()}/{limiteRecolectores}).");
                return true;
            }
            return false; // no le llega: ahorra, pero no bloquea el resto
        }

        // 2. TROPAS DE EDIFICIOS ECONÓMICOS (mineros, tesoreros): piedra y oro
        foreach (PlanEdificio plan in planConstruccion)
        {
            if (plan.tropasSonMilitares) continue;
            if (plan.edificioConstruido == null) continue;

            // En economía apuntamos SIEMPRE al objetivo completo (no al máximo
            // alcanzado): si un edificio se quedó sin dotar por falta de hueco,
            // se completa en cuanto haya población. Sin recursos no hay partida.
            int objetivo = plan.tropasAReclutar;
            if (plan.Vivas() < objetivo)
            {
                if (!HayHuecoPoblacion()) return false;

                GameObject nuevo = plan.edificioConstruido.ComprarTropa();
                if (nuevo != null)
                {
                    plan.Registrar(nuevo);
                    LogIA($"IAEnemiga: repuesto trabajador de '{plan.nombre}' ({plan.Vivas()}/{plan.tropasAReclutar}).");
                    return true;
                }
                return false;
            }
        }

        // 3. CABALLEROS
        int objetivoCaballeros = Mathf.Min(cantidadCaballeros, picoCaballeros);
        if (CaballerosVivos() < objetivoCaballeros)
        {
            if (!HayHuecoPoblacion()) return false;

            GameObject nuevo = miBase.ComprarCaballero();
            if (nuevo != null)
            {
                RegistrarCaballero(nuevo);
                LogIA($"IAEnemiga: repuesto CABALLERO caído ({CaballerosVivos()}/{cantidadCaballeros}).");
                return true;
            }
            return false;
        }

        // 4. TROPAS MILITARES DE EDIFICIOS (arqueros, gladiadores)
        foreach (PlanEdificio plan in planConstruccion)
        {
            if (!plan.tropasSonMilitares) continue;
            if (plan.edificioConstruido == null) continue;

            int objetivo = Mathf.Min(plan.tropasAReclutar, plan.pico);
            if (plan.Vivas() < objetivo)
            {
                if (!HayHuecoPoblacion()) return false;

                GameObject nuevo = plan.edificioConstruido.ComprarTropa();
                if (nuevo != null)
                {
                    plan.Registrar(nuevo);
                    miEjercito.Add(new MiembroEjercito(nuevo, plan.rolFormacion));
                    LogIA($"IAEnemiga: repuesto soldado de '{plan.nombre}' ({plan.Vivas()}/{plan.tropasAReclutar}).");
                    return true;
                }
                return false;
            }
        }

        return false; // no había bajas que reponer
    }

    // ====================================================================
    // ANTI-BLOQUEO
    // Ninguna fase puede eternizarse. Si la IA no puede progresar (sin recursos,
    // sin hueco de población, sin sitio donde construir...), pasa a lo siguiente.
    // Y pasado cierto tiempo ataca con lo que tenga, para que la partida SIEMPRE
    // se resuelva. Sin esto, un agente que no hace nada y una IA atascada darían
    // episodios infinitos y el entrenamiento no avanzaría.
    // ====================================================================
    void GestionarBloqueos()
    {
        // --- Ataque forzoso por tiempo ---
        // Se repite cada cierto tiempo: si tras un asalto fallido rehace el ejército,
        // volverá a lanzarse en vez de quedarse preparándose indefinidamente.
        if (tiempoDePartida >= proximoAtaqueForzoso && faseActual != FaseEstrategia.Asedio)
        {
            AvisoIA($"IAEnemiga: se acabó el tiempo de preparación. ¡ATAQUE FORZOSO con {miEjercito.Count} unidades!");
            faseActual = FaseEstrategia.Asedio;
            proximoAtaqueForzoso = tiempoDePartida + segundosEntreAtaquesForzosos;
            ciclosEnFase = 0;
            return;
        }

        // --- En Asedio ---
        if (faseActual == FaseEstrategia.Asedio)
        {
            miEjercito.RemoveAll(m => m.objeto == null);

            // Sin ejército no se asedia nada: a rehacerlo SIEMPRE (antes esto solo
            // valía antes del ataque forzoso, y después se quedaba muerta de pie).
            if (miEjercito.Count == 0)
            {
                LogIA("IAEnemiga: me he quedado sin ejército, vuelvo a reclutar.");
                enFormacion = false; cargando = false; ciclosEnFase = 0;
                faseActual = FaseEstrategia.PreparacionMilitar;
                return;
            }

            // ¿Está progresando el asalto? Si el castillo enemigo pierde vida,
            // NO está atascado aunque tarde: reiniciamos el contador de paciencia.
            BaseMilitar objetivoAsedio = BuscarBaseEnemiga();
            if (objetivoAsedio != null)
            {
                if (saludEnemigaVigilada < 0f) saludEnemigaVigilada = objetivoAsedio.salud;
                else if (objetivoAsedio.salud < saludEnemigaVigilada - 0.5f)
                {
                    saludEnemigaVigilada = objetivoAsedio.salud;
                    ciclosEnFase = 0; // avanza, le damos más tiempo
                    return;
                }
            }

            // El asalto también puede atascarse (tropas trabadas, formación imposible).
            // Si lleva demasiado sin resolverse, rehacemos formación y volvemos a cargar.
            if (ciclosEnFase >= ciclosMaximosPorFase)
            {
                AvisoIA("IAEnemiga: el asalto está atascado, reorganizo y vuelvo a cargar.");
                enFormacion = false; cargando = false; ciclosEnFormacion = 0; ciclosEnFase = 0;
            }
            return;
        }

        // --- Paciencia por fase ---
        if (ciclosEnFase < ciclosMaximosPorFase) return;

        // Si solo está esperando recursos o nivel, NO es un bloqueo: le damos
        // mucho más margen. Sin esto, con una economía lenta se saltaría todos
        // los edificios y acabaría atacando solo con caballeros.
        if (faseActual == FaseEstrategia.Construccion
            && ahorrandoParaEdificio
            && ciclosEnFase < ciclosMaximosAhorrando) return;

        ciclosEnFase = 0;

        switch (faseActual)
        {
            case FaseEstrategia.Economia:
                AvisoIA("IAEnemiga: economía atascada, paso a construir de todos modos.");
                faseActual = FaseEstrategia.Construccion;
                break;

            case FaseEstrategia.Construccion:
                // No puede pagar/colocar este edificio: lo salta y sigue con el plan
                AvisoIA($"IAEnemiga: no consigo avanzar con el edificio {planActual}, lo salto.");
                planActual++;
                if (planActual >= planConstruccion.Count)
                    faseActual = FaseEstrategia.PreparacionMilitar;
                break;

            case FaseEstrategia.PreparacionMilitar:
                AvisoIA($"IAEnemiga: no puedo completar el ejército ({miEjercito.Count}), salgo con lo que hay.");
                faseActual = FaseEstrategia.Expedicion;
                break;

            case FaseEstrategia.Expedicion:
                AvisoIA("IAEnemiga: expediciones atascadas, paso directamente al asalto.");
                faseActual = FaseEstrategia.Asedio;
                break;
        }
    }

    // ====================================================================
    // REACCIÓN 1: DEFENSA DE LA BASE
    // Si aparecen enemigos cerca de casa (atacando el castillo, los edificios
    // o los trabajadores), la IA abandona lo que esté haciendo y vuelve.
    // Es lo que más "humano" la hace: un jugador nunca ignora una invasión.
    // ====================================================================

    // Devuelve true si este ciclo se ha dedicado a defender.
    bool GestionarDefensa()
    {
        if (!defenderLaBase) return false;

        Tropa invasor = BuscarInvasorMasCercano();

        // --- No hay invasores: si estábamos defendiendo, volvemos a lo nuestro ---
        if (invasor == null)
        {
            if (defendiendoAhora)
            {
                defendiendoAhora = false;

                // Si estábamos asaltando, hay que rehacer la formación desde cero
                enFormacion = false;
                cargando = false;
                ciclosEnFormacion = 0;

                LogIA($"IAEnemiga: territorio despejado, retomando la fase {faseActual}.");
            }
            return false;
        }

        // --- Hay invasores: el ejército vuelve, pero la economía NO se detiene ---
        if (!defendiendoAhora)
        {
            defendiendoAhora = true;
            LogIA($"IAEnemiga: ¡ENEMIGOS EN LA BASE! Tropas a defender (sigo con {faseActual}).");
        }

        miEjercito.RemoveAll(m => m.objeto == null);

        // CONCENTRAR EL FUEGO: todos atacan al MISMO invasor (el más cercano al
        // castillo) en vez de repartirse. Matar rápido a uno reduce el daño que
        // recibimos; repartir golpes deja a todos los enemigos vivos pegando.
        // Es la táctica que usa cualquier jugador y la IA no la tenía.
        foreach (MiembroEjercito m in miEjercito)
        {
            if (m.objeto == null) continue;

            // Solo llamamos a los que estén dentro del radio de llamada
            float distancia = Vector3.Distance(m.objeto.transform.position, transform.position);
            if (distancia > radioLlamadaTropas) continue;

            Combate c = m.objeto.GetComponent<Combate>();
            if (c == null) continue;

            c.OrdenarAtaque(invasor.transform);
        }

        return true;
    }

    // Enemigo más cercano a MI CASTILLO dentro del radio de defensa
    Tropa BuscarInvasorMasCercano()
    {
        return BuscarInvasorEnRadio(transform.position, radioDefensaBase);
    }

    Tropa BuscarInvasorEnRadio(Vector3 centro, float radio)
    {
        Collider[] detectados = Physics.OverlapSphere(centro, radio);
        Tropa mejor = null;
        float distanciaMinima = Mathf.Infinity;

        foreach (Collider col in detectados)
        {
            Tropa t = col.GetComponent<Tropa>();
            if (t == null || miBase == null) continue;
            if (t.equipo == miBase.equipo) continue; // los míos no son invasores

            float d = Vector3.Distance(centro, t.transform.position);
            if (d < distanciaMinima)
            {
                distanciaMinima = d;
                mejor = t;
            }
        }
        return mejor;
    }

    // Cada ciclo intenta subir un nivel hacia el objetivo (de forma prioritaria).
    // MejorarCastillo solo gasta si puede pagarlo, así que si no le llega, ahorra.
    void GestionarSubidaNivel()
    {
        if (!subirNivelAutomatico) return;
        if (miBase.nivelCastillo >= nivelObjetivo) return; // ya llegó al objetivo

        // Mientras levanta su economía y su plan de construcción, NO sube de nivel
        // por iniciativa propia. Antes lo hacía siempre que podía pagarlo, y se
        // gastaba en un nivel que aún no necesitaba justo los recursos que le
        // faltaban para el edificio que estaba ahorrando: acababa saltándose
        // edificios. Durante el plan ya sube cuando hace falta, por dos vías:
        // cuando un edificio exige más nivel (TieneNivel) y cuando se queda sin
        // población (HayHuecoPoblacion).
        if (faseActual == FaseEstrategia.Economia || faseActual == FaseEstrategia.Construccion) return;

        IntentarSubirNivel();
    }

    // ¿Hay sitio en la población para reclutar? Si está llena, intenta subir de nivel.
    bool HayHuecoPoblacion()
    {
        if (miBase.poblacionActual < miBase.poblacionMaxima) return true;

        if (subirNivelAutomatico) IntentarSubirNivel();
        return miBase.poblacionActual < miBase.poblacionMaxima;
    }

    // ¿El castillo tiene el nivel necesario? Si no, intenta subir hasta alcanzarlo.
    bool TieneNivel(int nivelRequerido)
    {
        if (miBase.nivelCastillo >= nivelRequerido) return true;

        if (subirNivelAutomatico) IntentarSubirNivel();
        return miBase.nivelCastillo >= nivelRequerido;
    }

    // Llama a la mejora del castillo (que solo sube si la IA puede pagar el coste).
    // El mensaje de "EL ENEMIGO ha subido a Nivel X" lo escribe la propia BaseMilitar.
    void IntentarSubirNivel()
    {
        miBase.MejorarCastillo();
    }

    // --- FASE ECONOMÍA: leñadores desde el castillo ---
    void LogicaEconomia()
    {
        if (LeñadoresVivos() < limiteRecolectores)
        {
            // Si la población está llena, la IA sube de nivel para hacer hueco
            if (!HayHuecoPoblacion())
            {
                LogIA("IAEnemiga: población llena, ahorrando para mejorar el castillo...");
                return;
            }

            GameObject leñador = miBase.ComprarLeñador();
            if (leñador != null)
            {
                RegistrarLeñador(leñador);
                LogIA($"IAEnemiga: Leñador reclutado ({LeñadoresVivos()}/{limiteRecolectores}).");
            }
            else
            {
                LogIA("IAEnemiga: esperando recursos para leñador...");
            }
        }
        else
        {
            faseActual = FaseEstrategia.Construccion;
            LogIA("IAEnemiga: ¡Pasando a Fase de Construcción!");
        }
    }

    // --- FASE CONSTRUCCIÓN: recorre el plan de la lista ---
    void LogicaConstruccion()
    {
        // ¿Terminamos toda la lista?
        if (planActual >= planConstruccion.Count)
        {
            faseActual = FaseEstrategia.PreparacionMilitar;
            LogIA("IAEnemiga: Plan de construcción completo. ¡A lo militar!");
            return;
        }

        PlanEdificio plan = planConstruccion[planActual];

        // 1. Construir el edificio si aún no está
        if (plan.edificioConstruido == null)
        {
            if (plan.prefabEdificio == null)
            {
                AvisoIA($"IAEnemiga: '{plan.nombre}' no tiene prefab asignado, lo salto.");
                planActual++;
                return;
            }

            // El nivel requerido y el precio están en el PROPIO PREFAB del edificio,
            // que es la misma configuración que usa el jugador. Reglas iguales para ambos.
            EdificioReclutamiento datos = EdificioReclutamiento.DatosDePrefab(plan.prefabEdificio);
            if (datos == null)
            {
                AvisoIA($"IAEnemiga: el prefab de '{plan.nombre}' no tiene EdificioReclutamiento, lo salto.");
                planActual++;
                return;
            }

            // ¿Tiene nivel de castillo? Si no, intenta subir (es prioritario).
            if (!TieneNivel(datos.nivelRequerido))
            {
                ahorrandoParaEdificio = true; // esperando nivel, no atascada
                LogIA($"IAEnemiga: '{plan.nombre}' requiere nivel {datos.nivelRequerido} (tengo {miBase.nivelCastillo}). Mejorando castillo...");
                return;
            }

            // ¿Queda algún hueco donde ponerlo? Comprobarlo ANTES de ahorrar evita
            // pasarse medio minuto juntando recursos para un edificio sin sitio.
            if (!HayAnclajeLibre())
            {
                AvisoIA($"IAEnemiga: no queda sitio para '{plan.nombre}', lo salto sin ahorrar.");
                ahorrandoParaEdificio = false;
                planActual++;
                return;
            }

            // ¿Puede pagar el edificio? Si no, ahorra hasta el próximo ciclo.
            string motivo;
            if (!datos.PuedeConstruirlo(miBase, out motivo))
            {
                ahorrandoParaEdificio = true; // no está atascada, está ahorrando
                LogIA($"IAEnemiga: no puede construir '{plan.nombre}': {motivo}. Ahorrando...");
                return;
            }

            ahorrandoParaEdificio = false;

            plan.edificioConstruido = ConstruirEdificio(plan.prefabEdificio);

            if (plan.edificioConstruido == null)
            {
                LogIA($"IAEnemiga: no pude construir '{plan.nombre}' (sin hueco libre), lo salto.");
                planActual++;
                return;
            }

            // Construido: ahora se cobra el precio
            plan.edificioConstruido.CobrarConstruccion(miBase);
            LogIA($"IAEnemiga: '{plan.nombre}' construido (M:{datos.costeConstruccionMadera} P:{datos.costeConstruccionPiedra} O:{datos.costeConstruccionOro}).");
            return;
        }

        // 2. Reclutar solo las tropas ECONÓMICAS (mineros, recolectores de oro...).
        //    Las militares se reclutan más tarde, en la fase militar, para formar
        //    el ejército de forma ordenada. Aquí los edificios militares solo se levantan.
        if (!plan.tropasSonMilitares && plan.Vivas() < plan.tropasAReclutar)
        {
            if (!HayHuecoPoblacion())
            {
                // NO es un bloqueo: falta hueco y se resuelve subiendo de nivel.
                // Si lo tratáramos como atasco, saltaríamos el edificio económico
                // y nos quedaríamos sin su recurso el resto de la partida.
                ahorrandoParaEdificio = true;
                LogIA("IAEnemiga: población llena, mejorando castillo para sacar más recolectores...");
                return;
            }
            ahorrandoParaEdificio = false;

            GameObject tropa = plan.edificioConstruido.ComprarTropa();
            if (tropa != null)
            {
                plan.Registrar(tropa);
                LogIA($"IAEnemiga: recolector {plan.Vivas()}/{plan.tropasAReclutar} de '{plan.nombre}'.");
            }
            return;
        }

        // 3. Edificio terminado (construido y, si era económico, con sus recolectores) -> siguiente
        planActual++;
    }

    // --- FASE MILITAR: forma el ejército de forma ASCENDENTE ---
    // 1º caballeros (básico, del castillo)
    // 2º las tropas de los edificios militares, en el ORDEN del plan
    //    (ordena el plan de débil a fuerte: Arquería antes que Coliseo, etc.)
    // Cuando todo está reclutado -> Asedio.
    void LogicaPreparacion()
    {
        // 1. Caballeros desde el castillo (la base del ejército)
        if (CaballerosVivos() < cantidadCaballeros)
        {
            // Si la población está llena, la IA sube de nivel para hacer hueco
            if (!HayHuecoPoblacion())
            {
                LogIA("IAEnemiga: población llena, mejorando castillo para el ejército...");
                return;
            }

            GameObject caballero = miBase.ComprarCaballero();
            if (caballero != null)
            {
                RegistrarCaballero(caballero);
                LogIA($"IAEnemiga: caballero {CaballerosVivos()}/{cantidadCaballeros} reclutado.");
            }
            else
            {
                LogIA($"IAEnemiga: esperando MADERA para caballero (tiene {miBase.madera}, cuesta {miBase.costeCaballero}).");
            }
            return;
        }

        // 2. Tropas militares de los edificios, recorriendo el plan EN ORDEN.
        //    Reclutamos una por ciclo (la primera entrada militar que aún no esté completa).
        foreach (PlanEdificio plan in planConstruccion)
        {
            if (!plan.tropasSonMilitares) continue;     // las económicas ya se hicieron
            if (plan.edificioConstruido == null) continue; // no se llegó a construir, lo ignoramos

            if (plan.Vivas() < plan.tropasAReclutar)
            {
                if (!HayHuecoPoblacion())
                {
                    LogIA("IAEnemiga: población llena, mejorando castillo para el ejército...");
                    return;
                }

                GameObject tropa = plan.edificioConstruido.ComprarTropa();
                if (tropa != null)
                {
                    plan.Registrar(tropa);
                    miEjercito.Add(new MiembroEjercito(tropa, plan.rolFormacion));
                    LogIA($"IAEnemiga: {plan.nombre} {plan.Vivas()}/{plan.tropasAReclutar} (ejército: {miEjercito.Count}).");
                }
                return; // una tropa por decisión, para que se note la progresión
            }
        }

        // 3. Ejército completo -> a hacer expediciones antes de atacar
        faseActual = FaseEstrategia.Expedicion;
        LogIA($"IAEnemiga: ¡EJÉRCITO LISTO ({miEjercito.Count} unidades)! Empezando expediciones.");
    }

    // --- FASE EXPEDICIÓN: el grueso aguanta en el castillo y sale una partida pequeña a explorar ---
    // Los recolectores siguen a su trabajo (son autónomos, no los tocamos). El grueso del
    // ejército se queda idle en el castillo (su posición de spawn). Cada ciclo sacamos una
    // partida pequeña a un punto del mapa y al ciclo siguiente la traemos de vuelta.
    void LogicaExpedicion()
    {
        miEjercito.RemoveAll(m => m.objeto == null);

        // ¿Expediciones cumplidas?
        if (expedicionesHechas >= expedicionesObjetivo)
        {
            // Con ejército suficiente, al asalto
            if (miEjercito.Count >= tropasMinimasAtacar)
            {
                partidaFuera.Clear();
                faseActual = FaseEstrategia.Asedio;
                LogIA($"IAEnemiga: expediciones completas. ¡Preparando asalto con {miEjercito.Count} unidades!");
                LogicaAsedio();
                return;
            }

            // Sin ejército suficiente NO seguimos expedicionando en bucle:
            // volvemos a reclutar. (Antes esto encadenaba expediciones 4/3, 5/3...)
            partidaFuera.Clear();
            faseActual = FaseEstrategia.PreparacionMilitar;
            LogIA($"IAEnemiga: solo tengo {miEjercito.Count}/{tropasMinimasAtacar} tropas. Vuelvo a reclutar antes de atacar.");
            return;
        }

        // Si hay una partida explorando, la hacemos volver al castillo y contamos la expedición
        if (partidaFuera.Count > 0)
        {
            foreach (GameObject g in partidaFuera)
            {
                if (g == null) continue;
                Tropa t = g.GetComponent<Tropa>();
                if (t != null) t.OrdenarMovimiento(PuntoCercaDelCastillo());
            }
            partidaFuera.Clear();
            expedicionesHechas++;
            LogIA($"IAEnemiga: expedición {expedicionesHechas}/{expedicionesObjetivo} de vuelta al castillo.");
            return;
        }

        // No hay nadie fuera: mandamos una partida nueva a explorar
        Vector3 destino;
        if (BuscarPuntoExpedicion(out destino))
        {
            partidaFuera = SeleccionarPartidaExpedicion(tropasPorExpedicion);

            // Con un ejército diminuto no se puede mandar a nadie fuera. Damos la
            // expedición por hecha para no quedarnos dando vueltas eternamente.
            if (partidaFuera.Count == 0)
            {
                expedicionesHechas++;
                LogIA($"IAEnemiga: ejército demasiado pequeño para explorar, expedición {expedicionesHechas}/{expedicionesObjetivo} omitida.");
                return;
            }

            foreach (GameObject g in partidaFuera)
            {
                Tropa t = g.GetComponent<Tropa>();
                if (t == null) continue;
                Vector2 sep = Random.insideUnitCircle * 1.5f;
                t.OrdenarMovimiento(destino + new Vector3(sep.x, 0, sep.y));
            }
            LogIA($"IAEnemiga: partida de exploración saliendo ({partidaFuera.Count} tropas).");
        }
    }

    // Elige hasta 'cuantas' tropas rotando por el ejército, dejando SIEMPRE el grueso en casa
    List<GameObject> SeleccionarPartidaExpedicion(int cuantas)
    {
        List<GameObject> partida = new List<GameObject>();
        int total = miEjercito.Count;
        if (total <= 1) return partida; // si solo hay una tropa, no la mandamos sola

        // Nunca sacamos más de la mitad: el grupo grande siempre se queda en el castillo
        cuantas = Mathf.Min(cuantas, total / 2);

        for (int k = 0; k < cuantas; k++)
        {
            if (total == 0) break;
            indiceExpedicion %= total;
            GameObject g = miEjercito[indiceExpedicion].objeto;
            indiceExpedicion = (indiceExpedicion + 1) % total;
            if (g != null) partida.Add(g);
        }
        return partida;
    }

    // Punto aleatorio válido (en NavMesh) a cierta distancia del castillo
    bool BuscarPuntoExpedicion(out Vector3 destino)
    {
        for (int intentos = 0; intentos < 15; intentos++)
        {
            Vector2 circulo = Random.insideUnitCircle * radioExpedicion;
            Vector3 candidato = transform.position + new Vector3(circulo.x, 0, circulo.y);

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(candidato, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            {
                destino = hit.position;
                return true;
            }
        }
        destino = Vector3.zero;
        return false;
    }

    // Un punto cerquita del castillo (para que las partidas vuelvan sin amontonarse)
    Vector3 PuntoCercaDelCastillo()
    {
        Vector2 sep = Random.insideUnitCircle * 3f;
        Vector3 candidato = transform.position + new Vector3(sep.x, 0, sep.y);

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(candidato, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;
        return transform.position;
    }

    // --- FASE ASEDIO: reunir en formación cerca de la base enemiga y cargar todos juntos ---
    void LogicaAsedio()
    {
        miEjercito.RemoveAll(m => m.objeto == null);
        if (miEjercito.Count > picoEjercito) picoEjercito = miEjercito.Count;

        // --- REACCIÓN 2: RETIRADA ---
        // Si el asalto va mal y ha perdido buena parte del ejército, se retira a casa
        // y vuelve a la fase militar para rehacer fuerzas. Es lo que haría un jugador,
        // en vez de seguir enviando soldados sueltos a que los maten.
        if (retirarseSiPierde && cargando && picoEjercito > 0)
        {
            if (miEjercito.Count < Mathf.CeilToInt(picoEjercito * umbralRetirada))
            {
                LogIA($"IAEnemiga: asalto fracasado ({miEjercito.Count}/{picoEjercito}). ¡RETIRADA! Rehaciendo el ejército.");
                Retirarse();
                return;
            }
        }

        BaseMilitar baseEnemiga = BuscarBaseEnemiga();
        if (baseEnemiga == null)
        {
            LogIA("Comandante Rojo: no encuentro base enemiga que asediar.");
            return;
        }

        // 3) Ya estamos cargando: reforzamos (los que no estén asediando, que asedien)
        if (cargando)
        {
            foreach (MiembroEjercito m in miEjercito)
            {
                if (m.objeto == null) continue;
                Combate c = m.objeto.GetComponent<Combate>();
                if (c != null && !c.EstaAsediando()) c.OrdenarAsedio(baseEnemiga.transform);
            }
            return;
        }

        // 1) Primero los mandamos a formar cerca de la base enemiga
        if (!enFormacion)
        {
            ColocarEnFormacion(baseEnemiga.transform);
            enFormacion = true;
            ciclosEnFormacion = 0;
            LogIA("Comandante Rojo: tropas tomando formación de asalto.");
            return;
        }

        // 2) Les damos unos ciclos para agruparse y luego cargan todos a la vez
        ciclosEnFormacion++;
        if (ciclosEnFormacion >= ciclosParaCargar)
        {
            foreach (MiembroEjercito m in miEjercito)
            {
                if (m.objeto == null) continue;
                Combate c = m.objeto.GetComponent<Combate>();
                if (c != null) c.OrdenarAsedio(baseEnemiga.transform);
            }
            cargando = true;
            LogIA($"Comandante Rojo: ¡CARGA! Asaltando {baseEnemiga.gameObject.name} con {miEjercito.Count} soldados.");
        }
    }

    // Pequeñas variaciones por partida: el rival deja de ser idéntico cada vez.
    // Importante para el entrenamiento: evita que el agente memorice UNA secuencia.
    void AplicarVariabilidad()
    {
        // Reloj y contadores de bloqueo a cero: empieza una partida nueva
        tiempoDePartida = 0f;
        proximoAtaqueForzoso = segundosHastaAtaqueForzoso;
        ciclosEnFase = 0;
        faseRegistrada = faseActual;

        expedicionesObjetivo = numeroExpediciones;
        tropasMinimasAtacar = minTropasParaAtacar;

        if (!comportamientoVariable)
        {
            CancelInvoke("TomarDecisionEstrategica");
            intervaloReal = segundosEntreDecisiones;
            InvokeRepeating("TomarDecisionEstrategica", segundosEntreDecisiones, segundosEntreDecisiones);
            return;
        }

        expedicionesObjetivo = Mathf.Max(0, numeroExpediciones + Random.Range(-variacionExpediciones, variacionExpediciones + 1));
        tropasMinimasAtacar = Mathf.Max(1, minTropasParaAtacar + Random.Range(-variacionTropasAtacar, variacionTropasAtacar + 1));

        // El ritmo de decisión también varía: unas partidas es más ágil que otras
        float ritmo = segundosEntreDecisiones * (1f + Random.Range(-variacionRitmo, variacionRitmo));
        ritmo = Mathf.Max(0.5f, ritmo);

        CancelInvoke("TomarDecisionEstrategica");
        intervaloReal = ritmo;
        InvokeRepeating("TomarDecisionEstrategica", ritmo, ritmo);

        LogIA($"IAEnemiga: personalidad de esta partida -> expediciones {expedicionesObjetivo}, ataca con {tropasMinimasAtacar}, ritmo {ritmo:F1}s.");
    }

    // Manda a los supervivientes de vuelta a casa y vuelve a fase militar
    void Retirarse()
    {
        foreach (MiembroEjercito m in miEjercito)
        {
            if (m.objeto == null) continue;

            Combate c = m.objeto.GetComponent<Combate>();
            if (c != null) c.CancelarAtaque();

            Tropa t = m.objeto.GetComponent<Tropa>();
            if (t != null) t.OrdenarMovimiento(PuntoCercaDelCastillo());
        }

        // Reseteamos el asalto y volvemos a reclutar
        enFormacion = false;
        cargando = false;
        ciclosEnFormacion = 0;
        picoEjercito = miEjercito.Count; // el nuevo listón parte de lo que queda
        faseActual = FaseEstrategia.PreparacionMilitar;
    }

    // Coloca el ejército en 3 filas (frente / centro / retaguardia) mirando a la base enemiga
    void ColocarEnFormacion(Transform baseEnemiga)
    {
        Vector3 dir = baseEnemiga.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        Vector3 perp = Vector3.Cross(Vector3.up, dir); // eje lateral de las filas
        Vector3 reunion = baseEnemiga.position - dir * distanciaPreparacion; // punto de partida del asalto

        // Repartimos las tropas según su rol
        List<GameObject> frente = new List<GameObject>();
        List<GameObject> centro = new List<GameObject>();
        List<GameObject> retaguardia = new List<GameObject>();

        foreach (MiembroEjercito m in miEjercito)
        {
            if (m.objeto == null) continue;
            if (m.rol == RolFormacion.Frente) frente.Add(m.objeto);
            else if (m.rol == RolFormacion.Retaguardia) retaguardia.Add(m.objeto);
            else centro.Add(m.objeto);
        }

        // Frente más cerca de la base (+dir), retaguardia más lejos (-dir)
        ColocarFila(frente, reunion + dir * separacionFilas, perp);
        ColocarFila(centro, reunion, perp);
        ColocarFila(retaguardia, reunion - dir * separacionFilas, perp);
    }

    // Coloca una fila de tropas, centrada y separada lateralmente
    void ColocarFila(List<GameObject> fila, Vector3 centroFila, Vector3 perp)
    {
        for (int i = 0; i < fila.Count; i++)
        {
            if (fila[i] == null) continue;

            float desplazamiento = (i - (fila.Count - 1) / 2f) * espaciadoLateral;
            Vector3 slot = centroFila + perp * desplazamiento;

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(slot, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                slot = hit.position;

            Tropa t = fila[i].GetComponent<Tropa>();
            if (t != null) t.OrdenarMovimiento(slot);
        }
    }

    // --- CONSTRUCCIÓN (mecanismo) ---
    EdificioReclutamiento ConstruirEdificio(GameObject prefabEdificio)
    {
        Vector3 punto;
        if (!IntentarObtenerPuntoConstruccion(out punto))
        {
            return null; // No quedan huecos libres
        }

        GameObject edificioGO = Instantiate(prefabEdificio, punto, CalcularRotacionEdificio(punto));

        // Lo metemos en el contenedor dinámico para poder limpiarlo al reiniciar
        AreaPartida area = AreaPartida.Resolver(this);
        if (area != null) area.Adoptar(edificioGO);

        EdificioReclutamiento edificio = edificioGO.GetComponent<EdificioReclutamiento>();
        if (edificio != null)
        {
            edificio.AsignarCastillo(miBase); // ¡es NUESTRO edificio!
        }

        return edificio;
    }

    // Orienta el edificio mirando hacia su castillo. correccionGradosY ajusta el giro
    // por si la puerta del modelo no está en el frente (+Z).
    Quaternion CalcularRotacionEdificio(Vector3 posicionEdificio)
    {
        Vector3 direccion = transform.position - posicionEdificio; // hacia el castillo
        direccion.y = 0f;

        if (direccion.sqrMagnitude < 0.001f) return Quaternion.identity;

        Quaternion mirarCastillo = Quaternion.LookRotation(direccion);
        return mirarCastillo * Quaternion.Euler(0f, correccionGradosY, 0f);
    }

    BaseMilitar BuscarBaseEnemiga()
    {
        // CRÍTICO con varias partidas a la vez: si buscáramos en toda la escena,
        // el ejército podría marcharse a asediar el castillo de OTRA partida.
        AreaPartida area = AreaPartida.Resolver(this);
        if (area == null || miBase == null) return null;

        BaseMilitar objetivo = area.BaseEnemigaMasCercana(miBase.equipo, transform.position);

        return objetivo;
    }

    void GenerarAnclajesAleatorios()
    {
        int intentosMaximos = 60; // Más intentos: ahora rechazamos huecos con obstáculos
        int intentos = 0;

        while (puntosDeAnclaje.Count < cantidadEdificiosMaximos && intentos < intentosMaximos)
        {
            intentos++;

            // Dirección aleatoria + distancia aleatoria entre el mínimo y el máximo.
            // (Antes era .normalized * radioConstruccion, que clavaba TODOS los edificios
            //  en el borde exterior del círculo; por eso salían tan lejos del castillo.)
            float angulo = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            float distancia = Random.Range(radioConstruccionMin, radioConstruccion);
            Vector3 puntoCandidato = transform.position + direccion * distancia;

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(puntoCandidato, out hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                bool espacioLibre = true;
                foreach (Vector3 anclajeExistente in puntosDeAnclaje)
                {
                    if (Vector3.Distance(hit.position, anclajeExistente) < distanciaEntreEdificios)
                    {
                        espacioLibre = false;
                        break;
                    }
                }

                // Además, el terreno debe estar despejado de árboles/rocas alrededor
                Vector3 centroEsfera = hit.position + Vector3.up;
                bool hayObstaculos = Physics.CheckSphere(centroEsfera, radioLibre, capaObstaculos);

                if (espacioLibre && !hayObstaculos)
                {
                    puntosDeAnclaje.Add(hit.position);
                    anclajesOcupados.Add(false);
                }
            }
        }

        // ¿No hemos encontrado sitio ni para el plan? Reintentamos relajando las
        // exigencias: más radio y menos espacio libre alrededor. Es preferible
        // apretar un poco los edificios a quedarse sin poder construirlos.
        int necesarios = planConstruccion.Count;
        if (puntosDeAnclaje.Count < necesarios)
        {
            AvisoIA($"IAEnemiga: solo {puntosDeAnclaje.Count} huecos para {necesarios} edificios. Reintento con menos exigencias.");
            GenerarAnclajesRelajados(necesarios);
        }

        LogIA($"IAEnemiga: Se han generado {puntosDeAnclaje.Count} huecos de construcción válidos.");
    }

    // ¿Queda algún anclaje sin ocupar? (solo consulta, no reserva)
    bool HayAnclajeLibre()
    {
        for (int i = 0; i < anclajesOcupados.Count; i++)
            if (!anclajesOcupados[i]) return true;
        return false;
    }

    // Segunda pasada: más radio, menos separación y menos espacio libre exigido
    void GenerarAnclajesRelajados(int necesarios)
    {
        float radioAmplio = radioConstruccion * 1.6f;
        float separacion = distanciaEntreEdificios * 0.7f;
        float libreReducido = radioLibre * 0.7f;

        int intentos = 0;
        while (puntosDeAnclaje.Count < necesarios && intentos < 120)
        {
            intentos++;

            float angulo = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            float distancia = Random.Range(radioConstruccionMin, radioAmplio);
            Vector3 candidato = transform.position + direccion * distancia;

            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidato, out hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                continue;

            bool espacioLibre = true;
            foreach (Vector3 anclaje in puntosDeAnclaje)
            {
                if (Vector3.Distance(hit.position, anclaje) < separacion) { espacioLibre = false; break; }
            }
            if (!espacioLibre) continue;

            if (Physics.CheckSphere(hit.position + Vector3.up, libreReducido, capaObstaculos)) continue;

            puntosDeAnclaje.Add(hit.position);
            anclajesOcupados.Add(false);
        }
    }

    public bool IntentarObtenerPuntoConstruccion(out Vector3 puntoLibre)
    {
        for (int i = 0; i < puntosDeAnclaje.Count; i++)
        {
            if (!anclajesOcupados[i])
            {
                anclajesOcupados[i] = true;
                puntoLibre = puntosDeAnclaje[i];
                return true;
            }
        }

        puntoLibre = Vector3.zero;
        return false;
    }
}