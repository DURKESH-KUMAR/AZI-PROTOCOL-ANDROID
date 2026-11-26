using UnityEngine;

public class AnimatorManager : MonoBehaviour
{
    public Animator animator;

    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");
    static readonly int IsInteractingHash = Animator.StringToHash("isInteracting");
    public static readonly int IsJumpingHash = Animator.StringToHash("isJumping");

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    static float Snap(float value)
    {
        float abs = Mathf.Abs(value);
        if (abs <= 0f) return 0f;
        float snapped = abs < 0.55f ? 0.5f : 1f;
        return value > 0f ? snapped : -snapped;
    }

    public void ChangeAnimatorValues(float horizontalMovement, float verticalMovement, bool isSprinting)
    {
        float snappedHorizontalMovement = Snap(horizontalMovement);
        float snappedVerticalMovement = Snap(verticalMovement);

        if (isSprinting)
        {
            snappedHorizontalMovement = horizontalMovement;
            snappedVerticalMovement = 2f;
        }

        animator.SetFloat(HorizontalHash, snappedHorizontalMovement, 0.1f, Time.deltaTime);
        animator.SetFloat(VerticalHash, snappedVerticalMovement, 0.1f, Time.deltaTime);
    }

    public void PlayTargetAnim(string targetAnim, bool isInteracting)
    {
        PlayTargetAnim(Animator.StringToHash(targetAnim), isInteracting);
    }

    /// <summary>Hash overload: use with cached Animator.StringToHash values to avoid per-call hashing.</summary>
    public void PlayTargetAnim(int targetAnimHash, bool isInteracting)
    {
        animator.SetBool(IsInteractingHash, isInteracting);
        animator.CrossFade(targetAnimHash, 0.2f);
    }
}
