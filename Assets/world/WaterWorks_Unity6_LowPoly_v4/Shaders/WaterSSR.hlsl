#ifndef WATERWORKS_LOW_POLY_SSR_INCLUDED
#define WATERWORKS_LOW_POLY_SSR_INCLUDED

// Unity 6 / URP 17 Shader Graph custom-function compatible SSR helper.
// It intentionally caps the number of steps to keep the low-poly water cheap.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

inline float3 WaterSSR_ReconstructWorld(float2 uv)
{
    #if UNITY_REVERSED_Z
        real depth = SampleSceneDepth(uv);
    #else
        real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(uv));
    #endif

    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

inline float2 WaterSSR_WorldToUV(float3 worldPos)
{
    float4 clip = TransformWorldToHClip(worldPos);
    float2 ndc = clip.xy / max(abs(clip.w), 1e-5);
    float2 uv = ndc * 0.5 + 0.5;

    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1.0 - uv.y;
    #endif

    return uv;
}

void SSR_float(
    float3 viewDir,
    float stepSize,
    float4 screenPos,
    float samples,
    float thickness,
    float smoothness,
    float3 _normal,
    float3 _position,
    bool reconstructDepth,
    out float3 col)
{
    float2 startUV = saturate(screenPos.xy);
    float3 normalWS = normalize(_normal);

    float3 rayStart = reconstructDepth ? _position : WaterSSR_ReconstructWorld(startUV);
    float3 incident = normalize(-viewDir);
    float3 reflectionDir = normalize(reflect(incident, normalWS));

    float safeStep = max(abs(stepSize), 0.05);
    float safeThickness = max(abs(thickness), 0.02);
    int stepCount = clamp((int)round(samples), 2, 12);

    float3 rayPos = rayStart + normalWS * safeThickness;
    float2 hitUV = startUV;
    float hit = 0.0;

    [loop]
    for (int i = 0; i < 12; i++)
    {
        if (i >= stepCount)
            break;

        rayPos += reflectionDir * safeStep;
        float2 uv = WaterSSR_WorldToUV(rayPos);

        if (any(uv <= 0.001) || any(uv >= 0.999))
            break;

        float3 scenePos = WaterSSR_ReconstructWorld(uv);
        float rayDepth = distance(_WorldSpaceCameraPos, rayPos);
        float sceneDepth = distance(_WorldSpaceCameraPos, scenePos);
        float delta = rayDepth - sceneDepth;

        if (delta >= 0.0 && delta <= safeThickness * 3.0)
        {
            hitUV = uv;
            hit = 1.0;
            break;
        }
    }

    // Low-cost fallback gives a coherent reflection-like shift even when SSR misses.
    float2 fallbackUV = saturate(startUV + reflectionDir.xz * 0.025 * saturate(smoothness));
    float2 finalUV = lerp(fallbackUV, hitUV, hit);

    float edge = min(min(finalUV.x, 1.0 - finalUV.x), min(finalUV.y, 1.0 - finalUV.y));
    edge = saturate(edge * 12.0);

    col = SampleSceneColor(finalUV);

    // Mild color quantization keeps reflections from looking more realistic than the art style.
    float levels = lerp(8.0, 24.0, saturate(smoothness));
    col = floor(col * levels + 0.5) / levels;
    col *= edge;
}

#endif
