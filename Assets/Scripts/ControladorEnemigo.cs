using UnityEngine;

public class ControladorEnemigo : MonoBehaviour
{
    [Header("Configuración del Comandante Enemigo")]
    public float tiempoDePensamiento = 2f; // Cada cuántos segundos la IA toma decisiones

    private BaseMilitar miBase;
    private Mina minaObjetivo;
    private BaseMilitar baseJugador;

    void Start()
    {
        // 1. La IA reconoce su propio castillo (donde pondremos este script)
        miBase = GetComponent<BaseMilitar>();

        // 2. La IA mira el mapa y localiza la mina y tu castillo
        minaObjetivo = FindObjectOfType<Mina>();
        
        BaseMilitar[] todasLasBases = FindObjectsOfType<BaseMilitar>();
        foreach (BaseMilitar b in todasLasBases)
        {
            if (b.equipo == 0) baseJugador = b; // El equipo 0 eres tú
        }

        // 3. Arrancamos el cerebro de la IA para que piense en bucle
        InvokeRepeating("PensarYActuar", tiempoDePensamiento, tiempoDePensamiento);
    }

    void PensarYActuar()
    {
        // --- ACCIÓN 1: ECONOMÍA ---
        // La base intentará comprar un esqueleto siempre que tenga 5 de oro
        miBase.IntentarComprarTropa();

        // --- ACCIÓN 2: TÁCTICA MILITAR ---
        // Buscamos a todos los soldados que hay en el mapa
        Tropa[] todosLosSoldados = FindObjectsOfType<Tropa>();

        foreach (Tropa soldado in todosLosSoldados)
        {
            // Solo le damos órdenes a los soldados ROJOS
            if (soldado.equipo == miBase.equipo)
            {
                Combate scriptCombate = soldado.GetComponent<Combate>();

                // Si el esqueleto ya está peleando o persiguiendo a alguien, no lo interrumpimos
                if (scriptCombate.TieneObjetivo()) continue;

                // ESTRATEGIA: Si está sin hacer nada, lo mandamos a capturar la mina
                if (minaObjetivo != null)
                {
                    // Le damos la orden de caminar hacia la mina
                    // (Gracias al sistema de Aggro que programamos, si se cruza con tus caballeros, 
                    // se parará a pelear automáticamente y luego seguirá su camino).
                    soldado.OrdenarMovimiento(minaObjetivo.transform.position);
                }
                else if (baseJugador != null)
                {
                    // Si no hay mina por algún casual, que vaya directo a asediar tu castillo
                    scriptCombate.OrdenarAtaque(baseJugador.transform);
                }
            }
        }
    }
}