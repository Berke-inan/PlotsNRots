using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3 için gerekli kütüphane

namespace PlotNRots.Player
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class CameraManager : MonoBehaviour
    {
        [Header("Cinemachine Kameraları")]
        [SerializeField] private CinemachineCamera fpsCamera;
        [SerializeField] private CinemachineCamera tpsCamera;

        private PlayerInputHandler _inputHandler;
        private bool _isFPS = true;

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void OnEnable()
        {
            // Olayı dinlemeye başla
            _inputHandler.OnSwitchCamera += ToggleCamera;
        }

        private void OnDisable()
        {
            // Dinlemeyi bırak
            _inputHandler.OnSwitchCamera -= ToggleCamera;
        }

        private void ToggleCamera()
        {
            _isFPS = !_isFPS;

            // Önceliği yüksek olan kamera aktif olur
            if (_isFPS)
            {
                fpsCamera.Priority = 12;
                tpsCamera.Priority = 10;
            }
            else
            {
                tpsCamera.Priority = 12;
                fpsCamera.Priority = 10;
            }
        }
    }
}