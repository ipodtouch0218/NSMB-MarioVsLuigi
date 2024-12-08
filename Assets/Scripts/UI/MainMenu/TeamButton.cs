using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TeamButton : MonoBehaviour, ISelectHandler, IDeselectHandler {

    //---Serialized Variables
    [SerializeField] private Sprite overlayUnpressed, overlayPressed;
    [SerializeField] private Image overlay, flag;
    [SerializeField] public int index;

    public unsafe void OnEnable() {
        QuantumGame game = QuantumRunner.DefaultGame;
        Frame f = game.Frames.Predicted;
        var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

        TeamAsset[] teams = f.SimulationConfig.Teams;
        overlay.enabled = (playerData->Team % teams.Length) == index;
        flag.sprite = Settings.Instance.GraphicsColorblind ? teams[index].spriteColorblind : teams[index].spriteNormal;

        Settings.OnColorblindModeChanged += OnColorblindModeChanged;
    }

    public void OnDisable() {
        Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
    }

    public void OnSelect(BaseEventData eventData) {
        overlay.enabled = true;
        overlay.sprite = overlayUnpressed;
    }

    public void OnDeselect(BaseEventData eventData) {
        overlay.enabled = false;
        overlay.sprite = overlayUnpressed;
    }

    public void OnPress() {
        overlay.sprite = overlayPressed;
    }

    private unsafe void OnColorblindModeChanged() {
        QuantumGame game = QuantumRunner.DefaultGame;
        TeamAsset[] teams = game.Configurations.Simulation.Teams;
        flag.sprite = Settings.Instance.GraphicsColorblind ? teams[index].spriteColorblind : teams[index].spriteNormal;
    }
}
