using NSMB.Extensions;
using NSMB.Utils;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class PlayerListEntry : MonoBehaviour {

        //---Public Variables
        public PlayerRef player;
        public float typingCounter;

        //---Serialized Variables
        [SerializeField] private TMP_Text nameText, pingText, winsText;
        [SerializeField] private Image colorStrip;
        [SerializeField] private RectTransform background, options;
        [SerializeField] private GameObject blockerTemplate, firstButton, chattingIcon, settingsIcon, readyIcon;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LayoutElement layout;
        [SerializeField] private GameObject[] adminOnlyOptions;

        //---Private Variables
        private GameObject blockerInstance;
        private EntityRef playerDataEntity;
        // TODO private NicknameColor NicknameColor => player.NicknameColor;
        private NicknameColor NicknameColor => NicknameColor.White;

        public void OnEnable() {
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
            // TODO
            // player.OnInOptionsChangedEvent += OnInSettingsChanged;
            // player.OnIsReadyChangedEvent += OnIsReadyChanged;
            // OnInSettingsChanged(player.IsInOptions);
        }

        public void OnDisable() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
            // TODO
            // player.OnInOptionsChangedEvent -= OnInSettingsChanged;
            // player.OnIsReadyChangedEvent -= OnIsReadyChanged;
        }

        public void OnDestroy() {
            if (blockerInstance) {
                Destroy(blockerInstance);
            }
        }

        public void Start() {
            nameText.color = NicknameColor.color;

            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged, onlyIfActiveAndEnabled: true);
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView, onlyIfActiveAndEnabled: true);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            nameText.color = NicknameColor.color;
            if (NicknameColor.isRainbow) {
                nameText.color = Utils.Utils.GetRainbowColor();
            }

            if (typingCounter > 0 /* TODO && !player.IsMuted */) {
                chattingIcon.SetActive(true);
                typingCounter -= Time.deltaTime;
            } else {
                chattingIcon.SetActive(false);
                typingCounter = 0;
            }

            // UpdateText(e.Game.Frames.Predicted);
        }

        public unsafe void UpdateText(Frame f) {
            colorStrip.color = Utils.Utils.GetPlayerColor(f, player);

            var playerData = QuantumUtils.GetPlayerData(f, player);

            if (playerData == null) {
                return;
            }

            if (playerData->Wins == 0) {
                winsText.text = "";
            } else {
                winsText.text = "<sprite name=room_wins>" + playerData->Wins;
            }

            pingText.text = playerData->Ping + " " + Utils.Utils.GetPingSymbol(playerData->Ping);

            string permissionSymbol = "";
            if (playerData->IsRoomHost) {
                permissionSymbol += "<sprite name=room_host>";
            }

            int characterIndex = playerData->Character;
            characterIndex %= GlobalController.Instance.config.CharacterDatas.Length;
            string characterSymbol = GlobalController.Instance.config.CharacterDatas[characterIndex].UiString;

            string teamSymbol;
            if (f.Global->Rules.TeamsEnabled && Settings.Instance.GraphicsColorblind) {
                TeamAsset team = f.SimulationConfig.Teams[playerData->Team];
                teamSymbol = team.textSpriteColorblindBig;
            } else {
                teamSymbol = "";
            }

            RuntimePlayer runtimePlayer = f.GetPlayerData(player);
            nameText.text = permissionSymbol + characterSymbol + teamSymbol + runtimePlayer.PlayerNickname.ToValidUsername();

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

        public void ShowDropdown() {
            if (blockerInstance) {
                Destroy(blockerInstance);
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            bool adminOptions = NetworkHandler.Client.LocalPlayer.IsMasterClient && !game.PlayerIsLocal(player);
            foreach (GameObject option in adminOnlyOptions) {
                option.SetActive(adminOptions);
            }

            Canvas.ForceUpdateCanvases();

            blockerInstance = Instantiate(blockerTemplate, rootCanvas.transform);
            RectTransform blockerTransform = blockerInstance.GetComponent<RectTransform>();
            blockerTransform.offsetMax = blockerTransform.offsetMin = Vector2.zero;
            blockerInstance.SetActive(true);

            background.offsetMin = new(background.offsetMin.x, -options.rect.height);
            options.anchoredPosition = new(options.anchoredPosition.x, -options.rect.height);

            EventSystem.current.SetSelectedGameObject(firstButton);
            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Cursor);
        }

        public void HideDropdown(bool didAction) {
            Destroy(blockerInstance);

            background.offsetMin = new(background.offsetMin.x, 0);
            options.anchoredPosition = new(options.anchoredPosition.x, 0);

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
            // TODO MainMenuManager.Instance.Mute(player);
            HideDropdown(true);
        }

        public void PromotePlayer() {
            // TODO MainMenuManager.Instance.Promote(player);
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
