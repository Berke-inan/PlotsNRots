using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public InventoryManager inventory;
    public Transform handTransform;
    public Transform dropPoint;

    [Header("Ayarlar")]
    public float interactRange = 3f;

    private GameObject currentEquippedModel;
    private PlayerStats playerStats;

    private void Start()
    {
        playerStats = GetComponent<PlayerStats>();
        inventory.OnActiveItemChanged += EquipItem;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // Sol Týk: Eldeki eþyayý kullan / Aletle Vur
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryUseActiveItem();
        }

        // E Tuþu: Etkileþim 
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }

        // G Tuþu: Yere At
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropActiveItem();
        }

        // F Tuþu: Arabaya Bin
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryEnterVehicle();
        }

        // Fare Tekerleði: Envanterde Gezinme
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (scrollValue != 0)
        {
            ScrollInventory(scrollValue);
        }

#if UNITY_EDITOR
        LiveUpdateItemOffset();
#endif
    }

    // ==========================================
    // EÞYA KULLANMA VE ALET KIRILMA SÝSTEMÝ
    // ==========================================
    private void TryUseActiveItem()
    {
        if (inventory.activeSlotIndex == -1) return;

        // activeSlot eðer bir 'struct' ise deðerini doðrudan deðiþtiremeyiz, 
        // bu yüzden listeye doðrudan referansla eriþmek (inventory.slots[...]) daha güvenlidir.
        var slot = inventory.slots[inventory.activeSlotIndex];
        if (slot.IsEmpty || slot.item == null) return;

        // DURUM 1: TÜKETÝLEBÝLÝR (Yemek/Ýçecek)
        if (slot.item is ConsumableItemData consumable)
        {
            if (playerStats != null)
            {
                playerStats.ConsumeItem(consumable);
                inventory.RemoveActiveItem();
            }
        }
        // DURUM 2: EL ALETÝ (Çapa, Ýngiliz Anahtarý vb.)
        else if (slot.item is ToolItemData tool)
        {
            // Wrench (Anahtar) ise sol týk basýlý tutma mekaniðini WrenchController.cs yönetir, buradan çýk!
            if (tool.isWrench) return;

            // Alet ilk kez kullanýlýyorsa, canýný (durability) þablondan (ToolItemData) al
            if (inventory.slots[inventory.activeSlotIndex].currentDurability == -1)
            {
                inventory.slots[inventory.activeSlotIndex].currentDurability = tool.maxDurability;
            }

            // Vuruþ yapýldý, aletin canýný 1 düþür
            inventory.slots[inventory.activeSlotIndex].currentDurability--;

            // Vuruþ Sesi Çal
            if (tool.useSound != null)
            {
                AudioSource.PlayClipAtPoint(tool.useSound, transform.position);
            }

            int kalanCan = inventory.slots[inventory.activeSlotIndex].currentDurability;
            Debug.Log($"Alet kullanýldý! Kalan Can: {kalanCan} / {tool.maxDurability}");

            // Alet Kýrýlma Kontrolü
            if (kalanCan <= 0)
            {
                BreakEquippedTool(tool);
            }
        }
        else
        {
            Debug.Log("Bu eþya tüketilebilir veya kullanýlabilir bir alet deðil.");
        }
    }

    // ALET KIRILDIÐINDA ÇALIÞACAK FONKSÝYON
    private void BreakEquippedTool(ToolItemData tool)
    {
        if (tool.breakSound != null)
        {
            AudioSource.PlayClipAtPoint(tool.breakSound, transform.position);
        }

        if (currentEquippedModel != null)
        {
            Destroy(currentEquippedModel);
        }

        // Aleti envanterden tamamen sil
        inventory.RemoveActiveItem();
        Debug.Log($"<color=red>{tool.itemName} parçalandý!</color>");
    }

    // ==========================================
    // ORTAK ETKÝLEÞÝM
    // ==========================================
    private void TryInteract()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return;

        Ray ray = activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            InteractableItem itemOnGround = hit.collider.GetComponent<InteractableItem>();
            if (itemOnGround != null)
            {
                if (inventory.AddItem(itemOnGround.itemData, itemOnGround.amount))
                {
                    itemOnGround.PickUp();
                    return;
                }
            }

            BedInteractable bed = hit.collider.GetComponentInParent<BedInteractable>();
            if (bed != null)
            {
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
        if (currentEquippedModel != null)
        {
            Destroy(currentEquippedModel);
        }

        if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

        ItemData itemToEquip = inventory.slots[slotIndex].item;

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
        // YENÝ EKLENEN SATIR: Eðer oyuncu sol týka basýyorsa (tamir yapýyorsa) açýyý zorlamayý býrak, animasyona izin ver!
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;

        if (currentEquippedModel != null && inventory.activeSlotIndex >= 0 && inventory.activeSlotIndex < inventory.slots.Count)
        {
            var currentSlot = inventory.slots[inventory.activeSlotIndex];
            if (!currentSlot.IsEmpty && currentSlot.item != null)
            {
                currentEquippedModel.transform.localPosition = currentSlot.item.equipPosition;
                currentEquippedModel.transform.localEulerAngles = currentSlot.item.equipRotation;
            }
        }
    }
#endif
}