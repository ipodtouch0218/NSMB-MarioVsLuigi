﻿using System.Collections;
using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;
using NSMB.Utils;

namespace NSMB.Entities.Collectable {
    public class StarBouncer : CollectableEntity {

        //---Static Variables
        private static ContactFilter2D GroundFilter;

        //---Networked Variables
        [Networked] public NetworkBool IsStationary { get; set; }
        [Networked] public NetworkBool DroppedByPit { get; set; }
        [Networked] public NetworkBool Collectable { get; set; }
        [Networked] public NetworkBool Fast { get; set; }
        [Networked] public NetworkBool Passthrough { get; set; }

        //---Serialized Variables
        [SerializeField] private float pulseAmount = 0.2f, pulseSpeed = 0.2f;
        [SerializeField] private float moveSpeed = 3f, rotationSpeed = 30f, bounceAmount = 4f, deathBoostAmount = 20f;
        [SerializeField] private float blinkingSpeed = 0.5f, lifespan = 15f;
        [SerializeField] private Transform graphicTransform;
        [SerializeField] private ParticleSystem particles;

        //---Components
        [SerializeField] public SpriteRenderer sRenderer;
        [SerializeField] private PhysicsEntity physics;
        [SerializeField] private BoxCollider2D worldCollider;
        [SerializeField] private Animator animator;

        //--Private Variables
        private float pulseEffectCounter;
        private TrackIcon icon;

        public override void OnValidate() {
            base.OnValidate();
            if (!physics) physics = GetComponent<PhysicsEntity>();
            if (!sRenderer) sRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!worldCollider) worldCollider = GetComponent<BoxCollider2D>();
            if (!animator) animator = GetComponent<Animator>();
        }

        public void OnBeforeSpawned(byte direction, bool stationary, bool pit) {
            FacingRight = direction >= 2;
            Fast = direction == 0 || direction == 3;
            IsStationary = stationary;
            Collectable = stationary;
            DroppedByPit = pit;

            if (!stationary)
                DespawnTimer = TickTimer.CreateFromSeconds(Runner, lifespan);
        }

        public override void Spawned() {
            base.Spawned();
            icon = UIUpdater.Instance.CreateTrackIcon(this);

            if (IsStationary) {
                // Main star: use the "spawn-in" animation
                animator.enabled = true;
                body.isKinematic = true;
                body.velocity = Vector2.zero;
                StartCoroutine(PulseEffect());

            } else {
                // Player-dropped star
                Passthrough = true;
                sRenderer.color = new(1, 1, 1, 0.55f);
                gameObject.layer = Layers.LayerHitsNothing;
                body.velocity = new(moveSpeed * (FacingRight ? 1 : -1) * (Fast ? 2f : 1f), deathBoostAmount);

                // Death via pit boost, we need some extra velocity
                if (DroppedByPit)
                    body.velocity += Vector2.up * 3;

                body.isKinematic = false;
                worldCollider.enabled = true;
            }

            // Don't make a spawn sound if we're spawned before the game starts
            if (GameData.Instance.GameState == Enums.GameState.Playing)
                GameManager.Instance.sfx.PlayOneShot(Enums.Sounds.World_Star_Spawn);

            if (!GroundFilter.useTriggers) {
                GroundFilter.SetLayerMask((1 << Layers.LayerGround) | (1 << Layers.LayerPassthrough));
                GroundFilter.useTriggers = true;
            }
        }

