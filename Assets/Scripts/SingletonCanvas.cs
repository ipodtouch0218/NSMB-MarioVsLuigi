using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingletonCanvas : Singleton<SingletonCanvas> {
    void Awake() {
        if (!base.InstanceCheck()) return;
        instance = this;
    }
}
