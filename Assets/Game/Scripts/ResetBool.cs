using UnityEngine;

public class ResetBool : StateMachineBehaviour
{
    public string isInteractingBool;
    public bool isInteractingStatus;

    // Hash the parameter name once instead of on every state enter.
    int boolHash;
    string hashedName;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!ReferenceEquals(hashedName, isInteractingBool))
        {
            hashedName = isInteractingBool;
            boolHash = Animator.StringToHash(isInteractingBool);
        }
        animator.SetBool(boolHash, isInteractingStatus);
    }
}
