using UnityEngine;
using PlotNRots.SaveSystem;

[RequireComponent(typeof(VehicleController), typeof(SaveableEntity))]
public class VehicleDurability : MonoBehaviour, ISaveable
{
    [Header("Bozulma Ayarları")]
    public float maxDistanceBeforeBreakdown = 1500f;

    [Header("Canlı Veriler (Kayıt Sistemine Gider)")]
    public float currentDistance = 0f;
    public bool isBroken = false;

    [Header("Tamir Ayarları")]
    public float maxRepairHealth = 100f;
    private float currentRepairProgress = 0f;

    [Header("Sesler")]
    public AudioClip breakdownClip;
    public AudioClip repairCompleteClip;
    private AudioSource audioSource;

    [Header("Görsel Efektler (Duman)")]
    [Tooltip("Motorun altına yerleştireceğiniz siyah duman efekti")]
    public ParticleSystem blackSmokeParticle;
    [Tooltip("Bozulduğu ilk an egzozdan atılacak devasa duman patlaması miktarı")]
    public int initialBurstAmount = 60;
    [Tooltip("Araç bozukken durduğu yerde çıkan duman yoğunluğu")]
    public float idleSmokeRate = 8f;
    [Tooltip("Araç bozukken zorlanıp giderken çıkan maksimum duman yoğunluğu")]
    public float maxMovingSmokeRate = 35f;

    private VehicleController vehicle;

    [System.Serializable]
    public struct DurabilityData
    {
        public float savedDistance;
        public bool savedIsBroken;
    }

    private void Awake()
    {
        vehicle = GetComponent<VehicleController>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
    }

    private void Start()
    {
        // Oyun ilk başladığında dumanın kapalı olduğundan emin ol
        if (blackSmokeParticle != null && !isBroken)
        {
            blackSmokeParticle.Stop();
        }
        else if (blackSmokeParticle != null && isBroken)
        {
            blackSmokeParticle.Play(); // Save'den bozuk yüklendiyse dumanı başlat
        }
    }

    private void Update()
    {
        if (isBroken)
        {
            // Aracın eylemsizlikle ağır ağır durmasını sağlayan kısım
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 3f);
            }

            // --- YENİ: DİNAMİK DUMAN KONTROLÜ ---
            if (blackSmokeParticle != null)
            {
                // Parçacık sisteminin Emission (Yayım) modülüne kodla ulaşıyoruz
                var emission = blackSmokeParticle.emission;

                // Aracın hızına göre duman yoğunluğunu rölanti(8) ile maks(35) arasında hesapla
                // Arabanın bozukken sürünme hızını ortalama 10 km/h kabul ederek orantılıyoruz
                float targetRate = Mathf.Lerp(idleSmokeRate, maxMovingSmokeRate, vehicle.currentSpeed / 10f);

                emission.rateOverTime = targetRate;
            }
            return;
        }

        // Araç hareket ediyorsa mesafeyi kaydet
        if (vehicle.isPlayerInside && vehicle.currentSpeed > 0.1f)
        {
            currentDistance += (vehicle.currentSpeed / 3.6f) * Time.deltaTime;

            if (currentDistance >= maxDistanceBeforeBreakdown)
            {
                BreakDown();
            }
        }
    }

    private void BreakDown()
    {
        isBroken = true;
        currentRepairProgress = 0f;
        if (breakdownClip != null) audioSource.PlayOneShot(breakdownClip);

        // --- YENİ: İLK BOZULMA PATLAMASI ---
        if (blackSmokeParticle != null)
        {
            blackSmokeParticle.Play(); // Normal akışı başlat
            blackSmokeParticle.Emit(initialBurstAmount); // Anında 60 adet ekstra duman parçasını dışarı fırlat!
        }

        Debug.Log($"<color=red>{gameObject.name} arızalandı! Motor duman atıyor.</color>");
    }

    public void RepairVehicle(float repairAmount)
    {
        if (!isBroken) return;

        currentRepairProgress += repairAmount;

        if (currentRepairProgress >= maxRepairHealth)
        {
            isBroken = false;
            currentDistance = 0f;
            currentRepairProgress = 0f;

            if (repairCompleteClip != null) audioSource.PlayOneShot(repairCompleteClip);

            // --- YENİ: TAMİR BİTİNCE DUMANI KES ---
            if (blackSmokeParticle != null)
            {
                blackSmokeParticle.Stop();
            }

            Debug.Log($"<color=green>{gameObject.name} başarıyla tamir edildi!</color>");
        }
    }

    // ==========================================
    // ISAVEABLE IMPLEMENTASYONU (SENİN SİSTEMİN)
    // ==========================================
    public object SaveState()
    {
        return new DurabilityData
        {
            savedDistance = this.currentDistance,
            savedIsBroken = this.isBroken
        };
    }

    public void LoadState(object state)
    {
        var data = (DurabilityData)state;

        this.currentDistance = data.savedDistance;
        this.isBroken = data.savedIsBroken;
    }
}