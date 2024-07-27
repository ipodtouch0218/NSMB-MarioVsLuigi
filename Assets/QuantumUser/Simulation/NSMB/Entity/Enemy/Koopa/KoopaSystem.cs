using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Physics2D;
using static IInteractableTile;
using UnityEngine;

namespace Quantum {

    public unsafe class KoopaSystem : SystemMainThreadFilter<KoopaSystem.Filter>, ISignalOnStageReset, ISignalOnThrowHoldable {
        public struct Filter {
            public EntityRef Entity;
            public Koopa* Koopa;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Holdable* Holdable;
        }

        public override void Update(Frame f, ref Filter filter) {
            var koopa = filter.Koopa;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            // Inactive check
            if (!koopa->IsActive) {
                return;
            }

            // Despawn off bottom of stage
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (transform->Position.Y < stage.StageWorldMin.Y) {
                koopa->IsActive = false;
                koopa->IsDead = true;
                physicsObject->IsFrozen = true;
                return;
            }

            if (koopa->IsDead) {
                return;
            }

            QuantumUtils.Decrement(ref koopa->IgnoreOwnerFrames);

            if (koopa->IsInShell && !koopa->IsKicked) {
                if (QuantumUtils.Decrement(ref koopa->WakeupFrames)) {
                    koopa->IsInShell = false;
                    koopa->CurrentSpeed = koopa->Speed;
                    koopa->FacingRight = false;
                }
            }

            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                koopa->FacingRight = physicsObject->IsTouchingLeftWall;

                if (koopa->IsKicked) {
                    bool? playBumpSound = null;
                    QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                    foreach (var contact in contacts) {
                        FP dot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                        bool right = dot < 0;
                        if (FPMath.Abs(dot) < FP._0_75) {
                            continue;
                        }

                        // Floor tiles.
                        var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                        StageTile tile = f.FindAsset(tileInstance.Tile);
                        if (tile is IInteractableTile it) {
                            it.Interact(f, filter.Entity, right ? InteractionDirection.Right : InteractionDirection.Left,
                                new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);

                            playBumpSound &= (playBumpSound ?? true) & tempPlayBumpSound;
                        }
                    }

                    if (playBumpSound ?? true) {
                        f.Events.PlayBumpSound(f, filter.Entity);
                    }
                }
            }

            // Move
            if (koopa->IsKicked 
                || !koopa->IsInShell
                || physicsObject->IsTouchingLeftWall
                || physicsObject->IsTouchingRightWall
                || physicsObject->IsTouchingGround) {

                physicsObject->Velocity.X = koopa->CurrentSpeed * (koopa->FacingRight ? 1 : -1);
            }

            // Collide
            var hits = f.Physics2D.OverlapShape(*transform, filter.Collider->Shape);
            for (int i = 0; i < hits.Count; i++) {
                OnCollision(f, ref filter, hits[i], stage);
            }
        }

        public void OnCollision(Frame f, ref Filter filter, Hit hit, VersusStageData stage) {
            if (hit.Entity == filter.Entity) {
                return;
            }
            if (f.Exists(filter.Holdable->Holder)) {
                return;
            }

            var koopa = filter.Koopa;
            var koopaTransform = filter.Transform;
            var collider = filter.Collider;

            if (filter.Holdable->PreviousHolder == hit.Entity && koopa->IgnoreOwnerFrames > 0) {
                return;
            }

            if (f.Unsafe.TryGetPointer(hit.Entity, out MarioPlayer* mario)
                && f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* marioTransform)
                && f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* marioPhysicsObject)) {

                if (mario->IsDead) {
                    return;
                }

                // Mario touched an alive koopa.
                QuantumUtils.UnwrapWorldLocations(stage, koopaTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

                if (mario->InstakillsEnemies(*marioPhysicsObject)) {
                    koopa->Kill(f, filter.Entity, hit.Entity, true);
                    return;
                }

                bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                if (groundpounded) {
                    koopa->EnterShell(f, filter.Entity, hit.Entity, false);
                    koopa->Kick(f, filter.Entity, hit.Entity, 1);

                } else if (koopa->IsKicked || !koopa->IsInShell) {
                    if (attackedFromAbove) {
                        // Enter Shell
                        koopa->EnterShell(f, filter.Entity, hit.Entity, false);
                        mario->DoEntityBounce = true;

                    } else {
                        // Damage
                        if (mario->IsCrouchedInShell) {
                            mario->FacingRight = damageDirection.X < 0;
                            marioPhysicsObject->Velocity.X = 0;

                        } else if (mario->IsDamageable) {
                            mario->Powerdown(f, hit.Entity, false);
                            koopa->FacingRight = damageDirection.X > 0;
                        }
                    }
                } else {
                    // Stationary in shell, always kick (if we cant pick it up)
                    if (mario->CanPickupItem(f)) {
                        filter.Holdable->Pickup(f, filter.Entity, hit.Entity);
                    } else {
                        koopa->Kick(f, filter.Entity, hit.Entity, marioPhysicsObject->Velocity.X / 7);
                        koopa->FacingRight = filter.Transform->Position.X > marioTransform->Position.X;
                    }
                }
            } else if (f.Unsafe.TryGetPointer(hit.Entity, out Koopa* otherKoopa)) {
                if (otherKoopa->IsDead || !otherKoopa->IsActive) {
                    return;
                }

                bool turn = true;
                if (koopa->IsKicked) {
                    // Destroy them
                    otherKoopa->Kill(f, hit.Entity, filter.Entity, false);
                    turn = false;
                }
                if (otherKoopa->IsKicked) {
                    // Destroy ourselves
                    otherKoopa->Kill(f, filter.Entity, hit.Entity, false);
                    turn = false;
                }

                if (turn) {
                    var transform = filter.Transform;
                    var otherTransform = f.Unsafe.GetPointer<Transform2D>(hit.Entity);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, otherTransform->Position, out var ourPos, out var theirPos);
                    bool right = ourPos.X > theirPos.X;
                    koopa->FacingRight = right;
                    otherKoopa->FacingRight = !right;
                }
            } else if (f.Unsafe.TryGetPointer(hit.Entity, out Goomba* goomba)) {
                if (goomba->IsDead || !goomba->IsActive) {
                    return;
                }

                if (koopa->IsKicked) {
                    // Destroy them
                    goomba->Kill(f, hit.Entity, filter.Entity, true);
                } else {
                    var transform = filter.Transform;
                    var otherTransform = f.Unsafe.GetPointer<Transform2D>(hit.Entity);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, otherTransform->Position, out var ourPos, out var theirPos);
                    bool right = ourPos.X > theirPos.X;
                    koopa->FacingRight = right;
                    goomba->FacingRight = !right;
                }
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Koopa>();
            while (filter.NextUnsafe(out EntityRef entity, out Koopa* koopa)) {
                if (!koopa->IsActive) {
                    koopa->Reset(f, entity);
                }
            }
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching) {
            if (!f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            physicsObject->Velocity.Y = 0;
            if (crouching) {
                physicsObject->Velocity.X = mario->FacingRight ? 1 : -1;
                koopa->CurrentSpeed = 0;
            } else {
                koopa->IsKicked = true;
                koopa->CurrentSpeed = koopa->KickSpeed + FPMath.Abs(marioPhysics->Velocity.X / 7);
                f.Events.MarioPlayerThrewObject(f, marioEntity, mario, entity);
            }
            koopa->FacingRight = mario->FacingRight;
            koopa->IgnoreOwnerFrames = 15;
        }
    }
}