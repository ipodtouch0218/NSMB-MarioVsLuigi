using System.Collections.Generic;
using UnityEngine;
using TMPro;

using NSMB.Utils;
using System.Linq;

public class TeamScoreboard : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text text;

    //---Private Variables
    private readonly Dictionary<int, int> teamStars = new();
    private TeamManager teamManager;

    public void OnEnable() {
        TeamManager.OnTeamsFinalized += OnTeamsFinalized;
    }

    public void OnDisable() {
        TeamManager.OnTeamsFinalized -= OnTeamsFinalized;
    }

    private void OnTeamsFinalized(TeamManager manager) {
        teamManager = manager;
        foreach (int teamIndex in teamManager.GetValidTeams())
            teamStars[teamIndex] = 0;

        UpdateText();
    }

    public void Update() {
        if (CheckForStarCountUpdates())
            UpdateText();
    }

    public bool CheckForStarCountUpdates() {
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

    public void UpdateText() {
        string newString = "";
        foreach ((int index, int stars) in teamStars)
            newString += Utils.teamSprites[index] + Utils.GetSymbolString(stars.ToString()) + " ";

        text.text = newString.Trim();
    }
}
