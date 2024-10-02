using NSMB.Utils;
using Quantum;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreboardUpdater : MonoBehaviour {

    //---Properties
    public bool RequestSorting { get; set; }

    //---Serialized Variables
    [SerializeField] private ScoreboardEntry entryTemplate;
    [SerializeField] private GameObject teamHeader;
    [SerializeField] private TMP_Text spectatorText, teamHeaderText;
    [SerializeField] private Animator animator;
    [SerializeField] private InputActionReference toggleScoreboardAction;

    //---Private Variables
    private EntityRef owningPlayer;
    private bool isToggled;
    private List<ScoreboardEntry> entries = new();

    public void Initialize(EntityRef owner) {
        owningPlayer = owner;
        ShowWithoutAnimation();
    }

    public void OnEnable() {
        toggleScoreboardAction.action.performed += OnToggleScoreboard;
        toggleScoreboardAction.action.actionMap.Enable();
    }

    public void OnDisable() {
        toggleScoreboardAction.action.performed -= OnToggleScoreboard;
    }

    public unsafe void Start() {
        // Populate the scoreboard if we're a late joiner
        QuantumGame game = QuantumRunner.DefaultGame;
        Frame f;
        if (game != null
            && (f = game.Frames.Predicted) != null
            && f.Global->GameState >= GameState.Starting) {

            PopulateScoreboard(f);
        }

        QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
        QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
        QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
        QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
        QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
        QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        if (!RequestSorting) {
            return;
        }

        Frame f = e.Game.Frames.Predicted;
        SortScoreboard(f);
        RequestSorting = false;
    }

    public unsafe void PopulateScoreboard(Frame f) {
        UpdateTeamHeader(f);
        UpdateSpectatorCount(f);

        var playerFilter = f.Filter<MarioPlayer>();
        while (playerFilter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
            ScoreboardEntry newEntry = Instantiate(entryTemplate, entryTemplate.transform.parent);
            newEntry.Initialize(f, entity, this);
            entries.Add(newEntry);
        }

        SortScoreboard(f);
    }

    public unsafe void SortScoreboard(Frame f) {
        entries.Sort((se1, se2) => {
            var mario1 = f.Unsafe.GetPointer<MarioPlayer>(se1.Target);
            var mario2 = f.Unsafe.GetPointer<MarioPlayer>(se2.Target);

            if (mario1->Lives != 0 && mario2->Lives == 0) {
                return 1;
            } else if (mario1->Lives == 0 && mario2->Lives != 0) {
                return -1;
            }

            if (mario1->Stars > mario2->Stars) {
                return 1;
            } else if (mario1->Stars < mario2->Stars) {
                return -1;
            }

            if (mario1->Lives > mario2->Lives) {
                return 1;
            } else if (mario1->Lives < mario2->Lives) {
                return -1;
            }

            return (mario1->PlayerRef > mario2->PlayerRef) ? 1 : -1;
        });
    }

    public unsafe void UpdateTeamHeader(Frame f) {
        bool teamsEnabled = f.Global->Rules.TeamsEnabled;
        teamHeader.SetActive(teamsEnabled);

        if (!teamsEnabled) {
            return;
        }

        TeamAsset[] teamAssets = f.SimulationConfig.Teams;
        StringBuilder result = new();

        Dictionary<int, int> teams = QuantumUtils.GetTeamStars(f);
        var orderedTeams = teams.OrderBy(kvp => kvp.Key);
        foreach ((int key, int stars) in orderedTeams) {
            if (key < 0 || key >= teamAssets.Length) {
                continue;
            }

            result.Append(Settings.Instance.GraphicsColorblind ? teamAssets[key].textSpriteColorblind : teamAssets[key].textSpriteNormal);
            result.Append(Utils.GetSymbolString("x" + stars));
        }

        teamHeaderText.text = result.ToString();
    }

    public unsafe void UpdateSpectatorCount(Frame f) {
        int spectators = 0;
        var playerDataFilter = f.Filter<PlayerData>();
        while (playerDataFilter.NextUnsafe(out _, out PlayerData* playerData)) {
            if (playerData->IsSpectator) {
                spectators++;
            }
        }

        if (spectators > 0) {
            spectatorText.text = "<sprite name=room_spectator>" + Utils.GetSymbolString("x" + spectators.ToString());
        } else {
            spectatorText.text = "";
        }
    }

    public void Toggle() {
        isToggled = !isToggled;
        PlayAnimation(isToggled);
    }

    public void Show() {
        isToggled = true;
        PlayAnimation(isToggled);
    }

    public void ShowWithoutAnimation() {
        isToggled = true;
        animator.SetFloat("speed", 1);
        animator.Play("toggle", 0, 0.999f);
    }

    public void Hide() {
        isToggled = false;
        PlayAnimation(isToggled);
    }

    public void PlayAnimation(bool enabled) {
        animator.SetFloat("speed", enabled ? 1 : -1);
        animator.Play("toggle", 0, Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime));
    }

    private void OnToggleScoreboard(InputAction.CallbackContext context) {
        if (context.canceled) {
            return;
        }

        Toggle();
    }

    private void OnGameStateChanged(EventGameStateChanged e) {
        if (e.NewState == GameState.Starting) {
            // Ready to go.
            PopulateScoreboard(e.Frame);
        }
    }

    private void OnMarioPlayerDroppedStar(EventMarioPlayerDroppedStar e) {
        UpdateTeamHeader(e.Frame);
    }

    private void OnMarioPlayerDied(EventMarioPlayerDied e) {
        if (e.Entity != owningPlayer) {
            return;
        }

        Show();
    }

    private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
        UpdateTeamHeader(e.Frame);
    }

    private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
        if (e.Entity != owningPlayer) {
            return;
        }

        if (!Settings.Instance.generalScoreboardAlways) {
            Hide();
        }
    }

    private void OnPlayerAdded(EventPlayerAdded e) {
        UpdateSpectatorCount(e.Frame);
    }

    private void OnPlayerRemoved(EventPlayerRemoved e) {
        UpdateSpectatorCount(e.Frame);
    }
}
