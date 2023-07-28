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

    public void OnEnable() {
        teamManager = GameManager.Instance.teamManager;
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
        foreach ((int index, int stars) in teamStars) {
            Team team = ScriptableManager.Instance.teams[index];
            newString += (Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal) + Utils.GetSymbolString(stars.ToString()) + " ";
        }

        text.text = newString.Trim();
    }
}
