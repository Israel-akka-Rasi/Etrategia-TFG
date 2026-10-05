using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// ============================================================================
// NAVEGACIÓN SEGURA
// Único punto de paso para las órdenes de movimiento de TODAS las unidades.
//
// Un NavMeshAgent solo acepta órdenes si está sobre la malla de navegación.
// Una unidad puede quedar fuera de ella al nacer, durante un reinicio de
// episodio o al ser empujada, y darle una orden en ese estado produce un error
// y la orden se pierde en silencio. Antes cada script llamaba a SetDestination
// por su cuenta, y protegerlos uno a uno dejaba huecos. Ahora todos pasan por aquí.
//
// CÁLCULO DE RUTAS: cada SetDestination pide una ruta nueva y CANCELA la que se
// estuviera calculando. Unity calcula las rutas poco a poco (un número limitado
// de iteraciones por fotograma, compartido por todas las unidades). Varios
// scripts pedían la misma ruta en cada fotograma (Recolector al caminar,
// Combate al perseguir), así que con muchas unidades las rutas no llegaban a
// terminarse nunca: el agente se quedaba en "calculando" con velocidad 0.
// Por eso: (1) no se repite una petición al mismo destino mientras siga en
// curso, y (2) se amplía el presupuesto de cálculo por fotograma.
//
// La causa de fondo de las rutas lentas era otra: 11.712 NavMeshObstacle con
// Carve (árboles, rocas, montañas) troceaban la malla en 138.000 triángulos.
// Se quitaron de esos prefabs (ya forman parte del horneado de la malla):
// quedaron ~17.000 triángulos y las rutas pasaron a calcularse al instante.
//
// OJO: se compara con el destino PEDIDO, no con agente.destination. Unity
// guarda en destination el punto proyectado sobre la malla, que para un árbol
// o un castillo (cuyo centro queda fuera de la zona caminable) está a varios
// metros del pedido: comparando con él, la repetición nunca se detectaba.
// ============================================================================

public static class NavegacionSegura
{
    const float RADIO_RECOLOCACION = 5f;

    // Destinos a menos de esta distancia se consideran el mismo (no se repite la petición)
    const float MISMO_DESTINO = 0.5f;

    // Iteraciones de búsqueda de ruta por fotograma (Unity trae 100 por defecto).
    // El presupuesto es POR FOTOGRAMA: en el entrenamiento (escala 20 / captura
    // 300 = 15 fotogramas por segundo de juego) rinde 4 veces menos por segundo
    // de juego que en el editor a 1x, así que se deja margen de sobra.
    const int ITERACIONES_RUTA_POR_FOTOGRAMA = 5000;

    // Último destino pedido por cada agente (por su identificador)
    static readonly Dictionary<int, Vector3> ultimoPedido = new Dictionary<int, Vector3>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ConfigurarCalculoDeRutas()
    {
        NavMesh.pathfindingIterationsPerFrame = ITERACIONES_RUTA_POR_FOTOGRAMA;
    }

    // Mueve la unidad al destino. Devuelve false si no ha podido darse la orden.
    // Con "reanudar" quita además el freno del agente (isStopped), que tampoco
    // puede tocarse fuera de la malla.
    public static bool IrA(NavMeshAgent agente, Vector3 destino, bool reanudar = false)
    {
        if (agente == null || !agente.isActiveAndEnabled) return false;

        // Fuera de la malla: la recolocamos en el punto caminable más cercano
        if (!agente.isOnNavMesh && !Recolocar(agente)) return false;

        if (reanudar) agente.isStopped = false;

        // Ya va (o está calculando cómo ir) a ese mismo sitio: no repetimos la
        // petición, porque cancelaría la ruta en curso y volvería a empezar.
        int id = agente.GetInstanceID();
        Vector3 anterior;
        if ((agente.pathPending || agente.hasPath)
            && ultimoPedido.TryGetValue(id, out anterior)
            && (anterior - destino).sqrMagnitude < MISMO_DESTINO * MISMO_DESTINO)
            return true;

        // Las unidades destruidas dejan su entrada: se vacía de vez en cuando
        if (ultimoPedido.Count > 5000) ultimoPedido.Clear();

        ultimoPedido[id] = destino;
        return agente.SetDestination(destino);
    }

    // Pone o quita el freno del agente, solo si está sobre la malla
    public static void Frenar(NavMeshAgent agente, bool frenado)
    {
        if (agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh)
            agente.isStopped = frenado;
    }

    public static bool Recolocar(NavMeshAgent agente)
    {
        if (agente == null || !agente.isActiveAndEnabled) return false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(agente.transform.position, out hit, RADIO_RECOLOCACION, NavMesh.AllAreas))
            return agente.Warp(hit.position);

        return false;
    }
}