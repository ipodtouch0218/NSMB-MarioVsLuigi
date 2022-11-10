using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using NSMB.Extensions;

public class ScoreboardUpdater : MonoBehaviour {

    //---Static Variables
    public static ScoreboardUpdater Instance { get; private set; }
    private static IComparer<ScoreboardEntry> entryComparer;

    //---Serialized Variables
    [SerializeField] private GameObject entryTemplate, teamsHeader;

    //---Private Variables
    private readonly List<ScoreboardEntry> entries = new();
    private Animator animator;
    private bool manuallyToggled = false, autoToggled = false;

    public void OnEnable() {
        InputSystem.controls.UI.Scoreboard.performed += OnToggle;
    }

    public void OnDisable() {
        InputSystem.controls.UI.Scoreboard.performed -= OnToggle;
    }

    public void Awake() {
        Instance = this;
        animator = GetComponent<Animator>();
        entryComparer ??= new ScoreboardEntry.EntryComparer();

        teamsHeader.SetActive(LobbyData.Instance.Teams);
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

    public void Reposition() {
        entries.Sort(entryComparer);
        entries.ForEach(se => se.transform.SetAsLastSibling());
    }

    public void Populate(IEnumerable<PlayerController> players) {
        foreach (PlayerController player in players) {
            if (!player)
                continue;

            PlayerData data = player.Object.InputAuthority.GetPlayerData(player.Runner);

            GameObject entryObj = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entryObj.SetActive(true);
            entryObj.name = data.GetNickname();
            ScoreboardEntry entry = entryObj.GetComponent<ScoreboardEntry>();
            entry.target = player;

            entries.Add(entry);
        }

        Reposition();
    }
}
