using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class WaterVolume : MonoBehaviour
{
    [Header("Su Yüzeyi")]
    [Tooltip("Görünen su Plane objesini buraya býrak.")]
    [SerializeField] private Transform waterSurface;

    // Oyuncunun birden fazla collider'ý varsa
    // giriþ-çýkýþ mesajlarýnýn karýþmasýný engeller.
    private readonly Dictionary<Transform, int> playerColliderCounts = new();

    public float SurfaceHeight
    {
        get
        {
            if (waterSurface != null)
                return waterSurface.position.y;

            return transform.position.y;
        }
    }

    private void Awake()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform player = FindPlayer(other.transform);

        if (player == null)
        {
            Debug.Log("Su alanýna baþka bir obje girdi: " + other.name);
            return;
        }

        if (!playerColliderCounts.ContainsKey(player))
            playerColliderCounts[player] = 0;

        playerColliderCounts[player]++;

        // Ýlk collider suya girdiðinde çalýþýr.
        if (playerColliderCounts[player] == 1)
        {
            Debug.Log("Oyuncu suya girdi.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Transform player = FindPlayer(other.transform);

        if (player == null)
            return;

        if (!playerColliderCounts.ContainsKey(player))
            return;

        playerColliderCounts[player]--;

        // Oyuncunun bütün collider'larý sudan çýktýðýnda çalýþýr.
        if (playerColliderCounts[player] <= 0)
        {
            playerColliderCounts.Remove(player);
            Debug.Log("Oyuncu sudan çýktý.");
        }
    }

    private static Transform FindPlayer(Transform enteredObject)
    {
        Transform current = enteredObject;

        // Player etiketi alt objede veya ana objede olabilir.
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return current;

            current = current.parent;
        }

        return null;
    }

    private void OnValidate()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();

        if (boxCollider != null)
            boxCollider.isTrigger = true;
    }
}