using JetBrains.Annotations;
using NSMB.Translation;
using Photon.Client;
using Photon.Realtime;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NSMB.Utils.NetworkUtils;

namespace NSMB.UI.MainMenu {
    public unsafe class RoomSettingsCallbacks : MonoBehaviour {

        //---Serailized Variables
        [SerializeField] private TMP_Dropdown levelDropdown;
        [SerializeField] private TMP_InputField starsInputField, coinsInputField, livesInputField, timerInputField;
        [SerializeField] private TMP_Text playersCount, roomIdText, roomIdToggleButtonText;
        [SerializeField] private Slider playersSlider;
        [SerializeField] private Toggle privateEnabledToggle, timerEnabledToggle, livesEnabledToggle, drawEnabledToggle, teamsEnabledToggle, customPowerupsEnabledToggle;
        [SerializeField] private TeamChooser teamSelectorButton;

        //---Private Variables
        private bool isRoomCodeVisible;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public void UpdateRealtimeRoomProperties(QuantumGame game) {
            if (!IsHostLocal(game, out _)) {
                return;
            }

            Frame f = game.Frames.Predicted;
            var rules = f.Global->Rules;
            IntegerProperties intProperties = new IntegerProperties {
                Level = 0, // TODO
                StarRequirement = rules.StarsToWin,
                CoinRequirement = rules.CoinsForPowerup,
                Lives = rules.Lives,
                Timer = rules.TimerSeconds,
            };
            BooleanProperties boolProperties = new BooleanProperties {
                Teams = rules.TeamsEnabled,
                CustomPowerups = rules.CustomPowerupsEnabled,
                DrawOnTimeUp = rules.DrawOnTimeUp,
                
            };

            NetworkHandler.Client.CurrentRoom.SetCustomProperties(new PhotonHashtable {
                [Enums.NetRoomProperties.IntProperties] = (int) intProperties,
                [Enums.NetRoomProperties.BoolProperties] = (int) boolProperties,
            });
        }

        public void RefreshSettingsUI(QuantumGame game, Frame f, bool sendMessage) {
            // Realtime rules
            Room realtimeRoom = NetworkHandler.Client.CurrentRoom;
            ChangePrivate(!realtimeRoom.IsVisible);
            ChangeMaxPlayers(realtimeRoom.MaxPlayers);
           
            var rules = f.Global->Rules;
            ChangeLevelIndex(rules.Level, sendMessage);
            ChangeStarRequirement(rules.StarsToWin);
            ChangeCoinRequirement(rules.CoinsForPowerup);
            ChangeLives(rules.Lives);
            ChangeTime(rules.TimerSeconds);
            ChangeTeams(rules.TeamsEnabled);
            ChangeDrawOnTimeUp(rules.DrawOnTimeUp);
            ChangeCustomPowerups(rules.CustomPowerupsEnabled);
            SetRoomIdVisibility(isRoomCodeVisible);

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries(f);
                MainMenuManager.Instance.UpdateStartGameButton(game);
            }
        }

