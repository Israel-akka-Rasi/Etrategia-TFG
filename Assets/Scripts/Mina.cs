using UnityEngine;

public class Mina : MonoBehaviour
{
    [Header("Configuración Táctica")]
    public float radioCaptura = 4f;
    public int oroPorTropa = 1;
    public int limiteTropas = 5; // ¡El Saturation Cap que comentamos!

    [Header("Feedback Visual")]
    public MeshRenderer indicadorSuelo; // El círculo que cambia de color
    public Material matNeutral;
    public Material matAzul;
    public Material matRojo;
    public Material matDisputado;

    private BaseMilitar baseAzul;
    private BaseMilitar baseRojo;

    void Start()
    {
        // Buscamos automáticamente a los dos reyes del tablero
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == 0) baseAzul = b;
            else if (b.equipo == 1) baseRojo = b;
        }

        // La mina procesa el oro cada 1 segundo (igual que la economía base)
        InvokeRepeating("ProcesarMina", 1f, 1f);
    }

    void ProcesarMina()
    {
        int tropasAzules = 0;
        int tropasRojas = 0;

        // 1. Escaneamos qué soldados hay dentro del círculo
        Collider[] detectados = Physics.OverlapSphere(transform.position, radioCaptura);
        foreach (Collider col in detectados)
        {
            Tropa t = col.GetComponent<Tropa>();
            if (t != null)
            {
                if (t.equipo == 0) tropasAzules++;
                else if (t.equipo == 1) tropasRojas++;
            }
        }

        // 2. Aplicamos el límite máximo para que la economía no se rompa
        tropasAzules = Mathf.Min(tropasAzules, limiteTropas);
        tropasRojas = Mathf.Min(tropasRojas, limiteTropas);

        // 3. Lógica de Disputa y Recompensas
        if (tropasAzules > 0 && tropasRojas == 0)
        {
            // ¡DOMINIO AZUL!
            if (baseAzul != null) baseAzul.RecibirOroAdicional(tropasAzules * oroPorTropa);
            CambiarColor(matAzul);
        }
        else if (tropasRojas > 0 && tropasAzules == 0)
        {
            // ¡DOMINIO ROJO!
            if (baseRojo != null) baseRojo.RecibirOroAdicional(tropasRojas * oroPorTropa);
            CambiarColor(matRojo);
        }
        else if (tropasAzules > 0 && tropasRojas > 0)
        {
            // ¡ZONA EN DISPUTA!
            CambiarColor(matDisputado);
        }
        else
        {
            // ZONA ABANDONADA
            CambiarColor(matNeutral);
        }
    }

    void CambiarColor(Material nuevoMaterial)
    {
        if (indicadorSuelo != null && nuevoMaterial != null)
        {
            indicadorSuelo.material = nuevoMaterial;
        }
    }

    // Esto dibuja una esfera amarilla invisible en el editor para ayudarte a ajustar el tamaño
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioCaptura);
    }
}