using System.Collections.Generic;
using UnityEngine;

public class Movimiento : MonoBehaviour
{
    [Header("Selección Múltiple (¡NUEVO!)")]
    // Ahora usamos una LISTA para guardar muchos soldados a la vez
    public List<Tropa> tropasSeleccionadas = new List<Tropa>(); 

    [Header("Interfaz de Caja")]
    public RectTransform cajaVisual; 
    private Vector2 posInicialRaton; 
    private bool arrastrando = false; 

void Update()
    {
        // =========================================================
        // CLIC IZQUIERDO (0): SELECCIONAR, COMPRAR O CAJA
        // =========================================================
        if (Input.GetMouseButtonDown(0))
        {
            posInicialRaton = Input.mousePosition;
            arrastrando = true;
            if (cajaVisual != null) cajaVisual.gameObject.SetActive(true);

            // Siempre que hacemos un clic nuevo, deseleccionamos el batallón anterior y ocultamos el panel UI
            DeseleccionarTodo();

            Ray rayo = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(rayo, out RaycastHit impacto))
            {
                Tropa tropaTocada = impacto.collider.GetComponent<Tropa>();
                BaseMilitar baseTocada = impacto.collider.GetComponent<BaseMilitar>();

                if (tropaTocada != null && tropaTocada.equipo == 0)
                {
                    // Clic en un soldado individual
                    tropasSeleccionadas.Add(tropaTocada);
                    tropaTocada.ActivarSeleccion(true);
                    
                    arrastrando = false; // Como hemos tocado a uno, no dibujamos la caja
                    if (cajaVisual != null) cajaVisual.gameObject.SetActive(false);

                    // --- ¡NUEVO: CONEXIÓN CON LA INTERFAZ! ---
                    Combate scriptCombate = impacto.collider.GetComponent<Combate>();
                    
                    if (scriptCombate != null)
                    {
                        // Le pasamos la vida desde Tropa y el daño desde Combate. 
                        // NOTA: Si en tu script Tropa las variables no se llaman "salud" y "saludMaxima", cámbialo por tus nombres reales.
                        UIManager.Instancia.MostrarInfoTropa(tropaTocada, (int)scriptCombate.dano);
                    }
                }
                else if (baseTocada != null)
                {
                    // Clic en el castillo
                    baseTocada.IntentarComprarTropaBasica(); // Actualizado a la nueva función de Edades
                    arrastrando = false;
                    if (cajaVisual != null) cajaVisual.gameObject.SetActive(false);
                }
            }
        }

        // --- MANTENER CLIC IZQUIERDO: DIBUJAR CAJA ---
        if (Input.GetMouseButton(0) && arrastrando && cajaVisual != null)
        {
            Vector2 posActual = Input.mousePosition;

            Vector2 esquinaInferiorIzquierda = new Vector2(Mathf.Min(posInicialRaton.x, posActual.x), Mathf.Min(posInicialRaton.y, posActual.y));
            Vector2 esquinaSuperiorDerecha = new Vector2(Mathf.Max(posInicialRaton.x, posActual.x), Mathf.Max(posInicialRaton.y, posActual.y));

            cajaVisual.anchoredPosition = esquinaInferiorIzquierda;
            cajaVisual.sizeDelta = esquinaSuperiorDerecha - esquinaInferiorIzquierda;
        }

        // --- SOLTAR CLIC IZQUIERDO: ATRAPAR SOLDADOS ---
        if (Input.GetMouseButtonUp(0))
        {
            if (arrastrando && cajaVisual != null)
            {
                cajaVisual.gameObject.SetActive(false);
                AtraparTropasEnCaja(); // Llamamos a la magia
            }
            arrastrando = false;
        }

