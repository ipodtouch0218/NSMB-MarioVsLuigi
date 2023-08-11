using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

namespace NSMB.UI.MainMenu {
    public class PlayerListEntry : MonoBehaviour {

        //---Public Variables
        public PlayerData player;
        public float typingCounter;

        //---Serialized Variables
        [SerializeField] private TMP_Text nameText, pingText, winsText;
        [SerializeField] private Image colorStrip;
        [SerializeField] private RectTransform background, options;
        [SerializeField] private GameObject blockerTemplate, firstButton, chattingIcon, settingsIcon;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LayoutElement layout;
        [SerializeField] private GameObject[] adminOnlyOptions;

        //---Private Variables
        private GameObject blockerInstance;
        private NicknameColor nicknameColor;

        public void OnEnable() {
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
            player.OnInOptionsChangedEvent += OnInSettingsChanged;

            OnInSettingsChanged(player.IsInOptions);
        }

        public void OnDisable() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
            player.OnInOptionsChangedEvent -= OnInSettingsChanged;
        }

        public void OnDestroy() {
            if (blockerInstance)
                Destroy(blockerInstance);
        }

        public void Start() {
            nicknameColor = player.NicknameColor;
            nameText.color = nicknameColor.color;
        }

        public void Update() {
            if (nicknameColor.isRainbow) {
                nameText.color = Utils.Utils.GetRainbowColor(NetworkHandler.Runner);
            }

            if (typingCounter > 0 && !player.IsMuted) {
                chattingIcon.SetActive(true);
                typingCounter -= Time.deltaTime;
            } else {
                chattingIcon.SetActive(false);
                typingCounter = 0;
            }
        }

        public void UpdateText() {
            NetworkRunner runner = NetworkHandler.Instance.runner;

            colorStrip.color = Utils.Utils.GetPlayerColor(runner, player);

            if (player.Wins == 0) {
                winsText.text = "";
            } else {
                winsText.text = "<sprite name=room_wins>" + player.Wins;
            }

            string permissionSymbol = "";
            if (player.IsRoomOwner) {
                permissionSymbol += "<sprite name=room_host>";
                pingText.text = "";
            } else {
                int ping = player.Ping;
                pingText.text = ping + " " + Utils.Utils.GetPingSymbol(ping);
            }

            string characterSymbol = player.GetCharacterData().uistring;

            string teamSymbol;
            if (SessionData.Instance.Teams && Settings.Instance.GraphicsColorblind) {
                Team team = ScriptableManager.Instance.teams[player.Team];
                teamSymbol = team.textSpriteColorblindBig;
            } else {
                teamSymbol = "";
            }
            nameText.text = permissionSymbol + characterSymbol + teamSymbol + player.GetNickname();

            Transform parent = transform.parent;
            int childIndex = 0;
            for (int i = 0; i < parent.childCount; i++) {
                if (parent.GetChild(i) != gameObject)
                    continue;

                childIndex = i;
                break;
            }

            layout.layoutPriority = transform.parent.childCount - childIndex;
        }

        public void ShowDropdown() {
            NetworkRunner runner = NetworkHandler.Instance.runner;
            if (blockerInstance)
                Destroy(blockerInstance);

            bool admin = runner.IsServer && runner.LocalPlayer != player.Object.InputAuthority;
            foreach (GameObject option in adminOnlyOptions) {
                option.SetActive(admin);
            }

            Canvas.ForceUpdateCanvases();

            blockerInstance = Instantiate(blockerTemplate, rootCanvas.transform);
            RectTransform blockerTransform = blockerInstance.GetComponent<RectTransform>();
            blockerTransform.offsetMax = blockerTransform.offsetMin = Vector2.zero;
            blockerInstance.SetActive(true);

            background.offsetMin = new(background.offsetMin.x, -options.rect.height);
            options.anchoredPosition = new(options.anchoredPosition.x, -options.rect.height);

            EventSystem.current.SetSelectedGameObject(firstButton);
            MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor);
        }

        public void HideDropdown(bool didAction) {
            Destroy(blockerInstance);

            background.offsetMin = new(background.offsetMin.x, 0);
            options.anchoredPosition = new(options.anchoredPosition.x, 0);

            MainMenuManager.Instance.sfx.PlayOneShot(didAction ? Enums.Sounds.UI_Decide : Enums.Sounds.UI_Back);
        }

        public void BanPlayer() {
            MainMenuManager.Instance.Ban(player);
            HideDropdown(true);
        }

        public void KickPlayer() {
            MainMenuManager.Instance.Kick(player);
            HideDropdown(true);
        }

        public void MutePlayer() {
            MainMenuManager.Instance.Mute(player);
            HideDropdown(true);
        }

        public void PromotePlayer() {
            MainMenuManager.Instance.Promote(player);
            HideDropdown(true);
        }

        public void CopyPlayerId() {
            TextEditor te = new() {
                text = player.GetUserIdString()
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
    }
}
