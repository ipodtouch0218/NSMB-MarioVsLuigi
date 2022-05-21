using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ScoreboardUpdater : MonoBehaviour {

    public static ScoreboardUpdater instance;
    private static IComparer<ScoreboardEntry> entryComparer;

    [SerializeField] GameObject entryTemplate;
    private readonly List<ScoreboardEntry> entries = new();

    public void Awake() {
        instance = this;
        if (entryComparer == null)
            entryComparer = new ScoreboardEntry.EntryComparer();
    }

    public void Reposition() {
        entries.Sort(entryComparer);
        entries.ForEach(se => se.transform.SetAsLastSibling());
    }

    public void Populate(PlayerController[] players) {
        foreach (PlayerController player in players) {
            GameObject entryObj = Instantiate(entryTemplate, transform);
            entryObj.SetActive(true);
            entryObj.name = player.photonView.Owner.NickName;
            ScoreboardEntry entry = entryObj.GetComponent<ScoreboardEntry>();
            entry.target = player;

            entries.Add(entry);
        }

        Reposition();
    }
}