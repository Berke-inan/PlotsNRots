using UnityEngine;

namespace PlotNRots.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Hareket Ayarları")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float sprintSpeed = 15.0f;

        [Header("Zıplama & Yerçekimi")]
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -15.0f;

        [Header("Zemin Kontrol Ayarları")]
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private float groundCheckDistance = 0.15f;

        private CharacterController _controller;
        private PlayerInputHandler _inputHandler;
        private Vector3 _velocity;
        private bool _isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void OnEnable()
        {
            // Input'tan gelen zıplama emrini dinle
            _inputHandler.OnJump += HandleJump;
        }

        private void OnDisable()
        {
            _inputHandler.OnJump -= HandleJump;
        }

        private void Update()
        {
            // Her frame'de zeminde miyiz kontrol et
            PerformGroundCheck();

            HandleMovement();
            ApplyGravity();
        }

        private void HandleMovement()
        {
            Vector2 input = _inputHandler.MoveInput;
            Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;

            float currentSpeed = _inputHandler.IsSprinting ? sprintSpeed : walkSpeed;

            _controller.Move(moveDirection * (currentSpeed * Time.deltaTime));
        }

        private void ApplyGravity()
        {
            // Controller'ın buglı isGrounded'ı yerine kendi sağlam kontrolümüzü kullanıyoruz
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Yere tam yapışma
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleJump()
        {
            // Sadece yerdeyken zıplamaya izin ver
            if (_isGrounded)
            {
                // Fiziksel zıplama formülü: v = sqrt(h * -2 * g)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        private void PerformGroundCheck()
        {
            // CharacterController'ın geometrik sınırlarını kullanarak hatasız zemin taraması
            Vector3 center = _controller.bounds.center;
            float bottomY = center.y - _controller.bounds.extents.y + _controller.radius;
            Vector3 sphereCastOrigin = new Vector3(center.x, bottomY, center.z);

            _isGrounded = Physics.SphereCast(
                origin: sphereCastOrigin,
                radius: _controller.radius * 0.9f, // Duvarlara sürtünüp havada kalmayı önler
                direction: Vector3.down,
                out RaycastHit hit,
                maxDistance: groundCheckDistance,
                layerMask: groundLayerMask,
                queryTriggerInteraction: QueryTriggerInteraction.Ignore
            );
        }

        // Editor'de zemin kontrol mesafesini görebilmen için yardımcı
        private void OnDrawGizmosSelected()
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_controller != null)
            {
                Vector3 center = _controller.bounds.center;
                float bottomY = center.y - _controller.bounds.extents.y + _controller.radius;
                Vector3 sphereCastOrigin = new Vector3(center.x, bottomY, center.z);
                Vector3 castEnd = sphereCastOrigin + Vector3.down * groundCheckDistance;

                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(sphereCastOrigin, _controller.radius * 0.9f);
                Gizmos.DrawLine(sphereCastOrigin, castEnd);
                Gizmos.DrawWireSphere(castEnd, _controller.radius * 0.9f);
            }
        }
    }
}