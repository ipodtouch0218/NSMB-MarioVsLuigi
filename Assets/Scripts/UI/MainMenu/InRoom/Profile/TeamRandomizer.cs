using NSMB.Utilities;
using Quantum;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class TeamRandomizer : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private Button button;
        [SerializeField] private Image flag;
        [SerializeField] private Sprite enabledSprite, disabledSprite;
        [SerializeField] private GameObject defaultSelection;

        //---Private Variables
        private GameObject blockerInstance;

        public void Initialize() {
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
            QuantumEvent.Subscribe<EventHostChanged>(this, OnHostChanged);
        }

        public void OnEnable() {
            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                UpdateButtonState(game);
            }
        }

        public void OnDisable() {
            Close(false);
        }

        [Preserve]
        public unsafe void RandomizeTeam(int teamCount) {
            var game = QuantumRunner.DefaultGame;

            if (!QuantumViewUtils.TryGetHostPlayerSlot(game, out int slot)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                Close(false);
                canvas.EventSystem.SetSelectedGameObject(button.gameObject);
                return;
            }

            game.SendCommand(slot, new CommandRandomizeAllTeams {
                Teams = teamCount
            });

            canvas.PlayConfirmSound();
            Close(false);
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
        }

        [Preserve]
        public void UnlockTeams() {
            var game = QuantumRunner.DefaultGame;

            if (!QuantumViewUtils.TryGetHostPlayerSlot(game, out int slot)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                Close(false);
                canvas.EventSystem.SetSelectedGameObject(button.gameObject);
                return;
            }

            game.SendCommand(slot, new CommandRandomizeAllTeams {
                Clear = true
            });

            canvas.PlayConfirmSound();
            Close(false);
            canvas.EventSystem.SetSelectedGameObject(button.gameObject);
        }

        [Preserve]
        public unsafe void Open() {
            var game = QuantumRunner.DefaultGame;
            if (!QuantumViewUtils.TryGetHostPlayerSlot(game, out _)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                return;
            }

            blockerInstance = Instantiate(blockerTemplate, canvas.transform);
            blockerInstance.SetActive(true);
            content.SetActive(true);

            canvas.PlayCursorSound();
            canvas.EventSystem.SetSelectedGameObject(defaultSelection);
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

        private unsafe void UpdateButtonState(QuantumGame game) {
            Frame f = game.Frames.Predicted;

            if (QuantumViewUtils.TryGetHostPlayerSlot(game, out _)) {
                // We are host
                if (f.Global->Rules.TeamsEnabled) {
                    flag.sprite = enabledSprite;
                    button.interactable = true;
                } else {
                    flag.sprite = disabledSprite;
                    button.interactable = false;
                }
                button.gameObject.SetActive(true);
            } else {
                button.gameObject.SetActive(false);
            }
        }

        private void OnRulesChanged(EventRulesChanged e) {
            UpdateButtonState(e.Game);
        }

        private void OnHostChanged(EventHostChanged e) {
            UpdateButtonState(e.Game);
        }
    }
}
