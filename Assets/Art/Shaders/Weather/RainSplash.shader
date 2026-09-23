Shader "Plots & Rots/Rain Splash"
{
    Properties
    {
        _Softness(
            "Edge Softness",
            Range(.01,.5))
            =
            .16
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }


        Pass
        {
            Name "Rain Splash"

            Tags
            {
                "LightMode"="SRPDefaultUnlit"
            }


            Blend
                SrcAlpha
                OneMinusSrcAlpha

            ZWrite Off

            ZTest LEqual

            Cull Off


            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            CBUFFER_START(UnityPerMaterial)

                float _Softness;

            CBUFFER_END


            struct Attributes
            {
                float4 positionOS
                    :
                    POSITION;

                float4 color
                    :
                    COLOR;

                float2 uv
                    :
                    TEXCOORD0;
            };


            struct Varyings
            {
                float4 positionHCS
                    :
                    SV_POSITION;

                float4 color
                    :
                    COLOR;

                float2 uv
                    :
                    TEXCOORD0;

                float fogFactor
                    :
                    TEXCOORD1;
            };


            Varyings Vert(
                Attributes input)
            {
                Varyings output;


                output.positionHCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz);


                output.color =
                    input.color;


                output.uv =
                    input.uv;


                output.fogFactor =
                    ComputeFogFactor(
                        output.positionHCS.z);


                return output;
            }


            half4 Frag(
                Varyings input)
                :
                SV_Target
            {
                float2 p =
                    input.uv
                    *
                    2.0
                    -
                    1.0;


                float radius =
                    length(p);


                float alpha =
                    1.0
                    -
                    smoothstep(
                        1.0
                            -
                            _Softness,
                        1.0,
                        radius);


                alpha *=
                    input.color.a;


                clip(
                    alpha
                    -
                    .004);


                half3 color =
                    MixFog(
                        input.color.rgb,
                        input.fogFactor);


                return
                    half4(
                        color,
                        alpha);
            }


            ENDHLSL
        }
    }
}