using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class ChickenShelterController : MonoBehaviour
{
    private static readonly List<ChickenShelterController> ActiveChickens =
        new List<ChickenShelterController>();

    [Header("References")]
    [SerializeField] private AnimalShelter shelter;
    [SerializeField] private DayNightCycleManager dayNightCycleManager;

    [Header("Daily Schedule - Game Hours")]
    [SerializeField, Range(0f, 24f)] private float wakeTime = 6f;
    [SerializeField, Range(0f, 24f)] private float firstExitWindowStart = 8f;
    [SerializeField, Range(0f, 24f)] private float firstExitWindowEnd = 9f;
    [SerializeField, Range(0f, 24f)] private float returnWindowStart = 20f;
    [SerializeField, Range(0f, 24f)] private float returnWindowEnd = 21f;
    [SerializeField, Range(0f, 24f)] private float sleepTime = 22f;

    [Header("Independent Random Behaviour")]
    [Tooltip("Chance that an indoor chicken randomly chooses to go outside after completing an action.")]
    [SerializeField, Range(0f, 1f)] private float indoorExitChance = 0.35f;
    [Tooltip("Chance that an outdoor chicken randomly chooses to enter the coop after completing an action.")]
    [SerializeField, Range(0f, 1f)] private float outdoorEnterChance = 0.25f;

    [Header("Movement")]
    [SerializeField, Min(0.05f)] private float arrivalDistance = 0.15f;
    [SerializeField, Min(1f)] private float movementTimeout = 20f;
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 1.5f;

    [Header("Doorway Clearing")]
    [SerializeField, Min(0.1f)] private float doorwayClearDistance = 1.25f;
    [SerializeField, Min(0.02f)]
    private float doorwayClearArrivalDistance = 0.1f;

    [Header("Roaming")]
    [SerializeField, Min(0.25f)] private float outdoorRoamRadius = 3f;
    [SerializeField, Range(0f, 1f)] private float outdoorWanderChance = 0.75f;
    [SerializeField, Min(0f)] private float minimumIdleDuration = 2f;
    [SerializeField, Min(0f)] private float maximumIdleDuration = 5f;
    [SerializeField, Min(1)] private int destinationAttempts = 15;

    [Header("Indoor Roaming")]
    [SerializeField, Range(0f, 1f)] private float indoorPauseChance = 0.2f;
    [SerializeField, Min(0f)] private float minimumIndoorPauseDuration = 0.5f;
    [SerializeField, Min(0f)] private float maximumIndoorPauseDuration = 1.5f;
    [SerializeField, Min(0f)] private float minimumIndoorMoveDistance = 0.75f;

    [Header("Independent Behaviour - Real Seconds")]
    [Tooltip("Random one-time delay that prevents duplicated chickens from starting their behaviour loops together.")]
    [SerializeField, Min(0f)] private float maximumInitialActionOffset = 2f;
    [SerializeField, Min(0f)] private float minimumDecisionDelay = 0.25f;
    [SerializeField, Min(0f)] private float maximumDecisionDelay = 1.25f;

    [Header("Sleeping")]
    [SerializeField, Range(0f, 45f)] private float maximumSleepSlope = 8f;
    [SerializeField, Min(0f)] private float restAreaEdgePadding = 0.35f;
    [SerializeField, Min(0.1f)] private float minimumChickenSeparation = 0.8f;

    private NavMeshAgent agent;
    private NavMeshPath reusablePath;
    private Coroutine schedule;

    private bool initialized;
    private bool moveSucceeded;
    private string moveFailure;
    private bool isSleeping;
    private bool isOutside;
    private bool mustWanderAfterEntry;
    private bool dailyScheduleGenerated;
    private bool firstExitCompleted;
    private float firstExitTimeToday;
    private float returnTimeToday;
    private float previousClockTime = -1f;
    private bool missingNavMeshReported;
    private bool initialActionOffsetPending;
    private System.Random individualRandom;

    public bool IsSleeping => isSleeping;
    public bool IsOutside => isOutside;
    public bool IsInsideShelter => !isOutside;
    public float FirstExitTimeToday => firstExitTimeToday;
    public float ReturnTimeToday => returnTimeToday;

    private float CurrentTime => dayNightCycleManager == null
        ? 0f
        : Mathf.Repeat(dayNightCycleManager.currentTime, 24f);

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        reusablePath = new NavMeshPath();
        int seed = unchecked(
            (GetInstanceID() * 397) ^ System.Environment.TickCount);
        individualRandom = new System.Random(seed);
    }

    private void OnEnable()
    {
        if (!ActiveChickens.Contains(this))
        {
            ActiveChickens.Add(this);
        }

        if (initialized && schedule == null)
        {
            schedule = StartCoroutine(DailySchedule());
        }
    }

    private void OnDisable()
    {
        if (schedule != null)
        {
            StopCoroutine(schedule);
            schedule = null;
        }

        StopMoving();
        ActiveChickens.Remove(this);
    }

    private IEnumerator Start()
    {
        yield return null;
        ResolveReferences();

        if (dayNightCycleManager == null)
        {
            Debug.LogError(
                "No DayNightCycleManager was found; the chicken schedule " +
                "cannot run without the game clock.",
                this);
            yield break;
        }

        initialized = true;
        schedule = StartCoroutine(DailySchedule());
    }

    private void ResolveReferences()
    {
        if (dayNightCycleManager == null)
        {
            dayNightCycleManager = DayNightCycleManager.Instance != null
                ? DayNightCycleManager.Instance
                : FindFirstObjectByType<DayNightCycleManager>();
        }

        if (shelter == null)
        {
            shelter = FindNearestShelter();
        }
    }

    private AnimalShelter FindNearestShelter()
    {
        AnimalShelter nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (AnimalShelter candidate in
                 FindObjectsByType<AnimalShelter>(FindObjectsSortMode.None))
        {
            if (candidate.Species != AnimalSpecies.Chicken ||
                candidate.OutsideApproachPoint == null)
            {
                continue;
            }

            float distance = (
                candidate.OutsideApproachPoint.position -
                transform.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private IEnumerator DailySchedule()
    {
        while (enabled)
        {
            float now = CurrentTime;
            ObserveClock(now);

            if (IsSleepPeriod(now))
            {
                if (!isSleeping)
                {
                    Sleep();
                }

                yield return null;
                continue;
            }

            if (!dailyScheduleGenerated)
            {
                GenerateSchedule(now);
            }

            if (isSleeping)
            {
                WakeUp();
            }

            if (!HasValidShelter())
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (!agent.isOnNavMesh)
            {
                StopMoving();

                if (!missingNavMeshReported)
                {
                    Debug.LogError(
                        "The chicken is not positioned on the Chicken NavMesh.",
                        this);
                    missingNavMeshReported = true;
                }

                yield return null;
                continue;
            }

            missingNavMeshReported = false;

            if (now < firstExitTimeToday)
            {
                if (isOutside)
                {
                    yield return EnterShelter(firstExitTimeToday);
                }

                if (!isOutside)
                {
                    yield return PerformAction(false, firstExitTimeToday);
                }
            }
            else if (now < returnTimeToday)
            {
                if (!firstExitCompleted)
                {
                    if (isOutside)
                    {
                        firstExitCompleted = true;
                    }
                    else
                    {
                        yield return ExitShelter(returnTimeToday);
                        firstExitCompleted = moveSucceeded && isOutside;
                    }
                }
                else
                {
                    yield return PerformRandomDaytimeAction(returnTimeToday);
                }
            }
            else
            {
                yield return ReturnAndStayInside();
            }
        }

        schedule = null;
    }

    private bool HasValidShelter()
    {
        if (shelter == null)
        {
            shelter = FindNearestShelter();
        }

        if (shelter != null)
        {
            return true;
        }

        StopMoving();
        return false;
    }

    private void ObserveClock(float now)
    {
        if (previousClockTime >= 0f && now + 0.01f < previousClockTime)
        {
            dailyScheduleGenerated = false;
        }

        previousClockTime = now;
    }

    private void GenerateSchedule(float now)
    {
        float exitStart = Mathf.Clamp(
            firstExitWindowStart,
            wakeTime,
            returnWindowStart);
        float exitEnd = Mathf.Clamp(
            firstExitWindowEnd,
            exitStart,
            returnWindowStart);
        float returnStart = Mathf.Clamp(
            returnWindowStart,
            exitEnd,
            sleepTime);
        float returnEnd = Mathf.Clamp(
            returnWindowEnd,
            returnStart,
            sleepTime);

        firstExitTimeToday = NextFloat(exitStart, exitEnd);
        returnTimeToday = NextFloat(returnStart, returnEnd);
        isOutside = !IsPositionInsideShelter(transform.position);
        firstExitCompleted = isOutside && now >= firstExitTimeToday;
        mustWanderAfterEntry = false;
        initialActionOffsetPending = true;
        dailyScheduleGenerated = true;

        Debug.Log(
            $"Chicken schedule: first exit {firstExitTimeToday:0.00}, " +
            $"return {returnTimeToday:0.00}, sleep {sleepTime:0.00}.",
            this);
    }

    private IEnumerator PerformRandomDaytimeAction(float cutoff)
    {
        bool crosses = NextFloat01() <
            (isOutside ? outdoorEnterChance : indoorExitChance);

        if (crosses)
        {
            yield return isOutside
                ? EnterShelter(cutoff)
                : ExitShelter(cutoff);

            // Whether the crossing worked or not, this chicken waits for its
            // own random real-time interval before making another choice.
            yield return WaitUntil(
                NextFloat(minimumDecisionDelay, maximumDecisionDelay),
                cutoff);
            yield break;
        }

        yield return PerformAction(isOutside, cutoff);
    }

    private IEnumerator EnterShelter(float cutoff)
    {
        moveSucceeded = false;

        if (!HasEntrancePoints())
        {
            yield break;
        }

        yield return MoveTo(
            shelter.OutsideApproachPoint.position,
            shelter.OutsideApproachPoint.name,
            cutoff);

        if (!moveSucceeded)
        {
            yield break;
        }

        yield return MoveTo(
            shelter.InsideEntryPoint.position,
            shelter.InsideEntryPoint.name,
            cutoff);

        if (!moveSucceeded)
        {
            yield break;
        }

        isOutside = false;

        if (TryFindEntryClearDestination(out Vector3 destination))
        {
            // This short safety movement must finish even if the game clock
            // reaches a schedule boundary. MoveTo still enforces its real-time
            // timeout, so it cannot hold the doorway indefinitely.
            yield return MoveTo(
                destination,
                "indoor entry-clear destination",
                null,
                doorwayClearArrivalDistance);

            if (!moveSucceeded)
            {
                Debug.LogWarning(
                    "The chicken entered the coop but could not clear the " +
                    $"doorway because {moveFailure}.",
                    this);
            }
        }
        else
        {
            Debug.LogWarning(
                "The chicken entered the coop, but RestArea contains no " +
                "reachable, unoccupied doorway-clearing point.",
                this);
        }

        mustWanderAfterEntry = true;
        moveSucceeded = true;
    }

    private IEnumerator ExitShelter(float cutoff)
    {
        moveSucceeded = false;

        if (!HasEntrancePoints())
        {
            yield break;
        }

        yield return MoveTo(
            shelter.InsideEntryPoint.position,
            shelter.InsideEntryPoint.name,
            cutoff);

        if (moveSucceeded)
        {
            yield return MoveTo(
                shelter.OutsideApproachPoint.position,
                shelter.OutsideApproachPoint.name,
                cutoff);
        }

        if (moveSucceeded)
        {
            isOutside = true;
            mustWanderAfterEntry = false;
        }
    }

    private bool HasEntrancePoints()
    {
        if (shelter != null &&
            shelter.OutsideApproachPoint != null &&
            shelter.InsideEntryPoint != null)
        {
            return true;
        }

        Debug.LogError(
            "The shelter is missing an entrance navigation point.",
            shelter);
        return false;
    }

    private IEnumerator PerformAction(bool outside, float cutoff)
    {
        if (initialActionOffsetPending)
        {
            initialActionOffsetPending = false;
            yield return WaitUntil(
                NextFloat(0f, maximumInitialActionOffset),
                cutoff);
        }

        Vector3 destination = transform.position;
        bool shouldMove = outside
            ? NextFloat01() <= outdoorWanderChance
            : mustWanderAfterEntry || NextFloat01() > indoorPauseChance;
        bool foundDestination = shouldMove &&
            (outside
                ? TryFindOutdoorDestination(out destination)
                : TryFindRestDestination(false, out destination));

        if (foundDestination)
        {
            yield return MoveTo(
                destination,
                outside
                    ? "outdoor roaming destination"
                    : "indoor roaming destination",
                cutoff);

            if (!outside && moveSucceeded)
            {
                mustWanderAfterEntry = false;
            }

            if (moveSucceeded)
            {
                yield return WaitUntil(
                    NextFloat(minimumDecisionDelay, maximumDecisionDelay),
                    cutoff);
            }

            yield break;
        }

        StopMoving();
        float minimumPause = outside
            ? minimumIdleDuration
            : minimumIndoorPauseDuration;
        float maximumPause = outside
            ? maximumIdleDuration
            : maximumIndoorPauseDuration;
        float duration = NextFloat(
            Mathf.Min(minimumPause, maximumPause),
            Mathf.Max(minimumPause, maximumPause));
        yield return WaitUntil(duration, cutoff);
    }

    private IEnumerator ReturnAndStayInside()
    {
        if (isOutside)
        {
            yield return EnterShelter(sleepTime);

            if (isOutside)
            {
                yield break;
            }
        }

        // Evening return is the only location constraint. Once inside, every
        // chicken continues making independent random roaming/idle choices
        // until the shared sleep time stops it wherever it happens to be.
        yield return PerformAction(false, sleepTime);
    }

    private void Sleep()
    {
        StopMoving();
        isSleeping = true;
    }

    private void WakeUp()
    {
        isSleeping = false;
    }

    private bool TryFindOutdoorDestination(out Vector3 destination)
    {
        destination = transform.position;

        if (shelter.RoamAnchor == null)
        {
            Debug.LogError("The shelter is missing its RoamAnchor.", shelter);
            return false;
        }

        Vector3 anchor = shelter.RoamAnchor.position;
        float radiusSquared = outdoorRoamRadius * outdoorRoamRadius;

        for (int i = 0; i < destinationAttempts; i++)
        {
            Vector2 offset = NextInsideUnitCircle() * outdoorRoamRadius;
            Vector3 candidate = anchor + new Vector3(offset.x, 0f, offset.y);

            if (!TryGetReachablePoint(candidate, out Vector3 sampled))
            {
                continue;
            }

            Vector3 fromAnchor = sampled - anchor;
            fromAnchor.y = 0f;

            if (fromAnchor.sqrMagnitude <= radiusSquared &&
                !IsPositionInsideShelter(sampled))
            {
                destination = sampled;
                return true;
            }
        }

        return false;
    }

    private bool TryFindEntryClearDestination(out Vector3 destination)
    {
        destination = transform.position;

        if (!TryGetRestAreaBounds(
                out BoxCollider area,
                out float halfWidth,
                out _,
                out float halfDepth) ||
            shelter.InsideEntryPoint == null)
        {
            return false;
        }

        Vector3 entry = shelter.InsideEntryPoint.position;
        Vector3 inward = shelter.OutsideApproachPoint == null
            ? area.bounds.center - entry
            : entry - shelter.OutsideApproachPoint.position;
        inward.y = 0f;

        if (inward.sqrMagnitude < 0.01f)
        {
            inward = area.bounds.center - entry;
            inward.y = 0f;
        }

        if (inward.sqrMagnitude < 0.01f)
        {
            inward = transform.forward;
            inward.y = 0f;
        }

        inward.Normalize();

        float requiredDepth = Mathf.Max(
            doorwayClearDistance,
            arrivalDistance + agent.radius + doorwayClearArrivalDistance);
        float minimumDepth = Mathf.Max(
            agent.radius + doorwayClearArrivalDistance,
            0.35f);

        bool foundFallback = false;
        Vector3 fallback = destination;
        float desiredDepth = NextFloat(minimumDepth, requiredDepth);
        float bestScore = float.PositiveInfinity;
        const int samplesPerAxis = 7;

        for (int x = 0; x < samplesPerAxis; x++)
        {
            float localX = Mathf.Lerp(
                -halfWidth,
                halfWidth,
                x / (float)(samplesPerAxis - 1));

            for (int z = 0; z < samplesPerAxis; z++)
            {
                float localZ = Mathf.Lerp(
                    -halfDepth,
                    halfDepth,
                    z / (float)(samplesPerAxis - 1));
                Vector3 candidate = RestAreaPointAtWorldHeight(
                    area,
                    localX,
                    localZ,
                    entry.y);

                if (!TryGetReachablePoint(candidate, out Vector3 sampled) ||
                    !IsInsideBoxXZ(area, sampled) ||
                    !IsEntryPositionAvailable(sampled))
                {
                    continue;
                }

                Vector3 offset = sampled - entry;
                offset.y = 0f;
                float depth = Vector3.Dot(offset, inward);

                if (depth < minimumDepth)
                {
                    continue;
                }

                float lateralVariation = NextFloat(0f, 0.2f);
                float score = Mathf.Abs(depth - desiredDepth) +
                              lateralVariation;

                if (score < bestScore)
                {
                    bestScore = score;
                    fallback = sampled;
                    foundFallback = true;
                }
            }
        }

        destination = fallback;
        return foundFallback;
    }

    private bool TryFindRestDestination(
        bool sleeping,
        out Vector3 destination)
    {
        destination = transform.position;

        if (!TryGetRestAreaBounds(
                out BoxCollider area,
                out _,
                out _,
                out _))
        {
            return false;
        }

        // RestArea can contain stacked walkable surfaces (the lower coop floor
        // and the raised structure). Sampling arbitrary points through its 3D
        // volume mostly samples empty air and makes SamplePosition repeatedly
        // snap to the same nearby patch. Sample the actual NavMesh triangles
        // instead, so both elevations have a genuine chance of being chosen.
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        List<int> eligibleTriangles = new List<int>();
        List<float> cumulativeAreas = new List<float>();
        float totalArea = 0f;

        for (int triangle = 0;
             triangle + 2 < triangulation.indices.Length;
             triangle += 3)
        {
            int triangleNumber = triangle / 3;

            if (triangleNumber < triangulation.areas.Length)
            {
                int areaIndex = triangulation.areas[triangleNumber];

                if ((agent.areaMask & (1 << areaIndex)) == 0)
                {
                    continue;
                }
            }

            Vector3 a = triangulation.vertices[triangulation.indices[triangle]];
            Vector3 b = triangulation.vertices[triangulation.indices[triangle + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[triangle + 2]];
            Vector3 centre = (a + b + c) / 3f;

            if (!IsInsideBox(area, centre) &&
                !IsInsideBox(area, a) &&
                !IsInsideBox(area, b) &&
                !IsInsideBox(area, c))
            {
                continue;
            }

            float triangleArea = Vector3.Cross(b - a, c - a).magnitude * 0.5f;

            if (triangleArea <= 0.0001f)
            {
                continue;
            }

            totalArea += triangleArea;
            eligibleTriangles.Add(triangle);
            cumulativeAreas.Add(totalArea);
        }

        if (eligibleTriangles.Count == 0)
        {
            return false;
        }

        for (int attempt = 0; attempt < destinationAttempts; attempt++)
        {
            float selection = NextFloat(0f, totalArea);
            int selected = 0;

            while (selected < cumulativeAreas.Count - 1 &&
                   selection > cumulativeAreas[selected])
            {
                selected++;
            }

            int triangle = eligibleTriangles[selected];
            Vector3 a = triangulation.vertices[triangulation.indices[triangle]];
            Vector3 b = triangulation.vertices[triangulation.indices[triangle + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[triangle + 2]];
            float u = Mathf.Sqrt(NextFloat01());
            float v = NextFloat01();
            Vector3 candidate =
                (1f - u) * a + u * (1f - v) * b + u * v * c;

            if (!IsInsideBox(area, candidate) ||
                !TryGetReachablePoint(candidate, out Vector3 sampled) ||
                !IsInsideBox(area, sampled))
            {
                continue;
            }

            if (!sleeping)
            {
                Vector3 movement = sampled - transform.position;

                if (movement.sqrMagnitude <
                    minimumIndoorMoveDistance * minimumIndoorMoveDistance)
                {
                    continue;
                }
            }

            if (sleeping && !IsGroundFlat(sampled))
            {
                continue;
            }

            destination = sampled;
            return true;
        }

        return false;
    }

    private bool TryGetRestAreaBounds(
        out BoxCollider area,
        out float halfWidth,
        out float halfHeight,
        out float halfDepth)
    {
        area = shelter == null ? null : shelter.RestArea;
        halfWidth = 0f;
        halfHeight = 0f;
        halfDepth = 0f;

        if (area == null)
        {
            Debug.LogError("The shelter is missing its RestArea.", shelter);
            return false;
        }

        Vector3 scale = area.transform.lossyScale;
        float paddingX = restAreaEdgePadding /
            Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float paddingY = restAreaEdgePadding /
            Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
        float paddingZ = restAreaEdgePadding /
            Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

        halfWidth = area.size.x * 0.5f - paddingX;
        halfHeight = Mathf.Max(
            0.01f,
            area.size.y * 0.5f - paddingY);
        halfDepth = area.size.z * 0.5f - paddingZ;

        if (halfWidth > 0f && halfDepth > 0f)
        {
            return true;
        }

        Debug.LogError(
            "RestArea is too small for its current edge padding.",
            area);
        return false;
    }

    private static Vector3 RestAreaPoint(
        BoxCollider area,
        float localX,
        float localY,
        float localZ)
    {
        Vector3 local = area.center;
        local.x += localX;
        local.y += localY;
        local.z += localZ;
        return area.transform.TransformPoint(local);
    }

    private static Vector3 RestAreaPointAtWorldHeight(
        BoxCollider area,
        float localX,
        float localZ,
        float worldHeight)
    {
        Vector3 local = area.center;
        local.x += localX;
        local.z += localZ;
        Vector3 world = area.transform.TransformPoint(local);
        world.y = worldHeight;
        return world;
    }

    private bool TryGetReachablePoint(
        Vector3 candidate,
        out Vector3 destination)
    {
        destination = transform.position;

        if (!agent.enabled ||
            !agent.isOnNavMesh ||
            !NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                navMeshSampleDistance,
                agent.areaMask) ||
            !agent.CalculatePath(hit.position, reusablePath) ||
            reusablePath.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        destination = hit.position;
        return true;
    }

    private bool IsPositionInsideShelter(Vector3 position)
    {
        return shelter != null &&
               ((shelter.InteriorTrigger != null &&
                 IsInsideBoxXZ(shelter.InteriorTrigger, position)) ||
                (shelter.RestArea != null &&
                 IsInsideBoxXZ(shelter.RestArea, position)));
    }

    private static bool IsInsideBoxXZ(
        BoxCollider area,
        Vector3 worldPosition)
    {
        Vector3 local = area.transform.InverseTransformPoint(worldPosition) -
                        area.center;
        Vector3 halfSize = area.size * 0.5f;
        return Mathf.Abs(local.x) <= halfSize.x &&
               Mathf.Abs(local.z) <= halfSize.z;
    }

    private static bool IsInsideBox(
        BoxCollider area,
        Vector3 worldPosition)
    {
        Vector3 local = area.transform.InverseTransformPoint(worldPosition) -
                        area.center;
        Vector3 halfSize = area.size * 0.5f;
        return Mathf.Abs(local.x) <= halfSize.x &&
               Mathf.Abs(local.y) <= halfSize.y &&
               Mathf.Abs(local.z) <= halfSize.z;
    }

    private bool IsEntryPositionAvailable(Vector3 position)
    {
        float distance = Mathf.Max(
            minimumChickenSeparation,
            agent.radius * 2f + doorwayClearArrivalDistance);
        float distanceSquared = distance * distance;

        foreach (ChickenShelterController other in ActiveChickens)
        {
            if (other == null ||
                other == this ||
                other.shelter != shelter ||
                other.isOutside)
            {
                continue;
            }

            Vector3 offset = other.transform.position - position;
            offset.y = 0f;

            if (offset.sqrMagnitude < distanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsGroundFlat(Vector3 position)
    {
        return Physics.Raycast(
                   position + Vector3.up * 0.75f,
                   Vector3.down,
                   out RaycastHit hit,
                   1.5f,
                   Physics.DefaultRaycastLayers,
                   QueryTriggerInteraction.Ignore) &&
               Vector3.Angle(hit.normal, Vector3.up) <= maximumSleepSlope;
    }

    private IEnumerator MoveTo(
        Vector3 target,
        string targetName,
        float? cutoff,
        float stoppingDistance = -1f)
    {
        moveSucceeded = false;
        moveFailure = "an unknown movement failure";

        if (!agent.enabled || !agent.isOnNavMesh)
        {
            moveFailure = "its NavMeshAgent is not on an active NavMesh";
            yield break;
        }

        float originalStoppingDistance = agent.stoppingDistance;
        bool overrideStoppingDistance = stoppingDistance >= 0f;

        if (overrideStoppingDistance)
        {
            agent.stoppingDistance = stoppingDistance;
        }

        try
        {
            if (!NavMesh.SamplePosition(
                    target,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    agent.areaMask))
            {
                FailMove($"no Chicken NavMesh exists near {targetName}");
                yield break;
            }

            agent.isStopped = false;

            if (!agent.SetDestination(hit.position))
            {
                FailMove($"Unity rejected {targetName} as a destination");
                yield break;
            }

            float deadline = Time.time + movementTimeout;
            float lastClock = CurrentTime;

            while (agent.pathPending && Time.time < deadline)
            {
                if (cutoff.HasValue &&
                    ActionExpired(cutoff.Value, ref lastClock))
                {
                    FailMove(
                        "the schedule boundary was reached while calculating " +
                        $"the path to {targetName}");
                    yield break;
                }

                yield return null;
            }

            if (agent.pathPending)
            {
                FailMove($"path calculation to {targetName} timed out");
                yield break;
            }

            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                FailMove($"the path to {targetName} is incomplete");
                yield break;
            }

            float arrival = Mathf.Max(
                stoppingDistance >= 0f ? stoppingDistance : arrivalDistance,
                agent.stoppingDistance);

            while (Time.time < deadline)
            {
                if (cutoff.HasValue &&
                    ActionExpired(cutoff.Value, ref lastClock))
                {
                    FailMove(
                        "the schedule boundary was reached while moving to " +
                        targetName);
                    yield break;
                }

                if (!agent.pathPending &&
                    agent.remainingDistance <= arrival)
                {
                    moveSucceeded = true;
                    moveFailure = null;
                    yield break;
                }

                yield return null;
            }

            FailMove($"movement to {targetName} timed out");
        }
        finally
        {
            if (overrideStoppingDistance && agent != null)
            {
                agent.stoppingDistance = originalStoppingDistance;
            }
        }
    }

    private void FailMove(string reason)
    {
        moveFailure = reason;
        StopMoving();
    }

    private IEnumerator WaitUntil(float seconds, float cutoff)
    {
        float deadline = Time.time + seconds;
        float lastClock = CurrentTime;

        while (Time.time < deadline &&
               !ActionExpired(cutoff, ref lastClock))
        {
            yield return null;
        }
    }

    private bool ActionExpired(float cutoff, ref float lastClock)
    {
        float now = CurrentTime;
        bool clockMovedBackward = now + 0.01f < lastClock;
        lastClock = now;
        return clockMovedBackward || IsSleepPeriod(now) || now >= cutoff;
    }

    private bool IsSleepPeriod(float time)
    {
        return time >= sleepTime || time < wakeTime;
    }

    private float NextFloat(float minimum, float maximum)
    {
        float low = Mathf.Min(minimum, maximum);
        float high = Mathf.Max(minimum, maximum);

        if (Mathf.Approximately(low, high))
        {
            return low;
        }

        return low + (float)individualRandom.NextDouble() * (high - low);
    }

    private float NextFloat01()
    {
        return (float)individualRandom.NextDouble();
    }

    private Vector2 NextInsideUnitCircle()
    {
        float angle = NextFloat01() * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(NextFloat01());
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void StopMoving()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void OnValidate()
    {
        firstExitWindowStart = Mathf.Clamp(
            firstExitWindowStart,
            wakeTime,
            sleepTime);
        firstExitWindowEnd = Mathf.Clamp(
            firstExitWindowEnd,
            firstExitWindowStart,
            sleepTime);
        returnWindowStart = Mathf.Clamp(
            returnWindowStart,
            firstExitWindowEnd,
            sleepTime);
        returnWindowEnd = Mathf.Clamp(
            returnWindowEnd,
            returnWindowStart,
            sleepTime);

        maximumIdleDuration = Mathf.Max(
            minimumIdleDuration,
            maximumIdleDuration);
        maximumIndoorPauseDuration = Mathf.Max(
            minimumIndoorPauseDuration,
            maximumIndoorPauseDuration);
        maximumDecisionDelay = Mathf.Max(
            minimumDecisionDelay,
            maximumDecisionDelay);
        maximumInitialActionOffset = Mathf.Max(0f, maximumInitialActionOffset);
        minimumIndoorMoveDistance = Mathf.Max(0f, minimumIndoorMoveDistance);
        doorwayClearDistance = Mathf.Max(0.1f, doorwayClearDistance);
        doorwayClearArrivalDistance = Mathf.Max(
            0.02f,
            doorwayClearArrivalDistance);
        destinationAttempts = Mathf.Max(1, destinationAttempts);
    }
}