using NSMB.Extensions;
using NSMB.Translation;
using Photon.Realtime;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu {

    public unsafe class MainMenuChat : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private ChatMessage messagePrefab;
        [SerializeField] private TMP_InputField chatbox;
        [SerializeField] private UnityEngine.UI.Button sendBtn;
        [SerializeField] private TMP_Text chatPrompt;
        [SerializeField] private GameObject chatWindow;

        //---Private Variables
        private readonly List<ChatMessage> chatMessages = new();
        private int previousTextLength;

        public void OnEnable() {
            ChatManager.OnChatMessage += OnChatMessage;
            Settings.OnDisableChatChanged += OnDisableChatChanged;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnDisableChatChanged();
        }

        public void OnDisable() {
            ChatManager.OnChatMessage -= OnChatMessage;
            Settings.OnDisableChatChanged -= OnDisableChatChanged;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Start() {
            QuantumEvent.Subscribe<EventPlayerStartedTyping>(this, OnPlayerStartedTyping);   
        }

        public void ReplayChatMessages() {
            foreach (ChatMessage.ChatMessageData message in ChatManager.Instance.chatHistory) {
                OnChatMessage(message);
            }
            Canvas.ForceUpdateCanvases();
        }

        public void UpdatePlayerColors() {
            foreach (ChatMessage message in chatMessages) {
                message.UpdatePlayerColor();
            }
        }

        public void SendChat() {
            string text = chatbox.text.Replace("\n", " ").Trim();
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;

            List<PlayerRef> localPlayers = game.GetLocalPlayers();
            if (localPlayers.Count == 0) {
                return;
            }

            PlayerRef player = localPlayers[0];
            var playerData = f.Unsafe.GetPointer<PlayerData>(f.ResolveDictionary(f.Global->PlayerDatas)[player]);
            if (!playerData->CanSendChatMessage(f)) {
                return;
            }

            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Chat_Send);

            if (text.StartsWith('/')) {
                ChatManager.Instance.AddSystemMessage("ui.inroom.chat.command", ChatManager.Red);
            } else {
                ChatManager.Instance.SendChatMessage(text);
            }
            StartCoroutine(SelectTextboxNextFrame());
        }

        public void ClearChat() {
            foreach (ChatMessage message in chatMessages) {
                Destroy(message.gameObject);
            }

            chatMessages.Clear();
        }

        private IEnumerator SelectTextboxNextFrame() {
            yield return null;
            chatbox.SetTextWithoutNotify("");
            EventSystem.current.SetSelectedGameObject(chatbox.gameObject);
        }

        //---Callbacks
        public void OnChatMessage(ChatMessage.ChatMessageData data) {
            ChatMessage chat = Instantiate(messagePrefab, chatWindow.transform);
            chat.gameObject.SetActive(true);
            chat.Initialize(data);
            chatMessages.Add(chat);
        }

        public void OnDisableChatChanged() {
            foreach (ChatMessage msg in chatMessages) {
                msg.UpdateVisibleState(!Settings.Instance.GeneralDisableChat);
            }

            sendBtn.interactable = chatbox.interactable = !Settings.Instance.GeneralDisableChat;

            if (!chatbox.interactable) {
                chatbox.text = "";
            }

            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnLanguageChanged(TranslationManager tm) {
            string key;

            if (Settings.Instance.GeneralDisableChat) {
                key = "ui.inroom.chat.disabled";
            } else {
                key = "ui.inroom.chat.prompt";
            }

            chatPrompt.text = tm.GetTranslation(key);
        }

        public void OnTextboxChanged() {
            if (!MainMenuManager.Instance) {
                return;
            }

            int size = chatbox.text.Length;
            if (size == previousTextLength) {
                return;
            }

            previousTextLength = size;

            List<PlayerRef> localPlayers = QuantumRunner.DefaultGame.GetLocalPlayers();
            if (localPlayers.Count <= 0) {
                return;
            }

            PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(localPlayers[0]);
            if (!ple || ple.typingCounter > 2) {
                return;
            }

            QuantumRunner.DefaultGame.SendCommand(new CommandStartTyping());
        }

        private void OnPlayerStartedTyping(EventPlayerStartedTyping e) {
            PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(e.Player);
            if (ple) {
                ple.typingCounter = 4;
            }
        }
    }
}
