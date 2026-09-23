Shader "Plots & Rots/Stylized Cloud"
{
    Properties
    {
        _CloudTint("Cloud Tint", Color) = (1,1,1,1)
        _CloudOpacity("Cloud Opacity", Range(0,1)) = 1
        _CloudVariation("Cloud Variation", Range(.5,1.5)) = 1

        // Eski material/property block baðlantýlarý kýrýlmasýn diye duruyor.
        // Artýk görsel bir etkisi YOK.
        _CloudTransmission("Light Transmission (Legacy / Ignored)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }

            Cull Back
            ZWrite On
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _StylizedCloudHighlight;
            float4 _StylizedCloudBase;
            float4 _StylizedCloudUnderside;
            float3 _StylizedCloudSunDirection;

            // Sadece Storm / SnowStorm / Foggy sýrasýnda güçlenecek.
            float _StylizedCloudFogStrength;
            float _StylizedCloudFogNear;
            float _StylizedCloudFogFar;

            UNITY_INSTANCING_BUFFER_START(CloudProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CloudTint)
                UNITY_DEFINE_INSTANCED_PROP(float, _CloudOpacity)
                UNITY_DEFINE_INSTANCED_PROP(float, _CloudVariation)
                UNITY_DEFINE_INSTANCED_PROP(float, _CloudTransmission)
            UNITY_INSTANCING_BUFFER_END(CloudProps)

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
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float opacity =
                    saturate(
                        UNITY_ACCESS_INSTANCED_PROP(
                            CloudProps,
                            _CloudOpacity));

                // DITHER / NOISE / DELÝK YOK.
                // Cloud mesh tamamen solid.
                clip(opacity - 0.02);

                float variation =
                    UNITY_ACCESS_INSTANCED_PROP(
                        CloudProps,
                        _CloudVariation);

                float4 tint =
                    UNITY_ACCESS_INSTANCED_PROP(
                        CloudProps,
                        _CloudTint);

                float3 normalWS =
                    normalize(input.normalWS);

                float3 sunDirection =
                    normalize(_StylizedCloudSunDirection);

                float upward =
                    saturate(normalWS.y * .5 + .5);

                float sunFacing =
                    saturate(
                        dot(normalWS, sunDirection));

                // Basit low-poly 3 ton.
                float baseMask =
                    smoothstep(.16, .68, upward);

                float highlightMask =
                    smoothstep(.34, .88, sunFacing)
                    *
                    smoothstep(.20, .82, upward);

                half3 color =
                    lerp(
                        _StylizedCloudUnderside.rgb,
                        _StylizedCloudBase.rgb,
                        baseMask);

                color =
                    lerp(
                        color,
                        _StylizedCloudHighlight.rgb,
                        highlightMask);

                // Iþýk geçirgenliði / backscatter YOK.
                color *= tint.rgb * variation;

                // Beyaz patlamalarý önle.
                color = saturate(color);

                // ----------------------------------------------------------
                // FIRTINA BULUTU + SÝS ENTEGRASYONU
                // ----------------------------------------------------------

                float cloudDistance =
                    distance(
                        _WorldSpaceCameraPos,
                        input.positionWS);

                float fogNear =
                    max(
                        0.0,
                        _StylizedCloudFogNear);

                float fogFar =
                    max(
                        fogNear + 1.0,
                        _StylizedCloudFogFar);

                float distanceFog =
                    smoothstep(
                        fogNear,
                        fogFar,
                        cloudDistance)
                    *
                    saturate(
                        _StylizedCloudFogStrength);

                // Uzak bulut beyaz parlamaz.
                // Dünyanýn gerçek fog rengine karýþýr.
                color =
                    lerp(
                        color,
                        unity_FogColor.rgb,
                        distanceFog);

                color =
                    MixFog(
                        color,
                        input.fogFactor);

                return half4(color, 1);
            }

            ENDHLSL
        }
    }
}