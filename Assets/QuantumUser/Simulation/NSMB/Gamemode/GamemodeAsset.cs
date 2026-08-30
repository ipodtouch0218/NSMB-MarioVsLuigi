using Photon.Deterministic;
using Quantum.Prototypes;
using System;

namespace Quantum {
    public abstract unsafe class GamemodeAsset : AssetObject, IOrderedAsset {

        int IOrderedAsset.Order => Order;

        public string NamePrefix, TranslationKey, DescriptionTranslationKey, DiscordRpcKey;
        public string ObjectiveSymbolPrefix;
        public AssetRef<CoinItemAsset>[] AllCoinItems;
        public AssetRef<CoinItemAsset> FallbackCoinItem;
        public AssetRef<EntityPrototype> LooseCoinPrototype;
        public int Order;

        public GameRulesPrototype DefaultRules;

        public abstract void EnableGamemode(Frame f);

        public abstract void DisableGamemode(Frame f);

        public abstract void OnReturnToRoom(Frame f);

        public abstract void CheckForGameEnd(Frame f);

        public virtual int GetObjectiveCount(Frame f, PlayerRef player) {
            foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (player != mario->PlayerRef) {
                    continue;
                }

                return GetObjectiveCount(f, mario);
            }

            return -1;
        }

        public abstract int GetObjectiveCount(Frame f, MarioPlayer* mario);

        public virtual bool IsFastMusicEnabled(Frame f) {
            ref var rules = ref f.Global->Rules;

            if (rules.IsTimerEnabled && f.Global->Timer <= 60) {
                // Timer expiring, panic music
                return true;
            }

            if (rules.IsLivesEnabled) {
                // Low on lives. Two cases:
                // A: two players left, at least one has one life
                // B: three+ players left, all have one life

                int playersWithOneLife = 0;
                foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                    if (mario->IsValid(f)) {
                        if (mario->Lives == 1) {
                            playersWithOneLife++;
                        }
                    }
                }

                if ((f.Global->RealPlayers <= 2 && playersWithOneLife > 0) || (playersWithOneLife >= f.Global->RealPlayers)) {
                    return true;
                }
            }

            return false;
        }

        public bool CanItemSpawn(Frame f, CoinItemAsset coinItem, bool fromRouletteBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage.BannedCoinItems.Contains(coinItem)) {
                return false;
            }

            if (f.Global->Rules.IsCoinItemDisabled(f, coinItem)) {
                return false;
            }

            return coinItem.CanSpawn(f, fromRouletteBlock);
        }

        public virtual CoinItemAsset GetRandomItem(Frame f, MarioPlayer* mario, bool fromBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            // "Losing" variable based on ln(x+1)

            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;

            FP totalChance = 0;
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if (!CanItemSpawn(f, coinItem, fromBlock)) {
                    continue;
                }

                totalChance += GetItemSpawnWeight(f, coinItem, ourObjectiveCount);
            }

            FP rand = mario->RNG.Next(0, totalChance);
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if (!CanItemSpawn(f, coinItem, fromBlock)) {
                    continue;
                }

                FP chance = GetItemSpawnWeight(f, coinItem, ourObjectiveCount);

                if (rand < chance) {
                    return coinItem;
                }

                rand -= chance;
            }

            return f.FindAsset(FallbackCoinItem);
        }

        public abstract FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int ourObjectiveCount);

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
            for (int i = 0; i < teamObjectiveCounts.Length; i++) {
                teamObjectiveCounts[i] = -1;
            }

            foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (!mario->IsValid(f) || mario->GetTeam(f) is not byte team) {
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

        public readonly struct ObjectiveStatistics {
            public readonly FP Average;
            public readonly int Min, Max;
        }

        public virtual int GetTeamObjectiveCount(Frame f, byte team) {
            int sum = 0;
            foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (mario->GetTeam(f) != team || !mario->IsValid(f)) {
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

        public virtual int GetLastPlaceObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int min = int.MaxValue;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount < min && objectiveCount != -1) {
                    min = objectiveCount;
                }
            }

            return min;
        }

        public virtual FP GetAverageObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < teamObjectives.Length; i++) {
                if (teamObjectives[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            int sum = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > 0) sum += objectiveCount;
            }
            return (FP) sum / aliveTeamCount;
        }

        public virtual EntityRef SpawnLooseCoin(Frame f, FPVector2 position) {
            EntityRef newCoinEntity = f.Create(LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(newCoinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newCoinEntity);
            coinTransform->Position = position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            return newCoinEntity;
        }
    }
}