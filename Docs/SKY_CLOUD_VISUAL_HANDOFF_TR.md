# Plots & Rots — Hibrit Sky / Cloud Teknik Devir

Tarih: 21 Eylül 2026

## Son mimari

Sky gradient, sun/moon, stars ve hafif cirrus `Custom/LowPolySky` içinde kalır. Sunny ve PartlyCloudy ana cumulusları artık skybox'a project edilen R maskesi değildir; `StylizedCloudManager` tarafından yönetilen gerçek world-space 3D clusterlardır. Overcast/Rain/Storm/Snow için sky pass içinde ayrı, kesintisiz bir yüksek irtifa ceiling bulunur.

Packed texture korunur. R kanalı fair-weather silüet üretmez. B yalnız seyrek cirrus içindir. G/A ceiling'de kesme/eşik yerine çok düşük tonal varyasyon verir; dome projection kaynaklı uzun silüet, delik ve camouflage üretmez.

## 3D cluster yolu

- Kaynak: `Assets/Art/Models/Clouds/stylize_clouds.glb` içindeki üç shared mesh.
- Her mesh yaklaşık 1.154 vertex / 2.304 triangle.
- PC pool 20; Mobile pool 10.
- Sunny hedefi 7; PartlyCloudy hedefi 13 (mevcut appearance değerleriyle).
- Halka 250–800 m; irtifa 150–350 m; ölçek 10–20, Y oranı .65–.90.
- Deterministic seed 73129; farklı mesh, scale, yükseklik, yaw ve küçük roll.
- Kamera rotasyonu takip edilmez. Slotlar rüzgârın ters halka kenarında recycle edilir.
- Instantiate yalnız ilk pool kurulumunda; weather/movement sırasında instantiate/destroy yoktur.

Pool childları yalnız `Transform + MeshFilter + MeshRenderer` taşır. Collider, Rigidbody ve per-cloud MonoBehaviour yoktur. Tek manager hareket, recycle ve opacity geçişini yürütür.

## Materyal ve maliyet

`Plots & Rots/Stylized Cloud` tek `UniversalForwardOnly` pass kullanır. Shared materyalde GPU instancing açıktır. Üç mesh nedeniyle görünürlük/culling koşullarına bağlı en fazla yaklaşık üç instanced mesh grubu beklenir; gerçek draw call hedef cihaz profiler'ında doğrulanmalıdır.

AlphaTest queue, ZWrite On, Blend Off kullanılır. Geçişte 4×4 dither clip vardır; settled hedef sayı tam sayıdır, kalıcı dither shell yoktur. Shadows, probes ve motion vectors kapalıdır.

Azami PC bütçesi yaklaşık 23.080 vertex / 46.080 triangle; Mobile üst sınırı yaklaşık 11.540 vertex / 23.040 triangle. Normal görünür sayı pool sınırından düşüktür. `StylizedSkyController` global sun direction ve highlight/base/underside paletini yayınlar.

## Weather ve ceiling

`WeatherVisualsManager` mevcut appearance blend sonucunu manager'a iletir. Sunny→Partly count artar. Partly→Overcast sırasında 3D cluster opacity'leri azalırken ceiling artar. Obje aktifliği opacity .01 altında kapanır; toplu bir-frame pop yoktur.

Ceiling bağlantılı atmosfer tabakasıdır. G/A yalnız hafif renk kırılması yapar; silüet ya da transparan card üretmez. Rain/Storm daha koyu, Snow daha açık/soğuktur. Ceiling sun/moon/stars üzerine blend edildiği için kapalı havada celestial alanı bastırır.

## Unity hierarchy

Gerçek `SampleScene` içindeki `SeasonManager` GameObject'i SeasonManager, WeatherVisualsManager, StylizedSkyController, SnowAccumulationManager, SaveableEntity ve StylizedCloudManager bileşenlerini taşır. Manager anchor'ı Main Camera transformudur. Runtime childları `Pooled Cloud 00..19` olur.

Eski disabled `CloudGenerator` component'i gerçek SampleScene'den kaldırıldı; iki sistem aynı anda çalışmaz.

## Otomatik doğrulama

Unity 6000.3.19f1 / URP 17.3.0 / Windows D3D11 / PC_RPAsset:

- Weather regression 39/39.
- Sky/climate/cloud regression 38/38.
- Toplam 77/77.
- Shader import ve C# compile PASS.
- Gerçek `SampleScene.unity` additively açıldı: tek manager, shared material, 20 slot ve sıfır legacy component doğrulandı.
- 10 adet 1280×720 GPU capture üretildi.

Yeni kontroller pool sınırı, collider/Rigidbody yokluğu, shared material, per-cloud controller yokluğu, Sunny/Partly hedefleri, Overcast ceiling, yumuşak transition ve eski generator pasifliğini kapsar.

Görsel set `Docs/SkyPreview/00-contact-sheet.png`. Azimutlar 0°, 45°, 90°, 135°, 180°, 225°, 270° ve 315° yönlerini kapsar. Fair-weather karelerinde gerçek 3D cluster/depth; ceiling karelerinde kesintisiz hava tabakası vardır. Vertical stretch, smear, camouflage, grid, giant card ve horizon seam görülmedi.

## Değişen dosyalar

- `Assets/Scripts/World/StylizedCloudManager.cs` ve meta
- `Assets/Art/Sky/Clouds/StylizedCloud.shader/.mat` ve metaları
- `Assets/Art/Sky/LowPolySky.shader`
- `Assets/Scripts/World/StylizedSkyController.cs`
- `Assets/Scripts/World/WeatherVisualsManager.cs`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Editor/SkyWeatherValidation.cs`
- `Assets/Editor/SkyRenderValidation.cs`
- `Docs/SkyPreview/*`, doğrulama kaydı ve bu rapor

DayNightCycleManager, SeasonManager seçim/persistence/temperature/save-load, precipitation düzeltmeleri ve snow gameplay state korunmuştur. Terrain/prop/tree snow işi yapılmadı.

## Eski asset audit

- **KEEP / aktif:** `stylize_clouds.glb`; yeni manager üç meshini kullanır.
- **LEGACY / korunuyor:** `CloudGenerator.cs`; gerçek SampleScene'de yoktur, `Assets/_Recovery/0 (1).unity` hâlâ referans taşır.
- **LEGACY / korunuyor:** `Mat_Cloud.mat`; yeni yol kullanmaz, eski/Recovery bağlamı nedeniyle silinmedi.
- **KEEP:** packed mask ve source araçları; B cirrus ve G/A ceiling varyasyonu için referanslıdır.

## Manuel Play Mode kabulü

1. `SampleScene` açın, Console'u temizleyin ve Play'e girin.
2. Sunny öğlen 360° dönün: 4–8 cluster, geniş mavi boşluk, farklı yaw/ölçek ve world-space sabitliği.
3. Oyuncuyu 800 m'den fazla hızlı taşıyın: destroy/spawn olmadan recycle ve belirgin pop olmaması.
4. PartlyCloudy: 8–15 cluster, birkaç hero cloud ve düzenli grid olmaması.
5. Partly→Overcast→Rain→Storm→Snow: cumulus fade-out ve ceiling fade-in sürekliliği.
6. Sunrise/sunset/night: top/mid/bottom paleti ve moon/star geometry occlusion.
7. Rain/Snow particle takibini yaya/araçta, weather audio blend'ini kulaklıkla kontrol edin.
8. PC Profiler'da draw call/SetPass/GC; gerçek mobil cihazda 10 slot, Forward ve frame time ölçün.

Bu adımlarda scene/profile değerlerini değiştirmek gerekmez.
