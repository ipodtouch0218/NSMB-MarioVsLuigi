using UnityEngine;

public class MasterCanvas : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElementsPrefab;

    public void Awake() {
        NetworkHandler.OnLocalPlayerConfirmed += OnLocalPlayerAdded;
        OnLocalPlayerAdded();
    }

    public void OnDestroy() {
        NetworkHandler.OnLocalPlayerConfirmed -= OnLocalPlayerAdded;
    }

    private void OnLocalPlayerAdded() {
        foreach (var e in NetworkHandler.localPlayerConfirmations) {
            PlayerElements elements = Instantiate(playerElementsPrefab, transform);
            elements.Initialize(e.Player);
        }
        NetworkHandler.localPlayerConfirmations.Clear();
    }
}
