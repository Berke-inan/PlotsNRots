using UnityEngine;

/// <summary>
/// Keeps the fullscreen water-volume material aligned with a low-poly water surface.
/// Attach this to the lake/ocean surface MeshRenderer.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public sealed class Water_Settings : MonoBehaviour
{
    [Header("Volume")]
    [Min(0.1f)] public float volumeDepth = 25f;
    [Min(0f)] public float extraHorizontalPadding = 2f;
    public bool autoSizeFromRenderer = true;

    [Header("Optional Override")]
    [Tooltip("Leave empty to load Resources/Water_Volume automatically.")]
    public Material waterVolumeMaterial;

    private MeshRenderer _renderer;
    private Material _waterSurfaceMaterial;
    private WaterSurface _surface;

    private static readonly int PosId = Shader.PropertyToID("pos");
    private static readonly int BoundsId = Shader.PropertyToID("bounds");
    private static readonly int DisplacementAmountId = Shader.PropertyToID("_Displacement_Amount");

    private void OnEnable()
    {
        CacheReferences();
        ApplySettings();
    }

    private void OnValidate()
    {
        volumeDepth = Mathf.Max(0.1f, volumeDepth);
        extraHorizontalPadding = Mathf.Max(0f, extraHorizontalPadding);
        CacheReferences();
        ApplySettings();
    }

    private void LateUpdate()
    {
        CacheReferences();
        ApplySettings();
    }

    private void CacheReferences()
    {
        if (_renderer == null)
            _renderer = GetComponent<MeshRenderer>();

        if (_surface == null) _surface = GetComponent<WaterSurface>();

        if (_renderer != null)
            _waterSurfaceMaterial = _renderer.sharedMaterial;

        if (waterVolumeMaterial == null)
            waterVolumeMaterial = Resources.Load<Material>("Water_Volume");
    }

    private void ApplySettings()
    {
        if (waterVolumeMaterial == null || _renderer == null)
            return;

        float displacement = 0f;
        if (_waterSurfaceMaterial != null && _waterSurfaceMaterial.HasProperty(DisplacementAmountId))
            displacement = Mathf.Abs(_waterSurfaceMaterial.GetFloat(DisplacementAmountId));

        Bounds rendererBounds = _renderer.bounds;
        // Renderer bounds now include GPU displacement; their top is not the waterline.
        float surfaceY = _surface != null ? _surface.BaseWaterLevel : transform.position.y + (displacement * 0.33f);

        Vector3 size;
        Vector3 center;

        if (autoSizeFromRenderer)
        {
            size = new Vector3(
                rendererBounds.size.x + extraHorizontalPadding * 2f,
                volumeDepth,
                rendererBounds.size.z + extraHorizontalPadding * 2f);

            center = new Vector3(
                rendererBounds.center.x,
                surfaceY - volumeDepth * 0.5f,
                rendererBounds.center.z);
        }
        else
        {
            Vector4 existingBounds = waterVolumeMaterial.GetVector(BoundsId);
            size = new Vector3(existingBounds.x, existingBounds.y, existingBounds.z);
            center = new Vector3(transform.position.x, surfaceY - size.y * 0.5f, transform.position.z);
        }

        waterVolumeMaterial.SetVector(PosId, new Vector4(center.x, center.y, center.z, 0f));
        waterVolumeMaterial.SetVector(BoundsId, new Vector4(size.x, size.y, size.z, 0f));
    }
}
