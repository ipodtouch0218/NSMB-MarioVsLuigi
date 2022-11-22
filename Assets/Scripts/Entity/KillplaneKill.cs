using UnityEngine;

using Fusion;

public class KillplaneKill : NetworkBehaviour {

    //---Networked Variables
    [Networked] private TickTimer KillTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float killTime = 0f;

    public override void FixedUpdateNetwork() {
        if (transform.position.y >= GameManager.Instance.GetLevelMinY())
            return;

        if (!KillTimer.IsRunning)
            KillTimer = TickTimer.CreateFromSeconds(Runner, killTime);

        if (KillTimer.Expired(Runner))
            Runner.Despawn(Object);
    }
}
