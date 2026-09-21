using UnityEngine;
using UnityEngine.InputSystem; // Yeni Input Sistemi

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public InventoryManager inventory;
    public Transform handTransform; // Eldeki eşyanın duracağı koordinat
    public Transform dropPoint; // Eşyanın yere atılacağı koordinat

    [Header("Ayarlar")]
    public float interactRange = 3f; // Etkileşim (Eşya alma / Arabaya binme) menzili

    private GameObject currentEquippedModel; // O an elde tutulan 3D model

    private void Start()
    {
        // Envanterden "Aktif eşya değişti" sinyali gelirse EquipItem fonksiyonunu çalıştır
        inventory.OnActiveItemChanged += EquipItem;
    }

    private void Update()
    {
        // Donanımların bağlı olup olmadığını kontrol et (Hata önleme)
        if (Keyboard.current == null || Mouse.current == null) return;

        // E Tuşu: Baktığın etkileşimli nesneyi kullan
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }

        // G Tuşu: Eldeki eşyayı yere at
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropActiveItem();
        }

        // F Tuşu: Arabaya Bin
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryEnterVehicle();
        }

        // Fare Tekerleği (Scroll) ile Envanterde Hızlı Geçiş
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (scrollValue != 0)
        {
            ScrollInventory(scrollValue);
        }
    }

    private void TryInteract()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return;

        // Işını ekranın tam ortasından gönder
        Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(gameObject);
                return;
            }

            // Baktığımız objede "InteractableItem" kodu var mı?
            InteractableItem itemOnGround = hit.collider.GetComponentInParent<InteractableItem>();
            if (itemOnGround != null)
            {
                if (inventory.AddItem(itemOnGround.itemData, itemOnGround.amount))
                {
                    itemOnGround.PickUp();
                }
            }
        }
    }

    private void DropActiveItem()
    {
        // Elde bir şey yoksa işlemi iptal et
        if (inventory.activeSlotIndex == -1 || inventory.slots[inventory.activeSlotIndex].IsEmpty) return;

        ItemData itemToDrop = inventory.slots[inventory.activeSlotIndex].item;

        // Eşyayı DropPoint noktasında oluştur
        GameObject droppedItem = Instantiate(itemToDrop.worldPrefab, dropPoint.position, dropPoint.rotation);

        // Eşyayı ileriye doğru fırlat
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Camera activeCamera = Camera.main;
            Vector3 throwDirection = activeCamera != null ? activeCamera.transform.forward : transform.forward;
            rb.AddForce(throwDirection * 5f, ForceMode.Impulse);
        }

        // Envanterden sil
        inventory.RemoveActiveItem();
    }

    private void TryEnterVehicle()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return;

        Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // Baktığımız objede veya onun ebeveyn (parent) objesinde VehicleInteractable kodu var mı?
            VehicleInteractable vehicle = hit.collider.GetComponentInParent<VehicleInteractable>();
            if (vehicle != null)
            {
                // Varsa arabaya binme komutunu gönder ve kendini parametre olarak yolla
                vehicle.EnterVehicle(this.gameObject);
            }
        }
    }

    private void ScrollInventory(float scrollValue)
    {
        int currentIndex = inventory.activeSlotIndex;

        // Eğer elde eşya yoksa baştan başla
        if (currentIndex == -1) currentIndex = 0;

        if (scrollValue > 0)
        {
            currentIndex++;
            if (currentIndex >= inventory.maxSlots) currentIndex = 0; // Başa dön
        }
        else if (scrollValue < 0)
        {
            currentIndex--;
            if (currentIndex < 0) currentIndex = inventory.maxSlots - 1; // Sona dön
        }

        inventory.SetActiveSlot(currentIndex);
    }

    private void EquipItem(int slotIndex)
    {
        // 1. Elde önceki eşya varsa onu sil
        if (currentEquippedModel != null)
        {
            Destroy(currentEquippedModel);
        }

        // 2. Eğer slot boşsa veya -1 komutu geldiyse eli boş bırak
        if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

        // 3. Yeni eşyayı ele instantiate et
        ItemData itemToEquip = inventory.slots[slotIndex].item;
        if (itemToEquip.equipPrefab != null)
        {
            currentEquippedModel = Instantiate(itemToEquip.equipPrefab, handTransform);
            currentEquippedModel.transform.localPosition = Vector3.zero;
            currentEquippedModel.transform.localRotation = Quaternion.identity;
        }
    }
}
