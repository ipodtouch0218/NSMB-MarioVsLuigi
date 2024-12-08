using UnityEngine;
using UnityEngine.Events;

public class ReserveAnimationEvents : MonoBehaviour {

    //---Serailized Variables
    [SerializeField] private UnityEvent staticStartedCallback;

    public void OnStaticStarted() {
        staticStartedCallback.Invoke();
    }
}
