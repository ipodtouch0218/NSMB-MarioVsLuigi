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

    //---Private Variables
    private readonly List<Replay> replays = new();

    public void OnEnable() {
        replayTemplate.gameObject.SetActive(false);
        FindReplays();
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
        public bool IsTemporary => FilePath.Contains("temp/");
        public bool IsFavorited => FilePath.Contains("favorites/");
    }
}