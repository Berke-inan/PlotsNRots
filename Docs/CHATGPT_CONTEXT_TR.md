# CONTEXT FOR CHATGPT

Güncel devir: 21 Eylül 2026. Bu dosya tek başına yeni konuşmaya kopyalanabilir. Kaynak kod ve kullanıcının yeni değişiklikleri her zaman source of truth'tur. Snow/weather geçmişi `SKY_WEATHER_HANDOFF_TR.md`; güncel sky/cloud görsel raporu `SKY_CLOUD_VISUAL_HANDOFF_TR.md` dosyasındadır.

## Proje ve koruma sınırı

Plots & Rots low-poly/flat-shaded co-op yönelimli survival/farming/vehicle oyunu. Unity 6000.3.19f1, URP 17.3.0; PC_Renderer Deferred (mode 2), Mobile_Renderer Forward (mode 0). Tek etkin build sahnesi Assets/Scenes/SampleScene.unity. CharacterController, Input System, Cinemachine, inventory/item ScriptableObjects, araç WheelCollider/interaction/audio, BedInteractable uyku ve JSON save mevcut. Tam crop/soil moisture ve network transport implementasyonu bu görevde yazılmadı.

Başlangıç Autumn/Rainy/10 °C korunur. Son entegrasyon turunda kullanıcının açık isteğiyle yalnız WeatherEffects dalı Player prefabından scene-owned rig'e taşındı; Player'ın diğer component'leri baseline karşılaştırmasıyla korundu. Stage/index ve recovery sahnelerine dokunulmadı. Commit/PR oluşturulmadı.

## Saat, takvim ve event sırası

Assets/Scripts/World/DayNightCycleManager.cs: execution -300, scene-owned singleton, ISaveable. realSecondsPerDay 1200; currentTime, IsNight(), Sleep(), Daylight, NormalizedTime, SunDirection, MoonDirection, SetLocalSimulation(bool). Mevcut light/ambient/reflection eğrileri korunur. SunDirection = Quaternion.Euler(hour/24*360-90,170,0)*Vector3.back; moon ters yön. Güneş diski ve directional aynı geometriyi kullanır. Geometrik doğuş/batış 6/18; scene gameplay morningStartTime 7.5 ve nightStartTime 17.5 ayrı eşiklerdir. Yeni gün eventinin gerçek adı YeniGunBasladiSinyali. Sabah sınırı geçişleri sayılır; load yeni gün üretmez. Sleep gece saatini morningStart+.5 yapıp tek event yayınlar.

Assets/Scripts/World/SeasonManager.cs: execution -200; SaveableEntity ve SnowAccumulationManager gerektirir, scene-owned singleton. Salt okunur currentYear, currentDay, currentSeason, currentWeather, WeatherIntensity, CurrentTemperature, AccumulatedSnowAmount, TotalDay, SimulateLocally, IsRestoringState. Private serialized state ve eski alan adları için FormerlySerializedAs korunur. 30 gün/mevsim.

Season {Spring=0,Summer=1,Autumn=2,Winter=3}.
WeatherType {Sunny=0,Cloudy=1,Rainy=2,Snowy=3,PartlyCloudy=4,Overcast=5,Storm=6,SnowStorm=7,Foggy=8}. Eski 0–3 numaralarını değiştirmeyin; paralel Clear/Rain/Snow enum'u eklemeyin.

SeasonManager clock eventini dinler: takvim ilerler, gerekirse OnSeasonChanged(Season), DetermineWeather sıcaklığı seçer ve OnTemperatureChanged(float), ardından weather seçer ve OnWeatherChanged(WeatherType), en sonda OnDayAdvanced(). OnWeatherChanged aynı türde intensity değişince de yayınlanır. OnStateRestored() load snapshot sonunda gelir. Crop günlük büyüme için OnDayAdvanced kullanmalı; önceki state'i okuma riski olan ham clock veya OnSeasonChanged eventini tercih etmemeli. Load da season/temp/weather eventlerini yayınlar ama OnDayAdvanced yayınlamaz.

