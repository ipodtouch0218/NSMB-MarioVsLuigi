
using Photon.Deterministic;

namespace Quantum {
    public unsafe class StarChasersGamemode : GamemodeAsset {

        public override void EnableGamemode(Frame f) {
            f.SystemEnable<BigStarSystem>();
            f.Global->AutomaticStageRefreshTimer = f.Global->AutomaticStageRefreshInterval = 0;
        }

        public override void DisableGamemode(Frame f) {
            f.SystemDisable<BigStarSystem>();
        }

        public override void CheckForGameEnd(Frame f) {

            // End Condition: only one team alive
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            bool livesGame = f.Global->Rules.IsLivesEnabled;
            bool oneOrNoTeamAlive = true;
            int aliveTeam = -1;
            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if ((livesGame && mario->Lives <= 0) || mario->Disconnected) {
                    continue;
                }

                if (aliveTeam == -1 && mario->GetTeam(f) is byte team) {
                    aliveTeam = team;
                } else {
                    oneOrNoTeamAlive = false;
                    break;
                }
            }

            if (oneOrNoTeamAlive) {
                if (aliveTeam == -1) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                } else if (f.Global->RealPlayers > 1) {
                    // <team> wins, assuming more than 1 player
                    // so the player doesn't insta-win in a solo game.
                    GameLogicSystem.EndGame(f, false, aliveTeam);
                    return;
                }
            }

            int? winningTeam = GetWinningTeam(f, out int stars);

            // End Condition: team gets to enough stars
            if (winningTeam != null && stars >= f.Global->Rules.StarsToWin) {
                // <team> wins
                GameLogicSystem.EndGame(f, false, winningTeam.Value);
                return;
            }

            // End Condition: timer expires
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer <= 0) {
                if (f.Global->Rules.DrawOnTimeUp) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                }

                // Check if one team is winning
                if (winningTeam != null) {
                    // <team> wins
                    GameLogicSystem.EndGame(f, false, winningTeam.Value);
                    return;
                }
            }
        }

        public override int GetObjectiveCount(Frame f, PlayerRef player) {
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (player != mario->PlayerRef) {
                    continue;
                }

                return GetObjectiveCount(f, mario);
            }

            return -1;
        }

        public override int GetObjectiveCount(Frame f, MarioPlayer* mario) {
            if (mario == null || mario->Disconnected || (mario->Lives == 0 && f.Global->Rules.IsLivesEnabled)) {
                return -1;
            }

            return mario->GamemodeData.StarChasers->Stars;
        }

        // MAX(0,$B15+(IF(stars behind >0,LOG(B$1+1, 2.71828),0)*$C15*(1-(($M$15-$M$14))/$M$15)))
        public override PowerupAsset GetRandomItem(Frame f, MarioPlayer* mario) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            // "Losing" variable based on ln(x+1), x being the # of stars we're behind

            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;
            int leaderObjectiveCount = GetFirstPlaceObjectiveCount(f);

            var rules = f.Global->Rules;
            int starsToWin = rules.StarsToWin;
            bool custom = rules.CustomPowerupsEnabled;
            bool lives = rules.IsLivesEnabled;

            bool big = stage.SpawnBigPowerups;
            bool vertical = stage.SpawnVerticalPowerups;

            bool canSpawnMega = true;

            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;
            while (allPlayers.NextUnsafe(out _, out MarioPlayer* otherPlayer)) {
                // Check if another player is actively mega (not growing or shrinking)
                // If they are growing, we might have desynced. Hopefully, prediction wont be a full 2-3 seconds long...
                if (otherPlayer->CurrentPowerupState == PowerupState.MegaMushroom
                    && otherPlayer->MegaMushroomStartFrames == 0) {
                    canSpawnMega = false;
                    break;
                }
            }

            FP totalChance = 0;
            foreach (AssetRef<PowerupAsset> powerupAsset in f.SimulationConfig.AllPowerups) {
                PowerupAsset powerup = f.FindAsset(powerupAsset);
                if (powerup.State == PowerupState.MegaMushroom && !canSpawnMega) {
                    continue;
                }

                if ((powerup.BigPowerup && !big)
                    || (powerup.VerticalPowerup && !vertical)
                    || (powerup.CustomPowerup && !custom)
                    || (powerup.LivesOnlyPowerup && !lives)) {
                    continue;
                }

                totalChance += GetPowerupSpawnWeight(powerup, starsToWin, leaderObjectiveCount, ourObjectiveCount);
            }

            FP rand = mario->RNG.Next(0, totalChance);
            foreach (AssetRef<PowerupAsset> powerupAsset in f.SimulationConfig.AllPowerups) {
                PowerupAsset powerup = f.FindAsset(powerupAsset);
                if (powerup.State == PowerupState.MegaMushroom && !canSpawnMega) {
                    continue;
                }

                if ((powerup.BigPowerup && !big)
                    || (powerup.VerticalPowerup && !vertical)
                    || (powerup.CustomPowerup && !custom)
                    || (powerup.LivesOnlyPowerup && !lives)) {
                    continue;
                }

                FP chance = GetPowerupSpawnWeight(powerup, starsToWin, leaderObjectiveCount, ourObjectiveCount);

                if (rand < chance) {
                    return powerup;
                }

                rand -= chance;
            }

            return f.FindAsset(f.SimulationConfig.FallbackPowerup);
        }

        private FP GetPowerupSpawnWeight(PowerupAsset powerup, int starsToWin, int leaderStars, int ourStars) {
            int starDifference = leaderStars - ourStars;
            FP bonus = powerup.LosingSpawnBonus * FPMath.Log(starDifference + 1, FP.E) * (FP._1 - ((FP) (starsToWin - leaderStars) / starsToWin));
            return FPMath.Max(0, powerup.SpawnChance + bonus);
        }
    }
}