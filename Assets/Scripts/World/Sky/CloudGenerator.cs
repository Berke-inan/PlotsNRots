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

    [Header("1. Bulut Modelleri")]
    public List<CloudMeshType> bulutModelleri = new List<CloudMeshType>();

    [Header("2. Daðýlým ve Adet Ayarlarý")]
    public Vector3 uretimAlani = new Vector3(200f, 20f, 200f);
    public float minimumBulutBoyutu = 5f;
    public float maksimumBulutBoyutu = 15f;
    public float bulutlarArasiMesafe = 20f;
    public bool icIceGecmeyeIzinVer = false;

    [Header("3. Hava Durumuna Göre Bulut Sayýlarý")]
    public int sunnyCloudCount = 5;      // Güneþli havada çok az bulut
    public int cloudyCloudCount = 20;    // Parçalý bulutlu
    public int stormCloudCount = 45;     // Yaðmurlu/Karlý havada gökyüzü kapansýn

    [Header("4. Rüzgar (Hareket) Ayarlarý")]
    public float ruzgarHizi = 1f;
    public Vector3 hareketYonu = new Vector3(1f, 0f, 0f);

    [Header("5. Renk Ayarlarý")]
    public Color normalCloudColor = Color.white;
    public Color stormCloudColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public float colorTransitionSpeed = 0.5f;

    private List<GameObject> uretilenBulutlar = new List<GameObject>();
    private Color _targetColor = Color.white;
    private int _currentTargetCloudCount = 5;

    private void OnEnable()
    {
        SeasonManager.OnWeatherChanged += HandleWeatherChange;
    }

    private void OnDisable()
    {
        SeasonManager.OnWeatherChanged -= HandleWeatherChange;
    }

    private void Start()
    {
        if (SeasonManager.Instance != null)
        {
            HandleWeatherChange(SeasonManager.Instance.currentWeather);
        }
        else
        {
            _currentTargetCloudCount = sunnyCloudCount;
            BulutlariOlustur();
        }
    }

    private void Update()
    {
        transform.Translate(hareketYonu.normalized * ruzgarHizi * Time.deltaTime);

        // GECE PARLAMA ÇÖZÜMÜ: Ortam ýþýðýna (Ambient) göre hedef rengi karart
        // Eðer ortam ýþýðý gece düþüyorsa, bulutlar da kararýr.
        float ambientIþýk = Mathf.Clamp(RenderSettings.ambientIntensity, 0.15f, 1f);
        Color geceyeUyumluRenk = _targetColor * ambientIþýk;
        geceyeUyumluRenk.a = 1f; // Þeffaflýðý bozmamak için

        foreach (var bulut in uretilenBulutlar)
        {
            if (bulut != null)
            {
                MeshRenderer renderer = bulut.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    if (renderer.material.HasProperty("_BaseColor"))
                    {
                        Color currentColor = renderer.material.GetColor("_BaseColor");
                        renderer.material.SetColor("_BaseColor", Color.Lerp(currentColor, geceyeUyumluRenk, Time.deltaTime * colorTransitionSpeed));
                    }
                    else if (renderer.material.HasProperty("_Color"))
                    {
                        renderer.material.color = Color.Lerp(renderer.material.color, geceyeUyumluRenk, Time.deltaTime * colorTransitionSpeed);
                    }
                }
            }
        }
    }

    // SÝNYAL GELDÝÐÝNDE ÇALIÞACAK FONKSÝYON
    private void HandleWeatherChange(WeatherType newWeather)
    {
        switch (newWeather)
        {
            case WeatherType.Sunny:
                _targetColor = normalCloudColor;
                _currentTargetCloudCount = sunnyCloudCount;
                break;
            case WeatherType.Cloudy:
                _targetColor = normalCloudColor;
                _currentTargetCloudCount = cloudyCloudCount;
                break;
            case WeatherType.Rainy:
            case WeatherType.Snowy:
                _targetColor = stormCloudColor;
                _currentTargetCloudCount = stormCloudCount;
                break;
        }

        // Hava deðiþtiðinde eski bulutlarý silip yeni sayýya göre gökyüzünü baþtan çizer
        BulutlariOlustur();
    }

    [ContextMenu("Bulutlarý Yeniden Oluþtur")]
    public void BulutlariOlustur()
    {
        foreach (var bulut in uretilenBulutlar)
        {
            if (bulut != null) Destroy(bulut);
        }
        uretilenBulutlar.Clear();

        if (bulutModelleri == null || bulutModelleri.Count == 0) return;

        int denemeSayisi = 0;
        int olusanBulutSayisi = 0;

        while (olusanBulutSayisi < _currentTargetCloudCount && denemeSayisi < _currentTargetCloudCount * 10)
        {
            denemeSayisi++;

            Vector3 rastgeleKonum = transform.position + new Vector3(
                Random.Range(-uretimAlani.x / 2f, uretimAlani.x / 2f),
                Random.Range(-uretimAlani.y / 2f, uretimAlani.y / 2f),
                Random.Range(-uretimAlani.z / 2f, uretimAlani.z / 2f)
            );

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
                if (cokYakin) continue;
            }

            CloudMeshType secilenModel = bulutModelleri[Random.Range(0, bulutModelleri.Count)];
            if (secilenModel.mesh == null) continue;

            GameObject yeniBulut = new GameObject("LowPolyCloud_" + olusanBulutSayisi);
            yeniBulut.transform.position = rastgeleKonum;
            yeniBulut.transform.parent = this.transform;

            MeshFilter filter = yeniBulut.AddComponent<MeshFilter>();
            filter.mesh = secilenModel.mesh;

            MeshRenderer renderer = yeniBulut.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            renderer.material = secilenModel.material != null ? new Material(secilenModel.material) : new Material(Shader.Find("Standard"));

            float rastgeleBoyut = Random.Range(minimumBulutBoyutu, maksimumBulutBoyutu);
            yeniBulut.transform.localScale = new Vector3(rastgeleBoyut, rastgeleBoyut * Random.Range(0.7f, 1.2f), rastgeleBoyut);
            yeniBulut.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            uretilenBulutlar.Add(yeniBulut);
            olusanBulutSayisi++;
        }
    }
}