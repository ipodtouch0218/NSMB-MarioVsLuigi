using Quantum;
using UnityEngine;

public class MasterCanvas : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElementsPrefab;

    public void Start() {
        QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerAdded);
    }

    private void OnLocalPlayerAdded(CallbackLocalPlayerAddConfirmed e) {
        PlayerElements elements = Instantiate(playerElementsPrefab, transform);
        elements.Initialize(e.Player);
    }
}
