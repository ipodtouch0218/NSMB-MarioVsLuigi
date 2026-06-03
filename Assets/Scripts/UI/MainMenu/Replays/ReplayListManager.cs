using NSMB.Replay;
using NSMB.UI.Elements;
using NSMB.UI.MainMenu.Submenus.Prompts;
using NSMB.UI.Translation;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using SFB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Replays {
    public class ReplayListManager : Selectable {

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);
#endif

        //---Static Variables
        public static readonly string ReplayFileExtension = "mvlreplay";
        public static ReplayListManager Instance { get; private set; }
        public static string ReplayDirectory { get; private set; } 
        public static string TempDirectory { get; private set; }
        public static string SavedDirectory { get; private set; }
        public static string FavoriteDirectory { get; private set; }

        //---Properties
        public ReplayListEntry Selected { get; set; }
        public List<ReplayListEntry> ReplayListEntries => replayListEntries;
        public List<BinaryReplayFile> AllReplays => allReplays;
        public List<BinaryReplayFile> DisplayingReplays => string.IsNullOrEmpty(SearchTerm) ? allReplays : searchResults;
        private string SearchTerm => searchField.text?.Trim();
        private int SortIndex => sortDropdown.value;
        private bool SortAscending => ascendingToggle.isOn;
        public int PageCount => ((DisplayingReplays.Count - 1) / entriesPerPage) + 1;
        public int CurrentPage { get; set; } = -1;

        //---Serialized Variables
        [SerializeField] public MainMenuCanvas canvas;
        [SerializeField] private ReplayDeletePromptSubmenu deletePrompt;
        [SerializeField] private ReplayRenamePromptSubmenu renamePrompt;
        [SerializeField] private ReplayListEntry replayTemplate;
        [SerializeField] private TMP_Text noReplaysText, headerTemplate;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] internal VerticalLayoutGroup layout;
        [SerializeField] private TMP_Dropdown sortDropdown;
        [SerializeField] private SpriteChangingToggle ascendingToggle;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private TMP_Text replayInformation;
        [SerializeField] private GameObject importButton, loadingIcon;

        [SerializeField] private TMP_Text pageTemplate;
        [SerializeField] private int entriesPerPage = 25;
        [SerializeField] private int pageListNearbyNumbers = 2;

        [SerializeField] private GameObject progressBar;
        [SerializeField] private TMP_Text progressBarText;
        [SerializeField] private Image progressBarFill;

        //---Private Variables
        private readonly List<ReplayListEntry> replayListEntries = new();
        private readonly List<TMP_Text> headers = new();

        private string currentSearchTerm;
        private List<BinaryReplayFile> searchResults = new();
        private List<BinaryReplayFile> allReplays = new();
        private HashSet<string> loadedFilepaths = new();

        private readonly StringBuilder stringBuilder = new();

        private bool initialFindStarted;
        private bool ready;
        private CancellationTokenSource currentCancellationSource;
        private readonly object lockObject = new();

        private volatile int findFilesProcessed;
        private int findFilesTotal;

        [RuntimeInitializeOnLoadMethod]
        public static void CreateDirectories() {
            ReplayDirectory = Path.Combine(Application.persistentDataPath, "replays");
            TempDirectory = Path.Combine(ReplayDirectory, "temp");
            SavedDirectory = Path.Combine(ReplayDirectory, "saved");
            FavoriteDirectory = Path.Combine(ReplayDirectory, "favorite");

            Directory.CreateDirectory(TempDirectory);
            Directory.CreateDirectory(FavoriteDirectory);
            Directory.CreateDirectory(SavedDirectory);
        }

#if UNITY_EDITOR
        protected override void OnValidate() {
            base.OnValidate();
            this.SetIfNull(ref canvas, UnityExtensions.GetComponentType.Parent);
        }
