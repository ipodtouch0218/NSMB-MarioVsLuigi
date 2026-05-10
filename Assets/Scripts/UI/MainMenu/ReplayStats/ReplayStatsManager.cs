using NSMB.Replay;
using NSMB.Replay.Stats;
using NSMB.UI.MainMenu.Submenus.Replays;
using NSMB.UI.MainMenu.Submenus.RoomList;
using NSMB.UI.Translation;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using NUnit.Framework;
using Quantum;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class ReplayStatsManager : Selectable {
        //---Static Variables
        public static ReplayStatsManager Instance { get; private set; }

        //---Properties
        private StatOptions ViewingStats => (StatOptions)viewingStatisticDropdown.value;
        private int TargetPlayer => targetPlayerDropdown.value;

        //---Serialized Variables
        [SerializeField] public MainMenuCanvas canvas;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TimePointEntry entryTemplate;
        [SerializeField] internal VerticalLayoutGroup layout;

        // side panel
        [SerializeField] private TMP_Dropdown viewingStatisticDropdown, targetPlayerDropdown;
        [SerializeField] private TMP_Text entryCount;

        // bottom panel
        [SerializeField] private TMP_Text replayInformation;

        //---Private Variables
        private ReplayListEntry replayListEntry;
        private readonly StringBuilder stringBuilder = new();
        private readonly List<TimePointEntry> timePointEnteries = new();

        #region Switches
        public enum StatOptions {
            Stars,
            Death,
            KnockbackReceived,
            KnockbackDealt,
            Damage,
            ComboLanded,
            ComboRecieved,
            PowerupInfo,
            PowerupSpawns,
            BigCollectableSpawns,
        }

        private bool StatSupportsPlayers() {
            return ViewingStats switch {
                StatOptions.BigCollectableSpawns => false,
                _ => true,
            };
        }

        private IEnumerable<TimePoint> GetTimePoints(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            return viewingOptions switch {
                StatOptions.Stars => stats[TargetPlayer].StarsCollectedPoints,
                StatOptions.Death => stats[TargetPlayer].DeathPoints,
                StatOptions.KnockbackReceived => stats[TargetPlayer].KnockbackPoints,
                StatOptions.KnockbackDealt => GetKnockbackDealt(),
                StatOptions.Damage => stats[TargetPlayer].DamagePoints,
                StatOptions.ComboLanded => GetComboWithPlayer(),
                StatOptions.ComboRecieved => stats[TargetPlayer].ComboReceivedPoints,
                StatOptions.PowerupInfo => stats[TargetPlayer].PowerChangePoints,
                StatOptions.PowerupSpawns => GetItemDrops(),
                StatOptions.BigCollectableSpawns => ReplayStatsRecorder.Instance.GlobalInfo.BigCollectablesSpawned,
                _ => Enumerable.Empty<TimePoint>(),
            };
        }

        #endregion


#if UNITY_EDITOR
        protected override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref canvas, UnityExtensions.GetComponentType.Parent);
        }
