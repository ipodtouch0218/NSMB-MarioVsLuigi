using NSMB.Extensions;
using Quantum;
using UnityEngine;

namespace NSMB.Entities.Player {
    public unsafe class GoldBlockHelmetAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private new Animation animation;
        [SerializeField] private CharacterPoseData[] poseData;
        [SerializeField] private GameObject flyingModel, helmetModel, coinPrefab;
        [SerializeField] private SkinnedMeshRenderer helmetMeshRenderer;
        [SerializeField] private AudioSource sfx;

        //---Private Variables
        private MarioPlayerAnimator marioPlayerAnimator;
        private CharacterPoseData currentCharacterPoseData;

        public void OnValidate() {
            this.SetIfNull(ref animation);
            this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerPickedUpGoldBlockHelmet>(this, OnMarioPlayerPickedUpGoldBlockHelmet);
            QuantumEvent.Subscribe<EventGoldBlockHelmetGeneratedObjectiveCoin>(this, OnGoldBlockHelmetGeneratedObjectiveCoin);
        }

        public override void OnActivate(Frame f) {
            MarioPlayerAnimator.OnStartBlink += OnMarioStartBlink;
        }

        public override void OnDeactivate() {
            MarioPlayerAnimator.OnStartBlink -= OnMarioStartBlink;
            if (marioPlayerAnimator) {
                marioPlayerAnimator.DisableHeadwear = false;
            }
            helmetMeshRenderer.enabled = false;
            Destroy(helmetModel, 0.5f);
        }

        public override void OnLateUpdateView() {
            if (marioPlayerAnimator) {
                PoseWithScale pose = marioPlayerAnimator.SmallModelActive ? currentCharacterPoseData.SmallModelPose : currentCharacterPoseData.LargeModelPose;
                helmetModel.transform.SetParent(marioPlayerAnimator.ActiveHeadBone);
                helmetModel.transform.SetLocalPositionAndRotation(pose.Position, pose.Rotation);
                helmetModel.transform.localScale = pose.Scale;
            } else {
                helmetModel.transform.SetParent(transform);
            }
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
            if (f.Unsafe.TryGetPointer(EntityRef, out GoldBlockHelmet* helmet)) {
                if (helmet->AttachedTo == marioEntity) {
                    animation.Play();
                }
            }
        }

        private void OnMarioPlayerPickedUpGoldBlockHelmet(EventMarioPlayerPickedUpGoldBlockHelmet e) {
            if (e.Helmet == EntityRef) {
                SwapParentView(e.Entity);
                // TODO; Play pickup sound
                marioPlayerAnimator.PlaySound(SoundEffect.Powerup_1UP_Collect);
            }
        }

        private void OnGoldBlockHelmetGeneratedObjectiveCoin(EventGoldBlockHelmetGeneratedObjectiveCoin e) {
            if (e.Helmet != EntityRef) {
                return;
            }

            sfx.pitch = Random.Range(1.35f, 1.45f);
            sfx.Play();
            GameObject particle = Instantiate(coinPrefab, helmetModel.transform.position + (Vector3.up * 0.25f), Quaternion.identity);
            Destroy(particle, 0.3f);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            Frame f = PredictedFrame;
            if (f.Unsafe.TryGetPointer(EntityRef, out GoldBlockHelmet* helmet)) {
                SwapParentView(helmet->AttachedTo);
            }
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