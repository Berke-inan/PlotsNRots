#ifndef PLOTS_ROTS_TERRAIN_SNOW_INTERACTION_INCLUDED
#define PLOTS_ROTS_TERRAIN_SNOW_INTERACTION_INCLUDED

// Terrain-only snow interaction mask. Keeping this separate from GlobalSnow.hlsl
// prevents footprints from affecting props, buildings or vegetation.
TEXTURE2D(_GlobalSnowInteractionTex);
SAMPLER(sampler_GlobalSnowInteractionTex);

// x = world origin X (minimum corner)
// y = world origin Z (minimum corner)
// z = inverse world size
// w = world size
float4 _GlobalSnowInteractionWorldRect;

// x = enabled (0/1)
// y = texel size in world metres
// z = snow coverage cut strength
// w = visual depression depth used for the normal perturbation
float4 _GlobalSnowInteractionParams;


float2 TerrainSnowInteractionUV(float3 positionWS)
{
    return
        (positionWS.xz - _GlobalSnowInteractionWorldRect.xy)
        * _GlobalSnowInteractionWorldRect.z;
}


float TerrainSnowInteractionMask(float3 positionWS)
{
    if (_GlobalSnowInteractionParams.x < .5
        || _GlobalSnowInteractionWorldRect.z <= 0.0)
    {
        return 0.0;
    }

    float2 uv =
        TerrainSnowInteractionUV(positionWS);

    // Do not use wrapped sampling outside the local interaction clipmap.
    if (uv.x <= 0.0
        || uv.y <= 0.0
        || uv.x >= 1.0
        || uv.y >= 1.0)
    {
        return 0.0;
    }

    return
        saturate(
            SAMPLE_TEXTURE2D_LOD(
                _GlobalSnowInteractionTex,
                sampler_GlobalSnowInteractionTex,
                uv,
                0).r);
}


float TerrainSnowApplyInteractionCoverage(
    float baseSnowMask,
    float interactionMask)
{
    float cut =
        saturate(
            _GlobalSnowInteractionParams.z);

    return
        saturate(
            baseSnowMask
            *
            (1.0 - interactionMask * cut));
}


half3 ApplyTerrainSnowInteractionNormal(
    float3 positionWS,
    half3 baseNormalWS,
    float interactionMask,
    float baseSnowMask)
{
    if (_GlobalSnowInteractionParams.x < .5
        || interactionMask <= .002
        || baseSnowMask <= .002)
    {
        return normalize(baseNormalWS);
    }

    float texelWorld =
        max(
            .005,
            _GlobalSnowInteractionParams.y);

    float depth =
        max(
            0.0,
            _GlobalSnowInteractionParams.w);

    if (depth <= .0001)
    {
        return normalize(baseNormalWS);
    }

    float hL =
        -TerrainSnowInteractionMask(
            positionWS - float3(texelWorld, 0, 0))
        * depth;

    float hR =
        -TerrainSnowInteractionMask(
            positionWS + float3(texelWorld, 0, 0))
        * depth;

    float hD =
        -TerrainSnowInteractionMask(
            positionWS - float3(0, 0, texelWorld))
        * depth;

    float hU =
        -TerrainSnowInteractionMask(
            positionWS + float3(0, 0, texelWorld))
        * depth;

    float dx =
        (hR - hL)
        /
        (2.0 * texelWorld);

    float dz =
        (hU - hD)
        /
        (2.0 * texelWorld);

    float3 perturbed =
        normalize(
            (float3) baseNormalWS
            +
            float3(
                -dx,
                0.0,
                -dz));

    float blend =
        saturate(
            interactionMask
            *
            baseSnowMask
            *
            1.35);

    return
        (half3) normalize(
            lerp(
                (float3) normalize(baseNormalWS),
                perturbed,
                blend));
}

#endif
