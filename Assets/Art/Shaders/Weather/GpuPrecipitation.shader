Shader "Plots & Rots/GPU Precipitation"
{
    Properties
    {
        _RainColor("Rain Color", Color) = (0.72,0.82,0.92,0.72)
        _SnowColor("Snow Color", Color) = (0.96,0.985,1,0.92)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "GPU Precipitation"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct ParticleData
            {
                float3 position;
                float3 velocity;
                float seed;
                float age;
            };

            StructuredBuffer<ParticleData> _Particles;

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
                float4 _SnowColor;
                float4 _SnowSizeRange;
                float _Mode;
                float _RainWidth;
                float _RainLength;
                float _SoftParticleDistance;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float seed : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            float2 Rotate2D(float2 value, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(c * value.x - s * value.y, s * value.x + c * value.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                ParticleData particle = _Particles[input.instanceID];
                float3 worldPosition;

                if (_Mode < 0.5)
                {
                    float3 fallDirection = normalize(particle.velocity);
                    float3 viewDirection = normalize(_WorldSpaceCameraPos - particle.position);
                    float3 side = cross(viewDirection, fallDirection);
                    if (dot(side, side) < 0.0001)
                        side = UNITY_MATRIX_I_V[0].xyz;
                    side = normalize(side);

                    float lengthVariation = lerp(.72, 1.22, frac(particle.seed * 17.13));
                    float widthVariation = lerp(.82, 1.16, frac(particle.seed * 47.77));
                    worldPosition = particle.position
                        + side * (input.positionOS.x * _RainWidth * widthVariation)
                        + fallDirection * (input.positionOS.y * _RainLength * lengthVariation);
                }
                else
                {
                    float size = lerp(_SnowSizeRange.x, _SnowSizeRange.y, frac(particle.seed * 53.19));
                    float angle = particle.seed * 6.2831853 + _Time.y * lerp(.15, .6, frac(particle.seed * 7.7));
                    float2 local = Rotate2D(input.positionOS.xy, angle) * size;
                    float3 cameraRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                    float3 cameraUp = normalize(UNITY_MATRIX_I_V[1].xyz);
                    worldPosition = particle.position + cameraRight * local.x + cameraUp * local.y;
                }

                output.positionHCS = TransformWorldToHClip(worldPosition);
                output.uv = input.uv;
                output.seed = particle.seed;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.eyeDepth = -TransformWorldToView(worldPosition).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float alpha;
                half3 color;

                if (_Mode < 0.5)
                {
                    float2 p = input.uv * 2.0 - 1.0;
                    float edge = saturate(1.0 - abs(p.x) * 1.5);
                    float taper = smoothstep(0.0, .12, input.uv.y) * smoothstep(0.0, .12, 1.0 - input.uv.y);
                    alpha = edge * taper * _RainColor.a;
                    color = _RainColor.rgb;
                }
                else
                {
                    float2 p = input.uv * 2.0 - 1.0;
                    // Cheap hexagonal flake silhouette; no snowflake texture is required.
                    float hex = max(abs(p.y), abs(p.x) * .8660254 + abs(p.y) * .5);
                    alpha = smoothstep(1.0, .70, hex) * _SnowColor.a;
                    float variation = lerp(.86, 1.08, frac(input.seed * 91.37));
                    color = _SnowColor.rgb * variation;
                }

                // Soft intersection with opaque geometry.
                float2 screenUv = input.positionHCS.xy / _ScaledScreenParams.xy;
                float sceneRawDepth = SampleSceneDepth(screenUv);
                float sceneEye = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float soft = saturate((sceneEye - input.eyeDepth) / max(.01, _SoftParticleDistance));
                alpha *= soft;

                clip(alpha - .003);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
