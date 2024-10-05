using NSMB.Extensions;
using NSMB.UI.MainMenu;
using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

public class TeamChooser : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Canvas baseCanvas;
    [SerializeField] private GameObject blockerTemplate, content, disabledIcon, normalIcon;
    [SerializeField] private TeamButton[] buttons;
    [SerializeField] private Button button;
    [SerializeField] private Image flag;

    //---Private Variables
    private GameObject blockerInstance;

    public void OnEnable() {
        Settings.OnColorblindModeChanged += OnColorblindModeChanged;
    }

    public void OnDisable() {
        Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
    }

    public void SetEnabled(bool value) {
        button.interactable = value;
        normalIcon.SetActive(value);
        disabledIcon.SetActive(!value);

        if (value) {
            OnColorblindModeChanged();
        } else {
            Close(true);
        }
    }

    public void SelectTeam(TeamButton team) {
        int selected = team.index;

        QuantumGame game = QuantumRunner.DefaultGame;
        foreach (int slot in game.GetLocalPlayerSlots()) {
            QuantumRunner.DefaultGame.SendCommand(slot, new CommandChangePlayerData {
                EnabledChanges = CommandChangePlayerData.Changes.Team,
                Team = (byte) selected,
            });
        }
        
        Close(false);

        TeamAsset teamScriptable = game.Configurations.Simulation.Teams[selected];
        flag.sprite = Settings.Instance.GraphicsColorblind ? teamScriptable.spriteColorblind : teamScriptable.spriteNormal;

        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
        }
    }

    public unsafe void Open() {
        QuantumGame game = QuantumRunner.DefaultGame;
        Frame f = game.Frames.Predicted;
        var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

        TeamAsset[] teams = f.SimulationConfig.Teams;
        int selected = Mathf.Clamp(playerData->Team, 0, teams.Length);
        blockerInstance = Instantiate(blockerTemplate, baseCanvas.transform);
        blockerInstance.SetActive(true);
        content.SetActive(true);

        foreach (TeamButton button in buttons)
            button.OnDeselect(null);

        EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);

        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Cursor);
        }
    }

    public void Close(bool playSound) {
        if (!blockerInstance) {
            return;
        }

        Destroy(blockerInstance);
        EventSystem.current.SetSelectedGameObject(gameObject);
        content.SetActive(false);

        if (playSound && MainMenuManager.Instance) {
            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Back);
        }
    }

    private unsafe void OnColorblindModeChanged() {
        QuantumGame game = QuantumRunner.DefaultGame;
        Frame f = game.Frames.Predicted;
        var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

        TeamAsset[] teams = f.SimulationConfig.Teams;
        int selected = playerData->Team % teams.Length;
        flag.sprite = Settings.Instance.GraphicsColorblind ? teams[selected].spriteColorblind : teams[selected].spriteNormal;
    }
}
