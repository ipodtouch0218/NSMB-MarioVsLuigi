using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class ChatManager : MonoBehaviour {

    [SerializeField] private ChatMessage messagePrefab;
    [SerializeField] private TMP_InputField chatbox;
    [SerializeField] private GameObject chatWindow;

    public void LocalChatMessage(string message, Color? color = null, bool filter = false) {
        float y = 0;
        for (int i = 0; i < chatWindow.transform.childCount; i++) {
            GameObject child = chatWindow.transform.GetChild(i).gameObject;
            if (!child.activeSelf)
                continue;

            y -= child.GetComponent<RectTransform>().rect.height + 20;
        }

        ChatMessage chat = Instantiate(messagePrefab, Vector3.zero, Quaternion.identity, chatWindow.transform);
        chat.gameObject.SetActive(true);

        if (color != null) {
            Color fColor = (Color) color;
            message = $"<color=#{(byte) (fColor.r * 255):X2}{(byte) (fColor.g * 255):X2}{(byte) (fColor.b * 255):X2}>" + message;
        }

        if (filter)
            message = message.Filter();

        chat.SetText(message);
        Canvas.ForceUpdateCanvases();
    }

    public void SendChat() {
        NetworkRunner runner = NetworkHandler.Runner;
        PlayerData data = runner.GetLocalPlayerData();
        if (!data?.MessageCooldownTimer.ExpiredOrNotRunning(runner) ?? true) {
            //can't send a message yet.
            return;
        }

        string text = chatbox.text.Replace("<", "«").Replace(">", "»").Trim();
        if (text == "")
            return;

        if (text.StartsWith("/")) {
            LocalChatMessage("Slash commands are no longer necessary. Click on a player's name to moderate your room instead!", Color.red);
            return;
        }

        IncomingPlayerMessage(text);
        StartCoroutine(SelectTextboxNextFrame());
    }

    private IEnumerator SelectTextboxNextFrame() {
        yield return null;
        EventSystem.current.SetSelectedGameObject(chatbox.gameObject);
    }

    public void DisplayPlayerMessage(string message, PlayerRef source) {
        //what
        if (!source.IsValid)
            return;

        PlayerData data = source.GetPlayerData(NetworkHandler.Runner);

        //format message, in case we can't trust the host to do it for us.
        message = message.Substring(0, Mathf.Min(128, message.Length));
        message = message.Replace("<", "«").Replace(">", "»").Replace("\n", " ").Trim();

        //add username
        message = data.GetNickname() + ": " + message.Filter();

        LocalChatMessage(message);
    }

    public void IncomingPlayerMessage(string message, RpcInfo info = default) {
        NetworkRunner runner = NetworkHandler.Runner;
        PlayerRef player = info.Source;

        //what?
        if (!player.IsValid)
            return;

        PlayerData data = player.GetPlayerData(runner);

        //spam prevention.
        if (!data.MessageCooldownTimer.ExpiredOrNotRunning(runner))
            return;

        //validate message format
        message = message.Substring(0, Mathf.Min(128, message.Length));
        message = message.Replace("<", "«").Replace(">", "»").Replace("\n", " ").Trim();

        //empty message
        if (string.IsNullOrWhiteSpace(message))
            return;

        //message seems fine, send to rest of lobby.

    }
}