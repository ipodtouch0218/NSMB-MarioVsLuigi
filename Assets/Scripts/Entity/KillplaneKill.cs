using UnityEngine;

using Fusion;
using NSMB.Extensions;
using UnityEngine.Rendering.UI;

public class KillplaneKill : NetworkBehaviour {

    //---Networked Variables
    [Networked] private TickTimer KillTimer { get; set; }

    //---Serialized Variables
    [SerializeField] private float killTime = 0f;

    //---Private Variables
    private bool killed;

    public override void FixedUpdateNetwork() {
        //used when we are networked

        //if it is not running, check if we're below the stage
        if (!KillTimer.IsRunning && transform.position.y < GameManager.Instance.LevelMinY)
            KillTimer = TickTimer.CreateFromSeconds(Runner, killTime);

        if (KillTimer.Expired(Runner))
            Runner.Despawn(Object);
    }

    public void Update() {
        //used when we aren't networked
        if (killed || (Object && Object.IsValid))
            return;

        if (transform.position.y >= GameManager.Instance.LevelMinY)
            return;

        Destroy(gameObject, killTime);
        killed = true;
    }
}
