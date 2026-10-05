using UnityEngine;
using UnityEngine.UI;

public class BaseMilitar : MonoBehaviour
{
    [Header("Identidad y Progreso")]
    public int equipo; // 0 = Azul (Jugador), 1 = Rojo (Enemigo)
    public int nivelCastillo = 1; // 15 es el máximo

    [Header("Bóveda de Recursos")]
    public int madera = 40; // Arranque: da para 2-3 leñadores de salida
    public int piedra = 0;
    public int oro = 0;

    [Header("Licenciar Unidades")]
    [Range(0f, 1f)] public float porcentajeDevolucion = 0.5f; // Se recupera la mitad

    [Header("Gestión de Población")]
    public int poblacionActual = 1; // Empezamos en 1 por el Leñador inicial gratis
    public int poblacionMaxima = 8;
    public int poblacionPorNivel = 2; // Cuánta población da cada mejora de castillo

    [Header("Combate y Spawn")]
    public float salud = 2000f; // ~22s de asedio con un ejército completo
    private float saludMaxima;
    public Image barraSalud;
    public Transform puntoSpawn;

    [Header("Reclutamiento del Castillo")]
    public GameObject prefabLeñador; // Recolector de madera (jugador: Recolector / IA: RecolectorIA)
    public int costeLeñador = 10;    // Madera

    public GameObject prefabCaballero; // Unidad de combate básica
    public int costeCaballero = 12;    // Madera

    // --- Estado inicial guardado, para poder restaurarlo al reiniciar el episodio ---
    private int maderaInicial, piedraInicial, oroInicial;
    private int poblacionInicial, poblacionMaximaInicial, nivelInicial;

    // Área (partida) a la que pertenece este castillo
    private AreaPartida miArea;
    public AreaPartida MiArea()
    {
        if (miArea == null) miArea = AreaPartida.Resolver(this);
        return miArea;
    }

    // ¿Hemos llegado a guardar el estado de arranque?
    private bool estadoInicialGuardado = false;

    void Awake()
    {
        MiArea().Registrar(this);

        // El estado inicial se guarda AQUÍ y no en Start: ML-Agents puede pedir
        // un reinicio de episodio antes de que Start se ejecute, y entonces
        // restauraríamos ceros y los dejaríamos grabados como valores buenos.
        GuardarEstadoInicial();
    }

    void GuardarEstadoInicial()
    {
        if (estadoInicialGuardado) return;

        saludMaxima = salud;
        maderaInicial = madera;
        piedraInicial = piedra;
        oroInicial = oro;
        poblacionInicial = poblacionActual;
        poblacionMaximaInicial = poblacionMaxima;
        nivelInicial = nivelCastillo;

        estadoInicialGuardado = true;
    }

    void OnDestroy()
    {
        if (miArea != null) miArea.Desregistrar(this);
    }

    void Start()
    {
        GuardarEstadoInicial(); // por si acaso; normalmente ya se hizo en Awake

        // --- TROPA INICIAL GRATIS: un Leñador ---
        SpawnTropaInicial();

        ActualizarTextosUI();
    }

    // Crea la tropa inicial gratis (el Leñador). Se usa al arrancar y al reiniciar.
    public void SpawnTropaInicial()
    {
        if (prefabLeñador != null && puntoSpawn != null)
        {
            GameObject leñadorInicial = SpawnUtils.SpawnConDesplazamiento(prefabLeñador, puntoSpawn, 1.5f);
            AsignarBaseSiEsRecolector(leñadorInicial);
        }
    }

    // ====================================================================
    // REINICIO: restaura la base a su estado de arranque.
    // (La parte de volver a crear el leñador inicial y reiniciar la IA va en la Parte B.)
    // ====================================================================
    public void ReiniciarEstado()
    {
        // Sin estado guardado no hay nada que restaurar: restaurar ceros aquí
        // dejaría el castillo inservible para el resto de la partida.
        if (!estadoInicialGuardado)
        {
            GuardarEstadoInicial();
            return;
        }

        salud = saludMaxima;
        if (barraSalud != null) barraSalud.fillAmount = 1f;

        madera = maderaInicial;
        piedra = piedraInicial;
        oro = oroInicial;
        poblacionActual = poblacionInicial;
        poblacionMaxima = poblacionMaximaInicial;
        nivelCastillo = nivelInicial;

        ActualizarTextosUI();
    }

