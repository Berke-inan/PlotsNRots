using UnityEngine;

namespace PlotNRots.Items
{
    public interface IPlaceable
    {
        GameObject GetRealPrefab();
        GameObject GetPreviewPrefab();
        void OnPlaced(GameObject placedObject);
    }
}