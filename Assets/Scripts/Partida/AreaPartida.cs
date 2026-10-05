using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// ÁREA DE PARTIDA
// Representa UNA partida completa e independiente. Todo lo que pertenece a esa
// partida (mapa, castillos, tropas, edificios, recursos) cuelga de este objeto.
//
// ¿Por qué existe? Para poder entrenar varias partidas a la vez en la misma
// escena. Sin esto, un FindObjectsOfType<Tropa>() devolvería las tropas de
// TODAS las partidas mezcladas y el entrenamiento sería basura.
//
// Además resuelve un problema de rendimiento: en lugar de recorrer la escena
// entera cada vez, cada área mantiene su propio registro. A 15x de velocidad
// y con varias áreas, esa diferencia es enorme.
// ============================================================================

public class AreaPartida : MonoBehaviour
{
    // Todas las áreas vivas de la escena
    public static readonly List<AreaPartida> Todas = new List<AreaPartida>();

    [Header("Identidad")]
    public int idArea = 0;
    // Solo un área debe estar marcada como visible: es la que usa la interfaz,
    // la cámara y el control por ratón. El resto entrenan sin UI.
    public bool esAreaVisible = true;

    [Header("Contenedor de Objetos Dinámicos")]
    // Tropas y edificios creados durante la partida. Se vacía al reiniciar.
    public Transform contenedorDinamico;

    // --- Registros propios del área ---
    private readonly List<BaseMilitar> bases = new List<BaseMilitar>();
    private readonly List<Tropa> tropas = new List<Tropa>();
    private readonly List<EdificioReclutamiento> edificios = new List<EdificioReclutamiento>();
    private readonly List<Recurso> recursos = new List<Recurso>();

    void Awake()
    {
        if (!Todas.Contains(this)) Todas.Add(this);
    }

    void OnDestroy()
    {
        Todas.Remove(this);
    }

    // ========================================================================
    // RESOLUCIÓN: ¿a qué área pertenece este objeto?
    // Sube por la jerarquía buscando su área. Si la escena no usa áreas
    // (partida única de toda la vida), crea una implícita para que todo
    // siga funcionando igual sin tener que reorganizar nada.
    // ========================================================================
    public static AreaPartida Resolver(Component objeto)
    {
        if (objeto == null) return AreaPorDefecto();

        AreaPartida area = objeto.GetComponentInParent<AreaPartida>();
        if (area != null) return area;

        return AreaPorDefecto();
    }

    static AreaPartida AreaPorDefecto()
    {
        if (Todas.Count > 0) return Todas[0];

        // No hay ninguna: creamos un área implícita que englobe la escena
        GameObject go = new GameObject("AreaPartida (implícita)");
        AreaPartida nueva = go.AddComponent<AreaPartida>();
        nueva.esAreaVisible = true;
        return nueva;
    }

    // El área que ve el jugador humano (la que tiene interfaz y cámara)
    public static AreaPartida AreaVisible()
    {
        foreach (AreaPartida a in Todas)
            if (a.esAreaVisible) return a;
        return Todas.Count > 0 ? Todas[0] : null;
    }

    // ========================================================================
    // ALTA Y BAJA EN EL REGISTRO
    // Cada objeto se apunta al nacer y se borra al morir.
    // ========================================================================
    public void Registrar(BaseMilitar b) { if (b != null && !bases.Contains(b)) bases.Add(b); }
    public void Registrar(Tropa t) { if (t != null && !tropas.Contains(t)) tropas.Add(t); }
    public void Registrar(EdificioReclutamiento e) { if (e != null && !edificios.Contains(e)) edificios.Add(e); }
    public void Registrar(Recurso r) { if (r != null && !recursos.Contains(r)) recursos.Add(r); }

    public void Desregistrar(BaseMilitar b) { bases.Remove(b); }
    public void Desregistrar(Tropa t) { tropas.Remove(t); }
    public void Desregistrar(EdificioReclutamiento e) { edificios.Remove(e); }
    public void Desregistrar(Recurso r) { recursos.Remove(r); }

    // ========================================================================
    // CONSULTAS (sustituyen a los FindObjectsOfType)
    // Devuelven solo objetos de ESTA partida, y ya limpios de destruidos.
    // ========================================================================
    public List<BaseMilitar> Bases()
    {
        bases.RemoveAll(b => b == null);
        return bases;
    }

    public List<Tropa> Tropas()
    {
        tropas.RemoveAll(t => t == null);
        return tropas;
    }

    public List<EdificioReclutamiento> Edificios()
    {
        edificios.RemoveAll(e => e == null);
        return edificios;
    }

