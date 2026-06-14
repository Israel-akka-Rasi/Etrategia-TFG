using UnityEngine;

public class EdificioReclutamiento : MonoBehaviour
{
    [Header("Configuración del Edificio")]
    public string nombreEdificio = "Campamento Minero";
    public Transform puntoSpawn; // El objeto vacío delante de la puerta

    [Header("Propietario")]
    // 0 = jugador, 1 = IA. La IA lo pone a 1 al construirlo (con AsignarCastillo).
    public int equipo = 0;

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

    // --- DETECCIÓN DE CLIC DEL RATÓN ---
    private void OnMouseDown()
    {
        // El jugador solo puede abrir el menú de SUS edificios (equipo 0).
        if (equipo != 0) return;

        Debug.Log("Has hecho clic en: " + nombreEdificio);

        if (UIManager.Instancia != null)
        {
            UIManager.Instancia.AbrirMenuEdificio(this);
        }
    }
}