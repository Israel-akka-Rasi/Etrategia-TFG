using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// ENTORNO DE APRENDIZAJE POR REFUERZO
// Expone UNA partida como entorno RL, sin depender todavía de ML-Agents.
//
//   EjecutarAccion(int)    -> aplica una macroacción, devuelve si fue válida
//   ObtenerObservaciones() -> vector de estado normalizado
//   ConsumirRecompensa()   -> recompensa acumulada desde la última llamada
//   PartidaTerminada()     -> fin de episodio
//   ReiniciarEpisodio()    -> nuevo episodio
//
// Va como componente DENTRO de un AreaPartida: solo ve su propia partida, de
// modo que pueden entrenarse varias a la vez en la misma escena.
// ============================================================================

public enum AccionRL
{
    NoHacerNada = 0,

    // --- Reclutamiento ---
    ReclutarLenador = 1,      // Castillo
    ReclutarCaballero = 2,    // Castillo
    ReclutarMinero = 3,       // Campamento Minero
    ReclutarArquero = 4,      // Galería de Tiro
    ReclutarTesorero = 5,     // Gremio Comercial
    ReclutarGladiador = 6,    // Coliseo

    // --- Construcción y progresión ---
    ConstruirCampamento = 7,
    ConstruirGaleria = 8,
    ConstruirGremio = 9,
    ConstruirColiseo = 10,
    MejorarCastillo = 11,

    // --- Economía ---
    AsignarRecolectores = 12,  // Pone a trabajar a los ociosos
    ReplegarRecolectores = 13, // Los trae a casa (salvarlos de una incursión)

    // --- Reconversión ---
    LicenciarRecolector = 14,  // Cambia economía por recursos
    LicenciarSoldado = 15,

    // --- Militar ---
    AtacarBaseEnemiga = 16,
    AsaltarRecolectoresEnemigos = 17,
    DefenderBase = 18,
    ProtegerRecolectores = 19  // El ejército acude a donde trabajan los recolectores
}

public class EntornoRL : MonoBehaviour
{
    // Número de valores del vector de observaciones. Tiene que coincidir con el
    // "Space Size" del Behavior Parameters.
    public const int NUM_OBSERVACIONES = 22;

    [Header("Identidad del Agente")]
    public int equipoAgente = 0; // 0 = bando azul

    [Header("Prefabs de Edificios (versión del agente)")]
    public GameObject prefabCampamento;
    public GameObject prefabGaleria;
    public GameObject prefabGremio;
    public GameObject prefabColiseo;

    [Header("Colocación de Edificios")]
    public float radioConstruccionMin = 3f;
    public float radioConstruccion = 14f;
    public float distanciaEntreEdificios = 4f;
    public LayerMask capaObstaculos;
    public float radioLibre = 2.5f;

    [Header("Normalización de Observaciones")]
    // Valores de referencia para dejar las observaciones en torno a 0..1
    public float refMadera = 500f;
    public float refPiedra = 300f;
    public float refOro = 200f;
    public float refPoblacion = 30f;
    public float refUnidades = 10f;
    public float refSaludCastillo = 2000f;

    [Header("Escolta de Recolectores")]
    // Distancia a la que se considera que un enemigo está acosando a un trabajador
    public float radioAmenazaRecolectores = 15f;

    [Header("Fin Anticipado")]
    // Corta el episodio cuando el agente ya no puede remontar: sin recolectores,
    // sin madera para comprar uno y sin ejército. Sin esto se simularían minutos
    // enteros sin que pueda ocurrir nada, algo muy frecuente al empezar a
    // entrenar, cuando el agente todavía actúa al azar.
    public bool terminarSiNoHaySalida = true;

    [Header("Pesos de Recompensa")]
    public float recompensaVictoria = 1f;
    public float recompensaDerrota = -1f;
    public float pesoDanoAlEnemigo = 0.5f;
    public float pesoDanoRecibido = 0.5f;
    public float pesoRecursos = 0.0005f;
    public float pesoSubirNivel = 0.05f;
    public float pesoEdificio = 0.03f;
    public float pesoUnidadPerdida = -0.01f;
    // Penalizaciones pequeñas A PROPÓSITO. Con valores mayores se acumulaban en
    // las partidas largas hasta el punto de que arruinarse rápido puntuaba mejor
    // que luchar y perder, y el agente acabaría aprendiendo a suicidarse.
    public float penalizacionAccionInvalida = -0.0005f;
    public float penalizacionPorPaso = 0f;

    // Quedarse sin salida (sin recursos, recolectores ni ejército) debe ser el PEOR
    // desenlace, peor que una derrota en combate. Si no, cortar la partida por la
    // vía rápida resultaría más rentable que jugarla.
    public float penalizacionSinSalida = -0.5f;

    // --- Contexto ---
    private AreaPartida miArea;
    private BaseMilitar miCastillo;
    private BaseMilitar castilloEnemigo;

    // --- Estado para recompensas por diferencia ---
    private float recompensaAcumulada = 0f;
    private float saludEnemigaAnterior;
    private float saludPropiaAnterior;
    private int recursosTotalesAnterior;
    private int nivelAnterior;
    private int unidadesAnterior;