SetWeather(type,intensity), SetTemperature(float), DetermineWeather() local authority guard taşır. ApplySnapshot(WeatherSaveData) local simulation kapalıyken de server state kabul edebilir. Gerçek multiplayer değildir: gelecekte istemcide hem clock hem season SetLocalSimulation(false) yapılıp server saat/snapshot sırası uygulanmalıdır.

## İklim verisi

Assets/Scripts/World/SeasonWeatherProfile.cs: private serialized Entry[] (weather/weight/minimumIntensity/maximumIntensity), temperatureRange, maximumDailyTemperatureChange=4, freezingPoint=1, persistenceMultiplier=1.8. Assets/Settings/Weather/Spring.asset, Summer.asset, Autumn.asset, Winter.asset scene'e bağlıdır.

Temel ağırlıklar Sunny/Partly/Overcast/Rainy/Storm/Snowy/SnowStorm/Foggy sırasıyla:
Spring 20/25/18/25/5/0/0/7, sıcaklık 6–18 °C.
Summer 55/27/7/8/3/0/0/0, 20–32 °C.
Autumn 10/12/30/32/8/0/0/8, 4–16 °C.
Winter 10/5/25/0/0/50/8/2, −10–3 °C.
Legacy Cloudy ağırlığı sıfır ama API/görsel desteği sürer. Bunlar persistence ve yağış dönüşümünden önceki ağırlıklardır, gerçek sonuç yüzdesi değildir.

SelectForDay önceki aynı tür/yağış ailesine ×1.8 uygular. Assets/Scripts/World/WeatherRules.cs IsRain, IsSnow, IsStorm, SameFamily, ResolvePrecipitation içerir. Summer snow satırları dışlanır; SetWeather da yazın snow'u rain'e çevirir. Diğer mevsimlerde yağış sıcaklık ≤1 °C ise snow, üzeriyse rain; storm ailesi korunur. Sıcaklık günlük random hedefe en fazla 4 °C yaklaşır, sonra sezon aralığına clamp edilir. Bu clamp mevsim sınırında daha büyük atlama üretebilir. Tam meteoroloji veya saatlik sıcaklık yok.

Bağımsız xorshift32 randomState kaydedilir. Günlük seçim roll, intensityRoll, temperatureRoll olmak üzere üç draw tüketir. Storm UnityEngine.Random kullanır; gameplay PRNG'yi etkilemez. Eski Select(roll,intensityRoll,out) tests/compat için persistence olmadan korunur. Null/zero-weight profile Sunny intensity0 fallback verir; profile atanmazsa legacy scene weights yolu vardır.

## Sky ve weather visuals

Assets/Scripts/World/WeatherVisualsManager.cs sadece sunumdur. OnWeatherChanged ve SaveManager.OnGameLoaded dinler. Mevcut rainParticles/snowParticles, rainSoundClip, lightningLight/thunderSounds refs korunur. WeatherAppearanceProfile type/asset: Assets/Scripts/World/WeatherAppearanceProfile.cs ve Assets/Settings/Weather/WeatherAppearance.asset. WeatherVisuals struct sky colors, cloudColor/coverage, fogColor/density, darkness, directLightMultiplier, ambientMultiplier, windStrength ile görsel-only cloudOvercast/cirrusAmount/cloudThickness taşır.

