using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu {
    public class TeamChooser : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject blockerTemplate, content, disabledIcon, normalIcon;
        [SerializeField] private TeamButton[] buttons;
        [SerializeField] private Button button;
        [SerializeField] private Image flag;

        //---Private Variables
        private GameObject blockerInstance;

        public void OnEnable() {
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
        }

        public void OnDisable() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
        }

        public void SetEnabled(bool value) {
            button.interactable = value;
            normalIcon.SetActive(value);
            disabledIcon.SetActive(!value);

            if (value) {
                OnColorblindModeChanged();
            } else {
                Close(true);
            }
        }

        public void SelectTeam(TeamButton team) {
            int selected = team.index;

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

            foreach (TeamButton button in buttons) {
                button.OnDeselect(null);
            }

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
            QuantumGame game = NetworkHandler.Runner.Game;
            Frame f = game.Frames.Predicted;
            var playerData = QuantumUtils.GetPlayerData(f, game.GetLocalPlayers()[0]);

            TeamAsset[] teams = f.SimulationConfig.Teams;
            int selected = playerData->Team % teams.Length;
            flag.sprite = Settings.Instance.GraphicsColorblind ? teams[selected].spriteColorblind : teams[selected].spriteNormal;
        }
    }
}
