using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Game.Scoreboard {
    public class ScoreboardUpdater : MonoBehaviour {

        //---Properties
        public bool RequestSorting { get; set; }
        public EntityRef Target => playerElements.Entity;

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private ScoreboardEntry entryTemplate;
        [SerializeField] private GameObject teamHeader;
        [SerializeField] private TMP_Text spectatorText, teamHeaderText;
        [SerializeField] private Animator animator;
        [SerializeField] private InputActionReference toggleScoreboardAction;

        //---Private Variables
        private readonly List<ScoreboardEntry> entries = new();
        private bool isToggled;

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Initialize() {
            ShowWithoutAnimation();
        }

        public void OnEnable() {
            toggleScoreboardAction.action.performed += OnToggleScoreboard;
            toggleScoreboardAction.action.actionMap.Enable();
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
        }

        public void OnDisable() {
            toggleScoreboardAction.action.performed -= OnToggleScoreboard;
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
        }

        public unsafe void Start() {
            // Populate the scoreboard if we're a late joiner
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null) {
                PopulateScoreboard(game.Frames.Predicted);
            }

            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerObjectiveCoinsChanged>(this, OnMarioPlayerObjectiveCoinsChanged);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStarCoin>(this, OnMarioPlayerCollectedStarCoin);
            QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
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

            for (int i = 0; i < f.Global->RealPlayers; i++) {
                ref PlayerInformation info = ref f.Global->PlayerInfo[i];

                EntityRef entity = default;
                var filter = f.Filter<MarioPlayer>();
                while (filter.NextUnsafe(out EntityRef marioEntity, out MarioPlayer* mario)) {
                    if (mario->PlayerRef == info.PlayerRef) {
                        entity = marioEntity;
                        break;
                    }
                }

                ScoreboardEntry newEntry = Instantiate(entryTemplate, entryTemplate.transform.parent);
                newEntry.Initialize(f, i, entity, this);
                entries.Add(newEntry);
            }

            SortScoreboard(f);
        }

        public unsafe void SortScoreboard(Frame f) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            entries.Sort((a, b) => {
                f.Unsafe.TryGetPointer(a.Target, out MarioPlayer* marioA);
                f.Unsafe.TryGetPointer(b.Target, out MarioPlayer* marioB);

                if (marioA != null && marioB == null) {
                    return -1;
                } else if (marioA == null && marioB != null) {
                    return 1;
                } else if (marioA == null && marioB == null) {
                    goto indexBasedSorting;
                }

                if (marioA->Disconnected ^ marioB->Disconnected) {
                    if (marioA->Disconnected) {
                        return 1;
                    } else {
                        return -1;
                    }
                }

                if (f.Global->Rules.IsLivesEnabled && ((marioA->Lives == 0) ^ (marioB->Lives == 0))) {
                    return marioB->Lives - marioA->Lives;
                }

                int starDiff = gamemode.GetObjectiveCount(f, marioB) - gamemode.GetObjectiveCount(f, marioA);
                if (starDiff != 0) {
                    return starDiff;
                }

            indexBasedSorting:
                int indexA = int.MaxValue;
                int indexB = int.MaxValue;
                for (int i = 0; i < f.Global->RealPlayers; i++) {
                    PlayerRef player = f.Global->PlayerInfo[i].PlayerRef;
                    if (player == marioA->PlayerRef) {
                        indexA = i;
                    } else if (player == marioB->PlayerRef) {
                        indexB = i;
                    }
                }

                return indexA - indexB;
            });

            foreach (var entry in entries) {
                entry.transform.SetAsLastSibling();
            }
            spectatorText.transform.SetAsLastSibling();
        }

        public unsafe void UpdateTeamHeader(Frame f) {
            bool teamsEnabled = f.Global->Rules.TeamsEnabled;
            teamHeader.SetActive(teamsEnabled);

            if (!teamsEnabled) {
                return;
            }

            AssetRef<TeamAsset>[] teamAssets = f.SimulationConfig.Teams;
            StringBuilder result = new();

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            Span<int> teamObjectiveCounts = stackalloc int[10];
            gamemode.GetAllTeamsObjectiveCounts(f, teamObjectiveCounts);
            int aliveTeams = QuantumUtils.GetValidTeams(f);
            for (int i = 0; i < 10; i++) {
                if ((aliveTeams & (1 << i)) == 0) {
                    // Invalid team
                    continue;
                }

                int objectiveCount = teamObjectiveCounts[i];
                if (objectiveCount < 0) {
                    objectiveCount = 0;
                }
                TeamAsset team = f.FindAsset(teamAssets[i]);
                result.Append(Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal);
                result.Append(Utils.GetSymbolString("x" + objectiveCount));
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

        public EntityRef EntityAtPosition(int index) {
            if (index < 0 || index >= entries.Count) {
                return EntityRef.None;
            }

            return entries[index].Target;
        }

        private void OnToggleScoreboard(InputAction.CallbackContext context) {
            if (context.canceled) {
                return;
            }

            Toggle();
        }

        private void OnMarioPlayerDroppedStar(EventMarioPlayerDroppedStar e) {
            UpdateTeamHeader(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != Target) {
                return;
            }

            Show();
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            UpdateTeamHeader(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerCollectedStarCoin(EventMarioPlayerCollectedStarCoin e) {
            UpdateTeamHeader(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerObjectiveCoinsChanged(EventMarioPlayerObjectiveCoinsChanged e) {
            UpdateTeamHeader(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
            if (e.Entity != Target) {
                return;
            }

            if (!Settings.Instance.generalScoreboardAlways) {
                Hide();
            }
        }

        private void OnPlayerAdded(EventPlayerAdded e) {
            UpdateSpectatorCount(e.Game.Frames.Verified);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            UpdateSpectatorCount(e.Game.Frames.Verified);
        }

        private void OnColorblindModeChanged() {
            UpdateTeamHeader(QuantumRunner.DefaultGame.Frames.Predicted);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            Frame f = e.Game.Frames.Predicted;
            UpdateTeamHeader(f);
            UpdateSpectatorCount(f);
        }
    }
}
