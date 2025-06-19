using NSMB.Extensions;
using Quantum;
using UnityEngine;

namespace NSMB.Entities.Player {
    public unsafe class GoldBlockAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private new Animation animation;
        [SerializeField] private CharacterPoseData[] poseData;
        [SerializeField] private GameObject flyingModel, helmetModel, coinPrefab;
        [SerializeField] private SkinnedMeshRenderer helmetMeshRenderer;
        [SerializeField] private AudioSource sfx;

        [SerializeField] private Vector3 lostViaDamageInitialVelocity = new(-4, 6, 0);
        [SerializeField] private Vector2 lostViaDamageGravity = new(0, -38f);
        [SerializeField] private float lostViaDamageInitialAngularVelocity = 600f, lostViaDamageAngularDeceleration = 600f;
        [SerializeField] private float lostViaDamageDespawnTime = 0.75f;
        [SerializeField] private Vector3 lostViaDamageRotationOffset;

        //---Private Variables
        private MarioPlayerAnimator marioPlayerAnimator;
        private CharacterPoseData currentCharacterPoseData;

        public Vector2 lostViaDamageVelocity;
        public float lostViaDamageAngularVelocity;
        public bool lostViaDamage;

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

            EntityView.OnEntityDestroyed.AddListener(OnEntityDestroyed);
            MarioPlayerAnimator.OnStartBlink += OnMarioStartBlink;
        }

        public void OnDestroy() {
            MarioPlayerAnimator.OnStartBlink -= OnMarioStartBlink;
            EntityView.OnEntityDestroyed.RemoveListener(OnEntityDestroyed);
            if (helmetMeshRenderer.gameObject.activeInHierarchy) {
                MiscParticles.Instance.Play(ParticleEffect.Puff, helmetModel.transform.position);
            }
        }

        public void OnEntityDestroyed(QuantumGame game) {
            if (marioPlayerAnimator) {
                marioPlayerAnimator.DisableHeadwear = false;
            }

            helmetMeshRenderer.enabled = !lostViaDamage;
            float delay = lostViaDamage ? lostViaDamageDespawnTime: 0;
            Destroy(gameObject, delay);
            Destroy(helmetModel, delay);
        }

        public void LateUpdate() {
            Transform t = helmetModel.transform;
            if (lostViaDamage) {
                lostViaDamageAngularVelocity = Mathf.MoveTowards(lostViaDamageAngularVelocity, 0, lostViaDamageAngularDeceleration * Time.deltaTime);
                lostViaDamageVelocity += lostViaDamageGravity * Time.deltaTime;
                
                t.RotateAround(t.position + (t.rotation * lostViaDamageRotationOffset), t.right, lostViaDamageAngularVelocity * Time.deltaTime);
                t.position += (Vector3) lostViaDamageVelocity * Time.deltaTime;
            } else if (marioPlayerAnimator) { 
                PoseWithScale pose = marioPlayerAnimator.SmallModelActive ? currentCharacterPoseData.SmallModelPose : currentCharacterPoseData.LargeModelPose;
                t.SetParent(marioPlayerAnimator.ActiveHeadBone);
                t.SetLocalPositionAndRotation(pose.Position, pose.Rotation);
                t.localScale = pose.Scale;
            } else {
                t.SetParent(transform);
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
            if (f.Unsafe.TryGetPointer(EntityRef, out GoldBlock* goldBlock)) {
                if (goldBlock->AttachedTo == marioEntity) {
                    animation.Play();
                }
            }
        }

        private void OnMarioPlayerPickedUpGoldBlock(EventMarioPlayerPickedUpGoldBlock e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            SwapParentView(e.Entity);
            if (!NetworkHandler.IsReplayFastForwarding) {
                marioPlayerAnimator.PlaySound(SoundEffect.World_Gold_Block_Equip);
            }
        }

        private void OnGoldBlockGeneratedObjectiveCoin(EventGoldBlockGeneratedObjectiveCoin e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            if (!NetworkHandler.IsReplayFastForwarding) {
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


        }

        private void OnGoldBlockLostViaDamage(EventGoldBlockLostViaDamage e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            marioPlayerAnimator.PlaySound(SoundEffect.World_Gold_Block_Damage);
            helmetModel.transform.SetParent(null, true);
            lostViaDamage = true;
            lostViaDamageVelocity = Vector3.ProjectOnPlane(helmetModel.transform.rotation * lostViaDamageInitialVelocity, Vector3.forward);
            lostViaDamageAngularVelocity = lostViaDamageInitialAngularVelocity;
            helmetMeshRenderer.enabled = true;
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
        }

        private void OnGoldBlockRanOutOfCoins(EventGoldBlockRanOutOfCoins e) {
            if (e.GoldBlock != EntityRef) {
                return;
            }

            marioPlayerAnimator.PlaySound(SoundEffect.World_Gold_Block_Finished);
            helmetMeshRenderer.enabled = false;
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