    // Tipos de edificio ya construidos en este episodio. La recompensa por
    // construir se paga UNA vez por tipo (al desbloquearlo), no por cada copia:
    // en ppo_base_01 el agente levantaba ~12 Campamentos Mineros por partida
    // solo para cobrar la recompensa de cada uno.
    private HashSet<string> tiposEdificioConstruidos = new HashSet<string>();
    private bool finalContabilizado = false;

    // ---- Métricas del episodio (para analizar QUÉ estrategia aprende) ----
    // La recompensa dice cuánto va de bien, pero no qué hace el agente. Estas
    // cifras permiten ver, por ejemplo, si ataca alguna vez o solo se defiende.
    private int episodioRecursos = 0;
    private float episodioDanoInfligido = 0f;
    private int episodioUnidadesPerdidas = 0;
    private int episodioNivelMaximo = 1;
    private int episodioAtaquesLanzados = 0;

    // ---- Diagnóstico de la política ----
    // SOLO se mide: nada de esto entra en la recompensa ni en las observaciones,
    // así que añadirlo no cambia el entorno. Sirve para saber qué acciones elige
    // el agente, cuáles le fallan y si su economía llega a arrancar.
    private int[] episodioAccionesLanzadas;
    private int[] episodioAccionesValidas;
    private int episodioDecisiones = 0;
    private int episodioDecisionesConOciosos = 0;
    private int episodioRecolectoresMax = 0;
    // Control de población: unidades vivas frente al contador del castillo.
    // Deben coincidir siempre; un desfase > 0 indicaría que el contador se ha
    // descontado de más y el agente estaría superando el límite de población.
    private int episodioUnidadesMax = 0;
    private int episodioDesfasePoblacionMax = 0;

    [System.NonSerialized] public string ultimoResultado = "";

    public AreaPartida MiArea()
    {
        if (miArea == null) miArea = AreaPartida.Resolver(this);
        return miArea;
    }

    void Start()
    {
        LocalizarCastillos();
        GuardarEstadoParaRecompensas();
    }

    void LocalizarCastillos()
    {
        AreaPartida area = MiArea();
        if (area == null) return;

        miCastillo = area.BaseDeEquipo(equipoAgente);
        castilloEnemigo = area.BaseEnemigaMasCercana(equipoAgente, transform.position);
    }

    // ========================================================================
    // ACCIONES
    // Un false no es un error: significa que el agente eligió algo imposible
    // (sin recursos, sin nivel, sin población, sin objetivo). Se penaliza
    // levemente y servirá para el enmascaramiento de acciones.
    // ========================================================================
    public bool EjecutarAccion(int indiceAccion)
    {
        if (miCastillo == null) LocalizarCastillos();
        if (miCastillo == null) { ultimoResultado = "Sin castillo"; return false; }

        AccionRL accion = (AccionRL)indiceAccion;
        bool ok = false;

        switch (accion)
        {
            case AccionRL.NoHacerNada: ok = true; break;

            case AccionRL.ReclutarLenador:   ok = miCastillo.ComprarLeñador() != null; break;
            case AccionRL.ReclutarCaballero: ok = miCastillo.ComprarCaballero() != null; break;
            case AccionRL.ReclutarMinero:    ok = ReclutarEn("Campamento Minero"); break;
            case AccionRL.ReclutarArquero:   ok = ReclutarEn("Galeria de Tiro"); break;
            case AccionRL.ReclutarTesorero:  ok = ReclutarEn("Gremio Comercial"); break;
            case AccionRL.ReclutarGladiador: ok = ReclutarEn("Coliseo"); break;

            case AccionRL.ConstruirCampamento: ok = ConstruirEdificio(prefabCampamento); break;
            case AccionRL.ConstruirGaleria:    ok = ConstruirEdificio(prefabGaleria); break;
            case AccionRL.ConstruirGremio:     ok = ConstruirEdificio(prefabGremio); break;
            case AccionRL.ConstruirColiseo:    ok = ConstruirEdificio(prefabColiseo); break;

            case AccionRL.MejorarCastillo:
                if (miCastillo.PuedeMejorar()) { miCastillo.MejorarCastillo(); ok = true; }
                break;

            case AccionRL.AsignarRecolectores:  ok = AsignarRecolectoresOciosos(); break;
            case AccionRL.ReplegarRecolectores: ok = ReplegarRecolectores(); break;

            case AccionRL.LicenciarRecolector: ok = Licenciar(true); break;
            case AccionRL.LicenciarSoldado:    ok = Licenciar(false); break;

            case AccionRL.AtacarBaseEnemiga:            ok = OrdenarAtaqueGeneral(); if (ok) episodioAtaquesLanzados++; break;
            case AccionRL.AsaltarRecolectoresEnemigos:  ok = AsaltarEconomiaEnemiga(); break;
            case AccionRL.DefenderBase:                 ok = OrdenarDefensa(); break;
            case AccionRL.ProtegerRecolectores:         ok = ProtegerRecolectores(); break;
        }

        if (!ok) recompensaAcumulada += penalizacionAccionInvalida;
        recompensaAcumulada += penalizacionPorPaso;

        RegistrarDecision(indiceAccion, ok);

        ultimoResultado = $"{accion} -> {(ok ? "OK" : "inválida")}";
        return ok;
    }

