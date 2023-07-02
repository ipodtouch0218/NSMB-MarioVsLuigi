using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;

namespace NSMB.Entities {

    [OrderAfter(typeof(NetworkPhysicsSimulation2D))]
    public abstract class BasicEntity : NetworkBehaviour, IBlockBumpable {

        //---Networked Variables
        [Networked(OnChanged = nameof(OnFacingRightChanged))] public NetworkBool FacingRight { get; set; }
        [Networked(OnChanged = nameof(OnIsActiveChanged))] public NetworkBool IsActive { get; set; }
        [Networked] public TickTimer DespawnTimer { get; set; }
        [Networked] protected NetworkBool FirstSpawn { get; set; } = true;
        [Networked] protected Vector2 SpawnLocation { get; set; }

        //---Components
        [SerializeField] public Rigidbody2D body;
        [SerializeField] public NetworkRigidbody2D nrb;
        [SerializeField] public AudioSource sfx;

        //---Properties
        public bool IsRespawningEntity => Object.IsSceneObject;

        //---Public Variables
        public bool checkForNearbyPlayersWhenRespawning = true;

        //---Private Variables
        private bool brickBreakSound;

        public virtual void OnValidate() {
            if (!body) body = GetComponent<Rigidbody2D>();
            if (!nrb) nrb = GetComponent<NetworkRigidbody2D>();
            if (!sfx) sfx = GetComponent<AudioSource>();
        }

        public override void Spawned() {
            if (FirstSpawn) {
                SpawnLocation = body.position;

                if (IsRespawningEntity)
                    DespawnEntity();
            }
            GameManager.Instance.networkObjects.Add(Object);
            OnFacingRightChanged();

            FirstSpawn = false;
        }

        public override void FixedUpdateNetwork() {
            if (DespawnTimer.Expired(Runner)) {
                DespawnTimer = TickTimer.None;
                DespawnEntity();
                return;
            }
        }

        public void Update() {
            brickBreakSound = false;
        }

        public void PlaySound(Enums.Sounds sound, CharacterData character = null, byte variant = 0, float volume = 1f) {
            if (sound == Enums.Sounds.World_Block_Break) {
                if (brickBreakSound)
                    return;

                brickBreakSound = true;
            }

            sfx.PlayOneShot(sound, character, variant, volume);
        }

        public virtual void RespawnEntity() {
            if (IsActive)
                return;

            if (body) {
                body.position = SpawnLocation;
                body.velocity = Vector2.zero;
                body.rotation = 0;
            }
            IsActive = true;
        }

        public virtual void DespawnEntity(object data = null) {
            if (!IsRespawningEntity) {
                Runner.Despawn(Object);
                return;
            }

            if (!IsActive)
                return;

            if (body) {
                body.position = SpawnLocation;
                body.velocity = Vector2.zero;
                body.rotation = 0;
            }
            IsActive = false;
        }

        public virtual void OnIsActiveChanged() { }

        public virtual void OnFacingRightChanged() { }

        //---IBlockBumpable overrides
        public abstract void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction);

        //---OnChangeds
        public static void OnFacingRightChanged(Changed<BasicEntity> changed) {
            changed.Behaviour.OnFacingRightChanged();
        }

        public static void OnIsActiveChanged(Changed<BasicEntity> changed) {
            changed.Behaviour.OnIsActiveChanged();
        }
    }
}
