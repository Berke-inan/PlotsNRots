using System;
using UnityEngine;
using UnityEngine.InputSystem;
using PlotNRots.Items;
using PlotNRots.Managers;

namespace PlotNRots.Player
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Referanslar")]
        public InventoryManager inventory;
        public PlayerInputHandler inputHandler;
        public Transform handTransform;
        public Transform dropPoint;

        [Header("Etkileþim Ayarlarý")]
        public float interactRange = 3f;

        [Header("Yerleþtirme (Placement) Ayarlarý")]
        public LayerMask terrainLayer; // Hologramýn sadece topraða çýkmasý için
        public float placementRange = 10f; // Yerleþtirme menzili

        private GameObject currentEquippedModel;
        private IUsable currentUsableItem;

        // --- Yerleþtirme (IPlaceable) Ýçin Eklenenler ---
        private IPlaceable currentPlaceableItem;
        private GameObject currentPreview;

        private void Start()
        {
            inventory.OnActiveItemChanged += EquipItem;

            inputHandler.OnInteract += TryPickUp;
            inputHandler.OnDrop += DropActiveItem;
            inputHandler.OnUse += UseActiveItem;
            inputHandler.OnScroll += ScrollInventory;
            inputHandler.OnEnterVehicle += TryEnterVehicle;
            inputHandler.OnSlotSelect += SelectDirectSlot;
        }

        private void OnDestroy()
        {
            inventory.OnActiveItemChanged -= EquipItem;
            inputHandler.OnInteract -= TryPickUp;
            inputHandler.OnDrop -= DropActiveItem;
            inputHandler.OnUse -= UseActiveItem;
            inputHandler.OnScroll -= ScrollInventory;
            inputHandler.OnEnterVehicle -= TryEnterVehicle;
            inputHandler.OnSlotSelect -= SelectDirectSlot;

            ClearPreview();
        }

        // --- Hologramýn Pozisyonunu Güncelleyen Update Fonksiyonu ---
        private void Update()
        {
            if (currentPlaceableItem != null && currentPreview != null)
            {
                UpdatePreviewPosition();
            }
        }

        private void UpdatePreviewPosition()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null) return;

            Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, placementRange, terrainLayer))
            {
                currentPreview.SetActive(true);

                // FarmGridManager kullanýlarak her objenin gride oturmasý saðlanýr
                float gridSize = FarmGridManager.Instance.gridSize;
                int gridX = Mathf.RoundToInt(hit.point.x / gridSize);
                int gridZ = Mathf.RoundToInt(hit.point.z / gridSize);

                Vector3 snapPos = new Vector3(gridX * gridSize, hit.point.y, gridZ * gridSize);
                currentPreview.transform.position = snapPos;
            }
            else
            {
                // Topraða bakmýyorsak veya uzaktaysak hologramý gizle
                currentPreview.SetActive(false);
            }
        }

        private void UseActiveItem()
        {
            // 1. DURUM: Eþya IUsable ise (Çapa vb.) Sol týka basýnca kullanýr
            if (currentUsableItem != null)
            {
                currentUsableItem.Use();
            }
            // 2. DURUM: Eþya IPlaceable ise (Sprinkler vb.) Sol týka basýnca yerleþtirir
            else if (currentPlaceableItem != null && currentPreview != null && currentPreview.activeInHierarchy)
            {
                GameObject realObject = Instantiate(currentPlaceableItem.GetRealPrefab(), currentPreview.transform.position, currentPlaceableItem.GetRealPrefab().transform.rotation);
                currentPlaceableItem.OnPlaced(realObject);

                // Yere yerleþtirdiðimiz için envanterden eksilt
                inventory.RemoveActiveItem();
            }
        }

        private void EquipItem(int slotIndex)
        {
            if (currentEquippedModel != null)
            {
                Destroy(currentEquippedModel);
                currentUsableItem = null;
                currentPlaceableItem = null;
                ClearPreview(); // Farklý bir eþyaya geçildiðinde eski hologramý sil
            }

            if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

            ItemData itemToEquip = inventory.slots[slotIndex].item;
            if (itemToEquip.equipPrefab != null)
            {
                currentEquippedModel = Instantiate(itemToEquip.equipPrefab, handTransform);
                currentEquippedModel.transform.localPosition = Vector3.zero;
                currentEquippedModel.transform.localRotation = Quaternion.identity;

                // Obje IUsable mý kontrol et
                currentUsableItem = currentEquippedModel.GetComponent<IUsable>();

                // Obje IPlaceable mý kontrol et, öyleyse hologramý sahnede oluþtur
                currentPlaceableItem = currentEquippedModel.GetComponent<IPlaceable>();
                if (currentPlaceableItem != null)
                {
                    GameObject previewPrefab = currentPlaceableItem.GetPreviewPrefab();
                    if (previewPrefab != null)
                    {
                        currentPreview = Instantiate(previewPrefab);
                    }
                }
            }
        }

        private void ClearPreview()
        {
            if (currentPreview != null)
            {
                Destroy(currentPreview);
                currentPreview = null;
            }
        }

        private void TryPickUp()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null) return;

            Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                InteractableItem itemOnGround = hit.collider.GetComponentInParent<InteractableItem>();
                if (itemOnGround != null)
                {
                    if (inventory.AddItem(itemOnGround.itemData, itemOnGround.amount))
                    {
                        itemOnGround.PickUp();
                    }
                    return;
                }

                IInteractable interactableObj = hit.collider.GetComponentInParent<IInteractable>();
                if (interactableObj != null)
                {
                    interactableObj.Interact(this.gameObject);
                }
            }
        }

        private void TryEnterVehicle()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null) return;

            Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                VehicleInteractable vehicle = hit.collider.GetComponentInParent<VehicleInteractable>();
                if (vehicle != null)
                {
                    vehicle.EnterVehicle(this.gameObject);
                }
            }
        }

        private void SelectDirectSlot(int index)
        {
            if (index >= 0 && index < inventory.maxSlots)
            {
                inventory.SetActiveSlot(index);
            }
        }

        private void DropActiveItem()
        {
            if (inventory.activeSlotIndex == -1 || inventory.slots[inventory.activeSlotIndex].IsEmpty) return;

            ItemData itemToDrop = inventory.slots[inventory.activeSlotIndex].item;
            GameObject droppedItem = Instantiate(itemToDrop.worldPrefab, dropPoint.position, dropPoint.rotation);

            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Camera activeCamera = Camera.main;
                Vector3 throwDirection = activeCamera != null ? activeCamera.transform.forward : transform.forward;
                rb.AddForce(throwDirection * 5f, ForceMode.Impulse);
            }

            inventory.RemoveActiveItem();
        }

        private void ScrollInventory(float scrollValue)
        {
            int currentIndex = inventory.activeSlotIndex;
            if (currentIndex == -1) currentIndex = 0;

            if (scrollValue > 0)
            {
                currentIndex++;
                if (currentIndex >= inventory.maxSlots) currentIndex = 0;
            }
            else if (scrollValue < 0)
            {
                currentIndex--;
                if (currentIndex < 0) currentIndex = inventory.maxSlots - 1;
            }

            inventory.SetActiveSlot(currentIndex);
        }
    }
}