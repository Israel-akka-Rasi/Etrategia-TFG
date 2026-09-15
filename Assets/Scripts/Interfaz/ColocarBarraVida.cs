using UnityEngine;

public class ColocarBarraVida : MonoBehaviour
{
    private Transform camaraPrincipal;

    void Start()
    {
        camaraPrincipal = Camera.main.transform;
    }

    void LateUpdate()
    {
        // Esta línea es la clave de la estabilidad: 
        // Obliga a la barra a copiar la rotación EXACTA de la cámara.
        // Ignora cualquier rotación que el soldado tenga debido al movimiento en el NavMesh.
        // Así la barra se desliza estable sobre su cabeza, mirando siempre a la cámara plana.
        transform.rotation = camaraPrincipal.rotation;
    }
}