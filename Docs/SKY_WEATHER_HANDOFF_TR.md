# Plots & Rots — Sky / Weather teknik devir raporu

> Sky/cloud mimarisi artık 3D fair-weather cluster + kesintisiz high-altitude ceiling hibritidir. Güncel mimari, 75 kontrol ve 10 karelik kabul için `Docs/SKY_CLOUD_VISUAL_HANDOFF_TR.md` esas alınmalıdır. Bu dosyadaki eski procedural/projected-cloud açıklamaları tarihsel kayıttır.

20 Eylül 2026; kullanım limiti sonrası gerçek sahne denetimiyle güncellendi. Bu rapor mevcut sky iyileştirmesinin güncel kaydıdır; önceki `WEATHER_SYSTEM_HANDOFF_TR.md` tarihsel snow/weather çalışmasını anlatır. Repository kodu source of truth'tur.

**Güncel kabul durumu:** Son entegrasyon turunda scene-owned PrecipitationRig, Main Camera takip component'i ve ayrı Lightning Light diske bağlandı. Önceki iki açık bulgu kapatıldı; seçici snow materyal override'ları yapıldı. Sky/season/save tasarımı ve crop API'leri korundu. Bu turun A–G dosya listesi, refs ve doğrulama kapsamı `Docs/WEATHER_INTEGRATION_HANDOFF_TR.md` içindedir. Gerçek Game View/görsel/işitsel kabul manuel bekler.

## 1. PROJECT OVERVIEW

Unity 6000.3.19f1, URP 17.3.0. PC renderer Deferred, Mobile renderer Forward. Etkin build sahnesi `Assets/Scenes/SampleScene.unity`. Karakter, inventory, araç, interaction, uyku ve JSON save sistemleri mevcut. Dünya state'i SeasonManager ve DayNightCycleManager'da; görsel uygulama bunların tüketicisidir. Tam crop sistemi veya çalışan multiplayer transport bu değişikliğin kapsamında değildir.

Autumn/Rainy başlangıcı korunur. Son entegrasyon isteğiyle Player'daki WeatherEffects dalı ayarları korunarak SeasonManager/PrecipitationRig altına taşındı; Player'ın diğer component'leri korunur. Ayrı Lightning Light ve follower refs eklendi. Önceki sky/controller/profile entegrasyonu değişmedi.

## 2. EXISTING SYSTEM ANALYSIS

DayNightCycleManager saat, güneş/ay directional ışıkları, intensity eğrileri, ambient/reflection ve uyku akışını yürütüyordu. Sabah sınırını geçtiğinde `YeniGunBasladiSinyali` yayınlıyordu. SeasonManager bunu dinleyip takvimi ve dört tür weather seçimini ilerletiyordu. Önceki geliştirmeden dört SeasonWeatherProfile, bağımsız PRNG, intensity, snow accumulation, ISaveable ve load immediate yolu zaten vardı; korundu.

WeatherVisualsManager `OnWeatherChanged` dinleyerek 20 saniyelik geçiş, rain/snow emission, yağmur loop sesi, lightning/thunder ve fog uyguluyordu. `SaveManager.OnGameLoaded` sonunda immediate apply yapıyordu. Mevcut particle/light/audio referansları taşınmadı. Önceki controller sky materialini clone edip dört hava presetini gündüz katsayısıyla karartıyordu; ayrı astronomik sky katmanı yoktu.

SaveableEntity aynı objenin ISaveable component'lerini tip adıyla toplar. GameData.savedEntities ID → JSON string sözlüğüdür; iç JSON Newtonsoft TypeNameHandling.Auto kullanır. SaveManager clock entity'yi önce yükler ve sonda OnGameLoaded yayınlar. Yeni save altyapısı eklenmedi.

## 3. VISUAL PROBLEMS FOUND

Eski shader'ın noise/threshold tabanlı bulutları tek ölçekte, sınırlı renk ayrımıyla çalışıyordu. Günün saatini geometrik güneş yüksekliğine bağlayan sky gradient, astronomik güneş/ay diski ve gece yıldız katmanı yoktu. Hava presetini tek brightness çarpanıyla karartmak, gün doğumu ve kapalı geceyi aynı rengin koyu varyasyonuna yaklaştırıyordu. Eski materialde siyah cloud shadow ve yüksek coverage gibi değerler de gri kütle hissini artırıyordu.

Yeni çözüm makro formu üç noise oktavıyla kurar; ton basamaklarını siluete değil bulutun iç rengine uygular. İki ayrı ölçek ve hız, aydınlık/alt yüzey renkleri, ufukta sönümlenme ve saat tabanlı palet bu sorunları hedefler. Görsel kabul için gerçek oyundaki kamera ve sahne kompozisyonu ayrıca değerlendirilmelidir.

