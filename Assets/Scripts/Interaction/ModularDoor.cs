using System.Collections;
using UnityEngine;

public class ModularDoor : MonoBehaviour, IInteractable
{
    [Header("Door Panels")]
    [SerializeField] private Transform primaryPanel;
    [SerializeField] private Transform secondaryPanel;

    [Header("Open Rotation Offsets (Local)")]
    [SerializeField] private Vector3 primaryOpenRotation = new Vector3(0f, 90f, 0f);
    [SerializeField] private Vector3 secondaryOpenRotation = new Vector3(0f, -90f, 0f);

    [Header("Movement")]
    [Min(1f)]
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private bool startsOpen;

    [Header("Opening Direction")]
    [Tooltip("Enable only if the gate opens toward the player instead of away from them.")]
    [SerializeField] private bool reverseOpeningDirection;

    private Quaternion primaryClosedRotation;
    private Quaternion secondaryClosedRotation;
    private bool isOpen;
    private bool isMoving;
    private float openingDirection = 1f;

    private void Awake()
    {
        if (primaryPanel == null)
        {
            Debug.LogError($"{name}: ModularDoor requires a Primary Panel.", this);
            enabled = false;
            return;
        }

        primaryClosedRotation = primaryPanel.localRotation;

        if (secondaryPanel != null)
        {
            secondaryClosedRotation = secondaryPanel.localRotation;
        }

        isOpen = startsOpen;
        ApplyImmediateState();
    }

    public void Interact(GameObject interactor)
    {
        if (!enabled || isMoving)
        {
            return;
        }

        bool shouldOpen = !isOpen;

        // Choose a side only when opening. Closing always returns to the
        // stored closed rotations, regardless of the player's current side.
        if (shouldOpen)
        {
            openingDirection = GetOpeningDirection(interactor);
        }

        StartCoroutine(AnimateDoor(shouldOpen));
    }

    private IEnumerator AnimateDoor(bool open)
    {
        isMoving = true;

        Quaternion primaryTarget = GetTargetRotation(
            primaryClosedRotation,
            primaryOpenRotation * openingDirection,
            open);

        Quaternion secondaryTarget = secondaryPanel != null
            ? GetTargetRotation(
                secondaryClosedRotation,
                secondaryOpenRotation * openingDirection,
                open)
            : Quaternion.identity;

        while (Quaternion.Angle(primaryPanel.localRotation, primaryTarget) > 0.1f ||
               (secondaryPanel != null &&
                Quaternion.Angle(secondaryPanel.localRotation, secondaryTarget) > 0.1f))
        {
            primaryPanel.localRotation = Quaternion.RotateTowards(
                primaryPanel.localRotation,
                primaryTarget,
                rotationSpeed * Time.deltaTime);

            if (secondaryPanel != null)
            {
                secondaryPanel.localRotation = Quaternion.RotateTowards(
                    secondaryPanel.localRotation,
                    secondaryTarget,
                    rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        primaryPanel.localRotation = primaryTarget;

        if (secondaryPanel != null)
        {
            secondaryPanel.localRotation = secondaryTarget;
        }

        isOpen = open;
        isMoving = false;
    }

    private void ApplyImmediateState()
    {
        primaryPanel.localRotation = GetTargetRotation(
            primaryClosedRotation,
            primaryOpenRotation * openingDirection,
            isOpen);

        if (secondaryPanel != null)
        {
            secondaryPanel.localRotation = GetTargetRotation(
                secondaryClosedRotation,
                secondaryOpenRotation * openingDirection,
                isOpen);
        }
    }

    private float GetOpeningDirection(GameObject interactor)
    {
        if (interactor == null)
        {
            return reverseOpeningDirection ? -1f : 1f;
        }

        // Use the visible door panels, not this wrapper object's position.
        // The wrapper may have been reset before the imported model was parented
        // and can therefore be far away from the actual doorway.
        Vector3 doorCentre = primaryPanel.position;
        Vector3 doorNormal = transform.forward;

        if (secondaryPanel != null)
        {
            doorCentre = (primaryPanel.position + secondaryPanel.position) * 0.5f;

            // The hinge-to-hinge line lies across a closed double gate. Crossing
            // it with world up gives the normal of the gate plane, independent
            // of the wrapper object's position or rotation.
            Vector3 hingeLine = secondaryPanel.position - primaryPanel.position;
            Vector3 calculatedNormal = Vector3.Cross(Vector3.up, hingeLine);

            if (calculatedNormal.sqrMagnitude > 0.0001f)
            {
                doorNormal = calculatedNormal.normalized;
            }
        }

        Vector3 toInteractor = interactor.transform.position - doorCentre;
        float side = Vector3.Dot(doorNormal, toInteractor);
        float direction = side >= 0f ? 1f : -1f;

        return reverseOpeningDirection ? -direction : direction;
    }

    private static Quaternion GetTargetRotation(
        Quaternion closedRotation,
        Vector3 openOffset,
        bool open)
    {
        return open
            ? closedRotation * Quaternion.Euler(openOffset)
            : closedRotation;
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
    }
}
