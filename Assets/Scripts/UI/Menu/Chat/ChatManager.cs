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

    public void OnEnable() {
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
    }

    public void OnDisable() {
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
    }

    private void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        foreach (ChatMessage message in chatMessages) {
            if (message.player == player)
                message.player = -1;
        }
    }

    public void UpdatePlayerColors() {
        foreach (ChatMessage message in chatMessages) {
            message.UpdatePlayerColor();
        }
    }

    public void AddChatMessage(string message, PlayerRef player, Color? color = null, bool filter = false) {

        ChatMessage chat = Instantiate(messagePrefab, Vector3.zero, Quaternion.identity, chatWindow.transform);
        chat.gameObject.SetActive(true);

        if (color != null) {
            Color fColor = (Color) color;
            message = $"<color=#{(byte) (fColor.r * 255):X2}{(byte) (fColor.g * 255):X2}{(byte) (fColor.b * 255):X2}>" + message;
        }

        if (filter)
            message = message.Filter();

        chat.Initialize(message, player);
        chatMessages.Add(chat);
        Canvas.ForceUpdateCanvases();
    }

    public void AddSystemMessage(string key, params string[] replacements) {
        AddSystemMessage(key, null, replacements);
    }

    public void AddSystemMessage(string key, Color? color = null, params string[] replacements) {
        AddChatMessage(GlobalController.Instance.translationManager.GetTranslationWithReplacements(key, replacements), PlayerRef.None, color ?? Color.red);
    }

    public void SendChat() {
        NetworkRunner runner = NetworkHandler.Runner;
        PlayerData data = runner.GetLocalPlayerData();
        if (!data.MessageCooldownTimer.ExpiredOrNotRunning(runner)) {
            //can't send a message yet.
            return;
        }

        string text = chatbox.text.Replace("<", "«").Replace(">", "»").Trim();
        if (text == "")
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
        chatbox.text = "";
        yield return null;
        chatbox.text = "";
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
        message = message.Substring(0, Mathf.Min(128, message.Length));
        message = message.Replace("<", "«").Replace(">", "»").Replace("\n", " ").Trim();

        //add username
        message = data.GetNickname() + ": " + message.Filter();

        AddChatMessage(message, source);
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
        message = message.Substring(0, Mathf.Min(128, message.Length));
        message = message.Replace("<", "«").Replace(">", "»").Replace("\n", " ").Trim();

        //empty message
        if (string.IsNullOrWhiteSpace(message))
            return;

        data.MessageCooldownTimer = TickTimer.CreateFromSeconds(runner, 0.5f);

        //message seems fine, send to rest of lobby.
        SessionData.Instance.Rpc_ChatDisplayMessage(message, player);
    }
}