    public int NumeroDeAcciones()
    {
        return System.Enum.GetValues(typeof(AccionRL)).Length;
    }

    // ========================================================================
    // MÁSCARA DE ACCIONES
    // Dice qué acciones son posibles AHORA, sin ejecutar nada. AgenteRTS la
    // entrega a ML-Agents antes de cada decisión y la red solo elige entre las
    // posibles. Cada caso repite las condiciones de su acción en EjecutarAccion:
    // si se cambia una, hay que cambiar la otra.
    // Dos acciones pueden fallar aun estando permitidas, porque dependen de
    // algo que no se sabe sin intentarlo: construir (que haya un hueco libre
    // en el terreno) y AsignarRecolectores (que quede un recurso alcanzable).
    // ========================================================================
    public bool[] AccionesPosibles()
    {
        bool[] posibles = new bool[NumeroDeAcciones()];
        posibles[(int)AccionRL.NoHacerNada] = true; // siempre hay al menos una

        if (miCastillo == null) LocalizarCastillos();
        AreaPartida area = MiArea();
        if (miCastillo == null || area == null) return posibles;

        // Un solo recorrido por las unidades para todas las comprobaciones
        int recolectores = 0, ociosos = 0, soldados = 0, ejercito = 0, recolectoresEnemigos = 0;
        foreach (Tropa t in area.Tropas())
        {
            if (t == null) continue;

            if (t.equipo != equipoAgente)
            {
                if (t.EsRecolector()) recolectoresEnemigos++;
                continue;
            }

            if (t.EsRecolector())
            {
                recolectores++;
                Recolector r = t.GetComponent<Recolector>();
                if (r != null && r.EstaOcioso()) ociosos++;
            }
            else soldados++;

            Combate c = t.GetComponent<Combate>();
            if (c != null && c.enabled) ejercito++;
        }

        bool hayHueco = miCastillo.poblacionActual < miCastillo.poblacionMaxima;

        // --- Reclutamiento ---
        posibles[(int)AccionRL.ReclutarLenador] = hayHueco && miCastillo.prefabLeñador != null
                                                  && miCastillo.madera >= miCastillo.costeLeñador;
        posibles[(int)AccionRL.ReclutarCaballero] = hayHueco && miCastillo.prefabCaballero != null
                                                    && miCastillo.madera >= miCastillo.costeCaballero;
        posibles[(int)AccionRL.ReclutarMinero]    = hayHueco && PuedeReclutarEn("Campamento Minero");
        posibles[(int)AccionRL.ReclutarArquero]   = hayHueco && PuedeReclutarEn("Galeria de Tiro");
        posibles[(int)AccionRL.ReclutarTesorero]  = hayHueco && PuedeReclutarEn("Gremio Comercial");
        posibles[(int)AccionRL.ReclutarGladiador] = hayHueco && PuedeReclutarEn("Coliseo");

        // --- Construcción y progresión ---
        posibles[(int)AccionRL.ConstruirCampamento] = PuedePagarEdificio(prefabCampamento);
        posibles[(int)AccionRL.ConstruirGaleria]    = PuedePagarEdificio(prefabGaleria);
        posibles[(int)AccionRL.ConstruirGremio]     = PuedePagarEdificio(prefabGremio);
        posibles[(int)AccionRL.ConstruirColiseo]    = PuedePagarEdificio(prefabColiseo);
        posibles[(int)AccionRL.MejorarCastillo]     = miCastillo.PuedeMejorar();

        // --- Economía y reconversión ---
        posibles[(int)AccionRL.AsignarRecolectores]  = ociosos > 0;
        posibles[(int)AccionRL.ReplegarRecolectores] = recolectores > 0;
        posibles[(int)AccionRL.LicenciarRecolector]  = recolectores > 0;
        posibles[(int)AccionRL.LicenciarSoldado]     = soldados > 0;

        // --- Militar ---
        if (castilloEnemigo == null) LocalizarCastillos();
        posibles[(int)AccionRL.AtacarBaseEnemiga]           = ejercito > 0 && castilloEnemigo != null;
        posibles[(int)AccionRL.AsaltarRecolectoresEnemigos] = ejercito > 0 && recolectoresEnemigos > 0;
        posibles[(int)AccionRL.DefenderBase]                = ejercito > 0;
        posibles[(int)AccionRL.ProtegerRecolectores]        = ejercito > 0 && recolectores > 0;

        return posibles;
    }

    // ¿Algún edificio propio con ese nombre puede reclutar ya? (sin mirar la población)
    bool PuedeReclutarEn(string nombreEdificio)
    {
        foreach (EdificioReclutamiento e in MiArea().Edificios())
        {
            if (e.equipo != equipoAgente || e.nombreEdificio != nombreEdificio) continue;
            if (e.prefabTropa == null) continue;

            if (e.modoPrueba) return true; // en modo prueba reclutar es gratis
            if (miCastillo.madera >= e.costeMadera && miCastillo.piedra >= e.costePiedra
                && miCastillo.oro >= e.costeOro) return true;
        }
        return false;
    }

