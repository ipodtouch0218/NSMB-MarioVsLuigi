using UnityEngine;

using Fusion;

public class KillplaneKill : NetworkBehaviour {

    //---Networked Variables
    [Networked] private TickTimer KillTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float killTime = 0f;

    public override void Spawned() {
        KillTimer = TickTimer.CreateFromSeconds(Runner, killTime);
    }

    public override void FixedUpdateNetwork() {
        if (transform.position.y >= GameManager.Instance.LevelMinY)
            return;

        if (KillTimer.Expired(Runner)) {
            KillTimer = TickTimer.None;
            Runner.Despawn(Object);
        }
    }
}
