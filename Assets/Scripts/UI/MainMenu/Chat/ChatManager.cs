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
    public readonly HashSet<string> mutedPlayers = new();

    //---Serialized Variables
    [SerializeField] private PlayerListHandler playerList;

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
        QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged);
    }

    public void AddChatMessage(string message, PlayerRef player, Frame f, Color? color = null, bool filter = false) {
        if (filter) {
            message = message.Filter();
        }

        ChatMessage.ChatMessageData data = new() {
            isSystemMessage = false,
            player = player,
            userId = f.GetPlayerData(player).UserId,
            color = color ?? Color.black,
            message = message,
        };
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
        OnChatMessage?.Invoke(data);
    }

    public void SendChatMessage(string text) {
        QuantumRunner.DefaultGame.SendCommand(new CommandSendChatMessage {
            Message = text
        });
    }

    //---Callbacks
    private void OnChatMessageCallback(ChatMessage.ChatMessageData data) {
        if (NetworkHandler.IsReplay) {
            return;
        }

        if (data.isSystemMessage) {
            Debug.Log($"[Chat] {GlobalController.Instance.translationManager.GetTranslationWithReplacements(data.message, data.replacements)}");
        } else {
            RuntimePlayer runtimeData = QuantumRunner.DefaultGame.Frames.Predicted.GetPlayerData(data.player);
            Debug.Log($"[Chat] ({runtimeData.UserId}) {data.message}");
        }
    }

    public void OnPlayerSentChatMessage(EventPlayerSentChatMessage e) {
        // Format message, in case we can't trust the host to do it for us.
        string message = e.Message;
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        // Add username
        RuntimePlayer runtimeData = e.Frame.GetPlayerData(e.Player);
        message = runtimeData.PlayerNickname.ToValidUsername(e.Frame, e.Player) + ": " + message.Filter();

        AddChatMessage(message, e.Player, e.Frame);
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.WaitingForPlayers) {
            AddSystemMessage("ui.inroom.chat.server.started", Red);
        }
    }

    private void OnPlayerAdded(EventPlayerAdded e) {
        RuntimePlayer runtimeData = e.Frame.GetPlayerData(e.Player);
        AddSystemMessage("ui.inroom.chat.player.joined", Blue, "playername", runtimeData.PlayerNickname.ToValidUsername(e.Frame, e.Player));
    }

    private void OnPlayerRemoved(EventPlayerRemoved e) {
        RuntimePlayer runtimeData = e.Frame.GetPlayerData(e.Player);
        AddSystemMessage("ui.inroom.chat.player.quit", Blue, "playername", runtimeData.PlayerNickname.ToValidUsername(e.Frame, e.Player));
    }

    private void OnHostChanged(EventHostChanged e) {
        if (e.Game.PlayerIsLocal(e.NewHost)) {
            AddSystemMessage("ui.inroom.chat.hostreminder", Red);
        }
    }
}
