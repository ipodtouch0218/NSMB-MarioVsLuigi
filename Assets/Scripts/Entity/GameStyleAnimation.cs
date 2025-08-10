using UnityEngine;
using NSMB.Utilities;

public class GameStyleAnimation : MonoBehaviour
{
    public AnimationClip[] AnimationList;
    public Animation AnimatorOutput;    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AnimatorOutput.clip = AnimationList[(int)Utils.GetStageTheme()];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
