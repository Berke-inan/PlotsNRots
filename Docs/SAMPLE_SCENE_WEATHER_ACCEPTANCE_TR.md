# SampleScene — son entegrasyon ve manuel kabul

20 Eylül 2026. Lightning ve Player'a bağlı precipitation açıkları kalıcı olarak giderildi. Ayrıntılı A–G rapor: `Docs/WEATHER_INTEGRATION_HANDOFF_TR.md`. Yeni audit: `Docs/SampleSceneWeatherAudit.json`. Game View/görsel/işitsel kabul henüz yapılmadı; aşağıdaki adımlar kullanıcı tarafından Unity'de uygulanmalı.

## Sahnedeki son durum

SeasonManager/PrecipitationRig altında özgün RainEffect ve SnowEffect bulunur. PrecipitationFollower, gerçek Main Camera output transform'una bağlıdır; Player kapansa da rig aktiftir. Particle simulation world-space, maksimum emission100/100. SeasonManager/Lightning Light ayrı, gölgesiz Directional, başlangıç intensity0; WeatherVisuals referansı bağlıdır. Sun/Moon değişmedi. windZone/windAudio/precipitationAnchor isteğe bağlı ve boş.

Üç terrain SnowTerrain, Terrain_Main_House FlatShaded destekli kalır. Pickup Body/Bed/Front_Plate/Rear_Plate ve iki Mountain materyali için seçici Snow Lit kopyaları atanmıştır. Cam/su/particle, belirsiz iç-dış meshler ve embedded GLB kaynakları değiştirilmedi. Bu nedenle bütün prop'ların beyazlaşmasını beklemeyin.

## Hazırlık

SampleScene'i açıp Play'e girin. SaveManager Auto Save Interval Minutes'i yalnız Play Mode'da0 yapın; uzun test gerçek autosave listesinde döngü oluşturmasın. DayNightCycleManager Real Seconds Per Day=0 ile saati sabitleyin; weather transition ve parçacık hareketi sürer. ScriptableObject palet/profil assetlerini değiştirmeyin. Aşağıdaki kontroller SeasonManager inspector'ındaki Runtime weather controls üzerinden yapılır. Intensity slider sonrası weather düğmesine basın. Kalıcı prefab Apply veya migration menülerini bu test sırasında kullanmayın.

### 1. Sunny noon

Clock Current Time12, Temperature10, Intensity.8 → Sunny → Snap visuals to current state. Gradient ve iki cloud katmanını inceleyin; kamerayı döndürün. Pembe materyal/seam olmamalı. Console yeni hata üretmemeli.

### 2. Sunrise

PartlyCloudy + Snap, Current Time5.5→6→6.5→7. Renk/ışık/fog sürekli değişmeli, güneş ufuktan yükselmeli. Kesintisiz hareket görmek için Real Seconds Per Day120 yapıp ardından0'a dönün.

### 3. Sunset

Current Time17→17.5→18→18.5. Güneş karşı ufka inmeli, cloud/ambient/fog birlikte değişmeli. Bu tur palette tuning yapılmadı; gerçek sahne postprocess etkisini burada kabul edin.

### 4. Clear night / stars / moon

Saat22, Sunny + Snap. Ay/yıldız görünmeli; gökyüzü yalnız siyah düz renk olmamalı. FPS/TPS kamera açılarını kontrol edin.

### 5. Overcast star occlusion

Overcast seçin, Snap yapmadan20 saniye bekleyin. Cloud yoğunluğu ve yıldız örtülmesi artmalı. Sonra Sunny'ye dönün; yumuşak geçiş sürmeli.

### 6. Rain .25

Temperature10, Intensity.25 → Rainy. Artan yağış yaklaşık ilk6 saniyeden sonra başlar;20s sonunda hedef25/s, yağmur volume.2. Hafif yağış görünümü/sesi dinlenmeli.

### 7. Rain .85 yürüyüş

Intensity.85 → Rainy;20s sonunda hedef85/s, volume.68. Yürüyün, dönün, FPS/TPS değiştirin. SeasonManager/PrecipitationRig Main Camera'yı takip etmeli; önceden doğmuş damlalar world-space'te kalmalı. Kamera yüksekliği nedeniyle kapsama/offset'i gözle kontrol edin; bu tur özgün particle ayarları korunmuştur.

### 8. Araç enter + FPS/TPS + exit

Yağmur sürerken araca binin, V ile kamera değiştirin, F ile inin. Player inactive olabilir fakat scene rig ve emitters aktif kalmalı. Yağış araç kamerasını izlemeli; giriş/çıkış weather/intensity/calendar state'ini değiştirmemeli. Kamera blend süresi ve dışarıda yağış hissi manuel kabul konusudur.

### 9. Storm lightning + thunder

Storm .85 + Snap. İlk frame flash olmamalı.10–30 saniye sonra ayrı Lightning Light çift flash üretmeli; thunder1–3s gecikmeli gelir. Sun/Moon intensity eğrileri aynı kalmalı. Flash sırasında Sunny'ye basın: Lightning Light intensity hemen0 olmalı. Manager objesini kapatınca da ışık/ses temizlenmeli. Yeni ışık eklemeye veya Inspector referansı doldurmaya gerek yok.

### 10. Snow −5 °C

Autumn başlangıcında Temperature−5, Intensity1 → Snowy + Snap. Snow particle aktif, rain kapalı.30s içinde Ground snow yaklaşık.06 artar. Set ground snow to0/to1 ile terrain, Pickup Body/Bed/plakalar ve mountain karşılaştırın. Dikey yüzeyler az etkilenebilir; normal maskesi beklenen davranıştır. Cam/su/particle korunmalı. Sunny−5 °C'de birikmiş kar kalmalı.

### 11. Melt +10 °C

Temperature10, Sunny.30s içinde yaklaşık.015 erime beklenir. Manuel Set ground snow to1 hızlı başlangıç sağlayabilir. Yer karı weather türü değişince anında silinmemeli.

### 12. Save/load during weather transition

Saat22, Rainy.85, sıcaklık10 ve Ground snow1 ile pause/save menüsünden yeni `WeatherAcceptance_...` kaydı oluşturun; mevcut kaydı ezmeyin. Sunny öğlene ve snow0'a geçin. Başka bir weather geçişi sürerken test kaydını yükleyin: saat/sıcaklık/weather/snow geri gelmeli, rain85/s anında uygulanmalı,20s geçiş baştan oynamamalı. Snowy kaydında snow, Sunny kaydında kapalı precipitation beklenir. Storm kaydı ilk frame flash üretmeden scheduler beklemesine dönmeli.

Play'den çıkınca geçici değerleri scene/prefaba Apply etmeyin. Test save dosyaları Play'den çıkınca silinmez. Geçen/kalan adımları ve Console hatalarını not edin. Hedef cihaz FPS, araçta sürüş, ses ve görsel kalite bu checklist tamamlanana kadar **MANUAL/PENDING** durumundadır.
