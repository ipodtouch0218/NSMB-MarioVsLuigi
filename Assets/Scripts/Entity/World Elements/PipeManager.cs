using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Utils;

public class PipeManager : NetworkBehaviour, IPlayerInteractable {

    //---Properties
    public bool IsEntryAllowed => entryAllowed;
    public bool IsRoofPipe => bottom;
    public bool IsMiniOnly => miniOnly;
    public PipeManager OtherPipe => otherPipe;

    //---Serialized Variables
    [SerializeField] private bool entryAllowed = true, bottom = false, miniOnly = false;
    [SerializeField] private PipeManager otherPipe;
    [SerializeField] private BoxCollider2D hitbox;

    public void OnValidate() {
        this.SetIfNull(ref hitbox);
    }

    public void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact) {
        if (!entryAllowed) {
            return;
        }

        if (player.PipeReentryTimer.IsActive(Runner)) {
            return;
        }

        if (miniOnly && player.State != Enums.PowerupState.MiniMushroom) {
            return;
        }

        Utils.UnwrapLocations((Vector2) transform.position + hitbox.offset, player.body.Position, out Vector2 ours, out Vector2 theirs);
        float angle = Mathf.Abs(Vector2.SignedAngle(ours - theirs, Vector2.up));

        bool canEnter =
            (bottom && player.body.Data.HitRoof && angle < 90 && player.PreviousInputs.Buttons.IsSet(PlayerControls.Up)) ||
            (!bottom && player.IsOnGround && angle < 90 && player.PreviousInputs.Buttons.IsSet(PlayerControls.Down));

        if (!canEnter) {
            return;
        }

        // All clear enjoy your ride
        player.EnterPipe(this, bottom ? Vector2.up : Vector2.down);
    }

    //---Debug
#if UNITY_EDITOR
    private static readonly Color LineColor = new(1f, 1f, 0f, 0.25f);
    private static readonly float LineWidth = 0.125f;
    public void OnDrawGizmos() {
        if (!otherPipe) {
            return;
        }

        Gizmos.color = LineColor;
        Vector3 source = transform.position;
        Vector3 destination = otherPipe.transform.position;
        Vector3 direction = (destination - source).normalized;
        while (Vector3.Distance(source, destination) > LineWidth * 4f) {
            Gizmos.DrawLine(source, source + direction * LineWidth);
            source += LineWidth * 4f * direction;
        }
        Gizmos.DrawLine(source, destination);
    }
#endif
}
