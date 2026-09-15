using UnityEngine;
using System.Collections.Generic;

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
    private BaseMilitar castilloJugador; // El "banco" del que se cobra
    private List<MeshRenderer> renderersHolograma = new List<MeshRenderer>();

    // Busca (y cachea) el castillo del jugador en el mapa generado
    private BaseMilitar BuscarCastilloJugador()
    {
        if (castilloJugador != null) return castilloJugador;

        // El castillo del jugador dentro del área que estamos viendo
        AreaPartida area = AreaPartida.AreaVisible();
        if (area == null) return null;

        castilloJugador = area.BaseDeEquipo(0);
        if (castilloJugador != null && castilloCentro == null)
            castilloCentro = castilloJugador.transform;

        return castilloJugador;
    }

    // Devuelve true si se ha podido empezar la construcción.
    // Comprueba NIVEL de castillo y RECURSOS antes de sacar el holograma.
    public bool EmpezarConstruccion(GameObject prefabHolograma, GameObject prefabReal)
    {
        BaseMilitar castillo = BuscarCastilloJugador();
        if (castillo == null)
        {
            Debug.LogWarning("SistemaConstruccion: no encuentro el castillo del jugador.");
            return false;
        }

        // El nivel requerido y el precio están en el prefab del edificio
        EdificioReclutamiento datos = EdificioReclutamiento.DatosDePrefab(prefabReal);
        if (datos == null)
        {
            Debug.LogWarning("SistemaConstruccion: el prefab no tiene EdificioReclutamiento.");
            return false;
        }

        string motivo;
        if (!datos.PuedeConstruirlo(castillo, out motivo))
        {
            Debug.Log($"No puedes construir '{datos.nombreEdificio}': {motivo}");
            return false;
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

        return true;
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

            // 2. ¿Está el terreno despejado?
            Vector3 centroEsfera = hit.point + Vector3.up;
            bool areaDespejada = !Physics.CheckSphere(centroEsfera, radioColision, capaObstaculos);

            bool esValido = dentroDelTerritorio && areaDespejada;

            CambiarColorHolograma(esValido ? matValido : matInvalido);

            if (Input.GetMouseButtonDown(0) && esValido)
            {
                ColocarEdificio(hit.point);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            Destroy(hologramaActual);
            hologramaActual = null;
        }
    }

    // Instancia el edificio y COBRA su precio al castillo
    private void ColocarEdificio(Vector3 posicion)
    {
        BaseMilitar castillo = BuscarCastilloJugador();
        EdificioReclutamiento datos = EdificioReclutamiento.DatosDePrefab(prefabRealActual);

        // Revalidamos por si hemos gastado los recursos mientras colocábamos
        string motivo;
        if (datos != null && !datos.PuedeConstruirlo(castillo, out motivo))
        {
            Debug.Log($"Ya no puedes construir '{datos.nombreEdificio}': {motivo}");
            Destroy(hologramaActual);
            hologramaActual = null;
            return;
        }

        GameObject nuevoEdificio = Instantiate(prefabRealActual, posicion, Quaternion.identity);

        // Lo metemos en el contenedor dinámico para que se limpie al reiniciar episodio
        AreaPartida area = AreaPartida.AreaVisible();
        if (area != null) area.Adoptar(nuevoEdificio);

        // Cobramos el precio
        EdificioReclutamiento edificio = nuevoEdificio.GetComponent<EdificioReclutamiento>();
        if (edificio == null) edificio = nuevoEdificio.GetComponentInChildren<EdificioReclutamiento>(true);

        if (edificio != null)
        {
            edificio.equipo = 0; // Es del jugador

            int antesM = castillo.madera, antesP = castillo.piedra, antesO = castillo.oro;
            edificio.CobrarConstruccion(castillo);

            Debug.Log($"Construido '{edificio.nombreEdificio}' | Precio M:{edificio.costeConstruccionMadera} P:{edificio.costeConstruccionPiedra} O:{edificio.costeConstruccionOro}"
                    + $" | Madera {antesM}->{castillo.madera}, Piedra {antesP}->{castillo.piedra}, Oro {antesO}->{castillo.oro}");
        }
        else
        {
            Debug.LogWarning("¡Edificio construido SIN cobrar! No tiene componente EdificioReclutamiento.");
        }

        Destroy(hologramaActual);
        hologramaActual = null;
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