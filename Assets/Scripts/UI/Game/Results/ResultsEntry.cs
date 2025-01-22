using JimmysUnityUtilities;
using NSMB.Utils;
using Quantum;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultsEntry : MonoBehaviour {

    //---Seriaized Variables
    [SerializeField] private Image[] recolorables;
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text usernameText, starCountText;
    [SerializeField] private RectTransform childTransform;
    [SerializeField] private GameObject fullSlot, emptySlot;

    [SerializeField, ColorUsage(false)] private Color firstPlaceColor, secondPlaceColor, thirdPlaceColor, unrankedColor;
    [SerializeField] private float offscreenStartPosition = -500, slideInTimeSeconds = 0.1f;
    [SerializeField, ColorUsage(false)] private Color normalStarColor, outColor;

    public unsafe void Initialize(Frame f, PlayerRef owner, int ranking, float delay) {
        RuntimePlayer runtimePlayer = f.GetPlayerData(owner);
        PlayerData* data = QuantumUtils.GetPlayerData(f, owner);

        bool occupied = data != null;
        fullSlot.SetActive(occupied);
        emptySlot.SetActive(!occupied);

        if (occupied) {
            usernameText.text = runtimePlayer.PlayerNickname.ToValidUsername(f, owner);
            characterIcon.sprite = f.SimulationConfig.CharacterDatas[data->Character].ReadySprite;


        }

        Color color = Utils.GetPlayerColor(f, owner, considerDisqualifications: false);
        foreach (var recolorable in recolorables) {
            recolorable.color = color;
        }

        StartCoroutine(ResultsHandler.MoveObjectToTarget(childTransform, offscreenStartPosition, 0, slideInTimeSeconds, delay));
    }
}
