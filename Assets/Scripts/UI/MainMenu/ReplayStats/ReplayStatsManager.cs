using NSMB.Replay;
using NSMB.Replay.Stats;
using NSMB.UI.MainMenu.Submenus.Replays;
using NSMB.UI.MainMenu.Submenus.RoomList;
using NSMB.UI.Translation;
using NSMB.Utilities;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class ReplayStatsManager : Selectable {
        //---Nested types
        [System.Serializable]
        class StatOptionsWrapper {
            public string name;
            public string TranslationKey;
            public StatOptions[] StatOptions;
        }

        //---Public Variables
        public int selectedButton;
        public MainMenuCanvas canvas;

        //---Static Variables
        public static ReplayStatsManager Instance { get; private set; }

        //---Properties
        private StatOptionsWrapper CurrStatsGroup => statOptionGroup[selectedButton];
        private StatOptions ViewingStats => CurrStatsGroup.StatOptions[viewingStatisticDropdown.value];
        private int TargetPlayer => OptionSupportsAllPlayer() ? targetPlayerDropdown.value - 1 : targetPlayerDropdown.value;

        //---Serialized Variables
        [Header("Main Panel")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] internal VerticalLayoutGroup layout, leftTopLayout;

        // side panel
        [Header("Top-left Panel")]
        [SerializeField] private TMP_Dropdown viewingStatisticDropdown, targetPlayerDropdown;
        [SerializeField] private TMP_Text entryCount;

        // bottom panel
        [Header("Bottom-left Panel")]
        [SerializeField] private TMP_Text replayInformation;

        [Header("Templates")]
        [SerializeField] private TimePointEntry entryTemplate;
        [SerializeField] private StatsButton buttonTemplate;
        [SerializeField] private StatsToggle toggleTemplate;
        [SerializeField] private StatsList listTemplate;

        [Header("Lists")]
        [SerializeField] private StatOptionsWrapper[] statOptionGroup;
        //[SerializeField] private StatOptions[] positiveStats, negativeStats, stageStats, miscStats;

        //---Private Variables
        private ReplayListEntry replayListEntry;
        private readonly StringBuilder stringBuilder = new();
        private readonly List<TimePointEntry> timePointEnteries = new();
        public readonly List<StatsButton> statsButtons = new();
        private readonly List<StatsToggle> statToggles = new();
        private readonly List<StatsList> statLists = new();

        #region Point List Methods

        //---dictionaries - the value is if false will hide the entry
        private Dictionary<TimePoint, bool> GetKnockbackDealt() {
            Dictionary<PointKnockback, bool> temp = new();

            var target = statLists[0].Value;

            // loop through all players
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var currPlayer in stats) {
                // exclude ourself of course <3
                if (currPlayer.Key == TargetPlayer) {
                    continue;
                }

                bool show = true;
                if (target != -1 && currPlayer.Key != target) {
                    show = false;
                }

                foreach (var kbPoint in currPlayer.Value.KnockbackPoints) {
                    if (kbPoint.AttackerRef != null && kbPoint.AttackerRef == TargetPlayer) {
                        temp.Add(kbPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetDamageDealt() {
            Dictionary<PointDamage, bool> temp = new();

            var target = statLists[0].Value;

            // loop through all players
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var currPlayer in stats) {
                // exclude ourself of course <3
                if (currPlayer.Key == TargetPlayer) {
                    continue;
                }

                bool show = true;
                if (target != -1 && currPlayer.Key != target) {
                    show = false;
                }

                foreach (var dmgPoint in currPlayer.Value.DamagePoints) {
                    if (dmgPoint.AttackerRef != null && dmgPoint.AttackerRef == TargetPlayer) {
                        temp.Add(dmgPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetComboWithPlayer() {
            Dictionary<PointCombo, bool> temp = new();

            bool selfOnly = statToggles[0].Value;
            bool deathOnly = statToggles[1].Value;

            // loop through all enteries checking playerref
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var playerInfo in stats) {
                // don't include ourselves UwU
                if (playerInfo.Key == TargetPlayer) {
                    continue;
                }

                // now check the combo points
                foreach (var comboPoint in playerInfo.Value.ComboReceivedPoints) {
                    bool show = true;
                    if (selfOnly && comboPoint.GetParticipants().Count > 1) {
                        show = false;
                    }
                    if (deathOnly && !comboPoint.EndsInDeath()) {
                        show = false;
                    }
                    if (comboPoint.GetParticipants().ContainsKey(TargetPlayer)) {
                        temp.Add(comboPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private IEnumerable<TimePoint> GetPowerupInfo() {
            bool targetAll = TargetPlayer < 0;
            bool invincibleOnly = statToggles[0].Value;

            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            if (!targetAll) {
                if (!invincibleOnly) {
                    return stats[TargetPlayer].PowerChangePoints;
                } else {
                    List<TimePoint> temp = new();

                    temp.AddRange(stats[TargetPlayer].StarmanChangePoints);

                    foreach (var powerPoint in stats[TargetPlayer].PowerChangePoints) {
                        if (powerPoint.PowerupState == PowerupState.MegaMushroom) {
                            temp.Add(powerPoint);
                        }
                    }

                    temp.Sort();

                    return temp;
                }
            } else {
                if (!invincibleOnly) {
                    List<PointPowerChange> temp = new();
                    foreach (var playerInfo in stats.Values) {
                        temp.AddRange(playerInfo.PowerChangePoints);
                    }

                    temp.Sort();
                    return temp;
                } else {
                    List<TimePoint> temp = new();

                    foreach (var playerInfo in stats.Values) {
                        temp.AddRange(playerInfo.StarmanChangePoints);
                    }

                    foreach (var playerInfo in stats.Values) {
                        foreach (var powerPoint in playerInfo.PowerChangePoints) {
                            if (powerPoint.PowerupState == PowerupState.MegaMushroom) {
                                temp.Add(powerPoint);
                            }
                        }
                    }

                    temp.Sort();
                    return temp;
                }
            }
        }

        private Dictionary<TimePoint, bool> GetBigCollectableSpawns() {
            Dictionary<PointBigCollectableSpawned, bool> temp = new();

            bool hideSuccess = statToggles[0].Value;
            bool hideBlocks = statToggles[1].Value;

            var attempts = ReplayStatsRecorder.Instance.GlobalInfo;
            foreach (var spawn in attempts.BigCollectablesSpawned) {
                bool show = true;
                if (hideSuccess && spawn.WasBlocked) {
                    show = false;
                }

                if (hideBlocks && !spawn.WasBlocked) {
                    show = false;
                }

                temp.Add(spawn, show);
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetKills() {
            Dictionary<PointDeath, bool> temp = new();

            var target = statLists[0].Value;

            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var playerInfoEntry in stats) {
                if (playerInfoEntry.Key == TargetPlayer) {
                    continue;
                }

                bool show = true;
                if (target != -1 && playerInfoEntry.Key != target) {
                    show = false;
                }

                foreach (var deathPoint in playerInfoEntry.Value.DeathPoints) {
                    if (deathPoint.AttackerRef == TargetPlayer) {
                        temp.Add(deathPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        #endregion

        #region Switches
        public enum StatOptions {
            StarsCollected,
            KnockbackDealt,
            PowerupInfo,
            ComboLanded,
            Death,
            KnockbackReceived,
            Damage,
            ComboRecieved,
            PowerupSpawns,
            CoinsCollected,
            BigCollectableSpawns,
            PowerupGrabs,
            ReserveInfo,
            Kills,
            DamageDealt
        }

        private object GetTimePoints(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            return viewingOptions switch {
                StatOptions.StarsCollected => stats[TargetPlayer].StarsCollectedPoints,
                StatOptions.CoinsCollected => stats[TargetPlayer].CoinsCollectedPoints,
                StatOptions.Death => stats[TargetPlayer].DeathPoints,
                StatOptions.KnockbackReceived => stats[TargetPlayer].KnockbackPoints,
                StatOptions.KnockbackDealt => GetKnockbackDealt(),
                StatOptions.Damage => stats[TargetPlayer].DamagePoints,
                StatOptions.ComboLanded => GetComboWithPlayer(),
                StatOptions.ComboRecieved => stats[TargetPlayer].ComboReceivedPoints,
                StatOptions.PowerupInfo => GetPowerupInfo(),
                StatOptions.PowerupSpawns => stats[TargetPlayer].GetAllItemSpawnPoints(),
                StatOptions.BigCollectableSpawns => GetBigCollectableSpawns(),
                StatOptions.PowerupGrabs => stats[TargetPlayer].PowerupCollectPoints,
                StatOptions.ReserveInfo => stats[TargetPlayer].ReserveChangePoints,
                StatOptions.Kills => GetKills(),
                StatOptions.DamageDealt => GetDamageDealt(),
                _ => throw new NotImplementedException(),
            };
        }

        private TimePoint.DisplayArgs GetDisplayArgs(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            return viewingOptions switch {
                StatOptions.KnockbackDealt or
                StatOptions.ComboLanded or
                StatOptions.Kills or
                StatOptions.DamageDealt => TimePoint.DisplayArgs.FromAttacker,
                _ => TimePoint.DisplayArgs.Normal
            };
        }

        private bool OptionSupportTargetPlayer(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            return viewingOptions switch {
                StatOptions.BigCollectableSpawns => false,
                _ => true
            };
        }

        private bool OptionSupportsAllPlayer(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            return viewingOptions switch {
                StatOptions.PowerupInfo => statToggles[0].Value,
                _ => false
            };
        }

        private Dictionary<string, bool> GetToggleOptions(StatOptions? options = null) {
            Dictionary<string, bool> dictionary = new();
            StatOptions viewingOptions = options ?? ViewingStats;
            var togglePrefix = "ui.replay.stats.toggles.";
            var translationPrefix = togglePrefix+viewingOptions.ToString().ToLower()+".";

            switch (viewingOptions) {
            case StatOptions.ComboLanded:
                dictionary.Add(translationPrefix+"showselfonly", true);
                dictionary.Add(translationPrefix+"killsonly", false);
                break;
            case StatOptions.BigCollectableSpawns:
                dictionary.Add(translationPrefix+"hidesuccess", false);
                dictionary.Add(translationPrefix+"hideblocks", false);
                break;
            case StatOptions.PowerupInfo:
                dictionary.Add(translationPrefix+"showinvincible", false);
                break;
            }
            ;

            return dictionary;
        }

        private Dictionary<string, int> GetListOptions(StatOptions? options = null) {
            Dictionary<string, int> dictionary = new();
            StatOptions viewingOptions = options ?? ViewingStats;
            var listPrefix = "ui.replay.stats.list.";
            var translationPrefix = listPrefix+viewingOptions.ToString().ToLower()+".";
            switch(viewingOptions) {
            case StatOptions.Kills:
            case StatOptions.DamageDealt:
            case StatOptions.KnockbackDealt:
                dictionary.Add(listPrefix+"target", 0);
                break;
            }

            return dictionary;
        }

        #endregion


#if UNITY_EDITOR
        protected override void OnValidate() {
            base.OnValidate();
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

            if (replayListEntry == null) {
                return;
            }

            viewingStatisticDropdown.value = 0;
            targetPlayerDropdown.value = 0;
            scrollRect.verticalNormalizedPosition = 1;
            entryTemplate.gameObject.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
            toggleTemplate.gameObject.SetActive(false);
            listTemplate.gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) layout.transform);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) leftTopLayout.transform);
            TranslationManager.OnLanguageChanged += UpdateStatsDropdown;
            TranslationManager.OnLanguageChanged += UpdateEntryCount;
            TranslationManager.OnLanguageChanged += UpdateLists;
            TranslationManager.OnLanguageChanged += UpdatePlayerDropdown;
            Canvas.ForceUpdateCanvases();

            // create the buttons for cateGOries
            for (int i = 0; i < statOptionGroup.Length; i++) {
                var statGroup = statOptionGroup[i];
                var button = Instantiate(buttonTemplate, buttonTemplate.transform.parent);
                button.name = statGroup.name;
                button.gameObject.SetActive(true);
                button.Initialize(i, statGroup.TranslationKey);
                button.UpdateUI(GlobalController.Instance.translationManager);
                statsButtons.Add(button);
            }

            UpdateStatsDropdown(GlobalController.Instance.translationManager);
            UpdatePlayerDropdown(GlobalController.Instance.translationManager);
            UpdateEntryCount(GlobalController.Instance.translationManager);
            UpdateInformation(replayListEntry);

            ChangedViewingStats(true);
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
            TranslationManager.OnLanguageChanged -= UpdateLists;
            TranslationManager.OnLanguageChanged -= UpdatePlayerDropdown;

            foreach (var button in statsButtons) {
                Destroy(button.gameObject);
            }
            statsButtons.Clear();

            foreach (var toggle in statToggles) {
                Destroy(toggle.gameObject);
            }
            statToggles.Clear();

            foreach (var list in statLists) {
                Destroy(list.gameObject);
            }
            statLists.Clear();

            foreach (var entry in timePointEnteries) {
                Destroy(entry.gameObject);
            }
            timePointEnteries.Clear();
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

        private void UpdateToggles() {
            foreach (var entry in statToggles) {
                Destroy(entry.gameObject);
            }
            statToggles.Clear();

            var toggleOptions = GetToggleOptions();

            for (int i = 0; i < toggleOptions.Count; i++) {
                var toggle = toggleOptions.ElementAt(i);
                var translationKey = toggle.Key;
                var defaultValue = toggle.Value;
                var toggleObj = Instantiate(toggleTemplate, toggleTemplate.transform.parent);
                toggleObj.name = $"Toggle{i}";
                toggleObj.gameObject.SetActive(true);
                toggleObj.Initialize(translationKey, defaultValue);
                statToggles.Add(toggleObj);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) leftTopLayout.transform);
            Canvas.ForceUpdateCanvases();
        }

        private void UpdateLists(TranslationManager tm) {
            foreach (var entry in statLists) {
                Destroy(entry.gameObject);
            }
            statLists.Clear();

            var listsOptions = GetListOptions();

            for (int i = 0; i < listsOptions.Count; i++) {
                var list = listsOptions.ElementAt(i);
                var translationKey = list.Key;
                var defaultValue = list.Value;
                var listObj = Instantiate(listTemplate, listTemplate.transform.parent);
                listObj.name = $"List{i}";
                listObj.gameObject.SetActive(true);
                listObj.Initialize(translationKey, defaultValue);
                // just have it be a player list for now
                listObj.AddToDropdown(tm.GetTranslation("ui.generic.all"), -1);

                var stats = ReplayStatsRecorder.Instance.PlayerInfos;
                for (int j = 0; j < stats.Count; j++) {
                    // exclude ourselves
                    if (j == TargetPlayer) {
                        continue;
                    }
                    var info = stats.Values.ElementAt(j);
                    listObj.AddToDropdown(info.PlayerName, j);
                }
                statLists.Add(listObj);
            }
        }

        private void UpdatePlayerDropdown(TranslationManager tm) {
            // initializes as 0 though
            int index = targetPlayerDropdown.value;
            bool showAll = OptionSupportsAllPlayer();

            targetPlayerDropdown.ClearOptions();
            if (showAll) {
                targetPlayerDropdown.options.Add(new TMP_Dropdown.OptionData { text = tm.GetTranslation("ui.generic.all") });
            }
            BinaryReplayHeader header = replayListEntry.ReplayFile.Header;
            for (int i = 0; i < header.PlayerInformation.Length; i++) {
                ref ReplayPlayerInformation info = ref header.PlayerInformation[i];
                targetPlayerDropdown.options.Add(new TMP_Dropdown.OptionData { text = info.Nickname });
            }
            targetPlayerDropdown.SetValueWithoutNotify(index);
            targetPlayerDropdown.RefreshShownValue();
        }

        public void UpdateStatsDropdown(TranslationManager tm) {
            int index = viewingStatisticDropdown.value;

            viewingStatisticDropdown.ClearOptions();
            string prefix = tm.RightToLeft ? "<align=right>" : "";
            string tmPrefix = "ui.replay.stats.select.";

            // loop through all replay stat options
            foreach (StatOptions value in CurrStatsGroup.StatOptions) {
                viewingStatisticDropdown.options.Add(new TMP_Dropdown.OptionData { text = prefix + tm.GetTranslation(tmPrefix + value.ToString().ToLower()) });
            }
            viewingStatisticDropdown.SetValueWithoutNotify(index);
            viewingStatisticDropdown.RefreshShownValue();
        }

        private void UpdateEntryCount(TranslationManager tm) {
            entryCount.text = tm.GetTranslationWithReplacements("ui.replay.stats.occurences", "occurences", timePointEnteries.Count.ToString());
        }

        public void ChangedViewingStats(bool changeButtons) {
            if (changeButtons) {
                UpdateToggles();
                UpdateLists(GlobalController.Instance.translationManager);
            }
            UpdatePlayerDropdown(GlobalController.Instance.translationManager);
            bool targetPlayerSupported = OptionSupportTargetPlayer();
            targetPlayerDropdown.interactable = targetPlayerSupported;
            if (!targetPlayerSupported) {
                targetPlayerDropdown.captionText.text = "-";
            } else {
                targetPlayerDropdown.RefreshShownValue();
            }

            // clear all time point enteries
            foreach (var entry in timePointEnteries) {
                Destroy(entry.gameObject);
            }
            timePointEnteries.Clear();

            // new time points
            var timePointsList = GetTimePoints();
            var displayArg = GetDisplayArgs();
            int index = 0;
            if (timePointsList is IEnumerable<TimePoint> timePoints) {
                foreach (var point in timePoints) {
                    var entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
                    entry.name = $"TimePointEntry{index}";
                    entry.gameObject.SetActive(true);
                    entry.UpdateUI(point, index + 1, displayArg);
                    timePointEnteries.Add(entry);
                    index++;
                }
            } else if (timePointsList is Dictionary<TimePoint, bool> timePointDictionary) {
                foreach (var pointPair in timePointDictionary) {
                    var point = pointPair.Key;
                    var show = pointPair.Value;
                    if (show) {
                        var entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
                        entry.name = $"TimePointEntry{index}";
                        entry.gameObject.SetActive(true);
                        entry.UpdateUI(point, index + 1, displayArg);
                        timePointEnteries.Add(entry);
                    }
                    index++;
                }
            }

            UpdateEntryCount(GlobalController.Instance.translationManager);
        }

        #region Other Methods

        public void OnChangedViewingPlayer() {
            UpdateLists(GlobalController.Instance.translationManager);
            ChangedViewingStats(false);
        }

        public void ResetStatsDropdownPos() {
            viewingStatisticDropdown.value = 0;
        }

        #endregion
    }
}
