using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Tiles;

namespace NSMB.Entities {

    [OrderAfter(typeof(EntityMover))]
    public abstract class BasicEntity : NetworkBehaviour, IBlockBumpable {

        //---Networked Variables
        [Networked(OnChanged = nameof(OnFacingRightChanged))] public NetworkBool FacingRight { get; set; }
        [Networked(OnChanged = nameof(OnIsActiveChanged))] public NetworkBool IsActive { get; set; }
        [Networked] public TickTimer DespawnTimer { get; set; }
        [Networked] protected NetworkBool FirstSpawn { get; set; } = true;
        [Networked] protected Vector2 SpawnLocation { get; set; }

        //---Components
        [SerializeField] public EntityMover body;
        [SerializeField] public AudioSource sfx;

        //---Properties
        public bool IsRespawningEntity => Object.IsSceneObject;

        //---Public Variables
        public bool checkForNearbyPlayersWhenRespawning = true;

        //---Private Variables
        private bool brickBreakSound;

        public virtual void OnValidate() {
            if (!body) body = GetComponent<EntityMover>();
            if (!sfx) sfx = GetComponent<AudioSource>();
        }

        public override void Spawned() {
            if (FirstSpawn) {
                SpawnLocation = body ? body.position : transform.position;

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
            }
            IsActive = false;
        }

        public virtual void OnIsActiveChanged() { }

        public virtual void OnFacingRightChanged() { }

        //---IBlockBumpable overrides
        public abstract void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction);

        //---RPCs
        public void SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, Enums.PrefabParticle prefab) {
            if (!Runner.IsForward || IsProxy) return;

            if (HasStateAuthority)
                Rpc_SpawnResizableParticle(pos, right, flip, size, prefab);
            else
                SpawnResizableParticleInternal(pos, right, flip, size, prefab);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies | RpcTargets.StateAuthority)]
        private void Rpc_SpawnResizableParticle(Vector2 pos, bool right, bool flip, Vector2 size, Enums.PrefabParticle prefab) {
            SpawnResizableParticleInternal(pos, right, flip, size, prefab);
        }

        private void SpawnResizableParticleInternal(Vector2 pos, bool right, bool flip, Vector2 size, Enums.PrefabParticle prefab) {
            GameObject particle = Instantiate(prefab.GetGameObject(), pos, Quaternion.Euler(0, 0, flip ? 180 : 0));

            SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
            sr.size = size;

            SimplePhysicsMover body = particle.GetComponent<SimplePhysicsMover>();
            body.velocity = new Vector2(right ? 7 : -7, 6);
            body.angularVelocity = right ^ flip ? -300 : 300;

            particle.transform.position += new Vector3(sr.size.x * 0.25f, size.y * 0.25f * (flip ? -1 : 1));
        }

        public void SpawnParticle(Vector2 pos, Enums.PrefabParticle prefab) {
            if (!Runner.IsForward || IsProxy) return;

            if (HasStateAuthority)
                Rpc_SpawnParticle(pos, prefab);
            else
                SpawnParticleInternal(pos, prefab);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies | RpcTargets.StateAuthority)]
        private void Rpc_SpawnParticle(Vector2 pos, Enums.PrefabParticle prefab) {
            SpawnParticleInternal(pos, prefab);
        }

        private void SpawnParticleInternal(Vector2 pos, Enums.PrefabParticle prefab) {
            Instantiate(prefab.GetGameObject(), pos, Quaternion.identity);
        }

        public void SpawnTileBreakParticle(Vector2 pos, Color color, float rot = 0) {
            if (!Runner.IsForward || IsProxy) return;

            if (HasStateAuthority)
                Rpc_SpawnTileBreakParticle(pos, color, rot);
            else
                SpawnTileBreakParticleInternal(pos, color, rot);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies | RpcTargets.StateAuthority)]
        private void Rpc_SpawnTileBreakParticle(Vector2 pos, Color color, float rot) {
            SpawnTileBreakParticleInternal(pos, color, rot);
        }

        private void SpawnTileBreakParticleInternal(Vector2 pos, Color color, float rot) {
            GameManager.Instance.particleManager.Play(Enums.Particle.Entity_BrickBreak, pos, color, rot);
        }


        //---OnChangeds
        public static void OnFacingRightChanged(Changed<BasicEntity> changed) {
            changed.Behaviour.OnFacingRightChanged();
        }

        public static void OnIsActiveChanged(Changed<BasicEntity> changed) {
            changed.Behaviour.OnIsActiveChanged();
        }
    }
}
