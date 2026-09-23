> Tarihsel rapor: Güncel sky, sıcaklık ve mevsim sistemi için Docs/SKY_WEATHER_HANDOFF_TR.md ve Docs/CHATGPT_CONTEXT_TR.md dosyalarını kullanın. Bu dosyadaki eski enum, başlangıç mevsimi ve erime kuralları güncel tasarım değildir.

# Plots & Rots — Teknik devir raporu

Kaynak: gerçek repository kodları, sahne YAML'ı, materyal/Shader Graph dosyaları ve kurulu paketler. Önceden var olan kullanıcı değişiklikleri korunmuştur. Kesin doğrulama kapsamı `WeatherValidationResults.md` dosyasındadır.

## 1. PROJECT OVERVIEW

- Unity **6000.3.19f1**, URP **17.3.0**. PC_Renderer mode 2 (Deferred), Mobile_Renderer mode 0 (Forward).
- Input System 1.19.0, Cinemachine 3.1.7, Newtonsoft JSON 3.2.2, glTFast 6.19.0, AI Navigation 2.0.13 paketleri mevcut. Multiplayer Center 1.0.1, çalışan network gameplay altyapısı anlamına gelmez; NGO/RPC/NetworkVariable veya başka transport implementasyonu görülmedi.
- Build Settings'te tek etkin sahne `Assets/Scenes/SampleScene.unity`. `_Recovery/0.unity` ve üçüncü taraf model demo sahneleri de mevcut, değiştirilmedi.
- Manager'lar MonoBehaviour/singleton/Inspector referansı/C# eventleri kullanıyor. Ayrı GameManager/bootstrap/DI container görülmedi. SaveManager persistent; saat/mevsim/görsel manager'lar artık scene-owned.
- PlayerMovement CharacterController ve SphereCast ile hareket/zıplama/yerçekimi sağlar. PlayerInputHandler yeni Input System, PlayerCameraController/CameraManager ve Cinemachine kamera tarafını oluşturur.
- PlayerInteraction kamera raycast'iyle eşya kullanma/alma/bırakma ve araca binmeyi yönetir. BedInteractable HUD uyku animasyonu içinde Sleep ve PlayerStats yenilemesini çağırır.
- InventoryManager slot/durability/aktif eşya state'i taşır; ItemData, ConsumableItemData, ToolItemData ScriptableObject tipleri ve Resources üzerinden item çözümlemesi vardır.
- VehicleController Rigidbody + dört WheelCollider; steering/brake/anti-roll. VehicleInteractable, VehicleInterior, kamera, ışık, ses, TractorExhaust ayrı scriptlerdir. VehicleDurability mesafe bazlı arıza/tamir ve ISaveable; WrenchController tamir aracı davranışıdır.
- Tam crop/soil moisture/sulama kodu görülmedi; farming tool modelleri ve alet altyapısı var. Yeni CropSystem yazılmadı.
- UI Toolkit MainHUDManager/PauseMenuController, ayrıca inventory/vehicle UI. Save/load pause menüsünden çağrılır.
- Save namespace `PlotNRots.SaveSystem`: ISaveable, SaveableEntity, SaveManager, GameData. Entity ID → JSON string sözlüğü; içeride component tip adı → state. Newtonsoft TypeNameHandling.Auto, autosave ve eski autosave temizliği mevcut.
- Dünya: DayNightCycleManager, SeasonManager, WeatherVisualsManager. Eski CloudGenerator gerçek SampleScene'den kaldırıldı; aktif hibrit yol `Custom/LowPolySky + StylizedCloudManager`.
- İlk standalone `.mat` taraması: 81 materyal; 74 URP Lit, ayrıca FlatShadedShader, SG_Grass, LowPolyWater, MedievalTownLite_LIGHT, LowPolySky, glTF metallic/roughness grafiği ve bir built-in shader referansı. Gömülü GLB materyalleri bu sayıya dahil değil. Envanter: `WeatherMaterialAudit.tsv`.
- Dört Terrain material bağlantısı: üçü paket default TerrainLit, Terrain_Main_House özel flat-shaded materyal kullanıyordu. TerrainData içeriği değiştirilmedi.

## 2. BEFORE CHANGES

