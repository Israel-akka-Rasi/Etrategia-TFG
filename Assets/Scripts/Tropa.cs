using UnityEngine;
using UnityEngine.AI; 
using UnityEngine.UI; // ¡NUEVO! Importamos la librería de Interfaz de Usuario

public class Tropa : MonoBehaviour
{
    [Header("Identidad")]
    public int equipo; 
    public int salud = 3;
    private float saludMaxima; // Necesario para calcular el porcentaje de la barra (ej: 2 de 3)

    [Header("Visuales")]
    public GameObject indicadorSeleccion;
    public Image barraSalud; // ¡NUEVO! Aquí arrastraremos la imagen de "Relleno" verde

    private NavMeshAgent agente;

    void Awake()
    {
        // Enchufamos el cerebro al empezar
        agente = GetComponent<NavMeshAgent>();
        
        // Guardamos la salud inicial como el máximo (100%)
        saludMaxima = salud; 
    }

    // Esta función la sigue llamando el ratón
    public void OrdenarMovimiento(Vector3 destinoFinal)
    {
        // El NavMeshAgent calcula la ruta esquivando obstáculos, rota al muñeco y lo mueve por nosotros.
        agente.SetDestination(destinoFinal);
    }

    public void ActivarSeleccion(bool estado)
    {
        if (indicadorSeleccion != null)
        {
            indicadorSeleccion.SetActive(estado);
        }
    }
    
    // Ahora recibimos la cantidad de daño Y quién nos lo ha hecho
    public void RecibirDano(int cantidad, Tropa atacante)
    {
        salud -= cantidad;
        
        // Actualizamos la barra verde (Salud actual dividida entre Salud máxima)
        if (barraSalud != null)
        {
            barraSalud.fillAmount = salud / saludMaxima;
        }

        Debug.Log("🩸 " + gameObject.name + " recibe daño. Salud: " + salud);
        
        if (salud <= 0)
        {
            Debug.Log("💀 " + gameObject.name + " ha caído en combate.");
            Destroy(gameObject); // Borramos el modelo 3D del mapa
        }
        else if (atacante != null) // Si seguimos vivos y alguien nos ha pegado...
        {
            // ¡INSTINTO DE SUPERVIVENCIA!
            Combate miCombate = GetComponent<Combate>();
            
            // Si tenemos el script de combate y NO estábamos atacando a nadie más...
            if (miCombate != null && !miCombate.TieneObjetivo())
            {
                Debug.Log("😡 " + gameObject.name + " se defiende de " + atacante.gameObject.name);
                miCombate.OrdenarAtaque(atacante); // ¡Contraatacamos!
            }
        }
    }
}