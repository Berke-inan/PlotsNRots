using UnityEngine;

// Blender eksenleri farklý gelirse Inspector'dan deðiþtirebilmen için
public enum InteriorAxis { X, Y, Z }

public class VehicleInterior : MonoBehaviour
{
    [Header("Ana Fizik Kodu Baðlantýsý")]
    [Tooltip("Bu aracýn VehicleController kodunu buraya sürükle")]
    public VehicleController vehicle;

    [Header("Direksiyon Ayarlarý")]
    public Transform steeringWheel;
    [Tooltip("Gerçek arabalarda direksiyon tekerlekten çok daha fazla döner. (Örn: 10 yazarsan tekerlek 35 derece dönerken direksiyon 350 derece döner)")]
    public float steeringMultiplier = 10f;
    public InteriorAxis steerAxis = InteriorAxis.Z;

    [Header("Hýz Göstergesi")]
    public Transform speedNeedle;
    public float maxDialSpeed = 120f;
    public float speedZeroAngle = 140f;
    public float speedMaxAngle = -140f;
    public InteriorAxis speedAxis = InteriorAxis.Z;

    [Header("Devir (RPM) Göstergesi")]
    public Transform rpmNeedle;
    public float maxDialRPM = 8f;
    public float rpmZeroAngle = 140f;
    public float rpmMaxAngle = -140f;
    public InteriorAxis rpmAxis = InteriorAxis.Z;
    [Tooltip("Vites atma hissiyatý için sanal vites sayýsý")]
    public int virtualGears = 5;

    private Vector3 initialSteerRot;
    private float currentFakeRPM = 0f;

    private void Start()
    {
        // Direksiyonun baþlangýç açýsýný kaydet ki yamuk dönmesin
        if (steeringWheel != null) initialSteerRot = steeringWheel.localEulerAngles;
    }

    private void Update()
    {
        // Eðer araç kodu yoksa veya oyuncu arabada deðilse animasyon oynatma (Performans tasarrufu)
        if (vehicle == null || !vehicle.isPlayerInside) return;

        UpdateSteeringWheel();
        UpdateSpeedometer();
        UpdateTachometer();
    }

    private void UpdateSteeringWheel()
    {
        if (steeringWheel == null) return;

        // FÝZÝK KODUNU BOZMAMAK ÝÇÝN HARÝKA BÝR HÝLE: 
        // Tekerleklerin mevcut fiziksel açýsýný doðrudan tekerlekten okuyoruz!
        float averageSteer = (vehicle.frontLeftCollider.steerAngle + vehicle.frontRightCollider.steerAngle) / 2f;
        float visualSteer = averageSteer * steeringMultiplier;

        Vector3 newRot = initialSteerRot;
        if (steerAxis == InteriorAxis.Z) newRot.z = initialSteerRot.z - visualSteer;
        else if (steerAxis == InteriorAxis.Y) newRot.y = initialSteerRot.y - visualSteer;
        else if (steerAxis == InteriorAxis.X) newRot.x = initialSteerRot.x - visualSteer;

        steeringWheel.localEulerAngles = newRot;
    }

    private void UpdateSpeedometer()
    {
        if (speedNeedle == null) return;

        // Gerçek hýzý kadrana oranla
        float speedRatio = Mathf.Clamp01(vehicle.currentSpeed / maxDialSpeed);
        float targetAngle = Mathf.Lerp(speedZeroAngle, speedMaxAngle, speedRatio);
        ApplyNeedleRotation(speedNeedle, targetAngle, speedAxis);
    }

    private void UpdateTachometer()
    {
        if (rpmNeedle == null) return;

        float idleRPM = 1f; // Rolantide dururkenki devir
        float maxEngineRPM = maxDialRPM - 1f; // Kýrmýzý çizgi

        float speedRatio = Mathf.Clamp01(vehicle.currentSpeed / vehicle.maxSpeed);

        // Sanal Þanzýman Matematiði (Vites atma hissi için)
        float gearFloat = speedRatio * virtualGears;
        int currentGear = Mathf.FloorToInt(gearFloat);
        float rpmInCurrentGear = gearFloat - currentGear;

        // Gaza aniden basýnca devrin fýrlamasý
        float throttleSpike = vehicle.IsAccelerating ? 1.5f : 0f;

        float targetRPM = idleRPM + (rpmInCurrentGear * (maxEngineRPM - idleRPM)) + throttleSpike;
        if (vehicle.IsReversing) targetRPM = idleRPM + (vehicle.currentSpeed / vehicle.maxReverseSpeed) * 4f + throttleSpike;
        if (targetRPM > maxDialRPM) targetRPM = maxDialRPM;

        // Devrin pürüzsüzce (gazý býrakýnca yavaþça) inip çýkmasý
        float lerpSpeed = vehicle.IsAccelerating ? 5f : 2f;
        currentFakeRPM = Mathf.Lerp(currentFakeRPM, targetRPM, Time.deltaTime * lerpSpeed);

        float rpmRatio = Mathf.Clamp01(currentFakeRPM / maxDialRPM);
        float targetRpmAngle = Mathf.Lerp(rpmZeroAngle, rpmMaxAngle, rpmRatio);
        ApplyNeedleRotation(rpmNeedle, targetRpmAngle, rpmAxis);
    }

    private void ApplyNeedleRotation(Transform needle, float angle, InteriorAxis axis)
    {
        Vector3 rot = needle.localEulerAngles;
        if (axis == InteriorAxis.Z) rot.z = angle;
        else if (axis == InteriorAxis.Y) rot.y = angle;
        else if (axis == InteriorAxis.X) rot.x = angle;
        needle.localEulerAngles = rot;
    }
}