        public override void Render() {
            if (IsStationary || (GameData.Instance?.GameEnded ?? false))
                return;

            graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (FacingRight ? -1 : 1) * Time.deltaTime), Space.Self);

            float timeRemaining = DespawnTimer.RemainingTime(Runner) ?? 0;
            sRenderer.enabled = !(timeRemaining < 5 && timeRemaining * 2 % (blinkingSpeed * 2) < blinkingSpeed);
        }

        public override void FixedUpdateNetwork() {
            base.FixedUpdateNetwork();
            if (GameData.Instance?.GameEnded ?? false) {
                body.velocity = Vector2.zero;
                body.isKinematic = true;
                return;
            }

            if (!Object || IsStationary)
                return;

            if (!Collectable && body.velocity.y < 0)
                sRenderer.color = Color.white;

            body.velocity = new(moveSpeed * (FacingRight ? 1 : -1) * (Fast ? 2f : 1f), body.velocity.y);
            Collectable |= body.velocity.y < 0;

            if (HandleCollision())
                return;

            if (Passthrough && Collectable && body.velocity.y <= 0 && !Utils.Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale) && !Runner.GetPhysicsScene2D().OverlapBox(body.position, Vector3.one * 0.33f, 0, GroundFilter)) {
                Passthrough = false;
                gameObject.layer = Layers.LayerEntity;
            }
            if (!Passthrough) {
                if (body.position.y < GameManager.Instance.LevelMinY ||
                    (GameManager.Instance.loopingLevel && (body.position.x < GameManager.Instance.LevelMinX - 0.5f || body.position.x > GameManager.Instance.LevelMaxX + 0.5f))) {
                    DespawnEntity();
                    return;
                }

                if (Utils.Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale)) {
                    gameObject.layer = Layers.LayerHitsNothing;
                } else {
                    gameObject.layer = Layers.LayerEntity;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {

            if (GameData.Instance && GameData.Instance.Object && (!GameData.Instance.GameEnded || GameData.Instance.gameEndTime + 3 > Runner.SimulationTime)) {
                if (!Collector) {
                    GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, transform.position);
                } else {
                    bool sameTeam = Collector.data.Team == runner.GetLocalPlayerData().Team || Collector.cameraController.IsControllingCamera;
                    Collector.PlaySoundEverywhere(sameTeam ? Enums.Sounds.World_Star_Collect : Enums.Sounds.World_Star_CollectOthers);
                    if (sameTeam) GlobalController.Instance.rumbleManager.RumbleForSeconds(0f, 0.8f, 0.1f);

                    Instantiate(PrefabList.Instance.Particle_StarCollect, transform.position, Quaternion.identity);
                }
            }

            if (icon)
                Destroy(icon.gameObject);
        }

        private IEnumerator PulseEffect() {
            while (true) {
                pulseEffectCounter += Time.deltaTime;
                float sin = Mathf.Sin(pulseEffectCounter * pulseSpeed) * pulseAmount;
                graphicTransform.localScale = Vector3.one * 3f + new Vector3(sin, sin, 0);

                yield return null;
            }
        }

        private bool HandleCollision() {
            physics.UpdateCollisions();

            if (physics.Data.HitLeft || physics.Data.HitRight) {
                FacingRight = physics.Data.HitLeft;
                body.velocity = new(moveSpeed * (FacingRight ? 1 : -1), body.velocity.y);
            }

            if (physics.Data.OnGround && Collectable) {
                body.velocity = new(body.velocity.x, bounceAmount);
                if (physics.Data.HitRoof) {
                    DespawnEntity();
                    return true;
                }
            }

            return false;
        }

        public void DisableAnimator() {
            animator.enabled = false;
        }

        //---IPlayerInteractable overrides
        public override void InteractWithPlayer(PlayerController player) {
            if (player.IsDead)
                return;

            if (!Collectable || Collector)
                return;

            Collector = player;

            // We can collect
            player.Stars = (byte) Mathf.Min(player.Stars + 1, SessionData.Instance.StarRequirement);

            // Game mechanics
            if (IsStationary && Runner.IsServer)
                GameManager.Instance.tileManager.ResetMap();

            GameData.Instance.CheckForWinner();

            // Despawn
            Runner.Despawn(Object);
            //DespawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
        }

        //---CollectableEntity overrides
        public override void OnCollectedChanged() {
            if (Collector) {
                // Play collection fx
                graphicTransform.gameObject.SetActive(false);
                particles.Stop();
                sfx.Stop();

                if (icon)
                    icon.gameObject.SetActive(false);

            } else {
                // oops...
                graphicTransform.gameObject.SetActive(true);
                particles.Play();
                sfx.Play();
                if (icon)
                    icon.gameObject.SetActive(true);
            }
        }

        //---IBlockBumpable overrides
        public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
            // Do nothing when bumped
        }
    }
}
