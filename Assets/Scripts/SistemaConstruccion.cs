using UnityEngine;
using System.Collections.Generic; // Necesario para usar List

public class SistemaConstruccion : MonoBehaviour
{
    [Header("Referencias Principales")]
    public Transform castilloCentro;
    public float radioConstruccion = 15f; 
    public LayerMask capaSuelo; 

    [Header("Materiales del Holograma")]
    public Material matValido; 
    public Material matInvalido; 

    [Header("Sistema de Colisiones")]
    public LayerMask capaObstaculos;
    public float radioColision = 1.2f;

    private GameObject hologramaActual;
    private GameObject prefabRealActual;
    private List<MeshRenderer> renderersHolograma = new List<MeshRenderer>(); // Cambiado a List

    public void EmpezarConstruccion(GameObject prefabHolograma, GameObject prefabReal)
    {
        // --- NUEVO: Autodetectar el castillo del jugador en el mapa generado ---
        if (castilloCentro == null)
        {
            BaseMilitar[] todasLasBases = FindObjectsOfType<BaseMilitar>();
            foreach (BaseMilitar b in todasLasBases)
            {
                // Buscamos la base del Equipo 0 (Tu castillo azul)
                if (b.equipo == 0) 
                { 
                    castilloCentro = b.transform; 
                    break; 
                }
            }
        }

        if (hologramaActual != null) Destroy(hologramaActual);

        hologramaActual = Instantiate(prefabHolograma);
        prefabRealActual = prefabReal;

        renderersHolograma.Clear();
        renderersHolograma.AddRange(hologramaActual.GetComponentsInChildren<MeshRenderer>());
        
        MeshRenderer rendererPadre = hologramaActual.GetComponent<MeshRenderer>();
        if (rendererPadre != null && !renderersHolograma.Contains(rendererPadre))
        {
            renderersHolograma.Add(rendererPadre);
        }
    }

    void Update()
    {
        if (hologramaActual == null) return; 

        Ray rayo = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(rayo, out RaycastHit hit, 100f, capaSuelo))
        {
            hologramaActual.transform.position = hit.point;

            // 1. ¿Está dentro de nuestro territorio?
            float distancia = Vector3.Distance(hit.point, castilloCentro.position);
            bool dentroDelTerritorio = distancia <= radioConstruccion;

            // 2. ¿Está el terreno despejado? (Lanzamos una esfera invisible un poco por encima del suelo)
            Vector3 centroEsfera = hit.point + Vector3.up; 
            bool areaDespejada = !Physics.CheckSphere(centroEsfera, radioColision, capaObstaculos);

            // 3. Solo es válido si cumple AMBAS cosas
            bool esValido = dentroDelTerritorio && areaDespejada;

            CambiarColorHolograma(esValido ? matValido : matInvalido);

            if (Input.GetMouseButtonDown(0) && esValido)
            {
                Instantiate(prefabRealActual, hit.point, Quaternion.identity);
                Destroy(hologramaActual);
                hologramaActual = null; 
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            Destroy(hologramaActual);
            hologramaActual = null;
        }
    }

    private void CambiarColorHolograma(Material nuevoMaterial)
    {
        if (renderersHolograma == null) return;
        foreach (MeshRenderer mr in renderersHolograma)
        {
            mr.material = nuevoMaterial;
        }
    }
}