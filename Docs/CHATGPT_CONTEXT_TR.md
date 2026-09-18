# CONTEXT FOR CHATGPT

Plots & Rots low-poly/flat-shaded, co-op yönelimli survival/farming/vehicle oyunudur. Unity **6000.3.19f1**, **URP17.3.0**. PC_Renderer Deferred(mode2), Mobile_Renderer Forward(mode0). Tek etkin build sahnesi Assets/Scenes/SampleScene.unity; recovery ve asset demo sahneleri de var. Multiplayer Center paketi mevcut fakat NGO/RPC/NetworkVariable/transport gameplay implementasyonu görülmedi. Gerçek kod source of truth; bu özeti gerekçe göstererek mevcut mimariyi yeniden kurma.

Genel oyun: CharacterController hareket, yeni Input System, Cinemachine ve kamera scriptleri. InventoryManager slot/durability/aktif item state; ItemData, ConsumableItemData, ToolItemData ScriptableObjects, Resources item çözümlemesi. VehicleController Rigidbody+dört WheelCollider, ayrı interaction/interior/camera/light/audio/durability scriptleri. WrenchController tamir yapar. BedInteractable HUD sleep sequence içinde DayNightCycleManager.Sleep ve PlayerStats yenilemesi çağırır. Henüz tam CropSystem, soil moisture, rain watering veya sıcaklık modeli yok. Ayrı GameManager/bootstrap bulunmadı.

## Save ve yaşam döngüsü

Assets/Scripts/SaveSystem altında namespace PlotNRots.SaveSystem. ISaveable.SaveState() object ve LoadState(object). SaveableEntity aynı objenin component state'lerini tip adıyla dictionary'ye koyar, Newtonsoft TypeNameHandling.Auto ile JSON string'e çevirir. GameData.savedEntities entity ID→JSON string. SaveManager HashSet register/unregister ve persistentDataPath/Saves dosyaları/autosave sağlar. Dynamic entity spawn veya multi-scene restore yok. SaveManager persistent singleton ve execution order -1000; DayNight -300; Season -200. SaveableEntity Start'ta tekrar register ederek initialization sırası riskini azaltır. Load clock entity'yi önce restore eder, en sonda SaveManager.OnGameLoaded yayınlar.

SampleScene ID'leri **world-clock** ve **world-season-weather**. ClockSaveData.time kaydedilir. SeasonManager.WeatherSaveData: version1, year, season, dayOfSeason, currentWeather, weatherIntensity, globalSnowAmount, randomState. TypeNameHandling nedeniyle $type bilgisi vardır; dış sözlük value'su nested JSON string'dir. Transition/şimşek süresi ve cosmetic random kaydedilmez. Eski save'de bu ID'ler yoksa sahne başlangıç state'i kalır. Season/clock/görseller scene-owned; ileride sahneler arasında state taşınırsa snapshot açıkça aktarılmalıdır.

## Clock / season / weather state

**Assets/Scripts/World/DayNightCycleManager.cs** mevcut sunLight/moonLight rotation/intensity, ambient/reflection eğrilerini korur. realSecondsPerDay=1200; currentTime; sahnede morningStartTime7.5/nightStartTime17.5. IsNight(), Sleep(), Daylight, NormalizedTime, SetLocalSimulation(bool), ISaveable API. Daylight güneş eğrisini daylightReferenceIntensity1.5'e böler/clamp eder. Saat yüklemesi yeni gün eventini tetiklemez. Sabah boundary-crossing hesabı büyük adımlarda pencere atlamasını önler ve kalan saat korunur. Eventin adı TAM OLARAK **YeniGunBasladiSinyali**.

**Assets/Scripts/World/SeasonManager.cs** enumlar mevcut numaralarıyla:
Season {Spring=0,Summer=1,Autumn=2,Winter=3}; WeatherType {Sunny=0,Cloudy=1,Rainy=2,Snowy=3}. Clear/Rain/Snow karşılıkları Sunny/Rainy/Snowy; yeni paralel enum ekleme. daysPerSeason30. Private serialized state, eski isimler için FormerlySerializedAs. Salt okunur currentYear/currentDay/currentSeason/currentWeather, WeatherIntensity, TotalDay, SimulateLocally, IsRestoringState.

Akış: clock yeni gün → Season.AdvanceDay → takvim → gerekirse **OnSeasonChanged** → DetermineWeather → **OnWeatherChanged** → **OnDayAdvanced**. OnDayAdvanced günlük crop işlemleri için state seçimi sonrası doğru noktadır. OnWeatherChanged tür aynı kalsa bile intensity güncellemesini yayınlar. **OnStateRestored** ve IsRestoringState load için vardır. API SetWeather(type,intensity), DetermineWeather(), SetLocalSimulation(bool), ApplySnapshot(WeatherSaveData), SaveState/LoadState. RequireComponent SaveableEntity+SnowAccumulationManager.

