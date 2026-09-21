# Sky / Weather doğrulama kaydı

Tarih: 21 Eylül 2026

| Kontrol | Sonuç |
|---|---|
| Unity runtime/editor C# compile | PASS |
| WeatherValidation.Run | 39/39 PASS |
| SkyWeatherValidation.Run | 38/38 PASS |
| Toplam otomatik kontrol | 77/77 PASS |
| Shader import | PASS; shader error yok |
| Gerçek SampleScene asset açılışı | PASS; tek manager, shared material, 20 slot, legacy component yok |
| PC URP D3D11 render | PASS; 10 × 1280×720 PNG |

Ortam: Unity 6000.3.19f1, URP 17.3.0, Windows D3D11, NVIDIA GTX 1650, `PC_RPAsset`. Ana açık Unity instance'ı değiştirilmedi; çalışma `Temp/SkyCloudValidationProject` içindeki izole kopyada yapıldı.

```text
Weather regression checks passed: 39. Shader import checks do not replace rendered Forward/Deferred and Play Mode tests.
Sky/climate regression checks passed: 38, plus the weather regression suite.
Sky render captures written to C:\Users\PC\Documents\GitHub\PlotsNRots\Temp\SkyCloudValidationProject\SkyCaptures
```

Render seti Sunny noon, PartlyCloudy noon, sunrise, sunset, clear night, PartlyCloudy night, Overcast, Rain, Storm ve Snow'u kapsar. Final dosyalar `Docs/SkyPreview/` altındadır.

- Sunny/Partly ana bulutları gerçek 3D mesh; projected R silhouette yok.
- Farklı mesh/yaw/ölçek ve büyük blue gap mevcut.
- Settled durumda dither shell yok.
- Overcast ailesi kesintisiz ceiling; vertical stretch, camouflage hole, smear ve horizon seam yok.
- Night celestial alanı açık havada görünür; 3D geometry doğal depth occlusion sağlar.

Otomatik koşu particle/audio dinleme, oyuncu/araç ile precipitation follower, gerçek post-process, Mobile Forward cihazı ve profiler ölçümü değildir. Bunlar `Docs/SKY_CLOUD_VISUAL_HANDOFF_TR.md` son bölümündeki Play Mode adımlarıyla manuel doğrulanmalıdır. Terrain/prop/tree snow bu turun kapsamı dışındadır.
