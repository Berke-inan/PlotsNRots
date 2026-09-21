using UnityEngine;

// CLEAN REVISION 2026-08-03 V2
// ShelterRoamingArea is separate from RestArea.

public enum AnimalSpecies
{
    Chicken,
    Cow,
    Sheep
}

[DisallowMultipleComponent]
public class AnimalShelter : MonoBehaviour
{
    [Header("Shelter Settings")]
    [SerializeField] private AnimalSpecies species = AnimalSpecies.Chicken;
    [SerializeField, Min(1)] private int capacity = 10;

    [Header("Navigation Points")]
    [SerializeField] private Transform outsideApproachPoint;
    [SerializeField] private Transform insideEntryPoint;
    [SerializeField] private Transform roamAnchor;

    [Header("Shelter Areas")]
    [SerializeField] private BoxCollider interiorTrigger;
    [SerializeField] private BoxCollider restArea;
    [Tooltip("Complete navigable area inside the shelter boundary. Used for daytime indoor roaming and doorway waiting; RestArea remains for sleeping.")]
    [SerializeField] private BoxCollider shelterRoamingArea;
    [Tooltip("Optional bounded outdoor territory. When omitted, animals use the profile roam radius around RoamAnchor.")]
    [SerializeField] private BoxCollider outdoorRoamingArea;

    [Header("Local Navigation")]
    [Tooltip("Optional navigation module owned by this shelter prefab.")]
    [SerializeField] private ShelterNavigation shelterNavigation;

    private AnimalController doorwayOwner;
    private float doorwayReservationExpiresAt;

    public AnimalSpecies Species => species;
    public int Capacity => capacity;
    public Transform OutsideApproachPoint => outsideApproachPoint;
    public Transform InsideEntryPoint => insideEntryPoint;
    public Transform RoamAnchor => roamAnchor;
    public BoxCollider InteriorTrigger => interiorTrigger;
    public BoxCollider RestArea => restArea;
    public BoxCollider ShelterRoamingArea => shelterRoamingArea;
    public BoxCollider OutdoorRoamingArea => outdoorRoamingArea;
    public ShelterNavigation Navigation => shelterNavigation;

    public bool TryReserveDoorway(
        AnimalController animal,
        float reservationDuration)
    {
        if (animal == null)
        {
            return false;
        }

        if (doorwayOwner == null ||
            doorwayOwner == animal ||
            Time.time >= doorwayReservationExpiresAt)
        {
            doorwayOwner = animal;
            doorwayReservationExpiresAt = Time.time +
                Mathf.Max(1f, reservationDuration);
            return true;
        }

        return false;
    }

    public void RefreshDoorwayReservation(
        AnimalController animal,
        float reservationDuration)
    {
        if (doorwayOwner != animal)
        {
            return;
        }

        doorwayReservationExpiresAt = Time.time +
            Mathf.Max(1f, reservationDuration);
    }

    public void ReleaseDoorway(AnimalController animal)
    {
        if (doorwayOwner != animal)
        {
            return;
        }

        doorwayOwner = null;
        doorwayReservationExpiresAt = 0f;
    }

    public bool Supports(AnimalSpecies requestedSpecies)
    {
        return species == requestedSpecies;
    }

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);

        if (shelterNavigation == null)
        {
            shelterNavigation = GetComponentInChildren<ShelterNavigation>(true);
        }
    }
}