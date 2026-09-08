using JimmysUnityUtilities;
using NSMB.Replay;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using SFB;
using System;
using System.IO;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Replays {
    public class ReplayListEntry : MonoBehaviour {

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        public static extern void DownloadFile(string gameObjectName, string methodName, string filename, byte[] byteArray, int byteArraySize);
#endif

        //---Properties
        public BinaryReplayFile ReplayFile { get; private set; }
        public bool IsTemporary => string.IsNullOrEmpty(ReplayFile.FilePath) || FileInFolder(ReplayListManager.TempDirectory, ReplayFile.FilePath);
        public bool IsFavorited => !string.IsNullOrEmpty(ReplayFile.FilePath) && FileInFolder(ReplayListManager.FavoriteDirectory, ReplayFile.FilePath);
        private bool Selected => manager.Selected == this;
        public bool IsOpen { get; private set; }

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] internal GameObject defaultSelection, mainPanel, buttonPanel;
        [SerializeField] private TMP_Text nameText, dateText, favoriteButtonText;
        [SerializeField] public TMP_Text warningText;
        [SerializeField] private Image mapImage;
        [SerializeField] private Sprite defaultMapSprite;
        [SerializeField] private RectTransform dropDownRectTransform;
        [SerializeField] private Color criticalColor, warningColor, favoriteColor;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] public Button button, exportButton;
        [SerializeField] private Button[] compatibleButtons;

        //---Private Variables
        private ReplayListManager manager;
        //private Coroutine showHideButtonsCoroutine;

        public void Initialize(ReplayListManager ourManager, BinaryReplayFile ourReplay) {
            manager = ourManager;
            ReplayFile = ourReplay;
            // gameObject.SetActive(true);
        }

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void UpdateNavigation(ReplayListEntry previous) {
            if (previous) {
                Navigation previousNavigation = previous.button.navigation;
                previousNavigation.selectOnDown = button;
                previous.button.navigation = previousNavigation;
            }

            Navigation currentNavigation = button.navigation;
            currentNavigation.mode = Navigation.Mode.Explicit;
            currentNavigation.selectOnUp = previous ? previous.button : null;
            currentNavigation.selectOnDown = null;
            button.navigation = currentNavigation;
        }

        public void HideButtons() {
            if (!Selected) {
                return;
            }
            if (IsOpen) {
                /*
                if (showHideButtonsCoroutine != null) {
                    StopCoroutine(showHideButtonsCoroutine);
                }
                showHideButtonsCoroutine = StartCoroutine(SmoothResize(48, 0));
                */
                dropDownRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 48);
                Canvas.ForceUpdateCanvases();
            }
            canvasGroup.interactable = false;
            button.interactable = true;
            IsOpen = false;
        }

        [Preserve]
        public void OnClick() {
            manager.Select(this, true);
        }

        public void OnSelect(bool open) {
            if (open) {
                /*
                if (showHideButtonsCoroutine != null) {
                    StopCoroutine(showHideButtonsCoroutine);
                }
                showHideButtonsCoroutine = StartCoroutine(SmoothResize(86, 0));
                */
                dropDownRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 86);
                Canvas.ForceUpdateCanvases();
                
                canvasGroup.interactable = true;
                button.interactable = false;
                canvas.PlayCursorSound();
                canvas.EventSystem.SetSelectedGameObject(defaultSelection);
            } else {
                HideButtons();
            }
            IsOpen = open;
        }

        /*
        private IEnumerator SmoothResize(float target, float time) {
            float start = dropDownRectTransform.sizeDelta.y;
            float progress = 0;
            while (progress < time) {
                progress += Time.deltaTime;
                float alpha = progress / time;
                dropDownRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Lerp(start, target, Utils.EaseInOut(alpha)));
                Canvas.ForceUpdateCanvases();
                manager.layout.SetLayoutVertical();
                yield return null;
            }
            dropDownRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, target);
            Canvas.ForceUpdateCanvases();
            manager.layout.SetLayoutVertical();
            showHideButtonsCoroutine = null;
        }
        */

        [Preserve]
        public void OnFavoriteClicked() {
            string newFolder;
            if (IsTemporary || IsFavorited) {
                // Save / Unfavorite
                newFolder = ReplayListManager.SavedDirectory;
            } else {
                // Favorite
                newFolder = ReplayListManager.FavoriteDirectory;
            }

            string filename = Path.GetFileName(ReplayFile.FilePath);
            string destination = Path.Combine(newFolder, filename);

            File.Move(ReplayFile.FilePath, destination);
            ReplayFile.FilePath = destination;
            UpdateText();
            canvas.PlayConfirmSound();
        }

        [Preserve]
        public void OnWatchClick() {
            ActiveReplayManager.Instance.StartReplayPlayback(ReplayFile);
        }

        [Preserve]
        public void OnRenameClick() {
            manager.StartRename(this);
        }

        [Preserve]
        public void OnExportClick() {
#if UNITY_WEBGL && !UNITY_EDITOR
            static void FileDownloadedCallback() {
                // Cool... I don't care.
            }

            if (ReplayFile.LoadAllIfNeeded() == ReplayParseResult.Success) {
                using MemoryStream stream = new((int) ReplayFile.FileSize);
                long replaySize = ReplayFile.WriteToStream(stream);
                DownloadFile(name, nameof(FileDownloadedCallback), $"{ReplayFile.Header.GetDisplayName()}.{ReplayListManager.ReplayFileExtension}", stream.ToArray(), (int) replaySize);
            }
#else
            string validDisplayName = ReplayFile.Header.GetDisplayName().ReplaceAny(Path.GetInvalidFileNameChars(), '-');

            TranslationManager tm = GlobalController.Instance.translationManager;
            StandaloneFileBrowser.SaveFilePanelAsync(tm.GetTranslation("ui.extras.replays.actions.export.prompt"), null, validDisplayName, ReplayListManager.ReplayFileExtension, (file) => {
                if (string.IsNullOrWhiteSpace(file)) {
                    return;
                }

                if (!string.IsNullOrEmpty(ReplayFile.FilePath) && File.Exists(ReplayFile.FilePath)) {
                    // File exists on the hard drive, just copy to the destination.
                    File.Copy(ReplayFile.FilePath, file);
                } else if (ReplayFile.Header.IsCompatible && ReplayFile.LoadAllIfNeeded() == ReplayParseResult.Success) {
                    // Write using export stream
                    using FileStream stream = new(file, FileMode.OpenOrCreate);
                    ReplayFile.WriteToStream(stream);
                } else {
                    // Incompatible and doesn't exist on drive anymore, nothing we can really do.
                    canvas.PlaySound(SoundEffect.UI_Error);
                }
            });
#endif
        }

        [Preserve]
        public void OnDeleteClick() {
            manager.StartDeletion(this);
        }

        private static ProfilerMarker profilerMarker = new("UpdateText");
        public void UpdateText() {
            if (ReplayFile == null) {
                return;
            }

            profilerMarker.Begin(gameObject);

            TranslationManager tm = GlobalController.Instance.translationManager;
            BinaryReplayHeader header = ReplayFile.Header;

            nameText.SetTextIfDifferent(header.GetDisplayName());
            dateText.SetTextIfDifferent(tm.DateTimeToLocalizedString(DateTime.UnixEpoch.AddSeconds(header.UnixTimestamp), false, false));

            bool rtl = tm.RightToLeft;
            warningText.SetHorizontalAlignmentIfDifferent(rtl ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right);
            dateText.SetHorizontalAlignmentIfDifferent(rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left);
            nameText.SetHorizontalAlignmentIfDifferent(rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left);

            foreach (var button in compatibleButtons) {
                button.interactable = header.IsCompatible;
            }
            exportButton.interactable = header.IsCompatible || (!string.IsNullOrEmpty(ReplayFile.FilePath) && File.Exists(ReplayFile.FilePath));

            string finalWarningText;
            if (!header.IsCompatible) {
                finalWarningText = tm.GetTranslationWithReplacements("ui.extras.replays.incompatible", "version", header.Version.ToStringIgnoreHotfix() + ".X");
                warningText.color = criticalColor;
            } else if (IsTemporary) {
                /*
                int? deletion = manager.GetReplaysUntilDeletion(this);
                if (deletion.HasValue && deletion == 1) {
                    finalWarningText = tm.GetTranslation("ui.extras.replays.temporary.next");
                    warningText.color = criticalColor;
                } else if (deletion.HasValue && deletion <= 5) {
                    finalWarningText = tm.GetTranslationWithReplacements("ui.extras.replays.temporary", "expire", deletion.ToString());
                    warningText.color = criticalColor;
                } else {
                    finalWarningText = tm.GetTranslation("ui.extras.replays.temporary.nodelete");
                    warningText.color = warningColor;
                }
                */
                finalWarningText = tm.GetTranslation("ui.extras.replays.temporary.nodelete");
                warningText.color = warningColor;
            } else if (IsFavorited) {
                finalWarningText = tm.GetTranslation("ui.extras.replays.favorited");
                warningText.color = favoriteColor;
            } else {
                finalWarningText = "";
            }
            warningText.SetTextIfDifferent(finalWarningText);

            mapImage.sprite = header.GetMapSprite();
            if (!mapImage.sprite) {
                mapImage.sprite = defaultMapSprite;
            }

            string finalFavoriteButtonText;
            if (IsTemporary) {
                finalFavoriteButtonText = tm.GetTranslation("ui.extras.replays.actions.save");
            } else if (IsFavorited) {
                finalFavoriteButtonText = tm.GetTranslation("ui.extras.replays.actions.unfavorite");
            } else {
                finalFavoriteButtonText = tm.GetTranslation("ui.extras.replays.actions.favorite");
            }
            favoriteButtonText.SetTextIfDifferent(finalFavoriteButtonText);

            profilerMarker.End();
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateText();
        }

        private static bool FileInFolder(string folderPath, string filePath) {
            return !Path.GetRelativePath(Path.GetFullPath(folderPath), Path.GetFullPath(filePath))
                .StartsWith(".." + Path.DirectorySeparatorChar);
        }
    }
}
