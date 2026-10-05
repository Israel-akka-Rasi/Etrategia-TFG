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
            // El collider puede estar en un hijo del edificio, por eso InParent
            EdificioReclutamiento edificioTocado = impacto.collider.GetComponentInParent<EdificioReclutamiento>();

            if (tropaTocada != null && tropaTocada.equipo == 0)
            {
                tropasSeleccionadas.Add(tropaTocada);
                tropaTocada.ActivarSeleccion(true);
                
                arrastrando = false; 
                if (cajaVisual != null) cajaVisual.gameObject.SetActive(false);

                // Las unidades sin script de Combate (recolectores) también deben
                // mostrar su ficha: simplemente aparecen con 0 de daño.
                Combate scriptCombate = impacto.collider.GetComponent<Combate>();
                int danoUnidad = (scriptCombate != null) ? scriptCombate.dano : 0;
                UIManager.Instancia.MostrarInfoTropa(tropaTocada, danoUnidad);
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
            else if (edificioTocado != null && edificioTocado.equipo == 0)
            {
                // Clic en uno de NUESTROS edificios: abrimos su menú.
                // Se hace aquí (y no en OnMouseDown) porque DeseleccionarTodo ya ha
                // cerrado el resto de paneles justo antes, así que nunca se solapan.
                UIManager.Instancia.AbrirMenuEdificio(edificioTocado);

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
            
            // DEPURACIÓN: avisar si el clic no va a producir ninguna orden
            bool esSuelo = impacto.collider.CompareTag("Transitable");
            bool esEnemigo = (enemigoTocado != null && enemigoTocado.equipo == 1) || (baseTocada != null && baseTocada.equipo == 1);
            if (recursoTocado == null && !esSuelo && !esEnemigo)
                Debug.LogWarning($"DEPURACION clic derecho SIN EFECTO: tocó '{impacto.collider.name}' " +
                                 $"(etiqueta '{impacto.collider.tag}', capa '{LayerMask.LayerToName(impacto.collider.gameObject.layer)}').");
            // Punto de SUELO bajo el cursor, aunque el rayo haya tocado antes una
            // tropa propia u otro objeto: así la orden de mover nunca se pierde.
            bool haySuelo = Physics.Raycast(rayo, out RaycastHit golpeSuelo, Mathf.Infinity, LayerMask.GetMask("Suelo"));
            if (haySuelo) destinoCentral = golpeSuelo.point;

            float radioDeLlegada = 0.5f + (Mathf.Sqrt(tropasSeleccionadas.Count) * 0.4f); 

            // --- MARCHA EN GRUPO ---
            // Todas las unidades seleccionadas van al ritmo de la MÁS LENTA, para que
            // el grupo llegue junto y no se estire por el camino. Cada una recupera su
            // velocidad propia al terminar el desplazamiento (lo hace Tropa).
            float velocidadGrupo = Mathf.Infinity;
            foreach (Tropa s in tropasSeleccionadas)
            {
                if (s == null) continue;
                float v = s.VelocidadOriginal();
                if (v > 0f && v < velocidadGrupo) velocidadGrupo = v;
            }

            if (velocidadGrupo < Mathf.Infinity)
            {
                foreach (Tropa s in tropasSeleccionadas)
                {
                    if (s != null) s.FijarVelocidadGrupo(velocidadGrupo);
                }
            }

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
                else if (haySuelo)
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
            UIManager.Instancia.CerrarTodosLosPaneles();
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

        // 2. Solo las tropas del ÁREA VISIBLE: con varias partidas entrenando a la
        //    vez, una búsqueda global seleccionaría tropas de otras partidas.
        AreaPartida area = AreaPartida.AreaVisible();
        if (area == null) return;

        foreach (Tropa soldado in area.Tropas())
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