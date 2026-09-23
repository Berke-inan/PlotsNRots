#ifndef PLOTS_ROTS_GLOBAL_SNOW_INCLUDED
#define PLOTS_ROTS_GLOBAL_SNOW_INCLUDED

float _GlobalSnowAmount;
float _SnowNormalThreshold;
float _SnowEdgeSoftness;
float4 _GlobalSnowColor;

float _GlobalSnowThickness;
float _GlobalSnowDetailScale;
float _GlobalSnowDetailStrength;
float _GlobalSnowNormalStrength;
float _GlobalSnowSmoothness;


// ============================================================
// WORLD-SPACE PROCEDURAL SNOW DETAIL
// ============================================================

float SnowHash21(float2 p)
{
    p =
        frac(
            p
            *
            float2(
                123.34,
                456.21));

    p +=
        dot(
            p,
            p + 45.32);

    return
        frac(
            p.x
            *
            p.y);
}


float SnowValueNoise(float2 p)
{
    float2 i =
        floor(p);

    float2 f =
        frac(p);

    f =
        f
        *
        f
        *
        (
            3.0
            -
            2.0
            *
            f
        );


    float a =
        SnowHash21(
            i);

    float b =
        SnowHash21(
            i
            +
            float2(
                1,
                0));

    float c =
        SnowHash21(
            i
            +
            float2(
                0,
                1));

    float d =
        SnowHash21(
            i
            +
            float2(
                1,
                1));


    return
        lerp(
            lerp(
                a,
                b,
                f.x),

            lerp(
                c,
                d,
                f.x),

            f.y);
}


float SnowDetailNoise(
    float3 positionWS)
{
    float scale =
        max(
            .001,
            _GlobalSnowDetailScale);


    float2 p =
        positionWS.xz
        *
        scale;


    float n0 =
        SnowValueNoise(
            p);


    float n1 =
        SnowValueNoise(
            p
            *
            2.07
            +
            17.13);


    return
        n0
        *
        .68
        +
        n1
        *
        .32;
}


// ============================================================
// COVERAGE
// ============================================================

float GlobalSnowUpMask(
    float3 normalWS)
{
    float up =
        normalize(
            normalWS).y;


    float threshold =
        max(
            .001,
            _SnowNormalThreshold);


    float softness =
        max(
            0,
            _SnowEdgeSoftness);


    if (softness < .0001)
    {
        return
            step(
                threshold,
                up);
    }


    return
        smoothstep(
            threshold,
            threshold
            +
            softness,
            up);
}


// Eski Shader Graph custom function baðlantýlarý bozulmasýn.
float GlobalSnowMask(
    float3 normalWS)
{
    return
        saturate(
            _GlobalSnowAmount)
        *
        GlobalSnowUpMask(
            normalWS);
}


// Yeni shaderlarda bunu kullanýyoruz.
float GlobalSnowMaskAt(
    float3 positionWS,
    float3 normalWS)
{
    float amount =
        saturate(
            _GlobalSnowAmount);


    float upward =
        GlobalSnowUpMask(
            normalWS);


    float detail =
        SnowDetailNoise(
            positionWS);


    float breakup =
        lerp(
            1.0,

            lerp(
                .84,
                1.08,
                detail),

            saturate(
                _GlobalSnowDetailStrength));


    return
        saturate(
            amount
            *
            upward
            *
            breakup);
}


// ============================================================
// VISUAL THICKNESS
// ============================================================

float GlobalSnowDisplacement(
    float3 positionWS,
    float3 normalWS)
{
    float mask =
        GlobalSnowMaskAt(
            positionWS,
            normalWS);


    float detail =
        SnowDetailNoise(
            positionWS);


    float variation =
        lerp(
            .82,
            1.12,
            detail);


    return
        max(
            0,
            _GlobalSnowThickness)
        *
        mask
        *
        variation;
}


// ============================================================
// SNOW COLOR
// ============================================================

float3 GlobalSnowSurfaceColor(
    float3 baseColor,
    float3 positionWS,
    float3 normalWS)
{
    float mask =
        GlobalSnowMaskAt(
            positionWS,
            normalWS);


    float detail =
        SnowDetailNoise(
            positionWS
            *
            1.37
            +
            3.7);


    float3 snowColor =
        _GlobalSnowColor.rgb
        *
        lerp(
            .91,
            1.025,
            detail);


    return
        lerp(
            baseColor,
            snowColor,
            mask);
}


// ============================================================
// SNOW RELIEF NORMAL
// ============================================================

float3 GlobalSnowNormalWS(
    float3 positionWS,
    float3 baseNormalWS,
    float snowMask)
{
    float strength =
        saturate(
            _GlobalSnowNormalStrength)
        *
        saturate(
            snowMask);


    if (strength <= .0001)
    {
        return
            normalize(
                baseNormalWS);
    }


    const float epsilon =
        .07;


    float hL =
        SnowDetailNoise(
            positionWS
            -
            float3(
                epsilon,
                0,
                0));


    float hR =
        SnowDetailNoise(
            positionWS
            +
            float3(
                epsilon,
                0,
                0));


    float hD =
        SnowDetailNoise(
            positionWS
            -
            float3(
                0,
                0,
                epsilon));


    float hU =
        SnowDetailNoise(
            positionWS
            +
            float3(
                0,
                0,
                epsilon));


    float dx =
        hR - hL;


    float dz =
        hU - hD;


    float3 detailNormal =
        normalize(
            float3(
                -dx * 2.2,
                1,
                -dz * 2.2));


    return
        normalize(
            lerp(
                normalize(
                    baseNormalWS),

                detailNormal,

                strength));
}


// ============================================================
// SHADER GRAPH BACKWARD COMPATIBILITY
// ============================================================

void GlobalSnow_float(
    float3 BaseColor,
    float3 NormalWS,
    out float3 Color)
{
    Color =
        lerp(
            BaseColor,
            _GlobalSnowColor.rgb,
            GlobalSnowMask(
                NormalWS));
}


void GlobalSnow_half(
    half3 BaseColor,
    half3 NormalWS,
    out half3 Color)
{
    Color =
        lerp(
            BaseColor,
            (half3) _GlobalSnowColor.rgb,
            (half) GlobalSnowMask(
                NormalWS));
}

#endif