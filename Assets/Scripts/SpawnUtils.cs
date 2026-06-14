using UnityEngine;

// Utilidad compartida para spawnear tropas con una pequeña variación de posición.
// Antes este mismo código estaba COPIADO en BaseMilitar.cs y en EdificioReclutamiento.cs.
// Ahora vive en un solo sitio: si quieres cambiar cómo aparecen las tropas, lo cambias aquí.
public static class SpawnUtils
{
    // prefab          -> la unidad que queremos crear
    // puntoSpawn      -> el objeto vacío desde donde aparece
    // distanciaDespeje-> cuánto camina hacia adelante para no amontonarse en la puerta
    public static GameObject SpawnConDesplazamiento(GameObject prefab, Transform puntoSpawn, float distanciaDespeje = 1.0f)
    {
        if (prefab == null || puntoSpawn == null) return null;

        Vector3 variacion = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));
        Vector3 posicionFinal = puntoSpawn.position + variacion;

        GameObject nuevaTropa = Object.Instantiate(prefab, posicionFinal, Quaternion.identity);

        Tropa scriptTropa = nuevaTropa.GetComponent<Tropa>();
        if (scriptTropa != null)
        {
            Vector3 puntoDespeje = puntoSpawn.position + (puntoSpawn.forward * distanciaDespeje) + (variacion * 0.5f);
            scriptTropa.OrdenarMovimiento(puntoDespeje);
        }

        return nuevaTropa;
    }
}