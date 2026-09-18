using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider), typeof(WaterSurface))]
public sealed class WaterPhysicsVolume : MonoBehaviour
{
    [Min(.1f)] public float waterDepth = 25;
    [Min(0)] public float horizontalPadding = 2, topPadding = .5f;
    public bool autoFitTrigger = true;
    private BoxCollider _trigger;
    private MeshRenderer _renderer;
    private WaterSurface _surface;
    private readonly Dictionary<Collider, BuoyantObject> _contacts = new Dictionary<Collider, BuoyantObject>();
    private readonly List<Collider> _stale = new List<Collider>();
    public WaterSurface Surface => _surface;
    private void OnEnable() { Cache(); ConfigureTrigger(); }
    private void OnValidate() { waterDepth = Mathf.Max(.1f, waterDepth); Cache(); ConfigureTrigger(); }
    private void Cache() { _trigger = GetComponent<BoxCollider>(); _renderer = GetComponent<MeshRenderer>(); _surface = GetComponent<WaterSurface>(); }
    private void LateUpdate() { if (!Application.isPlaying && autoFitTrigger) ConfigureTrigger(); }
    public void ConfigureTrigger()
    {
        if (_trigger == null) return;
        _trigger.isTrigger = true;
        if (!autoFitTrigger || _renderer == null || _surface == null) return;
        Vector3 s = transform.lossyScale;
        // A zero scale cannot be repaired with division by epsilon: the actual collider remains flat.
        if (Mathf.Min(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) < .0001f) return;
        Bounds b = _renderer.bounds;
        float h = waterDepth + _surface.MaxWaveHeight + topPadding;
        Vector3 c = new Vector3(b.center.x, _surface.BaseWaterLevel - waterDepth + h*.5f, b.center.z);
        _trigger.center = transform.InverseTransformPoint(c);
        _trigger.size = new Vector3((b.size.x + horizontalPadding*2)/Mathf.Abs(s.x), h/Mathf.Abs(s.y), (b.size.z + horizontalPadding*2)/Mathf.Abs(s.z));
    }
    private void OnTriggerEnter(Collider c) { Register(c); }
    private void OnTriggerStay(Collider c) { Register(c); }
    private void OnTriggerExit(Collider c) { Unregister(c); }
    private void Register(Collider c)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _surface == null || !_surface.isActiveAndEnabled || c.isTrigger) return;
        if (_contacts.TryGetValue(c, out BuoyantObject existing))
        { if (existing != null && existing.isActiveAndEnabled) existing.SetWaterSurface(_surface); return; }
        Rigidbody rb = c.attachedRigidbody;
        if (rb == null) return;
        BuoyantObject body = rb.GetComponent<BuoyantObject>();
        if (body == null || !body.isActiveAndEnabled) return;
        _contacts.Add(c, body); body.SetWaterSurface(_surface);
    }
    private void Unregister(Collider c)
    {
        if (!_contacts.TryGetValue(c, out BuoyantObject body)) return;
        _contacts.Remove(c);
        // One hull collider leaving must not clear the other hull colliders' contact.
        foreach (BuoyantObject remaining in _contacts.Values) if (remaining == body) return;
        if (body != null) body.ClearWaterSurface(_surface);
    }
    private void FixedUpdate()
    {
        _stale.Clear();
        foreach (var entry in _contacts)
            if (entry.Key == null || !entry.Key.enabled || !entry.Key.gameObject.activeInHierarchy || entry.Value == null || !entry.Value.isActiveAndEnabled || _trigger == null || !_trigger.enabled)
                _stale.Add(entry.Key);
        foreach (Collider c in _stale) Unregister(c);
    }
    private void OnDisable()
    { foreach (BuoyantObject body in _contacts.Values) if (body != null) body.ClearWaterSurface(_surface); _contacts.Clear(); }
}
