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

    [Header("Botones de Reclutamiento")]
    public Button botonCaballero;
    public Button botonMinero;
    public Button botonTesorero;

    [Header("Textos de Requisitos")]
    public TMPro.TextMeshProUGUI textoRequisitoMinero;
    public TMPro.TextMeshProUGUI textoRequisitoTesorero;

    [Header("Nuevos Paneles y Construcción")]
    public GameObject panelConstruccion;
    public SistemaConstruccion sistemaConstruccion; // Enlázalo en el Inspector

    [Header("Prefabs de Edificios")]
    public GameObject hologramaCampamentoMinero;
    public GameObject prefabCampamentoMineroReal;

    [Header("Límites de Construcción")]
    public int limiteEdificios = 3;
    public Button botonConstruirCampamento; // Arrastra aquí tu botón del menú de construir
    public TMPro.TextMeshProUGUI textoLimiteCampamento; // Opcional: Para mostrar (1/3)

    [Header("Menú de Edificios Seleccionados")]
    public GameObject panelCampamentoMinero; // Tu nuevo panel de interfaz para el campamento
    private EdificioReclutamiento edificioSeleccionado; // Guarda el edificio físico que tocaste

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
        // Si el panel derecho está encendido...
        if (panelSeleccion.activeSelf)
        {
            // Y la tropa sigue viva...
            if (tropaSeleccionada != null)
            {
                // Actualizamos su vida constantemente como un radar médico
                txtVidaUnidad.text = "HP: " + (int)tropaSeleccionada.salud + " / " + (int)tropaSeleccionada.saludMaxima;
            }
            else
            {
                // Si la tropa es 'null' (porque acaba de morir en combate), cerramos el panel
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

    // --- ACTUALIZADO: Ahora le pasamos la "Tropa" entera en lugar de números sueltos ---
    public void MostrarInfoTropa(Tropa tropa, int ataque)
    {
        tropaSeleccionada = tropa; // Memorizamos a quién estamos mirando
        panelSeleccion.SetActive(true);
        
        txtNombreUnidad.text = tropa.gameObject.name.Replace("(Clone)", "");
        txtAtaqueUnidad.text = "Daño: " + ataque;
        
        // (La vida ya no hace falta ponerla aquí porque el Update() se encarga de ella automáticamente)

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
        tropaSeleccionada = null; // Soltamos a la tropa
        panelSeleccion.SetActive(false); 
    }

    public void BotonMejorarCastillo()
    {
        // Buscamos todos los castillos en el mapa
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach(BaseMilitar b in bases)
        {
            // Si es nuestro castillo (equipo 0), lo mejoramos
            if(b.equipo == 0) 
            {
                b.MejorarCastillo();
                break; // Paramos de buscar
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
                return; // Usamos return en vez de break para salir del todo
            }
        }
        
        // Si el código llega hasta aquí, es que no encontró ninguna base tuya
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

    public void AbrirPanelConstruccion()
    {
        // 1. Ocultamos el panel principal y abrimos el de construcción
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(true);

        // 2. ESCÁNER: Contamos cuántos Campamentos vivos hay en el mapa
        EdificioReclutamiento[] edificiosEnMapa = FindObjectsOfType<EdificioReclutamiento>();
        int contadorCampamentos = 0;
        
        foreach (EdificioReclutamiento edificio in edificiosEnMapa)
        {
            // Comprobamos el nombre exacto que le pusiste en el script del edificio
            if (edificio.nombreEdificio == "Campamento Minero")
            {
                contadorCampamentos++;
            }
        }

        // 3. Actualizamos el texto visual (Ej: "Campamento Minero 1/3")
        if (textoLimiteCampamento != null)
        {
            textoLimiteCampamento.text = $"Campamento ({contadorCampamentos}/{limiteEdificios})";
        }

        // 4. Bloqueamos o desbloqueamos el botón según el límite
        if (contadorCampamentos >= limiteEdificios)
        {
            botonConstruirCampamento.interactable = false; // Límite alcanzado
        }
        else
        {
            botonConstruirCampamento.interactable = true;  // Aún puedes construir
        }
    }

    // Esta función va en el OnClick del botón "Volver" o la "X"
    public void CerrarPanelConstruccion()
    {
        panelConstruccion.SetActive(false);
        panelReclutamiento.SetActive(true); // Volvemos al panel del castillo
    }

    // Esta función va en el OnClick del botón "Construir Campamento Minero"
    public void BotonConstruirCampamentoMinero()
    {
        // 1. Comprobamos si hay nivel y recursos suficientes (Ejemplo: 50 Madera)
        BaseMilitar miBase = FindObjectOfType<BaseMilitar>();
        
        if (miBase != null && miBase.nivelCastillo >= 5 && miBase.madera >= 50)
        {
            // 2. Le pasamos los prefabs al Sistema de Construcción para que salga el holograma
            sistemaConstruccion.EmpezarConstruccion(hologramaCampamentoMinero, prefabCampamentoMineroReal);
            
            // 3. (Opcional) Cerramos el panel para que el jugador vea bien dónde va a construir
            CerrarPanelConstruccion();
        }
        else
        {
            Debug.Log("No tienes nivel 5 o te faltan recursos para construir esto.");
        }
    }
    public void BotonConstruirCampamentoMineroPrueba()
    {
        if (sistemaConstruccion != null)
        {
            // Llamamos al sistema y le pasamos los prefabs
            sistemaConstruccion.EmpezarConstruccion(hologramaCampamentoMinero, prefabCampamentoMineroReal);
            
            // Opcional: Cerramos el panel para que no moleste al poner el edificio
            panelConstruccion.SetActive(false);
        }
    }

    // Modificamos este método para que chequee el nivel al abrirse
    public void MostrarPanelReclutamiento(bool mostrar, int nivelActual)
    {
        if (panelReclutamiento == null) return;

        panelReclutamiento.SetActive(mostrar);
        if (!mostrar) return;

        // 1. CABALLERO: Siempre disponible
        botonCaballero.interactable = true;

        // 2. MINERO: Nivel 5
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

        // 3. TESORERO: Nivel 10
        /*if (nivelActual >= 10)
        {
            botonTesorero.interactable = true;
            if(textoRequisitoTesorero != null) {
                textoRequisitoTesorero.text = "Tesorero (50 Mad / 30 Pied)";
                textoRequisitoTesorero.color = Color.white;
            }
        }
        else
        {
            botonTesorero.interactable = false;
            if(textoRequisitoTesorero != null) {
                textoRequisitoTesorero.text = "Bloqueado (Nv. 10)";
                textoRequisitoTesorero.color = Color.red;
            }
        }*/
    }
    public void AbrirMenuEdificio(EdificioReclutamiento edificio)
    {
        // 1. Cerramos los menús del Castillo y de Construcción para que no se solapen
        if (panelReclutamiento != null) panelReclutamiento.SetActive(false);
        if (panelConstruccion != null) panelConstruccion.SetActive(false);

        // 2. Guardamos la referencia de QUÉ edificio exacto hemos pinchado
        edificioSeleccionado = edificio;

        // 3. Si es un Campamento Minero, abrimos su panel exclusivo
        if (edificio.nombreEdificio == "Campamento Minero")
        {
            if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(true);
        }
    }

    // Esta función la llamará tu BOTÓN FÍSICO de "Reclutar Minero"
    public void BotonReclutarMineroEnEdificio()
    {
        if (edificioSeleccionado != null)
        {
            // Llama directamente a la función unificada de TU script
            edificioSeleccionado.ComprarTropa();
        }
    }

    // Para el botón de cerrar (X) del panel del campamento
    public void CerrarMenuEdificio()
    {
        // 1. Apagamos el panel del campamento
        if (panelCampamentoMinero != null) panelCampamentoMinero.SetActive(false);
        
        // 2. Limpiamos la memoria del edificio seleccionado
        edificioSeleccionado = null;
    }
}