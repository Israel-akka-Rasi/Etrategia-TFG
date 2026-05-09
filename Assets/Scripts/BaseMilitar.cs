using UnityEngine;
using UnityEngine.UI;

public class BaseMilitar : MonoBehaviour
{
    [Header("Identidad y Progreso")]
    public int equipo; // 0 = Azul (Jugador), 1 = Rojo (Enemigo)
    public int nivelCastillo = 1; // 15 es el máximo

    [Header("Bóveda de Recursos")]
    public int madera = 0;
    public int piedra = 0;
    public int oro = 0;

    [Header("Gestión de Población")]
    public int poblacionActual = 1; 
    public int poblacionMaxima = 5; 

    [Header("Combate y Spawn")]
    public float salud = 1000f;
    private float saludMaxima;
    public Image barraSalud;
    public Transform puntoSpawn;

    [Header("Evolución")]
    public int costeBaseMejora = 50; // Madera base para subir niveles

    [Header("Reclutamiento de Tropas")]
    public GameObject prefabCaballero;
    public int costeCaballero = 10; // Madera
    
    public GameObject prefabMinero;
    public int costeMinero = 20; // Madera

    public GameObject prefabTesorero;
    public int maderaTesorero = 20;
    public int piedraTesorero = 15; 

    void Start()
    {
        saludMaxima = salud;
        
        // --- TROPA INICIAL GRATIS ---
        // Instanciamos el primer caballero directamente en el punto de spawn
        if (prefabCaballero != null && puntoSpawn != null)
        {
            Instantiate(prefabCaballero, puntoSpawn.position + (puntoSpawn.forward * 1.5f), Quaternion.identity);
        }
        
        // Actualizamos la UI al arrancar
        ActualizarTextosUI();
    }

    // --- RECOLECCIÓN DEL MAPA ---
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
        if (equipo == 0 && UIManager.Instancia != null)
        {
            UIManager.Instancia.ActualizarEconomia(nivelCastillo, poblacionActual, poblacionMaxima, madera, piedra, oro);
        }
    }

    // --- LÓGICA DE MEJORA ---
    public void MejorarCastillo()
    {
        if (nivelCastillo >= 15) return;

        int maderaNecesaria = 0;
        int piedraNecesaria = 0;
        int oroNecesario = 0;

        if (nivelCastillo < 5) 
        {
            maderaNecesaria = costeBaseMejora * nivelCastillo;
        }
        else if (nivelCastillo < 10) 
        {
            maderaNecesaria = costeBaseMejora * nivelCastillo;
            piedraNecesaria = 20 * (nivelCastillo - 4); 
        }
        else 
        {
            piedraNecesaria = 50;
            oroNecesario = 10 * (nivelCastillo - 10);
        }

        if (madera >= maderaNecesaria && piedra >= piedraNecesaria && oro >= oroNecesario)
        {
            madera -= maderaNecesaria;
            piedra -= piedraNecesaria;
            oro -= oroNecesario;
            
            nivelCastillo++;
            poblacionMaxima += 2; 

            Debug.Log($"Mejorado a Nivel {nivelCastillo}. Costó: M:{maderaNecesaria} P:{piedraNecesaria} O:{oroNecesario}");

            if (equipo == 0) ActualizarInterfazAlMejorar();
            ActualizarTextosUI();
        }
        else
        {
            Debug.Log("Faltan recursos para mejorar el castillo.");
        }
    }

    void ActualizarInterfazAlMejorar()
    {
        if (UIManager.Instancia != null)
        {
            UIManager.Instancia.MostrarPanelReclutamiento(true, nivelCastillo);
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

    // --- RECLUTAMIENTO ---
    public void ComprarCaballero()
    {
        if (poblacionActual >= poblacionMaxima) return;

        if (madera >= costeCaballero)
        {
            madera -= costeCaballero;
            poblacionActual++;
            RealizarSpawnConDesplazamiento(prefabCaballero);
            ActualizarTextosUI();
        }
    }

    public void ComprarMinero()
    {
        if (nivelCastillo < 5) return;
        if (poblacionActual >= poblacionMaxima) return;

        if (madera >= costeMinero)
        {
            madera -= costeMinero;
            poblacionActual++;
            RealizarSpawnConDesplazamiento(prefabMinero);
            ActualizarTextosUI();
        }
    }

    public void ComprarTesorero()
    {
        if (nivelCastillo < 10) return;
        if (poblacionActual >= poblacionMaxima) return;

        if (madera >= maderaTesorero && piedra >= piedraTesorero)
        {
            madera -= maderaTesorero;
            piedra -= piedraTesorero;
            poblacionActual++;
            RealizarSpawnConDesplazamiento(prefabTesorero);
            ActualizarTextosUI();
        }
        else
        {
            Debug.Log("Faltan recursos para el Tesorero.");
        }
    }

    // Función unificada para no repetir código visual al comprar tropas
    private void RealizarSpawnConDesplazamiento(GameObject prefabUnidad)
    {
        Vector3 variacion = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
        Vector3 posicionFinal = puntoSpawn.position + variacion;

        GameObject nuevaTropa = Instantiate(prefabUnidad, posicionFinal, Quaternion.identity);

        Tropa scriptTropa = nuevaTropa.GetComponent<Tropa>();
        if (scriptTropa != null)
        {
            Vector3 puntoDespeje = puntoSpawn.position + (puntoSpawn.forward * 1.0f) + (variacion * 0.5f);
            scriptTropa.OrdenarMovimiento(puntoDespeje);
        }
    }
}