#endif
        public void Initialize(ReplayListEntry ourReplayEntry) {
            replayListEntry = ourReplayEntry;
            Instance = this;
        }

        protected override void OnEnable() {
            base.OnEnable();
#if UNITY_EDITOR
            // #if fixes an error in the editor.
            if (!GlobalController.Instance || !GlobalController.Instance.translationManager) {
                return;
            }
#endif

            viewingStatisticDropdown.value = 0;
            targetPlayerDropdown.value = 0;
            scrollRect.verticalNormalizedPosition = 1;
            entryTemplate.gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) layout.transform);
            TranslationManager.OnLanguageChanged += UpdateStatsDropdown;
            TranslationManager.OnLanguageChanged += UpdateEntryCount;
            Canvas.ForceUpdateCanvases();

            UpdatePlayerDropdown();
            UpdateStatsDropdown(GlobalController.Instance.translationManager);
            UpdateEntryCount(GlobalController.Instance.translationManager);
            UpdateInformation(replayListEntry);

            ChangedViewingStats();
        }

        protected override void OnDisable() {
            base.OnDisable();
#if UNITY_EDITOR
            // #if fixes an error in the editor.
            if (!GlobalController.Instance || !GlobalController.Instance.translationManager) {
                return;
            }
#endif

            TranslationManager.OnLanguageChanged -= UpdateStatsDropdown;
            TranslationManager.OnLanguageChanged -= UpdateEntryCount;
        }

        public void UpdateInformation(ReplayListEntry replay) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (replay == null) {
                replayInformation.text = tm.GetTranslation("ui.extras.replays.information.none");
                replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Center;
                return;
            }
            /*
            if (!replay.ReplayFile.Header.IsCompatible) {
                replayInformation.text = tm.GetTranslationWithReplacements("ui.extras.replays.incompatible", "version", replay.ReplayFile.Header.Version.ToStringIgnoreHotfix() + ".X");
                replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Center;
                return;
            }
            */

            BinaryReplayHeader header = replay.ReplayFile.Header;
            ref var rules = ref header.Rules;
            string gamemodeName;
            if (QuantumUnityDB.TryGetGlobalAsset(rules.Gamemode, out var gamemode)) {
                gamemodeName = gamemode.NamePrefix + tm.GetTranslation(gamemode.TranslationKey);
            } else {
                gamemodeName = "???";
            }

            stringBuilder.Clear();
            // Playerlist
            foreach (int i in Enumerable.Range(0, header.PlayerInformation.Length).OrderByDescending(idx => header.PlayerInformation[idx].FinalObjectiveCount)) {
                ref ReplayPlayerInformation info = ref header.PlayerInformation[i];

                // Color and width
                stringBuilder.Append("<width=85%>");
                if (header.Rules.TeamsEnabled) {
                    var allTeams = AssetRepository<TeamAsset>.AllAssets;
                    TeamAsset team = allTeams[info.Team % allTeams.Count];
                    stringBuilder.Append("<nobr>");
                    stringBuilder.Append("<color=#").Append(Utils.ColorToHex(team.color, false)).Append(">").Append(Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal);
                } else {
                    stringBuilder.Append("<color=white>");
                    stringBuilder.Append("<nobr>- ");
                }

                // Username
                stringBuilder.Append(string.IsNullOrWhiteSpace(info.Nickname) ? "noname" : info.Nickname);
                stringBuilder.Append("</nobr>");

                // Stars
                stringBuilder.Append("<width=100%><line-height=0><align=right><br>");
                stringBuilder.Append(gamemode ? Utils.GetSymbolString(gamemode.ObjectiveSymbolPrefix) : "");
                stringBuilder.Append(info.Team == header.WinningTeam ? "<color=yellow>" : "<color=white>");
                stringBuilder.Append(Mathf.Max(0, info.FinalObjectiveCount));

                // Fix formatting
                stringBuilder.AppendLine("<align=left><line-height=100%>");
            }
            stringBuilder.AppendLine();

            // Add rules
            string off = tm.GetTranslation("ui.generic.off");
            string on = tm.GetTranslation("ui.generic.on");

            stringBuilder.Append("<align=center><color=white>");
            stringBuilder.AppendLine(gamemodeName);

            if (gamemode is CoinRunnersGamemode) {
                stringBuilder.Append("<sprite name=room_timer> ").Append(Utils.SecondsToMinuteSeconds(rules.TimerMinutes * 60)).Append("    ");
                stringBuilder.Append("<sprite name=room_coins> ").Append(rules.CoinsForPowerup).Append("    ");
                stringBuilder.Append("<sprite name=room_lives> ").Append(rules.Lives > 0 ? rules.Lives : off).Append("    ");
                stringBuilder.Append("<sprite name=room_powerups>").Append(rules.CustomPowerupsEnabled ? on : off).Append("    ");
                stringBuilder.Append("<sprite name=room_teams>").AppendLine(rules.TeamsEnabled ? on : off);
            } else {
                // Default to star chasers
                stringBuilder.Append("<sprite name=room_stars> ").Append(rules.StarsToWin).Append("    ");
                stringBuilder.Append("<sprite name=room_coins> ").Append(rules.CoinsForPowerup).Append("    ");
                stringBuilder.Append("<sprite name=room_lives> ").Append(rules.Lives > 0 ? rules.Lives : off).Append("    ");
                stringBuilder.Append("<sprite name=room_timer> ").Append(rules.TimerMinutes > 0 ? Utils.SecondsToMinuteSeconds(rules.TimerMinutes * 60) : off).Append("    ");
                stringBuilder.Append("<sprite name=room_powerups>").Append(rules.CustomPowerupsEnabled ? on : off).Append("    ");
                stringBuilder.Append("<sprite name=room_teams>").AppendLine(rules.TeamsEnabled ? on : off);
            }
            stringBuilder.Append("<color=#aaa>").Append(tm.DateTimeToLocalizedString(DateTime.UnixEpoch.AddSeconds(header.UnixTimestamp), false, false)).Append(" - ");
            stringBuilder.Append(Utils.SecondsToMinuteSeconds(header.ReplayLengthInFrames / 60)).Append(" - ").Append(Utils.BytesToString(replay.ReplayFile.FileSize));

            replayInformation.SetText(stringBuilder);
            replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }

        private void UpdatePlayerDropdown() {
            // initializes as 0 though
            int index = targetPlayerDropdown.value;

            targetPlayerDropdown.ClearOptions();
            BinaryReplayHeader header = replayListEntry.ReplayFile.Header;
            for (int i = 0; i < header.PlayerInformation.Length; i++) {
                ref ReplayPlayerInformation info = ref header.PlayerInformation[i];
                targetPlayerDropdown.options.Add(new TMP_Dropdown.OptionData { text = info.Nickname });
            }
            targetPlayerDropdown.SetValueWithoutNotify(index);
            targetPlayerDropdown.RefreshShownValue();
        }

        private void UpdateStatsDropdown(TranslationManager tm) {
            int index = viewingStatisticDropdown.value;

            viewingStatisticDropdown.ClearOptions();
            string prefix = tm.RightToLeft ? "<align=right>" : "";
            string tmPrefix = "ui.replay.stats.statsselect.";

            // loop through all replay stat options
            foreach (StatOptions value in Enum.GetValues(typeof(StatOptions))) {
                viewingStatisticDropdown.options.Add(new TMP_Dropdown.OptionData { text = prefix + tm.GetTranslation(tmPrefix + value.ToString().ToLower()) });
            }
            viewingStatisticDropdown.SetValueWithoutNotify(index);
            viewingStatisticDropdown.RefreshShownValue();
        }

        private void UpdateEntryCount(TranslationManager tm) {
            entryCount.text = tm.GetTranslationWithReplacements("ui.replay.stats.occurences", "occurences", timePointEnteries.Count.ToString());
        }

        public void ChangedViewingStats() {
            bool targetPlayerSupported = StatSupportsPlayers();
            targetPlayerDropdown.interactable = targetPlayerSupported;
            if (!targetPlayerSupported) {
                targetPlayerDropdown.captionText.text = "N/A";
            } else {
                targetPlayerDropdown.RefreshShownValue();
            }

            foreach (var entry in timePointEnteries) {
                Destroy(entry.gameObject);
            }
            timePointEnteries.Clear();

            var timePoints = GetTimePoints();
            int index = 0;
            foreach (var point in timePoints) {
                var entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
                entry.name = $"TimePointEntry{index}";
                entry.gameObject.SetActive(true);
                entry.UpdateUI(point, index + 1);
                timePointEnteries.Add(entry);
                index++;
            }

            UpdateEntryCount(GlobalController.Instance.translationManager);
        }

        #region Other Methods

        private List<PointKnockback> GetKnockbackDealt() {
            List<PointKnockback> newList = new();

            // loop through all players
            var stats = ReplayStatsRecorder.Instance;
            foreach (var currPlayer in stats.PlayerInfos.Keys) {
                // exclude ourself of course <3
                if (currPlayer == TargetPlayer) {
                    continue;
                }

                var kbPoints = stats.PlayerInfos[currPlayer].KnockbackPoints;
                newList.AddRange(kbPoints);
            }

            // sort by index which is time occured
            newList.Sort();

            return newList;
        }

        private List<PointCoinCollected> GetItemDrops() {
            List<PointCoinCollected> newList = new();

            // loop through all players
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            var points = stats[TargetPlayer].CoinsCollectedPoints;
            foreach (var point in points) {
                // skip no item drops
                if (point.CoinItem == null) {
                    continue;
                }

                newList.Add(point);
            }

            return newList;
        }

        private List<PointCombo> GetComboWithPlayer() {
            List<PointCombo> newList = new();

            // loop through all enteries checking playerref
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var playerInfo in stats.Values) {
                // don't include ourselves UwU
                if (playerInfo.PlayerRef == TargetPlayer) {
                    continue;
                }

                // now check the combo points
                foreach (var comboPoint in playerInfo.ComboReceivedPoints) {
                    if (comboPoint.GetParticipants().ContainsKey(TargetPlayer)) {
                        newList.Add(comboPoint);
                    }
                }
            }

            return newList;
        }

        #endregion
    }
}
