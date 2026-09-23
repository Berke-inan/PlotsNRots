using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, DefaultExecutionOrder(110)]
public sealed class StylizedCloudManager : MonoBehaviour
{
    [Header("Shared cloud assets")]
    [SerializeField] private Mesh[] sharedMeshes = Array.Empty<Mesh>();
    [SerializeField] private Material sharedMaterial;
    [SerializeField] private WeatherCloudProfile cloudProfile;
    [SerializeField] private Transform anchor;

    [Header("PC pool")]
    [SerializeField, Range(16, 48)] private int poolSize = 32;
    [SerializeField] private int distributionSeed = 73129;

    [Header("World-space distribution")]
    [SerializeField, Min(1f)] private float minimumRadius = 220f;
    [SerializeField, Min(1f)] private float maximumRadius = 900f;
    [SerializeField] private Vector2 windDirection = new Vector2(1f, .3f);
    [SerializeField, Min(0f)] private float baseWindSpeed = 2.4f;

    [Header("Response")]
    [SerializeField, Min(.05f)] private float opacityResponse = .55f;
    [SerializeField, Min(.05f)] private float scaleResponse = .35f;
    [SerializeField, Min(.05f)] private float altitudeResponse = 18f;
    [SerializeField, Min(.05f)] private float tintResponse = 1.4f;

    private sealed class Slot
    {
        public Transform transform;
        public MeshRenderer renderer;
        public MaterialPropertyBlock properties;
        public float opacity;
        public float scaleSeed;
        public float verticalSeed;
        public float altitudeSeed;
        public float opacitySeed;
        public float transmissionSeed;
        public float tintSeed;
        public float variation;
    }

    private readonly List<Slot> slots = new List<Slot>(40);
    private System.Random random;
    private bool built;

    private WeatherCloudProfile.Style targetStyle;
    private WeatherCloudProfile.Style currentStyle;
    private float targetCount;
    private float currentCount;
    private float targetWind;
    private float currentWind;
    private Color currentTint = Color.white;
    private float ceilingOpacity;

    public int PoolCount => slots.Count;
    public int TargetVisibleCount => Mathf.RoundToInt(targetCount);
    public int VisibleCount { get; private set; }
    public float CeilingOpacity => ceilingOpacity;
    public bool IsTransitioning { get; private set; }
    public Material SharedMaterial => sharedMaterial;

    private void Awake()
    {
        ResolveAnchor();
        targetStyle = ResolveStyle(WeatherType.Sunny);
        currentStyle = targetStyle;
        currentTint = targetStyle.tint;
        BuildPool();
    }

    private void OnEnable()
    {
        ResolveAnchor();
        if (!built) BuildPool();
    }

    public void SetWeather(WeatherType weather, float intensity, WeatherVisuals visuals, float windStrength)
    {
        intensity = Mathf.Clamp01(intensity);

        targetStyle = ResolveStyle(weather);
        targetCount = Mathf.Lerp(targetStyle.cloudCount.x, targetStyle.cloudCount.y, intensity);
        targetCount = Mathf.Clamp(targetCount, 0f, slots.Count);

        targetWind = Mathf.Clamp01(windStrength) * targetStyle.windMultiplier;
        ceilingOpacity = Mathf.Clamp01(Mathf.Max(visuals.cloudOvercast, targetStyle.ceilingStrength) * visuals.cloudThickness);
    }

    public void SetWeather(WeatherVisuals visuals, float windStrength)
    {
        SeasonManager manager = SeasonManager.Instance;
        SetWeather(
            manager != null ? manager.currentWeather : WeatherType.Sunny,
            manager != null ? manager.WeatherIntensity : visuals.cloudCoverage,
            visuals,
            windStrength);
    }

    public void ApplyWeather(WeatherType weather, float intensity, WeatherVisuals visuals, float windStrength)
    {
        SetWeather(weather, intensity, visuals, windStrength);

        currentStyle = targetStyle;
        currentCount = targetCount;
        currentWind = targetWind;
        currentTint = targetStyle.tint;

        Vector3 center = anchor != null ? anchor.position : transform.position;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            slot.opacity = DesiredOpacity(slot, i);
            ApplyScaleAndAltitude(slot, 1f, center);
        }

