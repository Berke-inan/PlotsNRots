using System;
using System.Collections.Generic;
using UnityEngine;
using PlotNRots.SaveSystem; // Senin Kayýt Sistemin Eklendi

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    // YENÝ: Aletin kalan caný. (-1 ise bu eþyanýn can mekaniði yoktur, örn: Tohum, Yemek)
    public int currentDurability = -1;

    public bool IsEmpty => item == null || amount <= 0;
}

[RequireComponent(typeof(SaveableEntity))]
public class InventoryManager : MonoBehaviour, ISaveable // ISaveable Arayüzü Eklendi
{
    public int maxSlots = 8;
    public List<InventorySlot> slots = new List<InventorySlot>();

    public int activeSlotIndex = -1;

    public event Action OnInventoryChanged;
    public event Action<int> OnActiveItemChanged;

    // --- KAYIT SÝSTEMÝ ÝÇÝN VERÝ PAKETÝ ---
    [Serializable]
    public struct SlotSaveData
    {
        public string itemName; // ScriptableObject'in dosya adý
        public int amount;
        public int currentDurability;
    }

    [Serializable]
    public struct InventorySaveData
    {
        public List<SlotSaveData> savedSlots;
        public int savedActiveSlotIndex;
    }
    // --------------------------------------

    private void Awake()
    {
        for (int i = 0; i < maxSlots; i++)
        {
            slots.Add(new InventorySlot());
        }
    }

    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
        // 1. Stack (Birikme) Kontrolü
        foreach (var slot in slots)
        {
            if (!slot.IsEmpty && slot.item == itemToAdd && slot.amount < slot.item.maxStackSize)
            {
                slot.amount += amountToAdd;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        // 2. Boþ Yuva Bulma
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].item = itemToAdd;
                slots[i].amount = amountToAdd;
                slots[i].currentDurability = -1; // Yeni eklenen eþyanýn caný sýfýrlanýr (PlayerInteraction'da max can atanacak)

                if (activeSlotIndex == -1) SetActiveSlot(i);

                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        Debug.Log("Envanter Dolu!");
        return false;
    }

    public void RemoveActiveItem()
    {
        if (activeSlotIndex == -1 || slots[activeSlotIndex].IsEmpty) return;

        slots[activeSlotIndex].amount--;
        Debug.Log($"<color=orange>Eþya/Alet silindi!</color> Kalan miktar: {slots[activeSlotIndex].amount}");

        if (slots[activeSlotIndex].amount <= 0)
        {
            slots[activeSlotIndex].item = null;
            slots[activeSlotIndex].currentDurability = -1;
            SetActiveSlot(-1);
        }

        OnInventoryChanged?.Invoke();
    }

    public void SetActiveSlot(int index)
    {
        activeSlotIndex = index;
        OnActiveItemChanged?.Invoke(activeSlotIndex);
    }

    // ==========================================
    // ISAVEABLE IMPLEMENTASYONU (JSON KAYIT/YÜKLEME)
    // ==========================================

    // Oyun Kaydedilirken Çalýþýr
    public object SaveState()
    {
        InventorySaveData data = new InventorySaveData();
        data.savedSlots = new List<SlotSaveData>();

        foreach (var slot in slots)
        {
            if (slot.IsEmpty)
            {
                data.savedSlots.Add(new SlotSaveData { itemName = "", amount = 0, currentDurability = -1 });
            }
            else
            {
                // ScriptableObject'in kendisini kaydedemeyiz, bu yüzden ismini (ID'sini) kaydediyoruz.
                data.savedSlots.Add(new SlotSaveData
                {
                    itemName = slot.item.name,
                    amount = slot.amount,
                    currentDurability = slot.currentDurability
                });
            }
        }

        data.savedActiveSlotIndex = activeSlotIndex;
        return data;
    }

    // Oyun Yüklenirken Çalýþýr
    public void LoadState(object state)
    {
        var data = (InventorySaveData)state;

        for (int i = 0; i < maxSlots; i++)
        {
            if (i < data.savedSlots.Count)
            {
                var slotData = data.savedSlots[i];
                if (string.IsNullOrEmpty(slotData.itemName))
                {
                    slots[i].item = null;
                    slots[i].amount = 0;
                    slots[i].currentDurability = -1;
                }
                else
                {
                    // Ýsmi kaydedilen eþyayý Resources klasöründen bulup yüklüyoruz
                    ItemData loadedItem = LoadItemDataFromName(slotData.itemName);

                    slots[i].item = loadedItem;
                    slots[i].amount = slotData.amount;
                    slots[i].currentDurability = slotData.currentDurability;
                }
            }
        }

        SetActiveSlot(data.savedActiveSlotIndex);
        OnInventoryChanged?.Invoke();
    }

    // Yardýmcý Fonksiyon: Ýsmi bilinen ScriptableObject'i bulma
    private ItemData LoadItemDataFromName(string itemName)
    {
        // Önce doðrudan Resources/Items klasöründe var mý diye bakar (Hýzlý Yöntem)
        ItemData item = Resources.Load<ItemData>("Items/" + itemName);

        // Eðer bulamazsa Resources içindeki tüm ItemData'larý tarar (Garanti Yöntem)
        if (item == null)
        {
            ItemData[] allItems = Resources.LoadAll<ItemData>("");
            foreach (var i in allItems)
            {
                if (i.name == itemName) return i;
            }
        }
        return item;
    }
}