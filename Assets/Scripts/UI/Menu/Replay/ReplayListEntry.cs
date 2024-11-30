using NSMB.Translation;
using Quantum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReplayListEntry : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text nameText, dateText, warningText, favoriteButtonText;
    [SerializeField] private Image mapImage;
    [SerializeField] private Sprite defaultMapSprite;
    [SerializeField] private RectTransform dropDownRectTransform;

    //---Private Variables
    private ReplayListManager manager;
    private ReplayListManager.Replay replay;

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

    public void OnClick() {
        dropDownRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 86);
        Canvas.ForceUpdateCanvases();
        manager.layout.SetLayoutVertical();
    }

    public void OnWatchClick() {
        NetworkHandler.StartReplay(replay.ReplayFile);
    }

    public void UpdateText() {
        TranslationManager tm = GlobalController.Instance.translationManager;
        BinaryReplayFile replayFile = replay.ReplayFile;

        nameText.text = replayFile.GetDisplayName();
        dateText.text = DateTime.UnixEpoch.AddSeconds(replayFile.UnixTimestamp).ToString();
        
        if (!replayFile.IsCompatible) {
            warningText.text = tm.GetTranslation("ui.extras.replays.incompatible");
        } else if (replay.IsTemporary) {
            warningText.text = tm.GetTranslation("ui.extras.replays.temporary");
        } else if (replay.IsFavorited) {
            warningText.text = tm.GetTranslation("ui.extras.replays.favorited");
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
