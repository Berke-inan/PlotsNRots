Shader "Custom/LowPolyWaterSurface"
{
    Properties
    {
        // =====================================================
        // COLORS
        // =====================================================

        [Header(Water Colors)]

        [HDR]
        _ShallowColor(
            "Shallow Water Color",
            Color
        ) = (0.08, 0.70, 0.82, 1)

        [HDR]
        _DeepColor(
            "Deep Water Color",
            Color
        ) = (0.015, 0.16, 0.32, 1)

        _DepthColorDistance(
            "Depth Color Distance",
            Range(0.1, 50)
        ) = 10


        // =====================================================
        // SURFACE PATTERN
        // =====================================================

        [Header(Surface Pattern)]

        _SurfaceScale(
            "Surface Pattern Scale",
            Range(0.001, 1)
        ) = 0.035

        _SurfaceSpeed(
            "Surface Speed X Z",
            Vector
        ) = (0.06, 0.025, 0, 0)

        _SurfaceColorStrength(
            "Surface Color Strength",
            Range(0, 1)
        ) = 0.20

        _ColorSteps(
            "Low Poly Color Steps",
            Range(2, 16)
        ) = 6


        // =====================================================
        // WAVE 1
        // =====================================================

        [Header(Wave 1 - Large)]

        _Wave1Scale(
            "Wave 1 Scale",
            Range(0.001, 1)
        ) = 0.018

        _Wave1Speed(
            "Wave 1 Speed X Z",
            Vector
        ) = (0.35, 0.15, 0, 0)

        _Wave1Height(
            "Wave 1 Height",
            Range(0, 3)
        ) = 0.55


        // =====================================================
        // WAVE 2
        // =====================================================

        [Header(Wave 2 - Medium)]

        _Wave2Scale(
            "Wave 2 Scale",
            Range(0.001, 1)
        ) = 0.045

        _Wave2Speed(
            "Wave 2 Speed X Z",
            Vector
        ) = (-0.20, 0.30, 0, 0)

        _Wave2Height(
            "Wave 2 Height",
            Range(0, 3)
        ) = 0.28


        // =====================================================
        // WAVE 3
        // =====================================================

        [Header(Wave 3 - Detail)]

        _Wave3Scale(
            "Wave 3 Scale",
            Range(0.001, 1)
        ) = 0.09

        _Wave3Speed(
            "Wave 3 Speed X Z",
            Vector
        ) = (0.40, -0.25, 0, 0)

        _Wave3Height(
            "Wave 3 Height",
            Range(0, 3)
        ) = 0.12


        [Header(Global Waves)]

        _WaveStrength(
            "Global Wave Strength",
            Range(0, 3)
        ) = 1


        // =====================================================
        // SHORE FOAM
        // =====================================================

        [Header(Shore Foam)]

        [HDR]
        _FoamColor(
            "Foam Color",
            Color
        ) = (0.92, 0.98, 1.0, 1)

        _FoamWidth(
            "Foam Width",
            Range(0.01, 10)
        ) = 1.8

        _FoamScale(
            "Foam Pattern Scale",
            Range(0.001, 2)
        ) = 0.12

        _FoamSpeed(
            "Foam Speed X Z",
            Vector
        ) = (0.25, 0.10, 0, 0)

        _FoamCutoff(
            "Foam Cutoff",
            Range(0, 1)
        ) = 0.52

        _FoamSoftness(
            "Foam Softness",
            Range(0.001, 0.3)
        ) = 0.07

        _FoamBaseStrength(
            "Foam Base Strength",
            Range(0, 1)
        ) = 0.35

        _FoamAmount(
            "Foam Amount",
            Range(0, 2)
        ) = 1


        // =====================================================
        // LIGHT
        // =====================================================

        [Header(Lighting)]

        [HDR]
        _EdgeColor(
            "Fresnel Edge Color",
            Color
        ) = (0.15, 0.75, 0.90, 1)

        _EdgeStrength(
            "Fresnel Edge Strength",
            Range(0, 2)
        ) = 0.12

        _FresnelPower(
            "Fresnel Power",
            Range(0.5, 10)
        ) = 4

        _LightStrength(
            "Main Light Strength",
            Range(0, 1)
        ) = 0.30


        // =====================================================
        // TRANSPARENCY
        // =====================================================

        [Header(Transparency)]

        _Alpha(
            "Water Transparency",
            Range(0, 1)
        ) = 0.82
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "LowPolyWaterSurface"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off


            HLSLPROGRAM

            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            // =====================================================
            // STRUCTS
            // =====================================================

            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;

                half fogFactor : TEXCOORD1;

                UNITY_VERTEX_OUTPUT_STEREO
            };


            // =====================================================
            // MATERIAL DATA
            // =====================================================

            CBUFFER_START(UnityPerMaterial)

                float4 _ShallowColor;
                float4 _DeepColor;

                float _DepthColorDistance;


                float _SurfaceScale;
                float4 _SurfaceSpeed;
                float _SurfaceColorStrength;
                float _ColorSteps;


                float _Wave1Scale;
                float4 _Wave1Speed;
                float _Wave1Height;

                float _Wave2Scale;
                float4 _Wave2Speed;
                float _Wave2Height;

                float _Wave3Scale;
                float4 _Wave3Speed;
                float _Wave3Height;

                float _WaveStrength;


                float4 _FoamColor;

                float _FoamWidth;
                float _FoamScale;
                float4 _FoamSpeed;

                float _FoamCutoff;
                float _FoamSoftness;

                float _FoamBaseStrength;
                float _FoamAmount;


                float4 _EdgeColor;

                float _EdgeStrength;
                float _FresnelPower;

                float _LightStrength;


                float _Alpha;

            CBUFFER_END


            // =====================================================
            // HASH
            // =====================================================

            float hash21(float2 p)
            {
                p = frac(
                    p *
                    float2(
                        123.34,
                        456.21
                    )
                );

                p += dot(
                    p,
                    p + 45.32
                );

                return frac(
                    p.x * p.y
                );
            }


            // =====================================================
            // VALUE NOISE
            // =====================================================

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f =
                    f *
                    f *
                    (
                        3.0 -
                        2.0 * f
                    );


                float a =
                    hash21(i);

                float b =
                    hash21(
                        i +
                        float2(1, 0)
                    );

                float c =
                    hash21(
                        i +
                        float2(0, 1)
                    );

                float d =
                    hash21(
                        i +
                        float2(1, 1)
                    );


                return lerp(
                    lerp(
                        a,
                        b,
                        f.x
                    ),

                    lerp(
                        c,
                        d,
                        f.x
                    ),

                    f.y
                );
            }


            // =====================================================
            // CENTERED NOISE
            //
            // 0..1 değerini -1..1 yapar.
            // Dalga yüksekliğinde kullanıyoruz.
            // =====================================================

            float centeredNoise(float2 p)
            {
                return
                    valueNoise(p) *
                    2.0 -
                    1.0;
            }


            // =====================================================
            // VERTEX WAVES
            // =====================================================

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);


                float3 positionWS =
                    TransformObjectToWorld(
                        IN.positionOS.xyz
                    );


                float time =
                    _Time.y;


                // -------------------------------------------------
                // WAVE 1
                // Büyük ve yavaş dalga
                // -------------------------------------------------

                float2 wave1UV =
                    positionWS.xz *
                    _Wave1Scale;

                wave1UV +=
                    time *
                    _Wave1Speed.xy;


                float wave1 =
                    centeredNoise(
                        wave1UV
                    );


                // -------------------------------------------------
                // WAVE 2
                // Başka yönde hareket eden orta dalga
                // -------------------------------------------------

                float2 wave2UV =
                    positionWS.zx *
                    _Wave2Scale;

                wave2UV +=
                    time *
                    _Wave2Speed.xy;


                float wave2 =
                    centeredNoise(
                        wave2UV
                    );


                // -------------------------------------------------
                // WAVE 3
                // Küçük yüzey detayları
                // -------------------------------------------------

                float2 wave3UV =
                    positionWS.xz *
                    _Wave3Scale;

                wave3UV.x +=
                    time *
                    _Wave3Speed.x;

                wave3UV.y +=
                    time *
                    _Wave3Speed.y;


                float wave3 =
                    centeredNoise(
                        wave3UV
                    );


                // -------------------------------------------------
                // DALGALARI BİRLEŞTİR
                // -------------------------------------------------

                float totalWave =
                    wave1 *
                    _Wave1Height
                    +
                    wave2 *
                    _Wave2Height
                    +
                    wave3 *
                    _Wave3Height;


                positionWS.y +=
                    totalWave *
                    _WaveStrength;


                OUT.positionWS =
                    positionWS;


                OUT.positionCS =
                    TransformWorldToHClip(
                        positionWS
                    );


                OUT.fogFactor =
                    ComputeFogFactor(
                        OUT.positionCS.z
                    );


                return OUT;
            }


            // =====================================================
            // FRAGMENT
            // =====================================================

            half4 frag(Varyings IN) : SV_Target
            {
                // =================================================
                // SCREEN UV
                // =================================================

                float2 screenUV =
                    IN.positionCS.xy /
                    _ScaledScreenParams.xy;


                // =================================================
                // SCENE DEPTH
                // =================================================

                float rawSceneDepth =
                    SampleSceneDepth(
                        screenUV
                    );


                float sceneEyeDepth =
                    LinearEyeDepth(
                        rawSceneDepth,
                        _ZBufferParams
                    );


                // =================================================
                // WATER DEPTH
                // =================================================

                float waterEyeDepth =
                    -TransformWorldToView(
                        IN.positionWS
                    ).z;


                float depthDifference =
                    max(
                        sceneEyeDepth -
                        waterEyeDepth,
                        0.0
                    );


                // =================================================
                // SHALLOW / DEEP COLOR
                // =================================================

                float depthFactor =
                    saturate(
                        depthDifference /
                        max(
                            _DepthColorDistance,
                            0.001
                        )
                    );


                float3 baseWaterColor =
                    lerp(
                        _ShallowColor.rgb,
                        _DeepColor.rgb,
                        depthFactor
                    );


                // =================================================
                // MOVING SURFACE PATTERN
                // =================================================

                float2 surfaceUV1 =
                    IN.positionWS.xz *
                    _SurfaceScale;

                surfaceUV1 +=
                    _Time.y *
                    _SurfaceSpeed.xy;


                float2 surfaceUV2 =
                    IN.positionWS.zx *
                    (
                        _SurfaceScale *
                        0.73
                    );

                surfaceUV2 -=
                    _Time.y *
                    _SurfaceSpeed.yx *
                    0.65;


                float surfaceNoise1 =
                    valueNoise(
                        surfaceUV1
                    );


                float surfaceNoise2 =
                    valueNoise(
                        surfaceUV2
                    );


                float surfaceNoise =
                    saturate(
                        surfaceNoise1 *
                        0.60
                        +
                        surfaceNoise2 *
                        0.40
                    );


                // =================================================
                // LOW POLY COLOR STEPS
                // =================================================

                float steps =
                    max(
                        _ColorSteps,
                        2.0
                    );


                float quantized =
                    floor(
                        surfaceNoise *
                        steps
                    )
                    /
                    max(
                        steps - 1.0,
                        1.0
                    );


                quantized =
                    saturate(
                        quantized
                    );


                float surfaceBrightness =
                    lerp(
                        1.0 -
                        _SurfaceColorStrength,
                        1.0 +
                        _SurfaceColorStrength,
                        quantized
                    );


                float3 waterColor =
                    baseWaterColor *
                    surfaceBrightness;


                // =================================================
                // LOW-POLY TRIANGLE NORMAL
                // =================================================

                float3 normalWS =
                    normalize(
                        cross(
                            ddy(IN.positionWS),
                            ddx(IN.positionWS)
                        )
                    );


                if (normalWS.y < 0)
                {
                    normalWS =
                        -normalWS;
                }


                // =================================================
                // MAIN LIGHT
                // =================================================

                Light mainLight =
                    GetMainLight();


                float lightAmount =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );


                waterColor *=
                    (
                        1.0 -
                        _LightStrength
                    )
                    +
                    mainLight.color *
                    lightAmount *
                    _LightStrength;


                // =================================================
                // FRESNEL
                // =================================================

                float3 viewDirection =
                    SafeNormalize(
                        _WorldSpaceCameraPos -
                        IN.positionWS
                    );


                float fresnel =
                    pow(
                        1.0 -
                        saturate(
                            dot(
                                normalWS,
                                viewDirection
                            )
                        ),

                        _FresnelPower
                    );


                waterColor +=
                    _EdgeColor.rgb *
                    fresnel *
                    _EdgeStrength;


                // =================================================
                // SHORE MASK
                //
                // Terrain / taş / nesne suya yaklaştıkça 1 olur.
                // =================================================

                float shoreMask =
                    1.0 -
                    smoothstep(
                        0.0,
                        _FoamWidth,
                        depthDifference
                    );


                // =================================================
                // MOVING FOAM PATTERN
                // =================================================

                float2 foamUV1 =
                    IN.positionWS.xz *
                    _FoamScale;

                foamUV1 +=
                    _Time.y *
                    _FoamSpeed.xy;


                float2 foamUV2 =
                    IN.positionWS.zx *
                    (
                        _FoamScale *
                        1.37
                    );

                foamUV2 -=
                    _Time.y *
                    _FoamSpeed.yx *
                    0.73;


                float foamNoise1 =
                    valueNoise(
                        foamUV1
                    );


                float foamNoise2 =
                    valueNoise(
                        foamUV2
                    );


                float foamNoise =
                    saturate(
                        foamNoise1 *
                        0.62
                        +
                        foamNoise2 *
                        0.38
                    );


                // =================================================
                // BROKEN FOAM
                // =================================================

                float foamPattern =
                    smoothstep(
                        _FoamCutoff -
                        _FoamSoftness,

                        _FoamCutoff +
                        _FoamSoftness,

                        foamNoise
                    );


                // =================================================
                // FOAM BAND
                //
                // Bütün sığ alanı beyaza boyamak yerine
                // kıyıda şerit oluşturur.
                // =================================================

                float foamBand =
                    smoothstep(
                        0.08,
                        0.35,
                        shoreMask
                    )
                    *
                    (
                        1.0 -
                        smoothstep(
                            0.82,
                            1.0,
                            shoreMask
                        )
                    );


                // Terrain ile tam temas noktasında
                // ince bir köpük çizgisi.
                float contactFoam =
                    smoothstep(
                        0.80,
                        1.0,
                        shoreMask
                    );


                float brokenFoam =
                    foamBand *
                    lerp(
                        _FoamBaseStrength,
                        1.0,
                        foamPattern
                    );


                float foamAmount =
                    brokenFoam
                    +
                    contactFoam *
                    _FoamBaseStrength *
                    0.55;


                foamAmount =
                    saturate(
                        foamAmount *
                        _FoamAmount
                    );


                // =================================================
                // APPLY FOAM COLOR
                // =================================================

                float3 finalColor =
                    lerp(
                        waterColor,
                        _FoamColor.rgb,
                        foamAmount *
                        _FoamColor.a
                    );


                // =================================================
                // FOG
                // =================================================

                finalColor =
                    MixFog(
                        finalColor,
                        IN.fogFactor
                    );


                // =================================================
                // ALPHA
                // =================================================

                float finalAlpha =
                    saturate(
                        _Alpha
                        +
                        foamAmount *
                        (
                            1.0 -
                            _Alpha
                        )
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