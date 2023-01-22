using UnityEngine;

using Fusion;
using NSMB.Utils;

public class EnemySpawnpoint : NetworkBehaviour {

    //---Networked Variables
    [Networked] private NetworkObject CurrentEntity { get; set; }

    //---Serialized Variables
    [SerializeField] private NetworkPrefabRef prefab;

    public virtual bool AttemptSpawning() {
        if (CurrentEntity)
            return false;

        if (Runner.GetPhysicsScene2D().OverlapCircle(transform.position, 1.5f, Layers.MaskOnlyPlayers))
            return false;

        CurrentEntity = Runner.Spawn(prefab, transform.position, transform.rotation);
        return true;
    }

#if UNITY_EDITOR
    public void OnValidate() {
        icon = null;
    }

    private string icon;
    public void OnDrawGizmos() {
        if (prefab == NetworkPrefabRef.Empty)
            return;

        icon ??= UnityEditor.AssetDatabase.GUIDToAssetPath(prefab.ToUnityGuidString()).Split("/")[^1].Replace(".prefab", "");
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
#endif
}
