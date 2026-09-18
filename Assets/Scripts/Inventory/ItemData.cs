using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Temel Bilgiler")]
    public string itemID;
    public string itemName;
    public Sprite uiIcon; // Dairesel menüde görünecek ikon
    public int maxStackSize = 1;

    [Header("Prefab Referanslarý")]
    public GameObject worldPrefab; // Yere atýldýðýnda çýkacak model
    public GameObject equipPrefab; // Ele alýndýðýnda ele takýlacak model

    [Header("Elde Tutma Konum Ayarlarý (Offset)")]
    [Tooltip("Eþyanýn el transformuna göre yerel (local) pozisyonu")]
    public Vector3 equipPosition = Vector3.zero;

    [Tooltip("Eþyanýn el transformuna göre yerel (local) dönüþ açýsý (Euler)")]
    public Vector3 equipRotation = Vector3.zero;
}