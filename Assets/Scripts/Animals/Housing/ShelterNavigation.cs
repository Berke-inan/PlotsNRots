using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshSurface))]
[DefaultExecutionOrder(-1000)]
public sealed class ShelterNavigation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AnimalShelter shelter;
    [SerializeField] private NavMeshSurface surface;

    [Header("Runtime Building")]
    [Tooltip("Builds this shelter's local NavMesh when the placed shelter starts.")]
    [SerializeField] private bool buildOnStart = true;
    [Tooltip("Wait one frame so purchased/spawned shelter transforms settle first.")]
    [SerializeField] private bool delayBuildOneFrame = true;

    private bool isBuilt;

    public AnimalShelter Shelter => shelter;
    public NavMeshSurface Surface => surface;
    public bool IsBuilt => isBuilt;

    public bool SupportsAgent(NavMeshAgent agent)
    {
        return agent != null &&
               surface != null &&
               surface.agentTypeID == agent.agentTypeID;
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private System.Collections.IEnumerator Start()
    {
        if (!buildOnStart)
        {
            yield break;
        }

        if (delayBuildOneFrame)
        {
            yield return null;
        }

        Rebuild();
    }

    private void OnDisable()
    {
        if (surface != null)
        {
            surface.RemoveData();
        }

        isBuilt = false;
    }

    public void Rebuild()
    {
        ResolveReferences();

        isBuilt = false;

        if (surface == null)
        {
            Debug.LogError(
                "ShelterNavigation requires a NavMeshSurface.",
                this);
            return;
        }

        surface.RemoveData();
        surface.BuildNavMesh();
        isBuilt = surface.navMeshData != null;

        if (!isBuilt)
        {
            Debug.LogError(
                "The shelter NavMesh could not be built. Check its volume, " +
                "included layers, geometry mode, and agent type.",
                this);
        }
    }

    public void RemoveNavigation()
    {
        if (surface != null)
        {
            surface.RemoveData();
        }

        isBuilt = false;
    }

    private void ResolveReferences()
    {
        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
        }

        if (shelter == null)
        {
            shelter = GetComponentInParent<AnimalShelter>();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}