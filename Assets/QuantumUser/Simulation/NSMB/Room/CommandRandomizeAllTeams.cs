using Photon.Deterministic;
using System.Collections.Generic;
using System.Linq;

namespace Quantum {
    public class CommandRandomizeAllTeams : DeterministicCommand, ILobbyCommand {

        public int Teams;
        public bool Clear;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Teams);
            stream.Serialize(ref Clear);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* senderData) {
            // GOtta stop those filthy cheaters :/
            if (!senderData->IsRoomHost) {
                return;
            }

            if (Clear) {
                foreach ((_, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                    if (playerData->IsTeamLocked) {
                        playerData->IsTeamLocked = false;
                        f.Events.PlayerTeamChangedByHost(playerData->PlayerRef, 0, true);
                    }
                }
                return;
            }

            // Select [Teams] random teams we're gonna use
            List<int> teams = Enumerable.Range(0, 5).ToList();
            while (teams.Count > Teams) {
                teams.RemoveAt(f.RNG->Next(0, teams.Count));
            }

            // Shuffle the teams list
            for (int i = teams.Count - 1; i > 0; i--) {
                int randomIndex = f.RNG->Next(0, i + 1);
                (teams[i], teams[randomIndex]) = (teams[randomIndex], teams[i]);
            }

            // cannot store pointers, store entityRef to the players
            List<EntityRef> playerEntityRefs = new();
            foreach ((var entityRef, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                if (!playerData->ManualSpectator) {
                    playerEntityRefs.Add(entityRef);
                }
            }

            // now handle the list, this alGOrithm prevents infinite loops!
            int loopCount = playerEntityRefs.Count;
            for (int i = 0; i < loopCount; i++) {
                int team = teams[i % Teams];
                int index = f.RNG->Next(0, playerEntityRefs.Count);

                var playerData = f.Unsafe.GetPointer<PlayerData>(playerEntityRefs[index]);
                playerData->RequestedTeam = (byte) team;
                playerData->IsTeamLocked = playerData->PlayerRef != sender; // Don't self-lock the host
                playerEntityRefs.RemoveAt(index);
                f.Events.PlayerDataChanged(playerData->PlayerRef);
                f.Events.PlayerTeamRandomized(playerData->PlayerRef, (byte) team);
            }
        }
    }
}
