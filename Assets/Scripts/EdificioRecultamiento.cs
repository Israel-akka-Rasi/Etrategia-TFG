using UnityEngine;

public class EdificioReclutamiento : MonoBehaviour
{
    [Header("Configuración del Edificio")]
    public string nombreEdificio = "Campamento Minero";
    public Transform puntoSpawn; // El objeto vacío delante de la puerta

    [Header("Propietario")]
    // 0 = jugador, 1 = IA. La IA lo pone a 1 al construirlo (con AsignarCastillo).
    public int equipo = 0;

    [Header("Coste de CONSTRUIR este edificio")]
    // Nivel de castillo necesario para desbloquearlo y precio de levantarlo.
    // Lo leen tanto el jugador (SistemaConstruccion) como la IA: una sola configuración.
    public int nivelRequerido = 1;
    public int costeConstruccionMadera = 40;
    public int costeConstruccionPiedra = 0;
    public int costeConstruccionOro = 0;

    [Header("Configuración de la Tropa")]
    public GameObject prefabTropa;
    public int costeMadera = 20;
    public int costePiedra = 0;
    public int costeOro = 0;

    [Header("Modo Prueba")]
    // true  = reclutar es GRATIS (no gasta recursos). Útil mientras pruebas.
    // false = se comprueban y se gastan los recursos del castillo.
    public bool modoPrueba = true;

    // Castillo dueño (el "Banco") al que está conectado este edificio.
    private BaseMilitar miCastillo;

    // La IA llama a esto al construir el edificio para fijar su dueño directamente.
    public void AsignarCastillo(BaseMilitar castillo)
    {
        miCastillo = castillo;
        if (miCastillo != null) equipo = miCastillo.equipo;
    }

    void Start()
    {
        // Si nadie nos asignó dueño (caso del jugador), buscamos la base más cercana
        // de nuestro equipo. Para el jugador solo hay una base azul, así que acierta seguro.
        if (miCastillo == null)
        {
            BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
            float distanciaMinima = Mathf.Infinity;

            foreach (BaseMilitar b in bases)
            {
                if (b.equipo != equipo) continue;

                float distancia = Vector3.Distance(transform.position, b.transform.position);
                if (distancia < distanciaMinima)
                {
                    distanciaMinima = distancia;
                    miCastillo = b;
                }
            }
        }
    }

    // ====================================================================
    // COSTE DE CONSTRUCCIÓN (compartido por el jugador y la IA)
    // Los datos viven en el prefab del edificio, así solo se configuran
    // en un sitio y ambos bandos juegan con las mismas reglas.
    // ====================================================================

    // Lee los datos de construcción del PREFAB (sin instanciarlo).
    public static EdificioReclutamiento DatosDePrefab(GameObject prefabEdificio)
    {
        if (prefabEdificio == null) return null;

        // Puede estar en la raíz o en un hijo del prefab
        EdificioReclutamiento datos = prefabEdificio.GetComponent<EdificioReclutamiento>();
        if (datos == null) datos = prefabEdificio.GetComponentInChildren<EdificioReclutamiento>(true);

        if (datos == null)
            Debug.LogWarning($"'{prefabEdificio.name}' no tiene el componente EdificioReclutamiento: se construiría GRATIS.");

        return datos;
    }

    // ¿Este castillo tiene nivel y recursos para levantar este edificio?
    // 'motivo' explica el fallo (para logs y para la interfaz).
    public bool PuedeConstruirlo(BaseMilitar castillo, out string motivo)
    {
        if (castillo == null)
        {
            motivo = "Sin castillo";
            return false;
        }

        if (castillo.nivelCastillo < nivelRequerido)
        {
            motivo = $"Requiere castillo nivel {nivelRequerido}";
            return false;
        }

        if (castillo.madera < costeConstruccionMadera
         || castillo.piedra < costeConstruccionPiedra
         || castillo.oro < costeConstruccionOro)
        {
            motivo = $"Faltan recursos (M:{costeConstruccionMadera} P:{costeConstruccionPiedra} O:{costeConstruccionOro})";
            return false;
        }

        motivo = "";
        return true;
    }

    // Descuenta el precio del edificio del castillo dueño.
    public void CobrarConstruccion(BaseMilitar castillo)
    {
        if (castillo == null) return;

        castillo.madera -= costeConstruccionMadera;
        castillo.piedra -= costeConstruccionPiedra;
        castillo.oro -= costeConstruccionOro;
        castillo.ActualizarTextosUI();
    }

    // --- FUNCIÓN DE COMPRA --- (ahora devuelve la tropa creada, o null)
    public GameObject ComprarTropa()
    {
        if (miCastillo == null) return null;

        // 1. Población (se respeta siempre, también en modo prueba)
        if (miCastillo.poblacionActual >= miCastillo.poblacionMaxima)
        {
            Debug.Log("Población máxima alcanzada.");
            return null;
        }

        // 2. Recursos: solo se comprueban y se gastan fuera del modo prueba
        if (!modoPrueba)
        {
            bool hayRecursos = miCastillo.madera >= costeMadera
                            && miCastillo.piedra >= costePiedra
                            && miCastillo.oro >= costeOro;

            if (!hayRecursos)
            {
                Debug.Log("Faltan recursos en el castillo para reclutar aquí.");
                return null;
            }

            miCastillo.madera -= costeMadera;
            miCastillo.piedra -= costePiedra;
            miCastillo.oro -= costeOro;
        }

        // 3. Sumamos población, refrescamos UI y spawneamos
        miCastillo.poblacionActual++;
        miCastillo.ActualizarTextosUI();
        GameObject nuevaTropa = SpawnUtils.SpawnConDesplazamiento(prefabTropa, puntoSpawn, 1.5f);

        // 4. Si es un recolector autónomo (IA), le decimos que entregue a NUESTRO castillo
        if (nuevaTropa != null)
        {
            RecolectorIA recolectorIA = nuevaTropa.GetComponent<RecolectorIA>();
            if (recolectorIA != null) recolectorIA.AsignarBase(miCastillo);
        }

        return nuevaTropa;
    }

    // NOTA: el clic sobre el edificio ya NO se detecta aquí con OnMouseDown.
    // Lo gestiona Movimiento.cs con su raycast, porque OnMouseDown se ejecuta
    // ANTES que Update y el panel se abría justo antes de que el clic lo cerrara.
}