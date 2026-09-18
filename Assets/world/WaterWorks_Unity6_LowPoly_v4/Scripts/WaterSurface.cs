using UnityEngine;

/// <summary>Shared CPU/GPU wave model. A horizontal XZ mesh is required.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public sealed class WaterSurface : MonoBehaviour
{
    public float surfaceLevelOffset;
    [Tooltip("Leave empty to follow the renderer's material.")]
    public Material waterMaterial;
    [Header("Interaction waves")]
    [Range(1, 32)] public int maxRipples = 32;
    [Min(0.1f)] public float rippleLifetime = 5f;
    [Min(0.1f)] public float rippleSpeed = 2.5f;
    [Min(0.01f)] public float rippleDecay = 0.65f;
    [Min(0.01f)] public float maxInteractionHeight = 0.3f;

    private const int Capacity = 32;
    private readonly Vector4[] _ripples = new Vector4[Capacity]; // x,z,start,amplitude
    private readonly Vector4[] _shapes = new Vector4[Capacity]; // width,wavelength,unused,unused
    private MeshRenderer _renderer;
    private MeshFilter _filter;
    private MaterialPropertyBlock _block;
    private int _count, _settingsFrame = -1;
    private float _waveScale = .14f, _waveSpeed = .28f, _waveHeight = .11f, _secondaryStrength = .55f, _detailStrength = .28f;
    private static readonly Vector2 D1 = new Vector2(.943858f, .330350f);
    private static readonly Vector2 D2 = new Vector2(-.481919f, .876216f);
    private static readonly Vector2 D3 = new Vector2(.821370f, -.570396f);
    private static readonly int CountId = Shader.PropertyToID("_WWRippleCount"), RipplesId = Shader.PropertyToID("_WWRipples"), ShapesId = Shader.PropertyToID("_WWRippleShapes");
    private static readonly int SettingsId = Shader.PropertyToID("_WWRippleSettings"), TimeId = Shader.PropertyToID("_WWTime"), ActiveId = Shader.PropertyToID("_WWActive"), OffsetId = Shader.PropertyToID("_WWSurfaceOffset");
    private static readonly int ScaleId = Shader.PropertyToID("_WaveScale"), SpeedId = Shader.PropertyToID("_WaveSpeed"), HeightId = Shader.PropertyToID("_WaveHeight"), SecondaryId = Shader.PropertyToID("_SecondaryWaveStrength"), DetailId = Shader.PropertyToID("_DetailWaveStrength");

    public float BaseWaterLevel => (_filter != null && _filter.sharedMesh != null ? transform.TransformPoint(_filter.sharedMesh.bounds.center).y : transform.position.y) + surfaceLevelOffset;
    public float MaxWaveHeight { get { RefreshSettings(); return Mathf.Abs(_waveHeight) + maxInteractionHeight; } }
    public int ActiveRippleCount => _count;

    private void OnEnable()
    {
        _renderer = GetComponent<MeshRenderer>(); _filter = GetComponent<MeshFilter>();
        if (_block == null) _block = new MaterialPropertyBlock();
        _settingsFrame = -1; _count = 0; RefreshSettings(); Upload();
    }
    private void OnValidate()
    {
        maxRipples = Mathf.Clamp(maxRipples, 1, Capacity);
        rippleLifetime = Mathf.Max(.1f, rippleLifetime); rippleSpeed = Mathf.Max(.1f, rippleSpeed);
        rippleDecay = Mathf.Max(.01f, rippleDecay); maxInteractionHeight = Mathf.Max(.01f, maxInteractionHeight);
        _settingsFrame = -1;
    }
    private void LateUpdate()
    {
        RefreshSettings();
        for (int i = _count - 1; i >= 0; --i)
            if (Time.time - _ripples[i].z >= rippleLifetime)
            { --_count; _ripples[i] = _ripples[_count]; _shapes[i] = _shapes[_count]; }
        _count = Mathf.Min(_count, Mathf.Clamp(maxRipples, 1, Capacity));
        Upload();
    }
    private void OnDisable()
    {
        _count = 0;
        if (_renderer == null || _block == null) return;
        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(ActiveId, 0); _block.SetFloat(CountId, 0); _block.SetFloat(OffsetId, 0);
        _renderer.SetPropertyBlock(_block); _renderer.ResetLocalBounds();
    }
    private void RefreshSettings()
    {
        if (_settingsFrame == Time.frameCount) return;
        _settingsFrame = Time.frameCount;
        Material mat = waterMaterial != null ? waterMaterial : (_renderer != null ? _renderer.sharedMaterial : null);
        if (mat == null) return;
        if (mat.HasProperty(ScaleId)) _waveScale = mat.GetFloat(ScaleId);
        if (mat.HasProperty(SpeedId)) _waveSpeed = mat.GetFloat(SpeedId);
        if (mat.HasProperty(HeightId)) _waveHeight = mat.GetFloat(HeightId);
        if (mat.HasProperty(SecondaryId)) _secondaryStrength = Mathf.Clamp01(mat.GetFloat(SecondaryId));
        if (mat.HasProperty(DetailId)) _detailStrength = Mathf.Clamp01(mat.GetFloat(DetailId));
    }
    private void Upload()
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(ActiveId, 1); _block.SetFloat(TimeId, Time.time);
        _block.SetFloat(OffsetId, surfaceLevelOffset); _block.SetFloat(CountId, _count);
        _block.SetVectorArray(RipplesId, _ripples); _block.SetVectorArray(ShapesId, _shapes);
        _block.SetVector(SettingsId, new Vector4(rippleSpeed, rippleDecay, rippleLifetime, maxInteractionHeight));
        _renderer.SetPropertyBlock(_block);
        if (_filter != null && _filter.sharedMesh != null && Mathf.Abs(transform.lossyScale.y) > .0001f)
        {
            Bounds b = _filter.sharedMesh.bounds;
            b.Expand(new Vector3(0, 2f * (MaxWaveHeight + Mathf.Abs(surfaceLevelOffset)) / Mathf.Abs(transform.lossyScale.y), 0));
            _renderer.localBounds = b;
        }
    }
    public void AddRipple(Vector3 position, float amplitude, float width = 1.2f, float wavelength = 2.4f)
    {
        if (!isActiveAndEnabled || Mathf.Abs(amplitude) < .0001f) return;
        int budget = Mathf.Clamp(maxRipples, 1, Capacity);
        _count = Mathf.Min(_count, budget);
        int slot = _count;
        if (_count < budget) ++_count;
        else
        {
            slot = 0;
            for (int i = 1; i < _count; ++i) if (_ripples[i].z < _ripples[slot].z) slot = i;
        }
        _ripples[slot] = new Vector4(position.x, position.z, Time.time, Mathf.Clamp(amplitude, -maxInteractionHeight, maxInteractionHeight));
        _shapes[slot] = new Vector4(Mathf.Max(.2f, width), Mathf.Max(.5f, wavelength), 0, 0);
    }
    private void Evaluate(Vector2 xz, float time, out float height, out Vector2 gradient)
    {
        RefreshSettings();
        float t = time * _waveSpeed, w = 1f + _secondaryStrength + _detailStrength;
        float p1 = Vector2.Dot(xz, D1) * _waveScale + t;
        float p2 = Vector2.Dot(xz, D2) * _waveScale * .63f - t * 1.27f;
        float p3 = Vector2.Dot(xz, D3) * _waveScale * 1.47f + t * .71f;
        height = (Mathf.Sin(p1) + Mathf.Sin(p2) * _secondaryStrength + Mathf.Sin(p3) * _detailStrength) * _waveHeight / w;
        gradient = (Mathf.Cos(p1) * D1 * _waveScale + Mathf.Cos(p2) * D2 * (_waveScale * .63f * _secondaryStrength) + Mathf.Cos(p3) * D3 * (_waveScale * 1.47f * _detailStrength)) * (_waveHeight / w);
        float sum = 0; Vector2 slope = Vector2.zero;
        for (int i = 0; i < _count; ++i)
        {
            Vector4 r = _ripples[i], shape = _shapes[i];
            float age = time - r.z;
            if (age < 0 || age >= rippleLifetime) continue;
            Vector2 delta = xz - new Vector2(r.x, r.y);
            float distance = Mathf.Max(delta.magnitude, .001f), q = distance - age * rippleSpeed;
            if (Mathf.Abs(q) > shape.x * 3f) continue;
            float fade = Mathf.SmoothStep(0, 1, age / .15f) * Mathf.SmoothStep(0, 1, (rippleLifetime - age) / .75f);
            float envelope = r.w * Mathf.Exp(-q * q / (shape.x * shape.x) - age * rippleDecay) * fade;
            float k = 2f * Mathf.PI / shape.y;
            sum += envelope * Mathf.Cos(k * q);
            slope += delta / distance * envelope * (-2f * q / (shape.x * shape.x) * Mathf.Cos(k * q) - k * Mathf.Sin(k * q));
        }
        float factor = 1f / Mathf.Sqrt(1f + sum * sum / (maxInteractionHeight * maxInteractionHeight));
        height += sum * factor; gradient += slope * (factor * factor * factor);
    }
    public float GetWaterHeight(Vector3 worldPosition) => GetWaterHeight(worldPosition, Time.time);
    public float GetWaterHeight(Vector3 worldPosition, float time)
    { Evaluate(new Vector2(worldPosition.x, worldPosition.z), time, out float h, out _); return BaseWaterLevel + h; }
    public Vector3 GetWaterNormal(Vector3 worldPosition) => GetWaterNormal(worldPosition, Time.time);
    public Vector3 GetWaterNormal(Vector3 worldPosition, float time)
    { Evaluate(new Vector2(worldPosition.x, worldPosition.z), time, out _, out Vector2 g); return new Vector3(-g.x, 1, -g.y).normalized; }
    public float GetVerticalVelocity(Vector3 position, float time)
    { const float dt = .01f; return (GetWaterHeight(position, time + dt) - GetWaterHeight(position, time - dt)) / (2f * dt); }
}
