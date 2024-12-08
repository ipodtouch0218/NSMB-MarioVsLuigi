using NSMB.Extensions;
using NSMB.Translation;
using NSMB.Utils;
using Quantum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessage : MonoBehaviour {

    //---Public Variables
    [NonSerialized] public ChatMessageData data;

    //---Serialized Variables
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private Image image;

    public void OnValidate() {
        this.SetIfNull(ref chatText);
        this.SetIfNull(ref image);
    }

    public void OnDestroy() {
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(TranslationManager tm) {
        chatText.text = tm.GetTranslationWithReplacements(data.message, data.replacements);
        chatText.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
    }

    public void Initialize(ChatMessageData data) {
        this.data = data;
        chatText.color = data.color;

        if (data.isSystemMessage) {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        } else {
            chatText.richText = false;
            chatText.text = data.message;
        }

        UpdateVisibleState();
        UpdatePlayerColor();
    }

    public void UpdateVisibleState() {
        gameObject.SetActive(data.isSystemMessage || (!Settings.Instance.GeneralDisableChat && !ChatManager.Instance.mutedPlayers.Contains(data.userId)));
    }

    public void UpdatePlayerColor() {
        if (data.isSystemMessage) {
            return;
        }

        image.color = Utils.GetPlayerColor(QuantumRunner.DefaultGame.Frames.Predicted, data.player, 0.2f);
    }

    public class ChatMessageData {
        public PlayerRef player;
        public string userId;
        public Color color;
        public bool isSystemMessage;
        public string message;
        public string[] replacements;
    }
}
