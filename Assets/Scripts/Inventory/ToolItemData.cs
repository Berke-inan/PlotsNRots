using UnityEngine;

[CreateAssetMenu(fileName = "NewToolData", menuName = "Inventory/Tool Item Data")]
public class ToolItemData : ItemData // Mevcut ItemData'dan miras alýr
{
    [Header("Alet Dayanýklýlýk Ayarlarý")]
    public int maxDurability = 100;
    public bool isWrench = false; // Bu bir Ýngiliz Anahtarý mý?

    [Header("Alet Sesleri")]
    public AudioClip useSound;
    public AudioClip breakSound;
}