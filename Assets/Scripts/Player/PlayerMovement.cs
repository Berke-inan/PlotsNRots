using UnityEngine;

namespace FarmerSimulator.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Hareket Ayarları")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float sprintSpeed = 15.0f; // Unity Inspector üzerinden değiştirilebilir
        [SerializeField] private float gravity = -15.0f;

        private CharacterController _controller;
        private PlayerInputHandler _inputHandler;
        private Vector3 _velocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            HandleMovement();
            ApplyGravity();
        }

        private void HandleMovement()
        {
            Vector2 input = _inputHandler.MoveInput;
            Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;

            // Koşma durumuna göre anlık hızı belirle
            float currentSpeed = _inputHandler.IsSprinting ? sprintSpeed : walkSpeed;

            _controller.Move(moveDirection * (currentSpeed * Time.deltaTime));
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }
    }
}