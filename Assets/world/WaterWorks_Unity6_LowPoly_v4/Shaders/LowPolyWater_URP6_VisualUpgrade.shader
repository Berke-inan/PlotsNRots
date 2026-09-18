Shader "WaterWorks/LowPolyWater_URP6_VisualUpgrade"
{
    Properties
    {
        [Header(Water Colors)]
        [HDR] _ShallowColor("Shallow Water Color", Color) = (0.08, 0.70, 0.82, 0.88)
        [HDR] _DeepColor("Deep Water Color", Color) = (0.015, 0.16, 0.32, 0.96)
        _Opacity("Water Transparency", Range(0,1)) = 0.82
        _DepthDistance("Depth Color Distance", Range(0.1,50)) = 10

        [Header(Surface Pattern)]
        _SurfaceScale("Surface Pattern Scale", Range(0.001,1)) = 0.035
        _SurfaceSpeed("Surface Speed X Z", Vector) = (0.06, 0.025, 0, 0)
        _SurfaceColorStrength("Surface Color Strength", Range(0,1)) = 0.20
        _ColorSteps("Low Poly Color Steps", Range(2,16)) = 6

        [Header(Waves)]
        _WaveScale("Wave Scale", Range(0.01,3)) = 0.14
        _WaveSpeed("Wave Speed", Range(0,3)) = 0.28
        _WaveHeight("Wave Height", Range(0,1)) = 0.11
        _SecondaryWaveStrength("Secondary Wave Strength", Range(0,1)) = 0.55
        _DetailWaveStrength("Detail Wave Strength", Range(0,1)) = 0.28

        [Header(Shore Foam)]
        [HDR] _FoamColor("Foam Color", Color) = (0.92, 0.98, 1.0, 1)
        _FoamDistance("Foam Width", Range(0.01,10)) = 1.8
        _FoamScale("Foam Pattern Scale", Range(0.001,2)) = 0.12
        _FoamSpeed("Foam Speed X Z", Vector) = (0.25, 0.10, 0, 0)
        _FoamCutoff("Foam Cutoff", Range(0,1)) = 0.52
        _FoamSoftness("Foam Softness", Range(0.001,0.3)) = 0.07
        _FoamBaseStrength("Foam Base Strength", Range(0,1)) = 0.35
        _FoamAmount("Foam Amount", Range(0,2)) = 1
        _WakeFoam("Wake Foam", Range(0,1)) = 0.45

        [Header(Lighting)]
        [HDR] _EdgeColor("Fresnel Edge Color", Color) = (0.15, 0.75, 0.90, 1)
        _EdgeStrength("Fresnel Edge Strength", Range(0,2)) = 0.12
        _FresnelPower("Fresnel Power", Range(0.5,10)) = 4
        _LightStrength("Main Light Strength", Range(0,1)) = 0.30
        _FacetStrength("Low Poly Facet Strength", Range(0,1)) = 0.28
        _SpecularStrength("Specular Strength", Range(0,1)) = 0.12

        [Header(Refraction)]
        _Refraction("Refraction", Range(0,0.03)) = 0.002
        [Toggle] _EnableRefraction("Enable Refraction", Float) = 1

        // Eski materyaller bozulmasın diye legacy alanlar tutuldu.
        [HideInInspector] _FoamHardness("Legacy Foam Hardness", Range(1,12)) = 4
        [HideInInspector] _ColorBands("Legacy Color Bands", Range(2,12)) = 5
        [HideInInspector] _SpecularSteps("Legacy Specular Steps", Range(2,8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LowPolyWaterVisualUpgrade"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _EdgeColor;

                float _Opacity;
                float _DepthDistance;

                float _SurfaceScale;
                float4 _SurfaceSpeed;
                float _SurfaceColorStrength;
                float _ColorSteps;

                float _WaveScale;
                float _WaveSpeed;
                float _WaveHeight;
                float _SecondaryWaveStrength;
                float _DetailWaveStrength;

                float _FoamDistance;
                float _FoamScale;
                float4 _FoamSpeed;
                float _FoamCutoff;
                float _FoamSoftness;
                float _FoamBaseStrength;
                float _FoamAmount;
                float _WakeFoam;

                float _EdgeStrength;
                float _FresnelPower;
                float _LightStrength;
                float _FacetStrength;
                float _SpecularStrength;

                float _Refraction;
                float _EnableRefraction;

                float _FoamHardness;
                float _ColorBands;
                float _SpecularSteps;
            CBUFFER_END

            // WaterWorks etkileşim/ripple sistemi aynen korunur.
            #include "WaterInteraction.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            static const float2 kWaveDir1 = float2(0.943858, 0.330350);
            static const float2 kWaveDir2 = float2(-0.481919, 0.876216);
            static const float2 kWaveDir3 = float2(0.821370, -0.570396);

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float EvaluateWave(float2 xz, float timeValue)
            {
                float w2 = saturate(_SecondaryWaveStrength);
                float w3 = saturate(_DetailWaveStrength);
                float weightSum = max(0.001, 1.0 + w2 + w3);

                float p1 = dot(xz, kWaveDir1) * _WaveScale + timeValue;
                float p2 = dot(xz, kWaveDir2) * (_WaveScale * 0.63) - timeValue * 1.27;
                float p3 = dot(xz, kWaveDir3) * (_WaveScale * 1.47) + timeValue * 0.71;

                return (sin(p1) + sin(p2) * w2 + sin(p3) * w3) / weightSum;
            }

            float2 EvaluateWaveGradient(float2 xz, float timeValue)
            {
                float w2 = saturate(_SecondaryWaveStrength);
                float w3 = saturate(_DetailWaveStrength);
                float weightSum = max(0.001, 1.0 + w2 + w3);

                float s1 = _WaveScale;
                float s2 = _WaveScale * 0.63;
                float s3 = _WaveScale * 1.47;

                float p1 = dot(xz, kWaveDir1) * s1 + timeValue;
                float p2 = dot(xz, kWaveDir2) * s2 - timeValue * 1.27;
                float p3 = dot(xz, kWaveDir3) * s3 + timeValue * 0.71;

                float2 gradient =
                    cos(p1) * kWaveDir1 * s1 +
                    cos(p2) * kWaveDir2 * s2 * w2 +
                    cos(p3) * kWaveDir3 * s3 * w3;

                return (gradient / weightSum) * _WaveHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float timeValue = WaterClock() * _WaveSpeed;

                float wave = EvaluateWave(positionWS.xz, timeValue);

                float rippleHeight;
                float2 rippleGradient;
                EvaluateInteraction(positionWS.xz, WaterClock(), rippleHeight, rippleGradient);

                positionWS.y += wave * _WaveHeight + rippleHeight + _WWSurfaceOffset;

                float2 gradient = EvaluateWaveGradient(positionWS.xz, timeValue) + rippleGradient;
                float3 waveNormal = normalize(float3(-gradient.x, 1.0, -gradient.y));

                output.positionWS = positionWS;
                output.normalWS = waveNormal;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = GetNormalizedScreenSpaceUV(input.positionHCS);

                // -------------------------------------------------
                // DEPTH / SHALLOW-DEEP COLOR
                // -------------------------------------------------
                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    float depth = rawDepth;
                    bool sky = rawDepth <= 0.000001;
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                    bool sky = rawDepth >= 0.999999;
                #endif

                float3 sceneWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float thickness = sky ? _DepthDistance * 8.0 : max(0.0, length(sceneWS - input.positionWS));
                float depth01 = saturate(thickness / max(_DepthDistance, 0.001));

                half3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                // -------------------------------------------------
                // STYLIZED LOW-POLY SURFACE COLOR PATTERN
                // -------------------------------------------------
                float t = WaterClock();

                float2 surfaceUV1 = input.positionWS.xz * _SurfaceScale;
                surfaceUV1 += t * _SurfaceSpeed.xy;

                float2 surfaceUV2 = input.positionWS.zx * (_SurfaceScale * 0.73);
                surfaceUV2 -= t * _SurfaceSpeed.yx * 0.65;

                float surfaceNoise1 = valueNoise(surfaceUV1);
                float surfaceNoise2 = valueNoise(surfaceUV2);
                float surfaceNoise = saturate(surfaceNoise1 * 0.60 + surfaceNoise2 * 0.40);

                float steps = max(_ColorSteps, 2.0);
                float quantized = floor(surfaceNoise * steps) / max(steps - 1.0, 1.0);
                quantized = saturate(quantized);

                float surfaceBrightness = lerp(
                    1.0 - _SurfaceColorStrength,
                    1.0 + _SurfaceColorStrength,
                    quantized
                );

                half3 waterColor = baseColor * surfaceBrightness;

                // -------------------------------------------------
                // NORMAL / LOW-POLY FACETS / INTERACTION
                // -------------------------------------------------
                float ripple;
                float2 rippleGradient;
                EvaluateInteraction(input.positionWS.xz, t, ripple, rippleGradient);

                float2 slope = EvaluateWaveGradient(input.positionWS.xz, t * _WaveSpeed) + rippleGradient;
                float3 smoothNormal = normalize(float3(-slope.x, 1.0, -slope.y));

                float3 facetNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                facetNormal *= facetNormal.y < 0.0 ? -1.0 : 1.0;

                float3 n = normalize(lerp(smoothNormal, facetNormal, _FacetStrength));

                // -------------------------------------------------
                // LIGHTING
                // -------------------------------------------------
                Light mainLight = GetMainLight();
                float lightAmount = saturate(dot(n, mainLight.direction));

                half3 lighting =
                    (1.0h - (half)_LightStrength) +
                    mainLight.color * (half)lightAmount * (half)_LightStrength;

                waterColor *= lighting;

                // -------------------------------------------------
                // FRESNEL EDGE
                // -------------------------------------------------
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(
                    1.0 - saturate(dot(n, v)),
                    max(_FresnelPower, 0.001)
                );

                waterColor += _EdgeColor.rgb * fresnel * _EdgeStrength;

                // Hafif, temiz highlight. Ana görünümü bastırmaz.
                float3 h = SafeNormalize(mainLight.direction + v);
                float spec = pow(saturate(dot(n, h)), 48.0) * _SpecularStrength;
                waterColor += mainLight.color * spec;

                // -------------------------------------------------
                // BROKEN SHORE FOAM
                // -------------------------------------------------
                float shoreMask = 1.0 - smoothstep(
                    0.0,
                    max(_FoamDistance, 0.001),
                    thickness
                );

                float2 foamUV1 = input.positionWS.xz * _FoamScale;
                foamUV1 += t * _FoamSpeed.xy;

                float2 foamUV2 = input.positionWS.zx * (_FoamScale * 1.37);
                foamUV2 -= t * _FoamSpeed.yx * 0.73;

                float foamNoise1 = valueNoise(foamUV1);
                float foamNoise2 = valueNoise(foamUV2);
                float foamNoise = saturate(foamNoise1 * 0.62 + foamNoise2 * 0.38);

                float foamPattern = smoothstep(
                    _FoamCutoff - _FoamSoftness,
                    _FoamCutoff + _FoamSoftness,
                    foamNoise
                );

                float foamOuter = smoothstep(0.05, 0.35, shoreMask);
                float foamInner = 1.0 - smoothstep(0.80, 1.0, shoreMask);
                float foamBand = foamOuter * foamInner;

                float contactFoam = smoothstep(0.82, 1.0, shoreMask);

                float brokenFoam = foamBand * lerp(
                    _FoamBaseStrength,
                    1.0,
                    foamPattern
                );

                float wakeFoam = smoothstep(
                    0.05,
                    0.22,
                    length(rippleGradient)
                ) * _WakeFoam;

                float foamAmount = brokenFoam +
                                   contactFoam * _FoamBaseStrength * 0.55 +
                                   wakeFoam;

                foamAmount = saturate(foamAmount * _FoamAmount);

                waterColor = lerp(
                    waterColor,
                    _FoamColor.rgb,
                    foamAmount * _FoamColor.a
                );

                // -------------------------------------------------
                // ALPHA
                // -------------------------------------------------
                half alpha = saturate(
                    lerp(_Opacity * 0.60, _Opacity, depth01) +
                    foamAmount * (1.0 - _Opacity)
                );

                // -------------------------------------------------
                // REFRACTION
                // -------------------------------------------------
                if (_EnableRefraction > 0.5)
                {
                    float2 refractedUV = saturate(
                        uv + n.xz * _Refraction * (1.0 - foamAmount)
                    );

                    float refractedRaw = SampleSceneDepth(refractedUV);

                    #if !UNITY_REVERSED_Z
                        refractedRaw = lerp(UNITY_NEAR_CLIP_VALUE, 1, refractedRaw);
                    #endif

                    float3 refractedWS = ComputeWorldSpacePosition(
                        refractedUV,
                        refractedRaw,
                        UNITY_MATRIX_I_VP
                    );

                    // Öndeki opaque objenin arkasından yanlış örnek alınmasını engeller.
                    if (-TransformWorldToView(refractedWS).z <
                        -TransformWorldToView(input.positionWS).z)
                    {
                        refractedUV = uv;
                    }

                    half3 deltaColor =
                        SampleSceneColor(refractedUV) -
                        SampleSceneColor(uv);

                    waterColor += deltaColor * (1.0h - alpha) / max(alpha, 0.001h);
                }

                waterColor = MixFog(waterColor, input.fogFactor);
                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
