# WaterWorks v4 — Unity 6.3 LTS / URP

Hedef sürüm: kullanıcının belirttiği **6000.3.19f1**. Bu bir kaynak kod güncelleme paketidir; Unity Editor içinde derlenmiş veya görüntüsü doğrulanmış bir unitypackage değildir.

## Fotoğraftaki sorun ve değişiklikler

- Yüzey shader’ında derinlik rengi `floor` ile basamaklara ayrılıyordu. Dalga rengi ve parlama da basamaklıydı. Sürekli renk geçişine dönüştürüldü.
- `Volumetric_Water.shader` hem derinlik rengini hem emilimi basamaklara ayırıyordu. Bunlar da yumuşatıldı. Hacim efekti varsayılan olarak yalnızca kamera su altındayken uygulanır; yüzey üstünde ikinci bir renklendirme katmanı oluşturmaz.
- Yüzey önceden ekran rengini kendi içinde karıştırıp sonra aynı alfa ile tekrar karıştırıyordu. Tek alfa birleştirmesine geçildi; isteğe bağlı kırılma yalnızca renk farkını ekler.
- Fotoğraftaki `Lake_water > Scale Y = 0` fizik hacmini sıfıra indirir. Hazırlama menüsü seçili nesnenin sıfır Y ölçeğini 1 yapar. Parent ölçeği de sıfır olmamalı.
- Fotoğraftaki Plane, X≈402/Z≈372 ölçeğinde. Unity’nin standart 10 m Plane’i ise yaklaşık 4020×3720 m’lik yüzey olur. Büyütmek vertex sayısını artırmaz. Yeni adaptif ağ, tekne/kamera çevresinde ayrıntı sağlar.

Fotoğraf ve kod bu nedenleri destekliyor; sahnenin kendisi, materyal dosyaları ve Renderer ayarları pakette bulunmadığı için birebir görsel yeniden üretim yapılmadı.

## 1. Mevcut projeye güncelleme — önemli

1. Projeni veya mevcut WaterWorks klasörünü **Assets dışında** yedekle. Sahneyi de kaydet.
2. ZIP’i çıkart. İçindeki `Scripts`, `Shaders` ve `Editor` içeriklerini **mevcut aynı dosyaların bulunduğu klasörle birleştir**. Aynı adlı `.cs` dosyalarının üzerine yaz.
3. v3 ve v4 script klasörlerini Assets altında yan yana bırakma: aynı sınıflar iki kez derlenir ve CS0101/CS0111 hataları oluşur.
4. **Projende zaten bulunan `.meta` dosyalarını koru.** Silip baştan import etme. Orijinal ZIP’te bazı scriptlerin ve yüzey shader’ının `.meta` dosyaları yoktu; bu dosyaların projendeki GUID’leri bilinmiyor. Bu yüzden onlar için yeni GUID üretilmedi. Gerçekten yeni dosyaların `.meta` dosyaları pakette bulunur. Mevcut `.meta` ile çakışma olursa projendekini koru.
5. Unity’ye dönüp import/derlemenin tamamlanmasını bekle. İlk Console hatasını çözmeden kurulum menüsüne geçme.
6. Bu rehber v4 için geçerlidir; pakette bırakılan v3 README dosyaları geçmiş açıklamalardır.

## 2. Su yüzeyini hazırla

1. Hierarchy’de **Lake_water** nesnesini seç. Project’teki prefab veya materyali seçme.
2. Menüden **Tools > WaterWorks > 1 - Prepare Selected Lake** çalıştır.
3. Menü mevcut yüzeyin yeni materyal kopyasını `Assets/WaterWorksGenerated` altında oluşturur; `WaterSurface`, `WaterPhysicsVolume`, `WaterAdaptiveMesh` ekler ve suyun katı `MeshCollider` bileşenini devre dışı bırakır. BoxCollider yalnızca trigger olur.
4. Su nesnesi dünya uzayında yatay ve Rotation **(0,0,0)** olmalı. X/Z boyutunu koruyabilirsin; Y Scale **1** olmalı. Konum Y su seviyesidir; fotoğrafındaki değer **30**.
5. Kullanılan URP Asset’te **Depth Texture** açık olsun. Kamera bunu kapatacak şekilde override etmemeli. Menü proje genelindeki URP ayarlarını değiştirmez.
6. `WaterAdaptiveMesh > Follow Target` alanına tekneyi veya ana kamerayı sürükle. Tekne fiziğini yakından inceleyeceksen tekneyi seç. Boş bırakılırsa başlangıçta MainCamera aranır; bulunamazsa göl merkezi kullanılır.
7. Shader **WaterWorks/LowPolyWater_URP6** olmalı. Eski `SSR_Water` Shader Graph bu etkileşim sistemini içermez. Hazırlama menüsü gerekli yüzey shader’ına geçiş yapar.
8. Aynı yerde başka bir görünür su yüzeyi ya da eski kaldırma sistemi varsa onu devre dışı bırak. `Lake_Physics` içindeki eski scriptler ile yeni sistemin aynı Rigidbody’ye iki kez kuvvet uygulamamasını kontrol et. Yeni sistemin su algılaması `Lake_water` üzerindedir.
9. Physics Layer Collision Matrix’te su ve tekne katmanları birbiriyle etkileşebilmeli.

