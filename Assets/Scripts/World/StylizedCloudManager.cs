using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One owner for all fair-weather cloud geometry. Pool children contain render data only.
[DisallowMultipleComponent, DefaultExecutionOrder(110)]
public sealed class StylizedCloudManager : MonoBehaviour
{
    [Header("Shared cloud assets")]
    [SerializeField] private Mesh[] sharedMeshes = Array.Empty<Mesh>();
    [SerializeField] private Material sharedMaterial;
    [SerializeField] private Transform anchor;

    [Header("Pool budget")]
    [SerializeField, Range(12, 20)] private int pcPoolSize = 20;
    [SerializeField, Range(6, 10)] private int mobilePoolSize = 10;
    [SerializeField] private int distributionSeed = 73129;

    [Header("World-space distribution")]
    [SerializeField, Min(1)] private float minimumRadius = 250;
    [SerializeField, Min(1)] private float maximumRadius = 800;
    [SerializeField] private Vector2 altitudeRange = new Vector2(150, 350);
    [SerializeField] private Vector2 scaleRange = new Vector2(10, 20);
    [SerializeField] private Vector2 verticalScaleRange = new Vector2(.65f, .9f);
    [SerializeField] private Vector2 windDirection = new Vector2(1, .3f);
    [SerializeField, Min(0)] private float baseWindSpeed = 2.4f;
    [SerializeField, Min(.1f)] private float opacityResponse = .45f;

    private sealed class Slot
    {
        public Transform transform;
        public MeshRenderer renderer;
        public MaterialPropertyBlock properties;
        public float opacity;
        public float variation;
    }

    private readonly List<Slot> slots = new List<Slot>(20);
    private System.Random random;
    private float targetCount;
    private float targetWind;
    private float ceilingOpacity;
    private bool built;

    public int PoolCount => slots.Count;
    public int TargetVisibleCount => Mathf.RoundToInt(targetCount);
    public int VisibleCount { get; private set; }
    public float CeilingOpacity => ceilingOpacity;
    public bool IsTransitioning { get; private set; }
    public Material SharedMaterial => sharedMaterial;

    private void Awake()
    {
        ResolveAnchor();
        BuildPool();
    }

    private void OnEnable()
    {
        ResolveAnchor();
        if (!built) BuildPool();
    }

    public void SetWeather(WeatherVisuals state, float windStrength)
    {
        float fairCoverage = Mathf.InverseLerp(.1f, .42f, state.cloudCoverage);
        float fairCount = Mathf.Lerp(5, 14, fairCoverage);
        float ceilingBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .68f, state.cloudOvercast));
        // Settled weather uses whole, opaque clusters. Fractional opacity exists only while Tick
        // moves between two weather targets, so a stable sky never shows a dithered cloud shell.
        targetCount = Mathf.Round(Mathf.Clamp(fairCount * (1 - ceilingBlend), 0, slots.Count));
        ceilingOpacity = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .78f, state.cloudOvercast));
        targetWind = Mathf.Clamp01(windStrength);
    }

    public void ApplyWeather(WeatherVisuals state, float windStrength)
    {
        SetWeather(state, windStrength);
        for (int i = 0; i < slots.Count; i++) slots[i].opacity = Mathf.Clamp01(targetCount - i);
        ApplyOpacityProperties();
    }

    private void Update()
    {
        ResolveAnchor();
        Tick(Time.deltaTime, anchor != null ? anchor.position : transform.position);
    }

    // Public for deterministic editor validation; ordinary runtime work enters through Update.
    public void Tick(float deltaTime, Vector3 anchorPosition)
    {
        if (!built) BuildPool();
        float step = Mathf.Max(0, deltaTime) / Mathf.Max(.1f, opacityResponse);
        Vector3 wind = new Vector3(windDirection.x, 0, windDirection.y).normalized
            * baseWindSpeed * Mathf.Lerp(.45f, 2.25f, targetWind) * Mathf.Max(0, deltaTime);
        VisibleCount = 0;
        IsTransitioning = false;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            float desired = Mathf.Clamp01(targetCount - i);
            slot.opacity = Mathf.MoveTowards(slot.opacity, desired, step);
            IsTransitioning |= !Mathf.Approximately(slot.opacity, desired);
            slot.transform.position += wind;
            Vector3 horizontal = slot.transform.position - anchorPosition;
            horizontal.y = 0;
            if (horizontal.sqrMagnitude > maximumRadius * maximumRadius * 1.3f
                || horizontal.sqrMagnitude < minimumRadius * minimumRadius * .2f)
                PlaceSlot(slot, anchorPosition, true);
            slot.renderer.enabled = slot.opacity > .01f;
            if (slot.renderer.enabled) VisibleCount++;
        }
        ApplyOpacityProperties();
    }

    private void BuildPool()
    {
        if (built) return;
        built = true;
        random = new System.Random(distributionSeed);
        if (sharedMeshes == null || sharedMeshes.Length == 0 || sharedMaterial == null) return;
        int requested = Application.isMobilePlatform ? mobilePoolSize : pcPoolSize;
        int count = Mathf.Clamp(requested, 1, 20);
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
            var slot = new Slot {
                transform = cloud.transform,
                renderer = renderer,
                properties = new MaterialPropertyBlock(),
                variation = Mathf.Lerp(.86f, 1.08f, Next01())
            };
            slots.Add(slot);
            PlaceSlot(slot, center, false);
        }
        ApplyOpacityProperties();
    }

    private void PlaceSlot(Slot slot, Vector3 center, bool downwindEdge)
    {
        float angle = Next01() * Mathf.PI * 2;
        if (downwindEdge)
        {
            Vector2 againstWind = -windDirection.normalized;
            angle = Mathf.Atan2(againstWind.y, againstWind.x) + Mathf.Lerp(-.75f, .75f, Next01());
        }
        float radius = Mathf.Sqrt(Mathf.Lerp(minimumRadius * minimumRadius, maximumRadius * maximumRadius, Next01()));
        slot.transform.position = center + new Vector3(Mathf.Cos(angle) * radius,
            Mathf.Lerp(altitudeRange.x, altitudeRange.y, Next01()), Mathf.Sin(angle) * radius);
        float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, Next01());
        slot.transform.localScale = new Vector3(scale * Mathf.Lerp(.85f, 1.35f, Next01()),
            scale * Mathf.Lerp(verticalScaleRange.x, verticalScaleRange.y, Next01()), scale);
        slot.transform.rotation = Quaternion.Euler(0, Next01() * 360, Mathf.Lerp(-3, 3, Next01()));
    }

    private void ApplyOpacityProperties()
    {
        foreach (Slot slot in slots)
        {
            slot.properties.Clear();
            slot.properties.SetFloat("_CloudOpacity", slot.opacity);
            slot.properties.SetFloat("_CloudVariation", slot.variation);
            slot.renderer.SetPropertyBlock(slot.properties);
        }
    }

    private float Next01() => (float)random.NextDouble();

    private void ResolveAnchor()
    {
        if (anchor != null) return;
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera != null) anchor = gameplayCamera.transform;
    }
}
