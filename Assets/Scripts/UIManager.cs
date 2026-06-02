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

    [Header("Panel de Selección")]
    public GameObject panelSeleccion; 
    public TextMeshProUGUI txtNombreUnidad;
    public TextMeshProUGUI txtVidaUnidad;
    public TextMeshProUGUI txtAtaqueUnidad;
    public Image imgRetratoUnidad;

    [Header("Menús")]
    public GameObject panelReclutamiento;

    [Header("Botones de Reclutamiento (Castillo)")]
    public Button botonCaballero;
    public Button botonMinero;
    public Button botonTesorero;

    [Header("Textos de Requisitos (Castillo)")]
    public TMPro.TextMeshProUGUI textoRequisitoMinero;
    public TMPro.TextMeshProUGUI textoRequisitoTesorero;

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

    public void ActualizarEconomia(int nivel, int popActual, int popMax, int madera, int piedra, int oro)
    {
        txtNivelCastillo.text = "Nivel: " + nivel;
        txtPoblacion.text = "Población: " + popActual + " / " + popMax;
        txtMadera.text = "Madera: " + madera;
        txtPiedra.text = "Piedra: " + piedra;
        txtOro.text = "Oro: " + oro;
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

    public void BotonMejorarCastillo()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach(BaseMilitar b in bases) { if(b.equipo == 0) { b.MejorarCastillo(); break; } }
    }

    public void BotonComprarCaballero()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases) { if (b.equipo == 0) { b.ComprarCaballero(); return; } }
    }

    public void BotonComprarMinero()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases) { if (b.equipo == 0) { b.ComprarMinero(); break; } }
    }

    public void BotonComprarTesorero()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases) { if (b.equipo == 0) { b.ComprarTesorero(); break; } }
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

        // MODO PRUEBA: Quitamos el nivel del castillo
        ConfigurarBotonConstruccion(botonConstruirCampamento, textoLimiteCampamento, "Campamento", contCampamentos);
        ConfigurarBotonConstruccion(botonConstruirGaleria, textoLimiteGaleria, "Arquería", contGalerias);
        ConfigurarBotonConstruccion(botonConstruirGremio, textoLimiteGremio, "Gremio", contGremios);
        ConfigurarBotonConstruccion(botonConstruirColiseo, textoLimiteColiseo, "Coliseo", contColiseos);
    }

    private void ConfigurarBotonConstruccion(Button boton, TextMeshProUGUI texto, string nombre, int cantidad)
    {
        if (boton == null || texto == null) return;

        texto.text = $"{nombre} ({cantidad}/{limiteEdificios}) [TEST]";
        texto.color = Color.yellow; 
        boton.interactable = (cantidad < limiteEdificios); 
    }

    public void CerrarPanelConstruccion()
    {
        if (panelConstruccion != null) panelConstruccion.SetActive(false);
        if (panelReclutamiento != null) panelReclutamiento.SetActive(true); 
    }

    // --- MODO PRUEBA: EDIFICIOS GRATIS ---
    public void BotonConstruirCampamentoMinero()
    {
        sistemaConstruccion.EmpezarConstruccion(hologramaCampamentoMinero, prefabCampamentoMineroReal);
        CerrarPanelConstruccion();
    }

    public void BotonConstruirGaleriaDeTiro()
    {
        sistemaConstruccion.EmpezarConstruccion(hologramaGaleriaTiro, prefabGaleriaTiroReal);
        CerrarPanelConstruccion();
    }

    public void BotonConstruirGremioComercial()
    {
        sistemaConstruccion.EmpezarConstruccion(hologramaGremioComercial, prefabGremioComercialReal);
        CerrarPanelConstruccion();
    }

    public void BotonConstruirColiseo()
    {
        sistemaConstruccion.EmpezarConstruccion(hologramaColiseo, prefabColiseoReal);
        CerrarPanelConstruccion();
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

        botonCaballero.interactable = true;
        
        botonMinero.interactable = true;
        if(textoRequisitoMinero != null) { textoRequisitoMinero.text = "Minero [TEST]"; textoRequisitoMinero.color = Color.yellow; }

        botonTesorero.interactable = true;
        if(textoRequisitoTesorero != null) { textoRequisitoTesorero.text = "Tesorero [TEST]"; textoRequisitoTesorero.color = Color.yellow; }
    }
}