using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PlotNRots.Items;

namespace GasSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class FuelCanItem : MonoBehaviour, IUsable, IRefuelable
    {
        [Header("Benzin Bidonu Ayarları")]
        public float maxCapacity = 20f;
        public float pourSpeed = 5f;
        public string uniqueFuelCanID = "MainFuelCan";
        public float interactRange = 4f;

        public float CurrentFuel => currentCapacity;
        public float MaxFuel => maxCapacity;
        public bool IsStationary => true;

        private static Dictionary<string, float> sessionSavedCapacities = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionMemory()
        {
            sessionSavedCapacities.Clear();
        }

        private float currentCapacity;
        private bool isPouring = false;

        [Header("Ses ve Arayüz")]
        public AudioClip pourSound;
        private AudioSource audioSource;
        private ProfessionalFuelUI fuelUI;

        private readonly Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;

            fuelUI = GetComponent<ProfessionalFuelUI>();

            if (!sessionSavedCapacities.ContainsKey(uniqueFuelCanID))
            {
                sessionSavedCapacities[uniqueFuelCanID] = maxCapacity;
            }

            currentCapacity = sessionSavedCapacities[uniqueFuelCanID];
        }

        void Start() => UpdateUI();
        void OnEnable() => UpdateUI();
        void OnDisable()
        {
            SaveFuelData();
            StopPouring();
        }

        public void Use()
        {
            isPouring = true;
        }

        void Update()
        {
            bool isHolding = Mouse.current != null && Mouse.current.leftButton.isPressed;

            if (!isPouring || !isHolding || currentCapacity <= 0)
            {
                if (isPouring) StopPouring();
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(screenCenter);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                IRefuelable target = hit.collider.GetComponentInParent<IRefuelable>();

                if (target != null && target.IsStationary)
                {
                    // YENİ: Traktörün ne kadar benzine ihtiyacı var?
                    float neededFuel = target.MaxFuel - target.CurrentFuel;

                    // Eğer ihtiyaç yoksa veya %100 doluysa ANINDA DURDUR
                    if (neededFuel <= 0.01f)
                    {
                        StopPouring();
                        return;
                    }

                    float amountToPour = pourSpeed * Time.deltaTime;

                    // YENİ: Hem bidondaki yakıttan fazlasını dökemez, hem de hedefin ihtiyacından fazlasını dökemez! (Taşmayı/Boşa gitmeyi önler)
                    if (amountToPour > currentCapacity) amountToPour = currentCapacity;
                    if (amountToPour > neededFuel) amountToPour = neededFuel;

                    target.AddFuel(amountToPour);
                    currentCapacity -= amountToPour;

                    UpdateUI();

                    // YENİ: Traktörün UI'ını bul ve oyuncuya ne kadar dolduğunu geçici olarak göster!
                    FuelUI targetUI = hit.collider.GetComponentInParent<FuelUI>();
                    if (targetUI != null) targetUI.ShowTemporarily(1.0f);

                    if (!audioSource.isPlaying && pourSound != null)
                    {
                        audioSource.clip = pourSound;
                        audioSource.Play();
                    }
                }
                else StopPouring();
            }
            else StopPouring();
        }

        private void StopPouring()
        {
            isPouring = false;
            if (audioSource.isPlaying) audioSource.Stop();
            SaveFuelData();
        }

        private void SaveFuelData()
        {
            if (currentCapacity >= 0)
            {
                sessionSavedCapacities[uniqueFuelCanID] = currentCapacity;
            }
        }

        private void UpdateUI()
        {
            if (fuelUI != null) fuelUI.UpdateFuelUI(currentCapacity / maxCapacity);
        }

        public void AddFuel(float amount)
        {
            currentCapacity += amount;
            if (currentCapacity > maxCapacity) currentCapacity = maxCapacity;
            UpdateUI();
        }
    }
}