        #region Level Index
        public void SetLevelIndex() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            int index = levelDropdown.value;
            MainMenuManager.MapData mapData = MainMenuManager.Instance.maps[index];

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.Level,
                Level = mapData.mapAsset,
            });
        }
        private void ChangeLevelIndex(AssetRef<Map> newMap, bool changed) {
            MainMenuManager mm = MainMenuManager.Instance;
            int newDropdownIndex = mm.maps.IndexOf(md => md.mapAsset == newMap);

            if (changed && (levelDropdown.value != newDropdownIndex || IsHostLocal(QuantumRunner.DefaultGame, out _))) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.server.map", ChatManager.Red, "map", mm.maps[newDropdownIndex].translationKey);
            }

            mm.PreviewLevel(newDropdownIndex);
            levelDropdown.SetValueWithoutNotify(newDropdownIndex);
        }
        #endregion

        #region Stars
        public void SetStarRequirement() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            if (!int.TryParse(starsInputField.text, out int newValue)) {
                return;
            }
            newValue = Mathf.Clamp(newValue, 1, 25);

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.StarsToWin,
                StarsToWin = (byte) newValue,
            });
        }
        private void ChangeStarRequirement(int stars) {
            starsInputField.SetTextWithoutNotify(stars.ToString());
        }
        #endregion

        #region Coins
        public void SetCoinRequirement() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            if (!int.TryParse(coinsInputField.text, out int newValue)) {
                return;
            }
            newValue = Mathf.Clamp(newValue, 3, 25);

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.CoinsForPowerup,
                CoinsForPowerup = (byte) newValue,
            });
        }
        private void ChangeCoinRequirement(int coins) {
            coinsInputField.SetTextWithoutNotify(coins.ToString());
        }
        #endregion

        #region Lives
        public unsafe void SetLives() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            if (!int.TryParse(livesInputField.text, out int newValue)) {
                livesInputField.text = ((int) game.Frames.Predicted.Global->Rules.Lives).ToString();
                return;
            }
            newValue = Mathf.Clamp(newValue, 1, 25);

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.Lives,
                Lives = (byte) newValue,
            });
            ChangeLives(newValue);
        }

        public void EnableLives() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            int newValue = livesEnabledToggle.isOn ? Mathf.Clamp(int.Parse(livesInputField.text), 1, 25) : 0;
            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.Lives,
                Lives = (byte) newValue,
            });
        }

        private void ChangeLives(int lives) {
            bool enabled = lives > 0;
            livesEnabledToggle.SetIsOnWithoutNotify(enabled);
            livesInputField.interactable = enabled;

            if (enabled) {
                livesInputField.SetTextWithoutNotify(lives.ToString());
            }
        }
        #endregion

        #region Timer
        public void ClickTime() {
            int colon = timerInputField.text.IndexOf(':');
            if (colon != -1) {
                timerInputField.text = timerInputField.text[0..colon];
            }
        }

        public void SetTime() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            if (!int.TryParse(timerInputField.text.Split(':')[0], out int newValue)) {
                livesInputField.text = ((int) game.Frames.Predicted.Global->Rules.TimerSeconds).ToString();
                return;
            }

            newValue = Mathf.Clamp(newValue, 1, 99);
            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.TimerSeconds,
                TimerSeconds = (byte) newValue,
            });
        }

        public void EnableTime() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            if (!int.TryParse(timerInputField.text.Split(':')[0], out int newValue)) {
                newValue = 5;
            }

            newValue = timerEnabledToggle.isOn ? Mathf.Clamp(newValue, 1, 99) : 0;
            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.TimerSeconds,
                TimerSeconds = (byte) newValue,
            });
        }

        private void ChangeTime(int time) {
            timerEnabledToggle.SetIsOnWithoutNotify(time > 0);
            timerInputField.interactable = time > 0;
            drawEnabledToggle.interactable = time > 0;

            if (time <= 0) {
                return;
            }

            timerInputField.SetTextWithoutNotify($"{time}:00");
        }
        #endregion

        #region DrawOnTimeUp
        public void SetDrawOnTimeUp() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.DrawOnTimeUp,
                DrawOnTimeUp = drawEnabledToggle.isOn,
            });
        }
        private void ChangeDrawOnTimeUp(bool value) {
            drawEnabledToggle.SetIsOnWithoutNotify(value);
        }
        #endregion

        #region Teams
        public void SetTeams() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;

            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.TeamsEnabled,
                TeamsEnabled = teamsEnabledToggle.isOn,
            });

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries(f);
            }
        }
        private void ChangeTeams(bool value) {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;

            teamsEnabledToggle.SetIsOnWithoutNotify(value);
            teamSelectorButton.SetEnabled(value);

            if (!teamsEnabledToggle.isOn && value) {
                MainMenuManager.Instance.playerList.UpdateAllPlayerEntries(f);
            }
        }
        #endregion

        #region Custom Powerups
        public void SetCustomPowerups() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (!IsHostLocal(game, out int hostSlot)) {
                // Only hosts can change.
                return;
            }

            game.SendCommand(hostSlot, new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Changes.CustomPowerupsEnabled,
                CustomPowerupsEnabled = customPowerupsEnabledToggle.isOn,
            });
        }
        private void ChangeCustomPowerups(bool value) {
            customPowerupsEnabledToggle.SetIsOnWithoutNotify(value);
        }
        #endregion

        #region Players
        public void SetMaxPlayers() {
            Room room = NetworkHandler.Client.CurrentRoom;

            int oldValue = room.MaxPlayers;
            int newValue = (int) playersSlider.value;

            newValue = Mathf.Clamp(newValue, Mathf.Max(2, room.PlayerCount), 10);

            if (newValue == oldValue) {
                ChangeMaxPlayers(oldValue);
                return;
            }

            room.MaxPlayers = (byte) newValue;
            ChangeMaxPlayers(newValue);
        }
        private void ChangeMaxPlayers(int value) {
            playersSlider.SetValueWithoutNotify(value);
            playersCount.text = value.ToString();
        }
        #endregion

        /*
        #region Win Counter
        public void ClearWinCounters() {
            if (!Room.HasStateAuthority) {
                return;
            }

            foreach ((_, PlayerData data) in Room.PlayerDatas) {
                data.Wins = 0;
            }
        }
        #endregion
        */

        #region Private
        public void SetPrivate() {
            bool newValue = privateEnabledToggle.isOn;
            NetworkHandler.Client.CurrentRoom.IsVisible = !newValue;
        }
        private void ChangePrivate(bool value) {
            privateEnabledToggle.SetIsOnWithoutNotify(value);
        }
        public void CopyRoomCode() {
            TextEditor te = new() {
                text = NetworkHandler.Client.CurrentRoom.Name
            };
            te.SelectAll();
            te.Copy();
        }
        #endregion

        #region Room ID
        public void ToggleRoomIdVisibility() {
            SetRoomIdVisibility(!isRoomCodeVisible);
        }

        public void SetRoomIdVisibility(bool newValue) {
            isRoomCodeVisible = newValue;
            roomIdToggleButtonText.text = GlobalController.Instance.translationManager.GetTranslation(isRoomCodeVisible ? "ui.generic.hide" : "ui.generic.show");
            roomIdText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.inroom.settings.room.roomid", "id", isRoomCodeVisible ? NetworkHandler.Client.CurrentRoom.Name : "ui.inroom.settings.room.roomidhidden");
        }
        #endregion

        private bool IsHostLocal(QuantumGame game, out int slot) {
            Frame f = game.Frames.Predicted;
            PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(f, out _);
            if (!game.PlayerIsLocal(hostPlayer)) {
                // Only hosts can change.
                slot = 0;
                return false;
            }
            slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(hostPlayer)];
            return true;
        }

        //---Callbacks
        private void OnLanguageChanged(TranslationManager tm) {
            SetRoomIdVisibility(isRoomCodeVisible);
            roomIdText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        private void OnRulesChanged(EventRulesChanged e) {
            RefreshSettingsUI(e.Game, e.Frame, e.LevelChanged);
            UpdateRealtimeRoomProperties(e.Game);
        }
    }
}
