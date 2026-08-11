using UnityEngine;

public class TractorExhaust : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("Assign the Exhaust Particle System here.")]
    public ParticleSystem exhaustParticle;

    [Tooltip("Assign the Tractor's Rigidbody here.")]
    public Rigidbody tractorRigidbody;

    [Header("Engine Status")]
    [Tooltip("Is the player inside and the engine running?")]
    public bool isEngineRunning = false; // Ba�lang��ta motor kapal�

    [Header("Smoke Emission Rates")]
    public float idleEmissionRate = 5f;
    public float movingEmissionRate = 30f;

    [Header("Smoke Colors")]
    public Color idleSmokeColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
    public Color movingSmokeColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.MainModule mainModule;

    void Start()
    {
        if (exhaustParticle != null)
        {
            emissionModule = exhaustParticle.emission;
            mainModule = exhaustParticle.main;

            // Oyun ba�lad���nda motor kapal�ysa duman� s�f�rla
            if (!isEngineRunning)
            {
                emissionModule.rateOverTime = 0f;
            }
        }
        else
        {
            Debug.LogWarning("Exhaust Particle System is not assigned on " + gameObject.name);
        }
    }

    void Update()
    {
        if (exhaustParticle == null || tractorRigidbody == null) return;

        // E�ER MOTOR KAPALIYSA (Oyuncu d��ar�daysa)
        if (!isEngineRunning)
        {
            // Duman �retimini tamamen durdur ve fonksiyondan ��k
            emissionModule.rateOverTime = 0f;
            return;
        }

        // MOTOR �ALI�IYORSA H�z kontrol� yap
        float currentSpeed = tractorRigidbody.linearVelocity.magnitude;

        if (currentSpeed > 0.1f)
        {
            // Hareket halinde
            emissionModule.rateOverTime = movingEmissionRate;
            mainModule.startColor = movingSmokeColor;
        }
        else
        {
            // R�lantide duruyor
            emissionModule.rateOverTime = idleEmissionRate;
            mainModule.startColor = idleSmokeColor;
        }
    }

    // Bu metodu karakterin trakt�re bindi�i/indi�i scriptten �a��rabilirsin
    public void SetEngineState(bool state)
    {
        isEngineRunning = state;
    }
}