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
        }

        public void OnEnable() {
            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                OnRulesChanged(new EventRulesChanged {
                    Game = game,
                    Tick = game.Frames.Predicted.Number,
                });
            }
        }

        public void OnDisable() {
            Close(false);
        }

        public void SetEnabled(bool value) {
            button.interactable = value;
            Close(true);
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

        private unsafe void UpdateButtonInteractable(QuantumGame game) {
            Frame f = game.Frames.Predicted;

            if (f.Global->Rules.TeamsEnabled) {
                flag.sprite = enabledSprite;
                button.interactable = true;
            } else {
                flag.sprite = disabledSprite;
                button.interactable = false;
            }
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            UpdateButtonInteractable(e.Game);
        }
    }
}
