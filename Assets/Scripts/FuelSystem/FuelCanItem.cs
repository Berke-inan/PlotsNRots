using UnityEngine;
using UnityEngine.InputSystem;

namespace GasSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class FuelCanItem : MonoBehaviour
    {
        [Header("Fuel Can Settings")]
        public float maxCapacity = 20f;
        public float pourSpeed = 5f;

        // KOPYALANIP SIFIRLANMAYI ENGELLEYEN GLOBAL HAFIZA
        public static float globalSavedCapacity = -1f;
        private float currentCapacity;

        [Header("Input (Hold to Pour)")]
        public InputActionReference interactAction;

        [Header("Interaction Settings")]
        public float interactRange = 4f;

        [Header("Audio & Effects")]
        public AudioClip pourSound;
        private AudioSource audioSource;

        private ProfessionalFuelUI fuelUI;
        private bool isPouringInput = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;

            fuelUI = FindFirstObjectByType<ProfessionalFuelUI>();

            // HAFIZA KONTROLÜ: Eğer oyun yeni başladıysa (-1 ise) full doldur, değilse eskiyi hatırla.
            if (globalSavedCapacity < 0)
            {
                globalSavedCapacity = maxCapacity;
            }
            currentCapacity = globalSavedCapacity;
        }

        void OnEnable()
        {
            if (fuelUI != null)
            {
                fuelUI.ToggleUIVisibility(true);
                fuelUI.UpdateFuelUI(currentCapacity / maxCapacity);
            }

            if (interactAction != null)
            {
                interactAction.action.Enable();
                interactAction.action.started += OnInteractStarted;
                interactAction.action.canceled += OnInteractCanceled;
            }
        }

        void OnDisable()
        {
            isPouringInput = false;
            StopPouring();

            if (fuelUI != null)
            {
                fuelUI.ToggleUIVisibility(false);
                fuelUI.ToggleTargetUIVisibility(false); // Eşyayı bırakınca traktör barını da kapat
            }

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
            // Tuşa basılmıyorsa veya bidon boşsa işlemi durdur
            if (!isPouringInput || currentCapacity <= 0)
            {
                StopPouring();
                if (fuelUI != null) fuelUI.ToggleTargetUIVisibility(false); // Tuşu bırakınca Traktör barını kapat
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                IRefuelable target = hit.collider.GetComponentInParent<IRefuelable>();

                if (target != null && target.CurrentFuel < target.MaxFuel && target.IsStationary)
                {
                    float amountToPour = pourSpeed * Time.deltaTime;
                    if (amountToPour > currentCapacity) amountToPour = currentCapacity;

                    target.AddFuel(amountToPour);

                    // Bidondan düş ve Global Hafızaya kaydet
                    currentCapacity -= amountToPour;
                    globalSavedCapacity = currentCapacity;

                    // Arayüzleri Güncelle
                    if (fuelUI != null)
                    {
                        fuelUI.UpdateFuelUI(currentCapacity / maxCapacity);

                        // Traktör UI'ını GÖSTER ve GÜNCELLE
                        fuelUI.ToggleTargetUIVisibility(true);
                        fuelUI.UpdateTargetFuelUI(target.CurrentFuel / target.MaxFuel);
                    }

                    if (!audioSource.isPlaying && pourSound != null)
                    {
                        audioSource.clip = pourSound;
                        audioSource.Play();
                    }
                }
                else
                {
                    StopPouring();
                    if (fuelUI != null) fuelUI.ToggleTargetUIVisibility(false); // Yanlış yere bakıyorsa gizle
                }
            }
            else
            {
                StopPouring();
                if (fuelUI != null) fuelUI.ToggleTargetUIVisibility(false); // Havaya bakıyorsa gizle
            }
        }

        private void StopPouring()
        {
            if (audioSource.isPlaying) audioSource.Stop();
        }
    }
}