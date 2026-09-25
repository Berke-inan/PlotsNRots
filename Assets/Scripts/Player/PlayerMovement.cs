using UnityEngine;

namespace PlotNRots.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Hareket Ayarları")]
        [SerializeField] private float walkSpeed = 5.0f;
        [SerializeField] private float backwardWalkSpeed = 3.0f;
        [SerializeField] private float sprintSpeed = 15.0f;
        [SerializeField] private float crouchSpeed = 2.5f; // Eğilme hızı

        [Header("Eğilme & Boyut Ayarları")]
        [SerializeField] private float standHeight = 2.0f; // Karakterin normal boyu
        [SerializeField] private float crouchHeight = 1.0f; // Eğilmiş boyu
        [SerializeField] private float crouchTransitionSpeed = 10f; // Kameranın inip kalkma yumuşaklığı
        [SerializeField] private LayerMask obstacleLayerMask; // Tavan kontrolü için katman (Örn: Default)

        [Header("Havada Hareket (Air Control)")]
        [SerializeField][Range(0f, 1f)] private float airControl = 0.3f;

        [Header("Zıplama & Yerçekimi")]
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -15.0f;

        [Header("Zemin Kontrol Ayarları")]
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private float groundCheckDistance = 0.15f;

        private CharacterController _controller;
        private PlayerInputHandler _inputHandler;

        private Vector3 _velocity;
        private Vector3 _currentHorizontalVelocity;

        private bool _isGrounded;
        private bool _isCrouching; // Karakterin fiziksel olarak eğilip eğilmediğini tutar

        public bool IsGrounded => _isGrounded;
        public float VerticalVelocity => _velocity.y;
        public bool IsCrouching => _isCrouching; // Animator'a göndermek için dışa açtık

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void OnEnable() => _inputHandler.OnJump += HandleJump;
        private void OnDisable() => _inputHandler.OnJump -= HandleJump;

        private void Update()
        {
            PerformGroundCheck();
            HandleCrouch(); // Eğilme boyutu ayarları
            HandleMovement();
            ApplyGravity();
        }

        private void HandleCrouch()
        {
            // Oyuncu eğilmek istiyor mu?
            bool wantsToCrouch = _inputHandler.CrouchInput;

            // Eğer oyuncu eğilmeyi bırakmak istiyorsa ama tepesinde bir engel varsa
            if (_isCrouching && !wantsToCrouch)
            {
                if (!CanStandUp())
                {
                    wantsToCrouch = true; // Kafasını vurmamak için zorla eğik kal
                }
            }

            _isCrouching = wantsToCrouch;

            // Kapsülün hedef boyunu belirle ve pürüzsüzce değiştir
            float targetHeight = _isCrouching ? crouchHeight : standHeight;
            _controller.height = Mathf.Lerp(_controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

            // Kapsülün merkezini her zaman boyunun yarısında tut (Karakterin ayakları yerden kesilmesin diye)
            _controller.center = new Vector3(0, _controller.height / 2f, 0);
        }

        // Tavanı kontrol eden güvenli ışın (Kafanın üst kısmı)
        private bool CanStandUp()
        {
            // Kapsülün en üst küresinin merkezini bul
            Vector3 topSphereCenter = transform.position + _controller.center + Vector3.up * (_controller.height / 2f - _controller.radius);
            float checkDistance = standHeight - _controller.height; // Kalkması gereken mesafe kadar yukarı tarama yap

            // Yukarıya doğru bir küre fırlat, çarpan bir engel varsa ayağa kalkamaz
            return !Physics.SphereCast(topSphereCenter, _controller.radius * 0.9f, Vector3.up, out _, checkDistance, obstacleLayerMask);
        }

        private void HandleMovement()
        {
            Vector2 input = _inputHandler.MoveInput;
            Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;

            float targetSpeed;

            if (_isCrouching)
            {
                // Eğilirken koşmayı iptal eder, sadece eğilme hızı geçerlidir
                targetSpeed = crouchSpeed;
            }
            else if (input.y < 0)
            {
                // Geriye ve geriye çapraz gitme durumunda yürüme hızı
                targetSpeed = backwardWalkSpeed;
            }
            else if (input.y > 0)
            {
                // İleri (W) ve ileri çapraz (W+A, W+D) gitme durumlarında koşmaya izin ver
                targetSpeed = _inputHandler.IsSprinting ? sprintSpeed : walkSpeed;
            }
            else
            {
                // Sadece yanlara (A veya D) basılıyorsa (input.y == 0)
                targetSpeed = walkSpeed;
            }

            Vector3 targetVelocity = moveDirection * targetSpeed;

            if (_isGrounded)
            {
                _currentHorizontalVelocity = targetVelocity;
            }
            else
            {
                if (input != Vector2.zero)
                {
                    _currentHorizontalVelocity = Vector3.Lerp(_currentHorizontalVelocity, targetVelocity, airControl * Time.deltaTime * 10f);
                }
            }

            _controller.Move(_currentHorizontalVelocity * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (_isGrounded && _velocity.y < 0) _velocity.y = -2f;
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleJump()
        {
            // Eğilirken zıplamayı yasakla
            if (_isGrounded && !_isCrouching)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        private void PerformGroundCheck()
        {
            Vector3 center = _controller.bounds.center;
            float bottomY = center.y - _controller.bounds.extents.y + _controller.radius;
            Vector3 sphereCastOrigin = new Vector3(center.x, bottomY, center.z);

            _isGrounded = Physics.SphereCast(origin: sphereCastOrigin, radius: _controller.radius * 0.9f, direction: Vector3.down, out RaycastHit hit, maxDistance: groundCheckDistance, layerMask: groundLayerMask, queryTriggerInteraction: QueryTriggerInteraction.Ignore);
        }
    }
}