    // ¿Hay nivel y recursos para este edificio? (el hueco en el terreno no se comprueba)
    bool PuedePagarEdificio(GameObject prefab)
    {
        EdificioReclutamiento datos = EdificioReclutamiento.DatosDePrefab(prefab);
        string motivo;
        return datos != null && datos.PuedeConstruirlo(miCastillo, out motivo);
    }

    // --- DIAGNÓSTICO: se anota cada decisión (no influye en nada) ---
    void AsegurarContadores()
    {
        int n = NumeroDeAcciones();
        if (episodioAccionesLanzadas == null || episodioAccionesLanzadas.Length != n)
        {
            episodioAccionesLanzadas = new int[n];
            episodioAccionesValidas = new int[n];
        }
    }

    void RegistrarDecision(int indice, bool valida)
    {
        AsegurarContadores();

        if (indice >= 0 && indice < episodioAccionesLanzadas.Length)
        {
            episodioAccionesLanzadas[indice]++;
            if (valida) episodioAccionesValidas[indice]++;
        }
        episodioDecisiones++;

        // Estado de la economía tras la decisión: cuántos recolectores hay y si
        // alguno está parado. Los del agente NO trabajan al nacer, así que si
        // nunca usa AsignarRecolectores se quedan ociosos toda la partida.
        AreaPartida area = MiArea();
        if (area == null) return;

        int recolectores = 0, ociosos = 0, unidades = 0;
        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
        {
            unidades++;
            if (!t.EsRecolector()) continue;
            recolectores++;

            Recolector r = t.GetComponent<Recolector>();
            if (r != null && r.EstaOcioso()) ociosos++;
        }

        if (recolectores > episodioRecolectoresMax) episodioRecolectoresMax = recolectores;
        if (ociosos > 0) episodioDecisionesConOciosos++;

        if (unidades > episodioUnidadesMax) episodioUnidadesMax = unidades;
        if (miCastillo != null)
        {
            int desfase = unidades - miCastillo.poblacionActual;
            if (desfase > episodioDesfasePoblacionMax) episodioDesfasePoblacionMax = desfase;
        }
    }

    // Recuento en vivo del episodio en curso (para el panel de pruebas)
    public int[] AccionesLanzadasEpisodio() { AsegurarContadores(); return episodioAccionesLanzadas; }
    public int[] AccionesValidasEpisodio() { AsegurarContadores(); return episodioAccionesValidas; }

    // --- CONSTRUCCIÓN (programática, sin ratón) ---
    bool ConstruirEdificio(GameObject prefabReal)
    {
        if (prefabReal == null) return false;

        EdificioReclutamiento datos = EdificioReclutamiento.DatosDePrefab(prefabReal);
        if (datos == null) return false;

        string motivo;
        if (!datos.PuedeConstruirlo(miCastillo, out motivo)) return false;

        Vector3 punto;
        if (!BuscarPuntoLibre(out punto)) return false;

        GameObject nuevo = Instantiate(prefabReal, punto, Quaternion.identity);

        AreaPartida area = MiArea();
        if (area != null) area.Adoptar(nuevo);

        EdificioReclutamiento edificio = nuevo.GetComponent<EdificioReclutamiento>();
        if (edificio == null) edificio = nuevo.GetComponentInChildren<EdificioReclutamiento>(true);

        if (edificio != null)
        {
            edificio.equipo = equipoAgente;
            edificio.AsignarCastillo(miCastillo);
            edificio.CobrarConstruccion(miCastillo);

            // Solo el PRIMER edificio de cada tipo se premia
            if (tiposEdificioConstruidos.Add(edificio.nombreEdificio))
                recompensaAcumulada += pesoEdificio;
        }
        return true;
    }

