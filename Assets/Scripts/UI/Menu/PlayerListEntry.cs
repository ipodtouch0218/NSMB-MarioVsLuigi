using NSMB.Extensions;
using NSMB.Utils;
using Quantum;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class PlayerListEntry : MonoBehaviour {

        //---Static Variables
        public static event Action<PlayerListEntry> PlayerMuteStateChanged;

        //---Public Variables
        public PlayerRef player;
        public float typingCounter;

        //---Serialized Variables
        [SerializeField] private TMP_Text nameText, pingText, winsText, muteButtonText;
        [SerializeField] private Image colorStrip;
        [SerializeField] private RectTransform background, optionsTransform;
        [SerializeField] private GameObject blockerTemplate, firstButton, chattingIcon, settingsIcon, readyIcon;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LayoutElement layout;
        [SerializeField] private GameObject[] allOptions, adminOnlyOptions, othersOnlyOptions;

        //---Private Variables
        private GameObject blockerInstance;
        private EntityRef playerDataEntity;
        private string userId;
        private string nicknameColor;
        private bool constantNicknameColor;

        public void OnEnable() {
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
            if (QuantumRunner.DefaultGame != null) {
                UpdateText(QuantumRunner.DefaultGame.Frames.Predicted);
            }
        }

        public void OnDisable() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
        }

        public void OnDestroy() {
            if (blockerInstance) {
                Destroy(blockerInstance);
            }
        }

        public void Start() {
            RuntimePlayer runtimePlayer = QuantumRunner.DefaultGame.Frames.Predicted.GetPlayerData(player);
            nicknameColor = runtimePlayer?.NicknameColor ?? "#FFFFFF"; 
            userId = runtimePlayer?.UserId;
            nameText.color = Utils.Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);

            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged, onlyIfActiveAndEnabled: true);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView, onlyIfActiveAndEnabled: true);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            if (!constantNicknameColor) {
                nameText.color = Utils.Utils.SampleNicknameColor(nicknameColor, out _);
            }

            if (typingCounter > 0 && !ChatManager.Instance.mutedPlayers.Contains(userId)) {
                chattingIcon.SetActive(true);
                typingCounter -= Time.deltaTime;
            } else {
                chattingIcon.SetActive(false);
                typingCounter = 0;
            }

            // UpdateText(e.Game.Frames.Predicted);
        }

        private static readonly StringBuilder Builder = new();
        public unsafe void UpdateText(Frame f) {
            colorStrip.color = Utils.Utils.GetPlayerColor(f, player);
            var playerData = QuantumUtils.GetPlayerData(f, player);

            if (playerData == null) {
                return;
            }

            // Wins text
            if (playerData->Wins == 0) {
                winsText.text = "";
            } else {
                winsText.text = "<sprite name=room_wins>" + playerData->Wins;
            }

            // Ping text
            pingText.text = playerData->Ping + " " + Utils.Utils.GetPingSymbol(playerData->Ping);

            // Name text
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            Builder.Clear();

            if (ChatManager.Instance.mutedPlayers.Contains(runtimePlayer.UserId)) {
                Builder.Append("<sprite name=player_muted>");
            }

            if (playerData->IsRoomHost) {
                Builder.Append("<sprite name=room_host>");
            }

            int characterIndex = playerData->Character;
            characterIndex %= GlobalController.Instance.config.CharacterDatas.Length;
            Builder.Append(GlobalController.Instance.config.CharacterDatas[characterIndex].UiString);

            if (f.Global->Rules.TeamsEnabled && Settings.Instance.GraphicsColorblind) {
                TeamAsset team = f.SimulationConfig.Teams[playerData->Team];
                Builder.Append(team.textSpriteColorblindBig);
            }

            Builder.Append(runtimePlayer.PlayerNickname.ToValidUsername(f, player));
            nameText.text = Builder.ToString();

            Transform parent = transform.parent;
            int childIndex = 0;
            for (int i = 0; i < parent.childCount; i++) {
                if (parent.GetChild(i) != transform) {
                    continue;
                }

                childIndex = i;
                break;
            }

            layout.layoutPriority = transform.parent.childCount - childIndex;
        }

        public unsafe void ShowDropdown() {
            if (blockerInstance) {
                Destroy(blockerInstance);
            }

            foreach (var option in allOptions) {
                option.SetActive(true);
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            bool adminOptions = false;
            foreach (PlayerRef localPlayer in game.GetLocalPlayers()) {
                if (QuantumUtils.GetPlayerData(game.Frames.Predicted, localPlayer)->IsRoomHost) {
                    adminOptions = true;
                    break;
                }
            }

            if (!adminOptions) {
                foreach (var option in adminOnlyOptions) {
                    option.SetActive(false);
                }
            }

            bool othersOptions = !game.PlayerIsLocal(player);
            if (!othersOptions) {
                foreach (var option in othersOnlyOptions) {
                    option.SetActive(false);
                }
            }

            Canvas.ForceUpdateCanvases();

            blockerInstance = Instantiate(blockerTemplate, rootCanvas.transform);
            RectTransform blockerTransform = blockerInstance.GetComponent<RectTransform>();
            blockerTransform.offsetMax = blockerTransform.offsetMin = Vector2.zero;
            blockerInstance.SetActive(true);

            background.offsetMin = new(background.offsetMin.x, -optionsTransform.rect.height);
            optionsTransform.anchoredPosition = new(optionsTransform.anchoredPosition.x, -optionsTransform.rect.height);

            EventSystem.current.SetSelectedGameObject(firstButton);
            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Cursor);
        }

        public void HideDropdown(bool didAction) {
            Destroy(blockerInstance);

            background.offsetMin = new(background.offsetMin.x, 0);
            optionsTransform.anchoredPosition = new(optionsTransform.anchoredPosition.x, 0);

            MainMenuManager.Instance.sfx.PlayOneShot(didAction ? SoundEffect.UI_Decide : SoundEffect.UI_Back);
        }

        public void BanPlayer() {
            // TODO MainMenuManager.Instance.Ban(player);
            HideDropdown(true);
        }

        public void KickPlayer() {
            // TODO MainMenuManager.Instance.Kick(player);
            HideDropdown(true);
        }

        public void MutePlayer() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            if (runtimePlayer != null) {
                HashSet<string> mutedPlayers = ChatManager.Instance.mutedPlayers;
                if (mutedPlayers.Contains(userId)) {
                    mutedPlayers.Remove(userId);
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.unmuted", ChatManager.Blue, "playername", runtimePlayer.PlayerNickname.ToValidUsername(f, player));
                    muteButtonText.text = GlobalController.Instance.translationManager.GetTranslation("ui.inroom.player.mute");
                } else {
                    mutedPlayers.Add(userId);
                    ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.muted", ChatManager.Blue, "playername", runtimePlayer.PlayerNickname.ToValidUsername(f, player));
                    muteButtonText.text = GlobalController.Instance.translationManager.GetTranslation("ui.inroom.player.unmute");
                }
            }

            PlayerMuteStateChanged?.Invoke(this);
            UpdateText(f);
            HideDropdown(true);
        }

        public void PromotePlayer() {
            QuantumRunner.DefaultGame.SendCommand(new CommandChangeHost {
                NewHost = player,
            });
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            if (runtimePlayer != null) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.player.promoted", ChatManager.Blue, "playername", runtimePlayer.PlayerNickname.ToValidUsername(f, player));
            }
            HideDropdown(true);
        }

        public void CopyPlayerId() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            RuntimePlayer runtimePlayer = f.GetPlayerData(player);

            TextEditor te = new() {
                text = runtimePlayer.UserId.ToString(),
            };
            te.SelectAll();
            te.Copy();
            HideDropdown(true);
        }

        //---Callbacks
        private void OnColorblindModeChanged() {
            UpdateText(QuantumRunner.DefaultGame.Frames.Predicted);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.PreGameRoom) {
                UpdateText(e.Frame);
            }
        }

        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (e.Player != player) {
                return;
            }

            UpdateText(e.Frame);

            var playerData = QuantumUtils.GetPlayerData(e.Frame, e.Player);
            readyIcon.SetActive(playerData->IsReady);
            settingsIcon.SetActive(playerData->IsInSettings);
        }
    }
}
