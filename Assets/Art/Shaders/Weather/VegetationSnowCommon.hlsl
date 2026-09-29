#ifndef PLOTS_ROTS_VEGETATION_SNOW_INCLUDED
#define PLOTS_ROTS_VEGETATION_SNOW_INCLUDED

#include "Assets/Art/Shaders/Weather/GlobalSnow.hlsl"

// Shared tree/vegetation coverage progression.
// _SnowAmount is intentionally used as a per-material receiver/intensity
// multiplier. The actual seasonal amount comes from _GlobalSnowVisualAmount.
float PT_GlobalVegetationSnowMask(
    float fieldMask,
    float receiver)
{
    float accumulation =
        GlobalSnowVisualAmount();

    float threshold =
        lerp(
            0.92,
            0.08,
            accumulation);

    float coverage =
        smoothstep(
            threshold - 0.15,
            threshold + 0.15,
            saturate(fieldMask));

    // Prevent a visible pop immediately after the global visual threshold.
    float visible =
        smoothstep(
            0.02,
            0.10,
            accumulation);

    return
        saturate(
            coverage
            *
            visible
            *
            saturate(receiver));
}


// Broadleaf cards need a slower response than pine cards. Their authored UV
// field is dense, so the original area-only progression can make an entire
// shrub appear snowy very early. This variant accepts an explicit progression
// value and also scales snow opacity by that progression.
float PT_GlobalVegetationSnowMaskAtProgress(
    float fieldMask,
    float receiver,
    float progression)
{
    float accumulation =
        saturate(progression);

    if (accumulation <= 0.0001)
    {
        return 0.0;
    }

    float threshold =
        lerp(
            0.92,
            0.08,
            accumulation);

    float coverage =
        smoothstep(
            threshold - 0.15,
            threshold + 0.15,
            saturate(fieldMask));

    float visible =
        smoothstep(
            0.02,
            0.10,
            accumulation);

    return
        saturate(
            coverage
            *
            visible
            *
            saturate(receiver)
            *
            accumulation);
}


// Grass is not painted white by the snow system. Instead, blades are gradually
// buried by the rising snow cover. This is evaluated entirely in the vertex
// stage and is opt-in per material, so tree foliage using the same shader is
// unaffected.
float PT_GrassSnowHeightScale(
    float receiver,
    float minimumHeight)
{
    float strength =
        saturate(receiver);

    if (strength <= 0.0001)
    {
        return 1.0;
    }

    float accumulation =
        GlobalSnowVisualAmount();

    // Keep early snowfall almost unchanged, then bury grass progressively.
    // 0.25 ~= full height, 0.50 ~= 70-75%, 0.75 ~= 30-40%, 1.0 = min height.
    float burial =
        smoothstep(
            0.22,
            1.0,
            accumulation);

    float minHeight =
        clamp(
            minimumHeight,
            0.02,
            1.0);

    float snowScale =
        lerp(
            1.0,
            minHeight,
            burial);

    return
        lerp(
            1.0,
            snowScale,
            strength);
}


float3 PT_ApplyGrassSnowBurial(
    float3 positionOS,
    float receiver,
    float minimumHeight)
{
    float heightScale =
        PT_GrassSnowHeightScale(
            receiver,
            minimumHeight);

    if (heightScale >= 0.9999)
    {
        return positionOS;
    }

    // Grass meshes are authored around a ground-level pivot. Preserve any
    // below-pivot vertices and compress only the blade portion above it.
    float baseY =
        min(
            positionOS.y,
            0.0);

    positionOS.y =
        baseY
        +
        (positionOS.y - baseY)
        *
        heightScale;

    return positionOS;
}


// Keep the global snow tint, with a tiny world-up lighting variation so the
// result is not a perfectly flat white card.
float3 PT_GlobalVegetationSnowColor(
    float3 normalWS)
{
    float up =
        saturate(
            normalize(normalWS).y
            *
            0.5
            +
            0.5);

    return
        _GlobalSnowColor.rgb
        *
        lerp(
            0.92,
            1.02,
            up);
}

#endif
