using NSMB.Utils;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultsEntry : MonoBehaviour {

    //---Seriaized Variables
    [SerializeField] private Image leftHalf, rightHalf;
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text usernameText, starCountText;
    [SerializeField] private RectTransform childTransform;
    [SerializeField] private GameObject fullSlot, emptySlot;
    [SerializeField] private Color firstPlaceColor, secondPlaceColor, thirdPlaceColor, unrankedColor;
    [SerializeField] private float offscreenStartPosition = -500, slideInTimeSeconds = 0.1f;

    //---Private Variables
    private string nicknameColor;
    private bool constantNicknameColor;

    public unsafe void Initialize(Frame f, in PlayerInformation? info, int ranking, float delay, int stars = -1) {
        bool occupied = info.HasValue;
        fullSlot.SetActive(occupied);
        emptySlot.SetActive(!occupied);

        if (occupied) {
            PlayerRef player = info.Value.PlayerRef;

            usernameText.text = info.Value.Nickname.ToString().ToValidUsername(f, player);
            nicknameColor = info.Value.NicknameColor.ToString();
            usernameText.color = Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
            characterIcon.sprite = f.SimulationConfig.CharacterDatas[info.Value.Character].ReadySprite;

            if (stars < 0) {
                starCountText.text = "<sprite name=results_out>";
                rightHalf.color = unrankedColor;
            } else {
                starCountText.text = Utils.GetSymbolString("S" + stars.ToString(), Utils.resultsSymbols);
                rightHalf.color = ranking switch {
                    1 => firstPlaceColor,
                    2 => secondPlaceColor,
                    3 => thirdPlaceColor,
                    _ => unrankedColor
                };
            }

            leftHalf.color = Utils.GetPlayerColor(f, player, s: 0.7f, considerDisqualifications: false);
        } else {
            constantNicknameColor = true;
            leftHalf.color = rightHalf.color = unrankedColor;
        }

        StartCoroutine(ResultsHandler.MoveObjectToTarget(childTransform, -1.25f, 0, slideInTimeSeconds, delay));
    }

    public void Update() {
        if (!constantNicknameColor) {
            usernameText.color = Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
        }
    }
}
