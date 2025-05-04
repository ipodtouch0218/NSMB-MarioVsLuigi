using NSMB.Extensions;
using NSMB.Replay;
using NSMB.Translation;
using NSMB.UI.MainMenu;
using NSMB.UI.MainMenu.Submenus.Prompts;
using NSMB.Utils;
using Quantum;
using SFB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ReplayListManager : Selectable {

    public static ReplayListManager Instance { get; private set; }
    public static string ReplayDirectory => Path.Combine(Application.persistentDataPath, "replays");
    public static string TempDirectory => Path.Combine(ReplayDirectory, "temp");
    public static string FavoriteDirectory => Path.Combine(ReplayDirectory, "favorite");

    //---Properties
    public ReplayListEntry Selected { get; set; }
    public List<ReplayListEntry> Replays => replays;

    //---Serialized Variables
    [SerializeField] private MainMenuCanvas canvas;
    [SerializeField] private ReplayDeletePromptSubmenu deletePrompt;
    [SerializeField] private ReplayRenamePromptSubmenu renamePrompt;
    [SerializeField] private ReplayListEntry replayTemplate;
    [SerializeField] private TMP_Text noReplaysText, hiddenReplaysText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] internal VerticalLayoutGroup layout;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private SpriteChangingToggle ascendingToggle;
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private TMP_Text replayInformation;
    [SerializeField] private GameObject importButton, loadingIcon;

    //---Private Variables
    private readonly List<ReplayListEntry> replays = new();
    private readonly SortedSet<ReplayListEntry> temporaryReplays = new(new ReplayDateComparer());
    private bool sortAscending;
    private int sortIndex;
    private Coroutine findReplaysCoroutine;

#if UNITY_EDITOR
    protected override void OnValidate() {
        base.OnValidate();
        this.SetIfNull(ref canvas, UnityExtensions.GetComponentType.Parent);
    }
