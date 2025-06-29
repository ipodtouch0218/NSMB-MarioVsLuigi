using NSMB.UI.Translation;
using NSMB.Utilities;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Chat {
    public unsafe class ChatManager : MonoBehaviour {

        //---Static Variables
        public static readonly Color32 Red = new Color32(219, 107, 107, 255);
        public static readonly Color32 Blue = new Color32(85, 85, 202, 255);

        public static ChatManager Instance { get; private set; }
        public static event Action<ChatMessageData> OnChatMessage;
        public static event Action<ChatMessageData> OnChatMessageRemoved;

        //---Public Variables
        public readonly List<ChatMessageData> chatHistory = new();
        public readonly HashSet<string> mutedPlayers = new();

        //---Private Variables
        private AssetRef<Map> currentMap;
        private AssetRef<GamemodeAsset> currentGamemode;
        private ChatMessageData changeMapMessage, changeGamemodeMessage;

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
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumEvent.Subscribe<EventPlayerSentChatMessage>(this, OnPlayerSentChatMessage);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded, FilterOutReplay);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved, FilterOutReplay);
            QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged, FilterOutReplay);
            QuantumEvent.Subscribe<EventPlayerKickedFromRoom>(this, OnPlayerKickedFromRoom, FilterOutReplay);
            QuantumEvent.Subscribe<EventPlayerUnbanned>(this, OnPlayerUnbanned, FilterOutReplay);
        }

        private void OnUpdateView(CallbackUpdateView e) {
            Frame f = e.Game.Frames.Predicted;
            ref var rules = ref f.Global->Rules;

            TranslationManager tm = GlobalController.Instance.translationManager;
            if (rules.Gamemode != currentGamemode) {
                RemoveChatMessage(changeGamemodeMessage);
                string gamemodeName;
                if (f.TryFindAsset(rules.Gamemode, out GamemodeAsset gamemode)) {
                    gamemodeName = tm.GetTranslation(gamemode.TranslationKey);
                } else {
                    gamemodeName = "???";
                }
                changeGamemodeMessage = AddSystemMessage("ui.inroom.chat.server.gamemode", Red, "gamemode", gamemodeName);
                currentGamemode = rules.Gamemode;
            }
            if (rules.Stage != currentMap) {
                RemoveChatMessage(changeMapMessage);
                string stageName;
                if (f.TryFindAsset(rules.Stage, out Map map)
                    && f.TryFindAsset(map.UserAsset, out VersusStageData stageData)) {
                    stageName = tm.GetTranslation(stageData.TranslationKey);
                } else {
                    stageName = "???";
                }
                changeMapMessage = AddSystemMessage("ui.inroom.chat.server.map", Red, "map", stageName);
                currentMap = rules.Stage;
            }
        }

        public ChatMessageData AddChatMessage(string message, PlayerRef player, Frame f, Color? color = null, bool filter = false) {
            if (filter) {
                message = message.Filter();
            }

            ChatMessageData data = new() {
                isSystemMessage = false,
                player = player,
                userId = f.GetPlayerData(player).UserId,
                color = color ?? Color.black,
                message = message,
            };
            OnChatMessage?.Invoke(data);
            return data;
        }

        private static readonly Color SystemMessageColor = new(0x55/255f, 0x55/255f, 0x55/255f, 1);
        public ChatMessageData AddSystemMessage(string key, Color? color = null, params string[] replacements) {
            ChatMessageData data = new() {
                isSystemMessage = true,
                color = color ?? SystemMessageColor,
                message = key,
                replacements = replacements,
            };
            OnChatMessage?.Invoke(data);
            return data;
        }

        public void SendChatMessage(string text) {
            QuantumRunner.DefaultGame.SendCommand(new CommandSendChatMessage {
                Message = text
            });
        }

        public void RemoveChatMessage(ChatMessageData data) {
            int index = chatHistory.IndexOf(data);
            if (index == -1) {
                return;
            }

            OnChatMessageRemoved?.Invoke(data);
            chatHistory.RemoveAt(index);
        }

        //---Callbacks
        private void OnGameStarted(CallbackGameStarted e) {
            chatHistory.Clear();
            mutedPlayers.Clear();
            ref var rules = ref e.Game.Frames.Predicted.Global->Rules;
            currentGamemode = rules.Gamemode;
            currentMap = rules.Stage;
        }

        private void OnChatMessageCallback(ChatMessageData data) {
            chatHistory.Add(data);

            if (!IsReplay) {
                if (data.isSystemMessage) {
                    Debug.Log($"[Chat] {GlobalController.Instance.translationManager.GetTranslationWithReplacements(data.message, data.replacements)}");
                } else {
                    RuntimePlayer runtimeData = QuantumRunner.DefaultGame.Frames.Predicted.GetPlayerData(data.player);
                    Debug.Log($"[Chat] ({runtimeData.UserId}) {data.message}");
                }
            }
        }

        private void OnPlayerSentChatMessage(EventPlayerSentChatMessage e) {
            // Format message, in case we can't trust the host to do it for us.
            string message = e.Message;
            message = message[..Mathf.Min(128, message.Length)];
            message = message.Replace("\n", " ").Trim();

            // Add username
            Frame f = e.Game.Frames.Verified;
            RuntimePlayer runtimeData = f.GetPlayerData(e.Player);
            message = runtimeData.PlayerNickname.ToValidNickname(f, e.Player) + ": " + message.Filter();

            AddChatMessage(message, e.Player, f);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.WaitingForPlayers) {
                AddSystemMessage("ui.inroom.chat.server.started", Red);
            }
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            Frame f = e.Game.Frames.Predicted;
            RuntimePlayer runtimeData = f.GetPlayerData(e.Player);
            AddSystemMessage("ui.inroom.chat.player.joined", Blue, "playername", runtimeData.PlayerNickname.ToValidNickname(f, e.Player));
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            Frame f = e.Game.Frames.Predicted;
            RuntimePlayer runtimeData = f.GetPlayerData(e.Player);
            AddSystemMessage("ui.inroom.chat.player.quit", Blue, "playername", runtimeData.PlayerNickname.ToValidNickname(f, e.Player));
        }

        private void OnPlayerKickedFromRoom(EventPlayerKickedFromRoom e) {
            Frame f = e.Game.Frames.Predicted;
            RuntimePlayer runtimeData = f.GetPlayerData(e.Player);
            AddSystemMessage(e.Banned ? "ui.inroom.chat.player.banned" : "ui.inroom.chat.player.kicked", Blue, "playername", runtimeData.PlayerNickname.ToValidNickname(f, e.Player));
        }

        private void OnPlayerUnbanned(EventPlayerUnbanned e) {
            Frame f = e.Game.Frames.Predicted;
            AddSystemMessage("ui.inroom.chat.player.unbanned", Blue, "playername", e.PlayerInfo.Nickname.ToString().ToValidNickname(f, default));
        }

        private void OnHostChanged(EventHostChanged e) {
            if (e.Game.PlayerIsLocal(e.NewHost)) {
                AddSystemMessage("ui.inroom.chat.hostreminder", Red);
            }
        }
    }
}
