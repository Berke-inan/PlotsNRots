Shader "Plots & Rots/Global Snow Shell"
{
    Properties
    {
        _SnowShellThicknessMultiplier(
            "Thickness Multiplier",
            Range(0,1.5))
            =
            .75

        _SnowShellCoverageMultiplier(
            "Coverage Multiplier",
            Range(0,1.5))
            =
            1
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry+8"
        }


        Pass
        {
            Name "SnowShell"

            Tags
            {
                "LightMode"="UniversalForwardOnly"
            }


            Cull Back
            ZWrite On
            ZTest LEqual


            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Art/Shaders/Weather/GlobalSnow.hlsl"


            UNITY_INSTANCING_BUFFER_START(SnowShellProps)

                UNITY_DEFINE_INSTANCED_PROP(
                    float,
                    _SnowShellThicknessMultiplier)

                UNITY_DEFINE_INSTANCED_PROP(
                    float,
                    _SnowShellCoverageMultiplier)

            UNITY_INSTANCING_BUFFER_END(SnowShellProps)


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

                half fogFactor : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };


            Varyings Vert(
                Attributes input)
            {
                Varyings output =
                    (Varyings)0;


                UNITY_SETUP_INSTANCE_ID(
                    input);

                UNITY_TRANSFER_INSTANCE_ID(
                    input,
                    output);

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(
                    output);


                float3 positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz);


                float3 normalWS =
                    normalize(
                        TransformObjectToWorldNormal(
                            input.normalOS));


                float thicknessMultiplier =
                    UNITY_ACCESS_INSTANCED_PROP(
                        SnowShellProps,
                        _SnowShellThicknessMultiplier);


                float coverageMultiplier =
                    UNITY_ACCESS_INSTANCED_PROP(
                        SnowShellProps,
                        _SnowShellCoverageMultiplier);


                float displacement =
                    GlobalSnowDisplacement(
                        positionWS,
                        normalWS)
                    *
                    max(
                        0,
                        thicknessMultiplier)
                    *
                    max(
                        0,
                        coverageMultiplier);


                positionWS +=
                    float3(
                        0,
                        displacement,
                        0);


                output.positionWS =
                    positionWS;


                output.normalWS =
                    normalWS;


                output.positionHCS =
                    TransformWorldToHClip(
                        positionWS);


                output.fogFactor =
                    ComputeFogFactor(
                        output.positionHCS.z);


                output.shadowCoord =
                    TransformWorldToShadowCoord(
                        positionWS);


                return
                    output;
            }


            half4 Frag(
                Varyings input)
                :
                SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(
                    input);


                float coverageMultiplier =
                    UNITY_ACCESS_INSTANCED_PROP(
                        SnowShellProps,
                        _SnowShellCoverageMultiplier);


                float snowMask =
                    saturate(
                        GlobalSnowMaskAt(
                            input.positionWS,
                            input.normalWS)
                        *
                        coverageMultiplier);


                // Dikey ve aþaðý bakan yüzeyler kaplanmaz.
                clip(
                    snowMask
                    -
                    .025);


                half3 normalWS =
                    GlobalSnowNormalWS(
                        input.positionWS,

                        normalize(
                            input.normalWS),

                        snowMask);


                float detail =
                    SnowDetailNoise(
                        input.positionWS
                        *
                        1.37
                        +
                        3.7);


                half3 albedo =
                    _GlobalSnowColor.rgb
                    *
                    lerp(
                        .91,
                        1.025,
                        detail);


                InputData inputData =
                    (InputData)0;


                inputData.positionWS =
                    input.positionWS;


                inputData.positionCS =
                    input.positionHCS;


                inputData.normalWS =
                    normalWS;


                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS);


                inputData.shadowCoord =
                    input.shadowCoord;


                inputData.fogCoord =
                    input.fogFactor;


                inputData.vertexLighting =
                    VertexLighting(
                        input.positionWS,
                        normalWS);


                inputData.bakedGI =
                    SampleSH(
                        normalWS);


                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionHCS);


                inputData.shadowMask =
                    half4(
                        1,
                        1,
                        1,
                        1);


                half4 color =
                    UniversalFragmentPBR(
                        inputData,

                        albedo,

                        0,

                        half3(
                            0,
                            0,
                            0),

                        (half)_GlobalSnowSmoothness,

                        1,

                        half3(
                            0,
                            0,
                            0),

                        1);


                color.rgb =
                    MixFog(
                        color.rgb,
                        input.fogFactor);


                return
                    color;
            }

            ENDHLSL
        }
    }


    FallBack Off
}