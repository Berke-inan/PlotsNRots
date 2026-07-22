using System;
using UnityEngine;

namespace PlotNRots.Player
{
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }

        // Dışarıdan girdileri kapatıp açabilmek için kontrol bayrağı
        public bool IsInputActive { get; set; } = true;

        public Action OnSwitchCamera;
        public Action OnJump;
        public Action OnPauseToggle; // Menüyü aç/kapat eventi

        private InputSystem_Actions _inputActions;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputActions.Enable();

            _inputActions.Player.SwitchCamera.performed += ctx => OnSwitchCamera?.Invoke();
            _inputActions.Player.Jump.performed += ctx => OnJump?.Invoke();

            // Yeni Pause tuşu ataması
            _inputActions.Player.Pause.performed += ctx => OnPauseToggle?.Invoke();

            _inputActions.Player.Sprint.performed += ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled += ctx => IsSprinting = false;
        }

        private void OnDisable()
        {
            _inputActions.Disable();

            _inputActions.Player.SwitchCamera.performed -= ctx => OnSwitchCamera?.Invoke();
            _inputActions.Player.Jump.performed -= ctx => OnJump?.Invoke();
            _inputActions.Player.Pause.performed -= ctx => OnPauseToggle?.Invoke();

            _inputActions.Player.Sprint.performed -= ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled -= ctx => IsSprinting = false;
        }

        private void Update()
        {
            // Eğer oyun durduysa veya menü açıksa girdi okumayı kes (Kamera ve hareket dursun)
            if (!IsInputActive)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                return;
            }

            MoveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            LookInput = _inputActions.Player.Look.ReadValue<Vector2>();
        }
    }
}