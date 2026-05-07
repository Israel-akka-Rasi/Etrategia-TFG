using UnityEngine;
using Unity.AI.Navigation;

public class GeneradorTablero : MonoBehaviour
{
    [Header("Piezas Base (Suelos)")]
    public GameObject prefabAgua;   // El hex_water
    public GameObject prefabHierba; // El hex_grass

    [Header("Decoraciones y Edificios")]
    public GameObject prefabMontaña;    // Tu obstáculo
    public GameObject prefabMina;       // El generador de oro
    public GameObject prefabBaseAzul;   // Castillo azul
    public GameObject prefabBaseRoja;   // Castillo rojo
    public GameObject prefabArbol;            //Arboles para talar

    [Header("Ajustes Hexagonales")]
    public float distanciaX = 1.73f;
    public float distanciaZ = 1.5f;

    // EL MAPA: Matriz de 14x14
    private int[,] mapa = new int[,] 
    {
        {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 1, 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0}, // <-- Castillo Azul (4)
        {0, 1, 1, 1, 6, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 1, 1, 1, 1, 1, 3, 1, 1, 1, 1, 1, 1, 0}, // <-- Montaña Central 1
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0}, // <-- Montaña Central 2
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 0},
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 5, 1, 1, 0}, // <-- Castillo Rojo (5)
        {0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0},
        {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
    };

    void Start()
    {
        GenerarNivel();
        
        // ¡LA MAGIA! Una vez creado el mapa, horneamos el camino para la IA
        GetComponent<NavMeshSurface>().BuildNavMesh();
    }

    void GenerarNivel()
    {
        int alto = mapa.GetLength(0); // Filas
        int ancho = mapa.GetLength(1); // Columnas

        for (int z = 0; z < alto; z++)
        {
            for (int x = 0; x < ancho; x++)
            {
                // Matemáticas hexagonales
                float desplazamientoX = (z % 2 == 0) ? 0f : (distanciaX / 2f);
                Vector3 posicion = new Vector3((x * distanciaX) + desplazamientoX, 0f, z * distanciaZ);

                int tipoCasilla = mapa[z, x];

                // 1. Poner el Suelo Base (Agua o Hierba)
                GameObject sueloBase = (tipoCasilla == 0) ? prefabAgua : prefabHierba;
                Instantiate(sueloBase, posicion, sueloBase.transform.rotation, this.transform);

                // 2. Poner Edificios u Obstáculos encima (si no es 0 ni 1)
                GameObject adorno = null;
                
                switch (tipoCasilla)
                {
                    case 2: adorno = prefabMontaña; break;
                    case 3: adorno = prefabMina; break;
                    case 4: adorno = prefabBaseAzul; break;
                    case 5: adorno = prefabBaseRoja; break;
                    case 6: adorno = prefabArbol; break;
                }

                if (adorno != null)
                {
                    // 2. Poner Edificios u Obstáculos encima (Cambiamos Quaternion.identity)
                    Instantiate(adorno, posicion, adorno.transform.rotation, this.transform);
                }
            }
        }
    }
}