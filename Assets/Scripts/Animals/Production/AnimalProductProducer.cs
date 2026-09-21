using UnityEngine;

[DisallowMultipleComponent]
public class AnimalProductProducer : MonoBehaviour
{
    [Header("Production Configuration")]
    [SerializeField] private AnimalProductProfile productProfile;
    [SerializeField] private Transform harvestOutputPoint;

    [Header("Time Source")]
    [Tooltip("May remain empty when the scene uses DayNightCycleManager.Instance.")]
    [SerializeField] private DayNightCycleManager dayNightCycleManager;

    private bool producedToday;
    private float previousTime;
    private bool timeInitialized;

    private void OnEnable()
    {
        DayNightCycleManager.YeniGunBasladiSinyali += HandleNewDay;
        ResolveTimeManager();
        InitializeTimeState();
    }

    private void OnDisable()
    {
        DayNightCycleManager.YeniGunBasladiSinyali -= HandleNewDay;
    }

    private void Update()
    {
        if (!ResolveTimeManager() || productProfile == null)
        {
            return;
        }

        float currentTime = dayNightCycleManager.currentTime;

        if (!timeInitialized)
        {
            previousTime = currentTime;
            timeInitialized = true;
            return;
        }

        // This also handles a day rollover if the clock reaches 24 and wraps to 0.
        if (currentTime < previousTime)
        {
            producedToday = false;
        }

        if (!producedToday && CrossedTime(previousTime, currentTime, productProfile.ProductionTime))
        {
            Produce();
        }

        previousTime = currentTime;
    }

    private bool ResolveTimeManager()
    {
        if (dayNightCycleManager != null)
        {
            return true;
        }

        dayNightCycleManager = DayNightCycleManager.Instance;
        return dayNightCycleManager != null;
    }

    private void InitializeTimeState()
    {
        if (!ResolveTimeManager())
        {
            timeInitialized = false;
            return;
        }

        previousTime = dayNightCycleManager.currentTime;
        timeInitialized = true;

        // Entering Play Mode after today's production time must not create an
        // immediate product. Production occurs only when the clock crosses it.
        producedToday = productProfile != null &&
                        previousTime >= productProfile.ProductionTime;
    }

    private void HandleNewDay()
    {
        producedToday = false;

        if (dayNightCycleManager != null)
        {
            previousTime = dayNightCycleManager.currentTime;
            timeInitialized = true;
        }
    }

    private static bool CrossedTime(float previous, float current, float target)
    {
        if (current >= previous)
        {
            return previous < target && current >= target;
        }

        // Clock wrapped through midnight.
        return target > previous || target <= current;
    }

    private void Produce()
    {
        if (productProfile.WorldProductPrefab == null)
        {
            Debug.LogWarning($"{name} cannot produce: no World Product Prefab is assigned.", this);
            return;
        }

        Transform output = harvestOutputPoint != null ? harvestOutputPoint : transform;
        Vector3 basePosition = output.TransformPoint(productProfile.LocalSpawnOffset);

        for (int i = 0; i < productProfile.Quantity; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * productProfile.RandomHorizontalOffset;
            Vector3 spawnPosition = basePosition + new Vector3(randomOffset.x, 0f, randomOffset.y);

            Instantiate(
                productProfile.WorldProductPrefab,
                spawnPosition,
                output.rotation);
        }

        producedToday = true;
    }

    private void OnValidate()
    {
        if (harvestOutputPoint == null)
        {
            Transform candidate = transform.Find("HarvestOutputPoint");
            if (candidate != null)
            {
                harvestOutputPoint = candidate;
            }
        }
    }
}