**DayNightCycleManager:** Sun/moon rotation/intensity, ambient/reflection eğrileri; sabahın bir saatlik penceresinde YeniGunBasladiSinyali. Büyük adım pencereyi atlayabiliyordu; gece sonundaki fazla zaman sıfırlanıyordu. Sleep aynı eventi ayrıca gönderiyordu. Saat kaydı yoktu.

**SeasonManager:** Yeni gün eventi → gün artırma → 30 günde mevsim/yıl → inline WeatherProbabilities üzerinden Unity Random weighted seçim. OnWeatherChanged sadece tür değişiminde. Sahne oranları Spring %100 Cloudy, Summer %100 Sunny, Autumn %100 Rainy, Winter %100 Snowy; başlangıç Winter/Snowy. DontDestroyOnLoad kullanmasına rağmen aynı objedeki görsel manager sahne light/particle referansları taşıyordu.

**WeatherVisualsManager:** Intensity'yi kendi Random.Range(.2,1) çağrısıyla üretiyordu. Particle emission hemen değişiyor; sky/fog/audio her frame Lerp ile hedefe yaklaşıyordu, tanımlı süre yoktu. Storm coroutine vardı; disable temizliği eksikti. Sky geceye göre kararmıyordu. Fog density değiştirilmesine rağmen scene fog modu Linear idi. Paylaşılan skybox material runtime'da doğrudan değiştiriliyordu.

**CloudGenerator:** Eski mesh bulut alternatifi, hava değişiminde sil/yeniden oluştur ve renderer.material kullanımı içeriyor. Sahnede kapalıydı; yeni aktif görsel yoluna dahil edilmedi.

Weather/season intensity için save entegrasyonu ve global snow accumulation/shader desteği yoktu.

## 3. CHANGES MADE

Bu liste bu çalışmanın dosyalarıdır. Yeni Unity asset/script/folder `.meta` dosyaları kimlikleri taşır, birlikte tutulmalıdır.

