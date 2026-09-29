using UnityEngine;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(350)]
    [AddComponentMenu("Plots & Rots/Weather/Snow Footprint Emitter")]
    public sealed class SnowFootprintEmitter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SnowInteractionManager interactionManager;

        [SerializeField]
        private CharacterController characterController;

        [Tooltip("Optional animated foot bones/transforms. Leave empty to use automatic left/right offsets from the player root.")]
        [SerializeField]
        private Transform leftFoot;

        [SerializeField]
        private Transform rightFoot;

        [Header("Walking")]
        [SerializeField, Min(.1f)]
        private float stepDistance = .62f;

        [SerializeField, Min(0f)]
        private float minimumHorizontalSpeed = .12f;

        [SerializeField, Range(.05f, .6f)]
        private float lateralFootSeparation = .18f;

        [SerializeField, Range(-.2f, .4f)]
        private float fallbackForwardOffset = .05f;

        [Header("Footprint")]
        [Tooltip("X = sole width, Y = sole length in world metres.")]
        [SerializeField]
        private Vector2 footprintSize =
            new Vector2(
                .18f,
                .32f);

        [SerializeField, Range(0f, 1f)]
        private float footprintStrength = .95f;

        [SerializeField, Range(.02f, .45f)]
        private float edgeSoftness = .20f;

        [Header("Natural Footprint Variation")]
        [Tooltip("Small outward foot angle. Left/right feet mirror this value.")]
        [SerializeField, Range(0f, 10f)]
        private float toeOutAngle = 3.5f;

        [Tooltip("Speed at which the running footprint modifiers reach full strength.")]
        [SerializeField, Min(.5f)]
        private float runningReferenceSpeed = 6.5f;

        [Tooltip("Running steps become slightly farther apart instead of stamping at the exact same walking cadence.")]
        [SerializeField, Range(1f, 1.45f)]
        private float runningStepSpacingMultiplier = 1.18f;

        [Tooltip("Running stretches the sole a little in the travel direction.")]
        [SerializeField, Range(1f, 1.25f)]
        private float runningLengthMultiplier = 1.08f;

        [Tooltip("Running compresses the snow a little more strongly.")]
        [SerializeField, Range(1f, 1.2f)]
        private float runningStrengthMultiplier = 1.05f;

        [Tooltip("Tiny deterministic size difference between steps. 0.035 = about 3.5 percent.")]
        [SerializeField, Range(0f, .10f)]
        private float sizeVariation = .035f;

        [Header("Ground Detection")]
        [SerializeField]
        private LayerMask groundMask = -1;

        [SerializeField, Range(.2f, 3f)]
        private float groundRayDistance = 1.2f;

        [SerializeField, Range(0f, 1f)]
        private float minimumGroundUpNormal = .45f;

        [Tooltip("Footprints currently modify the active Unity Terrain snow shader only. Keep this enabled unless you later add prop/building footprint receivers.")]
        [SerializeField]
        private bool terrainOnly = true;

        private readonly RaycastHit[] rayHits =
            new RaycastHit[12];

        private Vector3 previousPosition;
        private float travelledDistance;
        private bool initialized;
        private bool leftNext = true;
        private int emittedStepCount;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            previousPosition =
                transform.position;

            travelledDistance = 0f;
            emittedStepCount = 0;
            initialized = true;
        }

        private void Update()
        {
            ResolveReferences();

            if (interactionManager == null
                || !interactionManager.CanStamp)
            {
                ResetTravel();
                return;
            }

            if (characterController != null
                && !characterController.isGrounded)
            {
                ResetTravel();
                return;
            }

            Vector3 currentPosition =
                transform.position;

            if (!initialized)
            {
                previousPosition = currentPosition;
                initialized = true;
                return;
            }

            Vector3 delta =
                currentPosition
                - previousPosition;

            previousPosition =
                currentPosition;

            delta.y = 0f;

            float distance =
                delta.magnitude;

            float speed =
                Time.deltaTime > .0001f
                    ? distance / Time.deltaTime
                    : 0f;

            if (speed < minimumHorizontalSpeed)
                return;

            travelledDistance +=
                distance;

            float runAmount =
                Mathf.InverseLerp(
                    2.2f,
                    Mathf.Max(2.21f, runningReferenceSpeed),
                    speed);

            float spacing =
                Mathf.Max(
                    .1f,
                    stepDistance
                    * Mathf.Lerp(
                        1f,
                        runningStepSpacingMultiplier,
                        runAmount));

            // Usually one step per frame. The small loop also keeps spacing
            // stable after a short hitch or a fast sprint.
            int safety = 0;

            while (travelledDistance >= spacing
                && safety < 3)
            {
                travelledDistance -= spacing;
                safety++;

                EmitStep(
                    leftNext,
                    speed);

                leftNext =
                    !leftNext;
            }
        }

        private void EmitStep(
            bool left,
            float horizontalSpeed)
        {
            Transform foot =
                left
                    ? leftFoot
                    : rightFoot;

            Vector3 forward =
                transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude <= .0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 right =
                new Vector3(
                    forward.z,
                    0f,
                    -forward.x);

            Vector3 samplePosition;

            if (foot != null)
            {
                samplePosition =
                    foot.position;

                Vector3 footForward =
                    foot.forward;

                footForward.y = 0f;

                if (footForward.sqrMagnitude > .0001f)
                    forward = footForward.normalized;
            }
            else
            {
                float side =
                    (left ? -1f : 1f)
                    * lateralFootSeparation
                    * .5f;

                samplePosition =
                    transform.position
                    + right * side
                    + forward * fallbackForwardOffset;
            }

            if (!TryFindTerrainSurface(
                    samplePosition,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal))
            {
                return;
            }

            Vector3 groundForward =
                Vector3.ProjectOnPlane(
                    forward,
                    hitNormal);

            if (groundForward.sqrMagnitude <= .0001f)
                groundForward = forward;
            else
                groundForward.Normalize();

            // A tiny mirrored toe-out makes left/right prints read as two feet instead
            // of identical ovals. Rotation is around the actual terrain normal so it
            // remains correct on slopes.
            float toeAngle =
                left
                    ? -toeOutAngle
                    : toeOutAngle;

            groundForward =
                Quaternion.AngleAxis(
                    toeAngle,
                    hitNormal)
                * groundForward;

            float runAmount =
                Mathf.InverseLerp(
                    2.2f,
                    Mathf.Max(2.21f, runningReferenceSpeed),
                    horizontalSpeed);

            float variation =
                SignedStepVariation(
                    emittedStepCount,
                    left);

            emittedStepCount++;

            float widthVariation =
                1f
                + variation
                * sizeVariation;

            float lengthVariation =
                1f
                + variation
                * sizeVariation
                * .55f;

            Vector2 finalSize =
                new Vector2(
                    footprintSize.x
                    * widthVariation
                    * Mathf.Lerp(1f, .98f, runAmount),

                    footprintSize.y
                    * lengthVariation
                    * Mathf.Lerp(
                        1f,
                        runningLengthMultiplier,
                        runAmount));

            float finalStrength =
                Mathf.Clamp01(
                    footprintStrength
                    * Mathf.Lerp(
                        1f,
                        runningStrengthMultiplier,
                        runAmount)
                    * (1f + variation * .025f));

            interactionManager.StampFootprint(
                hitPoint,
                groundForward,
                finalSize,
                finalStrength,
                edgeSoftness,
                left,
                variation);
        }

        private static float SignedStepVariation(
            int stepIndex,
            bool left)
        {
            // Deterministic pseudo-random variation. It deliberately avoids UnityEngine.Random
            // so footprint rendering cannot perturb gameplay random state.
            float seed =
                stepIndex * 12.9898f
                + (left ? 31.416f : 78.233f);

            return
                Mathf.Sin(seed)
                * .5f
                + Mathf.Sin(seed * .37f + 1.7f)
                * .5f;
        }

        private bool TryFindTerrainSurface(
            Vector3 samplePosition,
            out Vector3 hitPoint,
            out Vector3 hitNormal)
        {
            hitPoint = default;
            hitNormal = Vector3.up;

            float startY;
            float castDistance;

            if (characterController != null)
            {
                Bounds bounds =
                    characterController.bounds;

                startY =
                    bounds.min.y
                    + .55f;

                castDistance =
                    Mathf.Max(
                        groundRayDistance,
                        .75f);
            }
            else
            {
                startY =
                    samplePosition.y
                    + .55f;

                castDistance =
                    Mathf.Max(
                        groundRayDistance,
                        1f);
            }

            Vector3 origin =
                new Vector3(
                    samplePosition.x,
                    startY,
                    samplePosition.z);

            int hitCount =
                Physics.RaycastNonAlloc(
                    origin,
                    Vector3.down,
                    rayHits,
                    castDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore);

            float bestDistance =
                float.PositiveInfinity;

            RaycastHit bestHit =
                default;

            bool found = false;

            for (int i = 0;
                 i < hitCount;
                 i++)
            {
                RaycastHit hit =
                    rayHits[i];

                if (hit.collider == null)
                    continue;

                Transform hitTransform =
                    hit.collider.transform;

                if (hitTransform == transform
                    || hitTransform.IsChildOf(transform)
                    || transform.IsChildOf(hitTransform))
                {
                    continue;
                }

                if (terrainOnly
                    && !(hit.collider is TerrainCollider))
                {
                    continue;
                }

                if (hit.normal.y < minimumGroundUpNormal)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            if (!found)
                return false;

            hitPoint =
                bestHit.point;

            hitNormal =
                bestHit.normal.sqrMagnitude > .0001f
                    ? bestHit.normal.normalized
                    : Vector3.up;

            return true;
        }

        private void ResolveReferences()
        {
            if (interactionManager == null)
            {
                interactionManager =
                    SnowInteractionManager.Instance;
            }

            if (characterController == null)
            {
                characterController =
                    GetComponent<CharacterController>();

                if (characterController == null)
                {
                    characterController =
                        GetComponentInParent<CharacterController>();
                }
            }
        }

        private void ResetTravel()
        {
            previousPosition =
                transform.position;

            travelledDistance = 0f;
            initialized = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stepDistance =
                Mathf.Max(
                    .1f,
                    stepDistance);

            footprintSize.x =
                Mathf.Max(
                    .04f,
                    footprintSize.x);

            footprintSize.y =
                Mathf.Max(
                    .06f,
                    footprintSize.y);

            runningReferenceSpeed =
                Mathf.Max(
                    .5f,
                    runningReferenceSpeed);
        }
#endif
    }
}
