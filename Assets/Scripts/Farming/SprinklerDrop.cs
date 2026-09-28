using UnityEngine;
using System.Collections;
using PlotNRots.Managers;
using PlotNRots.Player;

namespace PlotNRots.Items
{
    public class SprinklerDrop : MonoBehaviour, IInteractable
    {
        [Header("Sprinkler Ayarları")]
        [Tooltip("Sulama menzili (Örn: 1 yaparsanız 3x3, 2 yaparsanız 5x5 alan sular)")]
        public int range = 1;

        [Tooltip("Kaç GERÇEK saniyede bir otomatik sulama yapsın?")]
        public float waterInterval = 5f;

        [Header("Envanter (Yerden Almak İçin)")]
        public ItemData sprinklerItemData;

        private Coroutine waterCoroutine;

        public void Initialize(int ignoredLevel = 1)
        {
            WaterArea();
            waterCoroutine = StartCoroutine(WaterRoutine());
        }

        private IEnumerator WaterRoutine()
        {
            while (true)
            {
                // Oyun içi zamanı yoksayar, gerçek hayattaki saniyeyi baz alır
                yield return new WaitForSecondsRealtime(waterInterval);
                WaterArea();
            }
        }

        private void WaterArea()
        {
            if (FarmGridManager.Instance == null) return;

            float gridSize = FarmGridManager.Instance.gridSize;

            int centerGridX = Mathf.RoundToInt(transform.position.x / gridSize);
            int centerGridZ = Mathf.RoundToInt(transform.position.z / gridSize);

            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    Vector2Int gridPos = new Vector2Int(centerGridX + x, centerGridZ + z);
                    FarmGridManager.Instance.WaterCell(gridPos);
                }
            }

            Debug.Log($"Sprinkler etrafını suladı! Menzil: {range}, Sonraki sulama {waterInterval} GERÇEK saniye sonra.");
        }

        public void Interact(GameObject interactor)
        {
            if (interactor.TryGetComponent<PlayerInteraction>(out PlayerInteraction playerInteractions))
            {
                if (playerInteractions.inventory.AddItem(sprinklerItemData, 1))
                {
                    if (waterCoroutine != null) StopCoroutine(waterCoroutine);
                    Destroy(gameObject);
                }
                else
                {
                    Debug.Log("Envanter dolu, Sprinkler yerden alınamadı.");
                }
            }
        }
    }
}