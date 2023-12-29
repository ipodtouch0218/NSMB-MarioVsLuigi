using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Utils;
using NSMB.Translation;

public class ChatMessage : MonoBehaviour {

    //---Public Variables
    [NonSerialized] public ChatMessageData data;

    //---Serialized Variables
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private Image image;

    public void OnValidate() {
        if (!chatText) chatText = GetComponent<TMP_Text>();
        if (!image) image = GetComponent<Image>();
    }

    public void OnDestroy() {
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(TranslationManager tm) {
        chatText.text = tm.GetTranslationWithReplacements(data.message, data.replacements);
        //chatText.isRightToLeftText = tm.RightToLeft;

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

        UpdatePlayerColor();
    }

    public void UpdatePlayerColor() {
        if (data.isSystemMessage)
            return;

        image.color = Utils.GetPlayerColor(NetworkHandler.Runner, data.player, 0.15f);
    }

    public class ChatMessageData {
        public PlayerRef player;
        public Color color;
        public bool isSystemMessage;
        public string message;
        public string[] replacements;
    }
}
