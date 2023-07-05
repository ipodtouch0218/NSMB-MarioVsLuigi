using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using NSMB.Extensions;

public class TeamButton : MonoBehaviour, ISelectHandler, IDeselectHandler {

    //---Serialized Variables
    [SerializeField] private Sprite overlayUnpressed, overlayPressed;
    [SerializeField] private Image overlay, flag;
    [SerializeField] public int index;

    public void OnEnable() {
        PlayerData data = NetworkHandler.Instance.runner.GetLocalPlayerData();
        overlay.enabled = (Mathf.Clamp(data.Team, 0, 4) == index);

        Team team = ScriptableManager.Instance.teams[index];
        flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
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
}
