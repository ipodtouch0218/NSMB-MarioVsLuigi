using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using NSMB.Extensions;
using NSMB.Utils;

public class TeamChooser : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Canvas baseCanvas;
    [SerializeField] private GameObject blockerTemplate, content, disabledIcon, normalIcon;
    [SerializeField] private TeamButton[] buttons;
    [SerializeField] private Button button;
    [SerializeField] private Image flagColor;

    //---Private Variables
    private GameObject blocker;

    public void SetEnabled(bool value) {
        button.interactable = value;
        normalIcon.SetActive(value);
        disabledIcon.SetActive(!value);

        if (!value)
            Close();
    }

    public void SelectTeam(TeamButton team) {
        PlayerData data = NetworkHandler.Instance.runner.GetLocalPlayerData();
        int selected = team.index;

        data.Rpc_SetTeamNumber((sbyte) selected);
        Close();
        flagColor.color = Utils.GetTeamColor(selected);
    }

    public void Open() {
        PlayerData data = NetworkHandler.Instance.runner.GetLocalPlayerData();
        int selected = Mathf.Clamp(data.Team, 0, 4);
        blocker = Instantiate(blockerTemplate, baseCanvas.transform);
        blocker.SetActive(true);
        content.SetActive(true);

        foreach (TeamButton button in buttons)
            button.OnDeselect(null);

        EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);
    }

    public void Close() {
        if (!blocker)
            return;

        Destroy(blocker);
        EventSystem.current.SetSelectedGameObject(gameObject);
        content.SetActive(false);
    }
}
