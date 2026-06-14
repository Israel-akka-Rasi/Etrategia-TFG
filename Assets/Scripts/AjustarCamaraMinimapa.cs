using UnityEngine;

public class AjustarCamaraMinimapa : MonoBehaviour
{
    private GeneradorTablero generador;

    void Start()
    {
        // Esperamos un segundo a que el mapa termine de generarse procedimentalmente
        Invoke("CentrarYEnfocar", 1f);
    }

    void CentrarYEnfocar()
    {
        generador = FindFirstObjectByType<GeneradorTablero>();
        if (generador == null) return;

        // Calculamos dónde está el centro exacto de tu mapa gigante
        float centroX = (generador.anchoMapa * generador.distanciaX) / 2f;
        float centroZ = (generador.altoMapa * generador.distanciaZ) / 2f;

        // Colocamos la cámara del minimapa en el centro y muy arriba
        transform.position = new Vector3(centroX, 100f, centroZ);
        
        // La rotamos 90 grados hacia abajo para que mire al suelo
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Ajustamos el "zoom" ortográfico para que encuadre todo el terreno
        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(centroX, centroZ);
    }
}