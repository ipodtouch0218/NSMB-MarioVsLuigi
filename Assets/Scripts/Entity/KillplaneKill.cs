using UnityEngine;

using Fusion;
using NSMB.Game;

public class KillplaneKill : SimulationBehaviour {

    //---Serialized Variables
    [SerializeField] private float killTime = 0f;

    //---Components
    [SerializeField] private BasicEntity entity;

    //---Private Variables
    private bool killed;

    public void OnValidate() {
        if (!entity) entity = GetComponent<BasicEntity>();
    }

    public override void FixedUpdateNetwork() {
        //used when we are networked
        if (!entity)
            return;

        //if it is not running, check if we're below the stage
        if (!entity.DespawnTimer.IsRunning && transform.position.y < GameManager.Instance.LevelMinY)
            entity.DespawnTimer = TickTimer.CreateFromSeconds(Runner, killTime);
    }

    public void Update() {
        // Used when we aren't networked
        if (killed || (Object && Object.IsValid))
            return;

        if (transform.position.y >= GameManager.Instance.LevelMinY)
            return;

        Destroy(gameObject, killTime);
        killed = true;
    }
}
