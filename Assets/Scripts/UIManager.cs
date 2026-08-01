using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    public static UIManager Instancia;

    [Header("Panel Económico")]
    public TextMeshProUGUI txtNivelCastillo;
    public TextMeshProUGUI txtPoblacion;
    public TextMeshProUGUI txtMadera;
    public TextMeshProUGUI txtPiedra;
    public TextMeshProUGUI txtOro;

    [Header("Coste de Mejora del Castillo")]
    public TextMeshProUGUI txtCosteMejora; // Muestra lo que cuesta subir al siguiente nivel
    public Button botonMejorarCastillo;    // Se desactiva si no llegan los recursos

    [Header("Panel de Selección")]
    public GameObject panelSeleccion;
    public TextMeshProUGUI txtNombreUnidad;
    public TextMeshProUGUI txtVidaUnidad;
    public TextMeshProUGUI txtAtaqueUnidad;
    public Image imgRetratoUnidad;

    [Header("Menús")]
    public GameObject panelReclutamiento;

    [Header("Botones de Reclutamiento (Castillo)")]
    // El castillo solo produce Leñador y Caballero.
    public Button botonLeñador;
    public Button botonCaballero;

    [Header("Nuevos Paneles y Construcción")]
    public GameObject panelConstruccion;
    public SistemaConstruccion sistemaConstruccion;

    [Header("Prefabs de Edificios")]
    public GameObject hologramaCampamentoMinero;
    public GameObject prefabCampamentoMineroReal;
    public GameObject hologramaGaleriaTiro;
    public GameObject prefabGaleriaTiroReal;
    public GameObject hologramaGremioComercial;
    public GameObject prefabGremioComercialReal;
    public GameObject hologramaColiseo;
    public GameObject prefabColiseoReal;

    [Header("Límites de Construcción")]
    public int limiteEdificios = 3;
    [Space(5)]
    public Button botonConstruirCampamento;
    public TextMeshProUGUI textoLimiteCampamento;
    [Space(5)]
    public Button botonConstruirGaleria;
    public TextMeshProUGUI textoLimiteGaleria;
    [Space(5)]
    public Button botonConstruirGremio;
    public TextMeshProUGUI textoLimiteGremio;
    [Space(5)]
    public Button botonConstruirColiseo;
    public TextMeshProUGUI textoLimiteColiseo;

    [Header("Menús de Edificios Seleccionados")]
    public GameObject panelCampamentoMinero;
    public GameObject panelGaleriaTiro;
    public GameObject panelGremioComercial;
    public GameObject panelColiseo;
    private EdificioReclutamiento edificioSeleccionado;

    private Tropa tropaSeleccionada;

    // Guardamos aquí el castillo del jugador para no buscarlo en cada clic de botón.
    private BaseMilitar baseJugador;

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        OcultarPanelSeleccion();
    }

    void Update()
    {
        if (panelSeleccion.activeSelf)
        {
            if (tropaSeleccionada != null)
            {
                txtVidaUnidad.text = "HP: " + (int)tropaSeleccionada.salud + " / " + (int)tropaSeleccionada.saludMaxima;
            }
            else
            {
                OcultarPanelSeleccion();
            }
        }
    }

    // --- Buscador cacheado de la base del jugador (Equipo 0) ---
    private BaseMilitar ObtenerBaseJugador()
    {
        if (baseJugador == null)
        {
            BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
            foreach (BaseMilitar b in bases)
            {
                if (b.equipo == 0)
                {
                    baseJugador = b;
                    break;
                }
            }
        }
        return baseJugador;
    }

    public void ActualizarEconomia(int nivel, int popActual, int popMax, int madera, int piedra, int oro)
    {
        txtNivelCastillo.text = "Nivel: " + nivel;
        txtPoblacion.text = "Población: " + popActual + " / " + popMax;
        txtMadera.text = "Madera: " + madera;
        txtPiedra.text = "Piedra: " + piedra;
        txtOro.text = "Oro: " + oro;

        ActualizarCosteMejora();
    }

    // Muestra cuánto cuesta el siguiente nivel y marca en rojo lo que falta
    private void ActualizarCosteMejora()
    {
        BaseMilitar castillo = ObtenerBaseJugador();
        if (castillo == null) return;

        bool esMaximo = castillo.nivelCastillo >= castillo.nivelMaximo;

        if (txtCosteMejora != null)
        {
            if (esMaximo)
            {
                txtCosteMejora.text = "Nivel máximo";
                txtCosteMejora.color = Color.white;
            }
            else
            {
                int m, p, o;
                castillo.CosteSiguienteNivel(out m, out p, out o);

                // Solo mostramos los recursos que de verdad hacen falta
                string linea = "Mejora: ";
                if (m > 0) linea += $"M {castillo.madera}/{m}  ";
                if (p > 0) linea += $"P {castillo.piedra}/{p}  ";
                if (o > 0) linea += $"O {castillo.oro}/{o}";

                txtCosteMejora.text = linea.TrimEnd();
                txtCosteMejora.color = castillo.PuedeMejorar() ? Color.white : Color.red;
            }
        }

        if (botonMejorarCastillo != null)
        {
            botonMejorarCastillo.interactable = !esMaximo && castillo.PuedeMejorar();
        }
    }

    public void MostrarInfoTropa(Tropa tropa, int ataque)
    {
        tropaSeleccionada = tropa;
        panelSeleccion.SetActive(true);
        txtNombreUnidad.text = tropa.gameObject.name.Replace("(Clone)", "");
        txtAtaqueUnidad.text = "Daño: " + ataque;

        if (imgRetratoUnidad != null && tropa.retratoTropa != null)
        {
            imgRetratoUnidad.sprite = tropa.retratoTropa;
            imgRetratoUnidad.color = Color.white;
        }
    }

    public void OcultarPanelSeleccion()
    {
        tropaSeleccionada = null;
        panelSeleccion.SetActive(false);
    }

    // --- BOTONES DEL CASTILLO ---
    public void BotonMejorarCastillo()
    {
        BaseMilitar b = ObtenerBaseJugador();
        if (b != null) b.MejorarCastillo();
    }

    public void BotonComprarLeñador()
    {
        BaseMilitar b = ObtenerBaseJugador();
        if (b != null) b.ComprarLeñador();
    }

    public void BotonComprarCaballero()
    {
        BaseMilitar b = ObtenerBaseJugador();
        if (b != null) b.ComprarCaballero();
    }

    public void AbrirPanelConstruccion()
    {
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(true);

        int contCampamentos = 0, contGalerias = 0, contGremios = 0, contColiseos = 0;

        EdificioReclutamiento[] edificiosEnMapa = FindObjectsOfType<EdificioReclutamiento>();
        foreach (EdificioReclutamiento edificio in edificiosEnMapa)
        {
            if (edificio.nombreEdificio == "Campamento Minero") contCampamentos++;
            else if (edificio.nombreEdificio == "Galeria de Tiro") contGalerias++;
            else if (edificio.nombreEdificio == "Gremio Comercial") contGremios++;
            else if (edificio.nombreEdificio == "Coliseo") contColiseos++;
        }

        // Ahora sí: se comprueba límite, NIVEL de castillo y RECURSOS.
        ConfigurarBotonConstruccion(botonConstruirCampamento, textoLimiteCampamento, "Campamento", contCampamentos, prefabCampamentoMineroReal);
        ConfigurarBotonConstruccion(botonConstruirGaleria, textoLimiteGaleria, "Arquería", contGalerias, prefabGaleriaTiroReal);
        ConfigurarBotonConstruccion(botonConstruirGremio, textoLimiteGremio, "Gremio", contGremios, prefabGremioComercialReal);
        ConfigurarBotonConstruccion(botonConstruirColiseo, textoLimiteColiseo, "Coliseo", contColiseos, prefabColiseoReal);
    }

    private void ConfigurarBotonConstruccion(Button boton, TextMeshProUGUI texto, string nombre, int cantidad, GameObject prefabReal)
    {
        if (boton == null || texto == null) return;

        EdificioReclutamiento datos = EdificioReclutamiento.DatosDePrefab(prefabReal);
        BaseMilitar castillo = ObtenerBaseJugador();

        // Sin datos o sin castillo no podemos evaluar nada
        if (datos == null || castillo == null)
        {
            texto.text = $"{nombre} (?)";
            texto.color = Color.gray;
            boton.interactable = false;
            return;
        }

        bool limiteOk = cantidad < limiteEdificios;
        bool nivelOk = castillo.nivelCastillo >= datos.nivelRequerido;
        bool recursosOk = castillo.madera >= datos.costeConstruccionMadera
                       && castillo.piedra >= datos.costeConstruccionPiedra
                       && castillo.oro >= datos.costeConstruccionOro;

        // El texto explica por qué no se puede, que es lo útil para el jugador
        if (!nivelOk)
        {
            texto.text = $"{nombre} — Nivel {datos.nivelRequerido}";
            texto.color = Color.red;
        }
        else if (!limiteOk)
        {
            texto.text = $"{nombre} ({cantidad}/{limiteEdificios}) — Límite";
            texto.color = Color.gray;
        }
        else
        {
            texto.text = $"{nombre} ({cantidad}/{limiteEdificios})  M:{datos.costeConstruccionMadera} P:{datos.costeConstruccionPiedra} O:{datos.costeConstruccionOro}";
            texto.color = recursosOk ? Color.white : Color.red;
        }

        boton.interactable = limiteOk && nivelOk && recursosOk;
    }

    public void CerrarPanelConstruccion()
    {
        if (panelConstruccion != null) panelConstruccion.SetActive(false);
        if (panelReclutamiento != null) panelReclutamiento.SetActive(true);
    }

    // --- CONSTRUCCIÓN (el panel solo se cierra si de verdad se pudo empezar) ---
    public void BotonConstruirCampamentoMinero()
    {
        if (sistemaConstruccion.EmpezarConstruccion(hologramaCampamentoMinero, prefabCampamentoMineroReal))
            CerrarPanelConstruccion();
    }

    public void BotonConstruirGaleriaDeTiro()
    {
        if (sistemaConstruccion.EmpezarConstruccion(hologramaGaleriaTiro, prefabGaleriaTiroReal))
            CerrarPanelConstruccion();
    }

    public void BotonConstruirGremioComercial()
    {
        if (sistemaConstruccion.EmpezarConstruccion(hologramaGremioComercial, prefabGremioComercialReal))
            CerrarPanelConstruccion();
    }

    public void BotonConstruirColiseo()
    {
        if (sistemaConstruccion.EmpezarConstruccion(hologramaColiseo, prefabColiseoReal))
            CerrarPanelConstruccion();
    }

    // Cierra TODOS los menús de golpe (castillo, construcción y edificios).
    // Lo llama Movimiento al hacer clic fuera, para que nunca se solapen paneles.
    public void CerrarTodosLosPaneles()
    {
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(false);
        CerrarMenuEdificio();
    }

    // --- INTERFAZ CONTEXTUAL DE EDIFICIOS SELECCIONADOS ---
    public void AbrirMenuEdificio(EdificioReclutamiento edificio)
    {
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(false);

        if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(false);
        if (panelGaleriaTiro != null) panelGaleriaTiro.SetActive(false);
        if (panelGremioComercial != null) panelGremioComercial.SetActive(false);
        if (panelColiseo != null) panelColiseo.SetActive(false);

        edificioSeleccionado = edificio;

        if (edificio.nombreEdificio == "Campamento Minero") { if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(true); }
        else if (edificio.nombreEdificio == "Galeria de Tiro") { if (panelGaleriaTiro != null) panelGaleriaTiro.SetActive(true); }
        else if (edificio.nombreEdificio == "Gremio Comercial") { if (panelGremioComercial != null) panelGremioComercial.SetActive(true); }
        else if (edificio.nombreEdificio == "Coliseo") { if (panelColiseo != null) panelColiseo.SetActive(true); }
    }

    public void BotonReclutarTropaEnEdificio()
    {
        if (edificioSeleccionado != null)
        {
            edificioSeleccionado.ComprarTropa();
        }
    }

    public void CerrarMenuEdificio()
    {
        if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(false);
        if (panelGaleriaTiro != null) panelGaleriaTiro.SetActive(false);
        if (panelGremioComercial != null) panelGremioComercial.SetActive(false);
        if (panelColiseo != null) panelColiseo.SetActive(false);

        edificioSeleccionado = null;
    }

    // --- MODO PRUEBA: BOTONES DEL CASTILLO SIEMPRE ACTIVOS ---
    public void MostrarPanelReclutamiento(bool mostrar, int nivelActual)
    {
        if (panelReclutamiento == null) return;

        panelReclutamiento.SetActive(mostrar);
        if (!mostrar) return;

        // Leñador y Caballero no tienen requisitos: siempre disponibles.
        if (botonLeñador != null) botonLeñador.interactable = true;
        if (botonCaballero != null) botonCaballero.interactable = true;
    }
}