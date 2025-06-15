using System;

namespace Quantum {
    public abstract unsafe class GamemodeAsset : AssetObject {

        public string ObjectiveSymbolPrefix;

        public abstract void EnableGamemode(Frame f);

        public abstract void DisableGamemode(Frame f);

        public abstract void CheckForGameEnd(Frame f);

        public abstract int GetObjectiveCount(Frame f, PlayerRef player);

        public abstract int GetObjectiveCount(Frame f, MarioPlayer* mario);

        public abstract PowerupAsset GetRandomItem(Frame f, MarioPlayer* mario);

        public virtual int? GetWinningTeam(Frame f, out int winningObjectiveCount) {
            winningObjectiveCount = 0;
            int? winningTeam = null;
            bool tie = false;

            Span<int> teamObjectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectiveCounts);

            for (int i = 0; i < Constants.MaxPlayers; i++) {
                int objectiveCount = teamObjectiveCounts[i];
                if (objectiveCount < 0) {
                    continue;
                } else if (winningTeam == null) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount > winningObjectiveCount) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount == winningObjectiveCount) {
                    tie = true;
                }
            }

            return tie ? null : winningTeam;
        }

        public virtual void GetAllTeamsObjectiveCounts(Frame f, Span<int> teamObjectiveCounts) {
            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;

            for (int i = 0; i < teamObjectiveCounts.Length; i++) {
                teamObjectiveCounts[i] = -1;
            }

            while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (mario->Disconnected || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)) {
                    continue;
                }
                if (mario->GetTeam(f) is not byte team) {
                    continue;
                }

                if (teamObjectiveCounts[team] == -1) {
                    teamObjectiveCounts[team] = 0;
                }

                if (team < teamObjectiveCounts.Length) {
                    teamObjectiveCounts[team] += GetObjectiveCount(f, mario);
                }
            }
        }

        public virtual int? GetTeamObjectiveCount(Frame f, byte? nullableTeam) {
            if (nullableTeam is not byte team) {
                return null;
            }
            return GetTeamObjectiveCount(f, team);
        }

        public virtual int GetTeamObjectiveCount(Frame f, byte team) {
            int sum = 0;
            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;
            while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (mario->GetTeam(f) != team
                    || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)) {
                    continue;
                }

                sum += GetObjectiveCount(f, mario);
            }

            return sum;
        }

        public virtual int GetFirstPlaceObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int max = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > max) {
                    max = objectiveCount;
                }
            }

            return max;
        }

    }
}