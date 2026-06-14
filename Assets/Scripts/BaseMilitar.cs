using UnityEngine;
using UnityEngine.UI;

public class BaseMilitar : MonoBehaviour
{
    [Header("Identidad y Progreso")]
    public int equipo; // 0 = Azul (Jugador), 1 = Rojo (Enemigo)
    public int nivelCastillo = 1; // 15 es el máximo

    [Header("Bóveda de Recursos")]
    public int madera = 0;
    public int piedra = 0;
    public int oro = 0;

    [Header("Gestión de Población")]
    public int poblacionActual = 1; // Empezamos en 1 por el Leñador inicial gratis
    public int poblacionMaxima = 5;

    [Header("Combate y Spawn")]
    public float salud = 1000f;
    private float saludMaxima;
    public Image barraSalud;
    public Transform puntoSpawn;

    [Header("Evolución")]
    public int costeBaseMejora = 50; // Madera base para subir niveles

    // ====================================================================
    // RECLUTAMIENTO DESDE EL CASTILLO
    // El castillo SOLO produce estas dos unidades. El resto (Minero, Arquero,
    // Recolector de Oro, Gladiador) salen de sus edificios (EdificioReclutamiento).
    // Esto vale igual para el jugador (equipo 0) y para la IA (equipo 1):
    // cada base asigna en el Inspector los prefabs de SU bando.
    // ====================================================================
    [Header("Reclutamiento del Castillo")]
    public GameObject prefabLeñador; // Recolector de madera (jugador: Recolector / IA: RecolectorIA)
    public int costeLeñador = 10;    // Madera

    public GameObject prefabCaballero; // Unidad de combate básica
    public int costeCaballero = 10;    // Madera

    void Start()
    {
        saludMaxima = salud;

        // --- TROPA INICIAL GRATIS: un Leñador ---
        // No tocamos población porque ya arrancamos en 1 contando con él.
        if (prefabLeñador != null && puntoSpawn != null)
        {
            GameObject leñadorInicial = SpawnUtils.SpawnConDesplazamiento(prefabLeñador, puntoSpawn, 1.5f);
            AsignarBaseSiEsRecolector(leñadorInicial);
        }

        ActualizarTextosUI();
    }

    // --- RECOLECCIÓN DEL MAPA ---
    public void RecibirRecurso(string tipo, int cantidad)
    {
        if (tipo == "Madera") madera += cantidad;
        else if (tipo == "Piedra") piedra += cantidad;
        else if (tipo == "Oro") oro += cantidad;

        ActualizarTextosUI();
    }

    // --- CONEXIÓN CON LA INTERFAZ ---
    public void ActualizarTextosUI()
    {
        if (equipo == 0 && UIManager.Instancia != null)
        {
            UIManager.Instancia.ActualizarEconomia(nivelCastillo, poblacionActual, poblacionMaxima, madera, piedra, oro);
        }
    }

    // --- LÓGICA DE MEJORA ---
    public void MejorarCastillo()
    {
        if (nivelCastillo >= 15) return;

        int maderaNecesaria = 0;
        int piedraNecesaria = 0;
        int oroNecesario = 0;

        if (nivelCastillo < 5)
        {
            maderaNecesaria = costeBaseMejora * nivelCastillo;
        }
        else if (nivelCastillo < 10)
        {
            maderaNecesaria = costeBaseMejora * nivelCastillo;
            piedraNecesaria = 20 * (nivelCastillo - 4);
        }
        else
        {
            piedraNecesaria = 50;
            oroNecesario = 10 * (nivelCastillo - 10);
        }

        if (madera >= maderaNecesaria && piedra >= piedraNecesaria && oro >= oroNecesario)
        {
            madera -= maderaNecesaria;
            piedra -= piedraNecesaria;
            oro -= oroNecesario;

            nivelCastillo++;
            poblacionMaxima += 2;

            Debug.Log($"Mejorado a Nivel {nivelCastillo}. Costó: M:{maderaNecesaria} P:{piedraNecesaria} O:{oroNecesario}");

            if (equipo == 0) ActualizarInterfazAlMejorar();
            ActualizarTextosUI();
        }
        else
        {
            Debug.Log("Faltan recursos para mejorar el castillo.");
        }
    }

    void ActualizarInterfazAlMejorar()
    {
        if (UIManager.Instancia != null && UIManager.Instancia.panelReclutamiento != null)
        {
            if (UIManager.Instancia.panelReclutamiento.activeSelf)
            {
                UIManager.Instancia.MostrarPanelReclutamiento(true, nivelCastillo);
            }
        }
    }

    // --- LÓGICA DE COMBATE ---
    public void RecibirDano(int cantidad)
    {
        salud -= cantidad;

        if (barraSalud != null)
        {
            barraSalud.fillAmount = salud / saludMaxima;
        }

        if (salud <= 0)
        {
            GameManager gestor = FindObjectOfType<GameManager>();
            if (gestor != null)
            {
                gestor.TerminarPartida(equipo);
            }
            Destroy(gameObject);
        }
    }

    // ====================================================================
    // MÉTODO GENÉRICO DE RECLUTAMIENTO
    // Comprueba población y recursos, cobra, spawnea y ASIGNA esta base a la
    // tropa creada (clave cuando hay varias bases del mismo bando).
    // Devuelve la tropa creada (o null si no se pudo).
    // ====================================================================
    public GameObject Reclutar(GameObject prefab, int costeM, int costeP = 0, int costeO = 0)
    {
        if (prefab == null) return null;
        if (poblacionActual >= poblacionMaxima) return null;

        if (madera < costeM || piedra < costeP || oro < costeO) return null;

        madera -= costeM;
        piedra -= costeP;
        oro -= costeO;
        poblacionActual++;

        GameObject nuevaTropa = SpawnUtils.SpawnConDesplazamiento(prefab, puntoSpawn);
        AsignarBaseSiEsRecolector(nuevaTropa);
        ActualizarTextosUI();
        return nuevaTropa;
    }

    // Si la tropa recién creada es un recolector autónomo (IA), le decimos
    // que esta es su base, para que entregue aquí y no en otra base roja.
    private void AsignarBaseSiEsRecolector(GameObject tropa)
    {
        if (tropa == null) return;

        RecolectorIA recolectorIA = tropa.GetComponent<RecolectorIA>();
        if (recolectorIA != null) recolectorIA.AsignarBase(this);
    }

    // --- Atajos para las dos unidades del castillo ---
    public GameObject ComprarLeñador()
    {
        return Reclutar(prefabLeñador, costeLeñador);
    }

    public GameObject ComprarCaballero()
    {
        return Reclutar(prefabCaballero, costeCaballero);
    }
}