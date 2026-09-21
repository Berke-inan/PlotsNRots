using UnityEngine;

// Follows the persistent rendered gameplay camera after Cinemachine has blended.
// The rig follows position only; rain/snow keep world-space direction.
// A small movement prediction keeps the precipitation volume in front of fast vehicles.
[DisallowMultipleComponent, DefaultExecutionOrder(10000)]
public sealed class PrecipitationFollower : MonoBehaviour
{
    [SerializeField, Tooltip("Persistent output camera, not a Cinemachine virtual camera.")]
    private Transform gameplayCamera;

    [Header("Fast vehicle support")]
    [SerializeField, Min(0f), Tooltip("How far into the camera movement to lead the precipitation rig.")]
    private float movementLeadTime = 0.45f;

    [SerializeField, Min(0f), Tooltip("Maximum horizontal lead so camera cuts/teleports cannot throw the rig far away.")]
    private float maximumLeadDistance = 24f;

    [SerializeField, Min(0f), Tooltip("Higher values react faster to acceleration while still filtering camera jitter.")]
    private float velocitySmoothing = 8f;

    [SerializeField, Min(1f), Tooltip("Larger jumps are treated as camera cuts/teleports instead of real velocity.")]
    private float teleportResetDistance = 30f;

    private Vector3 previousCameraPosition;
    private Vector3 smoothedVelocity;
    private bool initialized;

    private void OnEnable()
    {
        initialized = false;
        FollowNow();
    }

    private void LateUpdate() => FollowNow();

    public void FollowNow()
    {
        if (gameplayCamera == null) return;

        Vector3 cameraPosition = gameplayCamera.position;
        float dt = Time.deltaTime;

        if (!initialized || dt <= 0f)
        {
            initialized = true;
            previousCameraPosition = cameraPosition;
            smoothedVelocity = Vector3.zero;
            transform.position = cameraPosition;
            return;
        }

        Vector3 delta = cameraPosition - previousCameraPosition;
        previousCameraPosition = cameraPosition;

        // Camera cuts or teleport-like moves should snap the weather rig, not create a huge lead.
        if (delta.sqrMagnitude > teleportResetDistance * teleportResetDistance)
        {
            smoothedVelocity = Vector3.zero;
            transform.position = cameraPosition;
            return;
        }

        Vector3 rawVelocity = delta / Mathf.Max(0.0001f, dt);
        rawVelocity.y = 0f; // Only predict horizontal vehicle/player travel.

        float blend = 1f - Mathf.Exp(-velocitySmoothing * dt);
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, blend);

        Vector3 lead = smoothedVelocity * movementLeadTime;
        if (lead.sqrMagnitude > maximumLeadDistance * maximumLeadDistance)
            lead = lead.normalized * maximumLeadDistance;

        transform.position = cameraPosition + lead;

        // Intentionally do not copy camera rotation.
        // The particle systems use world-space velocity, so "down" always remains world -Y.
    }
}
