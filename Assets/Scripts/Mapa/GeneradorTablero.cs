using UnityEngine;
using Unity.AI.Navigation;
using System.Collections.Generic;

public class GeneradorTablero : MonoBehaviour
{
    [Header("Dimensiones del Mapa")]
    public int anchoMapa = 50;
    public int altoMapa = 50;

    [Header("Bases y Zonas Seguras")]
    public int cantidadBases = 3; 
    public float distanciaMinimaBases = 15f; 
    public int radioDespejado = 3; 

    [Header("Generación Procedimental")]
    public float escalaRuido = 0.15f; 
    [Space(5)]
    public float escalaRuidoAgua = 0.08f; 
    [Range(0f, 1f)] public float umbralAgua = 0.25f; 
    [Space(5)]
    [Range(0f, 1f)] public float densidadArboles = 0.55f;
    [Range(0f, 1f)] public float densidadMontanas = 0.75f;
    [Range(0f, 1f)] public float probabilidadOro = 0.02f;
    [Range(0f, 1f)] public float probabilidadDecoracion = 0.05f; 

    [Header("Piezas Base (Suelos)")]
    public GameObject prefabAgua;
    public GameObject prefabHierba;

    [Header("Edificios y Recursos Únicos")]
    public GameObject prefabMina;
    public GameObject prefabBaseAzul;
    public GameObject prefabBaseRoja;

    [Header("Variedad de Naturaleza (Arrays)")]
    public GameObject[] prefabsMontañas; 
    public GameObject[] prefabsArboles; 
    public GameObject[] prefabsDecoraciones; 

    [Header("Ajustes Hexagonales")]
    public float distanciaX = 1.73f;
    public float distanciaZ = 1.5f;

    private List<Vector2Int> posicionesBases = new List<Vector2Int>();

    void Start()
    {
        float semillaX = Random.Range(0f, 10000f);
        float semillaZ = Random.Range(0f, 10000f);
        
        float semillaAguaX = Random.Range(0f, 10000f);
        float semillaAguaZ = Random.Range(0f, 10000f);

        CalcularPosicionesBases();
        GenerarNivelProcedimental(semillaX, semillaZ, semillaAguaX, semillaAguaZ);
        
        GetComponent<NavMeshSurface>().BuildNavMesh();
    }

    void CalcularPosicionesBases()
    {
        posicionesBases.Clear();
        int intentos = 0; 
        
        while (posicionesBases.Count < cantidadBases && intentos < 1000)
        {
            int randX = Random.Range(6, anchoMapa - 6);
            int randZ = Random.Range(6, altoMapa - 6);
            Vector2Int nuevaPos = new Vector2Int(randX, randZ);

            bool muyCerca = false;
            foreach(Vector2Int posGuardada in posicionesBases)
            {
                if (Vector2.Distance(nuevaPos, posGuardada) < distanciaMinimaBases)
                {
                    muyCerca = true;
                    break;
                }
            }

            if (!muyCerca) posicionesBases.Add(nuevaPos);
            intentos++;
        }
    }

    bool EsZonaSegura(int x, int z, out bool esCentro, out int idBase)
    {
        esCentro = false;
        idBase = -1;

        for (int i = 0; i < posicionesBases.Count; i++)
        {
            float distancia = Vector2.Distance(new Vector2(x, z), new Vector2(posicionesBases[i].x, posicionesBases[i].y));
            if (distancia == 0) 
            {
                esCentro = true;
                idBase = i;
                return true;
            }
            if (distancia <= radioDespejado) return true; 
        }
        return false;
    }

    void GenerarNivelProcedimental(float seedX, float seedZ, float seedAguaX, float seedAguaZ)
    {
        int capaSuelo = LayerMask.NameToLayer("Suelo");

        for (int z = 0; z < altoMapa; z++)
        {
            for (int x = 0; x < anchoMapa; x++)
            {
                float desplazamientoX = (z % 2 == 0) ? 0f : (distanciaX / 2f);
                Vector3 posicion = new Vector3((x * distanciaX) + desplazamientoX, 0f, z * distanciaZ);

                int tipoSuelo = 1; 
                int adornoElegido = 0; 

                bool esCentroBase;
                int idBase;
                bool zonaProtegida = EsZonaSegura(x, z, out esCentroBase, out idBase);

                if (x == 0 || x == anchoMapa - 1 || z == 0 || z == altoMapa - 1)
                {
                    tipoSuelo = 0; 
                }
                else if (esCentroBase)
                {
                    adornoElegido = (idBase == 0) ? 4 : 5; 
                }
                else if (zonaProtegida)
                {
                    if (Random.value < probabilidadDecoracion) adornoElegido = 7; 
                }
                else
                {
                    float ruidoAgua = Mathf.PerlinNoise((x + seedAguaX) * escalaRuidoAgua, (z + seedAguaZ) * escalaRuidoAgua);
                    if (ruidoAgua < umbralAgua)
                    {
                        tipoSuelo = 0; 
                    }
                    else
                    {
                        float ruidoNaturaleza = Mathf.PerlinNoise((x + seedX) * escalaRuido, (z + seedZ) * escalaRuido);
                        
                        if (ruidoNaturaleza > densidadMontanas) adornoElegido = 2;
                        else if (ruidoNaturaleza > densidadArboles) adornoElegido = 6;
                        else 
                        {
                            if (Random.value < probabilidadOro) adornoElegido = 3;
                            else if (Random.value < probabilidadDecoracion) adornoElegido = 7;
                        }
                    }
                }

                // 1. Instanciar suelo
                GameObject prefabCasilla = (tipoSuelo == 0) ? prefabAgua : prefabHierba;
                GameObject baldosa = Instantiate(prefabCasilla, posicion, prefabCasilla.transform.rotation, this.transform);

                baldosa.layer = capaSuelo;
                foreach (Transform hijo in baldosa.GetComponentsInChildren<Transform>(true))
                {
                    hijo.gameObject.layer = capaSuelo;
                }

                // 2. Elegir el adorno  
                GameObject prefabAdornoFinal = null;
                switch (adornoElegido)
                {
                    case 2: 
                        if (prefabsMontañas.Length > 0) 
                            prefabAdornoFinal = prefabsMontañas[Random.Range(0, prefabsMontañas.Length)];
                        break;
                    case 3: prefabAdornoFinal = prefabMina; break;
                    case 4: prefabAdornoFinal = prefabBaseAzul; break;
                    case 5: prefabAdornoFinal = prefabBaseRoja; break;
                    case 6: 
                        if (prefabsArboles.Length > 0) 
                            prefabAdornoFinal = prefabsArboles[Random.Range(0, prefabsArboles.Length)];
                        break;
                    case 7:
                        if (prefabsDecoraciones.Length > 0) 
                            prefabAdornoFinal = prefabsDecoraciones[Random.Range(0, prefabsDecoraciones.Length)];
                        break;
                }

                // 3. Instanciar adorno COMO HIJO de la baldosa y llamar a la cámara
                if (prefabAdornoFinal != null)
                {
                    // AQUÍ ESTÁ EL CAMBIO CLAVE: baldosa.transform
                    Instantiate(prefabAdornoFinal, posicion, prefabAdornoFinal.transform.rotation, baldosa.transform);

                    if (adornoElegido == 4) 
                    {
                        CamaraControlador camara = Camera.main.GetComponent<CamaraControlador>();
                        if (camara != null)
                        {
                            camara.CentrarEn(posicion);
                        }
                    }
                }
            }
        }
    }
}