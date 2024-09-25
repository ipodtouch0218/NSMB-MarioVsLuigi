using Quantum;
using UnityEngine;

public class MasterCanvas : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private PlayerElements playerElementsPrefab;

    public void Start() {
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);    
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.Starting) {
            foreach (PlayerRef player in e.Game.GetLocalPlayers()) {
                PlayerElements elements = Instantiate(playerElementsPrefab, transform);
                elements.Initialize(player);
            }
        }
    }
}
