using UnityEngine;

[DisallowMultipleComponent]
public class AnimalAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string movingParameter = "IsMoving";
    [SerializeField] private string sleepingParameter = "IsSleeping";

    private int movingHash;
    private int sleepingHash;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        movingHash = Animator.StringToHash(movingParameter);
        sleepingHash = Animator.StringToHash(sleepingParameter);
    }

    public void SetMoving(bool value)
    {
        SetBoolIfPresent(movingHash, value);
    }

    public void SetSleeping(bool value)
    {
        SetBoolIfPresent(sleepingHash, value);
    }

    private void SetBoolIfPresent(int hash, bool value)
    {
        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(hash, value);
                return;
            }
        }
    }
}
