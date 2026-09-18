#ifndef WATERWORKS_INTERACTION_INCLUDED
#define WATERWORKS_INTERACTION_INCLUDED
// Per-renderer data supplied by WaterSurface; never global shader state.
float _WWActive, _WWTime, _WWSurfaceOffset, _WWRippleCount;
float4 _WWRippleSettings; // speed, decay, lifetime, max height
float4 _WWRipples[32];
float4 _WWRippleShapes[32];
float WaterClock() { return _WWActive > .5 ? _WWTime : _Time.y; }
void EvaluateInteraction(float2 xz, float time, out float height, out float2 gradient)
{
    height = 0; gradient = 0;
    [loop] for (int i = 0; i < min((int)_WWRippleCount, 32); ++i)
    {
        float4 r = _WWRipples[i], shape = _WWRippleShapes[i];
        float age = time - r.z;
        if (age < 0 || age >= _WWRippleSettings.z) continue;
        float2 delta = xz - r.xy;
        float distance = max(length(delta), .001);
        float q = distance - age * _WWRippleSettings.x;
        if (abs(q) > shape.x * 3) continue;
        float fade = smoothstep(0, 1, age / .15) * smoothstep(0, 1, (_WWRippleSettings.z - age) / .75);
        float envelope = r.w * exp(-q*q/(shape.x*shape.x) - age*_WWRippleSettings.y) * fade;
        float k = 6.28318530718 / shape.y;
        height += envelope * cos(k*q);
        gradient += delta / distance * envelope * (-2*q/(shape.x*shape.x)*cos(k*q) - k*sin(k*q));
    }
    float limit = max(.01, _WWRippleSettings.w);
    float factor = rsqrt(1 + height*height/(limit*limit));
    height *= factor; gradient *= factor*factor*factor;
}
#endif
