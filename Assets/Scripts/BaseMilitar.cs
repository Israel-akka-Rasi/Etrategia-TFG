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
    public void IntentarMejorarCastillo()
    {
        if (nivelCastillo == 1 && madera >= costeMejorarNivel2)
        {
            madera -= costeMejorarNivel2;
            nivelCastillo = 2;
            poblacionMaxima = 15; 
            Debug.Log("¡Avanzaste a la Edad de Piedra!");
            ActualizarTextosUI();
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
}