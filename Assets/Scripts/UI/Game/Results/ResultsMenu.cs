using NSMB.Extensions;
using NSMB.Translation;
using Quantum;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ResultsMenu : QuantumSceneViewComponent {

    //---Serialized Variables
    [SerializeField] private TMP_Text[] labels;
    [SerializeField] private TMP_Text countdown;
    [SerializeField] private Image checkbox, checkmark;
    [SerializeField] private AudioSource sfx;

    [SerializeField] private Color labelSelectedColor = Color.white, labelDeselectedColor = Color.gray, countdownCloseColor = Color.red;

    //---Properties
    private bool CanSaveReplay => !NetworkHandler.IsReplay && NetworkHandler.SavedRecordingPath != null;

    //---Private Variables
    private int cursor;
    private int previousCountdownTime;
    private bool inputted;
    private bool votedToContinue;
    private bool saveReplay;
    private bool exitPrompt;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
    }

    public override void OnEnable() {
        base.OnEnable();
        cursor = 0;
        Settings.Controls.UI.Navigate.performed += OnNavigate;
        Settings.Controls.UI.Navigate.canceled += OnNavigate;
        Settings.Controls.UI.Submit.performed += OnSubmit;
        TranslationManager.OnLanguageChanged += OnLanguageChanged;
        RefreshAll();
    }

    public override void OnDisable() {
        base.OnDisable();
        Settings.Controls.UI.Navigate.performed -= OnNavigate;
        Settings.Controls.UI.Navigate.canceled -= OnNavigate;
        Settings.Controls.UI.Submit.performed -= OnSubmit;
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;

        if (saveReplay) {
            string destination = ReplayListManager.ReplayDirectory;
            string path = NetworkHandler.SavedRecordingPath[destination.Length..];
            int nextSlash = path.IndexOf(Path.DirectorySeparatorChar, 1);
            if (nextSlash != -1) {
                path = path[(nextSlash + 1)..];
            }
            destination = Path.Combine(destination, "saved", path);
            File.Move(NetworkHandler.SavedRecordingPath, destination);
            Debug.Log($"[Replay] Made temporary replay '{NetworkHandler.SavedRecordingPath}' permanent: '{destination}'");
        }
    }

    public override unsafe void OnUpdateView() {
        int currentCountdownTime = PredictedFrame.Global->GameStartFrames / PredictedFrame.UpdateRate;

        if (currentCountdownTime != previousCountdownTime) {
            countdown.text = "<sprite name=room_timer>" + currentCountdownTime.ToString();
            countdown.color = currentCountdownTime <= 3 ? countdownCloseColor : Color.white; 
            previousCountdownTime = currentCountdownTime;
        }
    }
    
    private void OnNavigate(InputAction.CallbackContext context) {
        Vector2 input = context.ReadValue<Vector2>();

        if (GlobalController.Instance.optionsManager.isActiveAndEnabled || context.canceled || previousCountdownTime == 0) {
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
        if (GlobalController.Instance.optionsManager.isActiveAndEnabled || context.canceled || previousCountdownTime == 0) {
            return;
        }

        Click(cursor);
    }

    public void IncrementOption(int adjustment) {
        int newCursor = cursor + adjustment;
        if (newCursor < 0 || newCursor > labels.Length - 1) {
            return;
        }

        Deselect(cursor);
        Select(newCursor);
        cursor = newCursor;
        sfx.PlayOneShot(SoundEffect.UI_Cursor);
    }

    public void Deselect(int index) {
        TranslationManager tm = GlobalController.Instance.translationManager;
        switch (index) {
        case 0:
            labels[0].text = tm.GetTranslation(votedToContinue ? "ui.game.results.votedtocontinue" : "ui.pause.continue");
            break;
        case 1:
            labels[1].text = tm.GetTranslation(CanSaveReplay ? "ui.game.results.savereplay" : "ui.game.results.replayunavailable");
            break;
        case 2:
            exitPrompt = false;
            labels[2].text = tm.GetTranslation("ui.pause.quit");
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
        Deselect(cursor);
        Select(index);
        cursor = index;

        switch (index) {
        case 0:
            if (!votedToContinue) {
                foreach (int slot in Game.GetLocalPlayerSlots()) {
                    Game.SendCommand(slot, new CommandEndGameContinue());
                }
                sfx.PlayOneShot(SoundEffect.UI_Decide);
                votedToContinue = true;
                Select(0);
            }
            break;
        case 1:
            if (CanSaveReplay) {
                saveReplay = !saveReplay;
                checkmark.enabled = saveReplay;
                sfx.PlayOneShot(SoundEffect.UI_Decide);
            } else {
                sfx.PlayOneShot(SoundEffect.UI_Error);
            }
            break;
        case 2:
            if (exitPrompt) {
                NetworkHandler.Runner.Shutdown();
            } else {
                exitPrompt = true;
                labels[2].text = "» " + GlobalController.Instance.translationManager.GetTranslation(exitPrompt ? "ui.generic.confirmation" : "ui.pause.quit");
            }
            sfx.PlayOneShot(SoundEffect.UI_Decide);
            break;
        }
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