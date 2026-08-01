using UnityEngine;

public class CamaraControlador : MonoBehaviour
{
    [Header("Ajustes de Movimiento")]
    public float velocidadArrastre = 25f;
    public float velocidadTeclado = 20f;

    [Header("Controles")]
    // El botón DERECHO queda libre para dar órdenes a las tropas.
    // La cámara se mueve con el botón CENTRAL, con WASD/flechas o llevando
    // el ratón al borde de la pantalla.
    public int botonArrastreCamara = 2; // 2 = botón central de la rueda
    public bool bordePantallaActivo = true;
    public float margenBorde = 15f;

    [Header("Ajustes de Zoom")]
    public float velocidadZoom = 500f; 
    public float alturaMinima = 3f;  
    public float alturaMaxima = 15f; 

    [Header("Ajustes de Inicio")]
    public float offsetCamaraZ = -10f; // Lo movemos aquí, que es su lugar natural

    void Update()
    {
        ManejarArrastre();
        ManejarTeclado();
        ManejarBordePantalla();
        ManejarZoom();
    }

    void ManejarArrastre()
    {
        if (Input.GetMouseButton(botonArrastreCamara))
        {
            float movX = -Input.GetAxis("Mouse X") * velocidadArrastre * Time.deltaTime;
            float movZ = -Input.GetAxis("Mouse Y") * velocidadArrastre * Time.deltaTime;

            transform.Translate(new Vector3(movX, 0, movZ), Space.World);
        }
    }

    // WASD o flechas
    void ManejarTeclado()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (h != 0f || v != 0f)
        {
            Vector3 mov = new Vector3(h, 0f, v) * velocidadTeclado * Time.unscaledDeltaTime;
            transform.Translate(mov, Space.World);
        }
    }

    // Llevar el ratón al borde de la pantalla desplaza la cámara
    void ManejarBordePantalla()
    {
        if (!bordePantallaActivo) return;

        Vector3 raton = Input.mousePosition;

        // Si el ratón está fuera de la ventana, no hacemos nada
        if (raton.x < 0 || raton.y < 0 || raton.x > Screen.width || raton.y > Screen.height) return;

        float x = 0f, z = 0f;

        if (raton.x <= margenBorde) x = -1f;
        else if (raton.x >= Screen.width - margenBorde) x = 1f;

        if (raton.y <= margenBorde) z = -1f;
        else if (raton.y >= Screen.height - margenBorde) z = 1f;

        if (x != 0f || z != 0f)
        {
            Vector3 mov = new Vector3(x, 0f, z) * velocidadTeclado * Time.unscaledDeltaTime;
            transform.Translate(mov, Space.World);
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