using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.MainMenu;
using NSMB.UI.MainMenu.Submenus.Prompts;
using NSMB.Utils;
using Quantum;
using SFB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static BinaryReplayFile;
using static BinaryReplayHeader;

public class ReplayListManager : Selectable {

    public static ReplayListManager Instance { get; private set; }
    public static string ReplayDirectory => Path.Combine(Application.persistentDataPath, "replays");

    //---Properties
    public ReplayListEntry Selected { get; set; }
    public List<Replay> Replays => replays;

    //---Serialized Variables
    [SerializeField] private MainMenuCanvas canvas;
    [SerializeField] private ReplayDeletePromptSubmenu deletePrompt;
    [SerializeField] private ReplayRenamePromptSubmenu renamePrompt;
    [SerializeField] private ReplayListEntry replayTemplate;
    [SerializeField] private TMP_Text noReplaysText, hiddenReplaysText;
    [SerializeField] internal VerticalLayoutGroup layout;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private SpriteChangingToggle ascendingToggle;
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private TMP_Text replayInformation;
    [SerializeField] private GameObject importButton;

    //---Private Variables
    private readonly List<Replay> replays = new();
    private readonly SortedSet<Replay> temporaryReplays = new(new ReplayDateComparer());
    private bool sortAscending;
    private int sortIndex;

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
                canvas.EventSystem.SetSelectedGameObject(Selected.defaultSelection);
            } else {
                canvas.EventSystem.SetSelectedGameObject(GetFirstReplayEntry().button.gameObject);
            }
        }
    }

    public override void OnSelect(BaseEventData eventData) {
        StartCoroutine(SelectAtEndOfFrame());
    }

    public override void OnPointerDown(PointerEventData eventData) {
        // Do nothing.
    }

    public void StartRename(Replay replay) {
        renamePrompt.Open(replay);
    }

    public void StartDeletion(Replay replay) {
        deletePrompt.Open(replay);
    }

    public void Select(Replay replay) {
        foreach (var otherReplay in replays) {
            if (otherReplay != replay) {
                otherReplay.ListEntry.HideButtons();
            }
        }
        Selected = replay?.ListEntry;
        if (Selected) {
            Selected.OnSelect();
        }
        UpdateInformation(replay);
    }

    public void UpdateInformation(Replay replay) {
        if (replay == null) {
            replayInformation.text = GlobalController.Instance.translationManager.GetTranslation("ui.extras.replays.information.none");
            replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Center;
            return;
        }

        // TODO: possibly parse from initial frame instead of storing as separate members
        // Playerlist
        StringBuilder builder = new();
        BinaryReplayHeader file = replay.ReplayFile;
        for (int i = 0; i < file.PlayerInformation.Length; i++) {
            ref ReplayPlayerInformation info = ref file.PlayerInformation[i];

            // Color and width
            builder.Append("<width=85%>");
            if (file.Rules.TeamsEnabled) {
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
            builder.Append(info.Team == file.WinningTeam ? "<color=yellow>" : "<color=white>");
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
        var rules = file.Rules;
        builder.Append("<sprite name=room_stars> ").Append(rules.StarsToWin).Append("    ");
        builder.Append("<sprite name=room_coins> ").Append(rules.CoinsForPowerup).Append("    ");
        builder.Append("<sprite name=room_lives> ").Append(rules.Lives > 0 ? rules.Lives : off).Append("    ");
        builder.Append("<sprite name=room_timer> ").Append(rules.TimerSeconds > 0 ? Utils.SecondsToMinuteSeconds(rules.TimerSeconds) : off).Append("    ");
        builder.Append("<sprite name=room_powerups>").Append(rules.CustomPowerupsEnabled ? on : off).Append("    ");
        builder.Append("<sprite name=room_teams>").AppendLine(rules.TeamsEnabled ? on : off);

        // Add date
        builder.Append("<color=#aaa>").Append(DateTime.UnixEpoch.AddSeconds(file.UnixTimestamp).ToLocalTime().ToString()).Append(" - ");
        builder.Append(Utils.SecondsToMinuteSeconds(file.ReplayLengthInFrames / 60)).Append(" - ").Append(Utils.BytesToString(replay.FileSize));

        replayInformation.text = builder.ToString();
        replayInformation.horizontalAlignment = HorizontalAlignmentOptions.Left;
    }

    public void RemoveReplay(Replay replay) {
        if (replay == null) {
            return;
        }
        if (replay.ListEntry) {
            Destroy(replay.ListEntry.gameObject);
            if (replays.Count == 0) {
                noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation(Settings.Instance.GeneralReplaysEnabled ? "ui.extras.replays.none" : "ui.extras.replays.disabled");
            }
        }
        replays.Remove(replay);
    }

    public void FindReplays() {
        Instance = this;
        foreach (var replay in replays) {
            Destroy(replay.ListEntry.gameObject);
        }
        replays.Clear();
        temporaryReplays.Clear();

        string tempPath = Path.Combine(ReplayDirectory, "temp");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(Path.Combine(ReplayDirectory, "favorite"));
        Directory.CreateDirectory(Path.Combine(ReplayDirectory, "saved"));

        string[] files = Directory.GetFiles(Path.Combine(ReplayDirectory), "*.mvlreplay", SearchOption.AllDirectories);
        foreach (var filepath in files) {
            if (!File.Exists(filepath)) {
                continue;
            }

            using FileStream inputStream = new FileStream(filepath, FileMode.Open);
            long filesize = inputStream.Length;
            if (BinaryReplayHeader.TryLoadFromFile(inputStream, out BinaryReplayHeader replayHeader) == ReplayParseResult.Success) {
                Replay newReplay = new Replay {
                    FilePath = filepath,
                    FileSize = filesize,
                    ReplayFile = replayHeader,
                    ListEntry = Instantiate(replayTemplate, replayTemplate.transform.parent),
                };
                newReplay.ListEntry.Initialize(this, newReplay);
                replays.Add(newReplay);

                if (newReplay.FilePath.StartsWith(Path.Combine(ReplayDirectory, "temp"))) {
                    // Is temporary
                    temporaryReplays.Add(newReplay);
                }
            }
        }

        if (replays.Count == 0) {
            noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation(Settings.Instance.GeneralReplaysEnabled ? "ui.extras.replays.none" : "ui.extras.replays.disabled");
        } else {
            noReplaysText.text = "";
        }

        foreach (var replay in replays) {
            replay.ListEntry.UpdateText();
        }
        SortReplays();
        FilterReplays();

        ReplayListEntry firstReplay = GetFirstReplayEntry();
        if (gameObject.activeInHierarchy) {
            Select(null);
            if (firstReplay) {
                Selected = firstReplay;
                EventSystem.current.SetSelectedGameObject(firstReplay.button.gameObject);
            } else {
                EventSystem.current.SetSelectedGameObject(importButton);
            }
        }
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
            using FileStream stream = new FileStream(filepath, FileMode.Open);
            long filesize = stream.Length;
            ReplayParseResult parseResult = BinaryReplayHeader.TryLoadFromFile(stream, out BinaryReplayHeader parsedReplay);

            if (parseResult == ReplayParseResult.Success) {
                // Move into the replays folder
                string newPath = Path.Combine(ReplayDirectory, "saved", parsedReplay.UnixTimestamp + ".mvlreplay");
                File.Copy(filepath, newPath, false);

                Replay newReplay = new Replay {
                    FilePath = filepath,
                    FileSize = filesize,
                    ReplayFile = parsedReplay,
                    ListEntry = Instantiate(replayTemplate, replayTemplate.transform.parent)
                };
                newReplay.ListEntry.Initialize(this, newReplay);
                replays.Add(newReplay);
                newReplay.ListEntry.UpdateText();

                noReplaysText.text = "";
            } else {
                Debug.LogWarning($"[Replay] Failed to parse {filepath} as a replay: {parseResult}");
            }
        }
    }

    public void UpdateReplayNavigation() {
        ReplayListEntry previous = null;
        foreach (var replay in replays) {
            if (!replay.ListEntry.gameObject.activeSelf) {
                continue;
            }

            replay.ListEntry.UpdateNavigation(previous);
            previous = replay.ListEntry;
        }
    }

    public void SortReplays() {
        replays.Sort(sortIndex switch {
            1 => SortByName,
            2 => SortByStage,
            _ => SortByDate,
        });
        foreach (var replay in replays) {
            replay.ListEntry.transform.SetAsLastSibling();
        }
        hiddenReplaysText.transform.SetAsLastSibling();
        UpdateReplayNavigation();
    }

    public void FilterReplays() {
        // Reset state
        foreach (var replay in replays) {
            replay.ListEntry.gameObject.SetActive(true);
        }

        int hidden = 0;
        TranslationManager tm = GlobalController.Instance.translationManager;
        if (!string.IsNullOrWhiteSpace(searchField.text)) {
            foreach (var replay in replays) {
                // Check display name
                if (replay.ReplayFile.GetDisplayName().Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                // Check date
                if (DateTime.UnixEpoch.AddSeconds(replay.ReplayFile.UnixTimestamp).ToLocalTime().ToString().Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                // Check stage name
                if (QuantumUnityDB.TryGetGlobalAsset(replay.ReplayFile.Rules.Stage, out Map map)
                    && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {

                    if (tm.GetTranslation(stage.TranslationKey).Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                        continue;
                    }
                }

                // Check status
                if (replay.ListEntry.warningText.text.Contains(searchField.text, StringComparison.InvariantCultureIgnoreCase)) {
                    continue;
                }

                // Did not match
                replay.ListEntry.gameObject.SetActive(false);
                hidden++;
            }
        }

        hiddenReplaysText.gameObject.SetActive(hidden > 0);
        hiddenReplaysText.text = tm.GetTranslationWithReplacements("ui.extras.replay.search.hidden", "replays", hidden.ToString());
        UpdateReplayNavigation();
    }

    public ReplayListEntry GetFirstReplayEntry() {
        if (replays.Count <= 0) {
            return null;
        }

        return replays[0].ListEntry;
    }

    public int SortByDate(Replay a, Replay b) {
        if (sortAscending) {
            return a.ReplayFile.UnixTimestamp.CompareTo(b.ReplayFile.UnixTimestamp);
        } else {
            return b.ReplayFile.UnixTimestamp.CompareTo(a.ReplayFile.UnixTimestamp);
        }
    }

    public int SortByName(Replay a, Replay b) {
        if (sortAscending) {
            return a.ReplayFile.GetDisplayName().CompareTo(b.ReplayFile.GetDisplayName());
        } else {
            return b.ReplayFile.GetDisplayName().CompareTo(a.ReplayFile.GetDisplayName());
        }
    }

    public int SortByStage(Replay a, Replay b) {
        var allStages = GlobalController.Instance.config.AllStages;
        
        if (sortAscending) {
            return
                allStages.IndexOf(map => ((AssetRef<Map>) map) == a.ReplayFile.Rules.Stage)
                - allStages.IndexOf(map => ((AssetRef<Map>) map) == b.ReplayFile.Rules.Stage);
        } else {
            return
                allStages.IndexOf(map => ((AssetRef<Map>) map) == b.ReplayFile.Rules.Stage)
                - allStages.IndexOf(map => ((AssetRef<Map>) map) == a.ReplayFile.Rules.Stage);
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


        UpdateInformation(Selected ? Selected.Replay : null);
    }

    public int? GetReplaysUntilDeletion(Replay replay) {
        int max = Settings.Instance.generalMaxTempReplays;
        int index = temporaryReplays.IndexOf(r => r == replay);
        if (max <= 0 || !replay.IsTemporary || index == -1) {    
            return null;
        }

        return Mathf.Max(1, max - index);
    }

    public List<Replay> GetTemporaryReplaysToDelete() {
        if (Settings.Instance.generalMaxTempReplays <= 0) {
            return null;
        }

        List<Replay> replaysToDelete = new();

        foreach (var replay in temporaryReplays) {
            if (GetReplaysUntilDeletion(replay) == 1) {
                replaysToDelete.Add(replay);
            }
        }

        return replaysToDelete;
    }

    public class Replay {
        public string FilePath;
        public BinaryReplayHeader ReplayFile;
        public ReplayListEntry ListEntry;
        public long FileSize;
        public bool IsTemporary => FilePath.StartsWith(Path.Combine(ReplayDirectory, "temp"));
        public bool IsFavorited => FilePath.StartsWith(Path.Combine(ReplayDirectory, "favorite"));
    }

    public class ReplayDateComparer : IComparer<Replay> {
        public int Compare(Replay x, Replay y) {
            return x.ReplayFile.UnixTimestamp < y.ReplayFile.UnixTimestamp ? 1 : -1;
        }
    }
}