| FILE | CHANGE |
|---|---|
| Assets/Scripts/World/DayNightCycleManager.cs | Mevcut ışık eğrileri/event/API korunarak ISaveable, güvenli sabah crossing hesabı, Daylight/NormalizedTime ve local authority anahtarı. |
| Assets/Scripts/World/SeasonManager.cs | Eski enum/event isimleri korundu; private serialized state ve FormerlySerializedAs; profile, intensity, bağımsız PRNG, snapshot/restore/authority, OnDayAdvanced, kar dahil save DTO. Scene-owned lifecycle. |
| Assets/Scripts/World/SeasonWeatherProfile.cs | Yeni ScriptableObject: hava/ağırlık/intensity aralığı ve test edilebilir weighted seçim. |
| Assets/Scripts/World/SnowAccumulationManager.cs | Yeni tek dünya kar miktarı, birikim/erime, global shader state ve miktar eventi. |
| Assets/Scripts/World/WeatherVisualsManager.cs | Mevcut Inspector bağlantıları/renk paketleri korunarak süreli geçiş, gecikmeli precipitation artışı, clock modülasyonu, wind, immediate load ve lifecycle temizliği. |
| Assets/Scripts/SaveSystem/SaveManager.cs | Execution order -1000, clock-first restore, null veri kontrolü, OnGameLoaded ve singleton temizliği. |
| Assets/Scripts/SaveSystem/SaveableEntity.cs | Start'ta tekrar register; Awake/OnEnable sırası yüzünden kaçan kayıtları tamamlar. JSON formatı korunur. |
| Assets/Scripts/UI/MainHUDManager.cs | Ayrı kayıtsız gün sayacı yerine SeasonManager.TotalDay; OnDayAdvanced/OnStateRestored dinleme. |
| Assets/Art/Sky/LowPolySky.shader | Mevcut FBM/posterization korunur; hız değişince bulut konumu sıçramasın diye _CloudOffset. |
| Assets/Art/Shaders/Weather/GlobalSnow.hlsl | Ortak upward world-normal mask ve Shader Graph float/half Custom Function. |
| Assets/Art/Shaders/Weather/SnowLit.shader | URP 17.3 Lit property/keyword/pass uyumlu kar shader'ı. |
| Assets/Art/Shaders/Weather/LitForwardPass.hlsl | Forward surface albedo/metallic/smoothness kar blend. |
| Assets/Art/Shaders/Weather/LitGBufferPass.hlsl | Deferred/GBuffer kar blend. |
| Assets/Art/Shaders/Weather/TerrainLit.shader | Terrain ana shader, kar destekli add/base pass dependency. |
| Assets/Art/Shaders/Weather/TerrainLitAdd.shader | Dörtten fazla layer için ek terrain pass. |
| Assets/Art/Shaders/Weather/TerrainLitBase.shader | Uzak basemap için runtime kar. |
| Assets/Art/Shaders/Weather/TerrainLitPasses.hlsl | Terrain Forward/Deferred ve geometric/heightmap normal snow blend. |
| Assets/Art/Shaders/Weather/SnowTerrain.mat | Proje içi snow-compatible default Terrain material. |
| Assets/FlatShadedShader.shadergraph | Texture/brightness ve mevcut flat normal zinciri korunur; BaseColor önüne kar fonksiyonu eklenir. |
| Assets/Art/Shaders/Weather/SnowGltf.shadergraph | glTFast metallic/roughness grafiğinin mevcut property/texture/UV yapısını koruyan kar varyantı. |
| Assets/Art/Shaders/Weather/Unity-LICENSE.md | Türetilen URP kaynakları lisans bildirimi. |
| Assets/Art/Shaders/Weather/glTFast-LICENSE.md | Türetilen glTFast grafiği lisans bildirimi. |
| Assets/Settings/Weather/Spring.asset | Sunny/Cloudy/Rainy/Snowy = 40/25/35/0. |
| Assets/Settings/Weather/Summer.asset | 75/15/10/0. |
| Assets/Settings/Weather/Autumn.asset | 20/40/40/0. |
| Assets/Settings/Weather/Winter.asset | 10/20/0/70. |
| Assets/Scenes/SampleScene.unity | Profile'lar, snow manager, iki SaveableEntity ve ID, clock referansı, 20s transition, üç default Terrain material bağlantısı. |
| Assets/Editor/WeatherMaterialTools.cs | Seçili URP Lit dönüşümü; seçili objelerde opaque glTF materyal kopyalama/atama; shader audit. |
| Assets/Editor/SeasonManagerEditor.cs | Play Mode hava/intensity/snap/kar 0-1 test butonları. |
| Assets/Editor/WeatherValidation.cs | Weighted seçim, JSON, PRNG, takvim, authority, kar ve shader import regresyonları. |
| Tools/ValidateWeather.ps1 | Kurulu Unity Roslyn ve csproj referanslarıyla Runtime+Editor C# compile. |
| Docs/WeatherMaterialAudit.tsv | Değişiklik öncesi standalone material envanteri. |
| Docs/WEATHER_SYSTEM_HANDOFF_TR.md | Bu 14 bölümlü devir raporu. |
| Docs/CHATGPT_CONTEXT_TR.md | Bağımsız, kopyalanabilir teknik bağlam. |
| Docs/WeatherValidationResults.md | Gerçek test sonuçları ve kapsam sınırları. |

## 4. FINAL ARCHITECTURE

```text
DayNightCycleManager.AdvanceTime / Sleep
 -> YeniGunBasladiSinyali
 -> SeasonManager.AdvanceDay (authority kontrolü)
    -> day/season/year -> gerekirse OnSeasonChanged
    -> profile + bağımsız PRNG -> weather + intensity
    -> OnWeatherChanged
       -> WeatherVisualsManager.TransitionToWeather
          -> sky/fog/rain/snow/wind/audio -> uygunsa storm
    -> OnDayAdvanced (HUD ve gelecekte crop)

SeasonManager weather/intensity
 -> SnowAccumulationManager.Simulate(seconds)
 -> kalıcı yerdeki kar miktarı -> shader globals
 -> FlatShadedShader / Snow Lit / Terrain Snow Lit / SnowGltf

LoadGame -> önce clock -> season snapshot -> snow.SetAmount
 -> IsRestoringState altında eventler -> ApplyWeatherImmediate
 -> SaveManager.OnGameLoaded -> son immediate apply
```

Precipitation ve accumulation farklı state'tir. WeatherVisualsManager hava/intensity seçmez. Snow manager particle state'ine bakmaz. Mevcut eventler korunur; ikinci bir event bus oluşturulmadı.

## 5. IMPORTANT CLASSES

