using UnityEngine;

namespace PlotNRots.Items
{
    public class SprinklerHandItem : MonoBehaviour, IPlaceable
    {
        [Header("Modeller")]
        [Tooltip("Yere konacak, üzerinde SprinklerDrop scripti olan asıl prefab")]
        public GameObject realSprinklerPrefab;

        [Tooltip("Fareyi tuttuğumuz yerde yeşil/mavi görünecek olan Hologram prefab")]
        public GameObject previewHologramPrefab;

        [Header("Özellikler")]
        [Tooltip("1: 3x3 Alan | 2: 5x5 Alan | 3: 7x7 Alan")]
        public int sprinklerLevel = 1;

        // PlayerInteraction bu fonksiyonları çağırarak modelleri çeker
        public GameObject GetRealPrefab() => realSprinklerPrefab;
        public GameObject GetPreviewPrefab() => previewHologramPrefab;

        // Sol tıka basılıp eşya yere konduğunda otomatik çalışır
        public void OnPlaced(GameObject placedObject)
        {
            // Yere konan objenin içindeki SprinklerDrop scriptini bul ve seviyesini ona aktararak çalışmasını başlat
            if (placedObject.TryGetComponent<SprinklerDrop>(out SprinklerDrop sprinkler))
            {
                sprinkler.Initialize(sprinklerLevel);
            }
        }
    }
}