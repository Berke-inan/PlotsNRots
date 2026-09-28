using UnityEngine;
using PlotNRots.Player;
using PlotNRots.UI;

namespace PlotNRots.Items
{
    public class ChestDrop : MonoBehaviour, IInteractable
    {
        [Header("Sandık Ayarları")]
        public int chestCapacity = 16;
        public ItemData chestItemData;

        private ChestUIManager uiManager;

        private void Awake()
        {
            // Prefab'ın alt nesnelerinden (Child) ChestUIManager'ı otomatik olarak buluyoruz
            uiManager = GetComponentInChildren<ChestUIManager>(true);

            if (uiManager == null)
            {
                Debug.LogError($"{gameObject.name} prefabının altında ChestUIManager bileşeni bulunamadı!");
            }
        }

        // F tuşuna basıldığında tetiklenir
        public void Interact(GameObject interactor)
        {
            Debug.Log("Sandığa F tuşu ile basıldı!");

            if (uiManager != null)
            {
                uiManager.OpenChest(this);
            }
            else
            {
                Debug.LogError("ChestUIManager referansı boş!");
            }
        }
    }
}