using UnityEngine;

public class EdificioReclutamiento : MonoBehaviour
{
    [Header("Configuración del Edificio")]
    public string nombreEdificio = "Campamento Minero";
    public Transform puntoSpawn; // El objeto vacío delante de la puerta

    [Header("Configuración de la Tropa")]
    public GameObject prefabTropa; 
    public int costeMadera = 20;
    public int costePiedra = 0;
    public int costeOro = 0;

    // Referencia al castillo del jugador (el "Banco")
    private BaseMilitar miCastillo; 

    void Start()
    {
        // Al construirse, el edificio busca automáticamente tu castillo para conectarse a la economía
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == 0) // Equipo 0 es tu jugador
            {
                miCastillo = b;
                break;
            }
        }
    }

    // --- FUNCIÓN DE COMPRA (Igual que en BaseMilitar, pero conectada al Castillo) ---
    /*public void ComprarTropa()
    {
        if (miCastillo == null) return;
        
        // 1. Comprobamos población
        if (miCastillo.poblacionActual >= miCastillo.poblacionMaxima) 
        {
            Debug.Log("Población máxima alcanzada.");
            return;
        }

        // 2. Comprobamos recursos
        if (miCastillo.madera >= costeMadera && miCastillo.piedra >= costePiedra && miCastillo.oro >= costeOro)
        {
            // 3. Cobramos los recursos al castillo
            miCastillo.madera -= costeMadera;
            miCastillo.piedra -= costePiedra;
            miCastillo.oro -= costeOro;
            
            miCastillo.poblacionActual++;

            // 4. Actualizamos la interfaz
            miCastillo.ActualizarTextosUI();

            // 5. Instanciamos la tropa en este edificio
            RealizarSpawnConDesplazamiento();
        }
        else
        {
            Debug.Log("Faltan recursos en el castillo para reclutar aquí.");
        }
    }*/

    public void ComprarTropa()
    {
        if (miCastillo == null) return;
        
        // 1. Comprobamos población (esto lo mantenemos para no bugear el juego)
        if (miCastillo.poblacionActual >= miCastillo.poblacionMaxima) 
        {
            Debug.Log("Población máxima alcanzada.");
            return;
        }

        // 2. MODO PRUEBA: Nos saltamos la comprobación de recursos
        // if (miCastillo.madera >= costeMadera && miCastillo.piedra >= costePiedra && miCastillo.oro >= costeOro)
        // {
            // 3. MODO PRUEBA: No restamos recursos
            // miCastillo.madera -= costeMadera;
            // miCastillo.piedra -= costePiedra;
            // miCastillo.oro -= costeOro;
            
            miCastillo.poblacionActual++;
            miCastillo.ActualizarTextosUI();
            RealizarSpawnConDesplazamiento();
        // }
    }

    // --- LÓGICA DE SPAWN (Copiada exacta de tu BaseMilitar) ---
    private void RealizarSpawnConDesplazamiento()
    {
        Vector3 variacion = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
        Vector3 posicionFinal = puntoSpawn.position + variacion;

        GameObject nuevaTropa = Instantiate(prefabTropa, posicionFinal, Quaternion.identity);

        Tropa scriptTropa = nuevaTropa.GetComponent<Tropa>();
        if (scriptTropa != null)
        {
            // Le decimos que camine un poquito hacia adelante (usando el forward del punto de spawn)
            Vector3 puntoDespeje = puntoSpawn.position + (puntoSpawn.forward * 1.5f) + (variacion * 0.5f);
            scriptTropa.OrdenarMovimiento(puntoDespeje);
        }
    }

    // --- DETECCIÓN DE CLIC DEL RATÓN ---
    private void OnMouseDown()
    {
        // Cuando haces clic en el edificio, avisa al UIManager para que abra su menú
        Debug.Log("Has hecho clic en: " + nombreEdificio);
        
        if (UIManager.Instancia != null)
        {
            // Más adelante programaremos esta función en el UIManager
            UIManager.Instancia.AbrirMenuEdificio(this); 
        }
    }
}