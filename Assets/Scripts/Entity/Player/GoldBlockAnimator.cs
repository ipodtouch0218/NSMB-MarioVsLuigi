using NSMB.Entities.Player;
using NSMB.UI.Game;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.CoinItems {
    public unsafe class GoldBlockAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private new Animation animation;
        [SerializeField] private CharacterPoseData[] poseData;
        [SerializeField] private GameObject flyingModel, helmetModel, coinPrefab;
        [SerializeField] private SkinnedMeshRenderer helmetMeshRenderer;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private GameObject helmetPropellerParent, helmetPropellerBlades;

        [SerializeField] private Vector3 lostViaDamageInitialVelocity = new(-4, 6, 0);
        [SerializeField] private Vector2 lostViaDamageGravity = new(0, -38f);
        [SerializeField] private float lostViaDamageInitialAngularVelocity = 600f, lostViaDamageAngularDeceleration = 600f;
        [SerializeField] private float lostViaDamageDespawnTime = 0.75f;
        [SerializeField] private Vector3 lostViaDamageRotationOffset;

        //---Private Variables
        private MarioPlayerAnimator marioPlayerAnimator;
        private CharacterPoseData currentCharacterPoseData;
        private List<Renderer> glowRenderers = new();

        private Vector2 lostViaDamageVelocity;
        private float lostViaDamageAngularVelocity;
        private bool lostViaDamage, resyncedThisFrame;
        private float collectTime;

        public void OnValidate() {
            this.SetIfNull(ref animation);
            this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerPickedUpGoldBlock>(this, OnMarioPlayerPickedUpGoldBlock, onlyIfEntityViewBound: true);
            QuantumEvent.Subscribe<EventGoldBlockGeneratedObjectiveCoin>(this, OnGoldBlockGeneratedObjectiveCoin, onlyIfEntityViewBound: true);
            QuantumEvent.Subscribe<EventGoldBlockBrokenByMega>(this, OnGoldBlockBrokenByMega, onlyIfEntityViewBound: true);
            QuantumEvent.Subscribe<EventGoldBlockLostViaDamage>(this, OnGoldBlockLostViaDamage, onlyIfEntityViewBound: true);
            QuantumEvent.Subscribe<EventGoldBlockRanOutOfCoins>(this, OnGoldBlockRanOutOfCoins, onlyIfEntityViewBound: true);

            helmetMeshRenderer.GetComponentsInChildren(true, glowRenderers);

            EntityView.OnEntityDestroyed.AddListener(OnEntityDestroyed);
            MarioPlayerAnimator.OnStartBlink += OnMarioStartBlink;
            RenderPipelineManager.beginCameraRendering += URPOnPreRender;
        }

        public void OnDestroy() {
            MarioPlayerAnimator.OnStartBlink -= OnMarioStartBlink;
            RenderPipelineManager.beginCameraRendering -= URPOnPreRender;
            EntityView.OnEntityDestroyed.RemoveListener(OnEntityDestroyed);
        }

        public void OnEntityDestroyed(QuantumGame game) {
            if (marioPlayerAnimator) {
                marioPlayerAnimator.DisableHeadwear = false;
            }
            if (resyncedThisFrame || flyingModel.activeInHierarchy) {
                Destroy(gameObject);
                Destroy(helmetModel);
            } else {
                Destroy(gameObject, 3f);
                Destroy(helmetModel, 3f);
            }
        }

        public void LateUpdate() {
            Transform t = helmetModel.transform;

            if (PredictedFrame == null) {
                return;
            }

            if (!marioPlayerAnimator && PredictedFrame.Unsafe.TryGetPointer(EntityRef, out GoldBlock* goldBlock)) {
                SwapParentView(goldBlock->AttachedTo);
            }

            if (lostViaDamage) {
                lostViaDamageAngularVelocity = Mathf.MoveTowards(lostViaDamageAngularVelocity, 0, lostViaDamageAngularDeceleration * Time.deltaTime);
                lostViaDamageVelocity += lostViaDamageGravity * Time.deltaTime;
                
                t.RotateAround(t.position + (t.rotation * lostViaDamageRotationOffset), t.right, lostViaDamageAngularVelocity * Time.deltaTime);
                t.position += (Vector3) lostViaDamageVelocity * Time.deltaTime;
            } else if (marioPlayerAnimator) { 
                PoseWithScale pose = marioPlayerAnimator.SmallModelActive ? currentCharacterPoseData.SmallModelPose : currentCharacterPoseData.LargeModelPose;
                t.SetParent(marioPlayerAnimator.ActiveHeadBone);
                t.SetLocalPositionAndRotation(pose.Position, pose.Rotation);

                float collectScaleFactor = (Time.time - collectTime) / 0.04f;
                t.localScale = pose.Scale + (Vector3.one * Mathf.Lerp(0.4f, 0, collectScaleFactor));

                if (PredictedFrame.Unsafe.TryGetPointer(marioPlayerAnimator.EntityRef, out MarioPlayer* mario)) {
                    helmetMeshRenderer.enabled = !mario->IsCrouchedInShell;
                    if (mario->CurrentPowerupState == PowerupState.HammerSuit && mario->IsCrouching) {
                        t.localScale = new(t.localScale.x, t.localScale.y, t.localScale.z * 0.7f);
                    }
                    helmetPropellerParent.SetActive(mario->CurrentPowerupState == PowerupState.PropellerMushroom);
                }
                helmetPropellerBlades.transform.rotation = marioPlayerAnimator.PropellerBlades.transform.rotation;
            } else {
                t.SetParent(transform);
            }

            resyncedThisFrame = false;
        }

        public void SwapParentView(EntityRef entity) {
            QuantumEntityView attachedView = EntityView.EntityViewUpdater.GetView(entity);
            if (attachedView == null) {
                SwapParentView(null);
            } else if (attachedView.TryGetComponent(out MarioPlayerAnimator mario)) {
                SwapParentView(mario);
            }
        }

        public void SwapParentView(MarioPlayerAnimator marioPlayerAnimator) {
            if (this.marioPlayerAnimator) {
                this.marioPlayerAnimator.DisableHeadwear = false;
            }

            this.marioPlayerAnimator = marioPlayerAnimator;

            if (marioPlayerAnimator) {
                flyingModel.SetActive(false);
                helmetModel.SetActive(true);
                foreach (var data in poseData) {
                    if (data.Character == marioPlayerAnimator.Character) {
                        helmetMeshRenderer.sharedMesh = data.Mesh;
                        currentCharacterPoseData = data;
                    }
                }
                marioPlayerAnimator.DisableHeadwear = true;
            } else {
                flyingModel.SetActive(true);
                helmetModel.SetActive(false);
                currentCharacterPoseData = null;
            }
        }

        private void OnMarioStartBlink(EntityRef marioEntity) {
            Frame f = PredictedFrame;
            if (f.Unsafe.TryGetPointer(EntityRef, out GoldBlock* goldBlock)) {
                if (goldBlock->AttachedTo == marioEntity) {
                    animation.Play();
                }
            }
        }

        private unsafe void URPOnPreRender(ScriptableRenderContext context, Camera camera) {
            Color glowColor = marioPlayerAnimator && (PredictedFrame.Global->Rules.TeamsEnabled || !IsCameraFocus(camera)) ? marioPlayerAnimator.GlowColor : Color.clear;
            foreach (var renderer in glowRenderers) {
                MaterialPropertyBlock mpb = new();
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("GlowColor", glowColor);
                renderer.SetPropertyBlock(mpb);
            }
        }

        private bool IsCameraFocus(Camera camera) {
            if (!marioPlayerAnimator) {
                return false;
            }
            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (marioPlayerAnimator.EntityRef == playerElement.Entity && (camera == playerElement.Camera || camera == playerElement.ScrollCamera || camera == playerElement.UICamera)) {
                    return true;
                }
            }
            return false;
        }

        private void OnMarioPlayerPickedUpGoldBlock(EventMarioPlayerPickedUpGoldBlock e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            SwapParentView(e.Entity);
            collectTime = Time.time;
            if (!IsReplayFastForwarding) {
                marioPlayerAnimator.PlaySound(SoundEffect.World_Gold_Block_Equip);
            }
        }

        private void OnGoldBlockGeneratedObjectiveCoin(EventGoldBlockGeneratedObjectiveCoin e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            if (!IsReplayFastForwarding) {
                sfx.pitch = Random.Range(1.35f, 1.45f);
                sfx.Play();
            }
            GameObject particle = Instantiate(coinPrefab, helmetModel.transform.position + (Vector3.up * 0.25f), Quaternion.identity);
            Destroy(particle, 0.3f);
        }

        private void OnGoldBlockBrokenByMega(EventGoldBlockBrokenByMega e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            // TODO: play break particles?
        }

        private void OnGoldBlockLostViaDamage(EventGoldBlockLostViaDamage e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            if (!IsReplayFastForwarding) {
                marioPlayerAnimator.PlaySound(SoundEffect.World_Gold_Block_Damage);
            }
            helmetModel.transform.SetParent(null, true);
            lostViaDamage = true;
            lostViaDamageVelocity = Vector3.ProjectOnPlane(helmetModel.transform.rotation * lostViaDamageInitialVelocity, Vector3.forward);
            lostViaDamageAngularVelocity = lostViaDamageInitialAngularVelocity;
            helmetMeshRenderer.enabled = true;

            IEnumerator DelayedParticlePlay() {
                yield return new WaitForSeconds(lostViaDamageDespawnTime);
                MiscParticles.Instance.Play(ParticleEffect.Puff, helmetModel.transform.position);
                Destroy(gameObject);
                Destroy(helmetModel);
            }
            StartCoroutine(DelayedParticlePlay());
        }

        private void OnGoldBlockRanOutOfCoins(EventGoldBlockRanOutOfCoins e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }
            if (!IsReplayFastForwarding) {
                marioPlayerAnimator.PlaySound(SoundEffect.World_Gold_Block_Finished);
            }
            helmetMeshRenderer.enabled = false;
            MiscParticles.Instance.Play(ParticleEffect.Puff, helmetModel.transform.position);
            Destroy(gameObject);
            Destroy(helmetModel);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            Frame f = PredictedFrame;
            if (EntityView) {
                lostViaDamage = false;
                if (f.Unsafe.TryGetPointer(EntityRef, out GoldBlock* goldBlock)) {
                    SwapParentView(goldBlock->AttachedTo);
                }
            } else {
                Destroy(gameObject);
                Destroy(helmetModel);
            }
            resyncedThisFrame = true;
        }

        [System.Serializable]
        public class CharacterPoseData {
            public AssetRef<CharacterAsset> Character;
            public Mesh Mesh;
            public PoseWithScale SmallModelPose, LargeModelPose;
        }

        [System.Serializable]
        public struct PoseWithScale {
            public Pose Pose;
            public Vector3 Position {
                get => Pose.position;
                set => Pose.position = value;
            }
            public Quaternion Rotation {
                get => Pose.rotation;
                set => Pose.rotation = value;
            }
            public Vector3 Scale;

            public static PoseWithScale identity => new PoseWithScale {
                Pose = Pose.identity,
                Scale = Vector3.one
            };
        }
    }
}