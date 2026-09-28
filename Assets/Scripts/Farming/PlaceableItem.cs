using UnityEngine;

namespace PlotNRots.Items
{
    public class PlaceableItem : MonoBehaviour, IPlaceable
    {
        [Header("Prefabs")]
        public GameObject realPrefab;
        public GameObject previewPrefab;

        [Header("Item Data")]
        public int itemLevel = 1;

        public GameObject GetRealPrefab()
        {
            return realPrefab;
        }

        public GameObject GetPreviewPrefab()
        {
            return previewPrefab;
        }

        public void OnPlaced(GameObject placedObject)
        {
            if (placedObject.TryGetComponent<SprinklerDrop>(out SprinklerDrop sprinkler))
            {
                sprinkler.Initialize(itemLevel);
            }
        }
    }
}