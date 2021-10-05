using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonoBehaviourEnableOnGameStart : WaitForGameStart {
    public MonoBehaviour[] behaviours;
    public override void Execute() {
        foreach (MonoBehaviour behaviour in behaviours) {
            behaviour.enabled = true;
        }
    }
}