**DayNightCycleManager:** Eski public clock/light/curve alanları bağımlılıkları korur. Yeni simulateLocally, daylightReferenceIntensity=1.5. API: Daylight, NormalizedTime, IsNight, Sleep, SetLocalSimulation, SaveState/LoadState. ClockSaveData.time; load gün eventi göndermez. realSecondsPerDay varsayılan ve sahnede 1200; sahnede sabah 7.5, gece 17.5.

**SeasonManager:** daysPerSeason=30; private year/dayOfSeason/season/weather/weatherIntensity; profile dizisi enum sırasıyla. Eski inline ağırlıklar fallback. Salt okunur currentYear/currentDay/currentSeason/currentWeather, WeatherIntensity, TotalDay, SimulateLocally, IsRestoringState. API: SetWeather, DetermineWeather, SetLocalSimulation, ApplySnapshot, ISaveable. Eventler: OnSeasonChanged, OnWeatherChanged, OnStateRestored, OnDayAdvanced. RequireComponent SaveableEntity+SnowAccumulationManager. Clock eventine OnEnable/OnDisable; singleton OnDestroy temizliği. Scene-owned olmasının nedeni aynı objedeki görsellerin sahne referanslarıdır.

**WeatherVisualsManager:** Eski rainParticles/snowParticles, max emission, rainSoundClip/maxRainVolume, lightningLight/thunderSounds, dört WeatherVisuals serialized paketinin adları korunur. transitionDuration=20, precipitationDelayFraction=.3, nightBrightness=.12; clock. Optional windZone/windAudio/precipitationAnchor; maximumWind=2, maximumParticleDrift=6. stormThreshold=.6, interval=10–30s, thunderDelay=1–3s, lightningIntensity=3. API TransitionToWeather ve ApplyWeatherImmediate state'i manager'dan okur. OnWeatherChanged/OnGameLoaded dinler. Enable başına bir skybox kopyası; disable'da restore/destroy. LateUpdate day/night modülasyonunu sürdürür.

**SnowAccumulationManager:** accumulationPerSecond=.002, meltPerSecond=.0005, maximumAmount=1, normalThreshold=.45, edgeSoftness=.05, snowColor=(.88,.94,1), amount. API Amount, SetAmount(float), Simulate(float seconds); instance OnSnowAmountChanged(float). Aynı objenin SeasonManager referansı Awake'te alınır. Miktarın sahibi budur; SeasonManager save DTO'su onu paketler. Disable global karı sıfırlar, miktarı silmez; enable geri yayınlar.

**SeasonWeatherProfile:** Entry[]; weather/weight/minimumIntensity/maximumIntensity. Select(roll,intensityRoll,out intensity) random kaynağı içermez.

**SaveManager/SaveableEntity:** HashSet register, ID, component snapshot; sıra -1000/-300/-200 (save/clock/season), entity Start fallback. RequireComponent ID üretmez; SampleScene'de world-clock/world-season-weather zaten eklendi.

## 6. WEATHER ALGORITHM

Her sabah iki bağımsız xorshift32 değeri alınır. Birincisi ağırlık toplamında kümülatif seçim; ikincisi seçilen satırın min/max intensity aralığı. Toplamın 100 olması gerekmez. Negatif ağırlık 0; geçersiz enum atlanır; null/boş/sıfır toplam Sunny ve intensity0. Satırın ters min/max'ı düzeltilir, intensity 0..1'e sınırlandırılır.

Enum numaraları korunmuştur: Sunny=0, Cloudy=1, Rainy=2, Snowy=3. İstekteki Clear/Rain/Snow karşılıkları bunlardır. Bütün hazır profile satırları .2–1 intensity aralığında. Başlangıç Winter/Snowy korunur, intensity=.6, ilk frame zar atılmaz. Random state kaydedildiğinden aynı profile'larla gelecek seçim devam eder; cosmetic Unity Random bunu etkilemez. Aynı weather türünde yeni intensity de OnWeatherChanged gönderir.

TransitionDuration scaled saniye. Atmosfer SmoothStep ile tüm sürede; precipitation **artışı** ilk %30'dan sonra, azalması ilk andan itibaren. Rain↔Snow kısa kontrollü crossfade yapabilir. Yeni event devam eden geçişi güncel görsel değerlerden devralır. Önceki weather ayrıca kaydedilmez.

