using UnityEngine;
using PlotNRots.Items;

namespace PlotNRots.Player
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Referanslar")]
        public InventoryManager inventory;
        public PlayerInputHandler inputHandler;
        public Transform handTransform;
        public Transform dropPoint;

        [Header("Ayarlar")]
        public float interactRange = 3f;

        private GameObject currentEquippedModel;
        private IUsable currentUsableItem;

        private void Start()
        {
            inventory.OnActiveItemChanged += EquipItem;

            inputHandler.OnInteract += TryPickUp;
            inputHandler.OnDrop += DropActiveItem;
            inputHandler.OnUse += UseActiveItem;
            inputHandler.OnScroll += ScrollInventory;
            inputHandler.OnEnterVehicle += TryEnterVehicle; // Dinleyici eklendi
        }

        private void OnDestroy()
        {
            inventory.OnActiveItemChanged -= EquipItem;
            inputHandler.OnInteract -= TryPickUp;
            inputHandler.OnDrop -= DropActiveItem;
            inputHandler.OnUse -= UseActiveItem;
            inputHandler.OnScroll -= ScrollInventory;
            inputHandler.OnEnterVehicle -= TryEnterVehicle;
        }

        private void UseActiveItem()
        {
            currentUsableItem?.Use();
        }

        private void EquipItem(int slotIndex)
        {
            if (currentEquippedModel != null)
            {
                Destroy(currentEquippedModel);
                currentUsableItem = null;
            }

            if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

            ItemData itemToEquip = inventory.slots[slotIndex].item;
            if (itemToEquip.equipPrefab != null)
            {
                currentEquippedModel = Instantiate(itemToEquip.equipPrefab, handTransform);
                currentEquippedModel.transform.localPosition = Vector3.zero;
                currentEquippedModel.transform.localRotation = Quaternion.identity;
                currentUsableItem = currentEquippedModel.GetComponent<IUsable>();
            }
        }

        private void TryPickUp()
        {
            Debug.Log("1 - TryPickUp tetiklendi! (E tuþu algýlandý)");

            Camera activeCamera = Camera.main;
            if (activeCamera == null) return;

            Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.red, 2f);

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                Debug.Log("2 - Iþýn bir þeye çarptý: " + hit.collider.gameObject.name);

                // DEÐÝÞÝKLÝK BURADA: Artýk alt modele çarpsa bile ana objedeki scripti bulacak
                InteractableItem itemOnGround = hit.collider.GetComponentInParent<InteractableItem>();

                if (itemOnGround != null)
                {
                    Debug.Log("3 - Obje alýnabilir! Envantere ekleniyor...");
                    if (inventory.AddItem(itemOnGround.itemData, itemOnGround.amount))
                    {
                        itemOnGround.PickUp();
                        Debug.Log("4 - Ýþlem Baþarýlý: Obje yerden silindi.");
                    }
                }
                else
                {
                    Debug.Log("HATA: Çarpýlan objede veya ebeveyninde InteractableItem kodu eksik!");
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
                // Araç betiðinin adýnýn projendeki ile ayný olduðundan emin ol
                VehicleInteractable vehicle = hit.collider.GetComponentInParent<VehicleInteractable>();
                if (vehicle != null)
                {
                    vehicle.EnterVehicle(this.gameObject);
                }
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