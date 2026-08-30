using Photon.Deterministic;
using System;

namespace Quantum {
    public unsafe class StarChasersGamemode : GamemodeAsset {

        public AssetRef<EntityPrototype> BigStarPrototype;

        public override void EnableGamemode(Frame f) {
            f.SystemEnable<BigStarSystem>();
            f.Global->AutomaticStageRefreshTimer = f.Global->AutomaticStageRefreshInterval = 0;
        }

        public override void DisableGamemode(Frame f) {
            f.SystemDisable<BigStarSystem>();
        }

        public override void OnReturnToRoom(Frame f) {
            f.Global->MainBigStar = EntityRef.None;
            f.Global->BigStarSpawnTimer = 0;
            f.Global->UsedStarSpawns.ClearAll();
        }

        public override void CheckForGameEnd(Frame f) {
            // End Condition: only one team alive
            Span<int> objectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, objectiveCounts);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < objectiveCounts.Length; i++) {
                if (objectiveCounts[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            if (aliveTeamCount <= 1) {
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

            // End Condition: team gets to enough stars
            int? winningTeam = GetWinningTeam(f, out int stars);
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

        public override bool IsFastMusicEnabled(Frame f) {
            // Additional check- is any one player about to win
            GetWinningTeam(f, out int winningTeamStars);
            if (winningTeamStars + 1 >= f.Global->Rules.StarsToWin) {
                return true;
            }

            return base.IsFastMusicEnabled(f);
        }

        public override int GetObjectiveCount(Frame f, MarioPlayer* mario) {
            if (mario == null || !mario->IsValid(f)) {
                return -1;
            }

            // Make a copy to not modify the `type` variable
            // Which can cause desyncs.
            GamemodeSpecificData gamemodeDataCopy = mario->GamemodeData;
            return gamemodeDataCopy.StarChasers->Stars;
        }

        public override FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int ourStars) {
            int starsToWin = f.Global->Rules.StarsToWin;
            
            FP starsAvg = GetAverageObjectiveCount(f);
            int starsFirstPlace = GetFirstPlaceObjectiveCount(f);
            int starsLastPlace = GetLastPlaceObjectiveCount(f);

            FP avgDiff = ourStars - starsAvg;
            FP diffLeader = starsFirstPlace - ourStars;

            FP starBand = starsFirstPlace - starsLastPlace;

            FP normLeader = (FP)starsFirstPlace / starsToWin;
            FP normStarAvg = starsAvg / starsToWin;

            // item ranking formulas which is used for determining which items spawn
            FP itemRank = avgDiff - diffLeader / 5 * starBand / starsToWin * (normLeader * starsToWin / 4);

            // being above the average means you get different formula
            FP bonus;
            if (itemRank > 0) {
                FP magni = (starBand + normStarAvg * starsToWin) / starsToWin;
                bonus = item.AboveAverageBonus * FPMath.Log(FPMath.Abs(itemRank) + 1, FP.E) * magni;
            } else {
                FP magni = (starsAvg + starsFirstPlace * FP._0_50) / starsToWin;
                bonus = item.BelowAverageBonus * FPMath.Log(FPMath.Abs(itemRank) + 1, FP.E) * magni;
            }

            FP unclampedWeight = item.SpawnChance + bonus;

            if (f.TryResolveDictionary(f.Global->Rules.CoinItemCustomSpawnWeights, out var customWeights)) {
                if (customWeights.TryGetValue(item, out FP additionalWeight)) {
                    unclampedWeight += additionalWeight;
                }
            }
            
            return FPMath.Max(0, unclampedWeight);
        }
    }
}
