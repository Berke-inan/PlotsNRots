using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // Coroutine için gerekli

public class WrenchController : MonoBehaviour
{
    [Header("Tamir Ayarlarý")]
    public float repairRange = 3f;
    [Tooltip("Saniyede deðil, artýk her tok vuruþta ne kadar tamir edeceði")]
    public float repairAmountPerHit = 15f;

    [Header("Animasyon Ayarlarý (Eski Oyun Tarzý)")]
    public float beklemeSuresi = 0.5f;
    [Tooltip("Anahtarýn hangi yöne büküleceði (X, Y veya Z)")]
    public Vector3 donusAcisi = new Vector3(0f, 0f, 45f);
    public float animasyonSuresi = 0.3f;

    [Header("Sesler")]
    public AudioClip ratchetSound;
    private AudioSource audioSource;

    private Quaternion initialLocalRotation;
    private bool isAnimating = false;
    private float sonVurusZamani = 0f;

    private void Start()
    {
        initialLocalRotation = transform.localRotation;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
    }

    private void Update()
    {
        // GÜVENLÝK KÝLÝDÝ: Öylesine sahneye atýlmýþsa çalýþma
        if (transform.parent == null) return;

        // Sol týka basýlý tutulduðu sürece (Eski oyundaki gibi ritmik vuracak)
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            // Bekleme süresi dolduysa ve animasyon o an oynamýyorsa yeni bir vuruþ baþlat
            if (!isAnimating && Time.time - sonVurusZamani >= beklemeSuresi)
            {
                sonVurusZamani = Time.time;
                StartCoroutine(BilekHareketiAnimasyonu());
                TryRepair();
            }
        }
        else
        {
            // Sol týka basýlmýyorken, PlayerInteraction'ýn açýyý düzeltmesine izin ver
            if (!isAnimating)
            {
                initialLocalRotation = transform.localRotation;
            }
        }
    }

    // ESKÝ OYUNUNDAKÝ TOK ANÝMASYON SÝSTEMÝ
    private IEnumerator BilekHareketiAnimasyonu()
    {
        isAnimating = true;
        Quaternion hedefRotasyon = initialLocalRotation * Quaternion.Euler(donusAcisi);

        float gecenZaman = 0f;
        float yariSure = animasyonSuresi / 2f;

        // Cýrcýr sesi tam animasyon baþlarken çalsýn
        if (ratchetSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(ratchetSound);
        }

        // 1. AÞAMA: Somunu sýkma (Bükülme)
        while (gecenZaman < yariSure)
        {
            gecenZaman += Time.deltaTime;
            float t = gecenZaman / yariSure;
            transform.localRotation = Quaternion.Slerp(initialLocalRotation, hedefRotasyon, t);
            yield return null; // Bir sonraki kareyi bekle
        }

        // 2. AÞAMA: Geri çekilme (Orijinal konuma dönüþ)
        gecenZaman = 0f;
        while (gecenZaman < yariSure)
        {
            gecenZaman += Time.deltaTime;
            float t = gecenZaman / yariSure;
            transform.localRotation = Quaternion.Slerp(hedefRotasyon, initialLocalRotation, t);
            yield return null;
        }

        // Titremeleri önlemek için en son rotasyonu zorla sabitle
        transform.localRotation = initialLocalRotation;
        isAnimating = false;
    }

    private void TryRepair()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, repairRange))
        {
            // Yeni sistemimizdeki aracý bul ve tamir et
            VehicleDurability brokenVehicle = hit.collider.GetComponentInParent<VehicleDurability>();
            if (brokenVehicle != null && brokenVehicle.isBroken)
            {
                brokenVehicle.RepairVehicle(repairAmountPerHit);
            }
        }
    }
}