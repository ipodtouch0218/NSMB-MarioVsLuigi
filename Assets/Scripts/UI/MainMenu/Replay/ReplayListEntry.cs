using NSMB.Replay;
using NSMB.Translation;
using NSMB.UI.MainMenu;
using NSMB.Utils;
using SFB;
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReplayListEntry : MonoBehaviour {

    //---Properties
    public BinaryReplayFile ReplayFile { get; private set; }
    public bool IsTemporary => ReplayFile.FilePath.StartsWith(Path.Combine(ReplayListManager.ReplayDirectory, "temp"));
    public bool IsFavorited => ReplayFile.FilePath.StartsWith(Path.Combine(ReplayListManager.ReplayDirectory, "favorite"));
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
    [SerializeField] public Button button;
    [SerializeField] private Button[] compatibleButtons;

    //---Private Variables
    private ReplayListManager manager;
    private Coroutine showHideButtonsCoroutine;

    public void Initialize(ReplayListManager ourManager, BinaryReplayFile ourReplay) {
        manager = ourManager;
        ReplayFile = ourReplay;
        // gameObject.SetActive(true);
    }

    public void OnEnable() {
        TranslationManager.OnLanguageChanged += OnLanguageChanged;
    }

    public void OnDisable() {
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        mainPanel.SetActive(false);
        buttonPanel.SetActive(false);
    }

    public void Update() {
        //UpdateVisibility();
    }

    private static readonly Vector3[] corners = new Vector3[4];
    public void UpdateVisibility(Rect parentRect) {
        if (mainPanel.activeSelf) {
            return;
        }

        RectTransform ourRectTransform = ((RectTransform) transform);
        ourRectTransform.GetWorldCorners(corners);
        Vector3 topLeft = corners[1];
        Rect ourRect = new Rect(topLeft, ourRectTransform.rect.size);

        if (parentRect.Overlaps(ourRect)) {
            mainPanel.SetActive(true);
            buttonPanel.SetActive(true);
        }
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
            if (showHideButtonsCoroutine != null) {
                StopCoroutine(showHideButtonsCoroutine);
            }
            showHideButtonsCoroutine = StartCoroutine(SmoothResize(48, /*0.1f*/ 0));
        }
        canvasGroup.interactable = false;
        button.interactable = true;
        IsOpen = false;
    }

    public void OnClick() {
        manager.Select(this, true);
    }

    public void OnSelect(bool open) {
        if (open) {
            if (showHideButtonsCoroutine != null) {
                StopCoroutine(showHideButtonsCoroutine);
            }
            showHideButtonsCoroutine = StartCoroutine(SmoothResize(86, /*0.1f*/ 0));
            canvasGroup.interactable = true;
            button.interactable = false;
            canvas.PlayCursorSound();
            canvas.EventSystem.SetSelectedGameObject(defaultSelection);
        } else {
            HideButtons();
        }
        IsOpen = open;
    }

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

    public void OnFavoriteClicked() {
        string destination = ReplayListManager.ReplayDirectory;
        string path = ReplayFile.FilePath[destination.Length..];
        int nextSlash = path.IndexOf(Path.DirectorySeparatorChar, 1);
        if (nextSlash != -1) {
            path = path[(nextSlash + 1)..];
        }

        if (IsTemporary || IsFavorited) {
            // Save / Unfavorite
            destination = Path.Combine(destination, "saved", path);
        } else {
            // Favorite
            destination = Path.Combine(destination, "favorite", path);
        }

        File.Move(ReplayFile.FilePath, destination);
        ReplayFile.FilePath = destination;
        UpdateText();
        canvas.PlayConfirmSound();
    }

    public void OnWatchClick() {
        NetworkHandler.StartReplay(ReplayFile);
    }

    public void OnRenameClick() {
        manager.StartRename(this);
    }

    public void OnExportClick() {
        TranslationManager tm = GlobalController.Instance.translationManager;
        StandaloneFileBrowser.SaveFilePanelAsync(tm.GetTranslation("ui.extras.replays.actions.export.prompt"), null, ReplayFile.Header.GetDisplayName(), "mvlreplay", (file) => {
            if (string.IsNullOrWhiteSpace(file)) {
                return;
            }

            if (ReplayFile.LoadAllIfNeeded() == ReplayParseResult.Success) {
                using FileStream stream = new(file, FileMode.OpenOrCreate);
                ReplayFile.WriteToStream(stream);
            }
        });
    }

    public void OnDeleteClick() {
        manager.StartDeletion(this);
    }

    public void UpdateText() {
        if (ReplayFile == null) {
            return;
        }

        TranslationManager tm = GlobalController.Instance.translationManager;
        BinaryReplayHeader header = ReplayFile.Header;

        nameText.text = header.GetDisplayName();
        dateText.text = ReplayListManager.DateTimeToLocalizedString(DateTime.UnixEpoch.AddSeconds(header.UnixTimestamp), false, false);

        bool rtl = tm.RightToLeft;
        warningText.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
        dateText.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        nameText.horizontalAlignment = rtl ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;

        if (!header.IsCompatible) {
            warningText.text = tm.GetTranslationWithReplacements("ui.extras.replays.incompatible", "version", header.Version.ToString());
            warningText.color = criticalColor;
            foreach (var button in compatibleButtons) {
                button.interactable = false;
            }
        } else if (IsTemporary) {
            int? deletion = manager.GetReplaysUntilDeletion(this);
            if (deletion.HasValue && deletion == 1) {
                warningText.text = tm.GetTranslation("ui.extras.replays.temporary.next");
                warningText.color = criticalColor;
            } else if (deletion.HasValue && deletion <= 5) {
                warningText.text = tm.GetTranslationWithReplacements("ui.extras.replays.temporary", "expire", deletion.ToString());
                warningText.color = criticalColor;
            } else {
                warningText.text = tm.GetTranslation("ui.extras.replays.temporary.nodelete");
                warningText.color = warningColor;
            }
        } else if (IsFavorited) {
            warningText.text = tm.GetTranslation("ui.extras.replays.favorited");
            warningText.color = favoriteColor;
        } else {
            warningText.text = "";
        }

        mapImage.sprite = header.GetMapSprite();
        if (!mapImage.sprite) {
            mapImage.sprite = defaultMapSprite;
        }

        if (IsTemporary) {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.save");
        } else if (IsFavorited) {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.unfavorite");
        } else {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.favorite");
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateText();
    }
}
