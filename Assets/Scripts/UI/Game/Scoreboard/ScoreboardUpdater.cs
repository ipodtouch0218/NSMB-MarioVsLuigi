using NSMB.Extensions;
using NSMB.Utils;
using Quantum;
using System.Collections.Generic;
using System.Linq;
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
        private bool isToggled;
        private List<ScoreboardEntry> entries = new();

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Initialize() {
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
            if (game != null) {
                PopulateScoreboard(game.Frames.Predicted);
            }

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
            playerFilter.UseCulling = false;
            while (playerFilter.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                ScoreboardEntry newEntry = Instantiate(entryTemplate, entryTemplate.transform.parent);
                newEntry.Initialize(f, entity, this);
                entries.Add(newEntry);
            }

            SortScoreboard(f);
        }

        public unsafe void SortScoreboard(Frame f) {
            entries.Sort((se1, se2) => {
                if (f.Exists(se1.Target) && !f.Exists(se2.Target)) {
                    return -1;
                } else if (f.Exists(se2.Target) && !f.Exists(se1.Target)) {
                    return 1;
                } else if (!f.Exists(se1.Target) && !f.Exists(se2.Target)) {
                    return 0;
                }

                var mario1 = f.Unsafe.GetPointer<MarioPlayer>(se1.Target);
                var mario2 = f.Unsafe.GetPointer<MarioPlayer>(se2.Target);

                if (f.Global->Rules.IsLivesEnabled && (mario1->Lives == 0 ^ mario2->Lives == 0)) {
                    return mario2->Lives - mario1->Lives;
                }

                int starDiff = mario2->Stars - mario1->Stars;
                if (starDiff != 0) {
                    return starDiff;
                }

                return mario2->PlayerRef - mario1->PlayerRef;
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

            TeamAsset[] teamAssets = f.SimulationConfig.Teams;
            StringBuilder result = new();

            Dictionary<int, int> teams = QuantumUtils.GetTeamStars(f);
            var orderedTeams = teams.OrderBy(kvp => kvp.Key);
            foreach ((int key, int stars) in orderedTeams) {
                if (key < 0 || key >= teamAssets.Length) {
                    continue;
                }

                result.Append(Settings.Instance.GraphicsColorblind ? teamAssets[key].textSpriteColorblind : teamAssets[key].textSpriteNormal);
                result.Append(Utils.Utils.GetSymbolString("x" + stars));
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
                spectatorText.text = "<sprite name=room_spectator>" + Utils.Utils.GetSymbolString("x" + spectators.ToString());
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
            UpdateTeamHeader(e.Frame);
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != Target) {
                return;
            }

            Show();
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            UpdateTeamHeader(e.Frame);
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
            UpdateSpectatorCount(e.Frame);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            UpdateSpectatorCount(e.Frame);
        }
    }
}