#endif

    public void Show() {
        sortDropdown.value = 0;
        ascendingToggle.isOn = false;
        searchField.SetTextWithoutNotify("");
        replayTemplate.gameObject.SetActive(false);
        FindReplays();
    }

    public void Close() {
        foreach (var replay in replays) {
            Destroy(replay.gameObject);
        }
        replays.Clear();
        temporaryReplays.Clear();
        // GC.Collect();
    }

    protected override void OnEnable() {
        base.OnEnable();

        if (Application.isPlaying) {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }
    }

    protected override void OnDisable() {
        base.OnDisable();
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private IEnumerator SelectAtEndOfFrame() {
        yield return new WaitForEndOfFrame();
        if (!canvas.EventSystem.alreadySelecting) {
            if (Selected) {
                canvas.EventSystem.SetSelectedGameObject(Selected.IsOpen ? Selected.defaultSelection : Selected.button.gameObject);
            } else if (GetFirstReplayEntry() != null) {
                canvas.EventSystem.SetSelectedGameObject(GetFirstReplayEntry().button.gameObject);
            } else {
                canvas.EventSystem.SetSelectedGameObject(importButton);
            }
        }
    }

    public override void OnSelect(BaseEventData eventData) {
        StartCoroutine(SelectAtEndOfFrame());
    }

    public override void OnPointerDown(PointerEventData eventData) {
        // Do nothing.
    }

    public void StartRename(ReplayListEntry replay) {
        renamePrompt.Open(replay);
    }

    public void StartDeletion(ReplayListEntry replay) {
        deletePrompt.Open(replay);
    }

    public void Select(ReplayListEntry replay, bool open) {
        foreach (var otherReplay in replays) {
            if (otherReplay != replay) {
                otherReplay.HideButtons();
            }
        }
        Selected = replay;
        if (Selected) {
            Selected.OnSelect(open);
        }
        UpdateInformation(replay);
    }

    public void UpdateInformation(ReplayListEntry replay) {
        if (replay == null) {
            replayInformation.text = GlobalController.Instance.translationManager.GetTranslation("ui.extras.replays.information.none");
            replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Center;
            return;
        }

        // TODO: possibly parse from initial frame instead of storing as separate members
        // Playerlist
        StringBuilder builder = new();
        BinaryReplayHeader header = replay.ReplayFile.Header;
        for (int i = 0; i < header.PlayerInformation.Length; i++) {
            ref ReplayPlayerInformation info = ref header.PlayerInformation[i];

            // Color and width
            builder.Append("<width=85%>");
            if (header.Rules.TeamsEnabled) {
                var allTeams = GlobalController.Instance.config.Teams;
                TeamAsset team = QuantumUnityDB.GetGlobalAsset(allTeams[info.Team % allTeams.Length]);
                builder.Append("<nobr>");
                builder.Append("<color=#").Append(Utils.ColorToHex(team.color, false)).Append(">").Append(Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal);
            } else {
                builder.Append("<color=white>");
                builder.Append("<nobr>- ");
            }

            // Username
            builder.Append(info.Username);
            builder.Append("</nobr>");

            // Stars
            builder.Append("<width=100%><pos=90%><sprite name=room_stars>");
            builder.Append("<line-height=0><align=right><br>");
            builder.Append(info.Team == header.WinningTeam ? "<color=yellow>" : "<color=white>");
            builder.Append(info.FinalStarCount);

            // Fix formatting
            builder.AppendLine("<align=left><line-height=100%>");
        }
        builder.AppendLine();

        // Add rules
        TranslationManager tm = GlobalController.Instance.translationManager;
        string off = tm.GetTranslation("ui.generic.off");
        string on = tm.GetTranslation("ui.generic.on");

        builder.Append("<align=center><color=white>");
        var rules = header.Rules;
        builder.Append("<sprite name=room_stars> ").Append(rules.StarsToWin).Append("    ");
        builder.Append("<sprite name=room_coins> ").Append(rules.CoinsForPowerup).Append("    ");
        builder.Append("<sprite name=room_lives> ").Append(rules.Lives > 0 ? rules.Lives : off).Append("    ");
        builder.Append("<sprite name=room_timer> ").Append(rules.TimerSeconds > 0 ? Utils.SecondsToMinuteSeconds(rules.TimerSeconds) : off).Append("    ");
        builder.Append("<sprite name=room_powerups>").Append(rules.CustomPowerupsEnabled ? on : off).Append("    ");
        builder.Append("<sprite name=room_teams>").AppendLine(rules.TeamsEnabled ? on : off);

        // Add date
        builder.Append("<color=#aaa>").Append(DateTime.UnixEpoch.AddSeconds(header.UnixTimestamp).ToLocalTime().ToString()).Append(" - ");
        builder.Append(Utils.SecondsToMinuteSeconds(header.ReplayLengthInFrames / 60)).Append(" - ").Append(Utils.BytesToString(replay.ReplayFile.FileSize));

        replayInformation.text = builder.ToString();
        replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Left;
    }

    public void RemoveReplay(ReplayListEntry replay) {
        if (replay) {
            Destroy(replay.gameObject);
            if (replays.Count == 0) {
                noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation(Settings.Instance.GeneralReplaysEnabled ? "ui.extras.replays.none" : "ui.extras.replays.disabled");
            }
        }
        replays.Remove(replay);
    }

    public void FindReplays() {
        Instance = this;
        foreach (var replay in replays) {
            Destroy(replay.gameObject);
        }
        replays.Clear();
        temporaryReplays.Clear();
        hiddenReplaysText.gameObject.SetActive(false);

        string tempPath = Path.Combine(ReplayDirectory, "temp");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(Path.Combine(ReplayDirectory, "favorite"));
        Directory.CreateDirectory(Path.Combine(ReplayDirectory, "saved"));

        string[] files = Directory.GetFiles(Path.Combine(ReplayDirectory), "*.mvlreplay", SearchOption.AllDirectories);
        if (findReplaysCoroutine != null) {
            StopCoroutine(findReplaysCoroutine);
        }
        findReplaysCoroutine = StartCoroutine(FindReplaysCoroutine(files));
    }

    private IEnumerator FindReplaysCoroutine(string[] files) {
        noReplaysText.text = "";
        loadingIcon.SetActive(true);

        Stopwatch stopwatch = new();
        stopwatch.Start();
        foreach (var filepath in files) {
            if (!File.Exists(filepath)) {
                continue;
            }

            if (BinaryReplayFile.TryLoadNewFromFile(filepath, false, out BinaryReplayFile replay) == ReplayParseResult.Success) {
                ReplayListEntry newReplayEntry = Instantiate(replayTemplate, replayTemplate.transform.parent);
                newReplayEntry.Initialize(this, replay);
                newReplayEntry.name = Path.GetFileName(filepath);
                newReplayEntry.UpdateText();
                replays.Add(newReplayEntry);

                if (newReplayEntry.IsTemporary) {
                    temporaryReplays.Add(newReplayEntry);
                }

                SortReplays();
                scrollRect.verticalNormalizedPosition = 1;
                // FilterReplays();

                if (replays[0] == newReplayEntry) {
                    Select(newReplayEntry, false);
                    StartCoroutine(SelectAtEndOfFrame());
                }
            }

            // Max 10ms per frame.
            if (stopwatch.ElapsedMilliseconds > 10) {
                yield return null;
                stopwatch.Restart();
            }
        }

        FilterReplays();
        if (replays.Count == 0) {
            noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation(Settings.Instance.GeneralReplaysEnabled ? "ui.extras.replays.none" : "ui.extras.replays.disabled");
        }
        loadingIcon.SetActive(false);
        findReplaysCoroutine = null;
    }

    public void OnSortDropdownChanged() {
        sortIndex = sortDropdown.value;
        SortReplays();
    }

    public void OnAscendingSortToggleChanged() {
        sortAscending = ascendingToggle.isOn;
        SortReplays();
    }

    public void OnSearchChanged() {
        FilterReplays();
    }

    public void OnImportClicked() {
        TranslationManager tm = GlobalController.Instance.translationManager;
        string[] selected = StandaloneFileBrowser.OpenFilePanel(tm.GetTranslation("ui.extras.replays.actions.import"), "", "mvlreplay", false);

        foreach (var filepath in selected) {
            ReplayParseResult parseResult = BinaryReplayFile.TryLoadNewFromFile(filepath, true, out BinaryReplayFile parsedReplay);

            if (parseResult == ReplayParseResult.Success) {
                // Move into the replays folder
                string newPath = Path.Combine(ReplayDirectory, "saved", parsedReplay.Header.UnixTimestamp + ".mvlreplay");
                File.Copy(filepath, newPath, false);

                ReplayListEntry newReplayEntry = Instantiate(replayTemplate, replayTemplate.transform.parent);
                newReplayEntry.Initialize(this, parsedReplay);
                newReplayEntry.name = Path.GetFileName(filepath);

                replays.Add(newReplayEntry);
                newReplayEntry.UpdateText();

                noReplaysText.text = "";
            } else {
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Error);
                UnityEngine.Debug.LogWarning($"[Replay] Failed to parse {filepath} as a replay: {parseResult}");
            }
        }
    }

    public void UpdateReplayNavigation() {
        ReplayListEntry previous = null;
        foreach (var replay in replays) {
            if (!replay.gameObject.activeSelf) {
                continue;
            }

            replay.UpdateNavigation(previous);
            previous = replay;
        }
        Select(null, false);
        scrollRect.verticalNormalizedPosition = 1;
    }

    public void SortReplays() {
        replays.Sort(sortIndex switch {
            1 => SortByName,
            2 => SortByStage,
            _ => SortByDate,
        });
        foreach (var replay in replays) {
            replay.transform.SetAsLastSibling();
        }
        hiddenReplaysText.transform.SetAsLastSibling();
        
        UpdateReplayNavigation();
    }

    public void FilterReplays() {
        // Reset state
        foreach (var replay in replays) {
            replay.gameObject.SetActive(true);
        }

        int hidden = 0;
        TranslationManager tm = GlobalController.Instance.translationManager;
        if (!string.IsNullOrWhiteSpace(searchField.text)) {
            foreach (var replay in replays) {
                // Check display name
                if (replay.ReplayFile.Header.GetDisplayName().Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                // Check date
                if (DateTime.UnixEpoch.AddSeconds(replay.ReplayFile.Header.UnixTimestamp).ToLocalTime().ToString().Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                // Check stage name
                if (QuantumUnityDB.TryGetGlobalAsset(replay.ReplayFile.Header.Rules.Stage, out Map map)
                    && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {

                    if (tm.GetTranslation(stage.TranslationKey).Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                        continue;
                    }
                }

                // Check player usernames
                bool found = false;
                foreach (var playerInfo in replay.ReplayFile.Header.PlayerInformation) {
                    if (playerInfo.Username.Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                        found = true;
                        break;
                    }
                }
                if (found) {
                    continue;
                }

                // Check status
                if (replay.warningText.text.Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                // Did not match
                replay.gameObject.SetActive(false);
                hidden++;
            }
        }

        hiddenReplaysText.gameObject.SetActive(hidden > 0);
        hiddenReplaysText.text = tm.GetTranslationWithReplacements("ui.extras.replays.search.hidden", "replays", hidden.ToString());
        UpdateReplayNavigation();
    }

    public ReplayListEntry GetFirstReplayEntry() {
        if (replays.Count <= 0) {
            return null;
        }

        return replays[0];
    }

    public int SortByDate(ReplayListEntry a, ReplayListEntry b) {
        if (sortAscending) {
            return a.ReplayFile.Header.UnixTimestamp.CompareTo(b.ReplayFile.Header.UnixTimestamp);
        } else {
            return b.ReplayFile.Header.UnixTimestamp.CompareTo(a.ReplayFile.Header.UnixTimestamp);
        }
    }

    public int SortByName(ReplayListEntry a, ReplayListEntry b) {
        if (sortAscending) {
            return a.ReplayFile.Header.GetDisplayName().CompareTo(b.ReplayFile.Header.GetDisplayName());
        } else {
            return b.ReplayFile.Header.GetDisplayName().CompareTo(a.ReplayFile.Header.GetDisplayName());
        }
    }

    public int SortByStage(ReplayListEntry a, ReplayListEntry b) {
        var allStages = GlobalController.Instance.config.AllStages;
        
        if (sortAscending) {
            return
                allStages.IndexOf(map => map == a.ReplayFile.Header.Rules.Stage)
                - allStages.IndexOf(map => map == b.ReplayFile.Header.Rules.Stage);
        } else {
            return
                allStages.IndexOf(map => map == b.ReplayFile.Header.Rules.Stage)
                - allStages.IndexOf(map => map == a.ReplayFile.Header.Rules.Stage);
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        int index = sortDropdown.value;

        sortDropdown.ClearOptions();
        sortDropdown.options.Add(new TMP_Dropdown.OptionData { text = tm.GetTranslation("ui.extras.replays.sort.date") });
        sortDropdown.options.Add(new TMP_Dropdown.OptionData { text = tm.GetTranslation("ui.extras.replays.sort.alphabetical") });
        sortDropdown.options.Add(new TMP_Dropdown.OptionData { text = tm.GetTranslation("ui.extras.replays.sort.stage") });

        sortDropdown.SetValueWithoutNotify(index);
        sortDropdown.RefreshShownValue();


        UpdateInformation(Selected);
    }

    public int? GetReplaysUntilDeletion(ReplayListEntry replay) {
        int max = Settings.Instance.generalMaxTempReplays;
        int index = temporaryReplays.IndexOf(r => r == replay);
        if (max <= 0 || !replay.IsTemporary || index == -1) {    
            return null;
        }

        return Mathf.Max(1, max - index);
    }

    public List<ReplayListEntry> GetTemporaryReplaysToDelete() {
        if (Settings.Instance.generalMaxTempReplays <= 0) {
            return null;
        }

        List<ReplayListEntry> replaysToDelete = new();

        foreach (var replay in temporaryReplays) {
            if (GetReplaysUntilDeletion(replay) == 1) {
                replaysToDelete.Add(replay);
            }
        }

        return replaysToDelete;
    }

    public class ReplayDateComparer : IComparer<ReplayListEntry> {
        public int Compare(ReplayListEntry x, ReplayListEntry y) {
            return x.ReplayFile.Header.UnixTimestamp < y.ReplayFile.Header.UnixTimestamp ? 1 : -1;
        }
    }
}