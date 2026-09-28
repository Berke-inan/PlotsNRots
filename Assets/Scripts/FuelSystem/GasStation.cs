using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace GasSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class GasStation : MonoBehaviour
    {
        [Header("Settings")]
        public float timeToFillFullTank = 2.0f;

        [Header("Audio")]
        public AudioClip refuelSound;
        private AudioSource audioSource;

        [Header("UI Events")]
        public UnityEvent onRefuelStart;
        public UnityEvent onRefuelStop;

        private IRefuelable objectInZone;
        private bool isRefueling = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
        }

        // AKILLI TARAMA SİSTEMİ: Hem araçları (Parent) hem de oyuncunun elindeki bidonu (Child) bulur.
        private IRefuelable DetectRefuelable(Collider other)
        {
            IRefuelable target = other.GetComponentInParent<IRefuelable>();
            if (target == null)
            {
                target = other.GetComponentInChildren<IRefuelable>();
            }
            return target;
        }

        private void OnTriggerEnter(Collider other)
        {
            IRefuelable refuelableObj = DetectRefuelable(other);
            if (refuelableObj != null)
            {
                objectInZone = refuelableObj;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            IRefuelable refuelableObj = DetectRefuelable(other);
            if (refuelableObj != null && objectInZone == refuelableObj)
            {
                objectInZone = null;
                StopRefuelingProcess();
            }
        }

        void Update()
        {
            if (objectInZone == null) return;

            // E TUŞU YERİNE SOL TIK (MOUSE LEFT BUTTON) KONTROLÜ EKLENDİ
            bool isHoldingInteract = Mouse.current != null && Mouse.current.leftButton.isPressed;

            if (isHoldingInteract)
            {
                if (objectInZone.IsStationary && objectInZone.CurrentFuel < objectInZone.MaxFuel)
                {
                    if (!isRefueling) StartRefuelingProcess();

                    float fillSpeedPerSecond = objectInZone.MaxFuel / timeToFillFullTank;
                    objectInZone.AddFuel(fillSpeedPerSecond * Time.deltaTime);
                }
                else if (isRefueling)
                {
                    StopRefuelingProcess();
                }
            }
            else if (isRefueling)
            {
                StopRefuelingProcess();
            }
        }

        private void StartRefuelingProcess()
        {
            isRefueling = true;
            if (refuelSound != null)
            {
                audioSource.clip = refuelSound;
                if (!audioSource.isPlaying) audioSource.Play();
            }
            onRefuelStart?.Invoke();
        }

        private void StopRefuelingProcess()
        {
            if (isRefueling)
            {
                isRefueling = false;
                audioSource.Stop();
                onRefuelStop?.Invoke();
            }
        }
    }
}