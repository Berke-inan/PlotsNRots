using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleCameraController : MonoBehaviour
{
    [Header("Þoför Kamera Ayarlarý")]
    public float mouseSensitivity = 0.1f;
    public float maxPitchAngle = 45f;
    public float maxYawAngle = 110f;

    private float _pitch = 0f;
    private float _yaw = 0f;

    [HideInInspector]
    public Transform currentDriverHead; // Sistem burayý otomatik dolduracak

    // Yeni bir araca bindiðimizde kamerayý dümdüz ileri baktýrýr
    public void ResetRotation()
    {
        _pitch = 0f;
        _yaw = 0f;
    }

    private void LateUpdate()
    {
        if (Mouse.current == null || currentDriverHead == null) return;

        Vector2 lookInput = Mouse.current.delta.ReadValue();

        _pitch -= lookInput.y * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -maxPitchAngle, maxPitchAngle);

        _yaw += lookInput.x * mouseSensitivity;
        _yaw = Mathf.Clamp(_yaw, -maxYawAngle, maxYawAngle);

        // ÝÞTE EKLENEN YENÝ SATIR: Kamerayý fiziksel olarak þoförün kafasýna ýþýnla ve zýmbala!
        transform.position = currentDriverHead.position;

        // Þoför koltuðunun mevcut yönünü baz al, üstüne farenin bakýþýný ekle
        transform.rotation = currentDriverHead.rotation * Quaternion.Euler(_pitch, _yaw, 0f);
    }
}