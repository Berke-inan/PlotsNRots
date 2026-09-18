using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace GasSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class GasStation : MonoBehaviour
    {
        [Header("Input")]
        public InputActionReference interactAction;

        [Header("Settings")]
        public float timeToFillFullTank = 2.0f;

        [Header("Audio")]
        public AudioClip refuelSound;
        private AudioSource audioSource;

        [Header("UI Events")]
        public UnityEvent onRefuelStart;
        public UnityEvent onRefuelStop;

        private IRefuelable objectInZone;
        private Coroutine refuelCoroutine;

        private bool isHoldingInteractButton = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
        }

        void OnEnable()
        {
            if (interactAction != null)
            {
                // ÇÖZÜM 1: Input Action'ı açıkça aktif etmeliyiz!
                interactAction.action.Enable();

                interactAction.action.started += OnInteractStarted;
                interactAction.action.canceled += OnInteractCanceled;
            }
        }

        void OnDisable()
        {
            if (interactAction != null)
            {
                interactAction.action.started -= OnInteractStarted;
                interactAction.action.canceled -= OnInteractCanceled;

                // Kapatırken de disable edelim ki hafıza sızıntısı olmasın
                interactAction.action.Disable();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            IRefuelable refuelableObj = other.GetComponentInParent<IRefuelable>();
            if (refuelableObj != null)
            {
                objectInZone = refuelableObj;
                Debug.Log("İstasyon: Araç dolum alanına GİRDİ.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            IRefuelable refuelableObj = other.GetComponentInParent<IRefuelable>();
            if (refuelableObj != null && objectInZone == refuelableObj)
            {
                objectInZone = null;
                isHoldingInteractButton = false;
                StopRefuelingProcess();
                Debug.Log("İstasyon: Araç dolum alanından ÇIKTI.");
            }
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            isHoldingInteractButton = true;
            Debug.Log("İstasyon: E tuşuna BASILDI.");
        }

        private void OnInteractCanceled(InputAction.CallbackContext context)
        {
            isHoldingInteractButton = false;
            StopRefuelingProcess();
            Debug.Log("İstasyon: E tuşundan ÇEKİLDİ.");
        }

        void Update()
        {
            if (objectInZone != null && isHoldingInteractButton)
            {
                // Araç duruyor mu ve depo boş mu kontrolü
                if (objectInZone.IsStationary && refuelCoroutine == null && objectInZone.CurrentFuel < objectInZone.MaxFuel)
                {
                    refuelCoroutine = StartCoroutine(RefuelProcess());
                    if (refuelSound != null) { audioSource.clip = refuelSound; audioSource.Play(); }
                    onRefuelStart?.Invoke();
                    Debug.Log("İstasyon: Dolum BAŞLADI.");
                }
                else if (!objectInZone.IsStationary && refuelCoroutine != null)
                {
                    Debug.Log("İstasyon: Araç hareket ettiği (veya titrediği) için dolum iptal edildi!");
                    StopRefuelingProcess();
                }
            }
        }

        private void StopRefuelingProcess()
        {
            if (refuelCoroutine != null)
            {
                StopCoroutine(refuelCoroutine);
                refuelCoroutine = null;
                audioSource.Stop();
                onRefuelStop?.Invoke();
                Debug.Log("İstasyon: Dolum DURDU.");
            }
        }

        private IEnumerator RefuelProcess()
        {
            float fillSpeedPerSecond = objectInZone.MaxFuel / timeToFillFullTank;

            while (objectInZone != null && objectInZone.CurrentFuel < objectInZone.MaxFuel)
            {
                objectInZone.AddFuel(fillSpeedPerSecond * Time.deltaTime);
                yield return null;
            }

            Debug.Log("İstasyon: Depo FULLENDİ.");
            StopRefuelingProcess();
        }
    }
}