Emission=maxEmission×geçişteki intensity; sahnede 100/100. Wind Sunny'de .1, diğerlerinde Lerp(.2,1,intensity). Cloud hareketi, particle world X drift ve varsa WindZone/audio'ya uygulanır. WindZone custom SG_Grass'ı kendiliğinden hareket ettirmez.

Rain audio=intensity×maxRainVolume, pitch1.2→1. Storm Rain intensity **>.6**, geçiş tamamlanınca başlar. 10–30s bekleme, .1s tam flash, .05s ara, .1s yarım flash, 1–3s sonra thunder. Bu random lokal sunumdur, ağ senkronu değildir. Disable/weather değişimi storm'u temizler.

Sky/fog renk parlaklığı=Lerp(nightBrightness,1,clock.Daylight). Daylight güneş eğrisini referans şiddete böler. Sun/moon ve ambient/reflection kontrolü clock'ta kalır. FBM, altı seviyeli posterization ve step cloud silueti korunur.

## 7. GLOBAL SNOW SYSTEM

Globals: `_GlobalSnowAmount`, `_SnowNormalThreshold`, `_SnowEdgeSoftness`, `_GlobalSnowColor`. Material Properties/blackboard local property olarak tanımlanmaz; local override global'i ezmemeli.

```text
Snowy: amount += accumulationPerSecond * intensity * scaledDeltaTime
Sunny ve season != Winter: amount -= meltPerSecond * scaledDeltaTime
diğer durumlar: amount sabit
amount = clamp(amount, 0, maximumAmount)
```

TimeScale0'da ilerlemez. Sleep geceyi toplu simüle etmez. Bu temperature modeli değildir.

```text
up = normalize(normalWS).y
threshold = max(.001, _SnowNormalThreshold)
upward = softness≈0 ? step(threshold,up)
                     : smoothstep(threshold,threshold+softness,up)
mask = saturate(amount) * upward
albedo = lerp(originalAlbedo, snowColor, mask)
```

Dikey/aşağı yüzeyler karsız. FlatShadedShader mevcut ddx/ddy/cross/normalize world flat normalini kullanır. Snow Lit geometric world normal; Terrain geometric/heightmap normal kullanır, layer normal map maske oluşturmaz. SnowGltf world Normal Vector kullanır. Noise/displacement/tessellation/per-renderer weather script yok.

Snow Lit/Terrain karla metallic0, smoothness.1'e yaklaşır. Flat graph ve SnowGltf sadece BaseColor değiştirir, mevcut material stil parametreleri korunur. Stock URP Lit veya başka custom shader global'i otomatik kullanmaz. Prop/araç/yatak/ev ismi değil kullandığı shader belirleyicidir.

Terrain main/add/base pass desteklidir; basemap generator stock kalır, kar bake edilmez. Üç default Terrain SnowTerrain.mat'e geçti. Terrain_Main_House özel flat graph material'ini korur; graph da kar destekler. Mevcut özel terrain graph'ın layer/hole davranışı yeniden tasarlanmadı.

## 8. SAVE / LOAD

WeatherSaveData: version1, year, season, dayOfSeason, currentWeather, weatherIntensity, globalSnowAmount, randomState. ClockSaveData: time. Transition elapsed/source, lightning timer, cosmetic random kaydedilmez; hedef state'e snap edilir.

Gerçek dış format değişmedi; değerler JSON **string**'dir:

```json
{
  "savedEntities": {
    "world-clock": "{\"DayNightCycleManager\":{\"$type\":\"DayNightCycleManager+ClockSaveData, Assembly-CSharp\",\"time\":22.5}}",
    "world-season-weather": "{\"SeasonManager\":{\"$type\":\"SeasonManager+WeatherSaveData, Assembly-CSharp\",\"version\":1,\"year\":2,\"season\":3,\"dayOfSeason\":12,\"currentWeather\":3,\"weatherIntensity\":0.8,\"globalSnowAmount\":0.65,\"randomState\":17431}}"
  }
}
```

Clock entity önce restore olur, yeni gün eventi üretmez. Season state'i sanitize eder; snow.SetAmount shader global'ini hemen yayınlar. IsRestoringState=true altında season/weather/restore eventleri; görsel listener transition yerine immediate çağırır. Son OnGameLoaded, saat modülasyonu dahil tekrar snap sağlar. Particle kalıntıları ve eski storm temizlenir.