**Assets/Scripts/World/SeasonWeatherProfile.cs** yeni SO; Entry[] hava/ağırlık/min/maxIntensity. Select(roll,intensityRoll,out intensity) weighted seçim, kendi random kaynağı yok. Dizi sırası enum sırası. Assets/Settings/Weather/{Spring,Summer,Autumn,Winter}.asset ağırlıkları Sunny/Cloudy/Rainy/Snowy sırasıyla 40/25/35/0,75/15/10/0,20/40/40/0,10/20/0/70. Satır intensity .2–1. Null/boş/sıfır toplam Sunny0; negatif ağırlık sıfır. Eski inline WeatherProbabilities profile yoksa fallback.

Season bağımsız xorshift32 PRNG kullanır ve state'i kaydeder. Lightning/başka Unity Random gameplay hava seçimini değiştirmez. Başlangıç Winter/Snowy tercihi korunmuştur, intensity.6; ilk frame zar atılmaz, her sabah yeniden seçim.

## Görsel sistem

**Assets/Scripts/World/WeatherVisualsManager.cs** yalnız sunum. Mevcut dört WeatherVisuals renk/fog paketi, rainParticles/snowParticles, emission limitleri, rainSoundClip, thunderSounds, ayrı lightningLight serialized adları korunur. API TransitionToWeather() / ApplyWeatherImmediate() güncel Season state'ini okur. Event OnWeatherChanged ve SaveManager.OnGameLoaded.

20s transition, SmoothStep, yağış artışında ilk%30 gecikme; azalma hemen başlar. Rain↔Snow crossfade mümkündür; yeni event mevcut görsel değerlerden devralır. Emission=max×intensity, sahnede max100/100. Rain volume=intensity×.8, pitch1.2→1. Daylight ile sky/fog parlaklığı nightBrightness.12..1. LateUpdate bu modülasyonu geçiş bittikten sonra da sürdürür. Sun/moon/ambient clock'ta kalır. Fog ExponentialSquared (eski sahne Linear idi). Bir runtime skybox kopyası, disable'da restore/destroy; renderer taraması veya per-object material clone yok.

WindZone/windAudio/precipitationAnchor optional; SampleScene'de boş. WeatherEffects zaten oyuncu hiyerarşisi altında. Wind Sunny.1, diğerleri Lerp(.2,1,intensity); cloud offset hareketi, particle world X drift (max6), varsa WindZone(max2) ve ses. Özel SG_Grass wind entegrasyonu yok. WindAudio atanırsa Loop açık/Play On Awake kapalı gerekir.

Rain intensity>.6 storm; transition sonrası10–30s aralık; .1s tam flash,.05 ara,.1s yarım flash;1–3s sonra thunder. Cosmetic random lokal, network sync yok. Disable/hava değişiminde coroutine/flash/audio temizlenir. ApplyWeatherImmediate eski transition/particle kalıntılarını temizler, doğru emission/audio/sky/fog'a snap eder.

Load: önce clock, season snapshot içinde snow.SetAmount hemen global yayınlar; IsRestoringState=true altında season/weather/restore eventleri, visual immediate. En sonda OnGameLoaded yeniden snap. Clear'dan20s tekrar bekleme yok.

## Global kar

**Assets/Scripts/World/SnowAccumulationManager.cs** tek world component, aynı objedeki SeasonManager'ı Awake'te alır. API Amount, SetAmount(float), Simulate(seconds), instance OnSnowAmountChanged(float). Miktarı Season save DTO'su paketler. Snowy .002×intensity×scaledSeconds birikim; Winter dışı Sunny .0005×scaledSeconds erime; diğer durumlarda kalıcılık;0..maximumAmount1. TimeScale0 durur. Sleep gece için toplu simülasyon yapmaz. Disable global0; enable aynı miktarı tekrar yayınlar. Client SimulateLocally=false ise kar simüle edilmez.

**Assets/Art/Shaders/Weather/GlobalSnow.hlsl** globals TAM OLARAK:
_GlobalSnowAmount, _SnowNormalThreshold, _SnowEdgeSoftness, _GlobalSnowColor.
Material Properties/blackboard local property olarak yeniden tanımlama. Up=normalize(normalWS).y; threshold.45 (en az.001), softness.05; step ya da smoothstep(threshold,threshold+softness,up); mask=saturate(amount)×upward; lerp(originalAlbedo,snowColor,mask). Dikey/aşağı yüzey sıfır. GlobalSnow_float/half(BaseColor,NormalWS,out Color) Shader Graph Custom Function girişleri.

