using UnityEngine;

public class ResetisJumping : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(AnimatorManager.IsJumpingHash, false);
    }
}
