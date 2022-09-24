using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Extensions;

public class PlayerListHandler : MonoBehaviour {

    [SerializeField] private GameObject contentPane, template;
    private readonly Dictionary<string, PlayerListEntry> playerListEntries = new();

    private NetworkRunner Runner => NetworkHandler.Instance.runner;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        if (Runner.LocalPlayer != player) {
            AddPlayerEntry(player);
            UpdateAllPlayerEntries();
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        if (Runner.LocalPlayer != player) {
            RemovePlayerEntry(player);
            UpdateAllPlayerEntries();
        }
    }

    //public void OnPlayerPropertiesUpdate(PlayerRef targetPlayer, Hashtable changedProps) {
    //    if (changedProps.ContainsKey(Enums.NetPlayerProperties.Spectator))
    //        UpdateAllPlayerEntries();
    //    else
    //        UpdatePlayerEntry(targetPlayer);
    //}

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

        string id = data.GetUserId();
        if (!playerListEntries.ContainsKey(id)) {
            GameObject go = Instantiate(template, contentPane.transform);
            go.name = $"{data.GetNickname()} ({id})";
            go.SetActive(true);
            playerListEntries[id] = go.GetComponent<PlayerListEntry>();
            playerListEntries[id].player = player;
        }

        UpdatePlayerEntry(player);
    }

    public void RemoveAllPlayerEntries() {
        playerListEntries.Values.ToList().ForEach(entry => Destroy(entry.gameObject));
        playerListEntries.Clear();
    }

    public void RemovePlayerEntry(PlayerRef player) {
        PlayerData data = player.GetPlayerData(Runner);
        string userId = data.GetUserId();

        if (!playerListEntries.ContainsKey(userId))
            return;

        Destroy(playerListEntries[userId].gameObject);
        playerListEntries.Remove(userId);
    }

    public void UpdateAllPlayerEntries() {
        foreach (PlayerRef player in Runner.ActivePlayers)
            UpdatePlayerEntry(player);
    }

    public void UpdatePlayerEntry(PlayerRef player) {
        PlayerData data = player.GetPlayerData(Runner);
        string id = data.GetUserId();
        if (!playerListEntries.ContainsKey(id)) {
            AddPlayerEntry(player);
            return;
        }

        playerListEntries[id].UpdateText();
        ReorderEntries();
    }

    public void ReorderEntries() {
        foreach (PlayerRef player in Runner.ActivePlayers.OrderByDescending(pr => (int) pr)) {
            PlayerData data = player.GetPlayerData(Runner);
            string id = data.GetUserId();
            if (!playerListEntries.ContainsKey(id))
                continue;

            playerListEntries[id].transform.SetAsFirstSibling();
        }
    }
}