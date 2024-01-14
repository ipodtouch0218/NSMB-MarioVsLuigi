using System.Collections.Generic;

using NSMB.Entities.Player;

public class TeamManager {

    //---Private Variables
    private readonly Dictionary<int, HashSet<PlayerController>> teams = new();

    public void AddPlayer(PlayerController player) {
        sbyte teamid = player.Data.Team;

        if (!teams.TryGetValue(teamid, out HashSet<PlayerController> team)) {
            teams[teamid] = team = new();
        }

        team.Add(player);
    }

    public HashSet<PlayerController> GetTeamMembers(int team) {
        if (teams.TryGetValue(team, out HashSet<PlayerController> teamMembers)) {
            return teamMembers;
        }

        return null;
    }

    public IEnumerable<int> GetValidTeams() {
        return teams.Keys;
    }

    public bool GetTeamStars(int teamIndex, out int stars) {
        stars = 0;
        if (!teams.TryGetValue(teamIndex, out HashSet<PlayerController> team)) {
            return false;
        }

        bool hasAtLeastOnePlayer = false;
        foreach (PlayerController player in team) {
            if (!player || !player.Object || player.OutOfLives) {
                continue;
            }

            stars += player.Stars;
            hasAtLeastOnePlayer = true;
        }

        if (!hasAtLeastOnePlayer) {
            return false;
        }

        return true;
    }

    public int GetFirstPlaceStars() {
        int max = 0;
        foreach ((int index, var team) in teams) {
            if (GetTeamStars(index, out int stars)) {
                if (stars > max) {
                    max = stars;
                }
            }
        }
        return max;
    }

    public bool HasFirstPlaceTeam(out int teamIndex, out int maxStars) {
        maxStars = -1;
        teamIndex = -1;
        foreach ((int index, var team) in teams) {
            if (GetTeamStars(index, out int stars)) {
                if (stars > maxStars) {
                    maxStars = stars;
                    teamIndex = index;
                } else if (stars == maxStars) {
                    teamIndex = -1;
                }
            }
        }
        return teamIndex != -1;
    }

    public int GetAliveTeamCount() {
        int count = 0;
        foreach ((int index, var team) in teams) {
            foreach (PlayerController player in team) {
                if (!player || player.OutOfLives) {
                    continue;
                }

                count++;
                break;
            }
        }
        return count;
    }
}
