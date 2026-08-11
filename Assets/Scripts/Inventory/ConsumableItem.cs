using UnityEngine;

public enum ConsumableType
{
    Food,  // Maksimum enerjiyi artýrýr
    Drink  // Güncel enerjiyi artýrýr
}

[CreateAssetMenu(fileName = "New Consumable", menuName = "Inventory/Consumable Item")]
public class ConsumableItemData : ItemData
{
    [Header("Tüketim (Enerji) Ayarlarý")]
    public ConsumableType consumableType;

    [Tooltip("Ne kadar enerji vereceði (Food ise Max, Drink ise Güncel enerjiyi artýrýr)")]
    public float restoreAmount = 20f;
}