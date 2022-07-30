using NSMB.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScoreboardUpdater : MonoBehaviour {

    public static ScoreboardUpdater instance;
    private static IComparer<ScoreboardEntry> entryComparer;

    [SerializeField] GameObject entryTemplate;

    private readonly List<ScoreboardEntry> entries = new();
    private bool manuallyToggled = false, autoToggled = false;
    private Animator animator;

    public void OnEnable() {
        InputSystem.controls.UI.Scoreboard.performed += OnToggle;
    }
    public void OnDisable() {
        InputSystem.controls.UI.Scoreboard.performed -= OnToggle;
    }

    private void OnToggle(InputAction.CallbackContext context) {
        ManualToggle();
    }

    public void SetEnabled() {
        manuallyToggled = true;
        animator.SetFloat("speed", 1);
        animator.Play("toggle", 0, 0.99f);
    }

    public void ManualToggle() {
        if (autoToggled && !manuallyToggled) {
            //exception, already open. close.
            manuallyToggled = false;
            autoToggled = false;
        } else {
            manuallyToggled = !manuallyToggled;
        }
        PlayAnimation(manuallyToggled);
    }

    private void PlayAnimation(bool enabled) {
        animator.SetFloat("speed", enabled ? 1 : -1);
        animator.Play("toggle", 0, Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime));
    }

    public void OnDeathToggle() {
        if (!manuallyToggled) {
            PlayAnimation(true);
            autoToggled = true;
        }
    }

    public void OnRespawnToggle() {
        if (!manuallyToggled) {
            PlayAnimation(false);
            autoToggled = false;
        }
    }

    public void Awake() {
        instance = this;
        animator = GetComponent<Animator>();
        if (entryComparer == null)
            entryComparer = new ScoreboardEntry.EntryComparer();
    }

    public void Reposition() {
        entries.Sort(entryComparer);
        entries.ForEach(se => se.transform.SetAsLastSibling());
    }

    public void Populate(IEnumerable<PlayerController> players) {
        foreach (PlayerController player in players) {
            if (!player)
                continue;

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