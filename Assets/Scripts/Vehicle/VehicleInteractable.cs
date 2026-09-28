using UnityEngine;
using UnityEngine.InputSystem;
using PlotNRots.Player;

public class VehicleInteractable : MonoBehaviour
{
    [Header("Referanslar")]
    public VehicleController vehicleController;

    [Header("Modüler Kamera Hedefleri")]
    public Transform tpsTarget;
    public Transform fpsTarget;

    [Header("Ayarlar")]
    public float exitOffset = 1.8f;
    public float searchRadius = 0.5f;

    private GameObject activePlayer;
    private CameraManager playerCameraManager;
    private Collider[] vehicleColliders;
    private float enterTime;
    private bool isFpsActive = false;

    private TractorExhaust exhaustSystem;

    private void Start()
    {
        vehicleColliders = GetComponentsInChildren<Collider>();
        exhaustSystem = GetComponent<TractorExhaust>();
    }

    private void Update()
    {
        if (vehicleController.isPlayerInside && activePlayer != null && Time.time > enterTime + 0.2f)
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.vKey.wasPressedThisFrame)
            {
                isFpsActive = !isFpsActive;
                if (VehicleCameraManager.Instance != null)
                    VehicleCameraManager.Instance.SwitchCamera(isFpsActive);
            }

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
        playerCameraManager = activePlayer.GetComponent<CameraManager>();
        if (playerCameraManager != null) isFpsActive = playerCameraManager.IsFPS;

        if (VehicleCameraManager.Instance != null)
        {
            VehicleCameraManager.Instance.SetTargets(tpsTarget, fpsTarget);
            VehicleCameraManager.Instance.SwitchCamera(isFpsActive);
        }

        activePlayer.SetActive(false);
        vehicleController.isPlayerInside = true;

        if (exhaustSystem != null) exhaustSystem.SetEngineState(true);
        if (VehicleUI.Instance != null) VehicleUI.Instance.AraciDegistir(vehicleController);
    }

    public void ExitVehicle()
    {
        vehicleController.isPlayerInside = false;

        if (VehicleCameraManager.Instance != null) VehicleCameraManager.Instance.DisableCameras();
        if (VehicleUI.Instance != null) VehicleUI.Instance.AraciDegistir(null);
        if (exhaustSystem != null) exhaustSystem.SetEngineState(false);

        activePlayer.transform.position = FindSafeExitPosition();
        activePlayer.SetActive(true);

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