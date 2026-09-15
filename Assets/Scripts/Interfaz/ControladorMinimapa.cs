using UnityEngine;
using UnityEngine.EventSystems;

public class ControladorMinimapa : MonoBehaviour, IPointerClickHandler
{
    [Header("Arrastra aquí el Mapa Ampliado")]
    public GameObject mapaAmpliado;

    void Start()
    {
        // Al empezar la partida el mapa grande está OCULTO.
        // Solo aparecerá cuando el jugador haga clic en el minimapa.
        if (mapaAmpliado != null) mapaAmpliado.SetActive(false);
    }

    // Detecta el clic sobre el minimapa
    public void OnPointerClick(PointerEventData eventData)
    {
        // Si el mapa grande está apagado, lo enciende. Si está encendido, lo apaga.
        if (mapaAmpliado != null)
        {
            mapaAmpliado.SetActive(!mapaAmpliado.activeSelf);
        }
    }
}