Shader "Custom/PlotNRots_ReferenceSky"
{
    Properties
    {
        [Header(Gorseldeki Temiz Gunduz)]
        _DayTop ("Gunduz Tepe", Color) = (0.0, 0.45, 0.95, 1) 
        _DayBot ("Gunduz Ufuk", Color) = (0.4, 0.8, 1.0, 1)  
        
        [Header(Gorseldeki Temiz Gece)]
        _NightTop ("Gece Tepe", Color) = (0.02, 0.05, 0.1, 1) 
        _NightBot ("Gece Ufuk", Color) = (0.05, 0.1, 0.2, 1)  
        
        [Header(Gorseldeki Keskin Gunes ve Ay)]
        _SunSize ("Gunes Diski Boyutu", Range(0.001, 0.05)) = 0.01 
        _SunColor ("Gunes Rengi", Color) = (1.0, 0.95, 0.7, 1)
        
        _MoonSize ("Ay Diski Boyutu", Range(0.001, 0.05)) = 0.008 
        _MoonColor ("Ay Rengi", Color) = (0.8, 0.9, 1.0, 1)

        [Header(Pikselli Yildizlar)]
        _StarDensity ("Yildiz Yogunlugu", Range(0.001, 0.1)) = 0.02
        _StarBrightness ("Yildiz Parlakligi", Range(0.1, 5.0)) = 1.0

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

            float4 _DayTop, _DayBot, _NightTop, _NightBot, _SunColor, _MoonColor;
            float _SunSize, _MoonSize, _StarDensity, _StarBrightness;
            float4 _SunDir, _MoonDir;

            v0 vert (appdata v) {
                v0 o; o.pos = UnityObjectToClipPos(v.vertex); o.viewDir = v.vertex.xyz; return o;
            }

            float hash(float3 p) {
                p = frac(p * 0.3183099 + .1); p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            fixed4 frag (v0 i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);
                float sunY = _SunDir.y;

                // Zaman ve Gece/Gunduz Gecisi
                float dayBlend = smoothstep(-0.1, 0.2, sunY);
                float nightBlend = 1.0 - dayBlend;

                // 1. GORSELDEKI GIBI PURUZSUZ VE CANLI GOKYUZU
                // Eski koddaki cel-shaded banding ve kirlilik tamamen kaldirildi
                float gradient = smoothstep(-0.2, 0.6, dir.y);
                
                float3 topColor = lerp(_NightTop.rgb, _DayTop.rgb, dayBlend);
                float3 botColor = lerp(_NightBot.rgb, _DayBot.rgb, dayBlend);
                
                float4 skyColor = float4(lerp(botColor, topColor, gradient), 1.0);

                // 2. PIKSELLI YILDIZLAR
                float3 starGrid = floor(dir * 200.0); 
                float starNoise = hash(starGrid); 
                float isStar = step(1.0 - _StarDensity, starNoise);
                skyColor.rgb += isStar * nightBlend * smoothstep(0.1, 0.4, dir.y) * _StarBrightness;

                // 3. GORSELDEKI GIBI KESKIN VE BUYUK GUNES 
                // Etrafindaki tum yumusak parlamalar (korona) iptal edildi, renk soluk sariya cekildi
                float sunDot = dot(dir, _SunDir.xyz);
                float sunDisc = step(1.0 - _SunSize, sunDot); 
                skyColor.rgb = lerp(skyColor.rgb, _SunColor.rgb, sunDisc * smoothstep(-0.1, 0.1, sunY));

                // 4. KESKIN AY
                float moonDot = dot(dir, _MoonDir.xyz);
                float moonDisc = step(1.0 - _MoonSize, moonDot);
                skyColor.rgb = lerp(skyColor.rgb, _MoonColor.rgb, moonDisc * nightBlend);

                return skyColor;
            }
            ENDCG
        }
    }
}