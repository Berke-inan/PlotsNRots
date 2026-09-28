using UnityEngine;

namespace PlotNRots.Items
{
    public class ChestHandItem : MonoBehaviour, IPlaceable
    {
        [Header("Modeller")]
        public GameObject realChestPrefab; // Yere konacak asıl sandık (ChestDrop olan prefab)
        public GameObject previewHologramPrefab; // Hologram önizleme

        public GameObject GetRealPrefab() => realChestPrefab;
        public GameObject GetPreviewPrefab() => previewHologramPrefab;

        public void OnPlaced(GameObject placedObject)
        {
            Debug.Log("Sandık başarıyla yere yerleştirildi.");
        }
    }
}