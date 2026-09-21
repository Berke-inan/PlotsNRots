using UnityEngine;

[CreateAssetMenu(
    fileName = "AnimalSpeciesProfile",
    menuName = "PnR/Animals/Species Profile")]
public class AnimalSpeciesProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private AnimalSpecies species = AnimalSpecies.Chicken;

    [Header("Daily Schedule - Game Hours")]
    [SerializeField, Range(0f, 24f)] private float wakeTime = 6f;
    [SerializeField, Range(0f, 24f)] private float firstExitWindowStart = 8f;
    [SerializeField, Range(0f, 24f)] private float firstExitWindowEnd = 9f;
    [SerializeField, Range(0f, 24f)] private float returnWindowStart = 20f;
    [SerializeField, Range(0f, 24f)] private float returnWindowEnd = 21f;
    [SerializeField, Range(0f, 24f)] private float sleepTime = 22f;

    [Header("Independent Behaviour")]
    [SerializeField, Range(0f, 1f)] private float indoorExitChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float outdoorEnterChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float outdoorWanderChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float indoorPauseChance = 0.2f;
    [SerializeField, Min(0f)] private float minimumOutdoorPause = 2f;
    [SerializeField, Min(0f)] private float maximumOutdoorPause = 5f;
    [SerializeField, Min(0f)] private float minimumIndoorPause = 0.5f;
    [SerializeField, Min(0f)] private float maximumIndoorPause = 1.5f;
    [SerializeField, Min(0f)] private float minimumDecisionDelay = 0.25f;
    [SerializeField, Min(0f)] private float maximumDecisionDelay = 1.25f;
    [SerializeField, Min(0f)] private float maximumInitialActionOffset = 2f;

    [Header("Navigation")]
    [SerializeField, Min(0.25f)] private float outdoorRoamRadius = 3f;
    [SerializeField, Min(0f)] private float minimumIndoorMoveDistance = 0.75f;

    public AnimalSpecies Species => species;
    public float WakeTime => wakeTime;
    public float FirstExitWindowStart => firstExitWindowStart;
    public float FirstExitWindowEnd => firstExitWindowEnd;
    public float ReturnWindowStart => returnWindowStart;
    public float ReturnWindowEnd => returnWindowEnd;
    public float SleepTime => sleepTime;
    public float IndoorExitChance => indoorExitChance;
    public float OutdoorEnterChance => outdoorEnterChance;
    public float OutdoorWanderChance => outdoorWanderChance;
    public float IndoorPauseChance => indoorPauseChance;
    public float MinimumOutdoorPause => minimumOutdoorPause;
    public float MaximumOutdoorPause => maximumOutdoorPause;
    public float MinimumIndoorPause => minimumIndoorPause;
    public float MaximumIndoorPause => maximumIndoorPause;
    public float MinimumDecisionDelay => minimumDecisionDelay;
    public float MaximumDecisionDelay => maximumDecisionDelay;
    public float MaximumInitialActionOffset => maximumInitialActionOffset;
    public float OutdoorRoamRadius => outdoorRoamRadius;
    public float MinimumIndoorMoveDistance => minimumIndoorMoveDistance;

    private void OnValidate()
    {
        firstExitWindowStart = Mathf.Clamp(firstExitWindowStart, wakeTime, sleepTime);
        firstExitWindowEnd = Mathf.Clamp(firstExitWindowEnd, firstExitWindowStart, sleepTime);
        returnWindowStart = Mathf.Clamp(returnWindowStart, firstExitWindowEnd, sleepTime);
        returnWindowEnd = Mathf.Clamp(returnWindowEnd, returnWindowStart, sleepTime);
        maximumOutdoorPause = Mathf.Max(minimumOutdoorPause, maximumOutdoorPause);
        maximumIndoorPause = Mathf.Max(minimumIndoorPause, maximumIndoorPause);
        maximumDecisionDelay = Mathf.Max(minimumDecisionDelay, maximumDecisionDelay);
    }
}