    bool BuscarPuntoLibre(out Vector3 punto)
    {
        AreaPartida area = MiArea();

        List<Vector3> ocupados = new List<Vector3>();
        if (area != null)
        {
            foreach (EdificioReclutamiento e in area.Edificios())
                if (e.equipo == equipoAgente) ocupados.Add(e.transform.position);
        }

        for (int intentos = 0; intentos < 40; intentos++)
        {
            float angulo = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            float dist = Random.Range(radioConstruccionMin, radioConstruccion);
            Vector3 candidato = miCastillo.transform.position + dir * dist;

            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(candidato, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                continue;

            bool libre = true;
            foreach (Vector3 o in ocupados)
                if (Vector3.Distance(hit.position, o) < distanciaEntreEdificios) { libre = false; break; }

            if (!libre) continue;
            if (Physics.CheckSphere(hit.position + Vector3.up, radioLibre, capaObstaculos)) continue;

            punto = hit.position;
            return true;
        }

        punto = Vector3.zero;
        return false;
    }

    bool ReclutarEn(string nombreEdificio)
    {
        AreaPartida area = MiArea();
        if (area == null) return false;

        foreach (EdificioReclutamiento e in area.Edificios())
        {
            if (e.equipo != equipoAgente) continue;
            if (e.nombreEdificio != nombreEdificio) continue;

            if (e.ComprarTropa() != null) return true;
        }
        return false;
    }

    // --- ECONOMÍA ---
    bool AsignarRecolectoresOciosos()
    {
        AreaPartida area = MiArea();
        if (area == null) return false;

        bool alguno = false;
        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
        {
            // Nos guiamos por el TIPO declarado, no por tener el componente:
            // un prefab de combate podría arrastrar un Recolector sin usar.
            if (!t.EsRecolector()) continue;

            Recolector r = t.GetComponent<Recolector>();
            if (r == null || !r.EstaOcioso()) continue;

            if (r.BuscarTrabajo()) alguno = true;
        }
        return alguno;
    }

    bool ReplegarRecolectores()
    {
        AreaPartida area = MiArea();
        if (area == null) return false;

        bool alguno = false;
        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
        {
            // SOLO unidades de economía: replegar el ejército es otra acción
            // distinta (DefenderBase) y no debe solaparse con esta.
            if (!t.EsRecolector()) continue;

            Recolector r = t.GetComponent<Recolector>();
            if (r != null) r.DetenerTrabajo();

            t.OrdenarMovimiento(PuntoCercaDelCastillo());
            alguno = true;
        }
        return alguno;
    }

    // --- LICENCIAR: reconvertir unidades en recursos ---
    // Elige la unidad del tipo pedido más cercana al castillo (la menos útil
    // en ese momento: la que está de vuelta o de guardia, no la que trabaja lejos).
    bool Licenciar(bool recolector)
    {
        AreaPartida area = MiArea();
        if (area == null || miCastillo == null) return false;

        Tropa mejor = null;
        float minima = Mathf.Infinity;

        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
        {
            if (t.EsRecolector() != recolector) continue;

            float d = Vector3.Distance(miCastillo.transform.position, t.transform.position);
            if (d < minima) { minima = d; mejor = t; }
        }

        if (mejor == null) return false;

        // La devolución NO es un ingreso: se saca de la recompensa por recursos.
        // Sin esto, reclutar y licenciar en bucle "generaba" ingresos premiados
        // (ppo_v6_seed1: 10 % de las decisiones licenciando caballeros).
        int antes = TotalRecursos();
        bool ok = miCastillo.LicenciarTropa(mejor);
        recursosTotalesAnterior += TotalRecursos() - antes;
        return ok;
    }

    // --- MILITAR ---
    List<Combate> MiEjercito()
    {
        List<Combate> ejercito = new List<Combate>();
        AreaPartida area = MiArea();
        if (area == null) return ejercito;

        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
        {
            // Solo cuentan los Combate ACTIVOS: dar una orden a un componente
            // desactivado devolvería "OK" sin que la unidad hiciera nada.
            Combate c = t.GetComponent<Combate>();
            if (c != null && c.enabled) ejercito.Add(c);
        }
        return ejercito;
    }

    bool OrdenarAtaqueGeneral()
    {
        if (castilloEnemigo == null) LocalizarCastillos();
        if (castilloEnemigo == null) return false;

        List<Combate> ejercito = MiEjercito();
        if (ejercito.Count == 0) return false;

        foreach (Combate c in ejercito) c.OrdenarAsedio(castilloEnemigo.transform);
        return true;
    }

    bool OrdenarDefensa()
    {
        List<Combate> ejercito = MiEjercito();
        if (ejercito.Count == 0) return false;

        foreach (Combate c in ejercito)
        {
            c.CancelarAtaque();
            Tropa t = c.GetComponent<Tropa>();
            if (t != null) t.OrdenarMovimiento(PuntoCercaDelCastillo());
        }
        return true;
    }

    // Incursión contra la economía rival: más barata que el asedio y muy rentable.
    // Concentra el ataque en el trabajador más cercano a cada soldado.
    bool AsaltarEconomiaEnemiga()
    {
        AreaPartida area = MiArea();
        if (area == null) return false;

        List<Combate> ejercito = MiEjercito();
        if (ejercito.Count == 0) return false;

        List<Tropa> objetivos = new List<Tropa>();
        foreach (Tropa t in area.Tropas())
        {
            if (t.equipo == equipoAgente) continue;
            if (t.EsRecolector()) objetivos.Add(t);
        }
        if (objetivos.Count == 0) return false;

        foreach (Combate c in ejercito)
        {
            Tropa mejor = null;
            float minima = Mathf.Infinity;

            foreach (Tropa o in objetivos)
            {
                if (o == null) continue;
                float d = Vector3.Distance(c.transform.position, o.transform.position);
                if (d < minima) { minima = d; mejor = o; }
            }

            if (mejor != null) c.OrdenarAtaque(mejor.transform);
        }
        return true;
    }

    // --- ESCOLTA DE RECOLECTORES ---
    // DefenderBase trae al ejército al castillo, pero los recolectores trabajan
    // lejos de él. Sin esta acción no había forma de acudir a donde está el
    // problema cuando el rival asalta la economía en el campo.
    bool ProtegerRecolectores()
    {
        AreaPartida area = MiArea();
        if (area == null) return false;

        List<Combate> ejercito = MiEjercito();
        if (ejercito.Count == 0) return false;

        // Nuestros trabajadores
        List<Tropa> trabajadores = new List<Tropa>();
        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
            if (t.EsRecolector()) trabajadores.Add(t);

        if (trabajadores.Count == 0) return false;

        // ¿Hay algún enemigo de combate acosándolos?
        Tropa amenaza = null;
        Vector3 puntoDefensa = trabajadores[0].transform.position;
        float distanciaMinima = radioAmenazaRecolectores;

        foreach (Tropa enemigo in area.Tropas())
        {
            if (enemigo.equipo == equipoAgente) continue;

            Combate ce = enemigo.GetComponent<Combate>();
            if (ce == null || !ce.enabled) continue; // los trabajadores rivales no amenazan

            foreach (Tropa trabajador in trabajadores)
            {
                float d = Vector3.Distance(enemigo.transform.position, trabajador.transform.position);
                if (d < distanciaMinima)
                {
                    distanciaMinima = d;
                    amenaza = enemigo;
                    puntoDefensa = trabajador.transform.position;
                }
            }
        }

        if (amenaza != null)
        {
            // Hay acoso: todo el ejército concentra el fuego sobre el agresor
            foreach (Combate c in ejercito) c.OrdenarAtaque(amenaza.transform);
        }
        else
        {
            // Sin amenaza: el ejército se despliega junto a los trabajadores (escolta)
            foreach (Combate c in ejercito)
            {
                c.CancelarAtaque();

                Tropa t = c.GetComponent<Tropa>();
                if (t == null) continue;

                Vector2 sep = Random.insideUnitCircle * 4f;
                Vector3 destino = puntoDefensa + new Vector3(sep.x, 0, sep.y);

                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(destino, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                    destino = hit.position;

                t.OrdenarMovimiento(destino);
            }
        }
        return true;
    }

    Vector3 PuntoCercaDelCastillo()
    {
        Vector2 sep = Random.insideUnitCircle * 4f;
        Vector3 candidato = miCastillo.transform.position + new Vector3(sep.x, 0, sep.y);

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(candidato, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;
        return miCastillo.transform.position;
    }

    // ========================================================================
    // OBSERVACIONES (22 valores normalizados, del estado interno del juego)
    // ========================================================================
    public float[] ObtenerObservaciones()
    {
        if (miCastillo == null) LocalizarCastillos();

        // Si todavía no existe el castillo (por ejemplo, al arrancar, antes de
        // que se genere el mapa), devolvemos un vector de ceros del tamaño
        // correcto en lugar de romper. ML-Agents puede pedir observaciones en
        // ese momento si un agente se desactiva o se cierra muy pronto.
        if (miCastillo == null) return new float[NUM_OBSERVACIONES];

        AreaPartida area = MiArea();

        int lenadores = 0, mineros = 0, tesoreros = 0;
        int caballeros = 0, arqueros = 0, gladiadores = 0;
        int ociosos = 0, tropasEnemigas = 0, trabajadoresEnemigos = 0;

        if (area != null)
        {
            foreach (Tropa t in area.Tropas())
            {
                if (t.equipo != equipoAgente)
                {
                    tropasEnemigas++;
                    if (t.EsRecolector()) trabajadoresEnemigos++;
                    continue;
                }

                // El tipo está declarado en el prefab: no se deduce de sus stats
                switch (t.tipo)
                {
                    case TipoTropa.Lenador:    lenadores++; break;
                    case TipoTropa.Minero:     mineros++; break;
                    case TipoTropa.Tesorero:   tesoreros++; break;
                    case TipoTropa.Caballero:  caballeros++; break;
                    case TipoTropa.Arquero:    arqueros++; break;
                    case TipoTropa.Gladiador:  gladiadores++; break;
                }

                if (t.EsRecolector())
                {
                    Recolector r = t.GetComponent<Recolector>();
                    if (r != null && r.EstaOcioso()) ociosos++;
                }
            }
        }

        int campamentos = 0, galerias = 0, gremios = 0, coliseos = 0;
        if (area != null)
        {
            foreach (EdificioReclutamiento e in area.Edificios())
            {
                if (e.equipo != equipoAgente) continue;
                if (e.nombreEdificio == "Campamento Minero") campamentos = 1;
                else if (e.nombreEdificio == "Galeria de Tiro") galerias = 1;
                else if (e.nombreEdificio == "Gremio Comercial") gremios = 1;
                else if (e.nombreEdificio == "Coliseo") coliseos = 1;
            }
        }

        float saludPropia = miCastillo != null ? miCastillo.salud : 0f;
        float saludEnemiga = castilloEnemigo != null ? castilloEnemigo.salud : 0f;

        return new float[]
        {
            miCastillo.madera / refMadera,
            miCastillo.piedra / refPiedra,
            miCastillo.oro / refOro,

            miCastillo.poblacionActual / refPoblacion,
            miCastillo.poblacionMaxima / refPoblacion,
            (float)miCastillo.nivelCastillo / miCastillo.nivelMaximo,

            lenadores / refUnidades,
            mineros / refUnidades,
            tesoreros / refUnidades,
            ociosos / refUnidades,
            caballeros / refUnidades,
            arqueros / refUnidades,
            gladiadores / refUnidades,

            campamentos, galerias, gremios, coliseos,

            saludPropia / refSaludCastillo,
            saludEnemiga / refSaludCastillo,
            tropasEnemigas / (refUnidades * 2f),
            trabajadoresEnemigos / (refUnidades * 1.5f),

            TiempoNormalizado()
        };
    }

    public int TamanoObservaciones()
    {
        return ObtenerObservaciones().Length;
    }

    float TiempoNormalizado()
    {
        AreaPartida area = MiArea();
        GestorPartida g = (area != null) ? area.Gestor() : null;
        if (g == null || g.segundosMaximosPartida <= 0f) return 0f;

        return Mathf.Clamp01(g.TiempoPartida() / g.segundosMaximosPartida);
    }

    // ========================================================================
    // RECOMPENSAS (por diferencia respecto al paso anterior)
    // ========================================================================
    void GuardarEstadoParaRecompensas()
    {
        if (miCastillo == null) LocalizarCastillos();

        saludPropiaAnterior = miCastillo != null ? miCastillo.salud : 0f;
        saludEnemigaAnterior = castilloEnemigo != null ? castilloEnemigo.salud : 0f;
        recursosTotalesAnterior = TotalRecursos();
        nivelAnterior = miCastillo != null ? miCastillo.nivelCastillo : 1;
        tiposEdificioConstruidos.Clear();
        unidadesAnterior = ContarMisUnidades();
        finalContabilizado = false;

        episodioRecursos = 0;
        episodioDanoInfligido = 0f;
        episodioUnidadesPerdidas = 0;
        episodioNivelMaximo = nivelAnterior;
        episodioAtaquesLanzados = 0;

        AsegurarContadores();
        System.Array.Clear(episodioAccionesLanzadas, 0, episodioAccionesLanzadas.Length);
        System.Array.Clear(episodioAccionesValidas, 0, episodioAccionesValidas.Length);
        episodioDecisiones = 0;
        episodioDecisionesConOciosos = 0;
        episodioRecolectoresMax = 0;
        episodioUnidadesMax = 0;
        episodioDesfasePoblacionMax = 0;
    }

    int TotalRecursos()
    {
        if (miCastillo == null) return 0;
        return miCastillo.madera + miCastillo.piedra + miCastillo.oro;
    }

    int ContarMisEdificios()
    {
        AreaPartida area = MiArea();
        if (area == null) return 0;

        int n = 0;
        foreach (EdificioReclutamiento e in area.Edificios())
            if (e.equipo == equipoAgente) n++;
        return n;
    }

    int ContarMisUnidades()
    {
        AreaPartida area = MiArea();
        if (area == null) return 0;
        return area.TropasDeEquipo(equipoAgente).Count;
    }

    void EvaluarRecompensas()
    {
        if (miCastillo == null) return;

        // Daño hecho al castillo enemigo y daño recibido en el propio
        if (castilloEnemigo != null)
        {
            float dano = saludEnemigaAnterior - castilloEnemigo.salud;
            if (dano > 0)
            {
                recompensaAcumulada += (dano / refSaludCastillo) * pesoDanoAlEnemigo;
                episodioDanoInfligido += dano;
            }
            saludEnemigaAnterior = castilloEnemigo.salud;
        }

        float danoPropio = saludPropiaAnterior - miCastillo.salud;
        if (danoPropio > 0) recompensaAcumulada -= (danoPropio / refSaludCastillo) * pesoDanoRecibido;
        saludPropiaAnterior = miCastillo.salud;

        // Economía: premiamos el ingreso, no el gasto
        int recursos = TotalRecursos();
        int ingreso = recursos - recursosTotalesAnterior;
        if (ingreso > 0)
        {
            recompensaAcumulada += ingreso * pesoRecursos;
            episodioRecursos += ingreso;
        }
        recursosTotalesAnterior = recursos;

        // Progresión
        if (miCastillo.nivelCastillo > episodioNivelMaximo) episodioNivelMaximo = miCastillo.nivelCastillo;

        if (miCastillo.nivelCastillo > nivelAnterior)
        {
            recompensaAcumulada += (miCastillo.nivelCastillo - nivelAnterior) * pesoSubirNivel;
            nivelAnterior = miCastillo.nivelCastillo;
        }

        // (La recompensa por edificios ya no se calcula aquí: se paga en
        // ConstruirEdificio, una sola vez por tipo.)

        // Bajas propias
        int unidades = ContarMisUnidades();
        if (unidades < unidadesAnterior)
        {
            recompensaAcumulada += (unidadesAnterior - unidades) * pesoUnidadPerdida;
            episodioUnidadesPerdidas += unidadesAnterior - unidades;
        }
        unidadesAnterior = unidades;

        // Terminal (una sola vez)
        if (!finalContabilizado && PartidaTerminada())
        {
            finalContabilizado = true;
            recompensaAcumulada += AgenteHaGanado() ? recompensaVictoria : recompensaDerrota;

            // Arruinarse es peor que perder peleando
            if (EstadoSinSalida()) recompensaAcumulada += penalizacionSinSalida;
        }
    }

    // ---- Resumen del episodio, para las estadísticas de entrenamiento ----
    // Hay que consultarlo ANTES de reiniciar, mientras el resultado sigue anotado.
    public struct ResumenEpisodio
    {
        public bool victoria, empate, ruina, derrotaEnCombate;
        public int recursos, unidadesPerdidas, nivelMaximo, edificios, ataquesLanzados;
        public float danoInfligidoFraccion;

        // Diagnóstico de la política
        public int decisiones, recolectoresMax, unidadesMax, desfasePoblacionMax;
        public float tasaInvalidas, fraccionConOciosos;
        public int[] accionesLanzadas, accionesValidas;
    }

    public ResumenEpisodio ObtenerResumen()
    {
        ResumenEpisodio r = new ResumenEpisodio();
        r.victoria = AgenteHaGanado();
        r.ruina = EstadoSinSalida();
        r.empate = !r.ruina && HuboEmpate();
        r.derrotaEnCombate = !r.victoria && !r.empate && !r.ruina;

        r.recursos = episodioRecursos;
        r.unidadesPerdidas = episodioUnidadesPerdidas;
        r.nivelMaximo = episodioNivelMaximo;
        r.edificios = ContarMisEdificios();
        r.ataquesLanzados = episodioAtaquesLanzados;
        r.danoInfligidoFraccion = episodioDanoInfligido / refSaludCastillo;

        AsegurarContadores();
        int validas = 0;
        foreach (int v in episodioAccionesValidas) validas += v;

        r.decisiones = episodioDecisiones;
        r.recolectoresMax = episodioRecolectoresMax;
        r.unidadesMax = episodioUnidadesMax;
        r.desfasePoblacionMax = episodioDesfasePoblacionMax;
        r.tasaInvalidas = episodioDecisiones > 0 ? 1f - (float)validas / episodioDecisiones : 0f;
        r.fraccionConOciosos = episodioDecisiones > 0 ? (float)episodioDecisionesConOciosos / episodioDecisiones : 0f;
        // Copias: los contadores se ponen a cero al reiniciar el episodio
        r.accionesLanzadas = (int[])episodioAccionesLanzadas.Clone();
        r.accionesValidas = (int[])episodioAccionesValidas.Clone();
        return r;
    }

    // Devuelve la recompensa acumulada y la pone a cero (un step de RL)
    public float ConsumirRecompensa()
    {
        EvaluarRecompensas();
        float r = recompensaAcumulada;
        recompensaAcumulada = 0f;
        return r;
    }

    // Consulta sin vaciar (para la interfaz de depuración)
    public float RecompensaPendiente()
    {
        EvaluarRecompensas();
        return recompensaAcumulada;
    }

    // ========================================================================
    // FIN DE EPISODIO
    // ========================================================================
    GestorPartida Gestor()
    {
        AreaPartida area = MiArea();
        return (area != null) ? area.Gestor() : null;
    }

    // ¿El agente se ha quedado sin ninguna forma de remontar?
    // Sin recolectores no entra madera; sin madera no puede comprar recolectores;
    // y sin ejército tampoco puede ganar la partida por la fuerza.
    public bool EstadoSinSalida()
    {
        if (!terminarSiNoHaySalida || miCastillo == null) return false;

        // Si puede pagar un recolector, siempre hay salida
        if (miCastillo.madera >= miCastillo.costeLeñador) return false;

        AreaPartida area = MiArea();
        if (area == null) return false;

        foreach (Tropa t in area.TropasDeEquipo(equipoAgente))
        {
            if (t.EsRecolector()) return false; // aún produce

            // OJO: un componente desactivado sigue existiendo. Si solo
            // comprobáramos que no es null, un leñador con el Combate
            // desmarcado contaría como ejército y nunca detectaríamos
            // que la partida ya no tiene salida.
            Combate c = t.GetComponent<Combate>();
            if (c != null && c.enabled) return false; // aún puede pelear
        }

        return true; // ni economía, ni recursos, ni ejército
    }

    // El fin de episodio lo dicta el gestor, que guarda el resultado hasta que
    // lo recogemos. Mirar solo la salud no bastaba: la partida se reiniciaba en
    // el mismo frame y el final pasaba desapercibido.
    public bool PartidaTerminada()
    {
        GestorPartida g = Gestor();
        if (g != null && g.ResultadoPendiente) return true;

        if (EstadoSinSalida()) return true;

        // Red de seguridad por si no hubiera gestor
        if (miCastillo == null || castilloEnemigo == null) return false;
        return miCastillo.salud <= 0f || castilloEnemigo.salud <= 0f;
    }

    // Victoria solo si el derrotado es el OTRO equipo. El empate por tiempo
    // agotado (equipo -1) no cuenta como victoria.
    public bool AgenteHaGanado()
    {
        if (EstadoSinSalida()) return false; // sin salida = derrota

        GestorPartida g = Gestor();
        if (g != null && g.ResultadoPendiente)
        {
            return g.EquipoDerrotado >= 0 && g.EquipoDerrotado != equipoAgente;
        }

        if (castilloEnemigo == null || miCastillo == null) return false;
        return castilloEnemigo.salud <= 0f && miCastillo.salud > 0f;
    }

    // ¿Terminó en empate (tiempo agotado)? Útil para las estadísticas
    public bool HuboEmpate()
    {
        if (EstadoSinSalida()) return false; // no es empate: es derrota

        GestorPartida g = Gestor();
        return g != null && g.ResultadoPendiente && g.EquipoDerrotado < 0;
    }

    public void ReiniciarEpisodio()
    {
        GestorPartida g = Gestor();
        if (g != null) g.ReiniciarPartida(); // consume el resultado pendiente

        recompensaAcumulada = 0f;
        LocalizarCastillos();
        GuardarEstadoParaRecompensas();
    }
}