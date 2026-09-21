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

        public bool CrouchInput { get; private set; }

        public Action OnSwitchCamera;
        public Action OnJump;
        public Action OnPauseToggle;

        public Action OnInteract;
        public Action OnDrop;
        public Action OnUse;
        public Action<float> OnScroll;
        public Action OnEnterVehicle;

        // Klavye rakamlarıyla slot seçimi (0-9 arası index gönderir)
        public Action<int> OnSlotSelect;

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
            _inputActions.Player.Pause.performed += ctx => OnPauseToggle?.Invoke();

            _inputActions.Player.Sprint.performed += ctx => IsSprinting = true;
            _inputActions.Player.Sprint.canceled += ctx => IsSprinting = false;

            _inputActions.Player.Interact.performed += ctx => OnInteract?.Invoke();
            _inputActions.Player.Drop.performed += ctx => OnDrop?.Invoke();
            _inputActions.Player.Attack.performed += ctx => OnUse?.Invoke();
            _inputActions.Player.EnterVehicle.performed += ctx => OnEnterVehicle?.Invoke();

            _inputActions.Player.Crouch.performed += ctx => CrouchInput = true;
            _inputActions.Player.Crouch.canceled += ctx => CrouchInput = false;
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
            _inputActions.Player.Crouch.performed -= ctx => CrouchInput = true;
            _inputActions.Player.Crouch.canceled -= ctx => CrouchInput = false;
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

            // Fare Tekerleği Kontrolü
            if (Mouse.current != null)
            {
                float scrollValue = Mouse.current.scroll.ReadValue().y;
                if (scrollValue != 0) OnScroll?.Invoke(scrollValue);
            }

            // Rakam Tuşları (1'den 0'a kadar toplam 10 slot desteği)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) OnSlotSelect?.Invoke(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) OnSlotSelect?.Invoke(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) OnSlotSelect?.Invoke(2);
                if (Keyboard.current.digit4Key.wasPressedThisFrame) OnSlotSelect?.Invoke(3);
                if (Keyboard.current.digit5Key.wasPressedThisFrame) OnSlotSelect?.Invoke(4);
                if (Keyboard.current.digit6Key.wasPressedThisFrame) OnSlotSelect?.Invoke(5);
                if (Keyboard.current.digit7Key.wasPressedThisFrame) OnSlotSelect?.Invoke(6);
                if (Keyboard.current.digit8Key.wasPressedThisFrame) OnSlotSelect?.Invoke(7);
                if (Keyboard.current.digit9Key.wasPressedThisFrame) OnSlotSelect?.Invoke(8);
                if (Keyboard.current.digit0Key.wasPressedThisFrame) OnSlotSelect?.Invoke(9);
            }
        }
    }
}