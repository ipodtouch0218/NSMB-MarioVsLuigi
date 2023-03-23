using UnityEngine;

using Fusion;
using NSMB.Extensions;

public abstract class BasicEntity : NetworkBehaviour, IBlockBumpable {

    //---Networked Variables
    private NetworkBool facingRightDefault = false;
    [Networked(Default = nameof(facingRightDefault), OnChanged = nameof(OnFacingRightChanged))] public NetworkBool FacingRight { get; set; }

    //---Components
    [SerializeField] public Rigidbody2D body;
    [SerializeField] public AudioSource sfx;

    //---Private Variables
    private bool brickBreakSound;

    public virtual void OnValidate() {
        if (!body) body = GetComponent<Rigidbody2D>();
        if (!sfx) sfx = GetComponent<AudioSource>();
    }

    public override void Spawned() {
        GameManager.Instance.networkObjects.Add(Object);
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

    public virtual void Destroy(DestroyCause cause) {
        Runner.Despawn(Object);
    }

    public virtual void OnFacingChanged() { }

    //---IBlockBumpable overrides
    public abstract void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction);

    //---OnChangeds
    public static void OnFacingRightChanged(Changed<BasicEntity> changed) {
        changed.Behaviour.OnFacingChanged();
    }

    //---Helpers
    public enum DestroyCause {
        None, Killplane, Lava
    }
}
