using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;
    // Ýleride buraya 'float currentDurability' ekleyip silah kýrýlmalarýný yapabilirsin.

    public bool IsEmpty => item == null || amount <= 0;
}

public class InventoryManager : MonoBehaviour
{
    public int maxSlots = 8; // GTA/RDR tarzý dairesel menü için genelde 8 idealdir.
    public List<InventorySlot> slots = new List<InventorySlot>();

    public int activeSlotIndex = -1; // O an elde tutulan eþyanýn indeksi

    // UI ve Player'ýn haberdar olmasý için Event'ler (Sinyaller)
    public event Action OnInventoryChanged;
    public event Action<int> OnActiveItemChanged;

    private void Awake()
    {
        // Baþlangýçta boþ yuvalarý oluþtur
        for (int i = 0; i < maxSlots; i++)
        {
            slots.Add(new InventorySlot());
        }
    }

    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
        // 1. Önce ayný eþyadan var mý ve birikebiliyor mu (Stack) kontrol et
        foreach (var slot in slots)
        {
            if (!slot.IsEmpty && slot.item == itemToAdd && slot.amount < slot.item.maxStackSize)
            {
                slot.amount += amountToAdd;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        // 2. Yoksa boþ bir yuva bul
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].item = itemToAdd;
                slots[i].amount = amountToAdd;

                // Eðer elinde hiçbir þey yoksa ve ilk eþyayý aldýysa, direkt eline al
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
        if (slots[activeSlotIndex].amount <= 0)
        {
            slots[activeSlotIndex].item = null;
            SetActiveSlot(-1); // Eþya bittiyse eli boþalt
        }

        OnInventoryChanged?.Invoke();
    }

    public void SetActiveSlot(int index)
    {
        activeSlotIndex = index;
        OnActiveItemChanged?.Invoke(activeSlotIndex); // Player'a "yeni modeli eline al" sinyali gönder
    }
}