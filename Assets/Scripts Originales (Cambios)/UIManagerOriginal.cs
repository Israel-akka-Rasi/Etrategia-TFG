/*using UnityEngine;
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
        else if (imgRetratoUnidad != null)
        {
            imgRetratoUnidad.color = new Color(1, 1, 1, 0); 
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
        foreach(BaseMilitar b in bases)
        {
            if(b.equipo == 0) 
            {
                b.MejorarCastillo();
                break; 
            }
        }
    }

    public void BotonComprarCaballero()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == 0) 
            {
                b.ComprarCaballero();
                return; 
            }
        }
        Debug.LogWarning("ERROR: No se ha encontrado ninguna base con el equipo 0.");
    }

    public void BotonComprarMinero()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == 0) 
            {
                b.ComprarMinero();
                break;
            }
        }
    }

    public void BotonComprarTesorero()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == 0) 
            {
                b.ComprarTesorero();
                break;
            }
        }
    }

    // --- SISTEMA DE CONSTRUCCIÓN CON CANDADOS Y ESCÁNER ---
    public void AbrirPanelConstruccion()
    {
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(true);

        int nivelActualCastillo = 1;
        BaseMilitar miCastillo = FindObjectOfType<BaseMilitar>();
        if (miCastillo != null) nivelActualCastillo = miCastillo.nivelCastillo;

        // Contadores para el mapa
        int contCampamentos = 0;
        int contGalerias = 0;
        int contGremios = 0;
        int contColiseos = 0;
        
        EdificioReclutamiento[] edificiosEnMapa = FindObjectsOfType<EdificioReclutamiento>();
        foreach (EdificioReclutamiento edificio in edificiosEnMapa)
        {
            if (edificio.nombreEdificio == "Campamento Minero") contCampamentos++;
            else if (edificio.nombreEdificio == "Galeria de Tiro") contGalerias++;
            else if (edificio.nombreEdificio == "Gremio Comercial") contGremios++;
            else if (edificio.nombreEdificio == "Coliseo") contColiseos++;
        }

        // Configuración de los 4 botones según Nivel Requerido e Historial en el Mapa
        ConfigurarBotonConstruccion(botonConstruirCampamento, textoLimiteCampamento, "Campamento", contCampamentos, nivelActualCastillo, 5);
        ConfigurarBotonConstruccion(botonConstruirGaleria, textoLimiteGaleria, "Arquería", contGalerias, nivelActualCastillo, 8);
        ConfigurarBotonConstruccion(botonConstruirGremio, textoLimiteGremio, "Gremio", contGremios, nivelActualCastillo, 11);
        ConfigurarBotonConstruccion(botonConstruirColiseo, textoLimiteColiseo, "Coliseo", contColiseos, nivelActualCastillo, 14);
    }

    // Método auxiliar unificado para aplicar reglas visuales y funcionales a los botones
    private void ConfigurarBotonConstruccion(Button boton, TextMeshProUGUI texto, string nombre, int cantidad, int nivelCastillo, int nivelRequisito)
    {
        if (boton == null || texto == null) return;

        if (nivelCastillo < nivelRequisito)
        {
            texto.text = $"Bloqueado (Nv. {nivelRequisito})";
            texto.color = Color.red;
            boton.interactable = false;
        }
        else
        {
            texto.text = $"{nombre} ({cantidad}/{limiteEdificios})";
            texto.color = Color.white;
            boton.interactable = (cantidad < limiteEdificios);
        }
    }

    public void CerrarPanelConstruccion()
    {
        if (panelConstruccion != null) panelConstruccion.SetActive(false);
        if (panelReclutamiento != null) panelReclutamiento.SetActive(true); 
    }

    // --- ACCIONES DE LOS BOTONES DE CONSTRUCCIÓN (COMPROBACIÓN DE RECURSOS) ---

    public void BotonConstruirCampamentoMinero()
    {
        BaseMilitar miBase = FindObjectOfType<BaseMilitar>();
        if (miBase != null && miBase.nivelCastillo >= 5 && miBase.madera >= 50)
        {
            sistemaConstruccion.EmpezarConstruccion(hologramaCampamentoMinero, prefabCampamentoMineroReal);
            CerrarPanelConstruccion();
        }
        else
        {
            Debug.Log("No cumples los requisitos de nivel o recursos para el Campamento.");
        }
    }

    public void BotonConstruirGaleriaDeTiro()
    {
        BaseMilitar miBase = FindObjectOfType<BaseMilitar>();
        if (miBase != null && miBase.nivelCastillo >= 8 && miBase.madera >= 30)
        {
            sistemaConstruccion.EmpezarConstruccion(hologramaGaleriaTiro, prefabGaleriaTiroReal);
            CerrarPanelConstruccion();
        }
        else
        {
            Debug.Log("No cumples los requisitos de nivel o recursos para la Galería de Tiro.");
        }
    }

    public void BotonConstruirGremioComercial()
    {
        BaseMilitar miBase = FindObjectOfType<BaseMilitar>();
        if (miBase != null && miBase.nivelCastillo >= 11 && miBase.madera >= 40 && miBase.piedra >= 20)
        {
            sistemaConstruccion.EmpezarConstruccion(hologramaGremioComercial, prefabGremioComercialReal);
            CerrarPanelConstruccion();
        }
        else
        {
            Debug.Log("No cumples los requisitos de nivel o recursos para el Gremio Comercial.");
        }
    }

    public void BotonConstruirColiseo()
    {
        BaseMilitar miBase = FindObjectOfType<BaseMilitar>();
        if (miBase != null && miBase.nivelCastillo >= 14 && miBase.madera >= 50 && miBase.piedra >= 30 && miBase.oro >= 10)
        {
            sistemaConstruccion.EmpezarConstruccion(hologramaColiseo, prefabColiseoReal);
            CerrarPanelConstruccion();
        }
        else
        {
            Debug.Log("No cumples los requisitos de nivel o recursos para el Coliseo.");
        }
    }

    // --- INTERFAZ CONTEXTUAL DE EDIFICIOS SELECCIONADOS ---
    public void AbrirMenuEdificio(EdificioReclutamiento edificio)
    {
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(false);

        // Apagar cualquier panel de edificio que estuviera abierto previamente
        if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(false);
        if (panelGaleriaTiro != null) panelGaleriaTiro.SetActive(false);
        if (panelGremioComercial != null) panelGremioComercial.SetActive(false);
        if (panelColiseo != null) panelColiseo.SetActive(false);

        edificioSeleccionado = edificio;

        // Mostrar el panel correcto según el nombre configurado en el edificio
        if (edificio.nombreEdificio == "Campamento Minero")
        {
            if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(true);
        }
        else if (edificio.nombreEdificio == "Galeria de Tiro")
        {
            if (panelGaleriaTiro != null) panelGaleriaTiro.SetActive(true);
        }
        else if (edificio.nombreEdificio == "Gremio Comercial")
        {
            if (panelGremioComercial != null) panelGremioComercial.SetActive(true);
        }
        else if (edificio.nombreEdificio == "Coliseo")
        {
            if (panelColiseo != null) panelColiseo.SetActive(true);
        }
    }

    // Botón físico de reclutamiento universal (sirve para cualquier panel clonado)
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

    // Mantenemos este método intacto por si tu BaseMilitar lo requiere al arrancar o actualizarse
    public void MostrarPanelReclutamiento(bool mostrar, int nivelActual)
    {
        if (panelReclutamiento == null) return;

        panelReclutamiento.SetActive(mostrar);
        if (!mostrar) return;

        botonCaballero.interactable = true;

        if (nivelActual >= 5)
        {
            botonMinero.interactable = true;
            if(textoRequisitoMinero != null) {
                textoRequisitoMinero.text = "Minero (20 Madera)";
                textoRequisitoMinero.color = Color.white;
            }
        }
        else
        {
            botonMinero.interactable = false;
            if(textoRequisitoMinero != null) {
                textoRequisitoMinero.text = "Bloqueado (Nv. 5)";
                textoRequisitoMinero.color = Color.red;
            }
        }
    }
}*/