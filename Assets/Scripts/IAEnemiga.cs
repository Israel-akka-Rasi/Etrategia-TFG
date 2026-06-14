using UnityEngine;
using System.Collections.Generic;

public enum FaseEstrategia
{
    Economia,
    Construccion,
    PreparacionMilitar,
    Asedio
}

// Cada entrada describe UN edificio que la IA construirá y cuántas tropas sacará de él.
// Se rellena desde el Inspector. Añadir un edificio = añadir una fila a la lista.
[System.Serializable]
public class PlanEdificio
{
    public string nombre = "Edificio";        // Solo para que te aclares en el Inspector
    public GameObject prefabEdificio;          // El prefab del edificio a construir
    public int tropasAReclutar = 2;            // Cuántas unidades saca de él
    public bool tropasSonMilitares = false;    // true = se unen al ejército de asedio; false = economía

    // --- Estado en tiempo de ejecución (no se toca en el Inspector) ---
    [System.NonSerialized] public EdificioReclutamiento edificioConstruido;
    [System.NonSerialized] public int tropasReclutadas;
}

public class IAEnemiga : MonoBehaviour
{
    private BaseMilitar miBase;

    [Header("Estrategia Actual")]
    public FaseEstrategia faseActual = FaseEstrategia.Economia;

    [Header("Condiciones de Cambio de Fase")]
    public int limiteRecolectores = 4; // Leñadores antes de empezar a construir
    public int limiteEjercito = 5;     // Caballeros (del castillo) antes de atacar

    [Header("Plan de Construcción")]
    // El orden de la lista es el orden en que los construye (de arriba a abajo).
    public List<PlanEdificio> planConstruccion = new List<PlanEdificio>();

    [Header("Tiempos")]
    public float segundosEntreDecisiones = 3f; // Bajo para pruebas (sube a 10-15 en la versión final)

    [Header("Construcción Procedimental")]
    public float radioConstruccionMin = 3f;  // Distancia MÍNIMA: no pegar edificios al castillo
    public float radioConstruccion = 10f;    // Distancia MÁXIMA desde el castillo (antes 15)
    public int cantidadEdificiosMaximos = 6;
    public float distanciaEntreEdificios = 5f;

    // Coordenadas donde la IA puede levantar edificios
    private List<Vector3> puntosDeAnclaje = new List<Vector3>();
    private List<bool> anclajesOcupados = new List<bool>();

    // Conteo de tropas y progreso del plan
    private int recolectoresActuales = 0;
    private int planActual = 0; // En qué edificio de la lista vamos
    private List<GameObject> miEjercito = new List<GameObject>();

    void Start()
    {
        miBase = GetComponent<BaseMilitar>();
        GenerarAnclajesAleatorios();

        if (miBase != null)
        {
            Invoke("SpawnInicial", 1f);
            InvokeRepeating("TomarDecisionEstrategica", segundosEntreDecisiones, segundosEntreDecisiones);
        }
    }

    void SpawnInicial()
    {
        for (int i = 0; i < 2; i++)
        {
            if (miBase.ComprarLeñador() != null) recolectoresActuales++;
        }
    }

    void TomarDecisionEstrategica()
    {
        switch (faseActual)
        {
            case FaseEstrategia.Economia:
                LogicaEconomia();
                break;

            case FaseEstrategia.Construccion:
                LogicaConstruccion();
                break;

            case FaseEstrategia.PreparacionMilitar:
                LogicaPreparacion();
                break;

            case FaseEstrategia.Asedio:
                LogicaAsedio();
                break;
        }
    }

    // --- FASE ECONOMÍA: leñadores desde el castillo ---
    void LogicaEconomia()
    {
        if (recolectoresActuales < limiteRecolectores)
        {
            if (miBase.ComprarLeñador() != null)
            {
                recolectoresActuales++;
                Debug.Log("IAEnemiga: Leñador reclutado.");
            }
            else
            {
                Debug.Log("IAEnemiga: esperando recursos para leñador...");
            }
        }
        else
        {
            faseActual = FaseEstrategia.Construccion;
            Debug.Log("IAEnemiga: ¡Pasando a Fase de Construcción!");
        }
    }

    // --- FASE CONSTRUCCIÓN: recorre el plan de la lista ---
    void LogicaConstruccion()
    {
        // ¿Terminamos toda la lista?
        if (planActual >= planConstruccion.Count)
        {
            faseActual = FaseEstrategia.PreparacionMilitar;
            Debug.Log("IAEnemiga: Plan de construcción completo. ¡A lo militar!");
            return;
        }

        PlanEdificio plan = planConstruccion[planActual];

        // 1. Construir el edificio si aún no está
        if (plan.edificioConstruido == null)
        {
            if (plan.prefabEdificio == null)
            {
                Debug.LogWarning($"IAEnemiga: '{plan.nombre}' no tiene prefab asignado, lo salto.");
                planActual++;
                return;
            }

            plan.edificioConstruido = ConstruirEdificio(plan.prefabEdificio);

            if (plan.edificioConstruido == null)
            {
                Debug.Log($"IAEnemiga: no pude construir '{plan.nombre}' (sin hueco libre), lo salto.");
                planActual++;
                return;
            }

            Debug.Log($"IAEnemiga: '{plan.nombre}' construido.");
            return;
        }

        // 2. Reclutar sus tropas
        if (plan.tropasReclutadas < plan.tropasAReclutar)
        {
            GameObject tropa = plan.edificioConstruido.ComprarTropa();
            if (tropa != null)
            {
                plan.tropasReclutadas++;
                if (plan.tropasSonMilitares) miEjercito.Add(tropa); // las militares irán al asedio
                Debug.Log($"IAEnemiga: tropa {plan.tropasReclutadas}/{plan.tropasAReclutar} de '{plan.nombre}'.");
            }
            return;
        }

        // 3. Edificio terminado -> siguiente de la lista
        planActual++;
    }

