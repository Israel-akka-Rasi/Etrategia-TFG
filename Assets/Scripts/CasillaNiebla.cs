using UnityEngine;

public class CasillaNiebla : MonoBehaviour
{
    private Renderer[] misRenderers;
    private bool yaDescubierta = false;

    void Start()
    {
        // Recoge su propio modelo 3D y los de cualquier árbol/montaña que tenga encima
        misRenderers = GetComponentsInChildren<Renderer>(true);
        Ocultar();
    }

    public void Ocultar()
    {
        if (yaDescubierta) return; 
        
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