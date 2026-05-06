using UnityEngine;
using UnityEngine.UI; // Para la barra de vida
using TMPro; // Para el texto del oro

public class BaseMilitar : MonoBehaviour
{
    [Header("Configuración")]
    public int equipo;
    public int oro = 0;
    public int costeTropa = 5;
    public GameObject prefabTropa;
    public Transform puntoSpawn; // Dónde aparecen las tropas

    [Header("Estadísticas y UI")]
    public float salud = 100f;
    private float saludMaxima;
    public Image barraSalud;     // Aquí conectaremos la barra verde
    public TextMeshProUGUI textoOro; // Aquí conectaremos el texto flotante del oro

    void Start()
    {
        saludMaxima = salud;
        // Generamos oro cada segundo
        InvokeRepeating("GenerarOro", 1f, 1f);
    }

    void GenerarOro()
    {
        oro += 1;
        ActualizarTextoOro();
    }

    void ActualizarTextoOro()
    {
        if (textoOro != null)
        {
            textoOro.text = "Oro: " + oro;
        }
    }

    public void IntentarComprarTropa()
    {
        if (oro >= costeTropa)
        {
            oro -= costeTropa;
            ActualizarTextoOro();
            InstanciarTropa();
        }
        else
        {
            Debug.Log("No hay suficiente oro en la base " + gameObject.name);
        }
    }

    void InstanciarTropa()
    {
        // Tu lógica de NavMesh.SamplePosition que ya tenías
        Vector3 posicionAparicion = puntoSpawn != null ? puntoSpawn.position : transform.position;
        
        // Desvío aleatorio para que no se apilen
        posicionAparicion += (Vector3)(Random.insideUnitCircle * 2f);
        posicionAparicion.y = transform.position.y;

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(posicionAparicion, out hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Instantiate(prefabTropa, hit.position, Quaternion.identity);
        }
    }

    // El castillo se prepara para ser atacado
    public void RecibirDano(int cantidad)
    {
        salud -= cantidad;
        
        if (barraSalud != null) 
        {
            barraSalud.fillAmount = salud / saludMaxima;
        }

        if (salud <= 0)
        {
            Debug.Log("🏰 ¡El castillo " + gameObject.name + " ha sido destruido!");
            
            // --- ¡NUEVO! AVISAMOS AL GAME MANAGER ---
            GameManager gestor = FindObjectOfType<GameManager>();
            if (gestor != null)
            {
                // Le mandamos nuestro número de equipo para que sepa quién ha perdido
                gestor.TerminarPartida(equipo); 
            }
            // ----------------------------------------

            Destroy(gameObject);
        }
    }
    // Función pública para que la Mina u otros eventos puedan dar oro al castillo
    public void RecibirOroAdicional(int cantidad)
    {
        oro += cantidad;
        ActualizarTextoOro();
    }
}