using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class ChickenShelterController : MonoBehaviour
{
    private sealed class DoorwayTraffic
    {
        public ChickenShelterController Owner;
        public readonly List<ChickenShelterController> Waiting =
            new List<ChickenShelterController>();
    }

    private static readonly List<ChickenShelterController> ActiveChickens =
        new List<ChickenShelterController>();

    private static readonly Dictionary<AnimalShelter, DoorwayTraffic>
        TrafficByShelter = new Dictionary<AnimalShelter, DoorwayTraffic>();

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

    [Header("Random Indoor and Outdoor Stays - Game Hours")]
    [SerializeField, Min(0.05f)] private float minimumIndoorStay = 0.5f;
    [SerializeField, Min(0.05f)] private float maximumIndoorStay = 1.5f;
    [SerializeField, Min(0.05f)] private float minimumOutdoorStay = 1f;
    [SerializeField, Min(0.05f)] private float maximumOutdoorStay = 3f;

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
    [SerializeField, Min(0.05f)] private float sleepPreparationLeadTime = 0.1f;

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
    private bool sleepPositionPrepared;
    private bool hasReservedSleepPosition;
    private Vector3 reservedSleepPosition;

    private bool dailyScheduleGenerated;
    private float firstExitTimeToday;
    private float returnTimeToday;
    private float nextDoorTransitionTime;
    private float previousClockTime = -1f;
    private bool missingNavMeshReported;

    private AnimalShelter requestedDoorwayShelter;
    private bool hasDoorwayAccess;

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
        ReleaseDoorway();
        ActiveChickens.Remove(this);
        hasReservedSleepPosition = false;
        sleepPositionPrepared = false;
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
                if (now >= nextDoorTransitionTime)
                {
                    yield return isOutside
                        ? EnterShelter(returnTimeToday)
                        : ExitShelter(returnTimeToday);

                    ScheduleNextTransition();
                }
                else
                {
                    float cutoff = Mathf.Min(
                        nextDoorTransitionTime,
                        returnTimeToday);
                    yield return PerformAction(isOutside, cutoff);
                }
            }
            else
            {
                yield return ReturnAndPrepareForSleep();
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
            sleepPositionPrepared = false;
            hasReservedSleepPosition = false;
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

        firstExitTimeToday = Random.Range(exitStart, exitEnd);
        returnTimeToday = Random.Range(returnStart, returnEnd);
        isOutside = !IsPositionInsideShelter(transform.position);
        mustWanderAfterEntry = false;
        sleepPositionPrepared = false;
        hasReservedSleepPosition = false;

        nextDoorTransitionTime = now < firstExitTimeToday
            ? firstExitTimeToday
            : now < returnTimeToday
                ? now + RandomStayDuration(isOutside)
                : returnTimeToday;

        dailyScheduleGenerated = true;

        Debug.Log(
            $"Chicken schedule: first exit {firstExitTimeToday:0.00}, " +
            $"return {returnTimeToday:0.00}, sleep {sleepTime:0.00}.",
            this);
    }

    private void ScheduleNextTransition()
    {
        float delay = moveSucceeded ? RandomStayDuration(isOutside) : 0.1f;
        nextDoorTransitionTime = Mathf.Min(
            CurrentTime + delay,
            returnTimeToday);
    }

    private float RandomStayDuration(bool outside)
    {
        float minimum = outside ? minimumOutdoorStay : minimumIndoorStay;
        float maximum = outside ? maximumOutdoorStay : maximumIndoorStay;
        return Random.Range(Mathf.Min(minimum, maximum), Mathf.Max(minimum, maximum));
    }

    private IEnumerator EnterShelter(float cutoff)
    {
        moveSucceeded = false;

        if (!HasEntrancePoints())
        {
            yield break;
        }

        yield return AcquireDoorway(cutoff);

        if (!hasDoorwayAccess)
        {
            yield break;
        }

        yield return MoveTo(
            shelter.OutsideApproachPoint.position,
            shelter.OutsideApproachPoint.name,
            cutoff);

        if (!moveSucceeded)
        {
            ReleaseDoorway();
            yield break;
        }

        yield return MoveTo(
            shelter.InsideEntryPoint.position,
            shelter.InsideEntryPoint.name,
            cutoff);

        if (!moveSucceeded)
        {
            ReleaseDoorway();
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

        ReleaseDoorway();
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

        yield return AcquireDoorway(cutoff);

        if (!hasDoorwayAccess)
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

        ReleaseDoorway();
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

    private IEnumerator AcquireDoorway(float cutoff)
    {
        hasDoorwayAccess = false;

        if (shelter == null)
        {
            yield break;
        }

        requestedDoorwayShelter = shelter;

        if (!TrafficByShelter.TryGetValue(shelter, out DoorwayTraffic traffic))
        {
            traffic = new DoorwayTraffic();
            TrafficByShelter.Add(shelter, traffic);
        }

        PruneTraffic(shelter, traffic);

        if (traffic.Owner == null && traffic.Waiting.Count == 0)
        {
            traffic.Owner = this;
            hasDoorwayAccess = true;
            yield break;
        }

        if (traffic.Owner == this)
        {
            hasDoorwayAccess = true;
            yield break;
        }

        if (!traffic.Waiting.Contains(this))
        {
            traffic.Waiting.Add(this);
        }

        float lastClock = CurrentTime;

        while (enabled && requestedDoorwayShelter == shelter)
        {
            PruneTraffic(shelter, traffic);

            if (traffic.Owner == this)
            {
                hasDoorwayAccess = true;
                yield break;
            }

            if (traffic.Owner == null &&
                traffic.Waiting.Count > 0 &&
                traffic.Waiting[0] == this)
            {
                traffic.Waiting.RemoveAt(0);
                traffic.Owner = this;
                hasDoorwayAccess = true;
                yield break;
            }

            if (ActionExpired(cutoff, ref lastClock))
            {
                ReleaseDoorway();
                yield break;
            }

            yield return null;
        }

        ReleaseDoorway();
    }

    private static void PruneTraffic(
        AnimalShelter trafficShelter,
        DoorwayTraffic traffic)
    {
        if (traffic.Owner != null &&
            (!traffic.Owner.isActiveAndEnabled ||
             traffic.Owner.shelter != trafficShelter))
        {
            traffic.Owner.hasDoorwayAccess = false;
            traffic.Owner.requestedDoorwayShelter = null;
            traffic.Owner = null;
        }

        for (int i = traffic.Waiting.Count - 1; i >= 0; i--)
        {
            ChickenShelterController chicken = traffic.Waiting[i];

            if (chicken == null ||
                !chicken.isActiveAndEnabled ||
                chicken.shelter != trafficShelter)
            {
                traffic.Waiting.RemoveAt(i);
            }
        }
    }

    private void ReleaseDoorway()
    {
        AnimalShelter trafficShelter = requestedDoorwayShelter;
        requestedDoorwayShelter = null;
        hasDoorwayAccess = false;

        if (trafficShelter == null ||
            !TrafficByShelter.TryGetValue(
                trafficShelter,
                out DoorwayTraffic traffic))
        {
            return;
        }

        if (traffic.Owner == this)
        {
            traffic.Owner = null;
        }

        traffic.Waiting.Remove(this);
        PruneTraffic(trafficShelter, traffic);

        if (traffic.Owner == null && traffic.Waiting.Count == 0)
        {
            TrafficByShelter.Remove(trafficShelter);
        }
    }

    private IEnumerator PerformAction(bool outside, float cutoff)
    {
        Vector3 destination = transform.position;
        bool shouldMove = outside
            ? Random.value <= outdoorWanderChance
            : mustWanderAfterEntry || Random.value > indoorPauseChance;
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

            yield break;
        }

        StopMoving();
        float minimumPause = outside
            ? minimumIdleDuration
            : minimumIndoorPauseDuration;
        float maximumPause = outside
            ? maximumIdleDuration
            : maximumIndoorPauseDuration;
        float duration = Random.Range(
            Mathf.Min(minimumPause, maximumPause),
            Mathf.Max(minimumPause, maximumPause));
        yield return WaitUntil(duration, cutoff);
    }

    private IEnumerator ReturnAndPrepareForSleep()
    {
        if (isOutside)
        {
            yield return EnterShelter(sleepTime);

            if (isOutside)
            {
                yield break;
            }
        }

        float preparationTime = Mathf.Max(
            returnTimeToday,
            sleepTime - sleepPreparationLeadTime);

        if (CurrentTime < preparationTime)
        {
            yield return PerformAction(false, preparationTime);
            yield break;
        }

        if (sleepPositionPrepared)
        {
            StopMoving();
            yield return WaitUntil(1f, sleepTime);
            yield break;
        }

        if (!TryFindRestDestination(true, out Vector3 destination))
        {
            Debug.LogWarning(
                "No valid sleeping position was found; the chicken will " +
                "sleep at its current position.",
                this);
            sleepPositionPrepared = true;
            StopMoving();
            yield return WaitUntil(1f, sleepTime);
            yield break;
        }

        reservedSleepPosition = destination;
        hasReservedSleepPosition = true;
        yield return MoveTo(destination, "sleeping destination", sleepTime);

        if (moveSucceeded)
        {
            sleepPositionPrepared = true;
            StopMoving();
        }
        else
        {
            hasReservedSleepPosition = false;
        }
    }

    private void Sleep()
    {
        StopMoving();
        isSleeping = true;
    }

    private void WakeUp()
    {
        isSleeping = false;
        sleepPositionPrepared = false;
        hasReservedSleepPosition = false;
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
            Vector2 offset = Random.insideUnitCircle * outdoorRoamRadius;
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
        float bestDepth = float.NegativeInfinity;
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
                Vector3 candidate = RestAreaPoint(
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

                if (depth < minimumDepth || depth <= bestDepth)
                {
                    continue;
                }

                bestDepth = depth;
                fallback = sampled;
                foundFallback = true;

                if (depth >= requiredDepth)
                {
                    destination = sampled;
                }
            }
        }

        if (bestDepth >= requiredDepth)
        {
            return true;
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
                out float halfWidth,
                out float halfDepth))
        {
            return false;
        }

        for (int i = 0; i < destinationAttempts; i++)
        {
            Vector3 candidate = RestAreaPoint(
                area,
                Random.Range(-halfWidth, halfWidth),
                Random.Range(-halfDepth, halfDepth),
                transform.position.y);

            if (!TryGetReachablePoint(candidate, out Vector3 sampled) ||
                !IsInsideBoxXZ(area, sampled))
            {
                continue;
            }

            if (!sleeping)
            {
                Vector3 movement = sampled - transform.position;
                movement.y = 0f;

                if (movement.sqrMagnitude <
                    minimumIndoorMoveDistance * minimumIndoorMoveDistance)
                {
                    continue;
                }
            }

            if (sleeping &&
                (!IsGroundFlat(sampled) ||
                 !IsSleepPositionAvailable(sampled)))
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
        out float halfDepth)
    {
        area = shelter == null ? null : shelter.RestArea;
        halfWidth = 0f;
        halfDepth = 0f;

        if (area == null)
        {
            Debug.LogError("The shelter is missing its RestArea.", shelter);
            return false;
        }

        Vector3 scale = area.transform.lossyScale;
        float paddingX = restAreaEdgePadding /
            Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float paddingZ = restAreaEdgePadding /
            Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

        halfWidth = area.size.x * 0.5f - paddingX;
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

    private bool IsSleepPositionAvailable(Vector3 position)
    {
        float distanceSquared =
            minimumChickenSeparation * minimumChickenSeparation;

        foreach (ChickenShelterController other in ActiveChickens)
        {
            if (other == null ||
                other == this ||
                (!other.isSleeping && !other.hasReservedSleepPosition))
            {
                continue;
            }

            Vector3 occupied = other.hasReservedSleepPosition
                ? other.reservedSleepPosition
                : other.transform.position;
            Vector3 offset = occupied - position;
            offset.y = 0f;

            if (offset.sqrMagnitude < distanceSquared)
            {
                return false;
            }
        }

        return true;
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

        maximumIndoorStay = Mathf.Max(minimumIndoorStay, maximumIndoorStay);
        maximumOutdoorStay = Mathf.Max(minimumOutdoorStay, maximumOutdoorStay);
        maximumIdleDuration = Mathf.Max(
            minimumIdleDuration,
            maximumIdleDuration);
        maximumIndoorPauseDuration = Mathf.Max(
            minimumIndoorPauseDuration,
            maximumIndoorPauseDuration);
        minimumIndoorMoveDistance = Mathf.Max(0f, minimumIndoorMoveDistance);
        sleepPreparationLeadTime = Mathf.Clamp(
            sleepPreparationLeadTime,
            0.05f,
            Mathf.Max(0.05f, sleepTime - returnWindowEnd));
        doorwayClearDistance = Mathf.Max(0.1f, doorwayClearDistance);
        doorwayClearArrivalDistance = Mathf.Max(
            0.02f,
            doorwayClearArrivalDistance);
        destinationAttempts = Mathf.Max(1, destinationAttempts);
    }
}