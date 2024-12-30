using NSMB.Translation;
using NSMB.UI.MainMenu;
using NSMB.Utils;
using SFB;
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ReplayListEntry : MonoBehaviour {

    //---Properties
    public ReplayListManager.Replay Replay { get; private set; }

    //---Serialized Variables
    [SerializeField] private MainMenuCanvas canvas;
    [SerializeField] internal GameObject defaultSelection;
    [SerializeField] private TMP_Text nameText, dateText, warningText, favoriteButtonText;
    [SerializeField] private Image mapImage;
    [SerializeField] private Sprite defaultMapSprite;
    [SerializeField] private RectTransform dropDownRectTransform;
    [SerializeField] private Color criticalColor, warningColor, favoriteColor;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] public Button button;
    [SerializeField] private Button[] compatibleButtons;

    //---Private Variables
    private ReplayListManager manager;
    private bool selected;
    private Coroutine showHideButtonsCoroutine;

    public void OnEnable() {
        TranslationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);
    }

    public void OnDisable() {
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Initialize(ReplayListManager ourManager, ReplayListManager.Replay ourReplay) {
        manager = ourManager;
        Replay = ourReplay;
        gameObject.SetActive(true);
        UpdateText();
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
        if (!selected) {
            return;
        }
        if (showHideButtonsCoroutine != null) {
            StopCoroutine(showHideButtonsCoroutine);
        }
        selected = false;
        showHideButtonsCoroutine = StartCoroutine(SmoothResize(48, 0.1f));
        canvasGroup.interactable = false;
    }

    public void OnClick() {
        if (selected) {
            return;
        }
        manager.Select(Replay);
    }

    public void OnSelect() {
        if (showHideButtonsCoroutine != null) {
            StopCoroutine(showHideButtonsCoroutine);
        }
        showHideButtonsCoroutine = StartCoroutine(SmoothResize(86, 0.1f));
        selected = true;
        canvasGroup.interactable = true;
        canvas.PlayCursorSound();
        canvas.EventSystem.SetSelectedGameObject(defaultSelection);
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
        string path = Replay.FilePath[destination.Length..];
        int nextSlash = path.IndexOf(Path.DirectorySeparatorChar, 1);
        if (nextSlash != -1) {
            path = path[(nextSlash + 1)..];
        }

        if (Replay.IsTemporary || Replay.IsFavorited) {
            // Save / Unfavorite
            destination = Path.Combine(destination, "saved", path);
        } else {
            // Favorite
            destination = Path.Combine(destination, "favorite", path);
        }

        File.Move(Replay.FilePath, destination);
        Replay.FilePath = destination;
        UpdateText();
        canvas.PlayConfirmSound();
    }

    public void OnWatchClick() {
        NetworkHandler.StartReplay(Replay.ReplayFile);
    }

    public void OnRenameClick() {
        manager.StartRename(Replay);
    }

    public void OnExportClick() {
        TranslationManager tm = GlobalController.Instance.translationManager;
        StandaloneFileBrowser.SaveFilePanelAsync(tm.GetTranslation("ui.extras.replays.actions.export.prompt"), null, Replay.ReplayFile.GetDisplayName(), "mvlreplay", (file) => {
            if (string.IsNullOrWhiteSpace(file)) {
                return;
            }

            using FileStream stream = new(file, FileMode.OpenOrCreate);
            Replay.ReplayFile.WriteToStream(stream);
        });
    }

    public void OnDeleteClick() {
        manager.StartDeletion(Replay);
    }

    public void UpdateText() {
        if (Replay == null) {
            return;
        }

        TranslationManager tm = GlobalController.Instance.translationManager;
        BinaryReplayFile replayFile = Replay.ReplayFile;

        nameText.text = replayFile.GetDisplayName();
        dateText.text = DateTime.UnixEpoch.AddSeconds(replayFile.UnixTimestamp).ToLocalTime().ToString();
        
        if (!replayFile.IsCompatible) {
            if (replayFile.Version >= BinaryReplayFile.Versions.Length) {
                // Newer version
                warningText.text = tm.GetTranslationWithReplacements("ui.extras.replays.incompatible.future");
            } else {
                // Older version
                warningText.text = tm.GetTranslationWithReplacements("ui.extras.replays.incompatible", "version", BinaryReplayFile.Versions[replayFile.Version]);
            }
            warningText.color = criticalColor;
            foreach (var button in compatibleButtons) {
                button.interactable = false;
            }
        } else if (Replay.IsTemporary) {
            warningText.text = tm.GetTranslation("ui.extras.replays.temporary.nodelete");
            warningText.color = warningColor;
        } else if (Replay.IsFavorited) {
            warningText.text = tm.GetTranslation("ui.extras.replays.favorited");
            warningText.color = favoriteColor;
        } else {
            warningText.text = "";
        }

        mapImage.sprite = replayFile.GetMapSprite();
        if (!mapImage.sprite) {
            mapImage.sprite = defaultMapSprite;
        }

        if (Replay.IsTemporary) {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.save");
        } else if (Replay.IsFavorited) {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.unfavorite");
        } else {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.favorite");
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateText();
    }
}
