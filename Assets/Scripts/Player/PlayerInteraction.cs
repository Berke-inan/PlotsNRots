using UnityEngine;
using UnityEngine.InputSystem; // Yeni Input Sistemi

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public InventoryManager inventory;
    public Transform handTransform; // Eldeki eþyanýn duracaðý koordinat
    public Transform dropPoint; // Eþyanýn yere atýlacaðý koordinat

    [Header("Ayarlar")]
    public float interactRange = 3f; // Etkileþim (Eþya alma / Arabaya binme) menzili

    private GameObject currentEquippedModel; // O an elde tutulan 3D model

    private void Start()
    {
        // Envanterden "Aktif eþya deðiþti" sinyali gelirse EquipItem fonksiyonunu çalýþtýr
        inventory.OnActiveItemChanged += EquipItem;
    }

    private void Update()
    {
        // Donanýmlarýn baðlý olup olmadýðýný kontrol et (Hata önleme)
        if (Keyboard.current == null || Mouse.current == null) return;

        // E Tuþu: Yerdeki eþyayý al
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryPickUp();
        }

        // G Tuþu: Eldeki eþyayý yere at
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropActiveItem();
        }

        // F Tuþu: Arabaya Bin
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryEnterVehicle();
        }

        // Fare Tekerleði (Scroll) ile Envanterde Hýzlý Geçiþ
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (scrollValue != 0)
        {
            ScrollInventory(scrollValue);
        }
    }

    private void TryPickUp()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return;

        // Iþýný ekranýn tam ortasýndan gönder
        Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // Baktýðýmýz objede "InteractableItem" kodu var mý?
            InteractableItem itemOnGround = hit.collider.GetComponent<InteractableItem>();
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
        // Elde bir þey yoksa iþlemi iptal et
        if (inventory.activeSlotIndex == -1 || inventory.slots[inventory.activeSlotIndex].IsEmpty) return;

        ItemData itemToDrop = inventory.slots[inventory.activeSlotIndex].item;

        // Eþyayý DropPoint noktasýnda oluþtur
        GameObject droppedItem = Instantiate(itemToDrop.worldPrefab, dropPoint.position, dropPoint.rotation);

        // Eþyayý ileriye doðru fýrlat
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
            // Baktýðýmýz objede veya onun ebeveyn (parent) objesinde VehicleInteractable kodu var mý?
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

        // Eðer elde eþya yoksa baþtan baþla
        if (currentIndex == -1) currentIndex = 0;

        if (scrollValue > 0)
        {
            currentIndex++;
            if (currentIndex >= inventory.maxSlots) currentIndex = 0; // Baþa dön
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
        // 1. Elde önceki eþya varsa onu sil
        if (currentEquippedModel != null)
        {
            Destroy(currentEquippedModel);
        }

        // 2. Eðer slot boþsa veya -1 komutu geldiyse eli boþ býrak
        if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

        // 3. Yeni eþyayý ele instantiate et
        ItemData itemToEquip = inventory.slots[slotIndex].item;
        if (itemToEquip.equipPrefab != null)
        {
            currentEquippedModel = Instantiate(itemToEquip.equipPrefab, handTransform);
            currentEquippedModel.transform.localPosition = Vector3.zero;
            currentEquippedModel.transform.localRotation = Quaternion.identity;
        }
    }
}