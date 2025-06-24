using JimmysUnityUtilities;
using NSMB.Chat;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Chat {
    public class InGameChat : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private InGameChatMessage messagePrefab;
        [SerializeField] private Transform parent;

        //---Private Variables
        private readonly List<InGameChatMessage> activeMessages = new();

        public void OnEnable() {
            ChatManager.OnChatMessage += OnChatMessage;
        }

        public void OnDisable() {
            ChatManager.OnChatMessage -= OnChatMessage;
        }

        private void OnChatMessage(ChatMessageData data) {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game == null || !game.Frames.Predicted.RuntimeConfig.IsRealGame) {
                return;
            }

            InGameChatMessage newMessage = Instantiate(messagePrefab, parent);

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
            newMessage.gameObject.SetActive(active);
            newMessage.Initialize(data);
            newMessage.OnChatMessageDestroyed += OnChatMessageDestroyed;

            RectTransform newMessageTransform = newMessage.GetRectTransform();
            LayoutRebuilder.ForceRebuildLayoutImmediate(newMessageTransform);
            newMessageTransform.SetAnchoredPositionY(-newMessageTransform.sizeDelta.y);
            foreach (var message in activeMessages) {
                // Move other messages
                message.AdjustPosition(newMessageTransform.sizeDelta.y);
            }
            activeMessages.Add(newMessage);
        }

        private void OnChatMessageDestroyed(InGameChatMessage chat) {
            activeMessages.Remove(chat);
            chat.OnChatMessageDestroyed -= OnChatMessageDestroyed;
        }
    }
}