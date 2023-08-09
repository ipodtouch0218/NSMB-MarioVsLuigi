using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

using NSMB.Utils;
using NSMB.Game;

public class TeamScoreboard : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text text;

    //---Private Variables
    private readonly Dictionary<int, int> teamStars = new();
    private TeamManager teamManager;

    public void Update() {
        if (CheckForStarCountUpdates())
            UpdateText();
    }

    private bool CheckForStarCountUpdates() {
        bool updated = false;
        // Ew... Linq, but needed to avoid concurrent modification
        foreach (int index in teamStars.Keys.ToArray()) {
            int stars = teamStars[index];
            int newStars = teamManager.GetTeamStars(index);
            if (stars == newStars)
                continue;

            teamStars[index] = newStars;
            updated = true;
        }
        return updated;
    }

    private void UpdateText() {
        string newString = "";
        foreach ((int index, int stars) in teamStars) {
            Team team = ScriptableManager.Instance.teams[index];
            newString += (Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal) + Utils.GetSymbolString(stars.ToString()) + " ";
        }

        text.text = newString.Trim();
    }

    public void OnAllPlayersLoaded() {
        teamManager = GameManager.Instance.teamManager;
        foreach (int teamIndex in teamManager.GetValidTeams())
            teamStars[teamIndex] = 0;

        UpdateText();
    }
}
