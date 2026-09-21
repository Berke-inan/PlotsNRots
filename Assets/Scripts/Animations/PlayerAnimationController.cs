using UnityEngine;

namespace PlotNRots.Player
{
    [RequireComponent(typeof(Animator), typeof(PlayerInputHandler), typeof(PlayerMovement))]
    public class PlayerAnimationController : MonoBehaviour
    {
        private Animator _animator;
        private PlayerInputHandler _inputHandler;
        private PlayerMovement _playerMovement;

        // Animator Parametrelerinin Hash ID'leri (Performans için)
        private readonly int _dirXHash = Animator.StringToHash("DirX");
        private readonly int _dirYHash = Animator.StringToHash("DirY");
        private readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
        private readonly int _verticalVelocityHash = Animator.StringToHash("VerticalVelocity");
        private readonly int _jumpTriggerHash = Animator.StringToHash("Jump");

        private readonly int _isCrouchingHash = Animator.StringToHash("IsCrouching");

        private int _headLayerIndex;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _inputHandler = GetComponent<PlayerInputHandler>();
            _playerMovement = GetComponent<PlayerMovement>();

            // Kafayı düzelten katmanın index'i
            _headLayerIndex = _animator.GetLayerIndex("HeadLayer");
        }

        private void OnEnable()
        {
            _inputHandler.OnJump += HandleJumpAnimation;
        }

        private void OnDisable()
        {
            _inputHandler.OnJump -= HandleJumpAnimation;
        }

        private void HandleJumpAnimation()
        {
            // Sadece yerdeyken zıplama tetiklensin
            if (_playerMovement.IsGrounded)
            {
                _animator.SetTrigger(_jumpTriggerHash);
            }
        }

        private void Update()
        {
            if (!_inputHandler.IsInputActive)
            {
                _animator.SetFloat(_dirXHash, 0f, 0.1f, Time.deltaTime);
                _animator.SetFloat(_dirYHash, 0f, 0.1f, Time.deltaTime);
                return;
            }

            // 1. Zemin ve Yerçekimi Parametreleri
            _animator.SetBool(_isGroundedHash, _playerMovement.IsGrounded);
            _animator.SetFloat(_verticalVelocityHash, _playerMovement.VerticalVelocity);
            _animator.SetBool(_isCrouchingHash, _playerMovement.IsCrouching);

            // 2. Yön Parametreleri (Blend Tree İçin)
            Vector2 moveInput = _inputHandler.MoveInput;
            float targetX = moveInput.x;
            float targetY = moveInput.y;

            if (targetY > 0)
            {
                bool canSprint = _inputHandler.IsSprinting && Mathf.Abs(targetX) < 0.1f;
                targetY = canSprint ? 1f : 0.5f;
            }

            _animator.SetFloat(_dirXHash, targetX, 0.1f, Time.deltaTime);
            _animator.SetFloat(_dirYHash, targetY, 0.1f, Time.deltaTime);

            // 3. Düşerken Kafa Maskesini Devreye Sokma
            if (_headLayerIndex != -1)
            {
                bool isFalling = !_playerMovement.IsGrounded && _playerMovement.VerticalVelocity < 0f;
                float currentWeight = _animator.GetLayerWeight(_headLayerIndex);
                float targetWeight = isFalling ? 1f : 0f;

                float newWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * 10f);
                _animator.SetLayerWeight(_headLayerIndex, newWeight);
            }
        }
    }
}