using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleInteractable : MonoBehaviour
{
    [Header("Referanslar")]
    public VehicleController vehicleController;
    public GameObject vehicleCameraObj;

    [Header("Ayarlar")]
    public float exitOffset = 1.8f;
    public float searchRadius = 0.5f;

    private GameObject activePlayer;
    private Collider[] vehicleColliders;
    private float enterTime; // F tuþu çakýþmasýný önleyen zamanlayýcý

    private void Start()
    {
        vehicleColliders = GetComponentsInChildren<Collider>();
    }

    private void Update()
    {
        // Oyuncu arabaya bindikten sonra en az 0.2 saniye geçmesini bekler (Çakýþmayý önler)
        if (vehicleController.isPlayerInside && activePlayer != null && Time.time > enterTime + 0.2f)
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                ExitVehicle();
            }
        }
    }

    public void EnterVehicle(GameObject player)
    {
        enterTime = Time.time; // Arabaya binilen aný kaydet
        activePlayer = player;
        activePlayer.SetActive(false);
        vehicleController.isPlayerInside = true;
        vehicleCameraObj.SetActive(true);

        // --- YENÝ EKLENEN ---
        // UI'a "Bu aracý göstermeye baþla" diyoruz
        if (VehicleUI.Instance != null)
        {
            VehicleUI.Instance.AraciDegistir(vehicleController);
        }
    }

    public void ExitVehicle()
    {
        vehicleController.isPlayerInside = false;
        vehicleCameraObj.SetActive(false);

        // --- YENÝ EKLENEN ---
        // UI'a "Araçtan indik, ekraný kapat" diyoruz
        if (VehicleUI.Instance != null)
        {
            VehicleUI.Instance.AraciDegistir(null);
        }

        activePlayer.transform.position = FindSafeExitPosition();
        activePlayer.SetActive(true);
    }

    private Vector3 FindSafeExitPosition()
    {
        Vector3[] directions = { transform.right, -transform.right, -transform.forward };
        foreach (Vector3 dir in directions)
        {
            Vector3 candidatePos = transform.position + (dir * exitOffset);
            if (IsPositionValid(candidatePos)) return candidatePos;
        }
        return transform.position + (Vector3.up * 2f);
    }

    private bool IsPositionValid(Vector3 pos)
    {
        Collider[] hits = Physics.OverlapSphere(pos + Vector3.up, searchRadius);
        foreach (var hit in hits) if (!IsOwnCollider(hit)) return false;

        if (Physics.Raycast(pos + (Vector3.up * 1f), Vector3.down, 2f)) return true;
        return false;
    }

    private bool IsOwnCollider(Collider hit)
    {
        foreach (var c in vehicleColliders) if (hit == c) return true;
        return false;
    }
}