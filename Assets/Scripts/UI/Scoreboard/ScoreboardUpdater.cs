using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using System.Linq;

public class ScoreboardUpdater : MonoBehaviour {

    //---Static Variables
    public static ScoreboardUpdater Instance { get; private set; }

    //---Serialized Variables
    [SerializeField] private GameObject entryTemplate, teamsHeader;
    [SerializeField] private TMP_Text spectatorText;
    [SerializeField] private Animator animator;

    //---Private Variables
    private readonly List<ScoreboardEntry> entries = new();
    private bool manuallyToggled, autoToggled;

    public void OnValidate() {
        this.SetIfNull(ref animator);
    }

    public void OnEnable() {
        ControlSystem.controls.UI.Scoreboard.performed += OnToggleScoreboard;
        GameManager.OnAllPlayersLoaded += OnAllPlayersLoaded;
    }

    public void OnDisable() {
        ControlSystem.controls.UI.Scoreboard.performed -= OnToggleScoreboard;
        GameManager.OnAllPlayersLoaded -= OnAllPlayersLoaded;
    }

    public void Awake() {
        Instance = this;

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
            PlayerData data = player.GetPlayerData();
            if (!data) {
                continue;
            }

            if (data.IsCurrentlySpectating) {
                count++;
            }
        }
        spectatorText.text = (count == 0) ? "" : "<sprite name=room_spectator><sprite name=hudnumber_x><sprite name=hudnumber_" + count + ">";
    }

    public void SetEnabled() {
        manuallyToggled = true;
        animator.SetFloat("speed", 1);
        animator.Play("toggle", 0, 0.999f);
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
        if (manuallyToggled) {
            return;
        }

        PlayAnimation(true);
        autoToggled = true;
    }

    public void OnRespawnToggle() {
        if (manuallyToggled) {
            return;
        }

        PlayAnimation(false);
        autoToggled = false;
    }

    public void RepositionEntries() {
        // Order the scoreboard entries by stars, lives, then id
        entries.Sort();
        entries.ForEach(se => se.transform.SetAsLastSibling());
        spectatorText.transform.SetAsLastSibling();
    }

    public void CreateEntries() {

        NetworkRunner runner = NetworkHandler.Runner;
        List<PlayerData> actualPlayers = runner.ActivePlayers
            .Select(pr => pr.GetPlayerData())
            .Where(pd => pd && !pd.IsCurrentlySpectating)
            .ToList();

        foreach (PlayerData player in actualPlayers) {
            GameObject entryObj = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entryObj.SetActive(true);
            entryObj.name = player.GetNickname();
            ScoreboardEntry entry = entryObj.GetComponent<ScoreboardEntry>();
            entry.target = GameManager.Instance.AlivePlayers.FirstOrDefault(pc => pc.Data == player);

            entries.Add(entry);
        }

        RepositionEntries();
    }

    public void DestroyEntry(ScoreboardEntry entry) {
        entries.Remove(entry);
        Destroy(entry.gameObject);
    }

    //---Callbacks
    private void OnToggleScoreboard(InputAction.CallbackContext context) {
        ManualToggle();
    }

    private void OnAllPlayersLoaded() {
        CreateEntries();
        UpdateSpectatorCount();

        if (Settings.Instance.generalScoreboardAlways) {
            SetEnabled();
        }
    }

    private void OnPlayerListChanged(NetworkRunner runner, PlayerRef player) {
        UpdateSpectatorCount();
    }
}
