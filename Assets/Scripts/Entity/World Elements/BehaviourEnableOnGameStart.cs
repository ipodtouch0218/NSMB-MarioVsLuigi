using UnityEngine;

public class BehaviourEnableOnGameStart : WaitForGameStart {
    public Behaviour[] behaviours;
    public override void Execute() {
        foreach (Behaviour behaviour in behaviours)
            behaviour.enabled = true;
    }
}
