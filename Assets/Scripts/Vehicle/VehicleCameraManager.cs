using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class VehicleCameraManager : MonoBehaviour
{
    public static VehicleCameraManager Instance;

    [Header("Ortak Araç Kameralarý (Objeler)")]
    public GameObject tpsCameraObj;
    public GameObject fpsCameraObj;

    [Header("Cinemachine Bileþenleri")]
    public CinemachineCamera tpsCinemachine;
    public CinemachineCamera fpsCinemachine;

    [Header("FPS Kamera Ayarlarý (Ýç Görünüm)")]
    public float fpsSensitivity = 0.1f;
    public float fpsMaxPitch = 15f; // Yukarý/Aþaðý bakma sýnýrý (Daraltýldý)
    public float fpsMaxYaw = 15f;   // Saða/Sola bakma sýnýrý (Daraltýldý)

    [Header("TPS Kamera Ayarlarý (Dýþ Görünüm)")]
    public float tpsSensitivity = 0.2f;
    public float tpsDistance = 7f;  // Kameranýn arabaya uzaklýðý (Zevkine göre deðiþtir)
    public float tpsHeight = 3f;    // Kameranýn yerden yüksekliði
    public float tpsMinPitch = -15f;
    public float tpsMaxPitch = 70f;

    private Transform _tpsTarget;
    private Transform _fpsTarget;
    private bool _isFPS = false;

    private float _fpsPitch, _fpsYaw;
    private float _tpsPitch = 15f, _tpsYaw;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void SetTargets(Transform tpsFollow, Transform fpsHead)
    {
        _tpsTarget = tpsFollow;
        _fpsTarget = fpsHead;

        // --- SARI NOKTA VE SABÝTLÝK ÇÖZÜMÜ ---
        // Cinemachine'in otomatik takiplerini tamamen kapatýyoruz.
        // Artýk her iki kamerayý da biz kendi kusursuz kodumuzla (fareyle) yöneteceðiz!
        if (tpsCinemachine != null)
        {
            tpsCinemachine.Follow = null;
            tpsCinemachine.LookAt = null;
        }

        if (fpsCinemachine != null)
        {
            fpsCinemachine.Follow = null;
            fpsCinemachine.LookAt = null;
        }

        // FPS kamera açýlarýný sýfýrla
        _fpsPitch = 0f;
        _fpsYaw = 0f;

        // Arabaya ilk bindiðinde TPS kamerasý yamuk bakmasýn diye, aracýn baktýðý yöne (Y ekseni) hizala
        _tpsPitch = 15f;
        _tpsYaw = tpsFollow != null ? tpsFollow.eulerAngles.y : 0f;
    }

    public void SwitchCamera(bool isFPS)
    {
        _isFPS = isFPS;
        fpsCameraObj.SetActive(isFPS);
        tpsCameraObj.SetActive(!isFPS);

        if (fpsCinemachine != null) fpsCinemachine.Priority = isFPS ? 20 : 0;
        if (tpsCinemachine != null) tpsCinemachine.Priority = !isFPS ? 20 : 0;
    }

    public void DisableCameras()
    {
        fpsCameraObj.SetActive(false);
        tpsCameraObj.SetActive(false);

        if (fpsCinemachine != null) fpsCinemachine.Priority = 0;
        if (tpsCinemachine != null) tpsCinemachine.Priority = 0;
    }

    private void LateUpdate()
    {
        if (Mouse.current == null) return;
        Vector2 look = Mouse.current.delta.ReadValue();

        // ==========================================
        // 1. FPS KAMERA KONTROLÜ (Sýnýrlandýrýlmýþ)
        // ==========================================
        if (_isFPS && _fpsTarget != null && fpsCameraObj != null)
        {
            _fpsPitch -= look.y * fpsSensitivity;
            _fpsPitch = Mathf.Clamp(_fpsPitch, -fpsMaxPitch, fpsMaxPitch);

            _fpsYaw += look.x * fpsSensitivity;
            _fpsYaw = Mathf.Clamp(_fpsYaw, -fpsMaxYaw, fpsMaxYaw);

            fpsCameraObj.transform.position = _fpsTarget.position;
            fpsCameraObj.transform.rotation = _fpsTarget.rotation * Quaternion.Euler(_fpsPitch, _fpsYaw, 0f);
        }

        // ==========================================
        // 2. TPS KAMERA KONTROLÜ (GTA Tipi Serbest Yörünge)
        // ==========================================
        else if (!_isFPS && _tpsTarget != null && tpsCameraObj != null)
        {
            _tpsPitch -= look.y * tpsSensitivity;
            _tpsPitch = Mathf.Clamp(_tpsPitch, tpsMinPitch, tpsMaxPitch);

            _tpsYaw += look.x * tpsSensitivity;

            // Kamerayý aracýn etrafýnda pürüzsüzce döndüren o meþhur matematik
            Quaternion rotation = Quaternion.Euler(_tpsPitch, _tpsYaw, 0f);
            Vector3 targetPos = _tpsTarget.position + (Vector3.up * tpsHeight);
            Vector3 offset = rotation * new Vector3(0, 0, -tpsDistance);

            tpsCameraObj.transform.position = targetPos + offset;
            tpsCameraObj.transform.rotation = Quaternion.LookRotation(targetPos - tpsCameraObj.transform.position);
        }
    }
}