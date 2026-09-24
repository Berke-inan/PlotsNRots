using UnityEngine;

namespace PlotNRots.World.Weather
{
    /// <summary>
    /// Bu component'in bulunduðu GameObject ve bütün alt hiyerarþisi
    /// generic snow shell sisteminden tamamen hariç tutulur.
    ///
    /// Player, animal, NPC ve snow almamasý gereken özel prefab
    /// root'larýnda kullanýlýr.
    ///
    /// Runtime Update çalýþtýrmaz.
    /// Yalnýzca SnowShellManager tarafýndan marker olarak okunur.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Plots & Rots/Weather/Snow Exclusion")]
    public sealed class SnowExclusion : MonoBehaviour
    {
    }
}