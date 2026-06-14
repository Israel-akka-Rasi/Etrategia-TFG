using UnityEngine;

// Utilidad compartida para spawnear tropas con una pequeña variación de posición.
// Vive en un solo sitio para no duplicar este código en BaseMilitar y EdificioReclutamiento.
public static class SpawnUtils
{
    public static GameObject SpawnConDesplazamiento(GameObject prefab, Transform puntoSpawn, float distanciaDespeje = 1.0f)
    {
        if (prefab == null || puntoSpawn == null) return null;

        Vector3 variacion = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
        Vector3 posicionFinal = puntoSpawn.position + variacion;

        GameObject nuevaTropa = Object.Instantiate(prefab, posicionFinal, Quaternion.identity);

        // Si hay una partida en marcha, metemos la tropa en el contenedor dinámico
        // para poder borrarla de golpe al reiniciar el episodio.
        if (GestorPartida.Instancia != null && GestorPartida.Instancia.contenedorDinamico != null)
        {
            nuevaTropa.transform.SetParent(GestorPartida.Instancia.contenedorDinamico, true);
        }

        Tropa scriptTropa = nuevaTropa.GetComponent<Tropa>();
        if (scriptTropa != null)
        {
            Vector3 puntoDespeje = puntoSpawn.position + (puntoSpawn.forward * distanciaDespeje) + (variacion * 0.5f);
            scriptTropa.OrdenarMovimiento(puntoDespeje);
        }

        return nuevaTropa;
    }
}