#endif

        public void Initialize() {
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

            sortDropdown.value = 0;
            ascendingToggle.isOn = false;
            searchField.SetTextWithoutNotify("");
            replayTemplate.gameObject.SetActive(false);
            scrollRect.verticalNormalizedPosition = 1;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) layout.transform);
            Canvas.ForceUpdateCanvases();

            Settings.Controls.UI.Next.performed += OnNext;
            Settings.Controls.UI.Previous.performed += OnPrevious;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        protected override void OnDisable() {
            base.OnDisable();
#if UNITY_EDITOR
            // #if fixes an error in the editor.
            if (!GlobalController.Instance || !GlobalController.Instance.translationManager) {
                return;
            }
#endif

            CancelExistingTask();
            //_ = ClearReplayListEntries(default);

            Settings.Controls.UI.Next.performed -= OnNext;
            Settings.Controls.UI.Previous.performed -= OnPrevious;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Update() {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying) {
                return;
            }
#endif

            if (progressBar.activeInHierarchy) {
                progressBarFill.fillAmount = (float) findFilesProcessed / findFilesTotal;
                progressBarText.text = $"{findFilesProcessed} / {findFilesTotal}";
            }
        }

        public void AddReplay(BinaryReplayFile replayFile) {
            if (!string.IsNullOrEmpty(replayFile.FilePath)) {
                // Add if we haven't loaded this replay already.
                lock (lockObject) {
                    if (loadedFilepaths.Contains(replayFile.FilePath)) {
                        return;
                    }

                    loadedFilepaths.Add(replayFile.FilePath);
                }
            }

            allReplays.Add(replayFile);
            UpdateNoReplaysText();
        }

        public async Awaitable LoadReplays() {
            if (initialFindStarted) {
                return;
            }

            if (ready || Application.platform == RuntimePlatform.WebGLPlayer) {
                // Already loaded, just refresh the list.
                await StartNewTaskSequence(async (cancellationToken) => {
                    await SortReplays(cancellationToken);
                    await FilterReplays(cancellationToken);
                    await CreateReplayListEntries(cancellationToken);
                });
                return;
            } else {
                // Find replays from disk
                initialFindStarted = true;
                ready = false;
                noReplaysText.text = "";
                await FindReplays(default);
                await SortReplays(default);
                await FilterReplays(default);
                await CreateReplayListEntries(default);
                if (isActiveAndEnabled) {
                    StartCoroutine(SelectAtEndOfFrame());
                }
                initialFindStarted = false;
                ready = true;
            }
        }

        public override void OnSelect(BaseEventData eventData) {
            StartCoroutine(SelectAtEndOfFrame());
        }

        public override void OnPointerDown(PointerEventData eventData) {
            // Do nothing.
        }

        private void OnNext(InputAction.CallbackContext context) {
            if (CurrentPage == PageCount || loadingIcon.activeInHierarchy) {
                return;
            }

            if (canvas.EventSystem.currentSelectedGameObject
                && canvas.EventSystem.currentSelectedGameObject.TryGetComponent(out TMP_InputField inputField)
                && inputField.isFocused) {
                // Don't move left/right when focused on an input field
                return;
            }

            canvas.PlayCursorSound();
            _ = CreateReplayListEntries(default, CurrentPage + 1);
        }

        private void OnPrevious(InputAction.CallbackContext context) {
            if (CurrentPage == 0 || loadingIcon.activeInHierarchy) {
                return;
            }

            if (canvas.EventSystem.currentSelectedGameObject
                && canvas.EventSystem.currentSelectedGameObject.TryGetComponent(out TMP_InputField inputField)
                && inputField.isFocused) {
                // Don't move left/right when focused on an input field
                return;
            }

            canvas.PlayCursorSound();
            _ = CreateReplayListEntries(default, CurrentPage - 1);
        }

        public void StartRename(ReplayListEntry replay) {
            renamePrompt.Open(replay);
        }

        public void StartDeletion(ReplayListEntry replay) {
            deletePrompt.Open(replay);
        }

        public void Select(ReplayListEntry replay, bool open) {
            foreach (var otherReplay in replayListEntries) {
                if (otherReplay != replay) {
                    otherReplay.HideButtons();
                }
            }
            Selected = replay;
            if (Selected) {
                Selected.OnSelect(open);
            }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform) headerTemplate.transform.parent);
            UpdateInformation(replay);
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

        private void UpdateNoReplaysText() {
            if (allReplays.Count == 0) {
                noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation(Settings.Instance.GeneralReplaysEnabled ? "ui.extras.replays.none" : "ui.extras.replays.disabled");
            } else {
                noReplaysText.text = "";
            }
        }

        public async Awaitable CreateReplayListEntries(CancellationToken cancellationToken, BinaryReplayFile focus) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            await Awaitable.MainThreadAsync();

            int index = allReplays.IndexOf(focus);
            int page = (index != -1) ? index / entriesPerPage : 0;

            await CreateReplayListEntries(cancellationToken, page, focus);
        }

        public async Awaitable CreateReplayListEntries(CancellationToken cancellationToken, int? pageNullable = null, BinaryReplayFile focus = null) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                await Awaitable.MainThreadAsync();

                int page = Mathf.Clamp(pageNullable ?? CurrentPage, 0, PageCount - 1);
                CurrentPage = page;

                await ClearReplayListEntries(cancellationToken);

                string previousHeader = null;
                var displayingReplays = DisplayingReplays;
                ReplayListEntry focusEntry = null, previousEntry = null;
                int start = page * entriesPerPage;
                for (int i = start; i < start + entriesPerPage; i++) {
                    if (i >= displayingReplays.Count) {
                        break;
                    }

                    var replay = displayingReplays[i];
                    string header = GetHeader(replay);
                    if (header != previousHeader) {
                        AcquireHeader(header);
                        previousHeader = header;
                    }
                    var replayListEntry = AcquireReplayListEntry(replay, previousEntry);

                    if (replay == focus) {
                        focusEntry = replayListEntry;
                    }

                    previousEntry = replayListEntry;
                }

                if (focusEntry) {
                    Select(focusEntry, false);
                    scrollRect.ScrollToCenter((RectTransform) focusEntry.transform, false);
                } else {
                    Select(GetFirstReplayEntry(), false);
                    scrollRect.verticalNormalizedPosition = 1;
                }

                CreatePageNumbers();
                UpdateNoReplaysText();
                loadingIcon.SetActive(false);
            } catch {
                // Move exceptions to the main thread so they're printed.
                await Awaitable.MainThreadAsync();
                throw;
            }
        }

        public async Awaitable ClearReplayListEntries(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            await Awaitable.MainThreadAsync();
            foreach (var entry in replayListEntries) {
                entry.gameObject.SetActive(false);
            }
            foreach (var entry in headers) {
                entry.gameObject.SetActive(false);
            }

            Transform pagesParent = pageTemplate.transform.parent;
            for (int i = 1; i < pagesParent.childCount; i++) {
                Destroy(pagesParent.GetChild(i).gameObject);
            }
        }
        
        private TMP_Text AcquireHeader(string header) {
            var result = headers.FirstOrDefault(go => !go.isActiveAndEnabled);
            if (!result) {
                result = Instantiate(headerTemplate, headerTemplate.transform.parent);
                headers.Add(result);
            }

            result.text = $"- {header} -";
            result.name = result.text;
            result.transform.SetAsLastSibling();
            result.gameObject.SetActive(true);
            return result;
        }

        private ReplayListEntry AcquireReplayListEntry(BinaryReplayFile replay, ReplayListEntry previousEntry) {
            var result = replayListEntries.FirstOrDefault(rle => !rle.isActiveAndEnabled);
            if (!result) {
                result = Instantiate(replayTemplate, replayTemplate.transform.parent);
                replayListEntries.Add(result);
            }

            result.Initialize(this, replay);
            result.name = replay.Header.GetDisplayName();
            result.UpdateText();
            result.UpdateNavigation(previousEntry);
            result.transform.SetAsLastSibling();
            result.gameObject.SetActive(true);
            return result;
        }