Normal weather değişimi 20s smoothstep transition; yükselen yağış ilk %30'dan sonra başlar. Arada ayrı Overcast gameplay state'i oluşturmaz. Appearance darkness/coverage/fog/direct ve wind intensity ile ayarlanır. C# emission varsayılanı 1500 rain / 1000 snow; gerçek SampleScene iki maksimumu da 100 olarak override eder (100 × intensity); start speed eski prefab baseline × .7–1.2; world X drift wind ×6. Ses volume/pitch mevcut yoldadır. İsteğe bağlı WindZone, windAudio, precipitationAnchor; zorunlu yeni referans değiller. Emitters artık SeasonManager/PrecipitationRig altındadır; özgün particle ayarları korunur. Rain intensity>.6 veya explicit Storm scheduler başlatır; SnowStorm sadece snow/wind. İlk flash 10–30s sonra; .1/.05/.1 flash deseni, thunder 1–3s gecikir. Load/transition önce eski coroutine/flash/sesi durdurur.

Assets/Scripts/World/StylizedSkyController.cs execution100, manager objesinde. Assets/Scripts/World/SkyAtmosphereProfile.cs ve Assets/Settings/Weather/StylizedAtmosphere.asset palet/cloud/disk/star verisi. SkyController OnEnable tek runtime sky clone'ı üretir, OnDisable geri yükler/destroy eder. Normal frame WeatherVisuals.SetWeather ile cache günceller, Sky LateUpdate tek render parametre uygulaması yapar. Load ApplyWeather anında RenderNow çağırır. GameObject taraması, her objeye script, per-frame material clone ve DynamicGI.UpdateEnvironment yok.

Assets/Art/Sky/LowPolySky.shader gradient, sun/moon, stars, hafif B-channel cirrus ve kesintisiz high-altitude ceiling'i taşır. Packed R fair-weather silüet üretmez; G/A yalnız ceiling'de çok düşük tonal varyasyon verir. Sunny/Partly ana bulutları StylizedCloudManager'ın stylize_clouds.glb içindeki üç shared mesh ile kurduğu gerçek world-space 3D clusterlardır. PC pool20/mobile10; shared instanced material; collider/Rigidbody/per-cloud script/shadow yok; 250–800m halka recycle edilir. Ayrıntı ve final 10 GPU kare `Docs/SKY_CLOUD_VISUAL_HANDOFF_TR.md` ve `Docs/SkyPreview/` içindedir.

SkyController güneş yüksekliğinden night/day/twilight gradient/cloud paleti hesaplar. Clock.ApplyAtmosphere direct/ambient çarpanlarını, light tint ve Trilight ambient sky/equator/ground renklerini uygular. RenderSettings.sun gece moon'a geçer; sky material swap olmaz. Fog ExponentialSquared, sky horizon + weather tint, gece yoğunluk katsayısı1.12. Yeni postprocess/bloom eklenmedi. Baked ışık/probe içerikleri otomatik yeniden bake edilmez. Atmosferi tamamen disable etmek için WeatherVisuals ve SkyController birlikte kapanmalı; yalnız WeatherVisuals kapanınca sky saat güncellemesine devam eder.

## Snow ve materyaller

Assets/Scripts/World/SnowAccumulationManager.cs Amount, SetAmount, Simulate(seconds), OnSnowAmountChanged; gameplay state SeasonManager snapshot'ına dahildir. Birikim .002/s × intensity × (1−warmth), erime .0005/s × warmth; warmth=InverseLerp(0,10,temperature). Soğukta durmuş kar kalır; sıcak yağmurlu/bulutlu havada da erir. Precipitation kar ise birikim dalı seçilir. Sleep/offline catch-up, fiziksel kalınlık/iz/roof occlusion yok.

Globals _GlobalSnowAmount/_GlobalSnowColor/_SnowNormalThreshold/_SnowEdgeSoftness. Assets/Art/Shaders/Weather/GlobalSnow.hlsl yukarı dünya normaline mask uygular (threshold .45, softness .05). SnowLit.shader: Plots & Rots/Snow Lit; LitForwardPass.hlsl ve LitGBufferPass.hlsl iki renderer yolu. TerrainLit.shader, TerrainLitAdd.shader, TerrainLitBase.shader, TerrainLitPasses.hlsl splat/add/basemap yollarını destekler; basemap'e snow bake edilmez. SnowTerrain.mat önceki çalışmada default terrain refs'e bağlandı. Assets/FlatShadedShader.shadergraph mevcut flat normalinden GlobalSnow custom function tüketir; Terrain_Main_House bu yolu kullanır. SnowGltf.shadergraph: Plots & Rots/SnowGltf, glTFast metallic-roughness property uyumlu. Unity-LICENSE.md/glTFast-LICENSE.md korunur.