        // =========================================================
        // CLIC DERECHO (1): ÓRDENES AL BATALLÓN (Movimiento Orgánico)
        // =========================================================
        if (Input.GetMouseButtonDown(1) && tropasSeleccionadas.Count > 0)
        {
            Ray rayo = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(rayo, out RaycastHit impacto))
            {
                Tropa enemigoTocado = impacto.collider.GetComponent<Tropa>();
                BaseMilitar baseTocada = impacto.collider.GetComponent<BaseMilitar>(); 
                Vector3 destinoCentral = impacto.point;

                float radioDeLlegada = 0.5f + (Mathf.Sqrt(tropasSeleccionadas.Count) * 0.4f); 

                foreach (Tropa soldado in tropasSeleccionadas)
                {
                    if (soldado == null) continue;

                    Combate scriptCombate = soldado.GetComponent<Combate>();

                    if (enemigoTocado != null && enemigoTocado.equipo == 1)
                    {
                        if (scriptCombate != null) scriptCombate.OrdenarAtaque(enemigoTocado.transform);
                    }
                    else if (baseTocada != null && baseTocada.equipo == 1)
                    {
                        if (scriptCombate != null) scriptCombate.OrdenarAtaque(baseTocada.transform);
                    }
                    else if (impacto.collider.CompareTag("Transitable"))
                    {
                        Vector2 offsetAleatorio = UnityEngine.Random.insideUnitCircle * radioDeLlegada;
                        Vector3 destinoAleatorio = destinoCentral + new Vector3(offsetAleatorio.x, 0, offsetAleatorio.y);

                        UnityEngine.AI.NavMeshHit navHit;
                        if (UnityEngine.AI.NavMesh.SamplePosition(destinoAleatorio, out navHit, radioDeLlegada, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            destinoAleatorio = navHit.position; 
                        }

                        if (scriptCombate != null) scriptCombate.CancelarAtaque();
                        soldado.OrdenarMovimiento(destinoAleatorio);
                    }
                }
            }
        }
    }

    // =========================================================
    // FUNCIONES AUXILIARES (La magia matemática)
    // =========================================================

    void DeseleccionarTodo()
    {
        foreach (Tropa soldado in tropasSeleccionadas)
        {
            if (soldado != null) soldado.ActivarSeleccion(false); // Apagamos los aros
        }
        tropasSeleccionadas.Clear(); // Vaciamos la lista
        
        // --- NUEVO: Apagamos el panel de la interfaz al hacer clic fuera ---
        if (UIManager.Instancia != null)
        {
            UIManager.Instancia.OcultarPanelSeleccion();
        }
    }

    void AtraparTropasEnCaja()
    {
        // 1. Calculamos las dimensiones del cuadrado invisible en la pantalla
        Vector2 posActual = Input.mousePosition;
        float minX = Mathf.Min(posInicialRaton.x, posActual.x);
        float maxX = Mathf.Max(posInicialRaton.x, posActual.x);
        float minY = Mathf.Min(posInicialRaton.y, posActual.y);
        float maxY = Mathf.Max(posInicialRaton.y, posActual.y);

        Rect rectanguloSeleccion = new Rect(minX, minY, maxX - minX, maxY - minY);

        // 2. Buscamos TODOS los objetos Tropa del mapa
        Tropa[] todasLasTropas = FindObjectsByType<Tropa>(FindObjectsSortMode.None);

        foreach (Tropa soldado in todasLasTropas)
        {
            // Solo queremos atrapar a los de nuestro equipo
            if (soldado.equipo == 0)
            {
                // Traducimos su posición 3D al cristal de la pantalla (2D)
                Vector3 posicionEnPantalla = Camera.main.WorldToScreenPoint(soldado.transform.position);

                // Si las coordenadas de pantalla del soldado caen dentro de la caja...
                if (rectanguloSeleccion.Contains(posicionEnPantalla))
                {
                    tropasSeleccionadas.Add(soldado); // Lo metemos en la lista
                    soldado.ActivarSeleccion(true);   // Le encendemos el aro
                }
            }
        }
    }
}