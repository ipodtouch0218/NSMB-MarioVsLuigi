using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.CoinItems {
    public unsafe class CoinItemAnimator : QuantumEntityViewComponent {

        //---Serialized
        [SerializeField] private Transform graphicsRoot;
        [SerializeField] private new Renderer renderer;
        [SerializeField] private Animator childAnimator;
        [SerializeField] private Animation childAnimation;
        [SerializeField] private float blinkingRate = 4, scaleRate = 0.1333f, scaleSize = 0.3f;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private ParticleSystem koopaSpawnParticles;

        //---Private
        private int originalSortingOrder;
        private bool inSpawnAnimation;
        private MaterialPropertyBlock mpb;

        public void OnValidate() {
            this.SetIfNull(ref renderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref childAnimator, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref childAnimation, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventCoinItemBecameActive>(this, OnCoinItemBecameActive);
        }

        public override void OnActivate(Frame f) {
            if (!f.Unsafe.TryGetPointer(EntityRef, out CoinItem* coinItem)) {
                return;
            }
            var scriptable = QuantumUnityDB.GetGlobalAsset(coinItem->Scriptable);

            originalSortingOrder = renderer.sortingOrder;
            renderer.GetPropertyBlock(mpb = new());

            if (coinItem->SpawnReason == PowerupSpawnReason.BlueKoopa && koopaSpawnParticles) {
                koopaSpawnParticles.Play();
            }

            if (f.Exists(coinItem->ParentMarioPlayer)) {
                // Following mario
                renderer.sortingOrder = 15;
                if (childAnimator) {
                    childAnimator.enabled = false;
                }
            } else if (coinItem->BlockSpawn) {
                // Block spawn
                renderer.sortingOrder = -1000;
                if (!IsReplayFastForwarding) {
                    sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
                }
                if (childAnimation) {
                    childAnimation.Play();
                }
            } else if (coinItem->LaunchSpawn) {
                // Spawn with velocity
                renderer.sortingOrder = -1000;
                if (!IsReplayFastForwarding) {
                    sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
                }
            } else {
                // Spawned by any other means (blue koopa, usually.)
                if (!IsReplayFastForwarding) {
                    sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
                }
                if (childAnimation) {
                    childAnimation.Play();
                }
                renderer.sortingOrder = originalSortingOrder;
            }
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }

            if (!f.Unsafe.TryGetPointer(EntityRef, out CoinItem* coinItem)) {
                return;
            }

            if (f.Unsafe.TryGetPointer(EntityRef, out PhysicsObject* physicsObject)) {
                if (childAnimator) {
                    childAnimator.SetBool("onGround", physicsObject->IsTouchingGround);
                }
            }

            HandleSpawningAnimation(f, coinItem);
            HandleDespawningBlinking(coinItem);
        }

        private void HandleSpawningAnimation(Frame f, CoinItem* coinItem) {
            if (f.Exists(coinItem->ParentMarioPlayer) && coinItem->SpawnAnimationFrames > 0) {
                float timeRemaining = coinItem->SpawnAnimationFrames / 60f;
                float adjustment = Mathf.PingPong(timeRemaining, scaleRate) / scaleRate * scaleSize;
                graphicsRoot.localScale = Vector3.one * (1 + adjustment);

                if (!inSpawnAnimation) {
                    mpb.SetFloat("WaveEnabled", 0);
                    renderer.SetPropertyBlock(mpb);
                    inSpawnAnimation = true;
                }
            } else if (inSpawnAnimation) {
                renderer.transform.localScale = Vector3.one;
                inSpawnAnimation = false;
                renderer.sortingOrder = 15;

                mpb.SetFloat("WaveEnabled", 1);
                renderer.SetPropertyBlock(mpb);
            }
        }

        private void HandleDespawningBlinking(CoinItem* coinItem) {
            if (coinItem->Lifetime <= 60) {
                renderer.enabled = ((coinItem->Lifetime / 60f * blinkingRate) % 1) > 0.5f;
            }
        }

        private void OnCoinItemBecameActive(EventCoinItemBecameActive e) {
            if (e.Entity != EntityRef) {
                return;
            }

            renderer.sortingOrder = originalSortingOrder;
            renderer.gameObject.transform.localScale = Vector3.one;
            if (childAnimator) {
                childAnimator.enabled = true;
            }
        }
    }
}
