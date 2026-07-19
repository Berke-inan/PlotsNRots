using UnityEngine;

[RequireComponent(typeof(VehicleController))]
public class VehicleAudio : MonoBehaviour
{
    [Header("Motor ve Vites Ayarlarý")]
    public int numberOfGears = 3;
    public float minPitch = 0.7f;
    public float maxPitch = 2.2f;
    public float pitchSmoothness = 5f;
    public float fadeSpeed = 3f;

    [Header("Ses Dosyalarý")]
    public AudioClip startClip;
    public AudioClip idleClip;
    public AudioClip engineLoopClip;

    private AudioSource startSource, idleSource, engineSource;
    private VehicleController vehicle;
    private bool hasStarted = false;
    private float smoothedSpeed;

    private void Start()
    {
        vehicle = GetComponent<VehicleController>();

        startSource = gameObject.AddComponent<AudioSource>();
        idleSource = gameObject.AddComponent<AudioSource>();
        engineSource = gameObject.AddComponent<AudioSource>();

        startSource.playOnAwake = false; idleSource.playOnAwake = false; engineSource.playOnAwake = false;
        idleSource.clip = idleClip; idleSource.loop = true;
        engineSource.clip = engineLoopClip; engineSource.loop = true;

        idleSource.volume = 0f; engineSource.volume = 0f;
    }

    private void Update()
    {
        if (vehicle.isPlayerInside)
        {
            if (!hasStarted)
            {
                if (startClip != null) startSource.PlayOneShot(startClip);
                idleSource.Play(); engineSource.Play();
                hasStarted = true;
            }
            UpdateSounds();
        }
        else
        {
            FadeOut();
            if (hasStarted && idleSource.volume < 0.01f)
            {
                idleSource.Stop(); engineSource.Stop();
                hasStarted = false;
            }
        }
    }

    private void UpdateSounds()
    {
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, vehicle.currentSpeed, Time.deltaTime * 5f);

        // Ses sistemi artýk sadece W'ye deðil, arabanýn "Güç Üretip Üretmediðine" bakar (Geri gidiþ düzeltildi)
        float targetEngineVolume = vehicle.IsAccelerating ? 0.8f : 0.2f;

        idleSource.volume = Mathf.Lerp(idleSource.volume, 0.4f, Time.deltaTime * fadeSpeed);
        engineSource.volume = Mathf.Lerp(engineSource.volume, targetEngineVolume, Time.deltaTime * fadeSpeed);

        float activeMaxSpeed = vehicle.IsReversing ? vehicle.maxReverseSpeed : vehicle.maxSpeed;
        float speedRatio = Mathf.Clamp01(smoothedSpeed / activeMaxSpeed);

        if (speedRatio >= 0.98f && !vehicle.IsReversing)
        {
            float revLimiterPitch = maxPitch - (Mathf.Sin(Time.time * 40f) * 0.15f);
            engineSource.pitch = Mathf.Lerp(engineSource.pitch, revLimiterPitch, Time.deltaTime * 20f);
            return;
        }

        float gearFactor = 1f / numberOfGears;
        int currentGear = Mathf.FloorToInt(speedRatio / gearFactor);
        if (currentGear >= numberOfGears) currentGear = numberOfGears - 1;

        float rpmRatio = (speedRatio - (currentGear * gearFactor)) / gearFactor;
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, rpmRatio);
        targetPitch += (currentGear * 0.05f);

        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * pitchSmoothness);
    }

    private void FadeOut()
    {
        idleSource.volume = Mathf.Lerp(idleSource.volume, 0f, Time.deltaTime * fadeSpeed);
        engineSource.volume = Mathf.Lerp(engineSource.volume, 0f, Time.deltaTime * fadeSpeed);
    }
}