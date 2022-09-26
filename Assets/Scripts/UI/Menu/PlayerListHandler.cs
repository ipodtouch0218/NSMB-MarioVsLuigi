using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Extensions;

public class PlayerListHandler : MonoBehaviour {

    [SerializeField] private GameObject contentPane, template;
    private readonly Dictionary<PlayerRef, PlayerListEntry> playerListEntries = new();

    private NetworkRunner Runner => NetworkHandler.Instance.runner;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
        AddPlayerEntry(player);
        UpdateAllPlayerEntries();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        RemovePlayerEntry(player);
        UpdateAllPlayerEntries();
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

        if (!playerListEntries.ContainsKey(player)) {
            GameObject go = Instantiate(template, contentPane.transform);
            go.name = $"{data.GetNickname()} ({data.GetUserId()})";
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
            UpdatePlayerEntry(player);
    }

    public void UpdatePlayerEntry(PlayerRef player) {
        if (!playerListEntries.ContainsKey(player)) {
            AddPlayerEntry(player);
            return;
        }

        playerListEntries[player].UpdateText();
        ReorderEntries();
    }

    public void ReorderEntries() {
        foreach (PlayerRef player in Runner.ActivePlayers.OrderByDescending(pr => (int) pr)) {

            if (!playerListEntries.ContainsKey(player))
                continue;

            playerListEntries[player].transform.SetAsFirstSibling();
        }
    }
}