#if UNITY_WEBGL
#pragma warning disable CS1998 // Disable 'Async method runs synchronously' warning
        private async Awaitable FindReplays(CancellationToken cancellationToken) {
            // WebGL can't load from filesystem- so do nothing. Kthx.
            return;
        }
#pragma warning restore CS1998
#else
        private async Awaitable FindReplays(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            async Awaitable ResetProgressBar() {
                await Awaitable.MainThreadAsync();
                findFilesProcessed = 0;
                findFilesTotal = 0;
                progressBar.SetActive(false);
                progressBarFill.fillAmount = 0;
                progressBarText.text = "";
            }

            try {
                await Awaitable.MainThreadAsync();

                await ResetProgressBar();
                progressBar.SetActive(true);
                
                await Awaitable.BackgroundThreadAsync();

                string[] foundReplayFiles = Directory.GetFiles(ReplayDirectory, $"*.{ReplayFileExtension}", SearchOption.AllDirectories);
                findFilesTotal = foundReplayFiles.Length;

                HashSet<string> newLoadedFilepaths = new(loadedFilepaths);
                HashSet<BinaryReplayFile> newFoundReplays = new();
                foreach (var filepath in foundReplayFiles) {
                    findFilesProcessed++;

                    string normalizedFilepath = Path.GetFullPath(filepath);
                    if (cancellationToken.IsCancellationRequested) {
                        await ResetProgressBar();
                        return;
                    }

                    if (newLoadedFilepaths.Contains(normalizedFilepath)) {
                        // Already loaded
                        continue;
                    }
                    newLoadedFilepaths.Add(normalizedFilepath);

                    if (BinaryReplayFile.TryLoadNewFromFile(normalizedFilepath, includeReplayData: false, out var parsedReplay) != ReplayParseResult.Success) {
                        // Not a valid replay file
                        continue;
                    }

                    // This *IS* a replay file.
                    newFoundReplays.Add(parsedReplay);
                }

                if (cancellationToken.IsCancellationRequested) {
                    await ResetProgressBar();
                    return;
                }

                await Awaitable.MainThreadAsync();

                lock (lockObject) {
                    loadedFilepaths = newLoadedFilepaths;
                    allReplays.AddRange(newFoundReplays);
                }
            } catch {
                // Move exceptions to the main thread so they're printed.
                await ResetProgressBar();
                throw;
            }

            await ResetProgressBar();
        }
