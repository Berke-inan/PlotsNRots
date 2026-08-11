using UnityEngine;
using UnityEngine.InputSystem;
using PlotNRots.Player;

public class VehicleInteractable : MonoBehaviour
{
    [Header("Referanslar")]
    public VehicleController vehicleController;

    [Header("Modüler Kamera Hedefleri")]
    [Tooltip("Dış Kameranın (TPS) takip edeceği merkez nokta")]
    public Transform tpsTarget;
    [Tooltip("İç Kameranın (FPS) oturacağı şoför kafa noktası")]
    public Transform fpsTarget;

    [Header("Ayarlar")]
    public float exitOffset = 1.8f;
    public float searchRadius = 0.5f;

    private GameObject activePlayer;
    private CameraManager playerCameraManager;
    private Collider[] vehicleColliders;
    private float enterTime;
    private bool isFpsActive = false;

    // Egzoz sistemini kontrol etmek için referans ekledik
    private TractorExhaust exhaustSystem;

    private void Start()
    {
        vehicleColliders = GetComponentsInChildren<Collider>();
        // Traktör üzerindeki egzoz scriptini otomatik buluyoruz
        exhaustSystem = GetComponent<TractorExhaust>();
    }

    private void Update()
    {
        if (vehicleController.isPlayerInside && activePlayer != null && Time.time > enterTime + 0.2f)
        {
            if (Keyboard.current == null) return;

            // Kamera Geçişi (V)
            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                isFpsActive = !isFpsActive;
                if (VehicleCameraManager.Instance != null)
                {
                    VehicleCameraManager.Instance.SwitchCamera(isFpsActive);
                }
            }

            // Araçtan İnme (F)
            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                ExitVehicle();
            }
        }
    }

    public void EnterVehicle(GameObject player)
    {
        enterTime = Time.time;
        activePlayer = player;

        // 1. Oyuncunun o anki kamerasını (FPS mi TPS mi) öğren
        playerCameraManager = activePlayer.GetComponent<CameraManager>();
        if (playerCameraManager != null) isFpsActive = playerCameraManager.IsFPS;

        // 2. Merkezi Kamera Sistemine hedefleri gönder ve kamerayı aç
        if (VehicleCameraManager.Instance != null)
        {
            VehicleCameraManager.Instance.SetTargets(tpsTarget, fpsTarget);
            VehicleCameraManager.Instance.SwitchCamera(isFpsActive);
        }

        activePlayer.SetActive(false);
        vehicleController.isPlayerInside = true;

        // MOTORU ÇALIŞTIR
        if (exhaustSystem != null) exhaustSystem.SetEngineState(true);

        if (VehicleUI.Instance != null) VehicleUI.Instance.AraciDegistir(vehicleController);
    }

    public void ExitVehicle()
    {
        vehicleController.isPlayerInside = false;

        // Araç kameralarını uykuya al
        if (VehicleCameraManager.Instance != null) VehicleCameraManager.Instance.DisableCameras();

        if (VehicleUI.Instance != null) VehicleUI.Instance.AraciDegistir(null);

        // MOTORU KAPAT
        if (exhaustSystem != null) exhaustSystem.SetEngineState(false);

        activePlayer.transform.position = FindSafeExitPosition();
        activePlayer.SetActive(true);

        // İnerken oyuncunun kamerasını arabadaki son duruma eşitle
        if (playerCameraManager != null) playerCameraManager.SetCameraMode(isFpsActive);
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