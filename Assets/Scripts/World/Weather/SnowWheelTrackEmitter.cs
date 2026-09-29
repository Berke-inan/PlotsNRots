using UnityEngine;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(360)]
    [AddComponentMenu("Plots & Rots/Weather/Snow Wheel Track Emitter")]
    public sealed class SnowWheelTrackEmitter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SnowInteractionManager interactionManager;

        [Tooltip("Leave empty to automatically use every WheelCollider below this vehicle root.")]
        [SerializeField]
        private WheelCollider[] wheels;

        [Header("Track")]
        [Tooltip("Approximate tire contact width in world metres.")]
        [SerializeField, Range(.08f, .8f)]
        private float trackWidth = .24f;

        [Tooltip("Length of each GPU stamp. Nearby stamps overlap to form a continuous rut.")]
        [SerializeField, Range(.08f, 1.2f)]
        private float stampLength = .34f;

        [SerializeField, Range(.03f, .8f)]
        private float stampSpacing = .14f;

        [SerializeField, Range(0f, 1f)]
        private float trackStrength = .82f;

        [SerializeField, Range(.02f, .45f)]
        private float edgeSoftness = .13f;

        [Header("Tread Pattern")]
        [Tooltip("Creates a real repeating tire tread instead of a smooth rectangular rut.")]
        [SerializeField]
        private bool patternedTread = true;

        [Tooltip("Distance in metres between tread rows. Larger values create chunkier off-road/tractor-like lugs.")]
        [SerializeField, Range(.08f, .35f)]
        private float treadRepeat = .18f;

        [Tooltip("0 = nearly smooth tire rut, 1 = very clear tread blocks and grooves.")]
        [SerializeField, Range(0f, 1f)]
        private float treadContrast = .82f;

        [Tooltip("Normalized width of the centre groove left slightly less compressed.")]
        [SerializeField, Range(0f, .28f)]
        private float centerGrooveWidth = .10f;

        [Tooltip("0 = straight cross blocks, 1 = stronger V/chevron pattern.")]
        [SerializeField, Range(0f, 1f)]
        private float chevronAmount = .40f;

        [Header("Driving")]
        [SerializeField, Min(0f)]
        private float minimumHorizontalSpeed = .35f;

        [SerializeField, Range(1, 12)]
        private int maximumStampsPerWheelPerFrame = 6;

        [Header("Ground")]
        [SerializeField]
        private bool terrainOnly = true;

        [SerializeField, Range(0f, 1f)]
        private float minimumGroundUpNormal = .35f;

        private Vector3 previousRootPosition;
        private bool movementInitialized;

        private Vector3[] previousContacts;
        private float[] distanceAccumulators;
        private bool[] contactInitialized;

        private void Awake()
        {
            ResolveReferences();
            RebuildWheelState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RebuildWheelState();

            previousRootPosition =
                transform.position;

            movementInitialized =
                true;
        }

        private void Update()
        {
            ResolveReferences();

            if (interactionManager == null
                || !interactionManager.CanStamp
                || wheels == null
                || wheels.Length == 0)
            {
                ResetContacts();
                CacheRootPosition();
                return;
            }

            Vector3 currentRootPosition =
                transform.position;

            if (!movementInitialized)
            {
                previousRootPosition =
                    currentRootPosition;

                movementInitialized =
                    true;

                return;
            }

            Vector3 rootDelta =
                currentRootPosition
                - previousRootPosition;

            previousRootPosition =
                currentRootPosition;

            rootDelta.y = 0f;

            float speed =
                Time.deltaTime > .0001f
                    ? rootDelta.magnitude / Time.deltaTime
                    : 0f;

            if (speed < minimumHorizontalSpeed)
            {
                ResetContacts();
                return;
            }

            EnsureWheelStateMatches();

            for (int i = 0;
                 i < wheels.Length;
                 i++)
            {
                ProcessWheel(
                    i,
                    wheels[i]);
            }
        }

        private void ProcessWheel(
            int index,
            WheelCollider wheel)
        {
            if (wheel == null
                || !wheel.enabled
                || !wheel.gameObject.activeInHierarchy)
            {
                ResetContact(index);
                return;
            }

            if (!wheel.GetGroundHit(
                    out WheelHit hit)
                || hit.collider == null)
            {
                ResetContact(index);
                return;
            }

            if (terrainOnly
                && !(hit.collider is TerrainCollider))
            {
                ResetContact(index);
                return;
            }

            Vector3 normal =
                hit.normal.sqrMagnitude > .0001f
                    ? hit.normal.normalized
                    : Vector3.up;

            if (normal.y < minimumGroundUpNormal)
            {
                ResetContact(index);
                return;
            }

            Vector3 contact =
                hit.point;

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    wheel.transform.forward,
                    normal);

            if (forward.sqrMagnitude <= .0001f)
            {
                forward =
                    Vector3.ProjectOnPlane(
                        transform.forward,
                        normal);
            }

            if (forward.sqrMagnitude <= .0001f)
                forward = transform.forward;
            else
                forward.Normalize();

            if (!contactInitialized[index])
            {
                previousContacts[index] = contact;
                distanceAccumulators[index] = 0f;
                contactInitialized[index] = true;

                Stamp(
                    contact,
                    forward);

                return;
            }

            Vector3 previous =
                previousContacts[index];

            Vector3 delta =
                contact
                - previous;

            float distance =
                delta.magnitude;

            previousContacts[index] =
                contact;

            if (distance <= .0001f)
                return;

            float spacing =
                Mathf.Max(
                    .03f,
                    stampSpacing);

            float previousAccumulator =
                distanceAccumulators[index];

            float travelled =
                previousAccumulator
                + distance;

            int stampCount = 0;

            while (travelled >= spacing
                && stampCount < maximumStampsPerWheelPerFrame)
            {
                float distanceIntoSegment =
                    spacing
                    - previousAccumulator;

                float t =
                    Mathf.Clamp01(
                        distanceIntoSegment
                        /
                        Mathf.Max(
                            .0001f,
                            distance));

                Vector3 stampPosition =
                    Vector3.Lerp(
                        previous,
                        contact,
                        t);

                Stamp(
                    stampPosition,
                    forward);

                stampCount++;

                travelled -= spacing;
                previousAccumulator = 0f;
                previous = stampPosition;
                distance = Vector3.Distance(previous, contact);

                if (distance <= .0001f)
                    break;
            }

            distanceAccumulators[index] =
                Mathf.Repeat(
                    travelled,
                    spacing);
        }

        private void Stamp(
            Vector3 position,
            Vector3 forward)
        {
            interactionManager.StampTrack(
                position,
                forward,
                new Vector2(
                    trackWidth,
                    stampLength),
                trackStrength,
                edgeSoftness,
                patternedTread,
                treadRepeat,
                treadContrast,
                centerGrooveWidth,
                chevronAmount);
        }

        private void ResolveReferences()
        {
            if (interactionManager == null)
            {
                interactionManager =
                    SnowInteractionManager.Instance;
            }

            if (wheels == null
                || wheels.Length == 0)
            {
                wheels =
                    GetComponentsInChildren<WheelCollider>(
                        true);
            }
        }

        private void EnsureWheelStateMatches()
        {
            if (previousContacts == null
                || distanceAccumulators == null
                || contactInitialized == null
                || previousContacts.Length != wheels.Length)
            {
                RebuildWheelState();
            }
        }

        private void RebuildWheelState()
        {
            int count =
                wheels != null
                    ? wheels.Length
                    : 0;

            previousContacts =
                new Vector3[count];

            distanceAccumulators =
                new float[count];

            contactInitialized =
                new bool[count];
        }

        private void ResetContacts()
        {
            if (contactInitialized == null)
                return;

            for (int i = 0;
                 i < contactInitialized.Length;
                 i++)
            {
                ResetContact(i);
            }
        }

        private void ResetContact(
            int index)
        {
            if (contactInitialized == null
                || index < 0
                || index >= contactInitialized.Length)
            {
                return;
            }

            contactInitialized[index] = false;
            distanceAccumulators[index] = 0f;
        }

        private void CacheRootPosition()
        {
            previousRootPosition =
                transform.position;

            movementInitialized =
                true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            trackWidth =
                Mathf.Max(
                    .08f,
                    trackWidth);

            stampLength =
                Mathf.Max(
                    .08f,
                    stampLength);

            stampSpacing =
                Mathf.Clamp(
                    stampSpacing,
                    .03f,
                    .8f);

            maximumStampsPerWheelPerFrame =
                Mathf.Clamp(
                    maximumStampsPerWheelPerFrame,
                    1,
                    12);


            treadRepeat =
                Mathf.Clamp(
                    treadRepeat,
                    .08f,
                    .35f);

            treadContrast =
                Mathf.Clamp01(
                    treadContrast);

            centerGrooveWidth =
                Mathf.Clamp(
                    centerGrooveWidth,
                    0f,
                    .28f);

            chevronAmount =
                Mathf.Clamp01(
                    chevronAmount);
        }
#endif
    }
}