    // --- GESTIÓN DE POBLACIÓN ---
    // Liberar hueco cuando muere una unidad. Sin esto, el contador solo sube
    // y al llegar al tope ya no se puede reclutar nunca más.
    public void LiberarPoblacion(int cantidad = 1)
    {
        poblacionActual = Mathf.Max(0, poblacionActual - cantidad);
        ActualizarTextosUI();
    }

    // --- RECOLECCIÓN DEL MAPA ---
    public void RecibirRecurso(string tipo, int cantidad)
    {
        if (tipo == "Madera") madera += cantidad;
        else if (tipo == "Piedra") piedra += cantidad;
        else if (tipo == "Oro") oro += cantidad;

        ActualizarTextosUI();
    }

    // --- CONEXIÓN CON LA INTERFAZ ---
    public void ActualizarTextosUI()
    {
        if (equipo != 0 || UIManager.Instancia == null) return;

        // Solo el castillo del área que estamos viendo escribe en la interfaz.
        // Con varias áreas en la escena, si no, cada castillo azul pisaba los
        // números de los demás y la interfaz mezclaba partidas.
        AreaPartida area = MiArea();
        if (area != null && area != AreaPartida.AreaVisible()) return;

        UIManager.Instancia.ActualizarEconomia(nivelCastillo, poblacionActual, poblacionMaxima, madera, piedra, oro);
    }

    [Header("Evolución")]
    public int nivelMaximo = 6;          // Último nivel alcanzable (5 desbloquea el Coliseo)
    public int costeBaseMejora = 50;     // Madera:  base * nivel
    public int costePiedraMejora = 15;   // Piedra:  15 * (nivel-1), desde nivel 2
    public int costeOroMejora = 10;      // Oro:     10 * (nivel-3), desde nivel 4

    // --- COSTE DE LA SIGUIENTE MEJORA ---
    // Cada nivel pide justo los recursos que te desbloqueó el nivel anterior:
    // nivel 2 da Campamento (piedra) y nivel 4 da Gremio (oro).
    // Se puede consultar sin ejecutar la mejora (lo usa la interfaz para mostrarlo).
    public void CosteSiguienteNivel(out int maderaNecesaria, out int piedraNecesaria, out int oroNecesario)
    {
        maderaNecesaria = 0;
        piedraNecesaria = 0;
        oroNecesario = 0;

        if (nivelCastillo >= nivelMaximo) return; // Nivel máximo: no hay siguiente

        // Madera: siempre, y creciente
        maderaNecesaria = costeBaseMejora * nivelCastillo;

        // Piedra: a partir del nivel 2 (ya tienes Campamento Minero)
        if (nivelCastillo >= 2)
            piedraNecesaria = costePiedraMejora * (nivelCastillo - 1);

        // Oro: a partir del nivel 4 (ya tienes Gremio Comercial)
        if (nivelCastillo >= 4)
            oroNecesario = costeOroMejora * (nivelCastillo - 3);
    }

    // ¿Puede permitirse ahora mismo la siguiente mejora?
    public bool PuedeMejorar()
    {
        if (nivelCastillo >= nivelMaximo) return false;

        int m, p, o;
        CosteSiguienteNivel(out m, out p, out o);
        return madera >= m && piedra >= p && oro >= o;
    }

    // --- LÓGICA DE MEJORA ---
    public void MejorarCastillo()
    {
        if (nivelCastillo >= nivelMaximo) return;

        int maderaNecesaria, piedraNecesaria, oroNecesario;
        CosteSiguienteNivel(out maderaNecesaria, out piedraNecesaria, out oroNecesario);

        if (madera >= maderaNecesaria && piedra >= piedraNecesaria && oro >= oroNecesario)
        {
            madera -= maderaNecesaria;
            piedra -= piedraNecesaria;
            oro -= oroNecesario;

            nivelCastillo++;
            poblacionMaxima += poblacionPorNivel;

            string quien = (equipo == 0) ? "EL JUGADOR" : "EL ENEMIGO";
            AreaPartida areaLog = MiArea();
            if (areaLog != null) quien = $"[A{areaLog.idArea}] {quien}";
            Debug.Log($"{quien} ha subido a Nivel {nivelCastillo} (pob. máx: {poblacionMaxima}). Costó: M:{maderaNecesaria} P:{piedraNecesaria} O:{oroNecesario}");

            if (equipo == 0) ActualizarInterfazAlMejorar();
            ActualizarTextosUI();
        }
        else
        {
            Debug.Log("Faltan recursos para mejorar el castillo.");
        }
    }

