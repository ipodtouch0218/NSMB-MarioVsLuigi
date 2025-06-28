using NSMB.UI.Game;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using Quantum.Profiling;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class CoinAnimator : QuantumEntityViewComponent {

        //---Static
        public static event Action<Frame, CoinAnimator> ObjectiveCoinInitialized;
        public static event Action<CoinAnimator> ObjectiveCoinDestroyed;

        //---Serialized Variables
        [SerializeField] private LegacyAnimateSpriteRenderer defaultCoinAnimate, dottedCoinAnimate;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private ParticleSystem sparkles;
        [SerializeField] private bool looseCoin, objectiveCoin;

        //---Private Variables
        private bool alreadyBounced;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
            this.SetIfNull(ref sRenderer);
            this.SetIfNull(ref sparkles, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventCoinChangedType>(this, OnCoinChangedType, onlyIfEntityViewBound: true);
            QuantumEvent.Subscribe<EventCoinChangeCollected>(this, OnCoinChangedCollected, onlyIfEntityViewBound: true);
            QuantumEvent.Subscribe<EventCoinBounced>(this, OnCoinBounced, FilterOutReplayFastForward, onlyIfEntityViewBound: true);
            RenderPipelineManager.beginCameraRendering += URPOnPreRender;
        }

        public override void OnActivate(Frame f) {
            var coin = f.Unsafe.GetPointer<Coin>(EntityRef);

            bool dotted = coin->IsCurrentlyDotted;
            defaultCoinAnimate.isDisplaying = !dotted;
            dottedCoinAnimate.isDisplaying = dotted;
            sRenderer.enabled = true;
            alreadyBounced = false;

            if (looseCoin) {
                defaultCoinAnimate.frame = UnityEngine.Random.Range(0, defaultCoinAnimate.frames.Length);
                dottedCoinAnimate.frame = UnityEngine.Random.Range(0, dottedCoinAnimate.frames.Length);
            }
            if (objectiveCoin) {
                ObjectiveCoinInitialized?.Invoke(f, this);
            }
        }

        public override void OnDeactivate() {
            sRenderer.enabled = false;

            if (looseCoin) {
                ParticleSystem newSparkles = Instantiate(sparkles, sRenderer.transform.position, Quaternion.identity);
                newSparkles.gameObject.SetActive(true);
                newSparkles.Play();
                Destroy(newSparkles.gameObject, 0.5f);
            }
            if (objectiveCoin) {
                ObjectiveCoinDestroyed?.Invoke(this);
            }
        }

        public void OnDestroy() {
            RenderPipelineManager.beginCameraRendering -= URPOnPreRender;
        }

        public override void OnUpdateView() {
            using var profilerScope = HostProfiler.Start("CoinAnimator.OnUpdateView");
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)) {
                return;
            }

            var coin = f.Unsafe.GetPointer<Coin>(EntityRef);
            if (coin->CoinType.HasFlag(CoinType.BakedInStage)) {
                // Bodge: OnCoinChangedCollected doesnt work when collecting a coin at the exact same time as a level reset 
                sRenderer.enabled = !coin->IsCollected;
            } else {
                float despawnTimeRemaining = coin->Lifetime / 60f;
                sRenderer.enabled = !(despawnTimeRemaining < 3 && despawnTimeRemaining % 0.3f >= 0.15f);
            }
        }

        private unsafe void URPOnPreRender(ScriptableRenderContext context, Camera camera) {
            Frame f = PredictedFrame;
            if (f == null || !f.Unsafe.TryGetPointer(EntityRef, out ObjectiveCoin* coin)) {
                return;
            }

            Color newColor = sRenderer.color;
            bool sameTeam = IsSameTeamAsCamera(coin->UncollectableByTeam - 1, camera, out MarioPlayer* mario);
            if (mario != null && sameTeam && (!mario->CanCollectOwnTeamsObjectiveCoins || coin->SpawnedViaSelfDamage)) {
                // Can't collect
                newColor.a = 0.33f;
            } else {
                newColor.a = 1;
            }
            sRenderer.color = newColor;
        }

        private bool IsSameTeamAsCamera(int team, Camera camera, out MarioPlayer* mario) {
            Frame f = PredictedFrame;
            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (camera == playerElement.Camera || camera == playerElement.ScrollCamera || camera == playerElement.UICamera) {
                    if (!f.Unsafe.TryGetPointer(playerElement.Entity, out mario)) {
                        return false;
                    }

                    return (mario->GetTeam(f) ?? int.MinValue) == team;
                }
            }
            mario = null;
            return false;
        }

        private void OnCoinBounced(EventCoinBounced e) {
            if (e.Entity != EntityRef) {
                return;
            }

            if (alreadyBounced) {
                return;
            }

            sfx.pitch = objectiveCoin ? UnityEngine.Random.Range(1.35f, 1.45f) : 1f;
            sfx.volume = objectiveCoin ? 0.1f : 1f;
            sfx.PlayOneShot(SoundEffect.World_Coin_Drop);
            alreadyBounced = true;
        }

        private void OnCoinChangedCollected(EventCoinChangeCollected e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sRenderer.enabled = !e.Collected;
            if (e.Collected && !IsReplayFastForwarding) {
                if (looseCoin) {
                    sparkles.transform.SetParent(transform.parent);
                    sparkles.gameObject.SetActive(true);
                    sparkles.transform.position = sRenderer.transform.position;
                }

                sparkles.Play();
            }
        }

        private void OnCoinChangedType(EventCoinChangedType e) {
            if (e.Entity != EntityRef) {
                return;
            }

            bool dotted = e.Coin.IsCurrentlyDotted;
            defaultCoinAnimate.isDisplaying = !dotted;
            dottedCoinAnimate.isDisplaying = dotted;

            if (!dotted && !IsReplayFastForwarding) {
                sfx.PlayOneShot(SoundEffect.World_Coin_Dotted_Spawn);
            }
        }
    }
}
