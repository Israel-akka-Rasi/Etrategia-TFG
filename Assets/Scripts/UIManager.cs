using UnityEngine;
using UnityEngine.UI; 
using TMPro;

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

    // --- ¡NUEVO! El vigía ---
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

    // --- NUEVO: VIGILANCIA EN TIEMPO REAL ---
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
}