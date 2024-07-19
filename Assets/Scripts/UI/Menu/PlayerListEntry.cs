using NSMB.Extensions;
using NSMB.Utils;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class PlayerListEntry : MonoBehaviour {

        //---Public Variables
        public Player player;
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
        }

        public void Update() {
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
        }

        public void UpdateText() {

            colorStrip.color = Utils.Utils.GetPlayerColor(player);

            /*
            if (player.Wins == 0) {
                winsText.text = "";
            } else {
                winsText.text = "<sprite name=room_wins>" + player.Wins;
            }
            */

            int ping = player.CustomProperties[Enums.NetPlayerProperties.Ping] as int? ?? 0;
            if (ping == 0) {
                pingText.text = "";
            } else {
                pingText.text = ping + " " + Utils.Utils.GetPingSymbol(ping);
            }

            string permissionSymbol = "";
            if (player.IsMasterClient) {
                permissionSymbol += "<sprite name=room_host>";
            }

            /*
            string characterSymbol = player.GetCharacterData().uistring;

            string teamSymbol;
            if (SessionData.Instance.Teams && Settings.Instance.GraphicsColorblind) {
                Team team = ScriptableManager.Instance.teams[player.Team];
                teamSymbol = team.textSpriteColorblindBig;
            } else {
                teamSymbol = "";
            }
            */
            //nameText.text = permissionSymbol + characterSymbol + teamSymbol + player.GetNickname();

            nameText.text = permissionSymbol + player.NickName.ToValidUsername();

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

            bool adminOptions = NetworkHandler.Client.LocalPlayer.IsMasterClient && player != NetworkHandler.Client.LocalPlayer;
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
            TextEditor te = new() {
                text = player.UserId,
            };
            te.SelectAll();
            te.Copy();
            HideDropdown(true);
        }

        //---Callbacks
        private void OnColorblindModeChanged() {
            UpdateText();
        }

        private void OnInSettingsChanged(bool inSettings) {
            settingsIcon.SetActive(inSettings);
        }

        private void OnIsReadyChanged(bool isReady) {
            readyIcon.SetActive(isReady);
        }
    }
}
