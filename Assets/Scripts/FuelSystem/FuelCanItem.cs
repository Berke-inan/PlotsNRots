using UnityEngine;
using UnityEngine.InputSystem;

namespace GasSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class FuelCanItem : MonoBehaviour
    {
        [Header("Fuel Can Settings")]
        public float maxCapacity = 20f;
        public float currentCapacity = 20f;
        public float pourSpeed = 5f; // Saniyede kaç litre dolduracak

        [Header("Input (Hold to Pour)")]
        [Tooltip("Yeni Input Sistemindeki E tuşu (Interact) eylemini buraya sürükleyin")]
        public InputActionReference interactAction;

        [Header("Interaction Settings")]
        public float interactRange = 4f;

        [Header("Audio & Effects")]
        public AudioClip pourSound;
        private AudioSource audioSource;

        // UI'ı otomatik bulmak için referans
        private ProfessionalFuelUI fuelUI;
        private bool isPouringInput = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true; // Basılı tuttukça ses döngüye girsin

            fuelUI = FindFirstObjectByType<ProfessionalFuelUI>();
        }

        // Objeyi eline aldığında çalışır
        void OnEnable()
        {
            if (fuelUI != null)
            {
                fuelUI.ToggleUIVisibility(true);
                fuelUI.UpdateFuelUI(currentCapacity / maxCapacity);
            }

            // Input sistemini uyanmaya zorla ve basılı tutma olaylarını dinle
            if (interactAction != null)
            {
                interactAction.action.Enable();
                interactAction.action.started += OnInteractStarted; // Tuşa basıldı
                interactAction.action.canceled += OnInteractCanceled; // Tuştan el çekildi
            }
        }

        // Başka eşyaya geçildiğinde veya yere atıldığında
        void OnDisable()
        {
            isPouringInput = false;
            StopPouring();

            if (fuelUI != null) fuelUI.ToggleUIVisibility(false);

            if (interactAction != null)
            {
                interactAction.action.started -= OnInteractStarted;
                interactAction.action.canceled -= OnInteractCanceled;
            }
        }

        private void OnInteractStarted(InputAction.CallbackContext ctx) => isPouringInput = true;
        private void OnInteractCanceled(InputAction.CallbackContext ctx) => isPouringInput = false;

        void Update()
        {
            // Tuşa basılmıyorsa veya bidon boşsa doldurmayı durdur
            if (!isPouringInput || currentCapacity <= 0)
            {
                StopPouring();
                return;
            }

            // Ekranın tam ortasından (Crosshair'dan) ışın yolla
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                IRefuelable target = hit.collider.GetComponentInParent<IRefuelable>();

                if (target != null && target.CurrentFuel < target.MaxFuel && target.IsStationary)
                {
                    // Basılı tuttukça saniyede pourSpeed (5 litre) kadar benzin ver
                    float amountToPour = pourSpeed * Time.deltaTime;
                    if (amountToPour > currentCapacity) amountToPour = currentCapacity;

                    target.AddFuel(amountToPour);
                    currentCapacity -= amountToPour;

                    // UI'ı anlık güncelle
                    if (fuelUI != null) fuelUI.UpdateFuelUI(currentCapacity / maxCapacity);

                    // Sesi başlat
                    if (!audioSource.isPlaying && pourSound != null)
                    {
                        audioSource.clip = pourSound;
                        audioSource.Play();
                    }
                }
                else
                {
                    StopPouring(); // Bakıyor ama hedef traktör değil veya depo dolu
                }
            }
            else
            {
                StopPouring(); // Havaya bakıyor
            }
        }

        private void StopPouring()
        {
            if (audioSource.isPlaying) audioSource.Stop();
        }
    }
}