#ifndef WATERWORKS_LOW_POLY_VOLUME_INCLUDED
#define WATERWORKS_LOW_POLY_VOLUME_INCLUDED

// Lightweight helpers for the Unity 6 low-poly fullscreen water-volume shader.
// No ray marching is used: the ray/box intersection gives the distance travelled
// through the water volume in one calculation.

inline float WaterSafeReciprocal(float v)
{
    const float epsilon = 1e-5;
    return 1.0 / (abs(v) < epsilon ? (v < 0.0 ? -epsilon : epsilon) : v);
}

inline float3 WaterSafeReciprocal3(float3 v)
{
    return float3(WaterSafeReciprocal(v.x), WaterSafeReciprocal(v.y), WaterSafeReciprocal(v.z));
}

inline float2 WaterRayBoxDistance(float3 boxMin, float3 boxMax, float3 rayOrigin, float3 rayDirection)
{
    float3 invDir = WaterSafeReciprocal3(rayDirection);
    float3 t0 = (boxMin - rayOrigin) * invDir;
    float3 t1 = (boxMax - rayOrigin) * invDir;

    float3 tMin3 = min(t0, t1);
    float3 tMax3 = max(t0, t1);

    float tEnter = max(max(tMin3.x, tMin3.y), tMin3.z);
    float tExit  = min(min(tMax3.x, tMax3.y), tMax3.z);

    float distanceToBox = max(0.0, tEnter);
    float distanceInside = max(0.0, tExit - distanceToBox);
    return float2(distanceToBox, distanceInside);
}

inline float WaterQuantize01(float value, float bands)
{
    value = saturate(value);
    bands = max(1.0, bands);
    return floor(value * bands + 0.5) / bands;
}

inline float WaterLowPolyNoise(float2 p, float timeValue)
{
    // Broad, deterministic waves rather than high-frequency procedural noise.
    float a = sin(p.x * 1.17 + timeValue);
    float b = cos(p.y * 0.93 - timeValue * 0.83);
    float c = sin((p.x + p.y) * 0.51 + timeValue * 0.37);
    float n = (a + b + c) * (1.0 / 6.0) + 0.5;

    // Continuous variation avoids moving color bands.
    return saturate(n);
}

#endif