Eski save'de bu entity ID'leri yoksa başlangıç scene state'i kalır; geçmiş hava uydurulmaz. TypeNameHandling.Auto yapısı korundu; mevcut sistem kendi ürettiği güvenilir kayıtları hedefler. Dinamik entity spawn/despawn ve multi-scene bootstrap bu çalışma kapsamında değil.

## 9. UNITY INSPECTOR SETUP

**SampleScene'de otomatik yapılanlar:** SeasonManager objesine SnowAccumulationManager ve world-season-weather ID'li SaveableEntity; DayNightCycleManager'a world-clock ID'li SaveableEntity; dört profile; visual clock referansı; 20s transition; üç stock Terrain'e SnowTerrain.mat. Rain/snow/light/audio bağlantıları korunur. Bunları tekrar eklemeyin.

1. Unity 6000.3.19f1 import/compile bitmesini bekleyin. Açık Editor sahnesi disk değişikliklerini almalı; unsaved eski scene haliyle dosyayı ezmeyin.
2. SampleScene açın. SeasonManager profiles[0..3]=Spring/Summer/Autumn/Winter. Başlangıç Winter/Snowy korunur; farklı başlangıç isterseniz Play dışındayken season/day/weather'ı değiştirin.
3. WeatherVisualsManager: Rain Particles=WeatherEffects/RainEffect, Snow Particles=WeatherEffects/SnowEffect, emission100/100, Lightning Light=LightningLight, Clock=DayNightCycleManager. Mevcut rain/thunder clip'leri dolu olmalı.
4. Snow ayarlarını oyun hızına göre ayarlayın: .002/s, intensity.5 ile 100s'de .1 birikim; .0005/s ile 100s'de .05 erime. Edit Mode'da amount başlangıç örtüsüdür.
5. Terrain, Terrain_(1610.00, 0.00, 1000.00), TerrainLake: SnowTerrain.mat. Terrain_Main_House: mevcut özel flat material korunmalı.
6. Pickup gövdesi ve diğer uygun prop'larda kar için aşağıdaki migration adımlarını uygulayın. Bütün scene materyalleri topluca dönüştürülmedi.
7. **Optional WindZone:** Directional WindZone oluşturup visual.windZone'a bağlayın. **Optional WindAudio:** Loop açık, Play On Awake kapalı, Spatial Blend0 AudioSource; kendi rüzgâr clip'inizi atayıp visual.windAudio'ya bağlayın. Yeni bir rüzgâr sesi asset'i üretilmedi. Boşken cloud/particle drift çalışır.
8. WeatherEffects zaten oyuncu hiyerarşisi altında, precipitationAnchor bu yüzden boş. Ayrı anchor gerekiyorsa emitter'ları başlangıçta doğru yere koyup referansı atayın; Start'ta emitter-anchor offset'i korunur. Parent takibi ve anchor takibini çeliştirmeyin.
9. LightningLight sun/moon'dan ayrı ve enabled kalmalı. Rain/snow shape, material, lifetime/collision mevcut sanat ayarlarıdır; sistem yalnız emission ve velocity.x kontrolünü üstlenir. Yağış materyallerini snow shader'a geçirmeyin.
10. Yeni sahnelerde SeasonManager/Snow aynı objede, iki SaveableEntity ID'si, SaveManager, clock/visual bağlantıları gerekir. Aynı ID'li iki aktif dünya oluşturmayın.
11. Play'de SeasonManager Inspector altındaki Runtime weather controls butonlarını kullanın. Ham serialized weather alanı değişikliği event üretmez; butonlar API'yi çağırır.

## 10. MATERIAL / SHADER MIGRATION

**URP Lit → Plots & Rots/Snow Lit:** Project'te opaque `.mat` veya Hierarchy'de objesini seçin. `Tools > Plots & Rots > Weather > Convert selected URP Lit materials to Snow Lit`. Texture/property/keyword yapısı aynı; asset shader'ı değişir, Undo vardır. Shared material'in başka kullanımları da etkilenir; iç mekânla paylaşılıyorsa önce duplicate yapın. Transparent/embedded/paket/custom material atlanır ve Console'a yazılır.