## 4. CHANGES MADE

Aşağıdaki yollar repository köküne göredir. Yeni Unity assetlerinin `.meta` dosyaları da eklendi; GUID'leri scene bağlantılarıyla eşleşir.

| FILE | STATUS | PURPOSE | CHANGE |
|---|---|---|---|
| Assets/Art/Sky/LowPolySky.shader | UPDATED | Ana sky render | Aynı shader adı/GUID altında iki cloud katmanı, gradient, güneş, ay, yıldız, AA kenarlar |
| Assets/Art/Sky/SkyBoxMat.mat | UPDATED | Sky başlangıç görünümü | Yeni shader parametreleri ve gündüz paleti; eski kullanılmayan parametreler temizlendi |
| Assets/Scripts/World/StylizedSkyController.cs | NEW | Atmosfer sunumu | Tek runtime material, saat paleti, göksel yönler, fog ve ışık uygulaması |
| Assets/Scripts/World/SkyAtmosphereProfile.cs | NEW | Sanat ayarları | Gündüz/gece/alacakaranlık renkleri, diskler, yıldızlar, cloud ölçek/hızları |
| Assets/Scripts/World/WeatherAppearanceProfile.cs | NEW | Weather görünümü | Tür bazlı coverage, darkness, fog, direct/ambient ve wind verisi |
| Assets/Scripts/World/WeatherRules.cs | NEW | Ortak gameplay kuralları | Yağış aileleri, storm ayrımı ve sıcaklığa göre rain/snow dönüşümü |
| Assets/Scripts/World/DayNightCycleManager.cs | UPDATED | Mevcut saat entegrasyonu | SunDirection/MoonDirection, ApplyAtmosphere/ClearAtmosphere; mevcut event/eğriler korundu |
| Assets/Scripts/World/SeasonManager.cs | UPDATED | İklim state'i | Enum genişlemesi, sıcaklık, günlük seçim, v2 save, crop okuma API'leri |
| Assets/Scripts/World/SeasonWeatherProfile.cs | UPDATED | Data-driven günlük seçim | Sıcaklık aralığı, günlük drift, precipitation threshold, persistence weighting |
| Assets/Scripts/World/WeatherVisualsManager.cs | UPDATED | Görsel/işitsel geçiş | Appearance profili ve sky controller bağlantısı, intensity ile cloud/darkness/fog, particle speed, yeni türler |
| Assets/Scripts/World/SnowAccumulationManager.cs | UPDATED | Yerdeki kar | Birikim ve erimeyi günlük sıcaklığa bağlar |
| Assets/Settings/Weather/Spring.asset | UPDATED | İlkbahar iklimi | Yeni tür ağırlıkları, 6–18 °C |
| Assets/Settings/Weather/Summer.asset | UPDATED | Yaz iklimi | Yeni tür ağırlıkları, 20–32 °C, snow seçim engeli |
| Assets/Settings/Weather/Autumn.asset | UPDATED | Sonbahar iklimi | Yeni tür ağırlıkları, 4–16 °C |
| Assets/Settings/Weather/Winter.asset | UPDATED | Kış iklimi | Yeni tür ağırlıkları, −10–3 °C |
| Assets/Settings/Weather/StylizedAtmosphere.asset | NEW | Hazır sky paleti | Sahneye atanmış stilize atmosfer ayarları |
| Assets/Settings/Weather/WeatherAppearance.asset | NEW | Hazır weather görünümü | Dokuz enum için appearance kayıtları |
| Assets/Scenes/SampleScene.unity | UPDATED | Kullanıma hazır entegrasyon | SeasonManager objesine StylizedSkyController; clock/profil refs; Autumn 10 °C |
| Assets/Editor/SeasonManagerEditor.cs | UPDATED | Play Mode kontrolleri | Sıcaklık slider'ı; mevcut weather/intensity/snow kontrolleri |
| Assets/Editor/WeatherValidation.cs | UPDATED | Eski suite uyumu | Snow birikim/erime testleri yeni sıcaklık kuralına uyarlandı |
| Assets/Editor/SkyWeatherValidation.cs | NEW | Davranış regresyonu | İklim, persistence, save migration, authority, orbit, stars, ışık, material lifecycle |
| Assets/Editor/SkyRenderValidation.cs | NEW | GPU görsel doğrulama | İzole test sahnesini gerçek PC URP üzerinden PNG'ye render eder |
| Docs/SKY_WEATHER_HANDOFF_TR.md | NEW | Teknik devir | Bu 17 bölümlük rapor |
| Docs/WEATHER_SYSTEM_HANDOFF_TR.md | UPDATED | Tarihsel kayıt | Güncel rapora yönlendiren not eklendi |
| Docs/CHATGPT_CONTEXT_TR.md | UPDATED | Bağımsız devam bağlamı | Güncel mimari ve bilinen sınırlar |
| Docs/WeatherValidationResults.md | NEW | Kanıt ve tekrar çalıştırma | Test/import/render sonuçları ve kapsam |
| Tools/AuditWeatherScene.py | NEW — devam | Salt okunur gerçek scene denetimi | YAML fileID/GUID, prefab, materyal ve referans envanteri |
| Docs/SampleSceneWeatherAudit.json | NEW — devam | Güncel statik kanıt | Çözülmüş referanslar, materyal sayıları, kaynak hashleri |
| Docs/SAMPLE_SCENE_WEATHER_ACCEPTANCE_TR.md | NEW — devam | Kullanıcı Play Mode kabulü | 12 adım, açık entegrasyon bulguları, beklenen değerler |
| Docs/SkyPreview/*.png | NEW | Görsel kanıt | İzole render test sahnesi; SampleScene ekran görüntüsü değildir |

Korunan snow altyapısı: `Assets/Art/Shaders/Weather/*`, `Assets/FlatShadedShader.shadergraph`, `Assets/Editor/WeatherMaterialTools.cs`, `Tools/ValidateWeather.ps1`, SaveManager/SaveableEntity değişmeden kullanılmaktadır. Bunlar bu aşamada yeniden yazılmadı.

## 5. FINAL ARCHITECTURE

```text
DayNightCycleManager -- YeniGunBasladiSinyali --> SeasonManager
       |                                          |
       | saat + SunDirection                      +-- SeasonWeatherProfile
       |                                          |   sıcaklık + ağırlık + persistence
       |                                          +-- OnDayAdvanced --> gelecekte crop
       |                                          +-- OnTemperatureChanged --> tüketiciler
       |                                          +-- OnWeatherChanged
       |                                                  |
       v                                                  v
StylizedSkyController <--- geçişli appearance --- WeatherVisualsManager
       |                       WeatherAppearanceProfile    | rain/snow/audio/storm
       +-- SkyAtmosphereProfile                            | wind/emitter follow
       +-- tek sky material + fog + clock.ApplyAtmosphere

SeasonManager + sıcaklık --> SnowAccumulationManager --> global HLSL maskesi
SaveManager --> clock restore --> season/temp/snow restore --> immediate visuals
            --> OnGameLoaded --> son görsel senkronizasyon
```

`OnDayAdvanced` günlük state güncellendikten sonra yayınlanır. Crop büyümesi için clock eventini doğrudan dinlemek yerine bu event uygundur; böylece eski weather'ı okuma riski azalır. `OnSeasonChanged` takvim değişiminde yeni weather seçilmeden önce gelir. Load sırasında da season/weather/temp eventleri vardır; günlük büyümeyi bu eventlerden tetiklemeyin.

## 6. SKY SYSTEM

Shader adı `Custom/LowPolySky`. Tek pass, URP HLSL target 3.0; volumetric/raymarch/cloud mesh yok. View direction temelli dome projection kullanılır; kamera pozisyonu bulut UV'sini sürmez. Ufukta payda sonludur. Üç oktav makro noise iki katmanda farklı koordinatlarla örneklenir. Büyük katman scale 2.6, hız çarpanı .55; yakın katman ölçek oranı 1.9, hız 1.25. WindDirection (1,.3), CloudSpeed .008; rüzgâr şiddeti hareketi değiştirir. Ton bandı sayısı 4; kenarlarda türev tabanlı anti-aliasing bulunur.

Zenith/mid/horizon paletleri güneşin yüksekliğine göre gündüz, gece ve alacakaranlık arasında sürekli blend olur. Güneş yarıçapı .9°, ay 1.1°. Hafif halo vardır; yeni bloom/postprocess eklenmedi. Ay güneşin ters yönündedir, basit tonlu yüzeye sahiptir; astronomik ay evresi simülasyonu yoktur. Yıldızlar octahedral göksel koordinatlarda procedural noktalardır; saatle döner, gündüz söner. Her iki bulut katmanı yıldız, ay ve güneşin üzerine alpha ile bindirilir.

Runtime sky clone'ı yalnızca OnEnable'da oluşur. LateUpdate profile ve saatten shader parametrelerini günceller. WeatherVisuals normal karelerde yalnızca hedef sunum verisini aktarır; load'da ApplyWeather aynı yolu anında çalıştırır. Gece/gündüz material swap yapılmaz. Cloud displacement kozmetiktir, kaydedilmez.

## 7. WEATHER / SEASON SYSTEM

Enum numaraları korunmuştur: Sunny=0, Cloudy=1, Rainy=2, Snowy=3, PartlyCloudy=4, Overcast=5, Storm=6, SnowStorm=7, Foggy=8. Season: Spring=0, Summer=1, Autumn=2, Winter=3. Yeni paralel Clear/Rain enum'u yoktur.

| Mevsim | °C | Sunny | Partly | Overcast | Rainy | Storm | Snowy | SnowStorm | Foggy |
|---|---|---|---|---|---|---|---|---|---|
| Spring | 6–18 | 20 | 25 | 18 | 25 | 5 | 0 | 0 | 7 |
| Summer | 20–32 | 55 | 27 | 7 | 8 | 3 | 0 | 0 | 0 |
| Autumn | 4–16 | 10 | 12 | 30 | 32 | 8 | 0 | 0 | 8 |
| Winter | −10–3 | 10 | 5 | 25 | 0 | 0 | 50 | 8 | 2 |

Bunlar temel ağırlıklardır, nihai yüzdeler değildir. Legacy Cloudy tüm profillerde 0 ağırlıkla desteklenir. Bir önceki weather ile aynı tür veya aynı yağış ailesi 1.8 kat ağırlık alır. Günlük sıcaklık hedefi aralıktan seçilir, önceki değerden en fazla 4 °C yaklaşır, sonra mevsim aralığına clamp edilir. Mevsim sınırında bu clamp 4 °C'den büyük değişime neden olabilir; yıllık sürekli sıcaklık eğrisi gelecekte eklenebilir.

Yağış gerekiyorsa sıcaklık ≤1 °C ve mevsim Summer değilse snow; aksi halde rain. Storm/SnowStorm bu dönüşümde şiddetli ailesini korur. Summer profili snow satırlarını tamamen dışlar; SetWeather da yazın snow'u rain'e çevirir. Kışın sıcak günlerde profilede seçilen snow yağmura dönebilir. Sunny günler karda birikimi otomatik silmez.

WeatherIntensity ayrı 0–1 state. Normal türler .2–1; storm türleri .65–1. Coverage, darkness, fog, direct light ve wind intensity ile modüle edilir; emission/ses doğrudan intensity kullanır. Yağış artışı 20 saniyelik geçişin ilk %30'undan sonra başlar. Sistem ara Overcast gameplay günü üretmez; görsel cloud artışı yağıştan önce gelir. Yüksek Rainy intensity veya açık Storm thunder scheduler başlatır; SnowStorm şu anda rüzgâr/kar fırtınasıdır, lightning üretmez. Load yeni bir 10–30 saniye beklemeyle scheduler kurar; anında flash yapmaz.

## 8. LIGHTING / ATMOSPHERE

Saatin mevcut sun/moon intensity ve ambient/reflection eğrileri korunur. SkyController direct ve ambient çarpanlarını ve renklerini clock'a verir. SunDirection ile directional light aynı zaman geometrisini kullanır; moon ters yöndedir. RenderSettings.sun ufuk üzerinde uygun ışığa geçirilir; sky material değişmez.

AmbientMode.Trilight ve saatle değişen sky/equator/ground renkleri uygulanır. Fog ExponentialSquared'dır; rengi sky horizon ve profile fog tint karışımı, yoğunluğu weather + gece katsayısıdır. Yağmurlu gece soğuk/koyu, karlı sabah açık/soluk, öğlen berrak mavi palet kullanır. DynamicGI.UpdateEnvironment her kare çağrılmaz. Baked lightmap veya reflection probe içerikleri gerçek zamanlı yeniden bake edilmez.

## 9. SNOW SYSTEM

Snow precipitation particle miktarıdır; accumulated snow ayrı 0–1 state'tir. `Amount`, `SetAmount`, `Simulate(seconds)`, `OnSnowAmountChanged` ve SeasonManager.AccumulatedSnowAmount kullanılabilir. Default snow birikim hızı .002/s × intensity × (1−warmth), erime .0005/s × warmth; warmth 0–10 °C inverse lerp. Soğukta duran kar kalır; sıcak yağmurlu/bulutlu havada da erir. Kar yağışı devam ediyorsa o karede birikim yolu kullanılır. Sleep/oyun kapalı süre için catch-up uygulanmaz.

`_GlobalSnowAmount`, `_GlobalSnowColor`, `_SnowNormalThreshold`, `_SnowEdgeSoftness` GlobalSnow.hlsl tarafından dünya normalinin yukarı yönüne göre yüzeye uygulanır. Bu sadece global float ataması değildir: URP Lit Forward/GBuffer pass'leri, terrain splat/add/base pass'leri ve flat graph çıktısı maskeyi tüketir. Fiziksel kalınlık, iz, çatı altı occlusion veya mesh displacement yoktur; üst yöne bakan iç mekân yüzeyleri de etkilenebilir.

## 10. SAVE / LOAD

Scene SaveableEntity ID'leri `world-clock` ve `world-season-weather`. ClockSaveData.time ayrı entity'de. WeatherSaveData v2: year, season, dayOfSeason, currentWeather, weatherIntensity, currentTemperature, globalSnowAmount, randomState. version alanı DTO'da bilinçli olarak 1 varsayılanlıdır; SaveState açıkça 2 yazar. Eski kayıtta sıcaklık yoksa o mevsimin profil ortalaması kullanılır; 0 °C yanlışlıkla missing sanılmaz.

Load sırası: clock restore; calendar/weather/temperature/PRNG restore; snow amount/global shader restore; IsRestoringState altında eventler; WeatherVisuals immediate target + eski transition/flash/audio iptali + eski particle temizliği; SkyController anında parametreleri uygular; SaveManager.OnGameLoaded son senkronizasyonu yapar. Günlük seçim veya NewDay load'da tetiklenmez. Kaydedilmiş weather yeniden zar atılmadan korunur. Eski/elle bozulmuş bir Summer+Snow save'i aynen yüklenebilir; sonraki simülasyon güncel kuralları uygular.

PRNG xorshift32 gameplay'e özeldir, storm'un UnityEngine.Random çağrılarıyla karışmaz. Yeni sıcaklık seçimi üçüncü PRNG draw'ıdır; eski sürümle gelecekteki hava dizisinin birebir aynı kalması beklenmez. Aynı yeni sürümde save/load devamlılığı korunur. Cloud offset ve flash'ın tam kalan süresi kaydedilmez.

## 11. UNITY INSPECTOR SETUP

Kurulum diske yazıldı: SeasonManager altında PrecipitationRig/PrecipitationFollower ve ayrı Lightning Light. Follower Main Camera transform330585546'ya bağlı; rain890948984/snow627366671 scene-owned, lightning900100032 ayrı Directional. WeatherVisuals clock/sky/profile/audio refs korunur; emission100/100. Player'daki eski dal kaldırılmıştır; tekrar particle/light eklemeyin. windZone/windAudio/precipitationAnchor isteğe bağlı ve boş kalır. Palette/season ayarı değiştirilmedi.

Gerçek kabul için `Docs/SAMPLE_SCENE_WEATHER_ACCEPTANCE_TR.md` içindeki12 adımı kullanın. Son serialized refs ve değişen her dosya `Docs/WEATHER_INTEGRATION_HANDOFF_TR.md` B–D bölümlerindedir.

## 12. MATERIAL / SHADER NOTES

Sky shader adı ve GUID korunduğu için skybox reassign gerekmez. Terrain varsayılan malzemeleri önceki çalışmada SnowTerrain'e bağlandı; Terrain_Main_House flat graph kullanır. `Plots & Rots/Snow Lit`, `Plots & Rots/Terrain/Snow Lit`, `Plots & Rots/SnowGltf` destekli yollardır.

Standart URP Lit ve model embedded materialler global snow'u kendiliğinden okuyamaz. Bütün prop materyalleri otomatik migrate edilmedi. `WeatherMaterialTools` menüsünde seçili standalone opaque Lit materyalleri Snow Lit'e dönüştürebilirsiniz. glTF Pickup için seçili objenin opaque materyallerini kopyalayan araç, texture/UV property'lerini koruyarak renderer override oluşturur. GLB dosyası değiştirilmez. Glass/transparent/transmission atlanır; asset kopyaları Undo ile silinmez. Önce bir objede snow 0/1, normal/roughness ve texture karşılaştırması yapın.

`GlobalSnow.hlsl`, `SnowLit.shader`, `LitForwardPass.hlsl`, `LitGBufferPass.hlsl`, terrain shader/pass dosyaları, `SnowGltf.shadergraph` ve kökte `FlatShadedShader.shadergraph` önceki çalışmadan korunur. Paket upgrade'inde kopyalanmış URP/glTF shader kaynakları upstream ile karşılaştırılmalıdır; lisans dosyaları yanlarındadır.

## 13. AUTOMATED VALIDATION RESULTS

Runtime ve Editor Roslyn derlemeleri geçti. İzole Unity 6000.3.19f1 projesinde 39 weather regresyon kontrolü ve 18 yeni sky/climate kontrolü geçti; toplam 57, Unity exit 0. Kapsam: eski snapshot/PRNG/day/snow davranışı, sıcaklık ve persistence, Summer engeli, v1→v2 sıcaklık fallback'i, authority guard, sürekli orbit, star/daylight, weather darkness, Trilight ve material lifecycle.

GPU render sonuçları, güncel son koşu ve kapsam sınırları `WeatherValidationResults.md` dosyasındadır. İzole proje mevcut kaynakların kopyası ve aynı yerel URP paketlerini kullanır; ana oyun sahnesinin tüm asset/oyuncu akışlarının uçtan uca testi değildir. Shader import kontrolü platform build veya bütün shader keyword varyantlarını garanti etmez.

Devam aşamasında `Tools/AuditWeatherScene.py` gerçek scene/prefab referanslarını kontrol etti; sonuç `Docs/SampleSceneWeatherAudit.json`. Dört terrain destekli shader yoluna bağlı. 41 farklı serialized material referansı: 2 snow destekli, 19 URP Lit, 17 embedded model/GLB, 1 town graph, 1 water, 1 particle. Runtime instance sayısı veya bütün imported model envanteri değildir. Okunan gerçek Editor.log SampleScene yüklenmesini içeriyor; C#/shader/NullReference/MissingReference hata eşleşmesi yok. Bu gözlem yeni bir Play Mode testi değildir.

## 14. MANUAL TEST CHECKLIST

**Uygulanabilir sıra ve beklenen değerler:** `Docs/SAMPLE_SCENE_WEATHER_ACCEPTANCE_TR.md` içindeki 12 adımı kullanın. SO asseti düzenlemeyin veya migration menülerini Play Mode testi sırasında çalıştırmayın; gerçek sahnenin görsel kabulü bekliyor.

- SampleScene Play Mode: 05:30→08:00 ve 16:30→20:00 saat aralıklarında horizon, sun/moon yönü, ambient ve fog sürekliliği.
- Berrak gece: ay/yıldız görünümü; Overcast/Storm geçişinde bulut occlusion. Kamerayı döndürüp ufuk/projection artefaktı kontrolü.
- Sunny→Rainy intensity .25/.85: cloud önce, emission/ses sonra; geçiş sırasında yeni weather komutunda sıçrama olmaması.
- Rainy gece save; Sunny öğlene geç; load: saat, kar miktarı, sıcaklık, cloud/fog/particles/audio aynı karede doğru state; zorunlu flash olmaması.
- Snowy −5 °C: snow artışı; Sunny −5 °C: kar korunması; +10 °C: erime. Snow 0/1 ile terrain, flat prop, Snow Lit ve migrate edilmiş Pickup kontrolü.
- Mevsim sonu sleep ve çoklu gün sınırı: tek günlük callback sayısı, yeni sıcaklık/weather ve crop için OnDayAdvanced sırası.
- Player/araç hareketinde rain/snow emitters ve indoor precipitation davranışı; ses seviyeleri gerçek oyunda dinlenmeli.
- Manager objesini disable/enable ve sahneyi tekrar yükle: particle/audio/flash temizliği, material clone sızıntısı olmaması.
- PC Deferred ve hedef Mobile Forward cihazlarında GPU profiler, star aliasing, yoğun bulut fill-rate ve material varyantları.

## 15. KNOWN ISSUES

Önceki lightning None ve Player lifecycle açıkları giderildi. Gerçek araç sürüşünde camera blend/yağış kapsaması, flash parlaklığı ve ses manuel kabul bekler. Main Camera output değişirse follower referansı da güncellenmelidir.

Gerçek SampleScene'de etkileşimli Play Mode, ses dinleme, bütün prefab materyalleri, hedef mobil cihaz ve tam oyuncu build'i bu otomatik testin kapsamında değildir. Görsel render kanıtı sade bir test sahnesidir. Performans mimarisi hafiftir fakat hedef cihazda ölçülmüş FPS bütçesi iddiası yoktur. Standalone/embedded opaque prop migration'ı seçime bağlıdır. Network replication uygulanmadı; local authority/snapshot API'si yalnız hazırlıktır. Moon phases, mevsimsel gün uzunluğu, indoor weather occlusion, snow footprint/displacement, offline snow catch-up ve soil moisture yoktur. Günlük sıcaklık mevsim sınırında clamp nedeniyle keskin değişebilir. Bulut kozmetik offset'i load'da tam önceki konumu garanti etmez.

## 16. NEXT DEVELOPMENT STEPS

Öncelik artık12 adımlı gerçek Game View kabulüdür. Seçici beş Snow Lit kopyası bağlandı; belirsiz interior/exterior materyaller ve embedded kaynaklar sonraki ayrı incelemeye bırakıldı. Crop başka ekip üyesinindir; bu çalışmada crop kodu yazılmadı.

Crop için `OnDayAdvanced` dinleyen ayrı bir CropGrowthSystem eklenebilir: CurrentTemperature, currentSeason, WeatherIntensity ve WeatherRules.IsRain/IsSnow okuyup günlük derece-gün, sulama ve don hesabı yapar. Load eventlerinden büyüme üretmemeli; kendi ISaveable state'inde son işlenen TotalDay tutmalı. Soil moisture ve yağışın günlük toplamı bugün mevcut değildir, ayrı state olarak tasarlanmalıdır.

Ardından gerçek oyunda atmosfer palette tuning, gerekli prop migration, hedef GPU ölçümü ve multiplayer server snapshot adapter'ı uygundur. Saat ve SeasonManager için SetLocalSimulation(false) istemciye açılmadan önce server'ın saat/calendar/weather snapshot sırası tanımlanmalıdır.

## 17. CONTEXT FOR CHATGPT

Bağımsız dosya `Docs/CHATGPT_CONTEXT_TR.md`.

```text
# CONTEXT FOR CHATGPT

Güncel devir: 20 Eylül 2026. Bu dosya tek başına yeni konuşmaya kopyalanabilir. Kaynak kod ve kullanıcının yeni değişiklikleri her zaman source of truth'tur. Önceki 14 bölümlük WEATHER_SYSTEM_HANDOFF_TR.md snow çalışmasının tarihsel kaydıdır; güncel rapor SKY_WEATHER_HANDOFF_TR.md dosyasıdır.

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

Assets/Scripts/World/WeatherVisualsManager.cs sadece sunumdur. OnWeatherChanged ve SaveManager.OnGameLoaded dinler. Mevcut rainParticles/snowParticles, rainSoundClip, lightningLight/thunderSounds refs korunur. WeatherAppearanceProfile type/asset yeni: Assets/Scripts/World/WeatherAppearanceProfile.cs ve Assets/Settings/Weather/WeatherAppearance.asset. WeatherVisuals struct sky colors, cloudColor/coverage, fogColor/density, darkness, directLightMultiplier, ambientMultiplier, windStrength taşır.

Normal weather değişimi 20s smoothstep transition; yükselen yağış ilk %30'dan sonra başlar. Arada ayrı Overcast gameplay state'i oluşturmaz. Appearance darkness/coverage/fog/direct ve wind intensity ile ayarlanır. C# emission varsayılanı 1500 rain / 1000 snow; gerçek SampleScene iki maksimumu da 100 olarak override eder (100 × intensity); start speed eski prefab baseline × .7–1.2; world X drift wind ×6. Ses volume/pitch mevcut yoldadır. İsteğe bağlı WindZone, windAudio, precipitationAnchor; zorunlu yeni referans değiller. Emitters artık SeasonManager/PrecipitationRig altındadır; özgün particle ayarları korunur. Rain intensity>.6 veya explicit Storm scheduler başlatır; SnowStorm sadece snow/wind. İlk flash 10–30s sonra; .1/.05/.1 flash deseni, thunder 1–3s gecikir. Load/transition önce eski coroutine/flash/sesi durdurur.

Assets/Scripts/World/StylizedSkyController.cs execution100, manager objesinde. Assets/Scripts/World/SkyAtmosphereProfile.cs ve Assets/Settings/Weather/StylizedAtmosphere.asset palet/cloud/disk/star verisi. SkyController OnEnable tek runtime sky clone'ı üretir, OnDisable geri yükler/destroy eder. Normal frame WeatherVisuals.SetWeather ile cache günceller, Sky LateUpdate tek render parametre uygulaması yapar. Load ApplyWeather anında RenderNow çağırır. GameObject taraması, her objeye script, per-frame material clone ve DynamicGI.UpdateEnvironment yok.

Assets/Art/Sky/LowPolySky.shader, shader adı Custom/LowPolySky, GUID korundu. SkyBoxMat.mat yeni varsayılan parametrelerle güncellendi. Tek URP HLSL pass target3.0. Üç renkli gradient, güneş/ay AA diskleri + hafif güneş halo, procedural octahedral starfield. Stars güneş yüksekliğiyle görünür, saat matrisiyle döner. Moon=-Sun, faz yok. İki procedural noise cloud katmanı: macroScale2.6, ratio1.9, speed .55/1.25, toneSteps4, windDirection(1,.3), base speed .008. Dome projection world/view yönüne bağlı, camera position kullanmaz. Üç oktav makro alan, threshold silueti smooth; posterization iç renkte. Cloud alpha yıldız/ay/güneşi örter. Gerçek volumetric veya mesh bulut yok. Cloud displacement kaydedilmez.

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

Runtime+Editor Roslyn derlemesi Tools/ValidateWeather.ps1 ile geçti. İzole Unity 6000.3.19f1, yerel URP17.3.0 paketlerinde WeatherValidation.Run 39, SkyWeatherValidation.Run 18 ek kontrol geçti (57 toplam, exit0). Editor menüsü Tools/Plots & Rots/Weather/Run sky and climate regression checks. Tests yeni fixture GameObject'ler oluşturup RenderSettings'i geri yükler; Play Mode veya gerçek oyuncu build testi değildir.

Assets/Editor/SkyRenderValidation.cs yalnız batch modda izole test sahnesi oluşturur ve PC URP üzerinden gerçek PNG üretir. Görseller Docs/SkyPreview; sonuç/kapsam Docs/WeatherValidationResults.md. Bunlar SampleScene görüntüsü veya particle/ses kanıtı değildir. Ana sahnede gün doğumu/batımı, night clouds/star occlusion, save ortasında transition, Player/araç emitter takibi, storm sesleri, snow0/1 tüm materyaller, scene reload ve Mobile Forward cihazı elle test edilmeli. Hedef cihaz FPS veya tüm build shader varyantları doğrulanmış sayılmaz.

Crop başka ekip üyesinin sorumluluğudur; crop kodu eklemeyin. OnDayAdvanced, CurrentTemperature, currentSeason, WeatherIntensity, WeatherRules.IsRain/IsSnow ve TotalDay API'leri korunur. Soil moisture/günlük yağış integrali, yıllık sürekli sıcaklık, network adapter, indoor precipitation/snow occlusion ve prop migration kalan geliştirmelerdir. Mimariyi yeniden yıkmadan bu extension point'leri kullanın.




## Son entegrasyon turu — önceki açık bulgular kapatıldı

A–G rapor: Docs/WEATHER_INTEGRATION_HANDOFF_TR.md; güncel12 adım: Docs/SAMPLE_SCENE_WEATHER_ACCEPTANCE_TR.md. Önceki “lightningLight None / Player altında weather” tespitleri artık geçerli değildir.

Assets/Scripts/World/PrecipitationFollower.cs yeni, DefaultExecutionOrder10000. Serialize gameplayCamera Transform330585546 (Main Camera), LateUpdate yalnız position aktarır; camera rotation, Find/scan veya gameplay state değişimi yoktur. Cinemachine ortak output camera yaya/araç FPS/TPS için zaten kullanılır. Player.SetActive(false) scene-owned rig'i etkilemez; VehicleInteractable ve camera kodu değişmedi. Main output değişirse Inspector referansı güncellenmelidir.

Final hierarchy SeasonManager/PrecipitationRig/RainEffect ve SnowEffect; ayrıca SeasonManager/Lightning Light. Rain scene component890948984, snow627366671 (artık stripped prefab değil); follower900100020. Mevcut particle/renderer/curve/shape/transform verileri korunur; world simulation0. Scene maksimum emission100/100. WeatherVisuals.precipitationAnchor, windZone, windAudio boş ve opsiyonel. Yeni light900100032 ayrı realtime Directional, shadows0, intensity0, bounce0. Sun410087040 ve Moon31820247 değişmedi. StopStorm eski koduyla flash'ı sıfırlar; load ilk10–30s bekleme başlatır. WeatherVisuals/sky/season/save kaynakları bu turda değiştirilmedi.

Assets/Art/Materials/WeatherMigration altında SnowPickupBody/Bed/Plate ve SnowMountainB/25.mat kopyaları oluşturuldu. Pickup prefabındaki Body/Bed/Front_Plate/Rear_Plate renderer override'ları; SampleScene mountain material refs değişti. Yalnız shader GUID Snow Lit'e çevrildi; diğer serialized property/texture/normal/metallic/smoothness/UV verileri birebir aynı. Kaynak materyaller/GLB değişmedi. Belirsiz pCube/pCylinder, Tractor shared/interior ve embedded materyaller otomatik migrate edilmedi; glass/water/particle/custom FX korunur. Güncel audit41 unique serialized material ref,7 snow destekli,17 diğer shader,17 embedded gösterir (runtime obje sayısı değildir).

Assets/Editor/WeatherIntegrationValidation.cs eski39+18 suite'i çağırır, ardından yeni fixture kontrollerini çalıştırır: Player inactive/rig active, world particle konumu, camera follow, rain25/50/85/100, snow/clear immediate restore, storm wait/cancel, gerçek vehicle enter/exit/FPS/TPS metotları ve weather state korunması. Son sonuçlar Docs/WeatherValidationResults.md. Tools/AuditWeatherScene.py gerçek serialized refs/hierarchy'yi tekrar doğrular; Docs/WeatherIntegrationStaticChecks.json89 baseline/property/local-reference assertion sonucu içerir. Gerçek Game View, sürüş, ses, flash parlaklığı, snow0/1 materyal görünümü ve hedef cihaz FPS manuel kabul bekler; engine fixture'larını bunların yerine saymayın.

```
