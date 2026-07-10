using System;
using UnityEngine;

namespace FarmerSimulator.Player
{
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }

        // Diğer sınıfların okuyabileceği koşma durumu
        public bool IsSprinting { get; private set; }

        public Action OnSwitchCamera;

        private InputSystem_Actions _inputActions;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputActions.Enable();

            _inputActions.Player.SwitchCamera.performed += ctx => OnSwitchCamera?.Invoke();

            // Koşma tuşuna basıldığında ve çekildiğinde tetiklenecek olaylar
            _inputActions.Player.Sprint.performed += ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled += ctx => IsSprinting = false;
        }

        private void OnDisable()
        {
            _inputActions.Disable();
            _inputActions.Player.SwitchCamera.performed -= ctx => OnSwitchCamera?.Invoke();

            _inputActions.Player.Sprint.performed -= ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled -= ctx => IsSprinting = false;
        }

        private void Update()
        {
            MoveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            LookInput = _inputActions.Player.Look.ReadValue<Vector2>();
        }
    }
}