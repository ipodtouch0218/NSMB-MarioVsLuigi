using System;
using System.Collections.Generic;
using UnityEngine;
using NSMB.Utils;
using NSMB.UI.MainMenu;
using Photon.Realtime;
using Photon.Client;

public class ChatManager : MonoBehaviour, IOnEventCallback {

    //---Static Variables
    public static readonly Color32 Red = new Color32(219, 107, 107, 255);
    public static readonly Color32 Blue = new Color32(85, 85, 202, 255);

    public static ChatManager Instance { get; private set; }
    public static event Action<ChatMessage.ChatMessageData> OnChatMessage;

    //---Public Variables
    public readonly List<ChatMessage.ChatMessageData> chatHistory = new();

    public void Awake() {
        Instance = this;
    }

    public void OnEnable() {
        NetworkHandler.Client.AddCallbackTarget(this);
        OnChatMessage += OnChatMessageCallback;
    }

    public void OnDisable() {
        NetworkHandler.Client.RemoveCallbackTarget(this);
        OnChatMessage -= OnChatMessageCallback;
    }

    public void AddChatMessage(string message, int player, Color? color = null, bool filter = false) {
        if (filter) {
            message = message.Filter();
        }

        ChatMessage.ChatMessageData data = new() {
            isSystemMessage = false,
            playerNumber = player,
            color = color ?? Color.black,
            message = message,
        };
        chatHistory.Add(data);
        OnChatMessage?.Invoke(data);
    }

    private static readonly Color SystemMessageColor = new(0x55/255f, 0x55/255f, 0x55/255f, 1);
    public void AddSystemMessage(string key, Color? color = null, params string[] replacements) {
        ChatMessage.ChatMessageData data = new() {
            isSystemMessage = true,
            color = color ?? SystemMessageColor,
            message = key,
            replacements = replacements,
        };
        chatHistory.Add(data);
        OnChatMessage?.Invoke(data);
    }

    public void SendChatMessage(string text) {
        NetworkHandler.Client.OpRaiseEvent((byte) Enums.NetEvents.ChatMessage, text, new RaiseEventArgs {
            Receivers = ReceiverGroup.All
        }, SendOptions.SendReliable);
    }

    /* TODO
    public void IncomingPlayerMessage(string message, RpcInfo info) {
        NetworkRunner runner = NetworkHandler.Runner;
        PlayerRef player = info.Source;

        if (!player.IsRealPlayer) {
            return;
        }

        if (!player.TryGetPlayerData(out PlayerData data)) {
            return;
        }

        // Spam prevention & Muted
        if (data.IsMuted || data.MessageCooldownTimer.IsActive(runner)) {
            return;
        }

        // Validate message format
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        // Empty message
        if (string.IsNullOrWhiteSpace(message)) {
            return;
        }

        data.MessageCooldownTimer = TickTimer.CreateFromSeconds(runner, 0.5f);

        // Message seems fine, send to rest of lobby.
        SessionData.Instance.Rpc_ChatDisplayMessage(message, player);
    }
    */

    public void ClearChat() {
        chatHistory.Clear();
        if (MainMenuManager.Instance) {
            MainMenuManager.Instance.chat.ClearChat();
        }
    }

    //---Callbacks
    /* TODO
    private void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        foreach (ChatMessage.ChatMessageData data in chatHistory) {
            if (data.player == player) {
                data.player = PlayerRef.None;
            }
        }
    }

    private void OnPlayerDataReady(PlayerData pd) {
        if (pd.Owner == pd.Runner.LocalPlayer && pd.IsRoomOwner) {
            AddSystemMessage("ui.inroom.chat.hostreminder", Red);
        }

        AddSystemMessage("ui.inroom.chat.player.joined", Blue, "playername", pd.GetNickname());
    }
    */

    private void OnChatMessageCallback(ChatMessage.ChatMessageData data) {
        if (data.isSystemMessage) {
            Debug.Log($"[Chat] {GlobalController.Instance.translationManager.GetTranslationWithReplacements(data.message, data.replacements)}");
        } else {
            Player player = NetworkHandler.Client.CurrentRoom.Players[data.playerNumber];
            Debug.Log($"[Chat] ({player.UserId}) {data.message}");
        }
    }

    public void OnEvent(EventData photonEvent) {
        if (photonEvent.Code == (byte) Enums.NetEvents.ChatMessage) {
            if (!NetworkHandler.Client.CurrentRoom.Players.TryGetValue(photonEvent.Sender, out Player sender)) {
                return;
            }

            /* TODO
            if (data.IsMuted) {
                return;
            }
            */

            // Format message, in case we can't trust the host to do it for us.
            string message = (string) photonEvent.CustomData;
            message = message[..Mathf.Min(128, message.Length)];
            message = message.Replace("\n", " ").Trim();

            // Add username
            message = sender.NickName.ToValidUsername() + ": " + message.Filter();

            AddChatMessage(message, sender.ActorNumber);

            if (MainMenuManager.Instance) {
                PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(sender.ActorNumber);
                if (ple) {
                    ple.typingCounter = 0;
                }
            }
        }
    }
}
