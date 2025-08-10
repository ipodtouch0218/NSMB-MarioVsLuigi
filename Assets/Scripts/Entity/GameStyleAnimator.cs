using UnityEngine;
using NSMB.Utilities;

public class GameStyleAnimator : MonoBehaviour
{
    public RuntimeAnimatorController[] AnimatorList;
    public Animator AnimatorOutput;

    void Start() {
        Update();
    }
    void Update() {
        AnimatorOutput.runtimeAnimatorController = AnimatorList[(int) Utils.GetStageTheme()];
    }
}
