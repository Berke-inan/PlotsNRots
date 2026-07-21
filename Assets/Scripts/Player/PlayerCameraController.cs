using UnityEngine;

namespace PlotNRots.Player
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Kapsülün içindeki HeadTarget objesi")]
        [SerializeField] private Transform headTarget;

        [Header("Kamera Ayarları")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float maxPitchAngle = 85f; // Sadece yukarı/aşağı sınırı kaldı

        private PlayerInputHandler _inputHandler;
        private float _pitch = 0f;

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            // EĞER RADYAL MENÜ AÇIKSA KAMERA DÖNÜŞ İŞLEMLERİNİ İPTAL ET
            if (RadialInventoryUI.IsMenuOpen) return;

            HandleRotation();
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
    }
}