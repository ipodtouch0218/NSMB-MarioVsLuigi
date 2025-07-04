using Photon.Deterministic;
using Quantum.Physics2D;
using UnityEngine;

namespace Quantum {
    public unsafe class ObjectiveCoinSystem : SystemMainThread, ISignalOnMarioPlayerDropObjective, ISignalOnMarioPlayerCollectedCoin,
        ISignalOnMarioPlayerDied {

        public override bool StartEnabled => false;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public StarCoin* StarCoin;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<StarCoin, MarioPlayer>(f, OnStarCoinMarioInteraction);
        }

        public override void Update(Frame f) {
            if (!f.Exists(f.Global->MainBigStar) && QuantumUtils.Decrement(ref f.Global->BigStarSpawnTimer)) {
                VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                HandleSpawningNewStarCoin(f, stage);
            }

            Filter filter = default;
            var filterStruct = f.Unsafe.FilterStruct<Filter>();
            while (filterStruct.Next(&filter)) {
                var starCoin = filter.StarCoin;

                if (starCoin->DespawnCounter > 0) {
                    if (QuantumUtils.Decrement(ref starCoin->DespawnCounter)) {
                        f.Events.CollectableDespawned(filter.Entity, filter.Transform->Position + (FPVector2.Down / 4), false);
                        f.Destroy(filter.Entity);
                    }
                }
            }
        }

        private void HandleSpawningNewStarCoin(Frame f, VersusStageData stage) {
            int spawnpoints = stage.BigStarSpawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedStarSpawns;

            bool spawnedStarCoin = false;
            for (int i = 0; i < spawnpoints; i++) {
                // Find a spot...
                if (f.Global->UsedStarSpawnCount >= spawnpoints) {
                    usedSpawnpoints.ClearAll();
                    f.Global->UsedStarSpawnCount = 0;
                }

                int count = f.RNG->Next(0, spawnpoints - f.Global->UsedStarSpawnCount);
                int index = 0;
                for (int j = 0; j < spawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                }
                usedSpawnpoints.Set(index);
                f.Global->UsedStarSpawnCount++;

                // Spawn a coin.
                FPVector2 position = stage.BigStarSpawnpoints[index];
                HitCollection hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);

                if (hits.Count == 0) {
                    // Hit no players
                    var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as CoinRunnersGamemode;
                    EntityRef newEntity = f.Create(gamemode.StarCoinPrototype);
                    f.Global->MainBigStar = newEntity;
                    var newStarCoinTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                    newStarCoinTransform->Position = position;
                    spawnedStarCoin = true;
                    break;
                }
            }

