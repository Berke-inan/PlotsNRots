#ifndef PLOTS_ROTS_GLOBAL_SNOW_INCLUDED
#define PLOTS_ROTS_GLOBAL_SNOW_INCLUDED

float _GlobalSnowAmount;
float _GlobalSnowVisualAmount;
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
// SHELTER / ROOF OCCLUSION
// ============================================================
// Runtime SnowShelterVolume components publish up to 32 oriented boxes.
// A position inside one of these boxes is considered protected from
// accumulated snow. Keep the top of the volume below the actual roof so
// the roof itself can still receive snow.
#define GLOBAL_SNOW_MAX_SHELTERS 32

float _GlobalSnowShelterCount;
float4x4 _GlobalSnowShelterWorldToLocal[GLOBAL_SNOW_MAX_SHELTERS];
float4 _GlobalSnowShelterParams[GLOBAL_SNOW_MAX_SHELTERS];


float GlobalSnowShelterMask(
    float3 positionWS)
{
    int count =
        min(
            GLOBAL_SNOW_MAX_SHELTERS,
            max(
                0,
                (int) _GlobalSnowShelterCount));


    float shelter =
        0;


    [loop]
    for (int i = 0;
         i < count;
         i++)
    {
        float3 localPosition =
            mul(
                _GlobalSnowShelterWorldToLocal[i],
                float4(
                    positionWS,
                    1.0)).xyz;


        float yInside =
            step(
                abs(
                    localPosition.y),
                .5);


        float edgeDistance =
            min(
                .5 - abs(
                    localPosition.x),
                .5 - abs(
                    localPosition.z));


        float edgeSoftness =
            max(
                0,
                _GlobalSnowShelterParams[i].x);


        float horizontalInside =
            edgeSoftness <= .0001

                ?
                step(
                    max(
                        abs(
                            localPosition.x),
                        abs(
                            localPosition.z)),
                    .5)

                :
                smoothstep(
                    0,
                    edgeSoftness,
                    edgeDistance);


        shelter =
            max(
                shelter,
                yInside
                *
                horizontalInside);


        if (shelter >= .999)
        {
            break;
        }
    }


    return
        saturate(
            shelter);
}


float GlobalSnowExposureAt(
    float3 positionWS)
{
    return
        1.0
        -
        GlobalSnowShelterMask(
            positionWS);
}


// ============================================================
// COVERAGE
// ============================================================

float GlobalSnowVisualAmount()
{
    return
        saturate(
            _GlobalSnowVisualAmount);
}


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


// Ortak birikim egrisi.
// Amount arttikca beyazlik guclenmek yerine karla kaplanan ALAN genisler.
// Bu fonksiyon terrain, shell ve ileride vegetation tarafinda ayni progression'i kullanir.
float GlobalSnowCoverageFromNoise(
    float noiseValue)
{
    float amount =
        GlobalSnowVisualAmount();


    if (amount <= .0001)
    {
        return
            0;
    }


    float threshold =
        lerp(
            1.18,
            -.18,
            amount);


    return
        smoothstep(
            threshold - .13,
            threshold + .13,
            saturate(
                noiseValue));
}


// Farkli yuzey siniflari ayni accumulation curve'u kullanir,
// sadece world-space patch boyutu degisebilir.
//
// scaleMultiplier:
// Terrain -> ~0.12
// Props / shell -> ~0.55
float GlobalSnowCoverageAt(
    float3 positionWS,
    float3 normalWS,
    float scaleMultiplier)
{
    float amount =
        GlobalSnowVisualAmount();


    if (amount <= .0001)
    {
        return
            0;
    }


    float exposure =
        GlobalSnowExposureAt(
            positionWS);


    if (exposure <= .0001)
    {
        return
            0;
    }


    float upward =
        GlobalSnowUpMask(
            normalWS);


    if (upward <= .0001)
    {
        return
            0;
    }


    float scale =
        max(
            .001,
            _GlobalSnowDetailScale)
        *
        max(
            .001,
            scaleMultiplier);


    float macroNoise =
        SnowValueNoise(
            positionWS.xz
            *
            scale
            +
            float2(
                19.37,
                73.11));


    float coverage =
        GlobalSnowCoverageFromNoise(
            macroNoise);


    return
        saturate(
            coverage
            *
            upward
            *
            exposure);
}


// Eski Shader Graph custom function baglantilari bozulmasin.
// Position bilgisi olmayan eski yolda patch uretemeyiz;
// ancak ayni VISUAL amount ve slope progression'ini kullaniriz.
float GlobalSnowMask(
    float3 normalWS)
{
    return
        GlobalSnowVisualAmount()
        *
        GlobalSnowUpMask(
            normalWS);
}


// Generic props / building shell icin ortak world-space coverage.
float GlobalSnowMaskAt(
    float3 positionWS,
    float3 normalWS)
{
    return
        GlobalSnowCoverageAt(
            positionWS,
            normalWS,
            .55);
}


// ============================================================
// VISUAL THICKNESS
// ============================================================

float GlobalSnowDisplacement(
    float3 positionWS,
    float3 normalWS)
{
    // Geometrik yukseklik coverage patch'inden ayri tutulur.
    // Boylece fragment seviyesinde gorunen patch'ler kaynak mesh ile
    // z-fighting yapmaz; kalinlik visual accumulation ile yumusak artar.
    float amount =
        GlobalSnowVisualAmount();


    float exposure =
        GlobalSnowExposureAt(
            positionWS);


    if (exposure <= .0001)
    {
        return
            0;
    }


    float upward =
        GlobalSnowUpMask(
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
        amount
        *
        upward
        *
        exposure
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