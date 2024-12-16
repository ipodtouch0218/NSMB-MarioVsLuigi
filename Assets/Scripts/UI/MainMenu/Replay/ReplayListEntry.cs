using NSMB.Extensions;
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

    //---Serialized Variables
    [SerializeField] private TMP_Text nameText, dateText, warningText, favoriteButtonText;
    [SerializeField] private Image mapImage;
    [SerializeField] private Sprite defaultMapSprite;
    [SerializeField] private RectTransform dropDownRectTransform;
    [SerializeField] private Color criticalColor, warningColor, favoriteColor;

    //---Private Variables
    private ReplayListManager manager;
    private ReplayListManager.Replay replay;
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
        replay = ourReplay;
        gameObject.SetActive(true);
        UpdateText();
    }

    public void UpdateNavigation(ReplayListEntry previous) {

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
    }

    public void OnClick() {
        if (selected) {
            return;
        }
        if (showHideButtonsCoroutine != null) {
            StopCoroutine(showHideButtonsCoroutine);
        }
        showHideButtonsCoroutine = StartCoroutine(SmoothResize(86, 0.1f));
        manager.Selected(this);
        selected = true;
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Cursor);
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
        string destination = Path.Combine(Application.streamingAssetsPath, "replays");
        string path = replay.FilePath[destination.Length..];
        int nextSlash = path.IndexOf(Path.DirectorySeparatorChar, 1);
        if (nextSlash != -1) {
            path = path[(nextSlash + 1)..];
        }

        if (replay.IsTemporary || replay.IsFavorited) {
            // Save / Unfavorite
            destination = Path.Combine(destination, "saved", path);
        } else {
            // Favorite
            destination = Path.Combine(destination, "favorite", path);
        }

        File.Move(replay.FilePath, destination);
        replay.FilePath = destination;
        UpdateText();
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
    }

    public void OnWatchClick() {
        NetworkHandler.StartReplay(replay.ReplayFile);
    }

    public void OnRenameClick() {
        manager.StartRename(replay);
    }

    public void OnExportClick() {
        TranslationManager tm = GlobalController.Instance.translationManager;
        StandaloneFileBrowser.SaveFilePanelAsync(tm.GetTranslation("ui.extras.replays.actions.export.prompt"), null, replay.ReplayFile.GetDisplayName(), "mvlreplay", (file) => {
            if (string.IsNullOrWhiteSpace(file)) {
                return;
            }

            using FileStream stream = new(file, FileMode.OpenOrCreate);
            replay.ReplayFile.WriteToStream(stream);
        });
    }

    public void OnDeleteClick() {
        manager.StartDeletion(replay);
    }

    public void UpdateText() {
        if (replay == null) {
            return;
        }

        TranslationManager tm = GlobalController.Instance.translationManager;
        BinaryReplayFile replayFile = replay.ReplayFile;

        nameText.text = replayFile.GetDisplayName();
        dateText.text = DateTime.UnixEpoch.AddSeconds(replayFile.UnixTimestamp).ToLocalTime().ToString();
        
        if (!replayFile.IsCompatible) {
            warningText.text = tm.GetTranslation("ui.extras.replays.incompatible");
            warningText.color = criticalColor;
        } else if (replay.IsTemporary) {
            warningText.text = tm.GetTranslation("ui.extras.replays.temporary.nodelete");
            warningText.color = warningColor;
        } else if (replay.IsFavorited) {
            warningText.text = tm.GetTranslation("ui.extras.replays.favorited");
            warningText.color = favoriteColor;
        } else {
            warningText.text = "";
        }

        mapImage.sprite = replayFile.GetMapSprite();
        if (!mapImage.sprite) {
            mapImage.sprite = defaultMapSprite;
        }

        if (replay.IsTemporary) {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.save");
        } else if (replay.IsFavorited) {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.unfavorite");
        } else {
            favoriteButtonText.text = tm.GetTranslation("ui.extras.replays.actions.favorite");
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateText();
    }
}
