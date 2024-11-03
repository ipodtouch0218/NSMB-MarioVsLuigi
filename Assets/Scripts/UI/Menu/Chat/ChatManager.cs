using NSMB.Utils;
using NSMB.UI.MainMenu;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ChatManager : MonoBehaviour {

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
        OnChatMessage += OnChatMessageCallback;
    }

    public void OnDisable() {
        OnChatMessage -= OnChatMessageCallback;
    }

    public void Start() {
        QuantumEvent.Subscribe<EventPlayerSentChatMessage>(this, OnPlayerSentChatMessage);
        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
        QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
    }

    public void AddChatMessage(string message, PlayerRef player, Color? color = null, bool filter = false) {
        if (filter) {
            message = message.Filter();
        }

        ChatMessage.ChatMessageData data = new() {
            isSystemMessage = false,
            player = player,
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
        QuantumRunner.DefaultGame.SendCommand(new CommandSendChatMessage {
            Message = text
        });
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
            RuntimePlayer runtimeData = QuantumRunner.DefaultGame.Frames.Predicted.GetPlayerData(data.player);
            Debug.Log($"[Chat] ({runtimeData.UserId}) {data.message}");
        }
    }

    public void OnPlayerSentChatMessage(EventPlayerSentChatMessage e) {

        /* TODO
        if (data.IsMuted) {
            return;
        }
        */

        // Format message, in case we can't trust the host to do it for us.
        string message = e.Message;
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        // Add username
        RuntimePlayer runtimeData = e.Frame.GetPlayerData(e.Player);
        message = runtimeData.PlayerNickname.ToValidUsername() + ": " + message.Filter();

        AddChatMessage(message, e.Player);

        if (MainMenuManager.Instance) {
            PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(e.Player);
            if (ple) {
                ple.typingCounter = 0;
            }
        }
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.WaitingForPlayers) {
            AddSystemMessage("ui.inroom.chat.server.started", Red);
        }
    }

    private void OnPlayerAdded(EventPlayerAdded e) {
        RuntimePlayer runtimeData = e.Frame.GetPlayerData(e.Player);
        AddSystemMessage("ui.inroom.chat.player.joined", Blue, "playername", runtimeData.PlayerNickname.ToValidUsername());
    }

    private void OnPlayerRemoved(EventPlayerRemoved e) {
        RuntimePlayer runtimeData = e.Frame.GetPlayerData(e.Player);
        AddSystemMessage("ui.inroom.chat.player.quit", Blue, "playername", runtimeData.PlayerNickname.ToValidUsername());
    }
}