#endif

        private async Awaitable FilterReplays(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                await Awaitable.MainThreadAsync();
                string newSearchTerm = SearchTerm;

                await Awaitable.BackgroundThreadAsync();
                List<BinaryReplayFile> newSearchResults = new();

                if (string.IsNullOrEmpty(newSearchTerm)) {
                    newSearchResults.AddRange(allReplays);
                    searchResults = newSearchResults;
                    return;
                }

                TranslationManager tm = GlobalController.Instance.translationManager;
                foreach (var replay in allReplays) {
                    if (cancellationToken.IsCancellationRequested) {
                        return;
                    }

                    // Check display name
                    if (replay.Header.GetDisplayName().Contains(newSearchTerm, StringComparison.InvariantCultureIgnoreCase)) {
                        newSearchResults.Add(replay);
                        continue;
                    }

                    // Check date
                    if (tm.DateTimeToLocalizedString(DateTime.UnixEpoch.AddSeconds(replay.Header.UnixTimestamp), false, false).Contains(newSearchTerm, StringComparison.InvariantCultureIgnoreCase)) {
                        newSearchResults.Add(replay);
                        continue;
                    }

                    // Check stage name
                    if (QuantumUnityDB.TryGetGlobalAsset(replay.Header.Rules.Stage, out Map map)
                        && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {

                        if (tm.GetTranslation(stage.TranslationKey).Contains(newSearchTerm, StringComparison.InvariantCultureIgnoreCase)) {
                            newSearchResults.Add(replay);
                            continue;
                        }
                    }

                    // Check player usernames
                    bool found = false;
                    foreach (var playerInfo in replay.Header.PlayerInformation) {
                        if (playerInfo.Nickname.Contains(newSearchTerm, StringComparison.InvariantCultureIgnoreCase)) {
                            found = true;
                            break;
                        }
                    }
                    if (found) {
                        newSearchResults.Add(replay);
                        continue;
                    }

                    /*
                    // Check status
                    if (replay.warningText.text.Contains(newSearchTerm, StringComparison.InvariantCultureIgnoreCase)) {
                        searchResultsNew.Add(replay);
                        continue;
                    }
                    */

                    // Did not match.
                }

                if (cancellationToken.IsCancellationRequested) {
                    return;
                }

                searchResults = newSearchResults;
                currentSearchTerm = newSearchTerm;
            } catch {
                // Move exceptions to the main thread so they're printed.
                await Awaitable.MainThreadAsync();
                throw;
            }
        }

        private async Awaitable SortReplays(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            try {
                await Awaitable.MainThreadAsync();
                int sortIndex = SortIndex;
                bool sortAscending = SortAscending;

                Func<BinaryReplayFile, object> getSortingCriteria = sortIndex switch {
                    1 => (replay) => replay.Header.GetDisplayName(),
                    2 => (replay) => AssetRepository<Map>.AllAssetRefs.IndexOf(replay.Header.Rules.Stage),
                    _ => (replay) => replay.Header.UnixTimestamp,
                };

                var sortCriteriaPairs = allReplays.Select(r => (r, getSortingCriteria(r))).ToList();

                await Awaitable.BackgroundThreadAsync();

                List<BinaryReplayFile> newSortedReplays;
                if (sortAscending) {
                    newSortedReplays = sortCriteriaPairs.OrderBy(t => t.Item2).Select(t => t.Item1).ToList();
                } else {
                    newSortedReplays = sortCriteriaPairs.OrderByDescending(t => t.Item2).Select(t => t.Item1).ToList();
                }

                if (cancellationToken.IsCancellationRequested) {
                    return;
                }

                allReplays = newSortedReplays;
            } catch {
                // Move exceptions to the main thread so they're printed.
                await Awaitable.MainThreadAsync();
                throw;
            }
        }

        [Preserve]
        public void OnSortDropdownChanged() {
            RefreshList();
        }

        [Preserve]
        public void OnAscendingSortToggleChanged() {
            RefreshList();
        }

        [Preserve]
        public void OnSearchChanged() {
            if (SearchTerm == currentSearchTerm) {
                return;
            }
            RefreshList();
        }

        private void RefreshList() {
            _ = StartNewTaskSequence(async (cancellationToken) => {
                loadingIcon.SetActive(true);
                await ClearReplayListEntries(cancellationToken);
                await SortReplays(cancellationToken);
                await FilterReplays(cancellationToken);
                await CreateReplayListEntries(cancellationToken);
            });
        }

        [Preserve]
        public void OnImportClicked() {
            TranslationManager tm = GlobalController.Instance.translationManager;

#if UNITY_WEBGL && !UNITY_EDITOR
            UploadFile(name, nameof(ImportFile), $".{ReplayFileExtension}", false);
#else
            string[] selected = StandaloneFileBrowser.OpenFilePanel(tm.GetTranslation("ui.extras.replays.actions.import"), "", ReplayFileExtension, false);
            if (selected != null && selected.Length > 0) {
                StartCoroutine(ImportFile(selected[0], true));
            }
#endif
        }

        private async Awaitable ImportFile(string filepath, bool makeCopy) {
            try {
#if UNITY_WEBGL && !UNITY_EDITOR
                using UnityEngine.Networking.UnityWebRequest downloadRequest = new(filepath, "GET");
                downloadRequest.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                await downloadRequest.SendWebRequest();
                byte[] replay = ((UnityEngine.Networking.DownloadHandlerBuffer) downloadRequest.downloadHandler).data;
                using MemoryStream memStream = new MemoryStream(replay);

                ReplayParseResult parseResult = BinaryReplayFile.TryLoadNewFromStream(memStream, true, out BinaryReplayFile parsedReplay);
#else
                ReplayParseResult parseResult = BinaryReplayFile.TryLoadNewFromFile(filepath, true, out BinaryReplayFile parsedReplay);
#endif

                if (parseResult != ReplayParseResult.Success) {
                    GlobalController.Instance.PlaySound(SoundEffect.UI_Error);
                    Debug.LogWarning($"[Replay] Failed to parse {filepath} as a replay: {parseResult}");
                    return;
                }

                // Good to go.
                parsedReplay.Header.UnixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

                if (makeCopy) {
                    // Write into the replays folder (not copy, since we changed the timestamp in the header...)
                    string newPath = Path.Combine(ReplayDirectory, "saved", $"{parsedReplay.Header.UnixTimestamp}.{ReplayFileExtension}");
                    using (FileStream fs = new FileStream(newPath, FileMode.Create)) {
                        parsedReplay.WriteToStream(fs);
                    }
                    parsedReplay.FilePath = newPath;
                }

                AddReplay(parsedReplay);

                await StartNewTaskSequence(async (cancellationToken) => {
                    await SortReplays(cancellationToken);
                    await FilterReplays(cancellationToken);
                    await CreateReplayListEntries(cancellationToken, parsedReplay);
                });
            } catch {
                await Awaitable.MainThreadAsync();
                throw;
            }
        }

        private async Awaitable StartNewTaskSequence(Func<CancellationToken, Awaitable> asyncTask) {
            try {
                CancelExistingTask();

                var token = (currentCancellationSource = new()).Token;

                while (!ready) {
                    await Task.Delay(100);
                    if (token.IsCancellationRequested) {
                        return;
                    }
                }

                await asyncTask(token);
            } catch {
                // Move exceptions to the main thread so they're printed.
                await Awaitable.MainThreadAsync();
                throw;
            }
        }

        private void CancelExistingTask() {
            if (currentCancellationSource == null) {
                return;
            }
            
            currentCancellationSource.Cancel();
            currentCancellationSource.Dispose();
            currentCancellationSource = null;
        }

        private void CreatePageNumbers() {
            Transform parent = pageTemplate.transform.parent;
            for (int i = 1; i < parent.childCount; i++) {
                Destroy(parent.GetChild(i).gameObject);
            }

            int displayPage = CurrentPage + 1;

            // Beginning
            if (displayPage > pageListNearbyNumbers) {
                for (int i = 1; i <= Mathf.Min(pageListNearbyNumbers, displayPage - 1 - pageListNearbyNumbers); i++) {
                    InstantiatePageNumber(i);
                }
                if (displayPage > pageListNearbyNumbers * 2 + 1) {
                    InstantiatePageNumber("...");
                }
            }

            // Middle
            for (int i = Mathf.Max(1, displayPage - pageListNearbyNumbers); i <= Mathf.Min(displayPage + pageListNearbyNumbers, PageCount); i++) {
                InstantiatePageNumber(i, selected: i == displayPage);
            }

            // End
            if (displayPage < PageCount - pageListNearbyNumbers) {
                if (displayPage < PageCount - pageListNearbyNumbers * 2 - 1) {
                    InstantiatePageNumber("...");
                }
                for (int i = Mathf.Max(PageCount - pageListNearbyNumbers, displayPage + 1 + pageListNearbyNumbers); i <= PageCount; i++) {
                    InstantiatePageNumber(i);
                }
            }

            void InstantiatePageNumber(object text, bool selected = false) {
                var newPageNumber = Instantiate(pageTemplate, parent);
                newPageNumber.text = text.ToString();
                if (selected) {
                    newPageNumber.text = "» " + newPageNumber.text + " «";
                    newPageNumber.color = Color.white;
                }
                newPageNumber.gameObject.SetActive(true);
            }
        }

        public void RemoveReplay(ReplayListEntry replay) {
            if (!replay) {
                return;
            }

            lock (lockObject) {
                replayListEntries.Remove(replay);
                allReplays.Remove(replay.ReplayFile);
                searchResults.Remove(replay.ReplayFile);
                loadedFilepaths.Remove(replay.ReplayFile.FilePath);
            }

            replay.gameObject.SetActive(false);
        }

        public void RemoveReplayByPath(string path) {
            RemoveReplay(replayListEntries.FirstOrDefault(rle => rle.ReplayFile.FilePath == path));
        }

        public ReplayListEntry GetFirstReplayEntry() {
            return replayListEntries.FirstOrDefault(rle => rle.isActiveAndEnabled);
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

        private string GetHeader(BinaryReplayFile replay) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (SortIndex == 1) {
                // Name
                return null;
            } else if (SortIndex == 2) {
                // Stage
                if (QuantumUnityDB.TryGetGlobalAsset(replay.Header.Rules.Stage, out Map map)
                    && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {

                    return tm.GetTranslation(stage.TranslationKey);
                }
                return "???";
            } else {
                // Date
                return tm.DateTimeToLocalizedString(DateTime.UnixEpoch.AddSeconds(replay.Header.UnixTimestamp), true, true);
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            int index = sortDropdown.value;

            sortDropdown.ClearOptions();
            string prefix = tm.RightToLeft ? "<align=right>" : "";
            sortDropdown.options.Add(new TMP_Dropdown.OptionData { text = prefix + tm.GetTranslation("ui.extras.replays.sort.date") });
            sortDropdown.options.Add(new TMP_Dropdown.OptionData { text = prefix + tm.GetTranslation("ui.extras.replays.sort.alphabetical") });
            sortDropdown.options.Add(new TMP_Dropdown.OptionData { text = prefix + tm.GetTranslation("ui.extras.replays.sort.stage") });
            sortDropdown.SetValueWithoutNotify(index);
            sortDropdown.RefreshShownValue();

            UpdateInformation(Selected);
        }

        public static IList<string> GetTemporaryReplaysToDelete() {
            if (Settings.Instance.generalMaxTempReplays <= 0) {
                return Array.Empty<string>();
            }

            var x = Directory.EnumerateFiles(TempDirectory, $"*.{ReplayFileExtension}")
                .OrderByDescending(path => {
                    try {
                        return File.GetLastWriteTime(path);
                    } catch {
                        return default;
                    }
                })
                .Skip(Settings.Instance.generalMaxTempReplays)
                .ToList();

            return x;
        }
    }
}
