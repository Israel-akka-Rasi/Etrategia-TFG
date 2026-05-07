using UnityEngine;

public class Recurso : MonoBehaviour
{
    public enum TipoRecurso { Madera, Piedra, Oro }
    
    [Header("Configuración")]
    public TipoRecurso tipo;
    public int reservaHabilitada = 100; // Cuanto tiene el objeto antes de agotarse
    public int cantidadPorTanda = 2;    // Cuanto extrae el trabajador en cada golpe

    public void SerExtraido(int cantidad)
    {
        reservaHabilitada -= cantidad;
        if (reservaHabilitada <= 0)
        {
            Debug.Log("¡Recurso agotado!");
            Destroy(gameObject); 
        }
    }
}