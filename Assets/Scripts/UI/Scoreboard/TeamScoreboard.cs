using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

using NSMB.Utils;

public class TeamScoreboard : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text text;

    //---Private Variables
    private readonly Dictionary<int, int> teamStars = new();
    private TeamManager teamManager;

    public void OnTeamsFinalized(TeamManager manager) {
        if (!SessionData.Instance.Teams) {
            enabled = false;
            return;
        }

        if (teamManager != null)
            return;

        teamManager = manager;
        foreach (int teamIndex in teamManager.GetValidTeams())
            teamStars[teamIndex] = 0;

        UpdateText();
    }

    public void Update() {
        if (CheckForStarCountUpdates())
            UpdateText();
    }

    private bool CheckForStarCountUpdates() {
        bool updated = false;
        foreach (int index in teamStars.Keys.ToList()) {
            int currStars = teamStars[index];
            int newStars = teamManager.GetTeamStars(index);
            if (currStars == newStars)
                continue;

            teamStars[index] = newStars;
            updated = true;
        }
        return updated;
    }

    private void UpdateText() {
        string newString = "";
        foreach ((int index, int stars) in teamStars)
            newString += ScriptableManager.Instance.teams[index].textSprite + Utils.GetSymbolString(stars.ToString()) + " ";

        text.text = newString.Trim();
    }
}