    // --- FASE MILITAR: caballeros desde el castillo ---
    void LogicaPreparacion()
    {
        miEjercito.RemoveAll(soldado => soldado == null);

        if (miEjercito.Count < limiteEjercito)
        {
            GameObject nuevoSoldado = miBase.ComprarCaballero();
            if (nuevoSoldado != null)
            {
                miEjercito.Add(nuevoSoldado);
                Debug.Log($"IAEnemiga: Soldado registrado. Ejército: {miEjercito.Count}/{limiteEjercito}");
            }
        }
        else
        {
            faseActual = FaseEstrategia.Asedio;
            Debug.Log("IAEnemiga: ¡EJÉRCITO LISTO! ¡Iniciando fase de Asedio!");
            LogicaAsedio();
        }
    }

    // --- FASE ASEDIO: marchar al castillo enemigo ---
    void LogicaAsedio()
    {
        miEjercito.RemoveAll(soldado => soldado == null);

        BaseMilitar baseEnemiga = BuscarBaseEnemiga();
        if (baseEnemiga == null)
        {
            Debug.Log("Comandante Rojo: no encuentro base enemiga que asediar.");
            return;
        }

        foreach (GameObject soldado in miEjercito)
        {
            if (soldado == null) continue;

            Combate combate = soldado.GetComponent<Combate>();
            if (combate != null && !combate.EstaAsediando())
            {
                combate.OrdenarAsedio(baseEnemiga.transform);
            }
        }

        Debug.Log($"Comandante Rojo: asediando {baseEnemiga.gameObject.name} con {miEjercito.Count} soldados.");
    }

    // --- CONSTRUCCIÓN (mecanismo) ---
    EdificioReclutamiento ConstruirEdificio(GameObject prefabEdificio)
    {
        Vector3 punto;
        if (!IntentarObtenerPuntoConstruccion(out punto))
        {
            return null; // No quedan huecos libres
        }

        GameObject edificioGO = Instantiate(prefabEdificio, punto, Quaternion.identity);

        EdificioReclutamiento edificio = edificioGO.GetComponent<EdificioReclutamiento>();
        if (edificio != null)
        {
            edificio.AsignarCastillo(miBase); // ¡es NUESTRO edificio!
        }

        return edificio;
    }

    BaseMilitar BuscarBaseEnemiga()
    {
        BaseMilitar[] bases = FindObjectsOfType<BaseMilitar>();
        BaseMilitar objetivo = null;
        float distanciaMinima = Mathf.Infinity;

        foreach (BaseMilitar b in bases)
        {
            if (b.equipo == miBase.equipo) continue;

            float distancia = Vector3.Distance(transform.position, b.transform.position);
            if (distancia < distanciaMinima)
            {
                distanciaMinima = distancia;
                objetivo = b;
            }
        }

        return objetivo;
    }

    void GenerarAnclajesAleatorios()
    {
        int intentosMaximos = 30;
        int intentos = 0;

        while (puntosDeAnclaje.Count < cantidadEdificiosMaximos && intentos < intentosMaximos)
        {
            intentos++;

            // Dirección aleatoria + distancia aleatoria entre el mínimo y el máximo.
            // (Antes era .normalized * radioConstruccion, que clavaba TODOS los edificios
            //  en el borde exterior del círculo; por eso salían tan lejos del castillo.)
            float angulo = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            float distancia = Random.Range(radioConstruccionMin, radioConstruccion);
            Vector3 puntoCandidato = transform.position + direccion * distancia;

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(puntoCandidato, out hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                bool espacioLibre = true;
                foreach (Vector3 anclajeExistente in puntosDeAnclaje)
                {
                    if (Vector3.Distance(hit.position, anclajeExistente) < distanciaEntreEdificios)
                    {
                        espacioLibre = false;
                        break;
                    }
                }

                if (espacioLibre)
                {
                    puntosDeAnclaje.Add(hit.position);
                    anclajesOcupados.Add(false);
                }
            }
        }

        Debug.Log($"IAEnemiga: Se han generado {puntosDeAnclaje.Count} huecos de construcción válidos.");
    }

    public bool IntentarObtenerPuntoConstruccion(out Vector3 puntoLibre)
    {
        for (int i = 0; i < puntosDeAnclaje.Count; i++)
        {
            if (!anclajesOcupados[i])
            {
                anclajesOcupados[i] = true;
                puntoLibre = puntosDeAnclaje[i];
                return true;
            }
        }

        puntoLibre = Vector3.zero;
        return false;
    }
}