using UnityEngine;
using UnityEngine.EventSystems;

public class ControladorMinimapa : MonoBehaviour, IPointerClickHandler
{
    [Header("Arrastra aquí el Mapa Ampliado")]
    public GameObject mapaAmpliado;

    // Esta función detecta el clic
    public void OnPointerClick(PointerEventData eventData)
    {
        // Si el mapa gigante está apagado, lo enciende. Si está encendido, lo apaga.
        mapaAmpliado.SetActive(!mapaAmpliado.activeSelf);
    }
}