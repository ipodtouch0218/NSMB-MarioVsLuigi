using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu {
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

            if (NetworkHandler.Game != null) {
                OnRulesChanged(new EventRulesChanged {
                    Game = NetworkHandler.Game,
                    Frame = NetworkHandler.Game.Frames.Predicted,
                    LevelChanged = false,
                    Tick = NetworkHandler.Game.Frames.Predicted.Number,
                });
            }
        }

        public void OnDisable() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
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

            QuantumGame game = NetworkHandler.Runner.Game;
            foreach (int slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Team,
                    Team = (byte) selected,
                });
            }

            Close(false);

            TeamAsset teamScriptable = game.Configurations.Simulation.Teams[selected];
            flag.sprite = Settings.Instance.GraphicsColorblind ? teamScriptable.spriteColorblind : teamScriptable.spriteNormal;
            canvas.PlayConfirmSound();
        }

        public unsafe void Open() {
            QuantumGame game = NetworkHandler.Runner.Game;
            Frame f = game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

            TeamAsset[] teams = f.SimulationConfig.Teams;
            int selected = Mathf.Clamp(playerData->Team, 0, teams.Length);
            blockerInstance = Instantiate(blockerTemplate, canvas.transform);
            blockerInstance.SetActive(true);
            content.SetActive(true);

            EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);
            canvas.PlayCursorSound();
        }

        public void Close(bool playSound) {
            if (!blockerInstance) {
                return;
            }

            Destroy(blockerInstance);
            EventSystem.current.SetSelectedGameObject(gameObject);
            content.SetActive(false);

            if (playSound) {
                canvas.PlaySound(SoundEffect.UI_Back);
            }
        }

        private unsafe void OnColorblindModeChanged() {
            QuantumGame game = NetworkHandler.Game;
            if (game == null) {
                return;
            }

            Frame f = game.Frames.Predicted;
            if (f.Global->Rules.TeamsEnabled) {
                TeamAsset[] teams = f.SimulationConfig.Teams;
                flag.sprite = Settings.Instance.GraphicsColorblind ? teams[selected].spriteColorblind : teams[selected].spriteNormal;
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            Frame f = e.Frame;
            if (f.Global->Rules.TeamsEnabled) {
                TeamAsset team = f.SimulationConfig.Teams[selected % f.SimulationConfig.Teams.Length];
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

            Frame f = e.Frame;
            var playerData = QuantumUtils.GetPlayerData(f, e.Player);
            selected = playerData->Team;

            if (f.Global->Rules.TeamsEnabled) {
                TeamAsset team = f.SimulationConfig.Teams[selected % f.SimulationConfig.Teams.Length];
                flag.sprite = Settings.Instance.GraphicsColorblind ? team.spriteColorblind : team.spriteNormal;
            }
        }
    }
}
