using System.Collections.Generic;

public class TeamManager {

    private readonly Dictionary<int, HashSet<PlayerController>> teams = new();

    public void AddPlayer(PlayerController player) {
        sbyte teamid = player.data.Team;

        if (!teams.TryGetValue(teamid, out HashSet<PlayerController> team))
            teams[teamid] = team = new();

        team.Add(player);
    }

    public int GetTeamStars(int teamIndex) {
        if (!teams.TryGetValue(teamIndex, out HashSet<PlayerController> team))
            return 0;

        int stars = 0;
        foreach (PlayerController player in team) {
            if (!player || player.Lives == 0)
                continue;

            stars += player.Stars;
        }

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

    public bool HasFirstPlaceTeam(out int teamIndex) {
        int max = 0;
        teamIndex = -1;
        foreach ((int index, var team) in teams) {
            int count = GetTeamStars(index);

            if (count > max) {
                max = count;
                teamIndex = index;
            } else if (count == max) {
                teamIndex = -1;
            }
        }
        return teamIndex != -1;
    }
}
