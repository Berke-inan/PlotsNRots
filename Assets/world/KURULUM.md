# Boyanabilir Low Poly Terrain

Bu paket, gönderdiğin `FlatShadedShader.shadergraph` dosyasındaki köşeli normal
hesabını Terrain'in katmanlı boyamasıyla birleştirmek için hazırlanmıştır.
Graph dosyasının yerine, Terrain'e uygun yeni bir materyal oluşturur.

## Dosyanda bulunan sorun

Base Color bağlantısı yalnızca `GroundTex → Sample Texture 2D → Brightness ile
Multiply` üzerinden geliyor. Terrain'in boya ağırlıklarını (_Control) ve Terrain
Layer dokularını (_Splat0 vb.) okumuyor. Bu nedenle Paint Texture ile yapılan
değişiklikler bu shader'ın rengini etkileyemez. Boyama verisinin gerçekten
oluştuğu ayrıca Unity'de kontrol edilmelidir.

Köşeli normal zinciri `Position (World) → DDX/DDY → Cross Product → Normalize`.
Bu dünya uzayı sonucu `Normal (Tangent Space)` girişine doğrudan bağlı. Yeni
shader normal hesabını dünya uzayında kullanır ve ters yöne bakmasını engeller.
Bu düzeltme nedeniyle aydınlatma, eski grafikteki hatalı yönlendirmeyle tamamen
aynı görünmeyebilir; aynı geometrik yüzeyleri köşeli aydınlatır.

## Kurulum

1. Unity projesinde `Assets` altında `Editor` adlı klasör oluştur.
2. Paketteki `Editor/FlatTerrainSetup.cs` dosyasını bu klasöre koy. Bu bir Editor
   aracıdır; Terrain'e Add Component ile eklenmez.
3. Unity'nin derlemesini bekle. Hierarchy'den Terrain nesneni seç.
4. `Tools > Low Poly Terrain > Kurulum` menüsünü aç.
5. Terrain alanının dolu olduğunu kontrol et. `Materyali oluştur ve Terrain'e
   uygula` düğmesine bas.
6. Terrain Inspector'ında `Paint Terrain > Paint Texture` bölümüne dön.
7. Var olan çim, toprak veya kaya katmanını seçerek boya. Katman yoksa `Edit
   Terrain Layers > Create Layer` ile önce çim dokusunu, sonra diğer dokuları
   ekle. İlk katman zeminin taban kaplaması olur; diğer katmanlar üzerine boyanır.
8. Sahneyi kaydet.

Texture'ları eski GroundTex alanına değil, Terrain Layer içindeki Diffuse
alanına ver. Doku tekrar sıklığını her Terrain Layer'ın Tile Size ayarıyla değiştir.

Araç, seçili eski materyalde `_Brightness` bulursa değerini başlangıç için alır.
Dosyandaki varsayılan değer 3; gerçek materyalinde farklı bir değer olabilir.
Zemin fazla açık görünürse kurulum penceresindeki Parlaklık değerini 1 yapıp
`Parlaklığı mevcut materyale uygula` düğmesine bas. Yeniden shader oluşturmana
gerek yok. Bu kontrol yalnızca bu araçla oluşturulmuş materyaller içindir.

## Kontrol et

- İki farklı Terrain Layer arasında bir çizgi boya: renk yalnızca fırçanın
  geçtiği yerde değişmeli ve geçişler fırça kuvvetine uymalı.
- Aynı yerdeki tepede köşeli yüzeylerin hâlâ göründüğünü kontrol et.
- Kamerayı uzaklaştır: basemap geçişinde katmanlar kaybolmamalı.
- Kullandığın Draw Instanced ayarında ve Game görünümünde dene.
- Console'da yeni shader hatası olmamalı. Sahneyi kapatıp açınca materyal ve boya
  korunmalı. SSAO, farklı renderer veya dört katmandan fazla katman kullanıyorsan
  bu durumları da kendi projen üzerinde kontrol et.

Materyal pembeleşirse veya araç kaynak uyuşmazlığı bildirirse Console'daki ilk
tam hata mesajını, Unity sürümünü ve URP sürümünü paylaş. Araç paket yapısı
beklenenle eşleşmezse tahmini bir değişiklik uygulamak yerine durur.

## Ne değişir?

Araç, projende kurulu URP paketinden TerrainLit, Add Pass, Base Pass, Input,
Lighting Passes ve DepthNormals kaynaklarının yerel kopyalarını oluşturur.
Kopyalarda geometrik normal hesabını ve parlaklık çarpanını değiştirir. Uzak
basemap ve ek katman geçişleri de bu kopyalara bağlanır. Yerleşik Terrain
katman okuma, tiling, Terrain instancing, delik, gölge ve boyama altyapısı korunur.
Desteklenen shader modeli, ekran türevleri için en az 3.0 olur.

Dosyalar `Assets/LowPolyTerrain/Generated_...` altında oluşur. Her kurulum ayrı
dosyalar oluşturur; başka Terrain materyallerinin üstüne yazılmaz. Kurulu paket
dosyaları, eski Shader Graph, eski materyal ve TerrainData değiştirilmez.
Terrain'in yalnızca materyal ataması değişir; sahne otomatik kaydedilmez.

Normal map ayrıntıları, gerçek üçgen yüzeylerini belirginleştiren geometrik
normallerle değiştirilir. Doku, metallic ve smoothness gibi katman özellikleri
Terrain tarafından okunmaya devam eder. Mat görünüm için katmanların smoothness
ayarını düşür. Bu araç zemin geometrisini ya da çarpışmasını değiştirmez.

## Geri dönme

Materyal atamasından hemen sonra Ctrl+Z kullanabilir veya Terrain Settings'teki
Material alanına önceki materyalini tekrar verebilirsin. Eski materyal silinmez.
Parlaklık değişikliğini de Undo ile geri alabilirsin.

URP paketini daha sonra yükseltirsen aracı yeni sürüm kaynaklarından yeniden
çalıştır. Eski üretilmiş kopyalar otomatik güncellenmez.

## Doğrulama durumu

Bu ortamda Unity Editor ve C# derleyicisi bulunmuyor. Sahne içi boyama,
shader derlemesi ve build testi yapılmış değildir. Kullanıcının tam Unity/URP
sürümü yalnızca `.shadergraph` dosyasından belirlenemedi.

C# dosyasının sözdizimi ayrıştırıcısı hata vermedi; bu bir derleme testi değildir.
Kaynak değiştirme noktaları Unity Graphics deposunun `2022.3/staging` ve
`6000.0/staging` Terrain kaynaklarında kontrol edildi. Normal yönü matematiği
farklı eğimler ve ters ekran eksenleriyle kontrol edildi. Araç, Unity'ye
aktarıldığında shader import hata mesajlarını okuyarak materyal atamasından
önce kontrol yapar; bu kontrol bütün shader varyantlarının veya hedef
platformların test edildiği anlamına gelmez.

Bu, dosyada görülen eksik Terrain desteğine yönelik hazırlanmış, proje içinde
doğrulanması gereken bir düzeltmedir.

## Kaynaklar

- [Unity Terrain Lit](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shader-terrain-lit.html)
- [Unity Graphics: TerrainLitPasses](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl)
- [Unity Graphics: TerrainLit](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLit.shader)

Paket Unity'nin kaynaklarını dağıtmaz. Araç kendi kurulu Unity/URP kaynaklarından
türev kopyalar oluştururken mevcut lisans ve telif bildirimlerini korur.
