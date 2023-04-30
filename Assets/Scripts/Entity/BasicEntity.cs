using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Tiles;

[OrderAfter(typeof(NetworkPhysicsSimulation2D))]
public abstract class BasicEntity : NetworkBehaviour, IBlockBumpable {

    //---Networked Variables
    private NetworkBool facingRightDefault = false;
    [Networked(Default = nameof(facingRightDefault), OnChanged = nameof(OnFacingRightChanged))] public NetworkBool FacingRight { get; set; }
    [Networked(OnChanged = nameof(OnIsActiveChanged))] public NetworkBool IsActive { get; set; }
    [Networked] public TickTimer DespawnTimer { get; set; }

    //---Public Variables
    public bool isRespawningEntity;

    //---Components
    [SerializeField] public Rigidbody2D body;
    [SerializeField] public AudioSource sfx;

    //---Private Variables
    private bool brickBreakSound;
    private Vector2 spawnLocation;

    public virtual void OnValidate() {
        if (!body) body = GetComponent<Rigidbody2D>();
        if (!sfx) sfx = GetComponent<AudioSource>();
    }

    public virtual void Start() {
        if (body)
            spawnLocation = body.position;
    }

    public override void Spawned() {
        GameManager.Instance.networkObjects.Add(Object);
        if (isRespawningEntity)
            DespawnEntity();
        OnFacingRightChanged();
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
            body.position = spawnLocation;
            body.velocity = Vector2.zero;
            body.rotation = 0;
        }
        IsActive = true;
    }

    public virtual void DespawnEntity(object data = null) {
        if (!isRespawningEntity) {
            Runner.Despawn(Object);
            return;
        }

        if (!IsActive)
            return;

        if (body) {
            body.position = spawnLocation;
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
