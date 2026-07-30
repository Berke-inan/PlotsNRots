Shader "Custom/LowPolyWaterFoam"
{
    Properties
    {
        [MainTexture]
        _MainTex("Water Texture", 2D) = "white" {}

        [HDR]
        _ShallowColor(
            "Shallow Water Color",
            Color
        ) = (0.05, 0.75, 0.95, 1)

        [HDR]
        _DeepColor(
            "Deep Water Color",
            Color
        ) = (0.01, 0.15, 0.40, 1)

        _TextureTiling(
            "Texture Tiling",
            Range(0.01, 2)
        ) = 0.12

        _TextureSpeed(
            "Texture Speed X Y",
            Vector
        ) = (0.025, 0.012, 0, 0)

        [Header(Waves)]

        _WaveHeight(
            "Wave Height",
            Range(0, 1)
        ) = 0.08

        _WaveScale(
            "Wave Scale",
            Range(0.01, 5)
        ) = 0.55

        _WaveSpeed(
            "Wave Speed",
            Range(0, 5)
        ) = 0.55

        _SecondWaveStrength(
            "Second Wave Strength",
            Range(0, 1)
        ) = 0.45

        _ColorSteps(
            "Low Poly Color Steps",
            Range(2, 16)
        ) = 6

        [Header(Foam)]

        [HDR]
        _FoamColor(
            "Foam Color",
            Color
        ) = (0.85, 0.97, 1.0, 1)

        _FoamWidth(
            "Foam Width",
            Range(0.01, 5)
        ) = 0.7

        _FoamScale(
            "Foam Pattern Scale",
            Range(0.1, 10)
        ) = 2.5

        _FoamSpeed(
            "Foam Speed",
            Range(0, 5)
        ) = 1

        _FoamCutoff(
            "Foam Cutoff",
            Range(0, 1)
        ) = 0.48

        _FoamSoftness(
            "Foam Softness",
            Range(0.001, 0.3)
        ) = 0.07

        [Header(Edges)]

        [HDR]
        _EdgeColor(
            "Edge Color",
            Color
        ) = (0.15, 0.85, 1, 1)

        _EdgeStrength(
            "Edge Strength",
            Range(0, 2)
        ) = 0.25

        [Header(Transparency)]

        _Alpha(
            "Water Transparency",
            Range(0, 1)
        ) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        LOD 200

        Pass
        {
            Name "LowPolyWaterFoam"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)

                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _TextureSpeed;

                float4 _FoamColor;
                float4 _EdgeColor;

                float _TextureTiling;

                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
                float _SecondWaveStrength;

                float _ColorSteps;

                float _FoamWidth;
                float _FoamScale;
                float _FoamSpeed;
                float _FoamCutoff;
                float _FoamSoftness;

                float _EdgeStrength;
                float _Alpha;

            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;

                float3 positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz
                    );

                float waveTime =
                    _Time.y * _WaveSpeed;

                // Birinci dalga yönü
                float waveA = sin(
                    (
                        positionWS.x +
                        positionWS.z * 0.70
                    )
                    * _WaveScale
                    + waveTime
                );

                // Ýkinci dalga yönü
                float waveB = sin(
                    (
                        positionWS.x * 0.60 -
                        positionWS.z
                    )
                    * (_WaveScale * 1.37)
                    - waveTime * 1.23
                );

                positionWS.y +=
                    (
                        waveA +
                        waveB * _SecondWaveStrength
                    )
                    * _WaveHeight;

                output.positionWS = positionWS;

                output.positionHCS =
                    TransformWorldToHClip(
                        positionWS
                    );

                output.fogFactor =
                    ComputeFogFactor(
                        output.positionHCS.z
                    );

                return output;
            }

            half4 Fragment(Varyings input)
                : SV_Target
            {
                /*
                 * HAREKETLÝ SU TEXTURE'I
                 */

                float2 waterUV_A =
                    input.positionWS.xz
                    * _TextureTiling
                    + _Time.y
                    * _TextureSpeed.xy;

                float2 waterUV_B =
                    input.positionWS.zx
                    * (_TextureTiling * 0.77)
                    - _Time.y
                    * _TextureSpeed.yx
                    * 0.65;

                half3 textureA =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        waterUV_A
                    ).rgb;

                half3 textureB =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        waterUV_B
                    ).rgb;

                half3 waterTexture =
                    (textureA + textureB) * 0.5h;

                /*
                 * LOW-POLY RENK BASAMAKLARI
                 */

                half brightness = dot(
                    waterTexture,
                    half3(
                        0.299h,
                        0.587h,
                        0.114h
                    )
                );

                float colorSteps =
                    max(2.0, _ColorSteps);

                half facetedBrightness =
                    floor(
                        saturate(brightness)
                        * colorSteps
                    )
                    / colorSteps;

                half3 waterColor =
                    lerp(
                        _DeepColor.rgb,
                        _ShallowColor.rgb,
                        facetedBrightness
                    );

                /*
                 * ÜÇGEN YÜZEY NORMALÝ
                 */

                float3 normalWS = normalize(
                    cross(
                        ddy(input.positionWS),
                        ddx(input.positionWS)
                    )
                );

                // Normal aþaðý bakýyorsa yukarý çevir.
                if (normalWS.y < 0.0)
                {
                    normalWS = -normalWS;
                }

                /*
                 * ANA IÞIK
                 */

                Light mainLight = GetMainLight();

                half lightAmount =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                half3 finalColor =
                    waterColor
                    * (
                        0.62h +
                        mainLight.color
                        * lightAmount
                        * 0.38h
                    );

                /*
                 * KAMERA AÇISINA GÖRE KENAR PARLAKLIÐI
                 */

                float3 viewDirection =
                    SafeNormalize(
                        _WorldSpaceCameraPos
                        - input.positionWS
                    );

                half fresnel = pow(
                    1.0h -
                    saturate(
                        dot(
                            normalWS,
                            viewDirection
                        )
                    ),
                    4.0h
                );

                finalColor +=
                    _EdgeColor.rgb
                    * fresnel
                    * _EdgeStrength;

                /*
                 * TERRAIN ÝLE TEMAS KÖPÜÐÜ
                 */

                // Fragment aþamasýndaki SV_POSITION,
                // ekrandaki piksel konumudur.
                float2 screenUV =
                    input.positionHCS.xy
                    / _ScaledScreenParams.xy;

                // Terrain ve diðer opak nesnelerin
                // kamera derinliði.
                float rawSceneDepth =
                    SampleSceneDepth(screenUV);

                float sceneEyeDepth =
                    LinearEyeDepth(
                        rawSceneDepth,
                        _ZBufferParams
                    );

                // Su yüzeyinin kamera derinliði.
                float waterEyeDepth =
                    -TransformWorldToView(
                        input.positionWS
                    ).z;

                // Terrain ile su arasýndaki mesafe.
                float depthDifference =
                    max(
                        sceneEyeDepth -
                        waterEyeDepth,
                        0.0
                    );

                // Mesafe küçüldükçe köpük artar.
                half shoreMask =
                    1.0h -
                    smoothstep(
                        0.0h,
                        _FoamWidth,
                        depthDifference
                    );

                /*
                 * HAREKETLÝ KÖPÜK DESENÝ
                 */

                float foamTime =
                    _Time.y * _FoamSpeed;

                float foamWaveA = sin(
                    (
                        input.positionWS.x +
                        input.positionWS.z
                    )
                    * _FoamScale
                    + foamTime
                );

                float foamWaveB = sin(
                    (
                        input.positionWS.x * 0.73 -
                        input.positionWS.z
                    )
                    * (_FoamScale * 1.31)
                    - foamTime * 0.83
                );

                half foamNoise =
                    saturate(
                        (
                            foamWaveA +
                            foamWaveB
                        )
                        * 0.25h
                        + 0.5h
                    );

                half foamPattern =
                    smoothstep(
                        _FoamCutoff -
                        _FoamSoftness,

                        _FoamCutoff +
                        _FoamSoftness,

                        foamNoise +
                        shoreMask * 0.25h
                    );

                half foamAmount =
                    saturate(
                        shoreMask
                        * lerp(
                            0.55h,
                            1.0h,
                            foamPattern
                        )
                    );

                finalColor =
                    lerp(
                        finalColor,
                        _FoamColor.rgb,
                        foamAmount
                        * _FoamColor.a
                    );

                /*
                 * SAHNE SÝSÝ
                 */

                finalColor =
                    MixFog(
                        finalColor,
                        input.fogFactor
                    );

                // Köpük bölgesi daha opak görünür.
                half finalAlpha =
                    saturate(
                        _Alpha
                        + foamAmount
                        * (1.0h - _Alpha)
                    );

                return half4(
                    finalColor,
                    finalAlpha
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}