**Pickup glTF → Plots & Rots/SnowGltf:** Hierarchy'de Pickup kökünü seçin. `Tools > Plots & Rots > Weather > Create snow glTF materials for selected objects`. Sadece glTFast metallic/roughness shader'ıyla tam eşleşen opaque materyaller kopyalanır. Yeni `.mat` dosyaları `Assets/Art/Materials/WeatherMigration` altında; texture subasset referansları, UV ve diğer material değerleri korunur. Renderer sharedMaterials override edilir, GLB kaynağı değişmez. Sahneyi kaydedin. Cam/transparent/transmission atlanır. Undo atamayı geri alır, yeni asset dosyaları kalır. Zaten dönüştürülmüş rendererlarda tekrar kopya üretmez.

**FlatShadedShader:** Material değiştirmeyin; mevcut graph'a fonksiyon eklendi.

**Terrain:** SampleScene bağlantıları yapıldı; yeni stock Terrain için SnowTerrain.mat atayın. Paint/layer/hole verilerini değiştirmeyin; >4 layer ve uzak basemap'i görsel test edin.

**Değiştirmeyin:** LowPolySky, LowPolyWater, Mat_GlassPickup, particle material, SG_Grass ve MedievalTownLite_LIGHT. Son ikisi özel vertex/alpha/wind mantığı incelenerek GlobalSnow_float/half ile ayrıca genişletilebilir. World geometric normal kullanın, snow global'lerini blackboard local/exposed property olarak tekrar eklemeyin.

## 11. TEST CHECKLIST

Gerçekte çalıştırılan kontroller `WeatherValidationResults.md` içindedir. Aşağıdakiler Editor davranış/görsel checklist'idir:

1. **Sunny→Rainy:** Play, Sunny, intensity.8 Rainy. Sky/fog 20s'de yumuşak değişir; rain artışı yaklaşık6s sonra başlar; sonunda emission80/audio.64. Ani burst olmamalı.
2. **Rain→Snow:** .8 Rainy'den .5 Snowy'ye; rain/ses azalır, snow50'ye çıkar. Karın yerdeki miktarı ayrı artar.
3. **Aynı tür intensity:** Rainy.3→Rainy.9. Tür değişmeden event/transition uygulanır; .9 storm koşulunu sağlar.
4. **Storm:** .6'da yok, .61/.9'da transition sonrası10–30s içinde çift flash. .1/.05/.1s; thunder1–3s sonra. Sunny'ye geçiş eski storm'u keser.
5. **Gece:** Rainy'de clock.currentTime12 ve0 karşılaştırın; gece sky/fog kararmalı, mevcut sun/moon/ambient davranışı sürmeli.
6. **Normal mask:** Uyumlu materyalli küp/rampa/araç/prop ve snow1. Üstler beyaz, dikey/alt karsız. Döndürün: world-up yönü izlenmeli. Flat üçgenler okunabilir kalmalı.
7. **Kalıcılık:** snow0, Snowy.5,100s→yaklaşık.1; Cloudy100s değişmez; Spring+Sunny100s→.05 azalır; Winter+Sunny erimez.
8. **Save/Quit/Load:** Snowy.8, snow.65, saat22.5, takvimi not alın. Save, başka state'e geçin veya Play'i yeniden başlatın, Load. Aynı yıl/mevsim/gün/saat/weather/intensity/snow, 20s bekleme yok. JSON'da world-clock/world-season-weather bulunmalı.
9. **Yıl devri:** Winter day30, gece, Sleep veya sabah crossing. Year+1, Spring day1, tek günlük event/zar; HUD calendar'dan güncellenir.
10. **Eski save:** World entity içermeyen kayıt crash üretmez, scene başlangıcı kalır.
11. **Disable/enable:** Storm'da visual disable; flash0, audio/particle durur. Enable güncel state'e snap. Tekrar enable çift coroutine/event üretmemeli.
12. **Authority:** SeasonManager ve clock SetLocalSimulation(false); zaman/zar/kar durur. ApplySnapshot state/globals/görselleri getirir. Bu ağ testi değildir.
13. **Terrain:** Yakın/uzak kamera, dört terrain, >4 layer test arazisi; Forward ve Deferred; kar basemap'te kaybolmamalı, hole kapanmamalı.
14. **Migration:** Pickup gövdesinde SnowGltf, camda eski material. Snow0'da texture/UV/roughness önceki görünümle karşılaştırılır. Prop URP Lit snow0 görünümü korur.
15. **Pause/performance:** TimeScale0 transition/karı durdurur. Profiler'da weather kaynaklı Renderer taraması/material clone artışı olmamalı; skybox clone enable başına bir tane.

