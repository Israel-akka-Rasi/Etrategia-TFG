using UnityEngine;

public class CasillaNiebla : MonoBehaviour
{
    [Header("Modo Debug")]
    public bool modoPruebaSinNiebla = true; // Activa esta casilla en el Inspector para ver todo

    private Renderer[] misRenderers;
    private bool yaDescubierta = false;

    void Start()
    {
        // Recoge su propio modelo 3D y los de cualquier árbol/montaña que tenga encima
        misRenderers = GetComponentsInChildren<Renderer>(true);
        
        if (modoPruebaSinNiebla)
        {
            // Si hacemos trampas, engañamos al script diciendo que ya ha sido descubierta
            yaDescubierta = true;
        }
        else
        {
            // Si el juego es normal, se oculta al nacer
            Ocultar();
        }
    }

    public void Ocultar()
    {
        // Si ya está descubierta o estamos en modo prueba, cancelamos la ocultación
        if (yaDescubierta || modoPruebaSinNiebla) return; 
        
        foreach (Renderer r in misRenderers)
        {
            if (r != null) r.enabled = false; // Apagamos el modelo 3D
        }
    }

    public void Descubrir()
    {
        if (yaDescubierta) return;
        yaDescubierta = true; // Si quieres que la niebla vuelva a aparecer al irte, quita esta línea
        
        foreach (Renderer r in misRenderers)
        {
            if (r != null) r.enabled = true; // Encendemos el modelo 3D
        }
    }
}