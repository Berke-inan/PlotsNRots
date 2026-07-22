using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemID;
    public string itemName;
    public Sprite uiIcon; // Dairesel menüde görünecek ikon
    public GameObject worldPrefab; // Yere atýldýðýnda çýkacak model
    public GameObject equipPrefab; // Ele alýndýðýnda ele takýlacak model (Bazen yerdekiyle ayný olur)
    public int maxStackSize = 1;
}