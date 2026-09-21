using UnityEngine;

[CreateAssetMenu(
    fileName = "AnimalProductProfile",
    menuName = "PnR/Animals/Product Profile")]
public class AnimalProductProfile : ScriptableObject
{
    [Header("Product")]
    [SerializeField] private GameObject worldProductPrefab;
    [SerializeField, Min(1)] private int quantity = 1;

    [Header("Daily Production")]
    [SerializeField, Range(0f, 24f)] private float productionTime = 7f;

    [Header("Spawn Placement")]
    [SerializeField] private Vector3 localSpawnOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float randomHorizontalOffset = 0.15f;

    public GameObject WorldProductPrefab => worldProductPrefab;
    public int Quantity => quantity;
    public float ProductionTime => productionTime;
    public Vector3 LocalSpawnOffset => localSpawnOffset;
    public float RandomHorizontalOffset => randomHorizontalOffset;

    private void OnValidate()
    {
        quantity = Mathf.Max(1, quantity);
        productionTime = Mathf.Clamp(productionTime, 0f, 24f);
        randomHorizontalOffset = Mathf.Max(0f, randomHorizontalOffset);
    }
}
