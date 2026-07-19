using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(VehicleController))]
public class VehicleLights : MonoBehaviour
{
    [Header("Iþýk Objeleri")]
    public GameObject[] headlights;
    public GameObject[] brakeLights;
    public GameObject[] reverseLights;

    private VehicleController vehicle;
    private bool areHeadlightsOn = false;

    // Geçmiþ durumlarý takip etmek için deðiþkenler (Akýllý Sistem)
    private bool wasNight = false;
    private bool wasPlayerInside = false;

    private void Start()
    {
        vehicle = GetComponent<VehicleController>();
        ToggleLights(headlights, false);
        ToggleLights(brakeLights, false);
        ToggleLights(reverseLights, false);
    }

    private void Update()
    {
        // Gündüz/Gece durumunu DayNightCycleManager'dan çekiyoruz
        bool isNight = false;
        if (DayNightCycleManager.Instance != null)
        {
            isNight = DayNightCycleManager.Instance.IsNight();
        }

        bool isPlayerInside = vehicle.isPlayerInside;

        // --- FAR KONTROLÜ (OTOMATÝK VE MANUEL) ---
        if (isPlayerInside)
        {
            // 1. Arabaya YENÝ BÝNDÝÐÝNDE ve hava karanlýksa otomatik aç
            if (!wasPlayerInside && isNight)
            {
                areHeadlightsOn = true;
                ToggleLights(headlights, true);
            }
            // 2. Araba içindeyken HAVA KARARDIÐINDA otomatik aç
            else if (isNight && !wasNight)
            {
                areHeadlightsOn = true;
                ToggleLights(headlights, true);
            }
            // 3. Araba içindeyken SABAH OLDUÐUNDA otomatik kapat
            else if (!isNight && wasNight)
            {
                areHeadlightsOn = false;
                ToggleLights(headlights, false);
            }

            // 4. MANUEL KONTROL (Oyuncu yine de L tuþuyla müdahale edebilsin)
            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            {
                areHeadlightsOn = !areHeadlightsOn;
                ToggleLights(headlights, areHeadlightsOn);
            }
        }
        else
        {
            // Arabadan indiði an farlarý kapat
            if (wasPlayerInside)
            {
                areHeadlightsOn = false;
                ToggleLights(headlights, false);
            }
        }

        // --- FREN VE GERÝ VÝTES LAMBALARI (Ayný Kalarak Devam Ediyor) ---
        bool shouldBrakeLightsBeOn = vehicle.IsBraking && isPlayerInside;
        ToggleLights(brakeLights, shouldBrakeLightsBeOn);

        bool shouldReverseLightsBeOn = vehicle.IsReversing && isPlayerInside;
        ToggleLights(reverseLights, shouldReverseLightsBeOn);

        // Bir sonraki karede (frame) kontrol edebilmek için anlýk durumu geçmiþe kaydet
        wasNight = isNight;
        wasPlayerInside = isPlayerInside;
    }

    private void ToggleLights(GameObject[] lightArray, bool state)
    {
        foreach (GameObject lightObj in lightArray)
        {
            if (lightObj != null && lightObj.activeSelf != state)
            {
                lightObj.SetActive(state);
            }
        }
    }
}