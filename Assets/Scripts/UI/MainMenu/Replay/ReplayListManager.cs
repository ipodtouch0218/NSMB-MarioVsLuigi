using NSMB.Extensions;
using NSMB.UI.MainMenu;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReplayListManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ReplayListEntry replayTemplate;
    [SerializeField] private TMP_Text noReplaysText;
    [SerializeField] internal VerticalLayoutGroup layout;
    [SerializeField] private GameObject renamePrompt, renameDefaultSelected, deletePrompt;
    [SerializeField] private TMP_InputField renameText;
    [SerializeField] private TMP_Text renamePlaceholder, deleteText;

    //---Private Variables
    private readonly List<Replay> replays = new();
    private Replay target;

    public void OnEnable() {
        replayTemplate.gameObject.SetActive(false);
        FindReplays();
    }

    public void StartRename(Replay replay) {
        target = replay;
        renameText.text = replay.ReplayFile.GetDisplayName(false);
        renamePlaceholder.text = replay.ReplayFile.GetDefaultName();
        renamePrompt.SetActive(true);
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
    }

    public void Selected(ReplayListEntry entry) {
        foreach (var replay in replays) {
            if (replay.ListEntry != entry) {
                replay.ListEntry.HideButtons();
            }
        }
    }

    public void CancelRename() {
        renamePrompt.SetActive(false);
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Back);
    }
    
    public void ConfirmRename() {
        if (string.IsNullOrWhiteSpace(renameText.text)) {
            renameText.text = null;
        }

        if (renameText.text != target.ReplayFile.GetDisplayName()) {
            // Confirm + no change = no change. Do this because translations matter.
            target.ReplayFile.CustomName = renameText.text;

            using FileStream file = new FileStream(target.FilePath, FileMode.OpenOrCreate);
            target.ReplayFile.WriteToStream(file);
            target.ListEntry.UpdateText();
        }

        renamePrompt.SetActive(false);
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
    }

    public void StartDeletion(Replay replay) {
        target = replay;
        deletePrompt.SetActive(true);
        deleteText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.extras.replays.delete.text", "replayname", replay.ReplayFile.GetDisplayName());
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
    }

    public void CancelDeletion() {
        deletePrompt.SetActive(false);
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Back);
    }

    public void ConfirmDeletion() {
        File.Delete(target.FilePath);
        Destroy(target.ListEntry.gameObject);
        replays.Remove(target);
        target = null;
        deletePrompt.SetActive(false);
        MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);

        if (replays.Count == 0) {
            noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation("ui.extras.replays.none");
        } else {
            noReplaysText.text = "";
        }
    }

    public void FindReplays() {
        foreach (var replay in replays) {
            Destroy(replay.ListEntry.gameObject);
        }
        replays.Clear();

        Directory.CreateDirectory(Path.Combine(Application.streamingAssetsPath, "replays", "temp"));
        Directory.CreateDirectory(Path.Combine(Application.streamingAssetsPath, "replays", "favorite"));
        Directory.CreateDirectory(Path.Combine(Application.streamingAssetsPath, "replays", "saved"));

        string[] files = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "replays"), "*.mvlreplay", SearchOption.AllDirectories);

        foreach (var file in files) {
            if (File.Exists(file)) {
                using FileStream inputStream = new FileStream(file, FileMode.Open);
                if (BinaryReplayFile.TryLoadFromFile(inputStream, out BinaryReplayFile replayFile)) {
                    Replay newReplay = new Replay {
                        FilePath = file,
                        ReplayFile = replayFile,
                        ListEntry = Instantiate(replayTemplate, replayTemplate.transform.parent)
                    };
                    newReplay.ListEntry.Initialize(this, newReplay);
                    replays.Add(newReplay);
                }
            }
        }

        if (replays.Count == 0) {
            noReplaysText.text = GlobalController.Instance.translationManager.GetTranslation("ui.extras.replays.none");
        } else {
            noReplaysText.text = "";
        }
    }

    public class Replay {
        public string FilePath;
        public BinaryReplayFile ReplayFile;
        public ReplayListEntry ListEntry;
        public bool IsTemporary => FilePath.StartsWith(Path.Combine(Application.streamingAssetsPath, "replays", "temp"));
        public bool IsFavorited => FilePath.StartsWith(Path.Combine(Application.streamingAssetsPath, "replays", "favorite"));
    }
}