using System.Collections.Generic;
using UnityEngine;

public class CloudGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct CloudMeshType
    {
        public Mesh mesh;
        public Material material;
    }

    [Header("1. Bulut Mesh Listesi")]
    [Tooltip("Elindeki 3 farklý bulut modelini (Mesh ve Materyalleriyle) buraya ekle.")]
    public List<CloudMeshType> bulutModelleri = new List<CloudMeshType>();

    [Header("2. Daðýlým ve Adet Ayarlarý")]
    [Range(5, 150)] public int bulutSayisi = 30;
    public Vector3 uretimAlani = new Vector3(200f, 20f, 200f);
    public float minimumBulutBoyutu = 5f;
    public float maksimumBulutBoyutu = 15f;

    [Header("3. Low-Poly Kontrol Parametreleri")]
    [Range(5f, 100f)]
    [Tooltip("Bulutlarýn birbirine yaklaþabileceði minimum mesafe. Deðeri düþürürseniz bulutlar sýklaþýr.")]
    public float bulutlarArasiMesafe = 20f;

    [Tooltip("Açýk olduðunda bulutlar birbirinin içinden geçebilir. Kapalýyken jilet gibi temiz, ayrý kümeler oluþur.")]
    public bool icIceGecmeyeIzinVer = false;

    [Header("4. Rüzgar (Hareket) Ayarlarý")]
    public float ruzgarHizi = 1f;
    public Vector3 hareketYonu = new Vector3(1f, 0f, 0f);

    // Üretilen bulutlarý hafýzada tutmak için
    private List<GameObject> uretilenBulutlar = new List<GameObject>();

    void Start()
    {
        BulutlariOlustur();
    }

    void Update()
    {
        // Rüzgar etkisi: Tüm bulut kümesini yavaþça kaydýrýr
        transform.Translate(hareketYonu.normalized * ruzgarHizi * Time.deltaTime);
    }

    [ContextMenu("Bulutlarý Yeniden Oluþtur")]
    public void BulutlariOlustur()
    {
        // Eski bulutlarý temizle (Editörde test ederken kolaylýk saðlar)
        foreach (var bulut in uretilenBulutlar)
        {
            if (bulut != null) Destroy(bulut);
        }
        uretilenBulutlar.Clear();

        if (bulutModelleri == null || bulutModelleri.Count == 0)
        {
            Debug.LogWarning("Lütfen CloudGenerator scriptine en az bir adet Bulut Meshi ekleyin!");
            return;
        }

        int denemeSayisi = 0;
        int olusanBulutSayisi = 0;

        // Sonsuz döngüye girmemesi için güvenli bir deneme sýnýrý koyuyoruz
        while (olusanBulutSayisi < bulutSayisi && denemeSayisi < bulutSayisi * 10)
        {
            denemeSayisi++;

            // Belirlenen alan içinde rastgele bir konum seç (Yüksekliði Manager objesinin yüksekliðini baz alýr)
            Vector3 rastgeleKonum = transform.position + new Vector3(
                Random.Range(-uretimAlani.x / 2f, uretimAlani.x / 2f),
                Random.Range(-uretimAlani.y / 2f, uretimAlani.y / 2f),
                Random.Range(-uretimAlani.z / 2f, uretimAlani.z / 2f)
            );

            // Eðer iç içe geçme KAPALIYSA, bu konumun yakýnýnda baþka bulut var mý diye kontrol et
            if (!icIceGecmeyeIzinVer)
            {
                bool cokYakin = false;
                foreach (var mevcutBulut in uretilenBulutlar)
                {
                    if (Vector3.Distance(rastgeleKonum, mevcutBulut.transform.position) < bulutlarArasiMesafe)
                    {
                        cokYakin = true;
                        break;
                    }
                }
                if (cokYakin) continue; // Eðer çok yakýnsa bu konumu pas geç, yeni konum dene
            }

            // Listeden rastgele bir mesh tipi seç
            CloudMeshType secilenModel = bulutModelleri[Random.Range(0, bulutModelleri.Count)];
            if (secilenModel.mesh == null) continue;

            // Runtime'da boþ bir obje oluþturup MeshFilter ve MeshRenderer ekliyoruz
            GameObject yeniBulut = new GameObject("LowPolyCloud_" + olusanBulutSayisi);
            yeniBulut.transform.position = rastgeleKonum;
            yeniBulut.transform.parent = this.transform;

            MeshFilter filter = yeniBulut.AddComponent<MeshFilter>();
            filter.mesh = secilenModel.mesh;

            MeshRenderer renderer = yeniBulut.AddComponent<MeshRenderer>();
            // Bulutlarýn yere gölge düþürmesini tamamen kapatýr
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.material = secilenModel.material != null ? secilenModel.material : new Material(Shader.Find("Standard"));

            // Low-poly tarzýna uygun olarak bulutlarý rastgele döndür ve boyutlandýr
            float rastgeleBoyut = Random.Range(minimumBulutBoyutu, maksimumBulutBoyutu);
            yeniBulut.transform.localScale = new Vector3(rastgeleBoyut, rastgeleBoyut * Random.Range(0.7f, 1.2f), rastgeleBoyut);
            yeniBulut.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            uretilenBulutlar.Add(yeniBulut);
            olusanBulutSayisi++;
        }
    }

    // Editör modundayken üretim alanýný kutu þeklinde görmek için çizim
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, uretimAlani);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, uretimAlani);
    }
}