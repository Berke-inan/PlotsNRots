using UnityEngine;
using Unity.Cinemachine; // Hata verirse sadece 'using Cinemachine;' kullan

namespace PlotNRots.Player
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Kapsülün içindeki HeadTarget objesi")]
        [SerializeField] private Transform headTarget;
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private PlayerMovement playerMovement;

        [Header("Özellik Aç/Kapat")]
        [SerializeField] private bool enableHeadbob = true;     // Sallanmayı aç/kapat
        [SerializeField] private bool enableFOVChange = true;   // FOV değişimini aç/kapat

        [Header("Kamera Ayarları")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float maxPitchAngle = 85f;

        [Header("Kamera Yükseklik (Eğilme)")]
        [SerializeField] private float standHeight = 1.6f;
        [SerializeField] private float crouchHeight = 0.8f;
        [SerializeField] private float heightLerpSpeed = 10f;

        [Header("FOV (Görüş Açısı) Ayarları")]
        [SerializeField] private float baseFOV = 60f;
        [SerializeField] private float sprintFOV = 75f;
        [SerializeField] private float jumpFOV = 80f;
        [SerializeField] private float fovLerpSpeed = 8f;

        [Header("Headbob (Sallanma Hızı ve Miktarı)")]
        [SerializeField] private float walkBobSpeed = 12f;
        [SerializeField] private float walkBobAmount = 0.04f;
        [SerializeField] private float sprintBobSpeed = 16f;
        [SerializeField] private float sprintBobAmount = 0.08f;
        [SerializeField] private float crouchBobSpeed = 8f;
        [SerializeField] private float crouchBobAmount = 0.02f;

        private PlayerInputHandler _inputHandler;
        private float _pitch = 0f;
        private float _defaultYPos;
        private float _timer;
        private float _targetFOV;

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _defaultYPos = standHeight;
            _targetFOV = baseFOV;
        }

        private void Update()
        {
            // EĞER RADYAL MENÜ AÇIKSA KAMERA DÖNÜŞ İŞLEMLERİNİ İPTAL ET
            if (RadialInventoryUI.IsMenuOpen) return;

            HandleRotation();

            if (playerMovement != null && headTarget != null)
            {
                HandleHeightAndBob();
            }
            if (virtualCamera != null)
            {
                HandleFOV();
            }
        }

        private void HandleRotation()
        {
            if (headTarget == null) return;

            Vector2 lookInput = _inputHandler.LookInput;

            // Yukarı/Aşağı bakış (Sadece görünmez kafa döner)
            _pitch -= lookInput.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -maxPitchAngle, maxPitchAngle);
            headTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // Sağa/Sola bakış (Tüm vücut anında döner)
            transform.Rotate(Vector3.up * (lookInput.x * mouseSensitivity));
        }

        private void HandleHeightAndBob()
        {
            // 1. Hedef yüksekliği belirle (Eğilme / Ayakta) ve her zaman uygula
            float targetHeight = playerMovement.IsCrouching ? crouchHeight : standHeight;
            _defaultYPos = Mathf.Lerp(_defaultYPos, targetHeight, Time.deltaTime * heightLerpSpeed);

            Vector3 localPos = headTarget.localPosition;

            // 2. Headbob ayarı açıksa sallanmayı hesapla
            if (enableHeadbob)
            {
                Vector2 input = _inputHandler.MoveInput;
                bool isMoving = input.magnitude > 0.1f && playerMovement.IsGrounded;

                if (isMoving)
                {
                    float currentSpeed = walkBobSpeed;
                    float currentAmount = walkBobAmount;

                    if (playerMovement.IsCrouching)
                    {
                        currentSpeed = crouchBobSpeed;
                        currentAmount = crouchBobAmount;
                    }
                    else if (_inputHandler.IsSprinting && input.y > 0)
                    {
                        currentSpeed = sprintBobSpeed;
                        currentAmount = sprintBobAmount;
                    }

                    _timer += Time.deltaTime * currentSpeed;
                    float bobOffset = Mathf.Sin(_timer) * currentAmount;

                    localPos.y = _defaultYPos + bobOffset;
                }
                else
                {
                    // Durduğunda kamerayı sarsmadan merkeze döndür
                    _timer = 0;
                    localPos.y = Mathf.Lerp(localPos.y, _defaultYPos, Time.deltaTime * 5f);
                }
            }
            else
            {
                // Headbob kapalıysa sadece eğilme/kalkma yüksekliğinde sabit kalsın
                _timer = 0;
                localPos.y = _defaultYPos;
            }

            // X ekseni (sağ/sol) sallanması her zaman 0 (Mide bulantısını engellemek için)
            localPos.x = 0;
            headTarget.localPosition = localPos;
        }

        private void HandleFOV()
        {
            // FOV sistemi kapalıysa hedef FOV her zaman temel değer kalır
            if (!enableFOVChange)
            {
                _targetFOV = baseFOV;
            }
            else
            {
                // Havadayken
                if (!playerMovement.IsGrounded)
                {
                    _targetFOV = jumpFOV;
                }
                // Yerdeyken ve Koşarken
                else if (_inputHandler.IsSprinting && _inputHandler.MoveInput.y > 0 && !playerMovement.IsCrouching)
                {
                    _targetFOV = sprintFOV;
                }
                // Normal Yürüme / Durma
                else
                {
                    _targetFOV = baseFOV;
                }
            }

            // Mevcut FOV'u pürüzsüzce hedefe ulaştır
            virtualCamera.Lens.FieldOfView = Mathf.Lerp(virtualCamera.Lens.FieldOfView, _targetFOV, Time.deltaTime * fovLerpSpeed);
        }
    }
}