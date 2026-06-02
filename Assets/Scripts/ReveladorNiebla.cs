using UnityEngine;

public class ReveladorNiebla : MonoBehaviour
{
    [Header("Ajustes de Visión")]
    public float radioVision = 10f; 
    public LayerMask capaSuelo; // <-- Ahora buscaremos la capa "Suelo"

    void Start()
    {
        InvokeRepeating("DespejarNiebla", 0.5f, 0.2f);
    }

    void DespejarNiebla()
    {
        Collider[] casillasCercanas = Physics.OverlapSphere(transform.position, radioVision, capaSuelo);
        
        foreach (Collider c in casillasCercanas)
        {
            CasillaNiebla casilla = c.GetComponentInParent<CasillaNiebla>();
            if (casilla != null)
            {
                casilla.Descubrir();
            }
        }
    }
}