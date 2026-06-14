using UnityEngine;

public class Recurso : MonoBehaviour
{
    public enum TipoRecurso { Madera, Piedra, Oro }

    [Header("Configuración")]
    public TipoRecurso tipo;
    public int reservaHabilitada = 100; // Cuánto tiene el nodo antes de agotarse
    public int cantidadPorTanda = 2;    // Cuánto extrae el trabajador en cada golpe

    // Estado inicial y referencias para poder ocultar/restaurar el nodo
    private int reservaInicial;
    private Renderer[] renderers;
    private Collider[] colliders;

    void Awake()
    {
        reservaInicial = reservaHabilitada;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    // ¿Le queda material? Los recolectores lo usan para no ir a nodos vacíos.
    public bool EstaDisponible()
    {
        return reservaHabilitada > 0;
    }

    public void SerExtraido(int cantidad)
    {
        reservaHabilitada -= cantidad;
        if (reservaHabilitada <= 0)
        {
            reservaHabilitada = 0;
            Agotar();
        }
    }

    // En vez de destruir el nodo (para poder restaurarlo al reiniciar), lo "apagamos":
    // ocultamos el modelo y desactivamos sus colliders. El GameObject sigue existiendo.
    private void Agotar()
    {
        MostrarNodo(false);
    }

    // Restaura el nodo a su estado inicial (lleno y visible). Lo llama el GestorPartida al reiniciar.
    public void ReiniciarRecurso()
    {
        reservaHabilitada = reservaInicial;
        MostrarNodo(true);
    }

    private void MostrarNodo(bool visible)
    {
        if (renderers != null)
            foreach (Renderer r in renderers) if (r != null) r.enabled = visible;

        if (colliders != null)
            foreach (Collider c in colliders) if (c != null) c.enabled = visible;
    }
}