        ApplyProperties();
    }

    public void ApplyWeather(WeatherVisuals visuals, float windStrength)
    {
        SeasonManager manager = SeasonManager.Instance;
        ApplyWeather(
            manager != null ? manager.currentWeather : WeatherType.Sunny,
            manager != null ? manager.WeatherIntensity : visuals.cloudCoverage,
            visuals,
            windStrength);
    }

    private void Update()
    {
        ResolveAnchor();
        Tick(Time.deltaTime, anchor != null ? anchor.position : transform.position);
    }

    public void Tick(float deltaTime, Vector3 anchorPosition)
    {
        if (!built) BuildPool();
        if (slots.Count == 0) return;

        float dt = Mathf.Max(0f, deltaTime);
        float opacityStep = dt / Mathf.Max(.05f, opacityResponse);
        float scaleT = 1f - Mathf.Exp(-scaleResponse * dt);
        float styleT = 1f - Mathf.Exp(-tintResponse * dt);

        currentCount = Mathf.MoveTowards(currentCount, targetCount, opacityStep * 7f);
        currentWind = Mathf.Lerp(currentWind, targetWind, styleT);
        currentTint = Color.Lerp(currentTint, targetStyle.tint, styleT);
        currentStyle = LerpStyle(currentStyle, targetStyle, styleT);

        Vector2 wind2 = windDirection.sqrMagnitude > .0001f ? windDirection.normalized : Vector2.right;
        Vector3 wind = new Vector3(wind2.x, 0f, wind2.y)
            * baseWindSpeed
            * Mathf.Lerp(.45f, 2.2f, Mathf.Clamp01(currentWind))
            * dt;

        VisibleCount = 0;
        IsTransitioning = false;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            float desiredOpacity = DesiredOpacity(slot, i);

            slot.opacity = Mathf.MoveTowards(slot.opacity, desiredOpacity, opacityStep);
            IsTransitioning |= !Mathf.Approximately(slot.opacity, desiredOpacity);

            slot.transform.position += wind;
            ApplyScaleAndAltitude(slot, scaleT, anchorPosition);

            Vector3 horizontal = slot.transform.position - anchorPosition;
            horizontal.y = 0f;
            float sqr = horizontal.sqrMagnitude;

            if (sqr > maximumRadius * maximumRadius * 1.25f || sqr < minimumRadius * minimumRadius * .16f)
                PlaceSlot(slot, anchorPosition, true);

            slot.renderer.enabled = slot.opacity > .01f;
            if (slot.renderer.enabled) VisibleCount++;
        }

        ApplyProperties();
    }

    private void BuildPool()
    {
        if (built) return;
        built = true;
        random = new System.Random(distributionSeed);

        if (sharedMeshes == null || sharedMeshes.Length == 0 || sharedMaterial == null)
        {
            Debug.LogWarning("StylizedCloudManager has no shared meshes/material assigned.", this);
            return;
        }

        int count = Mathf.Clamp(poolSize, 1, 48);
        Vector3 center = anchor != null ? anchor.position : transform.position;

        for (int i = 0; i < count; i++)
        {
            Mesh mesh = sharedMeshes[i % sharedMeshes.Length];
            if (mesh == null) continue;

            var cloud = new GameObject($"Pooled Cloud {i:00}");
            cloud.transform.SetParent(transform, true);

            var filter = cloud.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = cloud.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.enabled = false;

            var slot = new Slot
            {
                transform = cloud.transform,
                renderer = renderer,
                properties = new MaterialPropertyBlock(),
                scaleSeed = Next01(),
                verticalSeed = Next01(),
                altitudeSeed = Next01(),
                opacitySeed = Next01(),
                transmissionSeed = Next01(),
                tintSeed = Next01(),
                variation = Mathf.Lerp(.90f, 1.04f, Next01())
            };

            slots.Add(slot);
            PlaceSlot(slot, center, false);
        }

        ApplyProperties();
    }

    private void PlaceSlot(Slot slot, Vector3 center, bool downwindEdge)
    {
        float angle = Next01() * Mathf.PI * 2f;

        if (downwindEdge)
        {
            Vector2 direction = windDirection.sqrMagnitude > .0001f ? windDirection.normalized : Vector2.right;
            Vector2 againstWind = -direction;
            angle = Mathf.Atan2(againstWind.y, againstWind.x) + Mathf.Lerp(-.8f, .8f, Next01());
        }

        float radius = Mathf.Sqrt(Mathf.Lerp(
            minimumRadius * minimumRadius,
            maximumRadius * maximumRadius,
            Next01()));

        slot.transform.position = center + new Vector3(
            Mathf.Cos(angle) * radius,
            Mathf.Lerp(currentStyle.altitudeRange.x, currentStyle.altitudeRange.y, slot.altitudeSeed),
            Mathf.Sin(angle) * radius);

        slot.transform.rotation = Quaternion.Euler(
            Mathf.Lerp(-2f, 2f, Next01()),
            Next01() * 360f,
            Mathf.Lerp(-4f, 4f, Next01()));

        ApplyScaleAndAltitude(slot, 1f, center);
    }

    private void ApplyScaleAndAltitude(Slot slot, float t, Vector3 center)
    {
        float scale = Mathf.Lerp(currentStyle.scaleRange.x, currentStyle.scaleRange.y, slot.scaleSeed);

        Vector3 desiredScale = new Vector3(
            scale * Mathf.Lerp(.84f, 1.30f, slot.scaleSeed),
            scale * Mathf.Lerp(.66f, .90f, slot.verticalSeed),
            scale);

        slot.transform.localScale = t >= .999f
            ? desiredScale
            : Vector3.Lerp(slot.transform.localScale, desiredScale, t);

        float desiredY = center.y + Mathf.Lerp(currentStyle.altitudeRange.x, currentStyle.altitudeRange.y, slot.altitudeSeed);
        Vector3 position = slot.transform.position;
        position.y = Mathf.MoveTowards(position.y, desiredY, altitudeResponse * Time.deltaTime);
        slot.transform.position = position;
    }

    private float DesiredOpacity(Slot slot, int index)
    {
        float member = Mathf.Clamp01(currentCount - index);
        if (member <= 0f) return 0f;

        float authored = Mathf.Lerp(currentStyle.opacityRange.x, currentStyle.opacityRange.y, slot.opacitySeed);
        return member * authored;
    }

    private void ApplyProperties()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];

            float transmission = Mathf.Lerp(
                currentStyle.transmissionRange.x,
                currentStyle.transmissionRange.y,
                slot.transmissionSeed);

            Color perCloudTint = Color.Lerp(Color.white, currentTint, Mathf.Lerp(.60f, .92f, slot.tintSeed));

            slot.properties.Clear();
            slot.properties.SetFloat("_CloudOpacity", slot.opacity);
            slot.properties.SetFloat("_CloudVariation", slot.variation);
            slot.properties.SetFloat("_CloudTransmission", transmission);
            slot.properties.SetColor("_CloudTint", perCloudTint);
            slot.renderer.SetPropertyBlock(slot.properties);
        }
    }

    private WeatherCloudProfile.Style ResolveStyle(WeatherType weather)
    {
        if (cloudProfile != null && cloudProfile.TryGet(weather, out WeatherCloudProfile.Style style))
            return style;

        return WeatherCloudProfile.Recommended(weather);
    }

    private static WeatherCloudProfile.Style LerpStyle(
        WeatherCloudProfile.Style a,
        WeatherCloudProfile.Style b,
        float t)
    {
        a.cloudCount = new Vector2Int(
            Mathf.RoundToInt(Mathf.Lerp(a.cloudCount.x, b.cloudCount.x, t)),
            Mathf.RoundToInt(Mathf.Lerp(a.cloudCount.y, b.cloudCount.y, t)));

        a.scaleRange = Vector2.Lerp(a.scaleRange, b.scaleRange, t);
        a.altitudeRange = Vector2.Lerp(a.altitudeRange, b.altitudeRange, t);
        a.opacityRange = Vector2.Lerp(a.opacityRange, b.opacityRange, t);
        a.transmissionRange = Vector2.Lerp(a.transmissionRange, b.transmissionRange, t);
        a.tint = Color.Lerp(a.tint, b.tint, t);
        a.ceilingStrength = Mathf.Lerp(a.ceilingStrength, b.ceilingStrength, t);
        a.windMultiplier = Mathf.Lerp(a.windMultiplier, b.windMultiplier, t);
        a.weather = b.weather;
        return a;
    }

    private float Next01() => (float)random.NextDouble();

    private void ResolveAnchor()
    {
        if (anchor != null) return;
        Camera camera = Camera.main;
        if (camera != null) anchor = camera.transform;
    }
}