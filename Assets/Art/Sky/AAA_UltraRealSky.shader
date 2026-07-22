Shader "Custom/AAA_UltraRealSky"
{
    Properties
    {
        [Header(Ultra Gercekci Gunduz)]
        _DayTop ("Gunduz Tepe", Color) = (0.1, 0.26, 0.54, 1) 
        _DayBot ("Gunduz Ufuk", Color) = (0.48, 0.65, 0.8, 1)  
        
        [Header(Ultra Gercekci Gun Batimi)]
        _SunsetTop ("Gun Batimi Tepe", Color) = (0.13, 0.2, 0.35, 1) 
        _SunsetBot ("Gun Batimi Ufuk", Color) = (1.0, 0.44, 0.16, 1) 
        
        [Header(Ultra Gercekci Gece)]
        _NightTop ("Gece Tepe", Color) = (0.01, 0.015, 0.03, 1) 
        _NightBot ("Gece Ufuk", Color) = (0.04, 0.07, 0.15, 1)  
        
        [Header(Hacimli ve Yasayan Bulutlar)]
        _CloudDensity ("Bulut Yogunlugu", Range(0, 1)) = 0.55
        _CloudSharpness ("Bulut Detay Keskinligi", Range(0.01, 1)) = 0.5
        _CloudShadowColor ("Bulut Golge Rengi (Alt Kisim)", Color) = (0.35, 0.4, 0.5, 1) 
        _CloudSunHighlight ("Gunes Yansimasi (Silver Lining)", Range(0, 5)) = 2.5 
        _CloudScale ("Bulut Buyuklugu", Float) = 2.0
        _CloudSpeed ("Ruzgar Hizi (Kayma)", Float) = 0.008
        _CloudMorphSpeed ("Bulut Evrim Hizi (Sekil Degistirme)", Float) = 0.02 

        [Header(Optik Gunes ve Ay Fizigi)]
        _SunSize ("Gunes Diski Boyutu", Range(0.0001, 0.01)) = 0.0008 
        _SunBlur ("Gunes Parlamasi (Korona)", Range(0.0001, 0.1)) = 0.015 
        _SunGlow ("Gun Batimi Isik Huzmesi", Range(0, 1)) = 0.4 
        
        _MoonSize ("Ay Diski Boyutu", Range(0.0001, 0.01)) = 0.001 
        _MoonBlur ("Ay Kenar Yumusakligi", Range(0.00001, 0.01)) = 0.0002 

        [Header(Sinematik Yildizlar)]
        _StarDensity ("Yildiz Yogunlugu", Range(0.001, 0.1)) = 0.035
        _StarBrightness ("Yildiz Parlakligi", Range(0.1, 5.0)) = 1.8
        _StarTwinkle ("Yildiz Yanip Sonme Hizi", Float) = 2.0 

        [HideInInspector] _SunDir ("Sun Direction", Vector) = (0,0,1,0)
        [HideInInspector] _MoonDir ("Moon Direction", Vector) = (0,0,-1,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 uv : TEXCOORD0; };
            struct v0 { float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; };

            float4 _DayTop, _DayBot, _SunsetTop, _SunsetBot, _NightTop, _NightBot;
            float _CloudDensity, _CloudSharpness, _CloudSpeed, _CloudScale, _CloudMorphSpeed;
            float4 _CloudShadowColor;
            float _CloudSunHighlight;
            float _SunSize, _SunBlur, _SunGlow, _MoonSize, _MoonBlur;
            float _StarDensity, _StarBrightness, _StarTwinkle;
            float4 _SunDir, _MoonDir;

            v0 vert (appdata v) {
                v0 o; o.pos = UnityObjectToClipPos(v.vertex); o.viewDir = v.vertex.xyz; return o;
            }

            float hash(float3 p) {
                p = frac(p * 0.3183099 + .1); p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float noise(float3 x) {
                float3 i = floor(x); float3 f = frac(x); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i+float3(0,0,0)), hash(i+float3(1,0,0)), f.x), lerp(hash(i+float3(0,1,0)), hash(i+float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i+float3(0,0,1)), hash(i+float3(1,0,1)), f.x), lerp(hash(i+float3(0,1,1)), hash(i+float3(1,1,1)), f.x), f.y), f.z);
            }
            
            // 6-Oktavl� Geli�mi� Fraktal G�r�lt� (AllSky kalitesindeki y�rt�k bulut kenarlar� i�in)
            float fbm(float3 p) {
                float f = 0.0; float amp = 0.5;
                for(int i=0; i<6; i++){
                    f += amp * noise(p);
                    // Her ad�mda uzay� d�nd�rerek organik (fayansla�mayan) bir kaos yarat
                    p = p * 2.03;
                    p.xz = float2(p.x * 0.8 - p.z * 0.6, p.x * 0.6 + p.z * 0.8); 
                    amp *= 0.5;
                }
                return f / 0.984375;
            }

            fixed4 frag (v0 i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);
                float sunY = _SunDir.y;

                // 1. ZEM�N RENG� (Rayleigh Da��l�m� Mant���)
                float sunsetBlend = smoothstep(-0.25, 0.1, sunY) * (1.0 - smoothstep(0.0, 0.4, sunY));
                float dayBlend = smoothstep(0.0, 0.3, sunY);
                float nightBlend = 1.0 - smoothstep(-0.2, 0.1, sunY);

                float3 topColor = _DayTop * dayBlend + _SunsetTop * sunsetBlend + _NightTop * nightBlend;
                float3 botColor = _DayBot * dayBlend + _SunsetBot * sunsetBlend + _NightBot * nightBlend;
                
                float gradient = smoothstep(-0.15, 0.6, dir.y);
                float4 skyColor = float4(lerp(botColor, topColor, gradient), 1.0);

                // 2. T�TREYEN VE CANLI YILDIZLAR
                float3 starGrid = floor(dir * 200.0); 
                float starNoise = hash(starGrid); 
                float isStar = step(1.0 - _StarDensity, starNoise);
                float3 starFrac = frac(dir * 200.0);
                float starDist = length(starFrac - 0.5); 
                
                // Y�ld�zlar�n parlamas�na zaman bazl� bir sin�s dalgas� (twinkle) eklendi
                float twinkle = (sin(_Time.y * _StarTwinkle + starNoise * 100.0) * 0.5 + 0.5) * 0.5 + 0.5;
                float starGlow = smoothstep(0.5, 0.1, starDist) * isStar * twinkle;
                
                skyColor.rgb += starGlow * nightBlend * smoothstep(0.0, 0.2, dir.y) * _StarBrightness;

                // 3. AY VE OPT�K G�NE�
                float moonDot = dot(dir, _MoonDir.xyz);
                float moonDisc = smoothstep(1.0 - _MoonSize - _MoonBlur, 1.0 - _MoonSize, moonDot);
                skyColor.rgb += moonDisc * float3(0.8, 0.85, 0.9) * nightBlend;

                float sunDot = dot(dir, _SunDir.xyz);
                // Merkez g�ne� diski
                float sunDisc = smoothstep(1.0 - _SunSize - 0.0001, 1.0 - _SunSize, sunDot);
                // G�ne�in etraf�ndaki yumu�ak korona/parlama
                float sunCorona = smoothstep(1.0 - _SunSize - _SunBlur, 1.0 - _SunSize, sunDot) * 0.5;
                // G�n bat�m�nda t�m ufka yay�lan s�cak h�zme
                float sunGlowEffect = smoothstep(1.0 - _SunGlow, 1.0, sunDot) * sunsetBlend;
                
                skyColor.rgb += (sunDisc + sunCorona) * float3(1.0, 0.9, 0.75) * (1.0 - nightBlend);
                skyColor.rgb += float3(1.0, 0.35, 0.0) * sunGlowEffect;

                // 4. HAC�ML�, EVR�MLE�EN VE I�IK KIRILMALI BULUTLAR
                if (dir.y > 0.0) 
                {
                    float2 cloudUV = dir.xz / (dir.y + 0.12); // Derinlik katar
                    cloudUV *= _CloudScale;
                    cloudUV.x += _Time.y * _CloudSpeed; // R�zgar kaymas�
                    
                    // ��TE S�H�R: Bulutlar�n 3D G�r�lt�s�ne zaman� Y ekseninde ekliyoruz. Bu onlar� yava��a eritip �ekil de�i�tirtir.
                    float timeEvolution = _Time.y * _CloudMorphSpeed;
                    float n = fbm(float3(cloudUV.x, timeEvolution, cloudUV.y));
                    
                    float cloudThickness = smoothstep(1.0 - _CloudDensity, 1.0, n);
                    float cloudAlpha = smoothstep(1.0 - _CloudDensity, (1.0 - _CloudDensity) + (1.0 - _CloudSharpness), n);
                    cloudAlpha *= smoothstep(0.02, 0.25, dir.y); // Ufuk sisi

                    // G�lgelendirme (Merkez a��k, altlar/kenarlar koyu)
                    float3 cloudColorBase = float3(1.0, 1.0, 1.0);
                    float3 shadedCloud = lerp(_CloudShadowColor.rgb, cloudColorBase, cloudThickness);

                    // G�ne�in ����� bulutlar�n kenarlar�ndan k�r�l�p ge�iyor (Subsurface Scattering / Silver Lining)
                    float sunScattering = max(0.0, sunDot);
                    float edgeGlow = pow(sunScattering, 5.0) * _CloudSunHighlight * (1.0 - cloudThickness); 
                    
                    // Saate g�re bulut renk paleti
                    float3 cloudSunsetColor = float3(1.0, 0.35, 0.15); // Alev alev yanan bulutlar
                    float3 cloudNightColor = float3(0.02, 0.03, 0.05); // Gece zifiri bulutlar

                    float3 finalCloudColor = (shadedCloud * dayBlend) + (cloudSunsetColor * sunsetBlend) + (cloudNightColor * nightBlend);
                    
                    // G�ne� parlamas�n� ekle (Sadece g�ne�li saatlerde)
                    finalCloudColor += float3(1.0, 0.8, 0.4) * edgeGlow * (1.0 - nightBlend);

                    // Gece saydaml���
                    cloudAlpha *= lerp(1.0, 0.35, nightBlend);

                    skyColor.rgb = lerp(skyColor.rgb, finalCloudColor, cloudAlpha);
                }

                return skyColor;
            }
            ENDCG
        }
    }
}