using JimmysUnityUtilities;
using NSMB.Replay;
using NSMB.Replay.Stats;
using NSMB.UI.MainMenu.Submenus.Replays;
using NSMB.UI.MainMenu.Submenus.RoomList;
using NSMB.UI.Translation;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections;
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
        public bool IsReady;

        //---Static Variables
        public static ReplayStatsManager Instance { get; private set; }

        //---Properties
        private StatOptionsWrapper CurrStatsGroup => statOptionGroup[selectedButton];
        private StatOptions ViewingStats => CurrStatsGroup.StatOptions[dropdownIndexMap[viewingStatisticDropdown.value]];
        private int TargetPlayer => OptionSupportsAllPlayer() ? targetPlayerDropdown.value - 1 : targetPlayerDropdown.value;

        //---Serialized Variables
        [Header("Main Panel")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] internal VerticalLayoutGroup layout, leftTopLayout;

        [SerializeField] private GameObject loading;
        [SerializeField] private GameObject progressBar;
        [SerializeField] private TMP_Text progressBarText;
        [SerializeField] private Image progressBarFill;

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

        [Header("Others")]
        [SerializeField] public ReplayStatsSubmenu statsSubmenu;

        //---Private Variables
        private ReplayListEntry replayListEntry;
        private readonly StringBuilder stringBuilder = new();
        private readonly List<TimePointEntry> timePointEnteries = new();
        public readonly List<StatsButton> statsButtons = new();
        private readonly List<StatsToggle> statToggles = new();
        private readonly List<StatsList> statLists = new();
        private readonly List<int> dropdownIndexMap = new();
        private bool prevStatSupportAll = false;

        #region Point List Methods

        //---dictionaries - the value is if false will hide the entry
        private Dictionary<TimePoint, bool> GetPowerupInfo() {
            bool targetAll = TargetPlayer < 0;
            bool invincibleOnly = GetToggleValOrDefault(0);

            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            Dictionary<TimePoint, bool> temp = new();
            if (!targetAll) {
                if (!invincibleOnly) {
                    int targetState = GetListValOrDefault(0);
                    bool showAllStates = targetState == -1;
                    foreach (var powerstate in stats[TargetPlayer].PowerChangePoints) {
                        bool show = true;
                        if (!showAllStates && powerstate.PowerupState != (PowerupState)targetState) {
                            show = false;
                        }

                        temp.Add(powerstate, show);
                    }
                } else {
                    List<TimePoint> tempList = new();
                    foreach (var starmanPoint in stats[TargetPlayer].StarmanChangePoints) {
                        tempList.Add(starmanPoint);
                    }

                    foreach (var powerPoint in stats[TargetPlayer].PowerChangePoints) {
                        if (powerPoint.PowerupState == PowerupState.MegaMushroom) {
                            tempList.Add(powerPoint);
                        }
                    }

                    tempList.Sort();
                    temp = tempList.ToDictionary(key => key, value => true);
                }
            } else {
                if (!invincibleOnly) {
                    int targetState = GetListValOrDefault(0);
                    bool showAllStates = targetState == -1;
                    foreach (var playerInfo in stats.Values) {
                        foreach (var powerstate in playerInfo.PowerChangePoints) {
                            bool show = true;
                            if (!showAllStates && powerstate.PowerupState != (PowerupState) targetState) {
                                show = false;
                            }

                            temp.Add(powerstate, show);
                        }
                    }
                } else {
                    List<TimePoint> tempList = new();
                    foreach (var playerInfo in stats.Values) {
                        foreach (var starmanPoint in playerInfo.StarmanChangePoints) {
                            tempList.Add(starmanPoint);
                        }
                        foreach (var powerPoint in playerInfo.PowerChangePoints) {
                            if (powerPoint.PowerupState == PowerupState.MegaMushroom) {
                                tempList.Add(powerPoint);
                            }
                        }
                    }

                    tempList.Sort();
                    temp = tempList.ToDictionary(key => key, value => true);
                }
            }
            return temp;
        }

        private IEnumerable<TimePoint> GetTauntInfo() {
            bool targetAll = TargetPlayer < 0;
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;

            if (!targetAll) {
                return stats[TargetPlayer].TauntPoints;
            } else {
                List<TimePoint> temp = new();

                foreach (var playerInfo in stats.Values) {
                    temp.AddRange(playerInfo.TauntPoints);
                }

                temp.Sort();
                return temp;
            }
        }


        private Dictionary<TimePoint, bool> GetKnockbackDealt() {
            Dictionary<PointKnockback, bool> temp = new();

            var target = statLists.Count == 0 ? -1 : GetListValOrDefault(0);

            // loop through all players
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var currPlayer in stats) {
                // exclude ourself of course <3
                if (currPlayer.Key == TargetPlayer) {
                    continue;
                }

                foreach (var kbPoint in currPlayer.Value.KnockbackPoints) {
                    if (kbPoint.AttackerRef != null && kbPoint.AttackerRef == TargetPlayer) {
                        bool show = true;
                        if (target != -1 && currPlayer.Key != target) {
                            show = false;
                        }

                        temp.Add(kbPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetDamageDealt() {
            Dictionary<PointDamage, bool> temp = new();

            var target = statLists.Count == 0 ? -1 : GetListValOrDefault(0);

            // loop through all players
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var currPlayer in stats) {
                // exclude ourself of course <3
                if (currPlayer.Key == TargetPlayer) {
                    continue;
                }

                foreach (var dmgPoint in currPlayer.Value.DamagePoints) {
                    if (dmgPoint.AttackerRef != null && dmgPoint.AttackerRef == TargetPlayer) {
                        bool show = true;
                        if (target != -1 && currPlayer.Key != target) {
                            show = false;
                        }

                        temp.Add(dmgPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetComboWithPlayer() {
            Dictionary<PointCombo, bool> temp = new();

            bool selfOnly = GetToggleValOrDefault(0);
            bool deathOnly = GetToggleValOrDefault(1);

            // loop through all enteries checking playerref
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var playerInfo in stats) {
                // don't include ourselves UwU
                if (playerInfo.Key == TargetPlayer) {
                    continue;
                }

                // now check the combo points
                foreach (var comboPoint in playerInfo.Value.ComboReceivedPoints) {
                    if (comboPoint.GetParticipants().ContainsKey(TargetPlayer)) {
                        bool show = true;
                        if (selfOnly && comboPoint.GetParticipants().Count > 1) {
                            show = false;
                        }
                        if (deathOnly && !comboPoint.EndsInDeath()) {
                            show = false;
                        }

                        temp.Add(comboPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetBigCollectableSpawns() {
            Dictionary<PointBigCollectableSpawned, bool> temp = new();

            bool hideSuccess = GetToggleValOrDefault(0);
            bool hideBlocks = GetToggleValOrDefault(1);

            var attempts = ReplayStatsRecorder.Instance.GlobalInfo;
            foreach (var spawn in attempts.BigCollectablesSpawned) {
                bool show = true;
                if (hideSuccess && !spawn.WasBlocked) {
                    show = false;
                }

                if (hideBlocks && spawn.WasBlocked) {
                    show = false;
                }

                temp.Add(spawn, show);
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetKills() {
            Dictionary<PointDeath, bool> temp = new();

            var target = statLists.Count == 0 ? -1 : GetListValOrDefault(0);

            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var playerInfoEntry in stats) {
                if (playerInfoEntry.Key == TargetPlayer) {
                    continue;
                }

                foreach (var deathPoint in playerInfoEntry.Value.DeathPoints) {
                    if (deathPoint.AttackerRef == TargetPlayer) {
                        bool show = true;
                        if (target != -1 && playerInfoEntry.Key != target) {
                            show = false;
                        }

                        temp.Add(deathPoint, show);
                    }
                }
            }

            return temp.ToDictionary(kvp => (TimePoint) kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<TimePoint, bool> GetPowerupSpawns() {
            Dictionary<TimePoint, bool> temp = new();

            bool randomOnly = GetToggleValOrDefault(0);

            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            foreach (var spawnPoint in stats[TargetPlayer].GetAllItemSpawnPoints()) {
                bool show = true;
                if (spawnPoint is PointBlockHit blockHitPoint) {
                    if (randomOnly && !blockHitPoint.WasRandom) {
                        show = false;
                    }
                }

                temp.Add(spawnPoint, show);
            }

            return temp;
        }

        #endregion

        #region Switches
        public enum StatOptions {
            StarsCollected, // can't be removed
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
            DamageDealt,
            Taunts,
            StarCountChange,
        }

        private object GetTimePoints(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            var stats = ReplayStatsRecorder.Instance.PlayerInfos;
            return viewingOptions switch {
                StatOptions.CoinsCollected => stats[TargetPlayer].CoinsCollectedPoints,
                StatOptions.Death => stats[TargetPlayer].DeathPoints,
                StatOptions.KnockbackReceived => stats[TargetPlayer].KnockbackPoints,
                StatOptions.KnockbackDealt => GetKnockbackDealt(),
                StatOptions.Damage => stats[TargetPlayer].DamagePoints,
                StatOptions.ComboLanded => GetComboWithPlayer(),
                StatOptions.ComboRecieved => stats[TargetPlayer].ComboReceivedPoints,
                StatOptions.PowerupInfo => GetPowerupInfo(),
                StatOptions.PowerupSpawns => GetPowerupSpawns(),
                StatOptions.BigCollectableSpawns => GetBigCollectableSpawns(),
                StatOptions.PowerupGrabs => stats[TargetPlayer].PowerupCollectPoints,
                StatOptions.ReserveInfo => stats[TargetPlayer].ReserveChangePoints,
                StatOptions.Kills => GetKills(),
                StatOptions.DamageDealt => GetDamageDealt(),
                StatOptions.Taunts => GetTauntInfo(),
                StatOptions.StarCountChange => stats[TargetPlayer].StarCountChangePoints,
                _ => throw new NotImplementedException(),
            };
        }

        private TimePoint.DisplayArgs GetDisplayArgs(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            return viewingOptions switch {
                StatOptions.ComboLanded or
                StatOptions.KnockbackDealt or
                StatOptions.ComboLanded or
                StatOptions.DamageDealt or
                StatOptions.Kills => TimePoint.DisplayArgs.FromAttacker,
                _ => TargetPlayer < 0 ? TimePoint.DisplayArgs.All : TimePoint.DisplayArgs.Normal
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
                StatOptions.PowerupInfo => GetToggleValOrDefault(0),
                StatOptions.Taunts => true,
                _ => false
            };
        }

        private bool ShowOption(StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            switch (viewingOptions) {
            case StatOptions.StarCountChange:
                if (QuantumUnityDB.TryGetGlobalAsset(replayListEntry.ReplayFile.Header.Rules.Gamemode, out var gamemode)) {
                    return gamemode is StarChasersGamemode;
                }
                return false;
            default:
                return true;
            };
        }

        // the "value" of the dictionary is the default value
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
            case StatOptions.PowerupSpawns:
                dictionary.Add(translationPrefix+"randomonly", false);
                break;
            };

            return dictionary;
        }

        public enum ListType {
            TargetPlayer,
            Powerup,
            PowerupFilter
        }

        private List<(string translation, int defaultValue, ListType listType)> GetListOptions(StatOptions? options = null) {
            List<(string translation, int defaultValue, ListType listType)> list = new();
            StatOptions viewingOptions = options ?? ViewingStats;
            var listPrefix = "ui.replay.stats.list.";
            var translationPrefix = listPrefix+viewingOptions.ToString().ToLower()+".";
            switch (viewingOptions) {
            case StatOptions.Kills:
            case StatOptions.DamageDealt:
            case StatOptions.KnockbackDealt:
                if (ReplayStatsRecorder.Instance.PlayerInfos.Count > 2) {
                    list.Add((listPrefix+"target", 0, ListType.TargetPlayer));
                }
                break;
            case StatOptions.PowerupInfo:
                if (!GetToggleValOrDefault(0)) {
                    list.Add((listPrefix+"powerupfilter", 0, ListType.PowerupFilter));
                }
                break;
            }

            return list;
        }

        private void UpdateLeftPanelText(TranslationManager tm, StringBuilder sb, StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            var infoPrefix = "ui.replay.stats.info.";
            var translationPrefix = infoPrefix+viewingOptions.ToString().ToLower();
            switch (viewingOptions) {
            case StatOptions.StarCountChange:
                AddOccurenceCount(tm, sb, true);
                int highestStarCount = GetMostStarsHad(timePointEnteries);
                var (mostStarsGet, mostStarsLost)= GetStarGrabLost(timePointEnteries);
                sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix, "highestStarCount", highestStarCount.ToString(), "starGrabCount", mostStarsGet.ToString(), "starLostCount", mostStarsLost.ToString()));
                break;
            case StatOptions.ComboRecieved:
            case StatOptions.ComboLanded:
                var comboPrefix = infoPrefix + "combo";
                AddOccurenceCount(tm, sb);

                if (timePointEnteries.IsEmpty()) {
                    break;
                }

                var longestCombo = GetLongestComboEntry(timePointEnteries);
                var mostComplexCombo = GetMostComplexComboEntry(timePointEnteries);
                var mostStarsCombo = GetMostStarsComboEntry(timePointEnteries);
                sb.AppendLine(tm.GetTranslationWithReplacements(comboPrefix,
                    "longestComboID", longestCombo.EntryInfo, "frameCount", longestCombo.timePoint.Length.ToString(),
                    "mostComplexComboID", mostComplexCombo.EntryInfo, "elements", (mostComplexCombo.timePoint as PointCombo).ComboElements.Count.ToString(),
                    "mostStarsComboID", mostStarsCombo.EntryInfo, "stars", (mostStarsCombo.timePoint as PointCombo).TotalStarsAfterCombo().ToString())
                );
                break;
            case StatOptions.PowerupInfo:
                AddOccurenceCount(tm, sb, true);

                if (!IsReady) {
                    break;
                }
                bool isCheckingInbinvincible = GetToggleValOrDefault(0);

                if (!isCheckingInbinvincible) {
                    int powerupFilter = GetListValOrDefault(0);
                    bool powerupFilterAll = powerupFilter == -1;

                    if (powerupFilterAll) {
                        var mostUsedState = GetMostUsedPowerupState(timePointEnteries);
                        FP timeSpent = GetTotalTimeSpentInPowerup(mostUsedState, timePointEnteries);
                        if (mostUsedState != PowerupState.NoPowerup) {
                            string secondsInPower = $"{(float) timeSpent:F2}";
                            string powerupTranslation = tm.GetTranslation("coinitem."+mostUsedState.ToString().ToLower());
                            sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+".mostused", "powerup", powerupTranslation));
                            sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+".timespent", "time", secondsInPower));
                        }
                    } else {
                        var timeSpent = GetTotalTimeSpentInPowerup((PowerupState) GetListValOrDefault(0), timePointEnteries);
                        string secondsInPower = $"{(float) timeSpent:F2}";
                        sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+".timespent", "time", secondsInPower));
                    }
                } else {
                    FP timeSpentMega = GetTotalTimeSpentInPowerup(PowerupState.MegaMushroom, timePointEnteries);
                    FP timeSpentStarman = GetTotalTimeSpentStarman(timePointEnteries);
                    string megaString = $"{(float) timeSpentMega:F2}";
                    string starmanString = $"{(float) timeSpentStarman:F2}";
                    sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+".timeinvincible", "megatime", megaString, "starmantime", starmanString));
                }
                break;
            case StatOptions.PowerupSpawns:
                var itemSpawnData = GetItemSpawnCount(timePointEnteries);
                foreach (var entry in itemSpawnData) {
                    var item = entry.Key;
                    var count = entry.Value;
                    string itemName = tm.GetTranslation(item.TranslationKey);

                    sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix, "powerup", itemName, "spawncount", count.ToString()));
                }
                break;
            case StatOptions.ReserveInfo:
                AddOccurenceCount(tm, sb, true);
                break;
            case StatOptions.CoinsCollected:
                AddOccurenceCount(tm, sb);
                int itemSpawnCount = GetItemSpawnCountInt(timePointEnteries);
                sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+".itemspawns", "spawncount", itemSpawnCount.ToString()));
                break;
            default:
                AddOccurenceCount(tm, sb);
                break;
            }
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


            // hide templates
            entryTemplate.gameObject.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
            toggleTemplate.gameObject.SetActive(false);
            listTemplate.gameObject.SetActive(false);
            loading.SetActive(!IsReady);

            TranslationManager.OnLanguageChanged += UpdateStatsDropdown;
            TranslationManager.OnLanguageChanged += UpdateLeftPanelText;
            TranslationManager.OnLanguageChanged += UpdateLists;
            TranslationManager.OnLanguageChanged += UpdatePlayerDropdown;

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
            if (!IsReady) {
                targetPlayerDropdown.ClearOptions();
                targetPlayerDropdown.interactable = false;
            }
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
            TranslationManager.OnLanguageChanged -= UpdateLeftPanelText;
            TranslationManager.OnLanguageChanged -= UpdateLists;
            TranslationManager.OnLanguageChanged -= UpdatePlayerDropdown;

            foreach (var button in statsButtons) {
                Destroy(button.gameObject);
            }
            statsButtons.Clear();

            foreach (var entry in timePointEnteries) {
                Destroy(entry.gameObject);
            }
            timePointEnteries.Clear();
        }

        public void Prepare() {
            IsReady = true;
            ResetToggleValues();
            ResetListValues();
            ChangedViewingStats(true);
            loading.SetActive(false);
        }

        public void UpdateProgressBar(int framesLoaded, int frameTotal, FP deltaTime) {
            if (progressBar.activeInHierarchy) {
                var tm = GlobalController.Instance.translationManager;
                float percentage = (float) framesLoaded / frameTotal;

                string loadedAsTime = TimePoint.FrameToTime(framesLoaded, 0, deltaTime);
                string totalAsTime = TimePoint.FrameToTime(frameTotal, 0, deltaTime);
                progressBarFill.fillAmount = percentage;
                progressBarText.text = $"{loadedAsTime} / {totalAsTime} - {framesLoaded} / {frameTotal} ({percentage * 100:F0}%)";
            }
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
        }

        public void UpdateLists(TranslationManager tm) {
            foreach (var entry in statLists) {
                Destroy(entry.gameObject);
            }
            statLists.Clear();
            var listsOptions = GetListOptions();

            for (int i = 0; i < listsOptions.Count; i++) {
                var (translationKey, defaultValue, type) = listsOptions.ElementAt(i);
                var listObj = Instantiate(listTemplate, listTemplate.transform.parent);
                listObj.name = $"List{i}";
                listObj.gameObject.SetActive(true);
                listObj.Initialize(translationKey, defaultValue, type);

                switch (type) {
                case ListType.TargetPlayer:
                    listObj.AddToDropdown(tm.GetTranslation("ui.generic.all"), -1);

                    if (!IsReady) {
                        break;
                    }

                    var stats = ReplayStatsRecorder.Instance.PlayerInfos;
                    for (int j = 0; j < stats.Count; j++) {
                        // exclude ourselves
                        if (j == TargetPlayer) {
                            continue;
                        }
                        var info = stats.Values.ElementAt(j);
                        listObj.AddToDropdown(info.PlayerName, j);
                    }
                    break;
                case ListType.Powerup:
                    listObj.AddToDropdown(tm.GetTranslation("ui.generic.all"), -1);

                    if (!IsReady) {
                        break;
                    }

                    var powerupStateArray = Enum.GetValues(typeof(PowerupState)).Cast<PowerupState>().ToList();
                    for (int j = 0; j < powerupStateArray.Count; j++) {
                        string powerupTranslation;
                        var powerupState = powerupStateArray[j];
                        if (powerupState == PowerupState.NoPowerup) {
                            powerupTranslation = tm.GetTranslation("ui.generic.none");
                        } else {
                            powerupTranslation = tm.GetTranslation("coinitem."+powerupState.ToString().ToLower());
                        }
                        listObj.AddToDropdown(powerupTranslation, j);
                    }
                    break;
                case ListType.PowerupFilter:
                    listObj.AddToDropdown(tm.GetTranslation("ui.generic.all"), -1);

                    if (!IsReady) {
                        break;
                    }

                    List<PowerupState> powerupStates = new();
                    foreach (var timePointEntry in timePointEnteries) {
                        var timePoint = timePointEntry.timePoint;
                        if (timePoint is PointPowerChange powerChange) {
                            if (!powerupStates.Contains(powerChange.PowerupState)) {
                                string powerupTranslation;
                                if (powerChange.PowerupState == PowerupState.NoPowerup) {
                                    powerupTranslation = tm.GetTranslation("ui.generic.none");
                                } else {
                                    powerupTranslation = tm.GetTranslation("coinitem."+powerChange.PowerupState.ToString().ToLower());
                                }
                                listObj.AddToDropdown(powerupTranslation, (int)powerChange.PowerupState);
                                powerupStates.Add(powerChange.PowerupState);
                            }
                        }
                    }
                    break;
                }
                statLists.Add(listObj);
            }
        }

        private void UpdatePlayerDropdown(TranslationManager tm) {
            // initializes as 0 though
            bool showAll = OptionSupportsAllPlayer();
            int index;
            if (showAll && !prevStatSupportAll) {
                index = targetPlayerDropdown.value + 1;
            } else if (!showAll && prevStatSupportAll) {
                index = targetPlayerDropdown.value - 1;
            } else {
                index = targetPlayerDropdown.value;
            }

            targetPlayerDropdown.ClearOptions();
            if (showAll) {
                targetPlayerDropdown.options.Add(new TMP_Dropdown.OptionData { text = tm.GetTranslation("ui.generic.all") });
            }
            BinaryReplayHeader header = replayListEntry.ReplayFile.Header;
            for (int i = 0; i < header.PlayerInformation.Length; i++) {
                ref ReplayPlayerInformation info = ref header.PlayerInformation[i];
                targetPlayerDropdown.options.Add(new TMP_Dropdown.OptionData { text = ReplayStatsRecorder.Instance.PlayerInfos[i].PlayerName });
            }
            targetPlayerDropdown.SetValueWithoutNotify(index);
            targetPlayerDropdown.RefreshShownValue();
            prevStatSupportAll = showAll;
        }

        public void UpdateStatsDropdown(TranslationManager tm) {
            int index = viewingStatisticDropdown.value;

            viewingStatisticDropdown.ClearOptions();
            string prefix = tm.RightToLeft ? "<align=right>" : "";
            string tmPrefix = "ui.replay.stats.select.";

            // loop through all replay stat options
            dropdownIndexMap.Clear();
            for (int i = 0; i < CurrStatsGroup.StatOptions.Count(); i++) {
                StatOptions value = CurrStatsGroup.StatOptions.ElementAt(i);
                if (!ShowOption(value)) {
                    continue;
                }
                viewingStatisticDropdown.options.Add(new TMP_Dropdown.OptionData { text = prefix + tm.GetTranslation(tmPrefix + value.ToString().ToLower()) });
                dropdownIndexMap.Add(i);
            }
            viewingStatisticDropdown.SetValueWithoutNotify(index);
            viewingStatisticDropdown.RefreshShownValue();
        }

        private void UpdateLeftPanelText(TranslationManager tm) {
            StringBuilder stringBuilder = new();
            UpdateLeftPanelText(tm, stringBuilder);
            entryCount.SetText(stringBuilder);
        }

        public void ChangedViewingStats(bool changeButtons) {
            if (!IsReady) {
                goto UpdatePanels;
            }

            UpdatePlayerDropdown(GlobalController.Instance.translationManager);
            bool targetPlayerSupported = OptionSupportTargetPlayer();
            targetPlayerDropdown.interactable = targetPlayerSupported;
            if (!targetPlayerSupported) {
                targetPlayerDropdown.captionText.text = "-";
            } else {
                targetPlayerDropdown.RefreshShownValue();
            }

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

        UpdatePanels:
            if (changeButtons) {
                UpdateToggles();
                UpdateLists(GlobalController.Instance.translationManager);
            }
            UpdateLeftPanelText(GlobalController.Instance.translationManager);
            scrollRect.verticalNormalizedPosition = 1;

            // what a stUPid hack
            // but we have to delay layout rebuilds by one frame or the info on the left will not display properly
            // thanks Unity <3
            StartCoroutine(WaitOneFrame(() => {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) layout.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) leftTopLayout.transform);
            }));
        }

        #region Other Methods

        private IEnumerator WaitOneFrame(Action action) {
            yield return null;
            action?.Invoke();
        }

        public FP GetTotalTimeSpentStarman(List<TimePointEntry> timePointEntries) {
            FP totalTimeSpentFrames = 0;

            // now check all time points
            foreach (var timePointEntry in timePointEntries) {
                if (timePointEntry.timePoint is not PointStarmanChange) {
                    continue;
                }

                var timePoint = timePointEntry.timePoint as PointStarmanChange;
                totalTimeSpentFrames += timePoint.Length * timePoint.DeltaTime;
            }
            return totalTimeSpentFrames;
        }

        public FP GetTotalTimeSpentInPowerup(PowerupState checkingState, List<TimePointEntry> timePointEntries) {
            FP totalTimeSpentFrames = 0;

            // now check all time points
            foreach (var timePointEntry in timePointEntries) {
                if (timePointEntry.timePoint is not PointPowerChange) {
                    continue;
                }
                var timePoint = timePointEntry.timePoint as PointPowerChange;
                if (timePoint.PowerupState != checkingState) {
                    continue;
                }
                totalTimeSpentFrames += timePoint.Length * timePoint.DeltaTime;
            }
            return totalTimeSpentFrames;
        }

        public PowerupState GetMostUsedPowerupState(List<TimePointEntry> timePointEntries) {
            Dictionary<PowerupState, int> stateFrames = new();
            foreach (PowerupState state in Enum.GetValues(typeof(PowerupState))) {
                if (state == PowerupState.NoPowerup) {
                    continue;
                }
                stateFrames[state] = 0;
            }

            // now check all time points
            foreach (var timePointEntry in timePointEntries) {
                if (timePointEntry.timePoint is not PointPowerChange) {
                    continue;
                }
                var timePoint = timePointEntry.timePoint as PointPowerChange;
                if (timePoint.PowerupState == PowerupState.NoPowerup) {
                    continue;
                }
                stateFrames[timePoint.PowerupState] += timePoint.Length;
            }

            // now compare
            PowerupState mostUsedState = PowerupState.NoPowerup;
            int longestPoint = 0;
            foreach (var kvp in stateFrames) {
                var state = kvp.Key;
                var duration = kvp.Value;

                if (duration > longestPoint) {
                    mostUsedState = state;
                    longestPoint = duration;
                }
            }

            return mostUsedState;
        }

        private int GetItemSpawnCountInt(List<TimePointEntry> timePointEnteries) {
            int count = 0;
            foreach (var timePointEntry in timePointEnteries) {
                var timePoint = timePointEntry.timePoint;

                if (timePoint is PointCoinCollected coinCollected) {
                    if (coinCollected.CoinItem == null) {
                        continue;
                    }

                    count++;
                }
            }

            return count;
        }

        private Dictionary<CoinItemAsset, int> GetItemSpawnCount(List<TimePointEntry> timePointEnteries) {
            Dictionary<CoinItemAsset, int> dictionary = new();
            foreach (var timePointEntry in timePointEnteries) {
                var timePoint = timePointEntry.timePoint;

                if (timePoint is PointCoinCollected coinCollected) {
                    // this shouldn't happen
                    if (coinCollected.CoinItem == null) {
                        continue;
                    }

                    if (!dictionary.TryAdd(coinCollected.CoinItem, 1)) {
                        dictionary[coinCollected.CoinItem]++;
                    }
                } else if (timePoint is PointBlockHit blockHit) {
                    if (blockHit.SpawnedItem == null) {
                        continue;
                    }

                    if (!dictionary.TryAdd(blockHit.SpawnedItem, 1)) {
                        dictionary[blockHit.SpawnedItem]++;
                    }
                }
            }

            return dictionary;
        } 

        private (int MostStarsGet, int MostStarsLost) GetStarGrabLost(List<TimePointEntry> timePointEntries) {
            int starsGrabbed = 0, starsLost = 0;

            for (int i = 0; i < timePointEnteries.Count - 1; i++) {
                var currEntry = timePointEnteries[i].timePoint as PointStarCountChange;
                var nextEntry = timePointEnteries[i + 1].timePoint as PointStarCountChange;

                int diff = nextEntry.StarCount - currEntry.StarCount;
                bool isLoss = diff < 0;

                if (isLoss) {
                    starsLost += -diff;
                } else {
                    starsGrabbed += diff;
                }
            }
            return (starsGrabbed, starsLost);
        }

        private int GetMostStarsHad(List<TimePointEntry> timePointEnteries) {
            int highestStarCount = 0;

            // loop through all enteries
            foreach (var timePointEntry in timePointEnteries) {
                var testPoint = timePointEntry.timePoint as PointStarCountChange;
                if (testPoint.StarCount > highestStarCount) {
                    highestStarCount = testPoint.StarCount;
                }
            }

            return highestStarCount;
        }

        private TimePointEntry GetMostStarsComboEntry(List<TimePointEntry> timePointEnteries) {
            TimePointEntry starsComboEntry = null;
            int comboMaxElements = 0, comboMostStars = 0;

            // loop through all combos
            foreach (var timePointEntry in timePointEnteries) {
                // first check if the combo is more complex
                var testPoint = timePointEntry.timePoint as PointCombo;
                if (testPoint.TotalStarsAfterCombo() > comboMostStars) {
                    starsComboEntry = timePointEntry;
                    comboMaxElements = testPoint.ComboElements.Count;
                    comboMostStars = testPoint.TotalStarsAfterCombo();
                    continue;
                }

                // the combo matches element count
                if (testPoint.TotalStarsAfterCombo() == comboMostStars) {
                    // discard combos that lost less stars
                    if (testPoint.ComboElements.Count < comboMaxElements) {
                        continue;
                    }

                    starsComboEntry = timePointEntry;
                    comboMaxElements = testPoint.ComboElements.Count;
                    comboMostStars = testPoint.TotalStarsAfterCombo();
                }
            }

            return starsComboEntry;
        }

        private TimePointEntry GetMostComplexComboEntry(List<TimePointEntry> timePointEnteries) {
            TimePointEntry complexComboEntry = null;
            int comboMaxElements = 0, comboMostStars = 0;

            // loop through all combos
            foreach (var timePointEntry in timePointEnteries) {
                // first check if the combo is more complex
                var testPoint = timePointEntry.timePoint as PointCombo;
                if (testPoint.ComboElements.Count > comboMaxElements) {
                    complexComboEntry = timePointEntry;
                    comboMaxElements = testPoint.ComboElements.Count;
                    comboMostStars = testPoint.TotalStarsAfterCombo();
                    continue;
                }

                // the combo matches element count
                if (testPoint.ComboElements.Count == comboMaxElements) {
                    // discard combos that lost less stars
                    if (testPoint.TotalStarsAfterCombo() < comboMostStars) {
                        continue;
                    }

                    complexComboEntry = timePointEntry;
                    comboMaxElements = testPoint.ComboElements.Count;
                    comboMostStars = testPoint.TotalStarsAfterCombo();
                }
            }

            return complexComboEntry;
        }

        private TimePointEntry GetLongestComboEntry(List<TimePointEntry> timePointEnteries) {
            TimePointEntry longestComboEntry = null;
            int comboMaxLength = 0, comboMaxElements = 0, comboMostStars = 0;

            // loop through all combos
            foreach(var timePointEntry in timePointEnteries) {
                // first check if the combo is longer
                var testPoint = timePointEntry.timePoint as PointCombo;
                if (testPoint.Length > comboMaxLength) {
                    longestComboEntry = timePointEntry;
                    comboMaxLength = testPoint.Length;
                    comboMaxElements = testPoint.ComboElements.Count;
                    comboMostStars = testPoint.TotalStarsAfterCombo();
                    continue;
                }

                // the combo matches length
                if (testPoint.Length == comboMaxLength) {
                    // discard combos with a shorter complexity
                    if (testPoint.ComboElements.Count < comboMaxElements) {
                        continue;
                    }

                    if (testPoint.TotalStarsAfterCombo() < comboMostStars) {
                        continue;
                    }

                    longestComboEntry = timePointEntry;
                    comboMaxLength = testPoint.Length;
                    comboMaxElements = testPoint.ComboElements.Count;
                    comboMostStars = testPoint.TotalStarsAfterCombo();
                }
            }

            return longestComboEntry;
        }

        private bool GetToggleValOrDefault(int index, StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            if (index >= statToggles.Count) {
                var togglePair = GetToggleOptions(viewingOptions);
                return togglePair.ElementAt(index).Value;
            } else {
                return statToggles[index].Value;
            }
        }

        private int GetListValOrDefault(int index, StatOptions? options = null) {
            StatOptions viewingOptions = options ?? ViewingStats;
            if (index >= statLists.Count) {
                var tuple = GetListOptions(viewingOptions);
                return tuple.ElementAt(index).defaultValue;
            } else {
                return statLists[index].Value;
            }
        }

        private void AddOccurenceCount(TranslationManager tm, StringBuilder sb, bool isChanges = false) {
            string translationPrefix = "ui.replay.stats.info.";
            if (isChanges) {
                sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+"changes", "changes", timePointEnteries.Count.ToString()));
            } else {
                sb.AppendLine(tm.GetTranslationWithReplacements(translationPrefix+"occurences", "occurences", timePointEnteries.Count.ToString()));
            }
        }

        public void OnChangedViewingPlayer() {
            ResetListValues();
            ChangedViewingStats(false);
            UpdateLists(GlobalController.Instance.translationManager);
        }

        public void UpdateLists() {
            UpdateLists(GlobalController.Instance.translationManager);
        }

        public void ResetStatsDropdownPos() {
            viewingStatisticDropdown.value = 0;
        }

        public void ResetSelection() {
            selectedButton = 0;
            viewingStatisticDropdown.value = 0;
            targetPlayerDropdown.value = 0;
        }

        private void ResetToggleValues() {
            for (int i = 0; i < statToggles.Count; i++) {
                var toggle = statToggles[i];
                var option = GetToggleOptions();

                toggle.Value = option.ElementAt(i).Value;
            }
        }

        public void ResetListValues() {
            for (int i = 0; i < statLists.Count; i++) {
                var list = statLists[i];
                var option = GetListOptions();

                if (i >= option.Count) {
                    break;
                }
                list.Value = option.ElementAt(i).defaultValue;
            }
        }

        public void PurgeObjects() {
            foreach (var toggle in statToggles) {
                Destroy(toggle.gameObject);
            }
            statToggles.Clear();

            foreach (var list in statLists) {
                Destroy(list.gameObject);
            }
            statLists.Clear();
        }

        public void CloseSubmenu() {
            canvas.CloseSubmenu(statsSubmenu);
        }

        #endregion
    }
}
