using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlotNRots.Player
{
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsInputActive { get; set; } = true;

        public Action OnSwitchCamera;
        public Action OnJump;
        public Action OnPauseToggle;

        public Action OnInteract;
        public Action OnDrop;
        public Action OnUse;
        public Action<float> OnScroll;
        public Action OnEnterVehicle;  // Araca binme sinyali eklendi

        private InputSystem_Actions _inputActions;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputActions.Enable();

            _inputActions.Player.SwitchCamera.performed += ctx => OnSwitchCamera?.Invoke();
            _inputActions.Player.Jump.performed += ctx =>OnJump?.Invoke(); 
            _inputActions.Player.Pause.performed += ctx => OnPauseToggle?.Invoke();

            _inputActions.Player.Sprint.performed += ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled += ctx => IsSprinting = false;

            _inputActions.Player.Interact.performed += ctx =>OnInteract?.Invoke();
            _inputActions.Player.Drop.performed += ctx => OnDrop?.Invoke();
            _inputActions.Player.Attack.performed += ctx => OnUse?.Invoke();

            // Yeni oluşturduğun Action buraya bağlanıyor
            _inputActions.Player.EnterVehicle.performed += ctx => OnEnterVehicle?.Invoke();
        }

        private void OnDisable()
        {
            _inputActions.Disable();

            _inputActions.Player.SwitchCamera.performed -= ctx => OnSwitchCamera?.Invoke();
            _inputActions.Player.Jump.performed -= ctx => OnJump?.Invoke();
            _inputActions.Player.Pause.performed -= ctx => OnPauseToggle?.Invoke();
            _inputActions.Player.Sprint.performed -= ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled -= ctx => IsSprinting = false;
            _inputActions.Player.Interact.performed -= ctx => OnInteract?.Invoke();
            _inputActions.Player.Drop.performed -= ctx => OnDrop?.Invoke();
            _inputActions.Player.Attack.performed -= ctx => OnUse?.Invoke();
            _inputActions.Player.EnterVehicle.performed -= ctx => OnEnterVehicle?.Invoke();
        }

        private void Update()
        {
            if (!IsInputActive)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                return;
            }

            MoveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            LookInput = _inputActions.Player.Look.ReadValue<Vector2>();

            if (Mouse.current != null)
            {
                float scrollValue = Mouse.current.scroll.ReadValue().y;
                if (scrollValue != 0) OnScroll?.Invoke(scrollValue);
            }
        }
    }
}