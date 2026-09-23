#ifndef PLOTS_N_ROTS_STYLIZED_SNOW_COMMON_INCLUDED
#define PLOTS_N_ROTS_STYLIZED_SNOW_COMMON_INCLUDED

TEXTURE2D(_GlobalSnowAlbedoTex);
SAMPLER(sampler_GlobalSnowAlbedoTex);
TEXTURE2D(_GlobalSnowNormalTex);
SAMPLER(sampler_GlobalSnowNormalTex);
TEXTURE2D(_GlobalSnowHeightTex);
SAMPLER(sampler_GlobalSnowHeightTex);

float _GlobalSnowAmount;
float _SnowNormalThreshold;
float _SnowEdgeSoftness;
float4 _GlobalSnowColor;
float _GlobalSnowDetailScale;
float _GlobalSnowNormalStrength;
float _GlobalSnowHeightStrength;
float _GlobalSnowSmoothness;
float _GlobalSnowDisplacement;

float SnowCoverageMask(float3 normalWS)
{
    float threshold = saturate(_SnowNormalThreshold);
    float softness = max(.001, _SnowEdgeSoftness);
    float slope = smoothstep(threshold - softness, threshold + softness, saturate(normalWS.y));
    return saturate(_GlobalSnowAmount) * slope;
}

float2 SnowWorldUV(float3 positionWS)
{
    return positionWS.xz / max(.01, _GlobalSnowDetailScale);
}

void SampleStylizedSnow(
    float3 positionWS,
    float3 geometricNormalWS,
    out float mask,
    out float3 albedo,
    out float3 normalWS,
    out float height)
{
    mask = SnowCoverageMask(normalize(geometricNormalWS));
    float2 uv = SnowWorldUV(positionWS);

    float3 albedoTex = SAMPLE_TEXTURE2D(_GlobalSnowAlbedoTex, sampler_GlobalSnowAlbedoTex, uv).rgb;
    float3 packedNormal = SAMPLE_TEXTURE2D(_GlobalSnowNormalTex, sampler_GlobalSnowNormalTex, uv).xyz * 2.0 - 1.0;
    height = SAMPLE_TEXTURE2D(_GlobalSnowHeightTex, sampler_GlobalSnowHeightTex, uv).r;

    // XZ world projection: tangent=X, bitangent=Z, normal=Y.
    normalWS = normalize(float3(packedNormal.x, max(.05, packedNormal.z), packedNormal.y));
    normalWS = normalize(lerp(float3(0, 1, 0), normalWS, saturate(_GlobalSnowNormalStrength)));

    float reliefShade = lerp(.88, 1.08, lerp(.5, height, saturate(_GlobalSnowHeightStrength)));
    albedo = _GlobalSnowColor.rgb * albedoTex * reliefShade;
}

#endif