Refraction ilk kurulumda kapalıdır. Açacaksan URP **Opaque Texture** özelliğini de aç, ardından materyalde **Enable Refraction** aç ve Refraction değerini **0.001–0.002** civarında tut. Bu özellik olmadan da dalgalar, köpük ve kaldırma çalışır.

Unity kaynak açıklaması: [URP Depth Texture ve dünya konumu](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/writing-shaders-urp-reconstruct-world-position.html).

## 3. Tekneyi hazırla

1. Teknenin ana nesnesini seç ve **Tools > WaterWorks > 2 - Prepare Selected Floating Object** çalıştır.
2. Aynı nesnede `Rigidbody`, gövdeyi temsil eden bir `Collider` ve `BuoyantObject` bulunmalı. Collider trigger olmamalı. Dinamik MeshCollider kullanacaksan Unity’nin convex kurallarına uy; test için BoxCollider daha kolaydır.
3. Rigidbody: **Use Gravity açık**, **Is Kinematic kapalı**. Dikey hareketi ve teknenin yatma eksenlerini Constraints ile kilitleme. Interpolate menü tarafından açılır.
4. Kütleyi tekneye göre ayarla. `Displaced Volume Override`: teknenin dış gövdesinin suyu dışarıda tuttuğu yaklaşık hacim, m³. Ahşabın kendi hacmi değildir. 0 bırakılırsa ana collider’dan yaklaşık hacim hesaplanır. Çok parçalı collider’larda override kullan; hacimler otomatik toplanmaz.
5. İlk deneme için otomatik BoxCollider örneklemesi yeterli. Özel tekne için alt gövdenin dört köşesine Empty yerleştirip **Buoyancy Points** listesine ver. Noktalar tabanda olmalı; **Submersion Depth** tabandan güverte/su geçirme sınırına kadar yüksekliği temsil eder. Boş/null eleman bırakma.
6. `Wake Origin` isteğe bağlı: kıçta, su çizgisine yakın bir Empty ver. Verilmezse hız yönüne göre arka kenar tahmin edilir. Dalga kaynağını teknenin önüne koyma.
7. Tekne hareketi Rigidbody kuvvetleriyle veya uygun fizik hareket sistemiyle yapılmalı. Transform’u her kare doğrudan taşımak doğru hız/temas hesabını bozabilir. Bu paket tekne sürüş kontrolcüsü içermez.

Başlangıç değerleri:

| Bileşen / ayar | Değer |
|---|---:|
| WaterSurface / Max Ripples | 32 |
| WaterSurface / Ripple Lifetime | 5 s |
| WaterSurface / Ripple Speed | 2.5 m/s |
| WaterSurface / Max Interaction Height | 0.3 m |
| BuoyantObject / Wake Interval | 0.25 s |
| BuoyantObject / Wake Strength | 0.025–0.04 |
| BuoyantObject / Wake Width | 1.2 m |
| BuoyantObject / Wake Wavelength | 2.4 m |
| BuoyantObject / Linear Water Drag, tekne için | 0.25–0.6 |
| BuoyantObject / Vertical Damping | 0.9 |
| BuoyantObject / Wave Normal Influence | 0.05 |
| Materyal / Low Poly Facet Strength | 0.12 |

Dalga izinin görünürlüğünü önce Wake Strength ile ayarla. Su seviyesi veya fizik hacmi için Transform Y Scale’ini değiştirme. Çok hafif cisimler için kuvvet sınırı vardır; gerçek bir sıvı çözücüsü değildir.

## 4. Hazır test — kontrolcü gerektirmez

1. Hazırladığın gölü seç.
2. **Tools > WaterWorks > 3 - Create Wake Test At Selected Lake** çalıştır.
3. 2×1×4 m, 2500 kg bir kutu/tekne temsili oluşur. Follow Target bu kutuya atanır. Gölün merkezinde su/zemin uygun değilse kutuyu başka bir su bölgesine taşı.
4. Play’e bas, kutunun yüzmesini bekle.
5. Kutunun **BuoyantObject** bileşenindeki üç nokta menüsünden **Test - Push forward (Play Mode)** seç.
6. Kutu ileri giderken arkasında yayılan halka izleri, yüzey eğiminde değişim ve hafif köpük görmelisin. Hızı düştüğünde yeni izler kesilmeli, eskiler yaklaşık 5 saniyede sönmeli. Tekrar itebilirsin.
7. Bu test nesnesini oyuna dahil etmek zorunda değilsin; test bitince kaldırabilirsin.

Adaptif mesh **Play Mode’da** üretilir. Edit Mode’da orijinal Plane görünmesi normaldir. Play bitince kaynak mesh geri yüklenir. Bu araç yalnızca düz, dikdörtgen, XZ su yüzeyleri içindir; özel kıyı kesimli veya eğimli bir mesh’e uygulanmamalı. Terrain suyun istenmeyen kısımlarını örter.

