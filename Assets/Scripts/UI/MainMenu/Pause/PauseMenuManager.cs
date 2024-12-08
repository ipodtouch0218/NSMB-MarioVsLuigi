using NSMB.Extensions;
using NSMB.Translation;
using NSMB.UI.Game;
using Quantum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NSMB.UI.Pause {

    public class PauseMenuManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private InputCollector inputCollector;
        [SerializeField] private GameObject main;

        [SerializeField] private PauseMenuOptionWrapper[] options;
        [SerializeField] private Material enabledMaterial, disabledMaterial;

        [SerializeField] private GameObject confirmationPrompt;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private TMP_Text yesConfirmText, noConfirmText;

        //---Private Variables
        private bool isPaused;

        private bool inputted;
        private int selected;
        private bool skipSound;
        private bool isHost;

        private bool isInConfirmation;
        private bool isInConfirmationYesSelected;
        private bool isInConfirmationForQuitting;
        private string originalNoText, originalYesText;

        private float unpauseTime;

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Start() {
            ControlSystem.controls.UI.Pause.performed += OnPause;
            if (NetworkHandler.IsReplay) {
                options[1].translationKey = "ui.pause.replay.hide";
            }
            UpdateLabels();
        }

        public void OnDestroy() {
            ControlSystem.controls.UI.Pause.performed -= OnPause;
        }

        private unsafe void OnPause(InputAction.CallbackContext context) {
            if (isPaused || unpauseTime == Time.unscaledTime) {
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            if (game != null && game.Frames.Predicted.Global->GameState != GameState.Playing && !game.Session.IsReplay) {
                return;
            }

            Pause(true);
        }

        public unsafe void Pause(bool playSound) {
            ControlSystem.controls.UI.Navigate.performed += OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled += OnNavigate;
            ControlSystem.controls.UI.Submit.performed += OnSubmit;
            ControlSystem.controls.UI.Cancel.performed += OnCancel;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);

            skipSound = true;

            QuantumGame game = QuantumRunner.DefaultGame;
            isHost = game == null || game.PlayerIsLocal(QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _));
            options[1].text.fontSharedMaterial = isHost || NetworkHandler.IsReplay ? enabledMaterial : disabledMaterial;
            SelectOption(0);

            isInConfirmation = false;
            confirmationPrompt.SetActive(false);

            main.SetActive(true);
            inputCollector.IsPaused = true;
            isPaused = true;

            if (playSound) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Pause);
            }
        }

        public void Unpause(bool playSound) {
            ControlSystem.controls.UI.Navigate.performed -= OnNavigate;
            ControlSystem.controls.UI.Navigate.canceled -= OnNavigate;
            ControlSystem.controls.UI.Submit.performed -= OnSubmit;
            ControlSystem.controls.UI.Cancel.performed -= OnCancel;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;

            main.SetActive(false);
            inputCollector.IsPaused = false;
            isPaused = false;

            if (playSound) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Pause);
            }
            unpauseTime = Time.unscaledTime;
        }

        public void OnNavigate(InputAction.CallbackContext context) {
            if (!isPaused) {
                return;
            }

            if (context.canceled) {
                inputted = false;
                return;
            }

            if (GlobalController.Instance.optionsManager.gameObject.activeSelf) {
                return;
            }

            Vector2 input = context.ReadValue<Vector2>();
            if (isInConfirmation) {
                if (input.x < 0.2f) {
                    SelectConfirmYes(true);
                } else if (input.x > 0.2f) {
                    SelectConfirmNo(true);
                }
                return;
            }

            if (input.y > 0 && !inputted) {
                IncrementOption(-1);
            } else if (input.y < 0 && !inputted) {
                IncrementOption(1);
            }
            inputted = true;
        }

        public void OnSubmit(InputAction.CallbackContext context) {
            if (!isPaused) {
                return;
            }

            if (GlobalController.Instance.optionsManager.gameObject.activeSelf) {
                return;
            }

            if (isInConfirmation) {
                if (isInConfirmationYesSelected) {
                    ClickConfirmYes();
                } else {
                    ClickConfirmNo();
                }
                return;
            }

            options[selected].trigger.OnPointerClick(null);
        }

        public void OnCancel(InputAction.CallbackContext context) {
            if (!isPaused) {
                return;
            }

            if (GlobalController.Instance.optionsManager.gameObject.activeSelf) {
                return;
            }

            if (isInConfirmation) {
                if (isInConfirmationYesSelected) {
                    SelectConfirmNo(true);
                } else {
                    ClickConfirmNo();
                }
            } else {
                if (selected == 0) {
                    Unpause(true);
                } else {
                    SelectOption(0);
                }
            }
        }

        public void SelectConfirmYes(bool sound) {
            if (sound && !isInConfirmationYesSelected) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
            }

            isInConfirmationYesSelected = true;
            yesConfirmText.text = "» " + originalYesText + " «";
            noConfirmText.text = originalNoText;
        }

        public void SelectConfirmNo(bool sound) {
            if (sound && isInConfirmationYesSelected) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
            }

            isInConfirmationYesSelected = false;
            yesConfirmText.text = originalYesText;
            noConfirmText.text = "» " + originalNoText + " «";
        }

        public unsafe void ClickConfirmYes() {
            if (isInConfirmationForQuitting) {
                QuantumRunner.Default.Shutdown();
            } else {
                QuantumGame game = QuantumRunner.DefaultGame;
                Frame f = game.Frames.Predicted;
                PlayerRef hostPlayer = QuantumUtils.GetHostPlayer(f, out _);

                int index = game.GetLocalPlayers().IndexOf(hostPlayer);
                if (index != -1) {
                    int slot = game.GetLocalPlayerSlots()[index];
                    game.SendCommand(slot, new CommandHostEndGame());
                }
            }
            Unpause(false);
        }

        public void ClickConfirmNo() {
            isInConfirmation = false;
            confirmationPrompt.SetActive(false);
            GlobalController.Instance.PlaySound(SoundEffect.UI_Back);
        }

        public void OpenSettings() {
            GlobalController.Instance.optionsManager.OpenMenu();
            GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
        }
        
        public void OpenConfirmationMenu(bool quit) {
            if (NetworkHandler.IsReplay && !quit) {
                // Toggle replay UI
                bool replayNowActive = playerElements.ReplayUi.ToggleReplayControls();
                options[1].translationKey = replayNowActive ? "ui.pause.replay.hide" : "ui.pause.replay.show";
                UpdateLabels();
                GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
                return;
            }
            if (!quit && !isHost) {
                return;
            }

            isInConfirmation = true;
            isInConfirmationForQuitting = quit;

            TranslationManager tm = GlobalController.Instance.translationManager;
            confirmationText.text = tm.GetTranslation("ui.generic.confirmation");

            confirmationPrompt.SetActive(true);
            GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
            originalYesText = yesConfirmText.text;
            originalNoText = noConfirmText.text;
            SelectConfirmNo(false);
        }

        public void IncrementOption(int increment) {
            int newIndex = selected + increment;

            if (newIndex == 1 && !isHost && !NetworkHandler.IsReplay) {
                newIndex += increment;
            }

            if (newIndex < 0 || newIndex >= options.Length) {
                return;
            }

            SelectOption(newIndex);
        }

        public void SelectOption(int index) {
            if (selected == index || selected < 0 || selected >= options.Length || (index == 1 && !isHost  && !NetworkHandler.IsReplay)) {
                skipSound = false;
                return;
            }

            selected = index;
            UpdateLabels();

            if (!skipSound) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
            }
            skipSound = false;
        }

        public void SelectOption(TMP_Text option) {
            skipSound = true;
            int index = -1;
            for (int i = 0; i < options.Length; i++) {
                if (options[i].text == option) {
                    index = i;
                    break;
                }
            }
            SelectOption(index);
        }

        public void UpdateLabels() {
            foreach (PauseMenuOptionWrapper option in options) {
                option.originalText = GlobalController.Instance.translationManager.GetTranslation(option.translationKey);
            }

            for (int i = 0; i < options.Length; i++) {
                PauseMenuOptionWrapper option = options[i];
                option.text.text = (selected == i) ? ("» " + option.originalText + " «") : option.originalText;
                //option.text.isRightToLeftText = GlobalController.Instance.translationManager.RightToLeft;
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            foreach (PauseMenuOptionWrapper option in options) {
                option.originalText = tm.GetTranslation(option.translationKey);
            }
            UpdateLabels();
        }

        [Serializable]
        public class PauseMenuOptionWrapper {
            public TMP_Text text;
            public EventTrigger trigger;
            public string translationKey;
            [NonSerialized] public string originalText;
        }
    }
}