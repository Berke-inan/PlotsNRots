Shader "GapperGames/Volumetric_Water"
{
    Properties
    {
        [HDR] Albedo("Volume Tint", Color) = (0.72, 0.95, 1.0, 1)
        [HDR] _ShallowColor("Shallow Color", Color) = (0.10, 0.72, 0.72, 1)
        [HDR] _DeepColor("Deep Color", Color) = (0.025, 0.20, 0.30, 1)
        density("Density", Range(0, 1)) = 0.18
        pos("Position", Vector) = (0, -12.5, 0, 0)
        bounds("Bounds", Vector) = (100, 25, 100, 0)
        _BandCount("Legacy Color Bands (unused)", Range(2, 12)) = 5
        [Toggle] _UnderwaterOnly("Only Apply Below Surface", Float) = 1
        _SurfaceFade("Camera Surface Fade", Range(0.01,2)) = 0.25
        _NoiseScale("Wave Color Scale", Range(0.001, 1)) = 0.07
        _NoiseSpeed("Wave Color Speed", Range(0, 2)) = 0.25
        _RefractionStrength("Refraction", Range(0, 0.03)) = 0.003
        _MaxDistance("Depth Color Distance", Range(1, 100)) = 22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "LowPolyWaterVolume"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Water_Volume.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 Albedo;
                half4 _ShallowColor;
                half4 _DeepColor;
                float density;
                float4 pos;
                float4 bounds;
                float _BandCount;
                float _UnderwaterOnly;
                float _SurfaceFade;
                float _NoiseScale;
                float _NoiseSpeed;
                float _RefractionStrength;
                float _MaxDistance;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float cameraDepth = pos.y + abs(bounds.y)*.5 - _WorldSpaceCameraPos.y;
                float cameraFade = _UnderwaterOnly > .5 ? smoothstep(0, max(.01,_SurfaceFade), cameraDepth) : 1;
                float2 uv = input.texcoord.xy;
                half4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                if (cameraFade <= 0) return source;

                // A sky/background pixel has no opaque scene depth. Previously those
                // pixels returned immediately, leaving a clear band on the horizon
                // while the camera was underwater. Reconstruct the camera far plane
                // for those pixels so the ray still travels through the water box.
                real rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth < 0.0001;
                    real depth = isSky ? 0.0 : rawDepth;
                #else
                    bool isSky = rawDepth > 0.9999;
                    real depth = isSky
                        ? 1.0
                        : lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 sceneWorldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 toScene = sceneWorldPos - _WorldSpaceCameraPos;
                float sceneDistance = length(toScene);

                // ComputeWorldSpacePosition at the far plane supplies the view-ray
                // direction for sky pixels. Use the camera far distance as a stable
                // upper limit instead of treating the missing depth as empty water.
                if (isSky)
                    sceneDistance = max(sceneDistance, _ProjectionParams.z);

                if (sceneDistance < 1e-4)
                    return source;

                float3 rayDirection = toScene / sceneDistance;
                float3 halfBounds = max(abs(bounds.xyz) * 0.5, float3(0.001, 0.001, 0.001));
                float3 boxMin = pos.xyz - halfBounds;
                float3 boxMax = pos.xyz + halfBounds;

                float2 boxDistance = WaterRayBoxDistance(boxMin, boxMax, _WorldSpaceCameraPos, rayDirection);
                float distanceToWater = boxDistance.x;
                float distanceInsideWater = boxDistance.y;

                if (distanceInsideWater <= 0.0 || distanceToWater >= sceneDistance)
                    return source;

                float visibleWaterLength = min(distanceInsideWater, sceneDistance - distanceToWater);
                if (visibleWaterLength <= 0.0)
                    return source;

                float depthFactor = 1 - exp(-visibleWaterLength / max(_MaxDistance, .001));

                float3 samplePoint = _WorldSpaceCameraPos +
                    rayDirection * (distanceToWater + visibleWaterLength * 0.5);

                float lowPolyNoise = WaterLowPolyNoise(
                    samplePoint.xz * _NoiseScale,
                    _Time.y * _NoiseSpeed);

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                waterColor *= lerp(0.90h, 1.08h, (half)lowPolyNoise);
                waterColor *= Albedo.rgb;

                // Cheap stylized refraction. No screen-space ray marching.
                float2 refractDirection = float2(
                    sin(samplePoint.x * _NoiseScale + _Time.y * _NoiseSpeed),
                    cos(samplePoint.z * _NoiseScale - _Time.y * _NoiseSpeed));

                float2 refractedUV = saturate(uv + refractDirection * _RefractionStrength * (1.0 - depthFactor * 0.5));
                half3 refractedScene = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    refractedUV,
                    _BlitMipLevel).rgb;

                float absorption = 1.0 - exp(-max(0.0, density) * visibleWaterLength);
                absorption *= cameraFade;

                refractedScene = lerp(source.rgb, refractedScene, cameraFade);
                half3 finalColor = lerp(refractedScene, waterColor, (half)absorption);
                return half4(finalColor, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
