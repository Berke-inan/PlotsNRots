#ifndef PLOTS_ROTS_GLOBAL_SNOW_INCLUDED
#define PLOTS_ROTS_GLOBAL_SNOW_INCLUDED
// Globals intentionally do NOT appear in material Properties or UnityPerMaterial.
float _GlobalSnowAmount;
float _SnowNormalThreshold;
float _SnowEdgeSoftness;
float4 _GlobalSnowColor;
float GlobalSnowMask(float3 normalWS)
{
    float up = normalize(normalWS).y;
    float threshold = max(0.001, _SnowNormalThreshold);
    float soft = max(0, _SnowEdgeSoftness);
    float upward = soft < 0.0001 ? step(threshold, up)
        : smoothstep(threshold, threshold + soft, up);
    return saturate(_GlobalSnowAmount) * upward;
}
void GlobalSnow_float(float3 BaseColor, float3 NormalWS, out float3 Color)
{
    Color = lerp(BaseColor, _GlobalSnowColor.rgb, GlobalSnowMask(NormalWS));
}
void GlobalSnow_half(half3 BaseColor, half3 NormalWS, out half3 Color)
{
    Color = lerp(BaseColor, (half3)_GlobalSnowColor.rgb, (half)GlobalSnowMask(NormalWS));
}
#endif
