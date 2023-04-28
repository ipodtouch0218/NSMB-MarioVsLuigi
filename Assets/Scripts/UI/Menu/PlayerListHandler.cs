using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Extensions;

public class PlayerListHandler : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject contentPane, template;

    //---Private Variables
    private readonly Dictionary<PlayerRef, PlayerListEntry> playerListEntries = new();

    //---Properties
    private NetworkRunner Runner => NetworkHandler.Instance.runner;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        AddPlayerEntry(player);
        UpdateAllPlayerEntries();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        RemovePlayerEntry(player);
        UpdateAllPlayerEntries();
    }

    public void OnEnable() {
        if (!NetworkHandler.Instance.runner)
            return;

        if (NetworkHandler.Instance.runner.SessionInfo.IsValid)
            PopulatePlayerEntries(true);

        NetworkHandler.OnPlayerJoined += OnPlayerJoined;
        NetworkHandler.OnPlayerLeft += OnPlayerLeft;
    }

    public void OnDisable() {
        RemoveAllPlayerEntries();

        NetworkHandler.OnPlayerJoined -= OnPlayerJoined;
        NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
    }

    public void PopulatePlayerEntries(bool addSelf) {
        RemoveAllPlayerEntries();
        foreach (PlayerRef player in Runner.ActivePlayers) {
            if (addSelf || Runner.LocalPlayer != player)
                AddPlayerEntry(player);
        }
    }

    public void AddPlayerEntry(PlayerRef player) {
        PlayerData data = player.GetPlayerData(Runner);
        if (!data || !template)
            return;

        if (!playerListEntries.ContainsKey(player)) {
            GameObject go = Instantiate(template, contentPane.transform);
            go.name = $"{data.GetNickname()} ({data.GetUserIdString()})";
            go.SetActive(true);
            playerListEntries[player] = go.GetComponent<PlayerListEntry>();
            playerListEntries[player].player = player;
        }

        UpdatePlayerEntry(player);
    }

    public void RemoveAllPlayerEntries() {
        playerListEntries.Values.ToList().ForEach(entry => Destroy(entry.gameObject));
        playerListEntries.Clear();
    }

    public void RemovePlayerEntry(PlayerRef player) {
        if (!playerListEntries.ContainsKey(player))
            return;

        Destroy(playerListEntries[player].gameObject);
        playerListEntries.Remove(player);
    }

    public void UpdateAllPlayerEntries() {
        foreach (PlayerRef player in Runner.ActivePlayers)
            UpdatePlayerEntry(player, false);

        if (MainMenuManager.Instance)
            MainMenuManager.Instance.chat.UpdatePlayerColors();
    }

    public void UpdatePlayerEntry(PlayerRef player, bool updateChat = true) {
        if (!playerListEntries.ContainsKey(player)) {
            AddPlayerEntry(player);
            return;
        }

        playerListEntries[player].UpdateText();
        ReorderEntries();

        if (updateChat && MainMenuManager.Instance)
            MainMenuManager.Instance.chat.UpdatePlayerColors();
    }

    public void ReorderEntries() {
        foreach (PlayerRef player in Runner.ActivePlayers.OrderBy(pr => (int) pr)) {

            if (!playerListEntries.ContainsKey(player))
                continue;

            playerListEntries[player].transform.SetAsFirstSibling();
        }
    }
}