Assets/Editor/WeatherMaterialTools.cs menüleri seçili standalone opaque URP Lit'i Snow Lit'e geçirir; seçili glTF objesinin opaque materiallerini kopyalayıp texture/UV koruyan renderer override yapar. GLB değiştirilmez; transparent/glass/transmission skip edilir. Bütün prop/embedded materialler toplu migrate edilmedi. Undo assignment'ı geri alır; üretilen asseti silmez. Docs/WeatherMaterialAudit.tsv önceki taramanın envanteri, güncel migration sayısı sanılmamalı.

## Save / load

Assets/Scripts/SaveSystem, namespace PlotNRots.SaveSystem. ISaveable.SaveState()->object, LoadState(object). SaveableEntity aynı GO component'lerini tip adıyla sözlüğe koyar; Newtonsoft TypeNameHandling.Auto JSON'u GameData.savedEntities entityID→JSON string içinde saklanır. SaveManager execution−1000 persistent singleton, persistentDataPath/Saves, register/autosave; SaveableEntity Start fallback register. Ayrı save formatı eklenmedi.

SampleScene ID'leri world-clock ve world-season-weather. ClockSaveData.time. SeasonManager.WeatherSaveData v2: version,year,season,dayOfSeason,currentWeather,weatherIntensity,currentTemperature,globalSnowAmount,randomState. DTO version default1 (missing eski alan ayırt edilir), SaveState açıkça2 yazar. Eski save sıcaklığı profil ortalamasından alır. Restore saved weather'ı dönüştürmeden korur; eski invalid Summer+Snow sonraki simulation'a kadar kalabilir. Alan sanitize uygulanır.

Load: clock önce; season/calendar/weather/temp/PRNG; snow.SetAmount; IsRestoringState=true altında temp/season/weather/state-restored eventleri; WeatherVisuals immediate apply eski transition ve storm'u iptal eder, particles.Clear yapar, sky/fog/lighting/audio doğru hedefe gider. Son SaveManager.OnGameLoaded immediate senkronizasyonu tekrarlar. Load NewDay üretmez ve weather reroll yapmaz. Flash scheduler doğru weather'a göre yeni bekleme başlatır, kalan süre serialize edilmez. Aynı sürümde PRNG devamlılığı vardır; eski sürüm iki draw kullandığı için ileri hava dizisi sürümler arasında değişebilir.

## Doğrulama ve açık işler

İzole Unity 6000.3.19f1 / URP17.3.0 son D3D11 koşusunda WeatherValidation.Run 39, SkyWeatherValidation.Run 38 ek kontrol geçti (77 toplam, exit0). Gerçek SampleScene asseti additively açılıp tek cloud manager, shared material, 20 slot ve legacy component yokluğu doğrulandı. `SkyRenderValidation` 1280×720 on URP kare üretti. Gerçek gameplay Play Mode, audio ve hedef mobil cihaz kabulü manuel kalır.

Assets/Editor/SkyRenderValidation.cs yalnız batch modda izole test sahnesi oluşturur ve PC URP üzerinden gerçek PNG üretir. Görseller Docs/SkyPreview; sonuç/kapsam Docs/WeatherValidationResults.md. Bunlar SampleScene görüntüsü veya particle/ses kanıtı değildir. Ana sahnede gün doğumu/batımı, night clouds/star occlusion, save ortasında transition, Player/araç emitter takibi, storm sesleri, snow0/1 tüm materyaller, scene reload ve Mobile Forward cihazı elle test edilmeli. Hedef cihaz FPS veya tüm build shader varyantları doğrulanmış sayılmaz.

