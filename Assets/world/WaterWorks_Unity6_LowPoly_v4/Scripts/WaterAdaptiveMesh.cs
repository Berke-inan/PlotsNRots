using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A single XZ grid with dense cells around a target, coarser cells at the lake edges.
/// GPU waves stay in world space when the mesh re-centres. No overlapping water patches.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(MeshFilter))]
public sealed class WaterAdaptiveMesh : MonoBehaviour
{
    [Tooltip("Assign the boat or camera. Empty uses MainCamera once at startup, otherwise the lake centre.")]
    public Transform followTarget;
    [Min(8)] public float detailedAreaSize = 96;
    [Range(16, 192)] public int detailedSegments = 128;
    [Range(4, 32)] public int outerSegments = 24;
    [Min(.5f)] public float recenterDistance = 3;
    private MeshFilter _filter;
    private Mesh _source, _generated;
    private Vector3[] _vertices, _normals;
    private float[] _xs, _zs;
    private Bounds _bounds;
    private Vector3 _lastTarget;
    private int _segments;

    private void OnEnable()
    {
        _filter = GetComponent<MeshFilter>(); _source = _filter.sharedMesh;
        if (_source == null) { Debug.LogWarning("WaterAdaptiveMesh requires an existing flat XZ mesh.", this); enabled = false; return; }
        Vector3 scale = transform.lossyScale;
        if (Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)) < .0001f || Vector3.Dot(transform.up,Vector3.up) < .9999f)
        { Debug.LogWarning("WaterAdaptiveMesh requires a horizontal mesh with non-zero scale.",this); enabled=false; return; }
        _bounds = _source.bounds;
        if (_bounds.size.x <= .001f || _bounds.size.z <= .001f) { enabled = false; return; }
        if (followTarget == null && Camera.main != null) followTarget = Camera.main.transform;
        int inner = Mathf.Clamp(detailedSegments, 16, 192), outer = Mathf.Clamp(outerSegments, 4, 32);
        detailedSegments = inner; outerSegments = outer; _segments = inner + 2 * outer;
        int n = _segments + 1;
        _vertices = new Vector3[n*n]; _normals = new Vector3[n*n]; _xs = new float[n]; _zs = new float[n];
        int[] indices = new int[_segments*_segments*6];
        int index = 0;
        for (int z = 0; z < _segments; ++z) for (int x = 0; x < _segments; ++x)
        {
            int i = z*n+x;
            indices[index++] = i; indices[index++] = i+n; indices[index++] = i+1;
            indices[index++] = i+1; indices[index++] = i+n; indices[index++] = i+n+1;
        }
        for (int i = 0; i < _normals.Length; ++i) _normals[i] = Vector3.up;
        _generated = new Mesh { name = "WaterWorks Adaptive Grid (runtime)", indexFormat = n*n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        _generated.MarkDynamic();
        Rebuild(); _generated.triangles = indices; _generated.normals = _normals;
        _filter.sharedMesh = _generated;
    }
    private void LateUpdate()
    {
        Vector3 p = followTarget != null ? followTarget.position : transform.TransformPoint(_bounds.center);
        p.y = 0;
        if ((p - _lastTarget).sqrMagnitude >= recenterDistance*recenterDistance) Rebuild();
    }
    private void Rebuild()
    {
        Vector3 p = followTarget != null ? followTarget.position : transform.TransformPoint(_bounds.center);
        _lastTarget = new Vector3(p.x, 0, p.z);
        Vector3 focus = transform.InverseTransformPoint(p), scale = transform.lossyScale;
        BuildAxis(_xs, _bounds.min.x, _bounds.max.x, focus.x, detailedAreaSize / Mathf.Max(.001f, Mathf.Abs(scale.x)));
        BuildAxis(_zs, _bounds.min.z, _bounds.max.z, focus.z, detailedAreaSize / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        int n = _segments+1;
        for (int z = 0; z < n; ++z) for (int x = 0; x < n; ++x) _vertices[z*n+x] = new Vector3(_xs[x], _bounds.center.y, _zs[z]);
        _generated.vertices = _vertices;
        _generated.bounds = _bounds;
    }
    private void BuildAxis(float[] axis, float min, float max, float focus, float detailSize)
    {
        float length = max-min;
        if (detailSize >= length*.9f)
        { for (int i = 0; i <= _segments; ++i) axis[i] = Mathf.Lerp(min,max,(float)i/_segments); return; }
        float half = Mathf.Max(.001f, detailSize)*.5f;
        float margin = length*.001f;
        float center = Mathf.Clamp(focus,min+half+margin,max-half-margin);
        float a = center-half, b = center+half;
        for (int i = 0; i <= outerSegments; ++i)
        { float t = (float)i/outerSegments; axis[i] = a-(a-min)*(1-t)*(1-t); }
        for (int i = 1; i <= detailedSegments; ++i) axis[outerSegments+i] = Mathf.Lerp(a,b,(float)i/detailedSegments);
        for (int i = 1; i <= outerSegments; ++i)
        { float t = (float)i/outerSegments; axis[outerSegments+detailedSegments+i] = b+(max-b)*t*t; }
    }
    private void OnDisable()
    {
        if (_filter != null && _filter.sharedMesh == _generated) _filter.sharedMesh = _source;
        if (_generated != null) { if (Application.isPlaying) Destroy(_generated); else DestroyImmediate(_generated); }
        _generated = null;
    }
}
