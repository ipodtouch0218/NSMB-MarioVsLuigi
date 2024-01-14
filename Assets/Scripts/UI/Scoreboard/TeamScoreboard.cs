using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TMPro;

using NSMB.Game;
using NSMB.Utils;

public class TeamScoreboard : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text text;

    //---Private Variables
    private readonly Dictionary<int, int> teamStars = new();
    private TeamManager teamManager;

    public void Update() {
        if (CheckForStarCountUpdates()) {
            UpdateText();
        }
    }

    private bool CheckForStarCountUpdates() {
        bool updated = false;
        // Ew... Linq, but needed to avoid concurrent modification
        foreach (int index in teamStars.Keys.ToArray()) {
            int stars = teamStars[index];
            teamManager.GetTeamStars(index, out int newStars);
            if (stars == newStars) {
                continue;
            }

            teamStars[index] = newStars;
            updated = true;
        }
        return updated;
    }

    private void UpdateText() {
        StringBuilder newString = new();
        foreach ((int index, int stars) in teamStars) {
            if (newString.Length != 0) {
                newString.Append(" ");
            }
            Team team = ScriptableManager.Instance.teams[index];
            newString.Append(Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal);
            newString.Append(Utils.GetSymbolString(stars.ToString()));
        }

        text.text = newString.ToString();
    }

    public void OnAllPlayersLoaded() {
        teamManager = GameManager.Instance.teamManager;
        foreach (int teamIndex in teamManager.GetValidTeams()) {
            teamStars[teamIndex] = 0;
        }

        UpdateText();
    }
}