Crop başka ekip üyesinin sorumluluğudur; crop kodu eklemeyin. OnDayAdvanced, CurrentTemperature, currentSeason, WeatherIntensity, WeatherRules.IsRain/IsSnow ve TotalDay API'leri korunur. Soil moisture/günlük yağış integrali, yıllık sürekli sıcaklık, network adapter, indoor precipitation/snow occlusion ve prop migration kalan geliştirmelerdir. Mimariyi yeniden yıkmadan bu extension point'leri kullanın.




## Son entegrasyon turu — önceki açık bulgular kapatıldı

A–G rapor: Docs/WEATHER_INTEGRATION_HANDOFF_TR.md; güncel12 adım: Docs/SAMPLE_SCENE_WEATHER_ACCEPTANCE_TR.md. Önceki “lightningLight None / Player altında weather” tespitleri artık geçerli değildir.

Assets/Scripts/World/PrecipitationFollower.cs yeni, DefaultExecutionOrder10000. Serialize gameplayCamera Transform330585546 (Main Camera), LateUpdate yalnız position aktarır; camera rotation, Find/scan veya gameplay state değişimi yoktur. Cinemachine ortak output camera yaya/araç FPS/TPS için zaten kullanılır. Player.SetActive(false) scene-owned rig'i etkilemez; VehicleInteractable ve camera kodu değişmedi. Main output değişirse Inspector referansı güncellenmelidir.

Final hierarchy SeasonManager/PrecipitationRig/RainEffect ve SnowEffect; ayrıca SeasonManager/Lightning Light. Rain scene component890948984, snow627366671 (artık stripped prefab değil); follower900100020. Mevcut particle/renderer/curve/shape/transform verileri korunur; world simulation0. Scene maksimum emission100/100. WeatherVisuals.precipitationAnchor, windZone, windAudio boş ve opsiyonel. Yeni light900100032 ayrı realtime Directional, shadows0, intensity0, bounce0. Sun410087040 ve Moon31820247 değişmedi. StopStorm eski koduyla flash'ı sıfırlar; load ilk10–30s bekleme başlatır. WeatherVisuals/sky/season/save kaynakları bu turda değiştirilmedi.

Assets/Art/Materials/WeatherMigration altında SnowPickupBody/Bed/Plate ve SnowMountainB/25.mat kopyaları oluşturuldu. Pickup prefabındaki Body/Bed/Front_Plate/Rear_Plate renderer override'ları; SampleScene mountain material refs değişti. Yalnız shader GUID Snow Lit'e çevrildi; diğer serialized property/texture/normal/metallic/smoothness/UV verileri birebir aynı. Kaynak materyaller/GLB değişmedi. Belirsiz pCube/pCylinder, Tractor shared/interior ve embedded materyaller otomatik migrate edilmedi; glass/water/particle/custom FX korunur. Güncel audit41 unique serialized material ref,7 snow destekli,17 diğer shader,17 embedded gösterir (runtime obje sayısı değildir).

Assets/Editor/WeatherIntegrationValidation.cs eski39+18 suite'i çağırır, ardından yeni fixture kontrollerini çalıştırır: Player inactive/rig active, world particle konumu, camera follow, rain25/50/85/100, snow/clear immediate restore, storm wait/cancel, gerçek vehicle enter/exit/FPS/TPS metotları ve weather state korunması. Son sonuçlar Docs/WeatherValidationResults.md. Tools/AuditWeatherScene.py gerçek serialized refs/hierarchy'yi tekrar doğrular; Docs/WeatherIntegrationStaticChecks.json89 baseline/property/local-reference assertion sonucu içerir. Gerçek Game View, sürüş, ses, flash parlaklığı, snow0/1 materyal görünümü ve hedef cihaz FPS manuel kabul bekler; engine fixture'larını bunların yerine saymayın.
