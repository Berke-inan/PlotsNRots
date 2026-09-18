using UnityEngine;
using UnityEngine.Events;

namespace GasSystem
{
    [RequireComponent(typeof(Rigidbody))]
    public class FuelSystem : MonoBehaviour, IRefuelable
    {
        [Header("Fuel Settings")]
        public float maxFuel = 100f;
        public float currentFuel;

        // --- IRefuelable SÖZLEŞMESİ GEREKSİNİMLERİ ---
        public float CurrentFuel => currentFuel;
        public float MaxFuel => maxFuel;
        public bool IsStationary => rb.linearVelocity.magnitude <= movementThreshold;
        // ---------------------------------------------

        [Header("Consumption Settings")]
        public float idleConsumptionRate = 0.5f;
        public float movingConsumptionRate = 3.0f;
        public bool isEngineRunning = true;

        [Header("Movement Detection")]
        public float movementThreshold = 1.0f;
        private Rigidbody rb;

        [Header("Events (UI)")]
        public UnityEvent<float> OnFuelPercentageChanged;
        public UnityEvent OnFuelEmpty;

        void Start()
        {
            currentFuel = maxFuel;
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (isEngineRunning && currentFuel > 0)
            {
                bool isMoving = rb.linearVelocity.magnitude > movementThreshold;
                float currentRate = isMoving ? movingConsumptionRate : idleConsumptionRate;

                currentFuel -= currentRate * Time.deltaTime;

                if (currentFuel <= 0)
                {
                    currentFuel = 0;
                    isEngineRunning = false;
                    OnFuelEmpty?.Invoke();
                }

                OnFuelPercentageChanged?.Invoke(currentFuel / maxFuel);
            }
        }

        // Bu fonksiyon Interface'deki AddFuel(float amount) imzasını karşılıyor
        public void AddFuel(float amount)
        {
            currentFuel += amount;
            if (currentFuel > maxFuel) currentFuel = maxFuel;

            if (currentFuel > 0) isEngineRunning = true;

            OnFuelPercentageChanged?.Invoke(currentFuel / maxFuel);
        }
    }
}