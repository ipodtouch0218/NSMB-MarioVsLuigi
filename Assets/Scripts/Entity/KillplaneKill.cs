using UnityEngine;

using Fusion;
using NSMB.Entities;
using NSMB.Extensions;
using NSMB.Game;

public class KillplaneKill : NetworkBehaviour {

    //---Serialized Variables
    [SerializeField] private float killTime = 0f;
    [SerializeField] private BasicEntity entity;

    //---Private Variables
    private bool killed;

    public void OnValidate() {
        this.SetIfNull(ref entity);
    }

    public override void FixedUpdateNetwork() {
        // Used when we are networked
        if (!entity) {
            return;
        }

        // If it is not running, check if we're below the stage
        if (!entity.DespawnTimer.IsRunning && transform.position.y < GameManager.Instance.LevelMinY) {
            entity.DespawnTimer = TickTimer.CreateFromSeconds(Runner, killTime);
        }
    }

    public void Update() {
        // Used when we aren't networked
        if (killed || Object) {
            return;
        }

        if (transform.position.y >= GameManager.Instance.LevelMinY) {
            return;
        }

        Destroy(gameObject, killTime);
        killed = true;
    }
}
