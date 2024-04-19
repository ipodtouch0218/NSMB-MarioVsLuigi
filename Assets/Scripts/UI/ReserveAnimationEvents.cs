using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class ReserveAnimationEvents : MonoBehaviour
{
    [SerializeField] private UnityEvent staticStartedCallback;

    public void OnStaticStarted() {
        staticStartedCallback.Invoke();
    }
}
