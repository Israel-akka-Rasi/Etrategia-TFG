using UnityEngine;
using UnityEngine.UI;

public class BaseMilitar : MonoBehaviour
{
    [Header("Identidad y Progreso")]
    public int equipo; // 0 = Azul (Jugador), 1 = Rojo (Enemigo)
    public int nivelCastillo = 1; // 1 = Madera, 2 = Piedra, 3 = Oro

    [Header("Bóveda de Recursos")]
    public int madera = 0;
    public int piedra = 0;
    public int oro = 0;

    [Header("Gestión de Población")]
    public int poblacionActual = 1; 
    public int poblacionMaxima = 5; 

    [Header("Costes (Nivel 1)")]
    public int costeTropaBasica = 10; 
    public int costeMejorarNivel2 = 50; 

    [Header("Combate y Spawn")]
    public float salud = 1000f;
    private float saludMaxima;
    public Image barraSalud;
    public GameObject prefabTropa;
    public Transform puntoSpawn;
    [Header("Evolución")]
    public int costeBaseMejora = 50; // Madera necesaria para pasar al nivel 2

    [Header("Reclutamiento de Tropas")]
    public GameObject prefabCaballero;
    public int costeCaballero = 10; // Madera
    
    public GameObject prefabMinero;
    public int costeMinero = 20; // Madera

    void Start()
    {
        saludMaxima = salud;
        
        // --- NUEVO: TROPA INICIAL GRATIS ---
        // Llamamos directamente a la fábrica una vez al empezar la partida
        GenerarTropa(); 
        
        // Actualizamos la UI al arrancar para que no salgan ceros
        ActualizarTextosUI();
    }

    // --- NUEVO: GENERACIÓN PASIVA ---
    /*void GenerarRecursoPasivo()
    {
        // En nivel 1 generamos madera, luego podríamos cambiarlo
        if (nivelCastillo == 1) madera += 1;
        else oro += 1;

        ActualizarTextosUI();
    }*/

    // --- NUEVO: RECOLECCIÓN DEL MAPA (Bosques/Minas) ---
    public void RecibirRecurso(string tipo, int cantidad)
    {
        if (tipo == "Madera") madera += cantidad;
        else if (tipo == "Piedra") piedra += cantidad;
        else if (tipo == "Oro") oro += cantidad;

        ActualizarTextosUI();
    }

    // --- CONEXIÓN CON LA INTERFAZ ---
    public void ActualizarTextosUI()
    {
        // Solo enviamos datos al Canvas si es NUESTRA base (Equipo 0)
        if (equipo == 0 && UIManager.Instancia != null)
        {
            UIManager.Instancia.ActualizarEconomia(nivelCastillo, poblacionActual, poblacionMaxima, madera, piedra, oro);
        }
    }

    // --- LÓGICA DE COMPRA ---
    public void IntentarComprarTropaBasica()
    {
        if (poblacionActual >= poblacionMaxima)
        {
            Debug.Log("¡Límite de población alcanzado! Necesitas mejorar el castillo.");
            return; 
        }

        if (madera >= costeTropaBasica)
        {
            madera -= costeTropaBasica;
            poblacionActual++;
            ActualizarTextosUI(); // Refrescamos la madera y la población en pantalla
            GenerarTropa(); 
        }
    }

    // --- LÓGICA DE SPAWN ---
    void GenerarTropa()
    {
        Vector3 posicionAparicion = puntoSpawn != null ? puntoSpawn.position : transform.position;
        posicionAparicion += (Vector3)(Random.insideUnitCircle * 2f);
        posicionAparicion.y = transform.position.y;

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(posicionAparicion, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Instantiate(prefabTropa, hit.position, Quaternion.identity);
        }
    }

    // --- LÓGICA DE MEJORA ---
    public void MejorarCastillo()
    {
        int costeActual = costeBaseMejora * nivelCastillo;

        if (madera >= costeActual)
        {
            madera -= costeActual;
            nivelCastillo++;
            poblacionMaxima += 2;

            Debug.Log("¡Castillo mejorado! Ahora es Nivel " + nivelCastillo);

            // --- NUEVO: Refrescar la UI de reclutamiento al subir de nivel ---
            // Si somos el jugador (equipo 0), le pedimos al UIManager que actualice los botones
            if (equipo == 0 && UIManager.Instancia != null)
            {
                // Pasamos 'true' para que se mantenga abierto y el nuevo nivel para que chequee los botones
                UIManager.Instancia.MostrarPanelReclutamiento(true, nivelCastillo);
            }

            if (nivelCastillo >= 5)
            {
                Debug.Log("¡Hito alcanzado! Ya puedes recolectar Piedra con unidades especializadas.");
            }

            ActualizarTextosUI();
        }
        else
        {
            Debug.Log("Madera insuficiente. Necesitas " + costeActual + " y tienes " + madera);
        }
    }

    // --- LÓGICA DE COMBATE ---
    public void RecibirDano(int cantidad)
    {
        salud -= cantidad;
        
        if (barraSalud != null) 
        {
            barraSalud.fillAmount = salud / saludMaxima;
        }

        if (salud <= 0)
        {
            GameManager gestor = FindObjectOfType<GameManager>();
            if (gestor != null)
            {
                gestor.TerminarPartida(equipo); 
            }
            Destroy(gameObject);
        }
    }

    public void ComprarCaballero()
    {
        if (poblacionActual >= poblacionMaxima) return;

        if (madera >= costeCaballero)
        {
            madera -= costeCaballero;
            poblacionActual++;

            // 1. Variación aleatoria un poco más dispersa
            Vector3 variacion = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
            Vector3 posicionFinal = puntoSpawn.position + variacion;

            GameObject nuevaTropa = Instantiate(prefabCaballero, posicionFinal, Quaternion.identity);

            Tropa scriptTropa = nuevaTropa.GetComponent<Tropa>();
            if (scriptTropa != null)
            {
                // 2. Bajamos la distancia de 2.0 a 1.0 metros
                float distanciaDespeje = 1.0f; 
                
                // El punto de destino será un poco hacia adelante (flecha azul) + la variación
                Vector3 puntoDespeje = puntoSpawn.position + (puntoSpawn.forward * distanciaDespeje) + (variacion * 0.5f);
                
                scriptTropa.OrdenarMovimiento(puntoDespeje);
            }

            ActualizarTextosUI();
        }
    }

    // Esta función irá al botón del Minero
    public void ComprarMinero()
    {
        if (nivelCastillo < 5) return;
        if (poblacionActual >= poblacionMaxima) return;

        if (madera >= costeMinero)
        {
            madera -= costeMinero;
            poblacionActual++;

            // 1. Añadimos el mismo margen aleatorio que al caballero
            Vector3 variacion = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
            Vector3 posicionFinal = puntoSpawn.position + variacion;

            GameObject nuevoMinero = Instantiate(prefabMinero, posicionFinal, Quaternion.identity);

            // 2. Le damos la orden de caminar un poco hacia adelante para despejar la puerta
            Tropa scriptTropa = nuevoMinero.GetComponent<Tropa>();
            if (scriptTropa != null)
            {
                // Usamos la misma lógica: 1 metro hacia adelante (puntoSpawn.forward)
                Vector3 puntoDespeje = puntoSpawn.position + (puntoSpawn.forward * 1.0f) + (variacion * 0.5f);
                scriptTropa.OrdenarMovimiento(puntoDespeje);
            }

            ActualizarTextosUI();
        }
    }
}