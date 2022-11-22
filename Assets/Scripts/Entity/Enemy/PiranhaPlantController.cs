﻿using UnityEngine;

using NSMB.Utils;
using Fusion;

public class PiranhaPlantController : KillableEntity {

    //---Networked Variables
    [Networked] public TickTimer PopupTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float playerDetectSize = 1;
    [SerializeField] private float popupTimerRequirement = 6f;

    //---Misc Variables
    private bool upsideDown;


    public override void Awake() {
        base.Awake();
        upsideDown = transform.eulerAngles.z != 0;
    }

    public override void Spawned() {
        PopupTimer = TickTimer.CreateFromSeconds(Runner, popupTimerRequirement);
    }

    public override void FixedUpdateNetwork() {
        GameManager gm = GameManager.Instance;
        if (gm) {
            if (gm.gameover) {
                animator.enabled = false;
                return;
            }

            if (!gm.IsMusicEnabled)
                return;
        }

        animator.SetBool("dead", IsDead);
        if (IsDead)
            return;

        if (Utils.GetTileAtWorldLocation(transform.position + (Vector3.down * 0.1f)) == null) {
            Kill();
            return;
        }

        if (PopupTimer.Expired(Runner)) {
            Collider2D closePlayer = Runner.GetPhysicsScene2D().OverlapCircle(transform.position, playerDetectSize, Layers.MaskOnlyPlayers);
            if (!closePlayer) {
                //no players nearby. pop up.
                animator.SetTrigger("popup");
            }
            PopupTimer = TickTimer.CreateFromSeconds(Runner, popupTimerRequirement);
        }
    }

    public void Respawn() {
        if (IsFrozen || !IsDead)
            return;

        IsFrozen = false;
        IsDead = false;
        PopupTimer = TickTimer.CreateFromSeconds(Runner, popupTimerRequirement);
        animator.Play("end", 0, 1);
        hitbox.enabled = true;
    }

    //---IPlayerInteractable overrides
    public override void InteractWithPlayer(PlayerController player) {
        if (player.IsStarmanInvincible || player.IsInShell || player.State == Enums.PowerupState.MegaMushroom) {
            Kill();
        } else {
            player.Powerdown(false);
        }
    }

    //---KillableEntity overrides
    public override void Kill() {
        PlaySound(Enums.Sounds.Enemy_PiranhaPlant_Death);
        PlaySound(IsFrozen ? Enums.Sounds.Enemy_Generic_FreezeShatter : Enums.Sounds.Enemy_Shell_Kick);

        IsDead = true;
        hitbox.enabled = false;

        GameManager.Instance.particleManager.Play(Enums.Particle.Generic_Puff, transform.position + new Vector3(0, upsideDown ? -0.5f : 0.5f, 0));
        Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, transform.position + Vector3.up * (upsideDown ? -1f : 1f));
    }

    public override void SpecialKill(bool right, bool groundpound, int combo) {
        Kill();
    }

    //---Debug
    private void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawSphere(transform.position + (Vector3) (playerDetectSize * new Vector2(0, transform.eulerAngles.z != 0 ? -0.5f : 0.5f)), playerDetectSize);
    }
}