## 12. KNOWN ISSUES

- Shader desteği ile sahnede atanmış material farklıdır. Pickup ve uygun prop'larda migration hâlâ gereklidir; tüm dünyanın karlı olduğu iddia edilmez.
- SG_Grass/MedievalTown/diğer custom shaderlar kar desteklemez. Custom foliage wind entegrasyonu da ayrı iştir.
- Shader varyantları URP17.3/glTFast6.19 kaynaklıdır; paket yükseltmesinde upstream farklarını karşılaştırın. Snow Lit uyumluluk için Lit altyapısını korur, mesh'leri otomatik flat-shade etmez.
- Tek global amount; çatı/indoor mask, biome, tekerlek izi, yüzey bazlı erime veya geometri kalınlığı yok. Uygun shader atanmış iç mekân üst yüzeyleri de kar alır. Gameplay traction/crop etkileri yok.
- Temperature yok; sadece Winter dışı Sunny eritir. Sleep sırasında geçen gece toplu snow simülasyonu yapılmaz.
- Gün ortası front/storm/blizzard enum türleri yok; ileride yeni enum değerlerini mevcut sayıların sonuna ekleyin.
- Multiplayer transport/late join/host migration/cosmetic lightning sync yok. Authority/snapshot uzatma noktaları hazır.
- SaveManager mevcut ID/HashSet yapısında; dinamik spawn ve scene bootstrap yok. Yeni sahnede eksik/çakışan ID veya Start öncesi load ayrıca ele alınmalı.
- SeasonManager scene-owned yapıldı: multi-scene geliştirmede snapshot açıkça aktarılmalı. Mevcut tek build sahnesinde sahneye ait particle/light referanslarının kalıcı manager'da boşa düşmesi önlendi.
- Particle çatı collision, lifetime, kapsama ve araç kamerası görünümü gerçek sahnede kontrol edilmeli. Mevcut parent ilişkileri korunur.
- Eski disabled CloudGenerator ani mesh üretimi/material instance davranışı içerir; yeni skybox sistemiyle birlikte etkinleştirmeyin.
- Import/regresyon testleri tam görsel/ses/Forward–Deferred karşılaştırması veya Windows player build değildir. Kesin test kapsamı ayrı sonuç dosyasındadır.

## 13. NEXT DEVELOPMENT STEPS

1. Önce material migration ve Play Mode görsel checklist; sanat/performance değerlerini gerçek oyun hızına göre ayarlayın.
2. Temperature sağlayıcısı mevsim+saatten beslensin; SnowAccumulationManager erime rate/koşulunu buna bağlayın, WeatherVisualsManager'a gameplay sıcaklık eklemeyin.
3. Crop günlük büyüme `SeasonManager.OnDayAdvanced` dinlesin; bu event weather seçiminden sonra gelir. `OnSeasonChanged` uygun mevsim, `OnWeatherChanged` yağış/intensity değişimi için. Yeni listener enable'da güncel state'i de okumalı.
4. Soil moisture/rain watering Rainy+WeatherIntensity'den beslensin; particle emission'dan gameplay hesaplanmasın. Crop/soil kendi ISaveable state'ini tutsun.
5. Kar gameplay etkileri SnowAccumulationManager.Amount/OnSnowAmountChanged üzerinden bağlansın; shader global state kaynağı olmasın.
6. Server'da clock/season/snow simülasyonu açık, client'ta iki SetLocalSimulation(false). WeatherSaveData+ClockSaveData taşınır; ApplySnapshot load/late-join snap'e uygundur. Sürekli ağ güncellemeleri için ayrıca görsel interpolation sözleşmesi gerekir. Client gameplay random çalıştırmamalı.

## 14. COPY-PASTE CONTEXT FOR CHATGPT

Aşağıdaki bağımsız bağlamın ayrı kopyası `CHATGPT_CONTEXT_TR.md` dosyasındadır.
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

