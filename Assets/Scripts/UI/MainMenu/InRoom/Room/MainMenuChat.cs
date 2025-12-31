using NSMB.Chat;
using NSMB.Utilities.Extensions;
using NSMB.UI.Translation;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.InRoom {

    public unsafe class MainMenuChat : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PlayerListHandler playerList;
        [SerializeField] private ChatMessage messagePrefab;
        [SerializeField] private TMP_InputField chatbox;
        [SerializeField] private UnityEngine.UI.Button sendBtn;
        [SerializeField] private TMP_Text chatPrompt;
        [SerializeField] private GameObject chatWindow;
        [SerializeField] private AudioSource sfx;

        //---Private Variables
        private readonly List<ChatMessage> chatMessages = new();
        private int previousTextLength;

        public void Initialize() {
            ChatManager.OnChatMessage += OnChatMessage;
            ChatManager.OnChatMessageRemoved += OnChatMessageRemoved;
            Settings.OnDisableChatChanged += OnDisableChatChanged;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            PlayerListEntry.PlayerMuteStateChanged += OnPlayerMuteStateChanged;
            PlayerListHandler.PlayerAdded += OnPlayerListEntryAddedOrRemoved;
            PlayerListHandler.PlayerRemoved += OnPlayerListEntryAddedOrRemoved;
            OnDisableChatChanged();
            messagePrefab.gameObject.SetActive(false);

            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
        }

        public void OnDestroy() {
            ChatManager.OnChatMessage -= OnChatMessage;
            ChatManager.OnChatMessageRemoved -= OnChatMessageRemoved;
            Settings.OnDisableChatChanged -= OnDisableChatChanged;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            PlayerListEntry.PlayerMuteStateChanged -= OnPlayerMuteStateChanged;
            PlayerListHandler.PlayerAdded -= OnPlayerListEntryAddedOrRemoved;
            PlayerListHandler.PlayerRemoved -= OnPlayerListEntryAddedOrRemoved;
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

            sfx.PlayOneShot(SoundEffect.UI_Chat_Send);

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
        private void OnChatMessage(ChatMessageData data) {
            ChatMessage chat = Instantiate(messagePrefab, chatWindow.transform);

            bool active;
            if (data.isSystemMessage) {
                active = true;
            } else {
                RuntimePlayer player = QuantumRunner.DefaultGame.Frames.Predicted.GetPlayerData(data.player);
                if (player == null) {
                    active = true;
                } else {
                    active = !ChatManager.Instance.mutedPlayers.Contains(player.UserId);
                }
            }
            chat.gameObject.SetActive(active);

            chat.Initialize(data);
            chatMessages.Add(chat);
        }

        private void OnChatMessageRemoved(ChatMessageData data) {
            int index = chatMessages.IndexOf(cm => cm.data == data);
            if (index == -1) {
                return;
            }

            Destroy(chatMessages[index].gameObject);
            chatMessages.RemoveAt(index);
        }

        private void OnDisableChatChanged() {
            foreach (ChatMessage msg in chatMessages) {
                msg.UpdateVisibleState();
            }

            sendBtn.interactable = chatbox.interactable = !Settings.Instance.GeneralDisableChat;

            if (!chatbox.interactable) {
                chatbox.text = "";
            }

            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        private void OnPlayerMuteStateChanged(PlayerListEntry player) {
            foreach (ChatMessage msg in chatMessages) {
                msg.UpdateVisibleState();
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            string key = Settings.Instance.GeneralDisableChat ? "ui.inroom.chat.disabled" : "ui.inroom.chat.prompt";

            chatPrompt.text = tm.GetTranslation(key);
            chatPrompt.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }

        public void OnTextboxChanged() {
            int size = chatbox.text.Length;
            if (size == previousTextLength) {
                return;
            }

            previousTextLength = size;

            var game = QuantumRunner.DefaultGame;
            List<PlayerRef> localPlayers = game.GetLocalPlayers();
            if (localPlayers.Count <= 0) {
                return;
            }

            PlayerListEntry ple = playerList.GetPlayerEntry(localPlayers[0]);
            if (!ple || ple.typingCounter > 2) {
                return;
            }

            var startTypingCommand = new CommandStartTyping();
            foreach (var player in game.GetLocalPlayerSlots()) {
                game.SendCommand(player, startTypingCommand);
            }
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            Frame f = e.Game.Frames.Verified;
            RuntimePlayer runtimePlayer = f.GetPlayerData(e.Player);
            foreach (var chatMessage in chatMessages) {
                if (chatMessage.data.userId == runtimePlayer.UserId) {
                    // Reassign this chat message
                    chatMessage.data.player = e.Player;
                }
            }
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            // Disassociate chat messages
            foreach (var chatMessage in chatMessages) {
                if (chatMessage.data.player == e.Player) {
                    chatMessage.data.player = PlayerRef.None;
                }
            }
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            ClearChat();
        }

        private void OnPlayerListEntryAddedOrRemoved(int index) {
            UpdatePlayerColors();
        }
    }
}
