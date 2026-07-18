using UnityEngine;

// Bu script yerdeki kürek, tohum veya silahýn üzerinde duracak.
public class InteractableItem : MonoBehaviour
{
    public ItemData itemData;
    public int amount = 1;

    // E tuþuna basýldýðýnda Player bu fonksiyonu tetikleyecek
    public void PickUp()
    {
        // Obje dünyadan siliniyor (Envantere ekleme iþlemi Player kodunda yapýlacak)
        Destroy(gameObject);
    }
}