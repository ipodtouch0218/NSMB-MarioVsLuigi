using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;

public class ScoreboardUpdater : MonoBehaviour {

    //---Static Variables
    public static ScoreboardUpdater Instance { get; private set; }
    private static IComparer<ScoreboardEntry> entryComparer;

    //---Serialized Variables
    [SerializeField] private GameObject entryTemplate, teamsHeader;
    [SerializeField] private TMP_Text spectatorText;

    //---Private Variables
    private readonly List<ScoreboardEntry> entries = new();
    private Animator animator;
    private bool manuallyToggled = false, autoToggled = false;

    public void OnEnable() {
        ControlSystem.controls.UI.Scoreboard.performed += OnToggleScoreboard;
        GameData.OnAllPlayersLoaded += OnAllPlayersLoaded;
    }

    public void OnDisable() {
        ControlSystem.controls.UI.Scoreboard.performed -= OnToggleScoreboard;
        GameData.OnAllPlayersLoaded -= OnAllPlayersLoaded;
    }

    public void Awake() {
        Instance = this;
        animator = GetComponent<Animator>();
        entryComparer ??= new ScoreboardEntry.EntryComparer();

        teamsHeader.SetActive(SessionData.Instance ? SessionData.Instance.Teams : false);

        NetworkHandler.OnPlayerJoined += OnPlayerListChanged;
        NetworkHandler.OnPlayerLeft += OnPlayerListChanged;
        UpdateSpectatorCount();
    }

    public void OnDestroy() {
        NetworkHandler.OnPlayerJoined -= OnPlayerListChanged;
        NetworkHandler.OnPlayerLeft -= OnPlayerListChanged;
    }

    public void UpdateSpectatorCount() {
        int count = 0;
        foreach (var player in NetworkHandler.Runner.ActivePlayers) {
            PlayerData data = player.GetPlayerData(NetworkHandler.Runner);
            if (!data)
                continue;

            if (!data.IsCurrentlySpectating)
                continue;

            count++;
        }
        spectatorText.text = (count == 0) ? "" : "<sprite name=room_spectator> <sprite name=hudnumber_" + count + ">";
    }

    public void SetEnabled() {
        manuallyToggled = true;
        animator.SetFloat("speed", 1);
        animator.Play("toggle", 0, 0.99f);
    }

    public void ManualToggle() {
        if (autoToggled && !manuallyToggled) {
            // exception: the scoreboard's already open. close.
            manuallyToggled = false;
            autoToggled = false;
        } else {
            manuallyToggled = !manuallyToggled;
        }
        PlayAnimation(manuallyToggled);
    }

    private void PlayAnimation(bool enabled) {
        animator.SetFloat("speed", enabled ? 1 : -1);
        animator.Play("toggle", 0, Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime));
    }

    public void OnDeathToggle() {
        if (manuallyToggled)
            return;

        PlayAnimation(true);
        autoToggled = true;
    }

    public void OnRespawnToggle() {
        if (manuallyToggled)
            return;

        PlayAnimation(false);
        autoToggled = false;
    }

    public void RepositionEntries() {
        // Order the scoreboard entries by stars, lives, then id
        entries.Sort(entryComparer);
        entries.ForEach(se => se.transform.SetAsLastSibling());
    }

    public void CreateEntries(IEnumerable<PlayerController> players) {
        foreach (PlayerController player in players) {
            if (!player)
                continue;

            PlayerData data = player.Object.InputAuthority.GetPlayerData(player.Runner);

            GameObject entryObj = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entryObj.SetActive(true);
            entryObj.name = data.GetNickname();
            ScoreboardEntry entry = entryObj.GetComponent<ScoreboardEntry>();
            entry.target = player;

            entries.Add(entry);
        }

        RepositionEntries();
    }

    //---Callbacks

    private void OnToggleScoreboard(InputAction.CallbackContext context) {
        ManualToggle();
    }

    private void OnAllPlayersLoaded() {
        CreateEntries(GameData.Instance.AlivePlayers);

        if (Settings.Instance.genericScoreboardAlways)
            SetEnabled();
    }

    private void OnPlayerListChanged(NetworkRunner runner, PlayerRef player) {
        UpdateSpectatorCount();
    }
}
