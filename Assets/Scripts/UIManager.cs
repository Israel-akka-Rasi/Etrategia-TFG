using UnityEngine;
using TMPro; // Necesario para TextMeshPro

public class UIManager : MonoBehaviour
{
    // Esto permite que cualquier script llame a UIManager.Instancia.Funcion()
    public static UIManager Instancia;

    [Header("Panel Económico (Siempre Visible)")]
    public TextMeshProUGUI txtNivelCastillo;
    public TextMeshProUGUI txtPoblacion;
    public TextMeshProUGUI txtMadera;
    public TextMeshProUGUI txtPiedra;
    public TextMeshProUGUI txtOro;

    [Header("Panel de Selección (Contextual)")]
    public GameObject panelSeleccion; // El fondo del panel para poder ocultarlo entero
    public TextMeshProUGUI txtNombreUnidad;
    public TextMeshProUGUI txtVidaUnidad;
    public TextMeshProUGUI txtAtaqueUnidad;

    void Awake()
    {
        // Configuramos el Singleton
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Al empezar la partida, nos aseguramos de que el panel de tropas esté oculto
        OcultarPanelSeleccion();
    }

    // --- FUNCIONES PARA LA ECONOMÍA ---
    public void ActualizarEconomia(int nivel, int popActual, int popMax, int madera, int piedra, int oro)
    {
        txtNivelCastillo.text = "Nivel del Castillo: " + nivel;
        txtPoblacion.text = popActual + " / " + popMax;
        txtMadera.text = madera.ToString();
        txtPiedra.text = piedra.ToString();
        txtOro.text = oro.ToString();
    }

    // --- FUNCIONES PARA LA SELECCIÓN DE TROPAS ---
    public void MostrarInfoTropa(string nombre, int vidaActual, int vidaMax, int ataque)
    {
        panelSeleccion.SetActive(true); // Encendemos el panel
        txtNombreUnidad.text = nombre;
        txtVidaUnidad.text = "HP: " + vidaActual + " / " + vidaMax;
        txtAtaqueUnidad.text = "Daño: " + ataque;
    }

    public void OcultarPanelSeleccion()
    {
        panelSeleccion.SetActive(false); // Apagamos el panel si hacemos clic en la hierba
    }
}