            if (!spawnedStarCoin) {
                f.Global->BigStarSpawnTimer = 30;
            }
        }

        public static void SpawnObjectiveCoins(Frame f, FPVector2 origin, int amount, byte exludeTeam, bool selfDamage) {
            if (amount <= 0) {
                return;
            }

            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as CoinRunnersGamemode;
            for (int i = 0; i < amount; i++) {
                EntityRef newCoin = f.Create(gamemode.ObjectiveCoinPrototype);
                var transform = f.Unsafe.GetPointer<Transform2D>(newCoin);
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(newCoin);

                transform->Position = origin;

                FP range = 100;
                FP angle = f.RNG->Next(0, range / 2) - f.RNG->Next(0, range / 2) + 90;
                physicsObject->Velocity = FPVector2.Rotate(FPVector2.Right, angle * FP.Deg2Rad);
                FP dot = FPVector2.Dot(physicsObject->Velocity, FPVector2.Up);
                physicsObject->Velocity *= dot * 7;

                if (origin.Y < stage.StageWorldMin.Y) {
                    physicsObject->Velocity.Y += 7;
                }

                var coin = f.Unsafe.GetPointer<ObjectiveCoin>(newCoin);
                coin->UncollectableByTeam = exludeTeam;
                coin->SpawnedViaSelfDamage = selfDamage;
            }

            f.Events.CoinGroupSpawned(origin, amount);
        }


        public void OnMarioPlayerDropObjective(Frame f, EntityRef entity, int amount, EntityRef attacker) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(entity);

            int coinDivideFactor = 1;
            if (f.Unsafe.TryGetPointer(attacker, out Holdable* holdableAttacker)) {
                if (f.Has<MarioPlayer>(holdableAttacker->Holder)) {
                    attacker = holdableAttacker->Holder;
                } else if (f.Has<MarioPlayer>(holdableAttacker->PreviousHolder)) {
                    attacker = holdableAttacker->PreviousHolder;
                } else {
                    attacker = entity;
                }
            } else if (f.Unsafe.TryGetPointer(attacker, out Projectile* attackerProjectile)) {
                // Projectiles spawn less coins.
                attacker = attackerProjectile->Owner;
                coinDivideFactor = 2;
            } else if (f.Has<Enemy>(attacker)) {
                // Don't give credit for normal entity damage.
                attacker = entity;
            }

            byte excludeTeamNumber = (byte) ((mario->GetTeam(f) + 1) ?? 0);
            bool selfDamage = false;
            if (f.Unsafe.TryGetPointer(attacker, out MarioPlayer* attackerMario)) {
                byte? team = mario->GetTeam(f);
                selfDamage = team != null && team == attackerMario->GetTeam(f);
            }

            // Spawn objective coins relative to the "amount" parameter
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
            int coinsToSpawn = (10 + 5 * (amount - 1)) / coinDivideFactor;
            SpawnObjectiveCoins(f, transform->Position + collider->Shape.Centroid + (FPVector2.Up * collider->Shape.Box.Extents.Y), coinsToSpawn, excludeTeamNumber, selfDamage);
            mario->GamemodeData.CoinRunners->ObjectiveCoins -= Mathf.Min(mario->GamemodeData.CoinRunners->ObjectiveCoins, coinsToSpawn) / 2;
            f.Events.MarioPlayerObjectiveCoinsChanged(entity);
        }

        public void OnStarCoinMarioInteraction(Frame f, EntityRef starCoinEntity, EntityRef marioEntity) {
            if (!f.Exists(starCoinEntity) || f.DestroyPending(starCoinEntity)) {
                return;
            }

            var starCoin = f.Unsafe.GetPointer<StarCoin>(starCoinEntity);
            if (starCoin->DespawnCounter > 0) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->IsDead) {
                return;
            }

            f.FindAsset<VersusStageData>(f.Map.UserAsset).ResetStage(f, false);
            f.Global->BigStarSpawnTimer = (ushort) (624 - (f.Global->RealPlayers * 12));
            
            mario->GamemodeData.CoinRunners->ObjectiveCoins += 25;
            starCoin->DespawnCounter = 105;
            starCoin->Collector = marioEntity;
            f.Events.MarioPlayerCollectedStarCoin(marioEntity, starCoinEntity);
            f.Events.MarioPlayerObjectiveCoinsChanged(marioEntity);
            GameLogicSystem.CheckForGameEnd(f);
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            // Lose half of all coins
            var mario = f.Unsafe.GetPointer<MarioPlayer>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            mario->GamemodeData.CoinRunners->ObjectiveCoins -= mario->GamemodeData.CoinRunners->ObjectiveCoins / 5;
            f.Events.MarioPlayerObjectiveCoinsChanged(entity);
        }

        public void OnMarioPlayerCollectedCoin(Frame f, EntityRef marioEntity, EntityRef coinEntity, FPVector2 worldLocation, QBoolean fromBlock, QBoolean downwards) {
            if (!f.Unsafe.TryGetPointer(coinEntity, out ObjectiveCoin* coin)) {
                // Powerup coin. Let the CoinSystem handle this.
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            mario->GamemodeData.CoinRunners->ObjectiveCoins++;

            f.Events.MarioPlayerCollectedObjectiveCoin(marioEntity);
            f.Events.MarioPlayerObjectiveCoinsChanged(marioEntity);
            GameLogicSystem.CheckForGameEnd(f);
        }
    }
}