    void ActualizarInterfazAlMejorar()
    {
        if (UIManager.Instancia != null && UIManager.Instancia.panelReclutamiento != null)
        {
            if (UIManager.Instancia.panelReclutamiento.activeSelf)
            {
                UIManager.Instancia.MostrarPanelReclutamiento(true, nivelCastillo);
            }
        }
    }

    // --- LÓGICA DE COMBATE ---
    public void RecibirDano(int cantidad)
    {
        // La vida nunca baja de cero. Sin esto, un castillo ya destruido seguía
        // acumulando daño hasta valores negativos, que podrían colarse en las
        // observaciones del agente como un dato sin sentido.
        salud = Mathf.Max(0f, salud - cantidad);

        if (barraSalud != null)
        {
            barraSalud.fillAmount = salud / saludMaxima;
        }

        if (salud <= 0)
        {
            // Ya NO destruimos la base (así se puede restaurar al reiniciar).
            // Solo avisamos del fin de partida: el GameManager decide si reinicia
            // (entrenamiento) o muestra el cartel de victoria/derrota (jugable).
            // Avisamos al gestor de NUESTRA partida (él decide si hay cartel,
            // si se reinicia solo por ser área de entrenamiento, etc.)
            GestorPartida gestor = MiArea() != null ? MiArea().Gestor() : null;
            if (gestor != null)
            {
                gestor.TerminarPartida(equipo);
            }
        }
    }

    // ====================================================================
    // MÉTODO GENÉRICO DE RECLUTAMIENTO
    // ====================================================================
    public GameObject Reclutar(GameObject prefab, int costeM, int costeP = 0, int costeO = 0)
    {
        if (prefab == null) return null;
        if (poblacionActual >= poblacionMaxima) return null;

        if (madera < costeM || piedra < costeP || oro < costeO) return null;

        madera -= costeM;
        piedra -= costeP;
        oro -= costeO;
        poblacionActual++;

        GameObject nuevaTropa = SpawnUtils.SpawnConDesplazamiento(prefab, puntoSpawn);
        AsignarBaseSiEsRecolector(nuevaTropa);
        AnotarCoste(nuevaTropa, costeM, costeP, costeO);
        ActualizarTextosUI();
        return nuevaTropa;
    }

    // Deja anotado en la unidad lo que costó, para poder devolver parte al licenciar
    public static void AnotarCoste(GameObject tropa, int m, int p, int o)
    {
        if (tropa == null) return;
        Tropa t = tropa.GetComponent<Tropa>();
        if (t == null) return;

        t.costeMaderaPagado = m;
        t.costePiedraPagado = p;
        t.costeOroPagado = o;
    }

    // --- LICENCIAR: retirar una unidad y recuperar parte de su coste ---
    // Permite reconvertir economía en ejército a mitad de partida. La devolución
    // es parcial a propósito: si fuera del 100% se podría comprar y vender sin
    // penalización y las decisiones dejarían de tener consecuencias.
    public bool LicenciarTropa(Tropa tropa)
    {
        if (tropa == null || tropa.equipo != equipo) return false;

        int devM = Mathf.RoundToInt(tropa.costeMaderaPagado * porcentajeDevolucion);
        int devP = Mathf.RoundToInt(tropa.costePiedraPagado * porcentajeDevolucion);
        int devO = Mathf.RoundToInt(tropa.costeOroPagado * porcentajeDevolucion);

        madera += devM;
        piedra += devP;
        oro += devO;

        Debug.Log($"Licenciado {tropa.tipo}. Devuelto M:{devM} P:{devP} O:{devO}");

        // El hueco de población lo libera el propio OnDestroy de la tropa
        Destroy(tropa.gameObject);
        ActualizarTextosUI();
        return true;
    }

    private void AsignarBaseSiEsRecolector(GameObject tropa)
    {
        if (tropa == null) return;

        RecolectorIA recolectorIA = tropa.GetComponent<RecolectorIA>();
        if (recolectorIA != null) recolectorIA.AsignarBase(this);
    }

    public GameObject ComprarLeñador()
    {
        return Reclutar(prefabLeñador, costeLeñador);
    }

    public GameObject ComprarCaballero()
    {
        return Reclutar(prefabCaballero, costeCaballero);
    }
}