## 5. Büyük göl ve performans

- Varsayılan ağ 176×176 hücre, **31.329 vertex / 61.952 üçgen** üretir. Yakındaki 96 m’lik bölge 128 hücreyle, yaklaşık **0.75 m** aralıkla örneklenir.
- 2.4 m dalga boyu için bu başlangıç çözünürlüğüdür. Daha belirgin yükselme için `Detailed Area Size = 64`, `Detailed Segments = 128` ile **0.5 m** aralık kullanabilirsin. Alan ve segment ayarlarını Play dışında değiştir.
- Uzak alan daha kaba olduğu için fizik yüzeyine göre görsel yüksekliğin yaklaşımı orada daha az hassastır. İnce dalga normalleri piksel başına hesaplanır; mesh ayrıntısı ise geometrik şekli belirler.
- Göl başına aynı anda en fazla 32 dalga olayı tutulur. Birden fazla tekne bu bütçeyi paylaşır; kapasite dolunca en eski olay değiştirilir.
- Bir gölde yalnızca bir ayrıntı merkezi vardır. Birbirinden çok uzak tekneler veya bölünmüş ekran için ek LOD tasarımı gerekir.
- Tek mesh, yakın/uzak geçişte ayrı üst üste yüzeyler üretmez. Yeniden merkezleme sırasında özellikle uzak bölgelerde küçük şekil değişimleri görülebilir.
- Hedef cihazda Profiler ile ölçüm yapılmadı. 32 piksel başına etkileşim hesabının GPU maliyeti vardır. Gerekirse Max Ripples’i 16’ya, segment sayısını 96’ya indir; görsel sonuca göre karar ver.

## 6. Kontrol ve bilinen sınırlar

**Burada Unity Editor/URP projesi mevcut değildi. C# API derlemesi, shader derlemesi, Play Mode, DX12 görüntüsü ve build testi yapılmadı.** Statik inceleme sonuçları `Docs/VALIDATION.md` içindedir.

Unity içinde matematik kontrolü için **Tools > WaterWorks > 4 - Run Wave Math Checks** menüsü eklendi. 17 assertion; normal–yükseklik türevi, darbenin yayılması/sönmesi, kapasite ve yükseklik sınırını kontrol eder. Bu kontroller bu çalışma ortamında çalıştırılmadı ve GPU/fizik testinin yerine geçmez.

Manuel kabul testi:

- Console’da yeni script/shader hatası olmamalı; su pembe görünmemeli.
- Eski ekran açısında sığ/derin renk geçişleri keskin katman oluşturmamalı. Low poly hissi düşük Facet Strength ile kalmalı.
- 1×1×1 m BoxCollider, Mass 500, Volume Override 0, Use Box Height açık, Buoyancy Multiplier 1 ile sakin suda yaklaşık yarıya kadar batmalı. Mass 1200 ile dibe inmeli. Testte dalga yüksekliğini geçici olarak 0 yapabilirsin.
- Kutuyu yatay/dikey döndürünce kutunun toplam hacmi yapay olarak büyümemeli.
- Tekne 5 m/s hızla itildiğinde hareket izi oluşmalı; durunca yeni iz kesilmeli.
- Birden fazla collider’lı teknenin yalnızca bir collider’ı trigger dışına çıktığında diğer temaslar devam ettiği sürece kaldırma sürmeli.
- Su/tekne nesnesini devre dışı bırakıp açınca ve Play Mode’a ikinci kez girince eski izler/temaslar kalmamalı.
- Perspektif ve ortografik kamerada; kırılma kapalı/açık koşullarında bak. Mobil/XR bu paketle doğrulanmadı.
- Kamera su üstündeyken eski fullscreen hacim katmanı görünmemeli. Su altına girerken yumuşak geçiş olmalı.

Kapsam: oyun için yaklaşık Arşimet kaldırması + analitik etkileşim dalgaları. Akışkanlar dinamiği, tam Kelvin izi, kıyıdan dalga yansıması, engel etrafında akış, su taşması veya ağ senkronizasyonu yoktur. Karakter yüzme kontrolcüsü eklenmedi. İstersen karakter sistemin `WaterSurface.GetWaterHeight/GetWaterNormal` üzerinden bağlanabilir; bu sınıf Rigidbody tekne/cisim içindir.

Eski fullscreen volume sistemi tek aktif göl materyal bağlamı için korunmuştur. Birden fazla Water_Settings aynı volume materyaline yazarsa son yazan göl kazanır; çoklu gölde fullscreen efekti ayrıca tasarlanmalıdır. Hacmin su çizgisi ortalama düzlemdir; dalga tepelerini birebir izleyen su altı geçişi değildir.

## Geri alma

Assets dışına aldığın yedekteki dosyaları aynı yollara geri koy. Sahneyi yedekten aç veya menü işlemini Undo ile geri al. Oluşturulan yeni materyal asset’leri `Assets/WaterWorksGenerated` altında kalır; referansı kullanılmayanları istersen kaldır. Eski `.meta` dosyalarını koru.
