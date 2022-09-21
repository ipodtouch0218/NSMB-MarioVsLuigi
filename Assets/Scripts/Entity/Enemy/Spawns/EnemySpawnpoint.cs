using UnityEngine;

using NSMB.Utils;

public class EnemySpawnpoint : MonoBehaviour {

    [SerializeField] private GameObject prefab;

    private GameObject currentEntity;

    public virtual bool AttemptSpawning() {
        if (currentEntity)
            return false;

        if (NetworkHandler.Instance.runner.GetPhysicsScene2D().OverlapCircle(transform.position, 1.5f, Layers.MaskOnlyPlayers))
            return false;

        currentEntity = NetworkHandler.Instance.runner.Spawn(prefab, transform.position, transform.rotation).gameObject;
        return true;
    }

    public void OnDrawGizmos() {
        string icon = prefab.name;
        float offset = icon switch {
            "BlueKoopa" => 0.15f,
            "RedKoopa" => 0.15f,
            "Koopa" => 0.15f,
            "Bobomb" => 0.22f,
            "Goomba" => 0.22f,
            "Spiny" => -0.03125f,
            _ => 0,
        };
        Gizmos.DrawIcon(transform.position + offset * Vector3.up, icon, true, new Color(1, 1, 1, 0.5f));
    }
}
