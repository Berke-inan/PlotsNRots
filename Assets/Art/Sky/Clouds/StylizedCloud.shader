Shader "Plots & Rots/Stylized Cloud"
{
    Properties
    {
        [HideInInspector] _CloudOpacity ("Opacity", Range(0,1)) = 1
        [HideInInspector] _CloudVariation ("Variation", Range(.75,1.2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        ZWrite On
        Blend Off

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _StylizedCloudHighlight;
            float4 _StylizedCloudBase;
            float4 _StylizedCloudUnderside;
            float4 _StylizedCloudSunDirection;

            UNITY_INSTANCING_BUFFER_START(CloudInstances)
                UNITY_DEFINE_INSTANCED_PROP(float, _CloudOpacity)
                UNITY_DEFINE_INSTANCED_PROP(float, _CloudVariation)
            UNITY_INSTANCING_BUFFER_END(CloudInstances)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Dither4x4(float2 pixel)
            {
                int x = (int)pixel.x & 3;
                int y = (int)pixel.y & 3;
                const float thresholds[16] = {
                    1,9,3,11, 13,5,15,7, 4,12,2,10, 16,8,14,6
                };
                return (thresholds[y*4+x]-.5)/16;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float opacity = UNITY_ACCESS_INSTANCED_PROP(CloudInstances,_CloudOpacity);
                clip(opacity-Dither4x4(input.positionCS.xy));

                float3 normal = normalize(input.normalWS);
                float sunFacing = saturate(dot(normal,normalize(_StylizedCloudSunDirection.xyz))*.5+.5);
                float top = smoothstep(-.05,.78,normal.y);
                float bottom = 1-smoothstep(-.52,.18,normal.y);
                float lighting = saturate(.18 + top*.56 + sunFacing*.26);
                lighting = round(lighting*2)/2;
                float3 color = lerp(_StylizedCloudUnderside.rgb,_StylizedCloudBase.rgb,smoothstep(0,.55,lighting));
                color = lerp(color,_StylizedCloudHighlight.rgb,smoothstep(.52,1,lighting));
                color = lerp(color,_StylizedCloudUnderside.rgb,bottom*.62);
                color *= UNITY_ACCESS_INSTANCED_PROP(CloudInstances,_CloudVariation);
                color = MixFog(color,input.fogFactor);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
