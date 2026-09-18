using System.Collections.Generic;
using UnityEngine;

/// <summary>Approximate displaced-volume buoyancy and motion-generated water ripples.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class BuoyantObject : MonoBehaviour
{
    [Header("Buoyancy")]
    [Min(1f)] public float waterDensity = 1000f;
    [Min(.01f)] public float buoyancyMultiplier = 1f;
    [Min(.01f)] public float submersionDepth = .6f;
    [Tooltip("Automatic box sampling uses the box height. Custom points use Submersion Depth.")]
    public bool useBoxHeight = true;
    [Tooltip("0: collider approximation. Boats: enclosed hull displacement in cubic metres, not wood volume.")]
    [Min(0)] public float displacedVolumeOverride;
    [Range(0, 1)] public float waveNormalInfluence = .05f;
    [Header("Water Resistance")]
    [Min(0)] public float linearWaterDrag = 1.8f;
    [Min(0)] public float angularWaterDrag = 1.2f;
    [Range(0, 2)] public float verticalDamping = .9f;
    [Min(1)] public float maxWaterAcceleration = 40f;
    [Header("Sampling")]
    public Transform[] buoyancyPoints;
    [Header("Wake and entry ripples")]
    public bool emitWakes = true;
    [Tooltip("Optional stern position; otherwise the trailing edge is estimated from velocity.")]
    public Transform wakeOrigin;
    [Min(.05f)] public float wakeInterval = .25f;
    [Min(.05f)] public float wakeMinSpeed = .35f;
    [Min(0)] public float wakeStrength = .025f;
    [Min(.2f)] public float wakeWidth = 1.2f;
    [Min(.5f)] public float wakeWavelength = 2.4f;
    public bool drawDebug = true;

    private Rigidbody _rigidbody;
    private Collider _collider;
    private WaterSurface _waterSurface;
    private readonly List<WaterSurface> _waters = new List<WaterSurface>();
    private readonly Vector3[] _autoPoints = new Vector3[5];
    private float _approximateVolume = 1f, _lastSubmergedFraction, _nextWake;
    private Vector3 _lastWakePosition;
    private bool _wasTouching;
    public bool IsInWater => _waterSurface != null;
    public float SubmergedFraction => _lastSubmergedFraction;

    private void Awake() { CacheReferences(); }
    private void OnDisable()
    { _waters.Clear(); _waterSurface = null; _lastSubmergedFraction = 0; _wasTouching = false; }
    private void OnValidate()
    {
        waterDensity = Mathf.Max(1, waterDensity); buoyancyMultiplier = Mathf.Max(.01f, buoyancyMultiplier);
        submersionDepth = Mathf.Max(.01f, submersionDepth); wakeInterval = Mathf.Max(.05f, wakeInterval);
        wakeWidth = Mathf.Max(.2f, wakeWidth); wakeWavelength = Mathf.Max(.5f, wakeWavelength);
        CacheReferences();
    }
    private void CacheReferences()
    { if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>(); if (_collider == null) _collider = GetComponent<Collider>(); }
    private void CalculateApproximateVolume()
    {
        if (displacedVolumeOverride > 0) { _approximateVolume = displacedVolumeOverride; return; }
        Vector3 scale = _collider.transform.lossyScale;
        float scaleVolume = Mathf.Abs(scale.x * scale.y * scale.z);
        if (_collider is BoxCollider box)
            _approximateVolume = box.size.x * box.size.y * box.size.z * scaleVolume;
        else if (_collider is SphereCollider sphere)
        {
            float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            _approximateVolume = 4f / 3f * Mathf.PI * r * r * r;
        }
        else
        {
            // Generic hull fallback. Set the explicit volume for accurate mass/displacement tuning.
            Bounds local = _collider is MeshCollider mesh && mesh.sharedMesh != null ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            _approximateVolume = local.size.x * local.size.y * local.size.z * scaleVolume;
        }
        _approximateVolume = Mathf.Max(.001f, _approximateVolume);
    }
    public void SetWaterSurface(WaterSurface surface)
    { if (surface != null && !_waters.Contains(surface)) _waters.Add(surface); }
    public void ClearWaterSurface(WaterSurface surface)
    {
        _waters.Remove(surface);
        if (_waterSurface == surface) { _waterSurface = null; _lastSubmergedFraction = 0; _wasTouching = false; }
    }
    private void FixedUpdate()
    {
        _waterSurface = null;
        for (int i = _waters.Count - 1; i >= 0; --i)
        {
            WaterSurface s = _waters[i];
            if (s == null || !s.isActiveAndEnabled) { _waters.RemoveAt(i); continue; }
            if (_waterSurface == null || s.BaseWaterLevel > _waterSurface.BaseWaterLevel) _waterSurface = s;
        }
        if (_waterSurface == null || _rigidbody == null || _collider == null || !_collider.enabled)
        { _lastSubmergedFraction = 0; _wasTouching = false; return; }
        CalculateApproximateVolume();
        int count = FillSamplePoints();
        if (count == 0) return;
        float depthRange = submersionDepth;
        if (useBoxHeight && !HasCustomPoints && _collider is BoxCollider box)
            depthRange = Mathf.Max(.01f, box.size.y * Mathf.Abs(box.transform.lossyScale.y));
        float massPerPoint = Mathf.Max(.001f, _rigidbody.mass / count);
        float liftPerPoint = waterDensity * Physics.gravity.magnitude * (_approximateVolume / count) * buoyancyMultiplier;
        float damping = Mathf.Min(2f * Mathf.Sqrt(liftPerPoint / (depthRange * massPerPoint)) * verticalDamping, .8f / Time.fixedDeltaTime);
        float total = 0;
        for (int i = 0; i < count; ++i)
        {
            Vector3 point = GetSamplePoint(i);
            float fraction = Mathf.Clamp01((_waterSurface.GetWaterHeight(point, Time.time) - point.y) / depthRange);
            total += fraction;
            if (fraction <= 0 || _rigidbody.isKinematic) continue;
            Vector3 up = Physics.gravity.sqrMagnitude > .0001f ? -Physics.gravity.normalized : Vector3.up;
            Vector3 direction = Vector3.Slerp(up, _waterSurface.GetWaterNormal(point, Time.time), waveNormalInfluence).normalized;
            float relativeVerticalSpeed = _rigidbody.GetPointVelocity(point).y - _waterSurface.GetVerticalVelocity(point, Time.time);
            Vector3 acceleration = direction * (liftPerPoint * fraction / massPerPoint) - Vector3.up * (relativeVerticalSpeed * damping * Mathf.Sqrt(fraction));
            acceleration = Vector3.ClampMagnitude(acceleration, maxWaterAcceleration);
            _rigidbody.AddForceAtPosition(acceleration * massPerPoint, point, ForceMode.Force);
        }
        _lastSubmergedFraction = Mathf.Clamp01(total / count);
        if (_lastSubmergedFraction > 0 && !_rigidbody.isKinematic)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, Vector3.up);
            float drag = (1f - Mathf.Exp(-linearWaterDrag * _lastSubmergedFraction * Time.fixedDeltaTime)) / Time.fixedDeltaTime;
            _rigidbody.AddForce(-horizontal * drag, ForceMode.Acceleration);
            float angularDrag = (1f - Mathf.Exp(-angularWaterDrag * _lastSubmergedFraction * Time.fixedDeltaTime)) / Time.fixedDeltaTime;
            _rigidbody.AddTorque(-_rigidbody.angularVelocity * angularDrag, ForceMode.Acceleration);
        }
        UpdateWake();
    }
    private void UpdateWake()
    {
        bool touching = _lastSubmergedFraction > .001f;
        if (!emitWakes) { _wasTouching = touching; return; }
        Bounds b = _collider.bounds;
        Vector3 center = b.center;
        float height = _waterSurface.GetWaterHeight(center);
        if (touching && !_wasTouching)
        {
            _waterSurface.AddRipple(center, Mathf.Min(.2f, Mathf.Abs(_rigidbody.linearVelocity.y) * .025f), wakeWidth, wakeWavelength);
            _nextWake = Time.time; _lastWakePosition = center;
        }
        _wasTouching = touching;
        // Submerged wrecks must not keep emitting a surface wake.
        if (!touching || b.max.y < height - .15f || b.min.y > height || Time.time < _nextWake) return;
        Vector3 velocity = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, Vector3.up);
        float speed = velocity.magnitude;
        if (speed < wakeMinSpeed) return;
        Vector3 direction = velocity / speed;
        float trailingExtent = Mathf.Abs(direction.x) * b.extents.x + Mathf.Abs(direction.z) * b.extents.z;
        Vector3 origin = wakeOrigin != null ? wakeOrigin.position : center - direction * trailingExtent * .8f;
        if (Vector3.ProjectOnPlane(origin - _lastWakePosition, Vector3.up).sqrMagnitude < .04f) return;
        _waterSurface.AddRipple(origin, Mathf.Min(.2f, speed * wakeStrength), wakeWidth, wakeWavelength);
        _lastWakePosition = origin; _nextWake = Time.time + wakeInterval;
    }
    private bool HasCustomPoints => buoyancyPoints != null && buoyancyPoints.Length > 0;
    private int FillSamplePoints()
    {
        if (HasCustomPoints) return buoyancyPoints.Length;
        if (_collider is BoxCollider box)
        {
            Vector3 c = box.center, e = box.size * .5f;
            float y = c.y - e.y;
            _autoPoints[0] = box.transform.TransformPoint(new Vector3(c.x-e.x*.7f,y,c.z-e.z*.7f));
            _autoPoints[1] = box.transform.TransformPoint(new Vector3(c.x+e.x*.7f,y,c.z-e.z*.7f));
            _autoPoints[2] = box.transform.TransformPoint(new Vector3(c.x-e.x*.7f,y,c.z+e.z*.7f));
            _autoPoints[3] = box.transform.TransformPoint(new Vector3(c.x+e.x*.7f,y,c.z+e.z*.7f));
            _autoPoints[4] = box.transform.TransformPoint(new Vector3(c.x,y,c.z));
            return 5;
        }
        if (_collider == null) return 0;
        Bounds b = _collider.bounds; _autoPoints[0] = new Vector3(b.center.x, b.min.y, b.center.z); return 1;
    }
    private Vector3 GetSamplePoint(int i) => HasCustomPoints ? (buoyancyPoints[i] != null ? buoyancyPoints[i].position : transform.position) : _autoPoints[i];
    [ContextMenu("Test - Push forward (Play Mode)")]
    private void PushForward()
    { if (Application.isPlaying) { CacheReferences(); if (!_rigidbody.isKinematic) _rigidbody.AddForce(transform.forward * 5f, ForceMode.VelocityChange); } }
    private void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        CacheReferences(); int n = FillSamplePoints(); Gizmos.color = Color.cyan;
        for (int i = 0; i < n; ++i) Gizmos.DrawWireSphere(GetSamplePoint(i), .08f);
    }
}
