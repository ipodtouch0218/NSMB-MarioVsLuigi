using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using NSMB.Extensions;
using NSMB.UI.MainMenu;

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

        PlayerData data = NetworkHandler.Instance.runner.GetLocalPlayerData();
        data.Rpc_SetTeamNumber((sbyte) selected);
        Close(false);
        Team teamScriptable = ScriptableManager.Instance.teams[selected];
        flag.sprite = Settings.Instance.GraphicsColorblind ? teamScriptable.spriteColorblind : teamScriptable.spriteNormal;

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Decide);
    }

    public void Open() {
        PlayerData data = NetworkHandler.Instance.runner.GetLocalPlayerData();
        int selected = Mathf.Clamp(data.Team, 0, 4);
        blockerInstance = Instantiate(blockerTemplate, baseCanvas.transform);
        blockerInstance.SetActive(true);
        content.SetActive(true);

        foreach (TeamButton button in buttons)
            button.OnDeselect(null);

        EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor);
    }

    public void Close(bool playSound) {
        if (!blockerInstance)
            return;

        Destroy(blockerInstance);
        EventSystem.current.SetSelectedGameObject(gameObject);
        content.SetActive(false);

        if (playSound && MainMenuManager.Instance)
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Back);
    }

    private void OnColorblindModeChanged() {
        if (!button.interactable || !NetworkHandler.Runner.GetLocalPlayerData()) return;

        int selected = NetworkHandler.Runner.GetLocalPlayerData().Team % 5;
        Team teamScriptable = ScriptableManager.Instance.teams[selected];
        flag.sprite = Settings.Instance.GraphicsColorblind ? teamScriptable.spriteColorblind : teamScriptable.spriteNormal;
    }
}
