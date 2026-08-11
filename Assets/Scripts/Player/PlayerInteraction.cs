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
    private PlayerStats playerStats; // YENÝ EKLENDÝ: Enerji sistemi için referans

    private void Start()
    {
        // PlayerStats bileþenini otomatik bul (Ayný obje üzerinde olduðunu varsayýyoruz)
        playerStats = GetComponent<PlayerStats>();

        // Envanterden "Aktif eþya deðiþti" sinyali gelirse EquipItem fonksiyonunu çalýþtýr
        inventory.OnActiveItemChanged += EquipItem;
    }

    private void Update()
    {
        // Donanýmlarýn baðlý olup olmadýðýný kontrol et (Hata önleme)
        if (Keyboard.current == null || Mouse.current == null) return;

        // YENÝ EKLENDÝ - Sol Týk: Eldeki eþyayý kullan/tüket
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryUseActiveItem();
        }

        // E Tuþu: Etkileþim (Yerdeki eþyayý al VEYA Yataða yat)
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract(); // GÜNCELLENDÝ: TryPickUp yerine ortak etkileþim fonksiyonu çaðrýldý
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

        // CANLI POZÝSYON GÜNCELLEMESÝ (SADECE UNITY EDÝTÖRÜNDE ÇALIÞIR)
#if UNITY_EDITOR
        LiveUpdateItemOffset();
#endif
    }

    // ==========================================
    // YENÝ EKLENEN KISIM: EÞYA KULLANMA (TÜKETÝM)
    // ==========================================
    private void TryUseActiveItem()
    {
        if (inventory.activeSlotIndex == -1) return;

        InventorySlot activeSlot = inventory.slots[inventory.activeSlotIndex];
        if (activeSlot.IsEmpty || activeSlot.item == null) return;

        // Eldeki eþya yenilebilir/içilebilir bir "ConsumableItemData" mý?
        if (activeSlot.item is ConsumableItemData consumable)
        {
            if (playerStats != null)
            {
                // Enerji/Can deðerlerini artýr
                playerStats.ConsumeItem(consumable);

                // Tüketildiði için envanterden 1 adet düþ
                inventory.RemoveActiveItem();
            }
        }
        else
        {
            // Ýleride balta, çapa gibi aletlerin kullaným mekanikleri buraya eklenebilir.
            Debug.Log("Bu eþya tüketilebilir bir þey deðil.");
        }
    }

    // ==========================================
    // GÜNCELLENEN KISIM: ORTAK ETKÝLEÞÝM
    // ==========================================
    private void TryInteract()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return;

        Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // DURUM 1: Baktýðýmýz þey yerdeki bir eþya mý?
            InteractableItem itemOnGround = hit.collider.GetComponent<InteractableItem>();
            if (itemOnGround != null)
            {
                if (inventory.AddItem(itemOnGround.itemData, itemOnGround.amount))
                {
                    itemOnGround.PickUp();
                    return; // Eþyayý aldýysak fonksiyondan çýk
                }
            }

            // DURUM 2: Baktýðýmýz þey bir Yatak mý? (YENÝ EKLENDÝ)
            BedInteractable bed = hit.collider.GetComponentInParent<BedInteractable>();
            if (bed != null)
            {
                // Yataða kendi oyuncu objemizi göndererek uyku dizisini baþlatýyoruz
                bed.InteractWithBed(this.gameObject);
                return;
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

    private void EquipItem(int slotIndex)
    {
        // 1. Önceki modeli yok et
        if (currentEquippedModel != null)
        {
            Destroy(currentEquippedModel);
        }

        // 2. Eðer slot boþsa veya hata varsa çýk
        if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

        // 3. Yeni modeli oluþtur
        ItemData itemToEquip = inventory.slots[slotIndex].item;

        // HATA ÖNLEME: Eðer itemToEquip veya equipPrefab null ise çökmeyi engelle
        if (itemToEquip != null && itemToEquip.equipPrefab != null)
        {
            currentEquippedModel = Instantiate(itemToEquip.equipPrefab, handTransform);

            currentEquippedModel.transform.localPosition = itemToEquip.equipPosition;
            currentEquippedModel.transform.localEulerAngles = itemToEquip.equipRotation;
        }
    }

#if UNITY_EDITOR
    private void LiveUpdateItemOffset()
    {
        // Eðer elde bir model varsa ve þu an aktif bir slot seçiliyse
        if (currentEquippedModel != null && inventory.activeSlotIndex >= 0 && inventory.activeSlotIndex < inventory.slots.Count)
        {
            InventorySlot currentSlot = inventory.slots[inventory.activeSlotIndex];

            // Eðer slot boþ deðilse, ItemData içindeki pozisyon deðerlerini anlýk olarak modele uygula
            if (!currentSlot.IsEmpty && currentSlot.item != null)
            {
                currentEquippedModel.transform.localPosition = currentSlot.item.equipPosition;
                currentEquippedModel.transform.localEulerAngles = currentSlot.item.equipRotation;
            }
        }
    }
#endif
}