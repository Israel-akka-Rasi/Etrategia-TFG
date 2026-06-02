using UnityEngine;

public class CamaraControlador : MonoBehaviour
{
    [Header("Ajustes de Movimiento")]
    public float velocidadArrastre = 25f;

    [Header("Ajustes de Zoom")]
    public float velocidadZoom = 500f; 
    public float alturaMinima = 3f;  
    public float alturaMaxima = 15f; 

    [Header("Ajustes de Inicio")]
    public float offsetCamaraZ = -10f; // Lo movemos aquí, que es su lugar natural

    void Update()
    {
        ManejarArrastre();
        ManejarZoom();
    }

    void ManejarArrastre()
    {
        if (Input.GetMouseButton(1))
        {
            float movX = -Input.GetAxis("Mouse X") * velocidadArrastre * Time.deltaTime;
            float movZ = -Input.GetAxis("Mouse Y") * velocidadArrastre * Time.deltaTime;

            transform.Translate(new Vector3(movX, 0, movZ), Space.World);
        }
    }

    void ManejarZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (scroll != 0f)
        {
            Vector3 movimiento = transform.forward * scroll * velocidadZoom * Time.deltaTime;
            Vector3 nuevaPosicion = transform.position + movimiento;

            if (nuevaPosicion.y >= alturaMinima && nuevaPosicion.y <= alturaMaxima)
            {
                transform.position = nuevaPosicion;
            }
        }
    }

    // --- NUEVO: El puente para que otros scripts muevan la cámara ---
    public void CentrarEn(Vector3 posicionObjetivo)
    {
        // Mantiene la altura actual (Y), pero centra la cámara en X y le aplica el desvío en Z
        transform.position = new Vector3(posicionObjetivo.x, transform.position.y, posicionObjetivo.z + offsetCamaraZ);
    }
}