Destek:
- **Assets/FlatShadedShader.shadergraph** mevcut kimlik/material'leri korunarak BaseColor önüne kar fonksiyonu alır; ddx/ddy/cross/normalize flat world normal hattı korunur. Tekstil/brightness/flat stil yeniden yazılmadı.
- **Assets/Art/Shaders/Weather/SnowLit.shader**, shader adı **Plots & Rots/Snow Lit**; URP17.3 Lit property uyumlu varyant. LitForwardPass.hlsl ve LitGBufferPass.hlsl kar blend içerir. Geometric normal kullanır, snow metallic0/smoothness.1'e yaklaşır.
- **TerrainLit.shader**, adı **Plots & Rots/Terrain/Snow Lit**; TerrainLitAdd.shader, TerrainLitBase.shader, TerrainLitPasses.hlsl. Forward/GBuffer,4+layer ve uzak basemap karı; basemap generator stock, kar texture'a bake edilmez. Heightmap/geometric normal, material bump'tan bağımsız maske.
- **SnowTerrain.mat**, SampleScene'deki Terrain/Terrain_(1610.00,0.00,1000.00)/TerrainLake'e atandı. Terrain_Main_House özel flat graph material'ini korur; o graph da kar destekler. TerrainData değişmedi.
- **SnowGltf.shadergraph**, adı **Plots & Rots/SnowGltf**; glTFast6.19 metallic/roughness graph varyantı, mevcut texture/property/UV yapısı korunur, world Normal Vector ile BaseColor blend. SnowGltf ve FlatShadedShader yalnız baseColor değiştirir, mevcut material smoothness korunur.
- **Assets/Art/Sky/LowPolySky.shader**, adı **Custom/LowPolySky**; posterized FBM bulutlar korunur, _CloudOffset rüzgâr hızında sıçramayı önler.

Global shader property stock URP Lit/custom shaderları otomatik karlı yapmaz. **Bütün scene materyalleri henüz migrate edilmedi.** SG_Grass, MedievalTownLite_LIGHT, su/cam/particle/sky materyalleri topluca değiştirilmemeli. Çatı/indoor mask, biome, iz/geometri kalınlığı yok; uygun shader atanmış iç mekân üst yüzeyleri de kar alır. Karın crop/traction gameplay etkisi henüz yok. URP/glTF fork'ları paket güncellemesinde upstream karşılaştırması ister; lisans dosyaları yanlarında.

## Inspector / migration / doğrulama

SampleScene SeasonManager objesi SeasonManager+WeatherVisualsManager+SnowAccumulationManager+SaveableEntity taşır. Dört profile bağlı. Visual clock=DayNightCycleManager; RainEffect/SnowEffect, LightningLight ve rain/thunder clip'leri mevcut bağlantılarıyla. Ayrı DayNightCycleManager objesinde world-clock SaveableEntity. Component'leri tekrar ekleme.

**Assets/Editor/WeatherMaterialTools.cs** menüleri Tools/Plots & Rots/Weather:
1. Convert selected URP Lit materials to Snow Lit: yalnız seçili standalone opaque URP Lit .mat, Undo'lu shader değişimi. Shared kullanım etkilenir; önce kopya gerekebilir.
2. Create snow glTF materials for selected objects: Hierarchy'de Pickup gibi kökü seç. Opaque glTF metallic/roughness materyallerini Assets/Art/Materials/WeatherMigration altına kopyalar ve renderer override eder; GLB/cam/transmission korunur. Sahneyi kaydet. Undo renderer atamasını geri alır; yeni asset dosyaları kalır.
3. Log selected renderer shader support: audit.

**Assets/Editor/SeasonManagerEditor.cs** Play Mode'da dört hava butonu, intensity slider, snap, ground snow0/1. Ham serialized field değişikliği event üretmez; testte bu butonları kullan.

**Tools/ValidateWeather.ps1** kurulu Unity Roslyn ve csproj referanslarıyla Runtime+Editor compile. **Assets/Editor/WeatherValidation.cs / WeatherValidation.Run** menüsü weighted sınırlar, save entity roundtrip, PRNG devamı, yıl devri, authority, kar erime/kalıcılık ve shader import kontrol eder; görsel snap/day-night/disable kontrolleri de vardır. Kesin çalıştırma sayısı/log/kapsam **Docs/WeatherValidationResults.md** içindedir. Kod testinin var olması gerçek sahne render/ses veya player build testinin yapıldığı anlamına gelmez.

Rapor **Docs/WEATHER_SYSTEM_HANDOFF_TR.md** 14 bölüm: mimari, önce/sonra, her dosya, API, algoritma, snow, save JSON, Inspector, migration, checklist, bilinen sorunlar, gelecek sıra, bu bağlam. İlk material envanteri **Docs/WeatherMaterialAudit.tsv**.

Sonraki sıra: migration+gerçek sahne görsel QA; temperature sağlayıcısı ve Snow melt koşulu; crop günlük OnDayAdvanced, sezon OnSeasonChanged, rain watering OnWeatherChanged+WeatherIntensity; soil moisture kendi ISaveable; snow gameplay Amount/OnSnowAmountChanged; server authority adapter. Client'ta Season ve clock simülasyonlarını kapat, server snapshotlarını taşı. ApplySnapshot load/late-join snap'e hazır; sürekli network visual interpolation, transport, host migration/cosmetic lightning sync henüz yapılmadı. Mevcut çalışan event/API'leri genişlet, yeniden tasarlama.
