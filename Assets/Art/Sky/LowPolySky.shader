Shader "Custom/LowPolySky"
{
    Properties
    {
        _SkyTopColor ("Zenith", Color) = (.11,.34,.62,1)
        _SkyMidColor ("Middle sky", Color) = (.32,.59,.8,1)
        _SkyBottomColor ("Horizon", Color) = (.72,.83,.88,1)
        [NoScaleOffset] _CloudMaskTex ("Packed cloud mask", 2D) = "black" {}
        _CloudHighlightColor ("Cloud highlight", Color) = (.98,.97,.93,1)
        _CloudBaseColor ("Cloud base", Color) = (.72,.78,.82,1)
        _CloudUndersideColor ("Cloud underside", Color) = (.38,.48,.62,1)
        _CloudCoverage ("Coverage", Range(0,1)) = .18
        _CloudOvercast ("Stratus blend", Range(0,1)) = 0
        _CirrusAmount ("Cirrus", Range(0,1)) = .3
        _CloudThickness ("Thickness", Range(0,1)) = .25
        _CloudScale ("Macro scale", Float) = .95
        _CloudLowerScale ("Lower layer scale", Float) = 1.72
        _CloudSoftness ("Coverage softness", Range(.01,.25)) = .075
        _CloudBreakup ("Edge breakup", Range(0,1)) = .16
        _CloudToneSteps ("Lighting bands", Range(2,8)) = 4
        _CloudHighlightStrength ("Highlight strength", Range(0,2)) = .9
        _CloudHorizonFade ("Horizon fade", Range(.04,.4)) = .18
        _CloudMotion ("Displacement XY / layer speeds ZW", Vector) = (0,0,.55,1.25)
        _SunDir ("Sun direction", Vector) = (0,1,0,0)
        _MoonDir ("Moon direction", Vector) = (0,-1,0,0)
        _SunColor ("Sun tint", Color) = (1,.95,.84,1)
        _MoonColor ("Moon tint", Color) = (.57,.72,1,1)
        _SunAngularRadius ("Sun radius degrees", Range(.1,3)) = .9
        _MoonAngularRadius ("Moon radius degrees", Range(.1,3)) = 1.1
        _SunHalo ("Sun halo", Range(0,1)) = .18
        _MoonHalo ("Moon halo", Range(0,1)) = .06
        _Daylight ("Daylight", Range(0,1)) = 1
        _Twilight ("Twilight", Range(0,1)) = 0
        _StarVisibility ("Night stars", Range(0,1)) = 0
        _StarDensity ("Star density", Range(0,1)) = .018
        _StarBrightness ("Star brightness", Range(0,3)) = .9
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_CloudMaskTex);
            SAMPLER(sampler_CloudMaskTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _SkyTopColor, _SkyMidColor, _SkyBottomColor;
            float4 _CloudHighlightColor, _CloudBaseColor, _CloudUndersideColor;
            float4 _SunDir, _MoonDir, _SunColor, _MoonColor, _CloudMotion;
            float _CloudCoverage, _CloudOvercast, _CirrusAmount, _CloudThickness;
            float _CloudScale, _CloudLowerScale, _CloudSoftness, _CloudBreakup;
            float _CloudToneSteps, _CloudHighlightStrength, _CloudHorizonFade;
            float _SunAngularRadius, _MoonAngularRadius, _SunHalo, _MoonHalo;
            float _Daylight, _Twilight, _StarVisibility, _StarDensity, _StarBrightness;
            CBUFFER_END
            float4x4 _StarRotation;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            // One inexpensive value-noise evaluation only breaks up authored edges.
            // It never defines the cloud silhouette.
            float EdgeNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            float2 Rotate(float2 p, float2 cs)
            {
                return float2(p.x * cs.x - p.y * cs.y, p.x * cs.y + p.y * cs.x);
            }
            float2 DomeUV(float3 dir, float2 cs, float scale, float2 offset)
            {
                // Upper-hemisphere octahedral coordinates remain finite at zenith and horizon.
                // R/cumulus is deliberately not sampled by the sky shader anymore.
                float denominator = max(.001,abs(dir.x)+abs(dir.y)+abs(dir.z));
                float2 oct = dir.xz/denominator;
                return Rotate(oct,cs)*scale+offset;
            }
            float StarField(float3 dir)
            {
                float3 rotated = mul((float3x3)_StarRotation, dir);
                float3 p = rotated / max(.001, abs(rotated.x) + abs(rotated.y) + abs(rotated.z));
                float2 oct = p.xz;
                if (p.y < 0) oct = (1 - abs(oct.yx)) * float2(oct.x >= 0 ? 1 : -1, oct.y >= 0 ? 1 : -1);
                float2 uv = oct * 150;
                float2 cell = floor(uv);
                float seed = Hash(cell + 41.7);
                float2 center = .25 + .5 * float2(Hash(cell + 8.1), Hash(cell + 73.4));
                float2 starOct = (cell + center) / 150;
                float3 star = float3(starOct.x, 1 - abs(starOct.x) - abs(starOct.y), starOct.y);
                if (star.y < 0) star.xz = (1 - abs(star.zx)) * float2(star.x >= 0 ? 1 : -1, star.z >= 0 ? 1 : -1);
                float distanceToStar = length(cross(normalize(star), rotated));
                float radius = lerp(.00035,.00105,Hash(cell + 101.3));
                float aa = max(.0001, length(fwidth(rotated)) * .4);
                return (1 - smoothstep(max(0,radius-aa), radius + aa, distanceToStar))
                    * step(1 - _StarDensity, seed) * lerp(.25,.8,Hash(cell + 17.2));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 dir = normalize(input.direction);
                float height = saturate(dir.y);
                float3 color = lerp(_SkyBottomColor.rgb, _SkyMidColor.rgb, smoothstep(0,.32,height));
                color = lerp(color, _SkyTopColor.rgb, smoothstep(.2,1,height));
                float horizon = smoothstep(-.025,.025,dir.y);
                float3 sunDirection = normalize(_SunDir.xyz), moonDirection = normalize(_MoonDir.xyz);
                float sunDot = dot(dir, sunDirection), moonDot = dot(dir, moonDirection);
                float sunRadius = _SunAngularRadius * PI / 180, moonRadius = _MoonAngularRadius * PI / 180;
                float sunAA = max(.000015, fwidth(sunDot)), moonAA = max(.000015, fwidth(moonDot));
                float sunDisc = smoothstep(cos(sunRadius)-sunAA, cos(sunRadius)+sunAA, sunDot);
                float moonDisc = smoothstep(cos(moonRadius)-moonAA, cos(moonRadius)+moonAA, moonDot);
                color += _SunColor.rgb * pow(saturate(sunDot),180) * _SunHalo * horizon;
                color += _SunColor.rgb * pow(saturate(sunDot),6) * _Twilight * pow(1-height,3) * .16;
                color = lerp(color,_SunColor.rgb*1.25,sunDisc*horizon);
                color += _MoonColor.rgb * pow(saturate(moonDot),220) * _MoonHalo * (1-_Daylight) * horizon;
                float moonSurface = .86 + .14 * floor(EdgeNoise(dir.xz*95 + dir.y*37)*3)/2;
                color = lerp(color,_MoonColor.rgb*moonSurface,moonDisc*horizon*(1-_Daylight));
                color += StarField(dir) * _StarVisibility * _StarBrightness * horizon * smoothstep(.02,.2,height) * (1-moonDisc);

                if (dir.y > .006)
                {
                    float2 highUV = DomeUV(dir,float2(.9781,.2079),1.15,float2(.5,.5)) + _CloudMotion.xy*_CloudMotion.z;
                    float2 lowUV = DomeUV(dir,float2(.9135,-.4067),1.82,float2(.17,.63)) + _CloudMotion.xy*_CloudMotion.w;
                    float4 highMask = SAMPLE_TEXTURE2D(_CloudMaskTex,sampler_CloudMaskTex,highUV);
                    float4 lowMask = SAMPLE_TEXTURE2D(_CloudMaskTex,sampler_CloudMaskTex,lowUV);

                    // G/A only modulate tone in a continuous high-altitude ceiling. They do not
                    // cut silhouettes, avoiding projection-stretched holes and camouflage shapes.
                    float ceilingActivation = smoothstep(.28,.76,_CloudOvercast);
                    float stratus = max(highMask.g,lowMask.g*.92);
                    float broadVariation = lerp(highMask.a,lowMask.a,.42);
                    float ceilingDetail = (stratus-.5)*.012 + (broadVariation-.5)*.016;
                    float cloudMask = ceilingActivation * smoothstep(.006,.075,height)
                        * saturate(.96 + ceilingDetail);

                    float lighting = saturate(.42 + sunDirection.y*.08 + height*.035 - _CloudThickness*.12);
                    float3 cloudShade = lerp(_CloudUndersideColor.rgb,_CloudBaseColor.rgb,lighting);
                    cloudShade = lerp(cloudShade,_CloudUndersideColor.rgb,_CloudThickness*(1-lighting)*.32);
                    cloudShade *= 1 + ceilingDetail;
                    float haze = 1-smoothstep(.04,.32,height);
                    cloudShade = lerp(cloudShade,_SkyBottomColor.rgb,haze*.58);
                    float cirrus = smoothstep(.32,.66,highMask.b)*_CirrusAmount*.08
                        * smoothstep(.01,_CloudHorizonFade*.8,height)*(1-ceilingActivation*.92);
                    float3 cirrusColor = lerp(_CloudBaseColor.rgb,_CloudHighlightColor.rgb,.72);
                    cirrusColor = lerp(cirrusColor,_SkyBottomColor.rgb,haze*.7);

                    // Shared final coverage occludes sun, moon and stars consistently.
                    color = lerp(color,cirrusColor,cirrus);
                    color = lerp(color,cloudShade,cloudMask);
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
