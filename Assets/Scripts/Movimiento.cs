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
    // =========================================================
    // CLIC IZQUIERDO (0): SELECCIONAR, COMPRAR O CAJA
    // =========================================================
    if (Input.GetMouseButtonDown(0))
    {
        // --- ¡EL ESCUDO ANTI-CLICS FANTASMA! ---
        // Si el ratón está sobre un elemento de la interfaz (como un botón o panel), ignoramos el resto.
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return; 
        }

        posInicialRaton = Input.mousePosition;
        arrastrando = true;
        if (cajaVisual != null) cajaVisual.gameObject.SetActive(true);

        DeseleccionarTodo();

        Ray rayo = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(rayo, out RaycastHit impacto))
        {
            Tropa tropaTocada = impacto.collider.GetComponent<Tropa>();
            BaseMilitar baseTocada = impacto.collider.GetComponent<BaseMilitar>();

            if (tropaTocada != null && tropaTocada.equipo == 0)
            {
                tropasSeleccionadas.Add(tropaTocada);
                tropaTocada.ActivarSeleccion(true);
                
                arrastrando = false; 
                if (cajaVisual != null) cajaVisual.gameObject.SetActive(false);

                Combate scriptCombate = impacto.collider.GetComponent<Combate>();
                if (scriptCombate != null)
                {
                    UIManager.Instancia.MostrarInfoTropa(tropaTocada, (int)scriptCombate.dano);
                }
            }
            else if (baseTocada != null)
            {
                // Clic en el castillo: ABRIMOS EL MENÚ en lugar de comprar
                if (baseTocada.equipo == 0) // Solo abrimos el menú si es nuestro castillo
                {
                    UIManager.Instancia.MostrarPanelReclutamiento(true,baseTocada.nivelCastillo);
                }
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

    // --- SOLTAR CLIC IZQUIERDO ---
    if (Input.GetMouseButtonUp(0))
    {
        if (arrastrando && cajaVisual != null)
        {
            cajaVisual.gameObject.SetActive(false);
            AtraparTropasEnCaja(); 
        }
        arrastrando = false;
    }

    // =========================================================
    // CLIC DERECHO (1): ÓRDENES (ATAQUE, RECOLECCIÓN O MOVIMIENTO)
    // =========================================================
    if (Input.GetMouseButtonDown(1) && tropasSeleccionadas.Count > 0)
    {
        Ray rayo = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(rayo, out RaycastHit impacto))
        {
            Tropa enemigoTocado = impacto.collider.GetComponent<Tropa>();
            BaseMilitar baseTocada = impacto.collider.GetComponent<BaseMilitar>();

            Debug.Log("He hecho clic derecho en: " + impacto.collider.gameObject.name);

            Recurso recursoTocado = impacto.collider.GetComponentInParent<Recurso>(); // <--- NUEVO
            Vector3 destinoCentral = impacto.point;

            float radioDeLlegada = 0.5f + (Mathf.Sqrt(tropasSeleccionadas.Count) * 0.4f); 

            foreach (Tropa soldado in tropasSeleccionadas)
            {
                if (soldado == null) continue;

                Combate scriptCombate = soldado.GetComponent<Combate>();
                Recolector scriptRecolector = soldado.GetComponent<Recolector>(); // <--- NUEVO

                // 1. ORDEN DE RECOLECCIÓN
                if (recursoTocado != null && scriptRecolector != null)
                {
                    bool puedeTrabajar = false;
                    if (recursoTocado.tipo == Recurso.TipoRecurso.Madera && scriptRecolector.puedeMadera) puedeTrabajar = true;
                    if (recursoTocado.tipo == Recurso.TipoRecurso.Piedra && scriptRecolector.puedePiedra) puedeTrabajar = true;
                    if (recursoTocado.tipo == Recurso.TipoRecurso.Oro && scriptRecolector.puedeOro) puedeTrabajar = true;

                    if (puedeTrabajar)
                    {
                        if (scriptCombate != null) scriptCombate.CancelarAtaque();
                        scriptRecolector.OrdenarRecoleccion(recursoTocado);
                    }
                    else
                    {
                        Debug.Log(soldado.name + " no tiene herramientas para " + recursoTocado.tipo);
                    }
                }
                // 2. ORDEN DE ATAQUE (Tropa o Edificio)
                else if ((enemigoTocado != null && enemigoTocado.equipo == 1) || (baseTocada != null && baseTocada.equipo == 1))
                {
                    if (scriptRecolector != null) scriptRecolector.DetenerTrabajo();
                    Transform tObjetivo = enemigoTocado != null ? enemigoTocado.transform : baseTocada.transform;
                    
                    if (scriptCombate != null) scriptCombate.OrdenarAtaque(tObjetivo);
                }
                // 3. ORDEN DE MOVIMIENTO AL SUELO
                else if (impacto.collider.CompareTag("Transitable"))
                {
                    if (scriptRecolector != null) scriptRecolector.DetenerTrabajo();
                    if (scriptCombate != null) scriptCombate.CancelarAtaque();

                    Vector2 offsetAleatorio = UnityEngine.Random.insideUnitCircle * radioDeLlegada;
                    Vector3 destinoAleatorio = destinoCentral + new Vector3(offsetAleatorio.x, 0, offsetAleatorio.y);

                    UnityEngine.AI.NavMeshHit navHit;
                    if (UnityEngine.AI.NavMesh.SamplePosition(destinoAleatorio, out navHit, radioDeLlegada, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        destinoAleatorio = navHit.position; 
                    }

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
            UIManager.Instancia.MostrarPanelReclutamiento(false,0);
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