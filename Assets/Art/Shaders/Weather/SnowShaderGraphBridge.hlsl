#ifndef PLOTS_N_ROTS_SNOW_SHADERGRAPH_BRIDGE_INCLUDED
#define PLOTS_N_ROTS_SNOW_SHADERGRAPH_BRIDGE_INCLUDED

#include "StylizedSnowCommon.hlsl"

// Shader Graph Custom Function entry point.
// Inputs: PositionWS / NormalWS / BaseColor.
// Blend Color and NormalWS with your graph's existing outputs using SnowMask.
void StylizedSnow_float(
    float3 PositionWS,
    float3 NormalWS,
    float3 BaseColor,
    out float3 Color,
    out float3 SnowNormalWS,
    out float SnowSmoothness,
    out float SnowMask,
    out float HeightOffset)
{
    float height;
    float3 snowColor;
    SampleStylizedSnow(PositionWS, NormalWS, SnowMask, snowColor, SnowNormalWS, height);
    Color = lerp(BaseColor, snowColor, SnowMask);
    SnowSmoothness = _GlobalSnowSmoothness;
    HeightOffset = (height - .5) * 2.0 * _GlobalSnowDisplacement * SnowMask;
}

#endif
