Shader "WaterWorks/LowPolyWater_URP6"
{
    Properties
    {
        [HDR] _ShallowColor("Shallow Color", Color) = (0.08, 0.70, 0.72, 0.82)
        [HDR] _DeepColor("Deep Color", Color) = (0.02, 0.24, 0.38, 0.92)
        [HDR] _FoamColor("Foam Color", Color) = (1.15, 1.15, 1.05, 1)
        _Opacity("Opacity", Range(0,1)) = 0.82
        _DepthDistance("Depth Blend Distance", Range(0.1,30)) = 8
        _FoamDistance("Shore Foam Width", Range(0.01,5)) = 1.1
        _FoamHardness("Foam Hardness", Range(1,12)) = 4

        [Header(Waves)]
        _WaveScale("Wave Scale", Range(0.01,3)) = 0.14
        _WaveSpeed("Wave Speed", Range(0,3)) = 0.28
        _WaveHeight("Wave Height", Range(0,1)) = 0.11
        _SecondaryWaveStrength("Secondary Wave Strength", Range(0,1)) = 0.55
        _DetailWaveStrength("Detail Wave Strength", Range(0,1)) = 0.28

        [Header(Stylization)]
        _ColorBands("Legacy Color Bands (unused)", Range(2,12)) = 5
        _FacetStrength("Low Poly Facet Strength", Range(0,1)) = 0.12
        _WakeFoam("Wake Foam", Range(0,1)) = 0.35
        _SpecularSteps("Legacy Specular Steps (unused)", Range(2,8)) = 3
        _SpecularStrength("Specular Strength", Range(0,1)) = 0.22
        _Refraction("Refraction", Range(0,0.03)) = 0.002
        [Toggle] _EnableRefraction("Enable Refraction", Float) = 1
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
            Name "LowPolyWater"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _Opacity;
                float _DepthDistance;
                float _FoamDistance;
                float _FoamHardness;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveHeight;
                float _SecondaryWaveStrength;
                float _DetailWaveStrength;
                float _ColorBands;
                float _FacetStrength;
                float _WakeFoam;
                float _SpecularSteps;
                float _SpecularStrength;
                float _Refraction;
                float _EnableRefraction;
            CBUFFER_END

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
                float rippleHeight; float2 rippleGradient;
                EvaluateInteraction(positionWS.xz, WaterClock(), rippleHeight, rippleGradient);
                positionWS.y += wave * _WaveHeight + rippleHeight + _WWSurfaceOffset;

                float2 gradient = EvaluateWaveGradient(positionWS.xz, timeValue);
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
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float depth = rawDepth;
                    bool sky = rawDepth <= .000001;
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                    bool sky = rawDepth >= .999999;
                #endif
                float3 sceneWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                // Reconstructed world distance also supports orthographic cameras.
                float thickness = sky ? _DepthDistance * 8 : max(0, length(sceneWS - input.positionWS));
                float depth01 = 1 - exp(-thickness / max(_DepthDistance, .001));
                half3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                float t = WaterClock();
                float wave = EvaluateWave(input.positionWS.xz, t * _WaveSpeed);
                baseColor *= lerp(.98h, 1.02h, (half)(wave*.5+.5));
                float ripple; float2 rippleGradient;
                EvaluateInteraction(input.positionWS.xz, t, ripple, rippleGradient);
                float2 slope = EvaluateWaveGradient(input.positionWS.xz, t*_WaveSpeed) + rippleGradient;
                float3 n = normalize(float3(-slope.x, 1, -slope.y));
                float3 facet = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                facet *= facet.y < 0 ? -1 : 1;
                n = normalize(lerp(n, facet, _FacetStrength));
                float foam = pow(1-saturate(thickness/max(_FoamDistance,.001)), _FoamHardness);
                foam = smoothstep(.08, .85, foam);
                float wakeFoam = smoothstep(.05,.22,length(rippleGradient)) * _WakeFoam;
                foam = saturate(foam + wakeFoam);
                Light mainLight = GetMainLight();
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 h = SafeNormalize(mainLight.direction + v);
                float spec = pow(saturate(dot(n,h)), 48) * _SpecularStrength;
                float fresnel = pow(1-saturate(dot(n,v)), 5);
                half3 waterColor = baseColor * (.65h + .35h * mainLight.color * saturate(dot(n,mainLight.direction)));
                waterColor += mainLight.color * spec + _ShallowColor.rgb * fresnel * .12;
                waterColor = lerp(waterColor, _FoamColor.rgb, foam);
                half alpha = saturate(lerp(_Opacity*.55, _Opacity, depth01) + foam*.2);
                waterColor = MixFog(waterColor, input.fogFactor);
                if (_EnableRefraction > .5)
                {
                    float2 refractedUV = saturate(uv + n.xz * _Refraction * (1-foam));
                    float refractedRaw = SampleSceneDepth(refractedUV);
                    #if !UNITY_REVERSED_Z
                        refractedRaw = lerp(UNITY_NEAR_CLIP_VALUE, 1, refractedRaw);
                    #endif
                    float3 refractedWS = ComputeWorldSpacePosition(refractedUV, refractedRaw, UNITY_MATRIX_I_VP);
                    // Reject distortion sampling an opaque foreground object.
                    if (-TransformWorldToView(refractedWS).z < -TransformWorldToView(input.positionWS).z)
                        refractedUV = uv;
                    // Hardware alpha blend composites once. Add only the refracted-minus-original correction.
                    half3 deltaColor = SampleSceneColor(refractedUV) - SampleSceneColor(uv);
                    waterColor += deltaColor * (1-alpha) / max(alpha,.001h);
                }
                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
