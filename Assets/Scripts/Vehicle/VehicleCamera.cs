using UnityEngine;
using UnityEngine.InputSystem;

public class VehicleCamera : MonoBehaviour
{
    [Header("Takip Ayarlarý")]
    [Tooltip("Arabanýn içindeki CameraTarget objesini buraya atýn")]
    public Transform targetVehicle;
    public float distance = 6f;
    public float height = 2f;
    public float followSmoothness = 10f;

    [Header("Dönüþ Ayarlarý (Mouse)")]
    public float mouseSensitivity = 2f;

    private float currentX = 0f;
    private float currentY = 15f; // Kameranýn baþlangýç yüksekliði açýsý

    private void OnEnable()
    {
        // Arabaya her bindiðimizde (Kamera aktif olduðunda) kamerayý arabanýn arkasýna hizala
        if (targetVehicle != null)
        {
            currentX = targetVehicle.eulerAngles.y;
            currentY = 15f;
        }
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Sadece envanter menüsü kapalýysa fareyi oku
        if (!RadialInventoryUI.IsMenuOpen)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity * Time.unscaledDeltaTime;
            currentX += mouseDelta.x;
            currentY -= mouseDelta.y;

            // Kameranýn yerin altýna girmesini engelle (-10 alt sýnýr, 60 üst sýnýr)
            currentY = Mathf.Clamp(currentY, -10f, 60f);
        }
    }

    private void LateUpdate()
    {
        if (targetVehicle == null) return;

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        Vector3 desiredPosition = targetVehicle.position - (rotation * Vector3.forward * distance) + (Vector3.up * height);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothness);

        // Kamera her zaman belirlediðimiz hedef noktasýna baksýn
        transform.LookAt(targetVehicle.position);
    }
}