Shader "Custom/LowPolySky"
{
    Properties
    {
        _SkyTopColor ("Gokyuzu Tepe Rengi", Color) = (0.2, 0.5, 0.8, 1)
        _SkyBottomColor ("Gokyuzu Ufuk Rengi", Color) = (0.6, 0.8, 0.9, 1)
        _CloudColor ("Bulut Rengi", Color) = (1, 1, 1, 1)
        _CloudCoverage ("Bulut Yogunlugu", Range(0, 1)) = 0.5
        _CloudScale ("Bulut Boyutu", Float) = 3.0
        _CloudOffset ("Runtime cloud displacement", Float) = 0
        _CloudSpeed ("Ruzgar Hizi", Float) = 0.015
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

            float4 _SkyTopColor, _SkyBottomColor, _CloudColor;
            float _CloudCoverage, _CloudScale, _CloudSpeed, _CloudOffset;

            v0 vert (appdata v) {
                v0 o; o.pos = UnityObjectToClipPos(v.vertex); o.viewDir = v.vertex.xyz; return o;
            }

            // Geliştirilmiş, pürüzsüzleştirilmiş Rastgelelik
            float random(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123); }
            float noise(float2 p) {
                float2 i = floor(p); float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(random(i), random(i + float2(1.0, 0.0)), f.x),
                            lerp(random(i + float2(0.0, 1.0)), random(i + float2(1.0, 1.0)), f.x), f.y);
            }

            // 3 Katmanlı FBM Gürültüsü
            float fbm(float2 p) {
                float f = 0.0;
                f += 0.5000 * noise(p); p = p * 2.02;
                f += 0.2500 * noise(p); p = p * 2.03;
                f += 0.1250 * noise(p);
                return f;
            }

            fixed4 frag (v0 i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);
                
                // ZEMİN GÖKYÜZÜ RENGİ
                float skyBlend = smoothstep(-0.1, 0.6, dir.y);
                float3 skyColor = lerp(_SkyBottomColor.rgb, _SkyTopColor.rgb, skyBlend);

                // KUSURSUZ LOW-POLY BULUTLAR
                if (dir.y > -0.05) 
                {
                    // Ufuk uzamasını engellemek için (dir.y + 0.3) ile bükülmeyi azalttık
                    float2 uv = dir.xz / (max(dir.y, 0.001) + 0.3); 
                    uv *= _CloudScale;
                    uv.x += _Time.y * _CloudSpeed + _CloudOffset;

                    float n = fbm(uv);

                    // POSTERİZE SİHRİ: Gürültüyü basamaklandırarak organik değil, köşeli/stilize yapıyoruz
                    n = floor(n * 6.0) / 6.0; 

                    float cloudShape = step(1.0 - _CloudCoverage, n);
                    
                    // Ufukta bulutları yavaşça sil (keskin bir çizgi olmasın)
                    cloudShape *= smoothstep(-0.05, 0.15, dir.y);

                    skyColor = lerp(skyColor, _CloudColor.rgb, cloudShape);
                }

                return fixed4(skyColor, 1.0);
            }
            ENDCG
        }
    }
}