using UnityEngine;

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

    public AnimalSpecies Species => species;
    public int Capacity => capacity;
    public Transform OutsideApproachPoint => outsideApproachPoint;
    public Transform InsideEntryPoint => insideEntryPoint;
    public Transform RoamAnchor => roamAnchor;
    public BoxCollider InteriorTrigger => interiorTrigger;
    public BoxCollider RestArea => restArea;

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
    }
}