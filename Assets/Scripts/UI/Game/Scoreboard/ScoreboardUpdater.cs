using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.UI.Game.Scoreboard {
    public class ScoreboardUpdater : MonoBehaviour {

        //---Properties
        public bool RequestSorting { get; set; }
        public EntityRef Target => playerElements.Entity;

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private ScoreboardEntry entryTemplate;
        [SerializeField] private GameObject teamHeader;
        [SerializeField] private LayoutElement repositioner;
        [SerializeField] private TMP_Text spectatorText, teamHeaderText;
        [SerializeField] private Animator animator;

        //---Private Variables
        private readonly List<ScoreboardEntry> entries = new();
        private bool isToggled;
        private StringBuilder stringBuilder = new();

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Initialize() {
            ShowWithoutAnimation();
        }

        public void OnEnable() {
            Settings.Controls.UI.Scoreboard.performed += OnToggleScoreboard;
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
            Settings.OnCondensedScoreboardChanged += OnCondensedScoreboardChanged;
            OnCondensedScoreboardChanged();
        }

        public void OnDisable() {
            Settings.Controls.UI.Scoreboard.performed -= OnToggleScoreboard;
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
            Settings.OnCondensedScoreboardChanged -= OnCondensedScoreboardChanged;
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
                foreach ((var marioEntity, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) { 
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

                int starDiff = gamemode.GetObjectiveCount(f, marioB) - gamemode.GetObjectiveCount(f, marioA);
                if (starDiff != 0) {
                    return starDiff;
                }

                if (f.Global->Rules.IsLivesEnabled && (marioA->Lives != marioB->Lives)) {
                    return marioB->Lives - marioA->Lives;
                }

            indexBasedSorting:
                return a.Index - b.Index;
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

            stringBuilder.Clear();

            var teams = f.Context.GetAllAssets<TeamAsset>();
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            Span<int> teamObjectiveCounts = stackalloc int[Constants.MaxPlayers];
            gamemode.GetAllTeamsObjectiveCounts(f, teamObjectiveCounts);

            int validTeams = QuantumUtils.GetValidTeams(f);
            for (int i = 0; i < teamObjectiveCounts.Length; i++) {
                if ((validTeams & (1 << i)) == 0) {
                    // Invalid team
                    continue;
                }

                int objectiveCount = Mathf.Max(0, teamObjectiveCounts[i]);
                
                TeamAsset team = teams[i];
                if (Settings.Instance.GeneralCondensedScoreboard) {
                    stringBuilder.Append("<color=#").Append(Utils.ColorToHex(team.color)).Append(">");
                    if (Settings.Instance.GraphicsColorblind) {
                        stringBuilder.Append(team.textSpriteColorblind);
                    }
                } else {
                    stringBuilder.Append(Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal).Append("<sprite name=hudnumber_x>");
                }
                stringBuilder.Append(Utils.GetSymbolString(objectiveCount.ToString()));
                if (Settings.Instance.GeneralCondensedScoreboard) {
                    stringBuilder.Append(" ");
                }
            }

            teamHeaderText.SetText(stringBuilder);
        }

        public unsafe void UpdateSpectatorCount(Frame f) {
            int spectators = 0;

            foreach ((_, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
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

        private void OnCondensedScoreboardChanged() {
            repositioner.preferredWidth = Settings.Instance.GeneralCondensedScoreboard ? 130 : 238;
            UpdateTeamHeader(QuantumRunner.DefaultGame.Frames.Predicted);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            Frame f = e.Game.Frames.Predicted;
            UpdateTeamHeader(f);
            UpdateSpectatorCount(f);
        }
    }
}
