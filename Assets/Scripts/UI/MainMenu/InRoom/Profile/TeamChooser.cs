using Quantum;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class TeamChooser : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private TeamButton[] buttons;
        [SerializeField] private Button button;
        [SerializeField] private Image flag;
        [SerializeField] private Sprite disabledSprite;

        //---Private Variables
        private GameObject blockerInstance;
        private int selected;

        public void Initialize() {
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public void OnEnable() {
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;

            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                OnRulesChanged(new EventRulesChanged {
                    Game = game,
                    Tick = game.Frames.Predicted.Number,
                });
            }
        }

        public void OnDisable() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
            Close(false);
        }

        public void SetEnabled(bool value) {
            button.interactable = value;

            if (value) {
                OnColorblindModeChanged();
            } else {
                Close(true);
            }
        }

        public void SelectTeam(TeamButton team) {
            selected = team.index;

            var game = QuantumRunner.DefaultGame;
            foreach (int slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Team,
                    Team = (byte) selected,
                });
            }

            Close(false);

            Frame f = game.Frames.Predicted;
            TeamAsset teamScriptable = f.FindAsset(game.Configurations.Simulation.Teams[selected]);
            flag.sprite = Settings.Instance.GraphicsColorblind ? teamScriptable.spriteColorblind : teamScriptable.spriteNormal;
            canvas.PlayConfirmSound();
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
        }

        public unsafe void Open() {
            var game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

            int selected = Mathf.Clamp(playerData->RequestedTeam, 0, f.SimulationConfig.Teams.Length);

            blockerInstance = Instantiate(blockerTemplate, canvas.transform);
            blockerInstance.SetActive(true);
            content.SetActive(true);

            canvas.PlayCursorSound();
            canvas.EventSystem.SetSelectedGameObject(buttons[selected].gameObject);
        }

        public void Close(bool playSound) {
            if (!blockerInstance) {
                return;
            }

            Destroy(blockerInstance);
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
            content.SetActive(false);

            if (playSound) {
                canvas.PlaySound(SoundEffect.UI_Back);
            }
        }

        private unsafe void OnColorblindModeChanged() {
            var game = QuantumRunner.DefaultGame;
            if (game == null) {
                return;
            }

            Frame f = game.Frames.Predicted;
            if (f.Global->Rules.TeamsEnabled) {
                TeamAsset team = f.FindAsset(f.SimulationConfig.Teams[selected]);
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            Frame f = e.Game.Frames.Predicted;
            if (f.Global->Rules.TeamsEnabled) {
                TeamAsset team = f.FindAsset(f.SimulationConfig.Teams[selected % f.SimulationConfig.Teams.Length]);
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
                button.interactable = true;
            } else {
                flag.sprite = disabledSprite;
                button.interactable = false;
            }
        }

        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (!e.Game.PlayerIsLocal(e.Player)) {
                return;
            }

            Frame f = e.Game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, e.Player);
            selected = playerData->RequestedTeam;

            if (f.Global->Rules.TeamsEnabled) {
                TeamAsset team = f.FindAsset(f.SimulationConfig.Teams[selected % f.SimulationConfig.Teams.Length]);
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
            }
        }
    }
}
