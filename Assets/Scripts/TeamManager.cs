using System.Collections.Generic;

using NSMB.Entities.Player;

public class TeamManager {

    //---Private Variables
    private readonly Dictionary<int, HashSet<PlayerController>> teams = new();

    public void AddPlayer(PlayerController player) {
        sbyte teamid = player.Data.Team;

        if (!teams.TryGetValue(teamid, out HashSet<PlayerController> team))
            teams[teamid] = team = new();

        team.Add(player);
    }

    public HashSet<PlayerController> GetTeamMembers(int team) {
        if (teams.TryGetValue(team, out HashSet<PlayerController> teamMembers))
            return teamMembers;

        return null;
    }

    public IEnumerable<int> GetValidTeams() {
        return teams.Keys;
    }

    public int GetTeamStars(int teamIndex) {
        if (!teams.TryGetValue(teamIndex, out HashSet<PlayerController> team))
            return 0;

        int stars = 0;
        bool hasAtLeastOnePlayer = false;
        foreach (PlayerController player in team) {
            if (!player || !player.Object || player.Lives == 0)
                continue;

            stars += player.Stars;
            hasAtLeastOnePlayer = true;
        }

        if (!hasAtLeastOnePlayer)
            return 0;

        return stars;
    }

    public int GetFirstPlaceStars() {
        int max = 0;
        foreach ((int index, var team) in teams) {
            int count = GetTeamStars(index);

            if (count > max)
                max = count;
        }
        return max;
    }

    public bool HasFirstPlaceTeam(out int teamIndex, out int stars) {
        stars = 0;
        teamIndex = -1;
        foreach ((int index, var team) in teams) {
            int count = GetTeamStars(index);

            if (count > stars) {
                stars = count;
                teamIndex = index;
            } else if (count == stars) {
                teamIndex = -1;
            }
        }
        return teamIndex != -1;
    }

    public int GetAliveTeamCount() {
        int count = 0;
        foreach ((int index, var team) in teams) {
            foreach (PlayerController player in team) {
                if (!player || player.Lives == 0)
                    continue;

                count++;
                break;
            }
        }
        return count;
    }
}
