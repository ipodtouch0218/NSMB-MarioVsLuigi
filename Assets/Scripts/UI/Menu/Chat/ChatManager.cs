using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class ChatManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ChatMessage messagePrefab;
    [SerializeField] private TMP_InputField chatbox;
    [SerializeField] private GameObject chatWindow;

    //---Private Variables
    private readonly List<ChatMessage> chatMessages = new();
    private int previousTextSize;

    public void OnEnable() {
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
    }

    public void OnDisable() {
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
    }

    private void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        foreach (ChatMessage message in chatMessages) {
            if (message.player == player)
                message.player = PlayerRef.None;
        }
    }

    public void UpdatePlayerColors() {
        foreach (ChatMessage message in chatMessages) {
            message.UpdatePlayerColor();
        }
    }

    public void AddChatMessage(string message, PlayerRef player, Color? color = null, bool filter = false) {

        ChatMessage chat = Instantiate(messagePrefab, chatWindow.transform);
        chat.gameObject.SetActive(true);

        if (filter)
            message = message.Filter();

        chat.Initialize(message, player, color);
        chatMessages.Add(chat);
        Canvas.ForceUpdateCanvases();
    }

    public void AddSystemMessage(string key, params string[] replacements) {
        AddSystemMessage(key, null, replacements);
    }

    public void AddSystemMessage(string key, Color? color = null, params string[] replacements) {

        ChatMessage chat = Instantiate(messagePrefab, chatWindow.transform);
        chat.gameObject.SetActive(true);

        color ??= Color.red;

        chat.InitializeSystem(key, replacements, color);
        chatMessages.Add(chat);
        Canvas.ForceUpdateCanvases();
    }

    public void OnTextboxChanged() {
        if (!MainMenuManager.Instance)
            return;

        int size = chatbox.text.Length;
        if (size == previousTextSize)
            return;

        previousTextSize = size;

        PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(NetworkHandler.Runner.LocalPlayer);
        if (!ple || ple.typingCounter > 2)
            return;

        SessionData.Instance.Rpc_UpdateTypingCounter();
    }

    public void SetTypingIndicator(PlayerRef player) {
        if (!MainMenuManager.Instance)
            return;

        PlayerListEntry ple = MainMenuManager.Instance.playerList.GetPlayerListEntry(player);
        if (ple) {
            ple.typingCounter = 4;
        }
    }

    public void SendChat() {
        NetworkRunner runner = NetworkHandler.Runner;
        PlayerData data = runner.GetLocalPlayerData();
        if (!data.MessageCooldownTimer.ExpiredOrNotRunning(runner)) {
            // Can't send a message yet.
            return;
        }

        string text = chatbox.text.Replace("\n", " ").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.StartsWith("/")) {
            AddSystemMessage("ui.inroom.chat.command");
            return;
        }

        SessionData.Instance.Rpc_ChatIncomingMessage(text);
        StartCoroutine(SelectTextboxNextFrame());
    }

    public void ClearChat() {
        foreach (ChatMessage message in chatMessages)
            Destroy(message.gameObject);

        chatMessages.Clear();
    }

    private IEnumerator SelectTextboxNextFrame() {
        yield return null;
        chatbox.SetTextWithoutNotify("");
        EventSystem.current.SetSelectedGameObject(chatbox.gameObject);
    }

    public void DisplayPlayerMessage(string message, PlayerRef source) {
        //what
        if (!source.IsValid)
            return;

        PlayerData data = source.GetPlayerData(NetworkHandler.Runner);

        if (!data || !data.Object.IsValid)
            return;

        if (data.IsMuted)
            return;

        //format message, in case we can't trust the host to do it for us.
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        //add username
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

        //spam prevention.
        if (data.IsMuted || !data.MessageCooldownTimer.ExpiredOrNotRunning(runner))
            return;

        //validate message format
        message = message[..Mathf.Min(128, message.Length)];
        message = message.Replace("\n", " ").Trim();

        //empty message
        if (string.IsNullOrWhiteSpace(message))
            return;

        data.MessageCooldownTimer = TickTimer.CreateFromSeconds(runner, 0.5f);

        //message seems fine, send to rest of lobby.
        SessionData.Instance.Rpc_ChatDisplayMessage(message, player);
    }
}
