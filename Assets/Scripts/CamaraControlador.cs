using UnityEngine;

public class CamaraControlador : MonoBehaviour
{
    [Header("Ajustes de Movimiento")]
    public float velocidadArrastre = 25f;

    [Header("Ajustes de Zoom")]
    public float velocidadZoom = 500f; // Valores altos porque la ruleta del ratón da números muy pequeños
    public float alturaMinima = 3f;  // Para no atravesar el suelo
    public float alturaMaxima = 15f; // Para no irnos al espacio

    void Update()
    {
        ManejarArrastre();
        ManejarZoom();
    }

    void ManejarArrastre()
    {
        // En Unity: 0 es Clic Izquierdo, 1 es Clic Derecho y 2 es el Botón Central (Ruleta)
        if (Input.GetMouseButton(1))
        {
            float movX = -Input.GetAxis("Mouse X") * velocidadArrastre * Time.deltaTime;
            float movZ = -Input.GetAxis("Mouse Y") * velocidadArrastre * Time.deltaTime;

            transform.Translate(new Vector3(movX, 0, movZ), Space.World);
        }
    }

    void ManejarZoom()
    {
        // Leemos la ruleta del ratón
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (scroll != 0f)
        {
            // Calculamos el movimiento hacia adelante/atrás en el ángulo de la cámara
            Vector3 movimiento = transform.forward * scroll * velocidadZoom * Time.deltaTime;
            Vector3 nuevaPosicion = transform.position + movimiento;

            // Bloqueamos la altura para no atravesar el mapa ni irnos demasiado lejos
            if (nuevaPosicion.y >= alturaMinima && nuevaPosicion.y <= alturaMaxima)
            {
                transform.position = nuevaPosicion;
            }
        }
    }
}