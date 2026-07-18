using UnityEngine;
using UnityEngine.InputSystem; // YENÝ INPUT SÝSTEMÝ EKLENDÝ

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public InventoryManager inventory;
    public Transform handTransform;
    public Transform dropPoint;

    [Header("Ayarlar")]
    public float interactRange = 3f;

    private GameObject currentEquippedModel;

    private void Start()
    {
        inventory.OnActiveItemChanged += EquipItem;
    }

    private void Update()
    {
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

        // Fare Tekerleði (Scroll) ile Envanterde Hýzlý Geçiþ
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (scrollValue != 0)
        {
            ScrollInventory(scrollValue);
        }
    }

    // Tekerlek kaydýrýldýðýnda çalýþacak yeni fonksiyon
    private void ScrollInventory(float scrollValue)
    {
        int currentIndex = inventory.activeSlotIndex;

        // Eðer elinde hiçbir þey yoksa varsayýlan olarak 0'dan (ilk yuvadan) baþla
        if (currentIndex == -1) currentIndex = 0;

        if (scrollValue > 0) // Tekerlek ileri/yukarý kaydýrýldýysa (Sonraki eþya)
        {
            currentIndex++;
            // Envanterin sonuna geldiyse tekrar baþa dön (Döngüsel)
            if (currentIndex >= inventory.maxSlots) currentIndex = 0;
        }
        else if (scrollValue < 0) // Tekerlek geri/aþaðý kaydýrýldýysa (Önceki eþya)
        {
            currentIndex--;
            // Envanterin baþýndayken geriye kaydýrýrsa en sona git (Döngüsel)
            if (currentIndex < 0) currentIndex = inventory.maxSlots - 1;
        }

        // Yeni hesaplanan yuvayý aktif et
        inventory.SetActiveSlot(currentIndex);
    }

    private void TryPickUp()
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
                }
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

    private void EquipItem(int slotIndex)
    {
        if (currentEquippedModel != null)
        {
            Destroy(currentEquippedModel);
        }

        if (slotIndex == -1 || inventory.slots[slotIndex].IsEmpty) return;

        ItemData itemToEquip = inventory.slots[slotIndex].item;
        if (itemToEquip.equipPrefab != null)
        {
            currentEquippedModel = Instantiate(itemToEquip.equipPrefab, handTransform);
            currentEquippedModel.transform.localPosition = Vector3.zero;
            currentEquippedModel.transform.localRotation = Quaternion.identity;
        }
    }
}