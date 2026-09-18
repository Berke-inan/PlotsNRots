using UnityEngine;
using PlotNRots.SaveSystem;
using System;

public class PlayerStats : MonoBehaviour, ISaveable
{
    [Header("Mutlak Sýnýrlar")]
    public float absoluteMaxEnergy = 100f;
    public float maxHealth = 100f;

    [Header("Güncel Deðerler")]
    [SerializeField] private float currentMaxEnergy = 100f;
    [SerializeField] private float currentEnergy = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Zamana Baðlý Azalma Ayarlarý (Saniyede)")]
    [Tooltip("Güncel enerji saniyede kaç azalacak?")]
    public float energyDrainPerSecond = 0.5f;
    [Tooltip("Maksimum enerji saniyede kaç azalacak? (Daha yavaþ olmalý)")]
    public float maxEnergyDrainPerSecond = 0.1f;

    // UI'ý tetiklemek için Event'ler
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float, float> OnEnergyChanged;

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        // Zamanla enerji azalmasý
        currentMaxEnergy -= maxEnergyDrainPerSecond * Time.deltaTime;
        currentEnergy -= energyDrainPerSecond * Time.deltaTime;

        // Sýnýrlandýrmalar
        if (currentMaxEnergy < 10f) currentMaxEnergy = 10f; // Max enerji belli bir seviyenin altýna düþmesin
        if (currentEnergy > currentMaxEnergy) currentEnergy = currentMaxEnergy;
        if (currentEnergy < 0f) currentEnergy = 0f;

        UpdateUI();
    }

    // ==========================================
    // TÜKETÝM VE EYLEM FONKSÝYONLARI
    // ==========================================
    public void ConsumeItem(ConsumableItemData item)
    {
        if (item.consumableType == ConsumableType.Food)
        {
            currentMaxEnergy += item.restoreAmount;
            if (currentMaxEnergy > absoluteMaxEnergy) currentMaxEnergy = absoluteMaxEnergy;
        }
        else if (item.consumableType == ConsumableType.Drink)
        {
            currentEnergy += item.restoreAmount;
            if (currentEnergy > currentMaxEnergy) currentEnergy = currentMaxEnergy;
        }
        UpdateUI();
    }

    // Ýleride aðaç kestiðinde vs. bu fonksiyonu çaðýracaksýn
    public bool TrySpendEnergy(float amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            UpdateUI();
            return true; // Eyleme izin ver
        }
        return false; // Enerji yetersiz
    }

    public void ReplenishStatsOnSleep()
    {
        currentMaxEnergy = absoluteMaxEnergy;
        currentEnergy = absoluteMaxEnergy;
        currentHealth = maxHealth;
        UpdateUI();
    }

    private void UpdateUI()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnEnergyChanged?.Invoke(currentEnergy, currentMaxEnergy, absoluteMaxEnergy);
    }

    // ==========================================
    // KAYIT SÝSTEMÝ (ISAVEABLE)
    // ==========================================
    [Serializable]
    private struct StatsSaveData
    {
        public float health, energy, maxEnergy;
    }

    public object SaveState()
    {
        return new StatsSaveData { health = currentHealth, energy = currentEnergy, maxEnergy = currentMaxEnergy };
    }

    public void LoadState(object state)
    {
        StatsSaveData data = (StatsSaveData)state;
        currentHealth = data.health;
        currentEnergy = data.energy;
        currentMaxEnergy = data.maxEnergy;
        UpdateUI();
    }
}