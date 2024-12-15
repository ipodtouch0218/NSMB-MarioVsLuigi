using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TeamButton : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Sprite overlayUnpressed, overlayPressed;
    [SerializeField] private Image flag;
    [SerializeField] public int index;

    public unsafe void OnEnable() {
        Settings.OnColorblindModeChanged += OnColorblindModeChanged;

        QuantumGame game = NetworkHandler.Runner.Game;
        Frame f = game.Frames.Predicted;
        var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

        TeamAsset[] teams = f.SimulationConfig.Teams;
        flag.sprite = Settings.Instance.GraphicsColorblind ? teams[index].spriteColorblind : teams[index].spriteNormal;
    }

    public void OnDisable() {
        Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
    }

    private unsafe void OnColorblindModeChanged() {
        QuantumGame game = QuantumRunner.DefaultGame;
        TeamAsset[] teams = game.Configurations.Simulation.Teams;
        flag.sprite = Settings.Instance.GraphicsColorblind ? teams[index].spriteColorblind : teams[index].spriteNormal;
    }
}
