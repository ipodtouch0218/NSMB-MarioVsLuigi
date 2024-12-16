using NSMB.Extensions;
using NSMB.Translation;
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
        [SerializeField] private AudioSource sfx;

        //---Private Variables
        private readonly List<ChatMessage> chatMessages = new();
        private int previousTextLength;

        public void Initialize() {
            ChatManager.OnChatMessage += OnChatMessage;
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
            Settings.OnDisableChatChanged -= OnDisableChatChanged;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
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
        public void OnChatMessage(ChatMessage.ChatMessageData data) {
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

        public void OnDisableChatChanged() {
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

            PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerEntry(localPlayers[0]);
            if (!ple || ple.typingCounter > 2) {
                return;
            }

            foreach (var player in QuantumRunner.DefaultGame.GetLocalPlayerSlots()) {
                QuantumRunner.DefaultGame.SendCommand(player, new CommandStartTyping());
            }
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            RuntimePlayer runtimePlayer = e.Frame.GetPlayerData(e.Player);
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
