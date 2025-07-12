using NSMB.Replay;
using NSMB.UI.MainMenu.Submenus.Replays;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.UI.Game.Results {
    public class ResultsMenu : QuantumSceneViewComponent {

        //---Properties
        private AudioSource sfx => GlobalController.Instance.sfx;

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private TMP_Text countdown;
        [SerializeField] private Image checkbox, checkmark;

        [SerializeField] private Color labelSelectedColor = Color.white, labelDeselectedColor = Color.gray, countdownCloseColor = Color.red;

        //---Properties
        private bool CanSaveReplay => !IsReplay && ActiveReplayManager.Instance.SavedRecordingPath != null;

        //---Private Variables
        private int cursor;
        private int previousCountdownTime;
        private bool inputted;
        private bool votedToContinue;
        private bool saveReplay;
        private bool exitPrompt;
        private Coroutine noReplaysCoroutine;

        public override void OnEnable() {
            base.OnEnable();
            cursor = 0;
            Settings.Controls.UI.Enable();
            Settings.Controls.UI.Navigate.performed += OnNavigate;
            Settings.Controls.UI.Navigate.canceled += OnNavigate;
            Settings.Controls.UI.Submit.performed += OnSubmit;
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            RefreshAll();

            countdown.gameObject.SetActive(!IsReplay);
            checkmark.gameObject.SetActive(!IsReplay);
            checkbox.gameObject.SetActive(!IsReplay);
        }

        public override void OnDisable() {
            base.OnDisable();
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
            Settings.Controls.UI.Submit.performed -= OnSubmit;
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;

            if (saveReplay) {
                string destination = ReplayListManager.ReplayDirectory;
                string path = ActiveReplayManager.Instance.SavedRecordingPath[destination.Length..];
                int nextSlash = path.IndexOf(Path.DirectorySeparatorChar, 1);
                if (nextSlash != -1) {
                    path = path[(nextSlash + 1)..];
                }
                destination = Path.Combine(destination, "saved", path);
                File.Move(ActiveReplayManager.Instance.SavedRecordingPath, destination);
                Debug.Log($"[Replay] Made temporary replay '{ActiveReplayManager.Instance.SavedRecordingPath}' permanent: '{destination}'");
            }
        }

        public override unsafe void OnUpdateView() {
            if (!IsReplay) {
                int currentCountdownTime = PredictedFrame.Global->GameStartFrames / PredictedFrame.UpdateRate;

                if (currentCountdownTime != previousCountdownTime) {
                    countdown.text = "<sprite name=room_timer>" + currentCountdownTime.ToString();
                    countdown.color = currentCountdownTime <= 3 ? countdownCloseColor : Color.white;
                    previousCountdownTime = currentCountdownTime;
                }
            }
        }

        private void OnNavigate(InputAction.CallbackContext context) {
            Vector2 input = context.ReadValue<Vector2>();

            if (GlobalController.Instance.optionsManager.isActiveAndEnabled || context.canceled || (previousCountdownTime == 0 && !IsReplay)) {
                inputted = false;
                return;
            }

            if (input.y > 0 && !inputted) {
                IncrementOption(-1);
            } else if (input.y < 0 && !inputted) {
                IncrementOption(1);
            }
            inputted = true;
        }

        private void OnSubmit(InputAction.CallbackContext context) {

            if (GlobalController.Instance.optionsManager.isActiveAndEnabled || context.canceled || (previousCountdownTime == 0 && !IsReplay)) {
                return;
            }

            Click(cursor);
        }

        public void IncrementOption(int adjustment) {
            int newCursor = cursor;

            do {
                newCursor += adjustment;
                if (newCursor < 0 || newCursor >= labels.Length) {
                    return;
                }
            } while (!labels[newCursor].isActiveAndEnabled);

            Deselect(cursor);
            Select(newCursor);
            cursor = newCursor;
            sfx.PlayOneShot(SoundEffect.UI_Cursor);
        }

        public void Deselect(int index) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            string text;
            switch (index) {
            case 0:
                if (IsReplay) {
                    text = tm.GetTranslation("ui.game.results.nextreplay");
                } else {
                    text = tm.GetTranslation(votedToContinue ? "ui.game.results.votedtocontinue" : "ui.pause.continue");
                }
                labels[0].text = text;
                break;
            case 1:
                if (IsReplay) {
                    text = tm.GetTranslation("ui.game.results.restartreplay");
                } else {
                    text = tm.GetTranslation(CanSaveReplay ? "ui.game.results.savereplay" : "ui.game.results.replayunavailable"); ;
                }
                labels[1].text = text;
                break;
            case 2:
                exitPrompt = false;
                labels[2].text = tm.GetTranslation("ui.game.results.quittomainmenu");
                break;
            }
            labels[index].color = labelDeselectedColor;
        }

        public void Select(int index) {
            Deselect(index);
            labels[index].text = "» " + labels[index].text;
            labels[index].color = labelSelectedColor;
        }

        public void Click(int index) {
            if (cursor != index) {
                Deselect(cursor);
                Select(index);
                cursor = index;
            }

            switch (index) {
            case 0:
                if (IsReplay) {
                    ReplayListManager replayManager = ReplayListManager.Instance;
                    int replayIndex = replayManager.Replays.IndexOf(rle => rle.ReplayFile == ActiveReplayManager.Instance.CurrentReplay);

                    BinaryReplayFile newReplay = replayManager.Replays[(replayIndex + 1) % replayManager.Replays.Count].ReplayFile;
                    if (replayIndex + 1 >= replayManager.Replays.Count || newReplay == ActiveReplayManager.Instance.CurrentReplay) {
                        labels[0].text = "» " + GlobalController.Instance.translationManager.GetTranslation("ui.game.results.nextreplay.nomore");
                        sfx.PlayOneShot(SoundEffect.UI_Error);
                        if (noReplaysCoroutine != null) {
                            StopCoroutine(noReplaysCoroutine);
                        }
                        noReplaysCoroutine = StartCoroutine(ResetTextAfterTime(0, 0.5f));
                    } else {
                        ActiveReplayManager.Instance.StartReplayPlayback(newReplay);
                    }
                } else {
                    // Vote to continue
                    if (!votedToContinue) {
                        foreach (int slot in Game.GetLocalPlayerSlots()) {
                            Game.SendCommand(slot, new CommandEndGameContinue());
                        }
                        sfx.PlayOneShot(SoundEffect.UI_Decide);
                        votedToContinue = true;
                        Select(0);
                    }
                }
                break;
            case 1:
                if (IsReplay) {
                    playerElements.ReplayUi.ResetReplay();
                    sfx.PlayOneShot(SoundEffect.UI_Decide);
                } else {
                    if (CanSaveReplay) {
                        saveReplay = !saveReplay;
                        checkmark.enabled = saveReplay;
                        sfx.PlayOneShot(SoundEffect.UI_Decide);
                    } else {
                        sfx.PlayOneShot(SoundEffect.UI_Error);
                    }
                }
                break;
            case 2:
                if (exitPrompt) {
                    QuantumRunner.Default.Shutdown();
                } else {
                    exitPrompt = true;
                    labels[2].text = "» " + GlobalController.Instance.translationManager.GetTranslation(exitPrompt ? "ui.generic.confirmation" : "ui.game.results.quittomainmenu");
                }
                sfx.PlayOneShot(SoundEffect.UI_Decide);
                break;
            }
        }

        IEnumerator ResetTextAfterTime(int index, float seconds) {
            yield return new WaitForSecondsRealtime(seconds);
            if (cursor == index) {
                Select(index);
            } else {
                Deselect(index);
            }
            noReplaysCoroutine = null;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            RefreshAll();
        }

        private void RefreshAll() {
            for (int i = 0; i < labels.Length; i++) {
                Deselect(i);
            }
            Select(cursor);
        }
    }
}