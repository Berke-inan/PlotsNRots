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

        // YENİ: Oyuncunun tuşa basılı tutup tutmadığını takip eden bayrak
        private bool isHoldingInteractButton = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
        }

        void OnEnable()
        {
            interactAction.action.started += OnInteractStarted;
            interactAction.action.canceled += OnInteractCanceled;
        }

        void OnDisable()
        {
            interactAction.action.started -= OnInteractStarted;
            interactAction.action.canceled -= OnInteractCanceled;
        }

        private void OnTriggerEnter(Collider other)
        {
            IRefuelable refuelableObj = other.GetComponentInParent<IRefuelable>();
            if (refuelableObj != null)
            {
                objectInZone = refuelableObj;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            IRefuelable refuelableObj = other.GetComponentInParent<IRefuelable>();
            if (refuelableObj != null && objectInZone == refuelableObj)
            {
                objectInZone = null;
                isHoldingInteractButton = false; // Alandan çıkınca tuş basımını sıfırla
                StopRefuelingProcess();
            }
        }

        // --- YENİ INPUT YÖNETİMİ ---
        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            isHoldingInteractButton = true;
        }

        private void OnInteractCanceled(InputAction.CallbackContext context)
        {
            isHoldingInteractButton = false;
            StopRefuelingProcess();
        }

        // --- SÜREKLİ GÜVENLİK KONTROLÜ (Oyunun Fizik Döngüsü) ---
        void Update()
        {
            // Eğer oyuncu alandaysa ve E tuşuna basılı tutuyorsa...
            if (objectInZone != null && isHoldingInteractButton)
            {
                // 1. Araç DURUYORSA ve dolum henüz BAŞLAMADIYSA -> Başlat
                if (objectInZone.IsStationary && refuelCoroutine == null && objectInZone.CurrentFuel < objectInZone.MaxFuel)
                {
                    refuelCoroutine = StartCoroutine(RefuelProcess());
                    if (refuelSound != null) { audioSource.clip = refuelSound; audioSource.Play(); }
                    onRefuelStart?.Invoke();
                }
                // 2. Araç HAREKET ETTİYSE ve dolum YAPILIYORSA -> Anında durdur
                else if (!objectInZone.IsStationary && refuelCoroutine != null)
                {
                    Debug.Log("Araç hareket ettiği için dolum iptal edildi!");
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
            }
        }

        private IEnumerator RefuelProcess()
        {
            float fillSpeedPerSecond = objectInZone.MaxFuel / timeToFillFullTank;

            // Güvenlik kontrollerini Update() üzerine aldığımız için burada sadece doldurma yapıyoruz
            while (objectInZone != null && objectInZone.CurrentFuel < objectInZone.MaxFuel)
            {
                objectInZone.AddFuel(fillSpeedPerSecond * Time.deltaTime);
                yield return null;
            }

            // Depo dolunca durdur
            StopRefuelingProcess();
        }
    }
}