    public List<Recurso> Recursos()
    {
        recursos.RemoveAll(r => r == null);
        return recursos;
    }

    // El castillo de un equipo concreto (0 = jugador/agente, 1 = enemigo)
    public BaseMilitar BaseDeEquipo(int equipo)
    {
        foreach (BaseMilitar b in Bases())
            if (b.equipo == equipo) return b;
        return null;
    }

    // El castillo enemigo más cercano a un punto
    public BaseMilitar BaseEnemigaMasCercana(int miEquipo, Vector3 desde)
    {
        BaseMilitar mejor = null;
        float minima = Mathf.Infinity;

        foreach (BaseMilitar b in Bases())
        {
            if (b.equipo == miEquipo) continue;
            float d = Vector3.Distance(desde, b.transform.position);
            if (d < minima) { minima = d; mejor = b; }
        }
        return mejor;
    }

    // Tropas de un equipo dentro de esta partida
    public List<Tropa> TropasDeEquipo(int equipo)
    {
        List<Tropa> lista = new List<Tropa>();
        foreach (Tropa t in Tropas())
            if (t.equipo == equipo) lista.Add(t);
        return lista;
    }

    // Nodo de recurso disponible más cercano, de un tipo concreto
    public Recurso RecursoMasCercano(Recurso.TipoRecurso tipo, Vector3 desde, float radioMaximo)
    {
        Recurso mejor = null;
        float minima = radioMaximo;

        foreach (Recurso r in Recursos())
        {
            if (r.tipo != tipo || !r.EstaDisponible()) continue;
            float d = Vector3.Distance(desde, r.transform.position);
            if (d < minima) { minima = d; mejor = r; }
        }
        return mejor;
    }

    // --- Gestor de ciclo de partida de ESTA área ---
    private GestorPartida gestor;
    public GestorPartida Gestor()
    {
        if (gestor != null) return gestor;

        gestor = GetComponentInChildren<GestorPartida>(true);

        // Escena antigua (un solo GestorPartida suelto): lo adoptamos
        if (gestor == null) gestor = FindObjectOfType<GestorPartida>();

        return gestor;
    }

    // --- Entorno de aprendizaje de ESTA área (si lo hay) ---
    // Igual que con el gestor: si no cuelga del área (escena sin áreas montadas,
    // con el entorno suelto en la raíz), lo buscamos en la escena. Sin este
    // respaldo el gestor creería que no hay agente y reiniciaría por su cuenta,
    // impidiendo que el agente llegue a ver el final del episodio.
    private EntornoRL entorno;
    public EntornoRL Entorno()
    {
        if (entorno != null) return entorno;

        entorno = GetComponentInChildren<EntornoRL>(true);
        if (entorno == null) entorno = FindObjectOfType<EntornoRL>();

        return entorno;
    }

    // Contenedor donde nacen tropas y edificios de esta partida.
    // Si no se ha asignado uno al área, se hereda el del GestorPartida (que es
    // como estaban montadas las escenas antiguas). Así SIEMPRE coinciden el sitio
    // donde nacen los objetos y el sitio que se vacía al reiniciar: si no,
    // el reinicio no borraría nada y las tropas se irían acumulando.
    public Transform Contenedor()
    {
        if (contenedorDinamico != null) return contenedorDinamico;

        GestorPartida g = Gestor();
        if (g != null && g.contenedorDinamico != null)
        {
            contenedorDinamico = g.contenedorDinamico;
            return contenedorDinamico;
        }

        contenedorDinamico = transform; // último recurso
        return contenedorDinamico;
    }

    // Mete un objeto recién creado en el contenedor dinámico de ESTA partida,
    // para que se limpie al reiniciar y quede dentro del área correcta.
    public void Adoptar(GameObject objeto)
    {
        if (objeto == null) return;

        Transform destino = Contenedor();
        if (destino != null) objeto.transform.SetParent(destino, true);

        // IMPORTANTE: Unity ejecuta Awake DENTRO de Instantiate, antes de que
        // podamos darle padre. En ese momento el objeto no cuelga de ningún
        // área, Resolver() cae en el área por defecto (Todas[0]) y se registra
        // ALLÍ. Sin esta corrección, todo lo creado durante la partida en
        // cualquier área acababa registrado en la misma.
        foreach (Tropa t in objeto.GetComponentsInChildren<Tropa>(true))
            t.CambiarArea(this);
        foreach (EdificioReclutamiento e in objeto.GetComponentsInChildren<EdificioReclutamiento>(true))
            e.CambiarArea(this);
    }
}