using System;
using System.Collections.Generic;
using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;
using NSMB.UI.MainMenu;

public class ChatManager : MonoBehaviour {

    //---Static Variables
    public static ChatManager Instance { get; private set; }
    public static event Action<ChatMessage.ChatMessageData> OnChatMessage;

    //---Public Variables
    public readonly List<ChatMessage.ChatMessageData> chatHistory = new();

    public void Awake() {
        Instance = this;
    }

    public void OnEnable() {
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
        OnChatMessage += OnChatMessageCallback;
    }

    public void OnDisable() {
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
        OnChatMessage -= OnChatMessageCallback;
    }

    public void AddChatMessage(string message, PlayerRef player, Color? color = null, bool filter = false) {

        if (filter)
            message = message.Filter();

        ChatMessage.ChatMessageData data = new() {
            isSystemMessage = false,
            player = player,
            color = color ?? Color.black,
            message = message,
        };
        chatHistory.Add(data);
        OnChatMessage?.Invoke(data);
    }

    public void AddSystemMessage(string key, params string[] replacements) {
        AddSystemMessage(key, null, replacements);
    }

    private static readonly Color DarkerRed = new(0.8f, 0, 0, 1);
    public void AddSystemMessage(string key, Color? color = null, params string[] replacements) {

        ChatMessage.ChatMessageData data = new() {
            isSystemMessage = true,
            color = color ?? DarkerRed,
            message = key,
            replacements = replacements,
        };
        chatHistory.Add(data);
        OnChatMessage?.Invoke(data);
    }

    public void DisplayPlayerMessage(string message, PlayerRef source) {
        // What
        if (!source.IsValid)
            return;

        PlayerData data = source.GetPlayerData(NetworkHandler.Runner);

        if (!data || !data.Object.IsValid)
            return;

        if (data.IsMuted)
            return;

        // Format message, in case we can't trust the host to do it for us.
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        // Add username
        message = data.GetNickname() + ": " + message.Filter();

        AddChatMessage(message, source);

        if (MainMenuManager.Instance) {
            PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(source);
            if (ple) {
                ple.typingCounter = 0;
            }
        }
    }

    public void IncomingPlayerMessage(string message, RpcInfo info) {
        NetworkRunner runner = NetworkHandler.Runner;
        PlayerRef player = info.Source;

        if (!player.IsValid)
            return;

        PlayerData data = player.GetPlayerData(runner);
        if (!data || !data.Object.IsValid)
            return;

        // Spam prevention & Muted
        if (data.IsMuted || data.MessageCooldownTimer.IsActive(runner))
            return;

        // Validate message format
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        // Empty message
        if (string.IsNullOrWhiteSpace(message))
            return;

        data.MessageCooldownTimer = TickTimer.CreateFromSeconds(runner, 0.5f);

        // Message seems fine, send to rest of lobby.
        SessionData.Instance.Rpc_ChatDisplayMessage(message, player);
    }

    public void ClearChat() {
        chatHistory.Clear();
        if (MainMenuManager.Instance)
            MainMenuManager.Instance.chat.ClearChat();
    }

    //---Callbacks
    private void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        foreach (ChatMessage.ChatMessageData data in chatHistory) {
            if (data.player == player)
                data.player = PlayerRef.None;
        }
    }

    private void OnChatMessageCallback(ChatMessage.ChatMessageData data) {
        if (data.isSystemMessage) {
            Debug.Log($"[Chat] {GlobalController.Instance.translationManager.GetTranslationWithReplacements(data.message, data.replacements)}");
        } else {
            PlayerData pd = data.player.GetPlayerData(NetworkHandler.Runner);
            Debug.Log($"[Chat] ({pd.GetUserIdString()}) {data.message}");
        }
    }
}
