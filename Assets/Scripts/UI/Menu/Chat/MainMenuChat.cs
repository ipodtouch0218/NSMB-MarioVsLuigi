using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using NSMB.Translation;
using NSMB.Extensions;
using Photon.Realtime;
using Photon.Client;

namespace NSMB.UI.MainMenu {

    public class MainMenuChat : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private ChatMessage messagePrefab;
        [SerializeField] private TMP_InputField chatbox;
        [SerializeField] private Button sendBtn;
        [SerializeField] private TMP_Text chatPrompt;
        [SerializeField] private GameObject chatWindow;

        //---Private Variables
        private readonly List<ChatMessage> chatMessages = new();
        private int previousTextLength;

        public void OnEnable() {
            ChatManager.OnChatMessage += OnChatMessage;
            Settings.OnDisableChatChanged += OnDisableChatChanged;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            NetworkHandler.Client.EventReceived += OnEvent;

            OnDisableChatChanged();
        }

        public void OnDisable() {
            ChatManager.OnChatMessage -= OnChatMessage;
            Settings.OnDisableChatChanged -= OnDisableChatChanged;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            NetworkHandler.Client.EventReceived -= OnEvent;
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
            /* TODO
            NetworkRunner runner = NetworkHandler.Runner;
            PlayerData data = runner.GetLocalPlayerData();
            if (data.MessageCooldownTimer.IsActive(runner)) {
                // Can't send a message yet.
                return;
            }
            */

            string text = chatbox.text.Replace("\n", " ").Trim();
            if (string.IsNullOrWhiteSpace(text)) {
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

            PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(NetworkHandler.Client.LocalPlayer.ActorNumber);
            if (!ple || ple.typingCounter > 2) {
                return;
            }

            NetworkHandler.Client.OpRaiseEvent((byte) Enums.NetEvents.ChatTyping, null, new RaiseEventArgs {
                Receivers = ReceiverGroup.All,
            }, SendOptions.SendUnreliable);
        }

        private void OnEvent(EventData photonEvent) {
            if (photonEvent.Code == (byte) Enums.NetEvents.ChatTyping) {
                Player player = NetworkHandler.Client.CurrentRoom.Players[photonEvent.Sender];
                PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(player.ActorNumber);
                if (ple) {
                    ple.typingCounter = 4;
                }
            }
        }
    }
}
