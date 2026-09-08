using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Results {
    public class ResultsEntry : MonoBehaviour {

        //---Seriaized Variables
        [SerializeField] private Image leftHalf, rightHalf;
        [SerializeField] private Image characterIcon, teamSprite;
        [SerializeField] private TMP_Text usernameText, starCountText;
        [SerializeField] private RectTransform childTransform;
        [SerializeField] private GameObject fullSlot, emptySlot;
        [SerializeField] private GameObject readyCheckmark;
        [SerializeField] private Color firstPlaceColor, secondPlaceColor, thirdPlaceColor, unrankedColor;
        [SerializeField] private float slideInTimeSeconds = 0.1f;

        //---Private Variables
        private PlayerRef player;
        private NicknameColor nicknameColor = NicknameColor.White;
        private int? index;
        private PlayerInformation? playerInfo;

        public void Start() {
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
        }

        public void OnDestroy() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
        }

        public unsafe void Initialize(Frame f, GamemodeAsset gamemode, int? playerInfoIndex, int ranking, float delay, int stars = -1) {
            index = playerInfoIndex;
            bool occupied = index.HasValue;
            if (occupied) {
                playerInfo = f.Global->PlayerInfo[index.Value];
            }
            fullSlot.SetActive(occupied);
            emptySlot.SetActive(!occupied);

            if (occupied) {
                player = playerInfo.Value.PlayerRef;

                usernameText.text = playerInfo.Value.Nickname.ToString().ToValidNickname(f, player);
                nicknameColor = NicknameColor.Parse(playerInfo.Value.NicknameColor.ToString());
                usernameText.color = nicknameColor.Sample();
                characterIcon.sprite = QuantumViewUtils.FindAssetOrDefault(playerInfo.Value.Character).ReadySprite;
                OnColorblindModeChanged();
                
                if (stars < 0) {
                    starCountText.text = "<sprite name=results_out>";
                    rightHalf.color = unrankedColor;
                } else {
                    starCountText.text = Utils.GetSymbolString(gamemode.ObjectiveSymbolPrefix + stars.ToString(), Utils.resultsSymbols);
                    rightHalf.color = ranking switch {
                        1 => firstPlaceColor,
                        2 => secondPlaceColor,
                        3 => thirdPlaceColor,
                        _ => unrankedColor
                    };
                }

                leftHalf.color = Utils.GetPlayerColor(f, player, s: 0.7f, considerDisqualifications: false);
                
                var playerData = QuantumUtils.GetPlayerData(f, player);
                readyCheckmark.SetActive(playerData != null && playerData->VotedToContinue);
            } else {
                player = PlayerRef.None;
                nicknameColor = NicknameColor.White;
                leftHalf.color = rightHalf.color = unrankedColor;
                readyCheckmark.SetActive(false);
            }

            StartCoroutine(ResultsHandler.MoveObjectToTarget(childTransform, -1.25f, 0, slideInTimeSeconds, delay));
        }

        public void Update() {
            if (!nicknameColor.Constant) {
                usernameText.color = nicknameColor.Sample();
            }
        }

        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (e.Player != player) {
                return;
            }

            var playerData = QuantumUtils.GetPlayerData(e.Game.Frames.Predicted, player);
            readyCheckmark.SetActive(playerData->VotedToContinue);
        }

        private unsafe void OnColorblindModeChanged() {
            if (playerInfo is not PlayerInformation info) {
                return;
            }

            bool showSymbol = Settings.Instance.GraphicsColorblind;
            teamSprite.gameObject.SetActive(showSymbol);

            if (!showSymbol) {
                return;
            }

            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            if (f.Global->Rules.TeamsEnabled) {
                var teams = f.Context.GetAllAssets<TeamAsset>();
                if (info.Team < teams.Count) {
                    var team = teams[info.Team];
                    teamSprite.sprite = team.spriteColorblind;
                } else {
                    teamSprite.sprite = null;
                }
            } else {
                var slot = Utils.GetPlayerSlotInfo(index);
                if (slot) {
                    teamSprite.sprite = slot.Sprite;
                } else {
                    teamSprite.sprite = null;
                }
            }
        }
    }
}