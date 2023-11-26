using UnityEngine;

using Fusion;

[CreateAssetMenu(fileName = "New Prefab List", menuName = "ScriptableObjects/PrefabList")]
public class PrefabList : ScriptableObject {

    public static PrefabList Instance => ScriptableManager.Instance.prefabs;

    //---Network Helpers
    public NetworkPrefabRef PlayerDataHolder, SessionDataHolder;
    public NetworkPrefabRef TilemapChunk;

    //---World Elements
    public NetworkPrefabRef Obj_Fireball;
    public NetworkPrefabRef Obj_LooseCoin;
    public NetworkPrefabRef Obj_BigStar;
    public NetworkPrefabRef Obj_BlockBump;
    public NetworkPrefabRef Obj_FrozenCube;

    //---Enemies
    public NetworkPrefabRef Enemy_GreenKoopa, Enemy_RedKoopa, Enemy_BlueKoopa, Enemy_Spiny;
    public NetworkPrefabRef Enemy_Goomba, Enemy_Bobomb;
    public NetworkPrefabRef Enemy_BulletBill;

    //---Powerups
    public NetworkPrefabRef Powerup_1Up;
    public NetworkPrefabRef Powerup_Starman, Powerup_MegaMushroom;
    public NetworkPrefabRef Powerup_Mushroom, Powerup_FireFlower, Powerup_BlueShell, Powerup_PropellerMushroom, Powerup_IceFlower;
    public NetworkPrefabRef Powerup_MiniMushroom;

    //---Particles
    public GameObject Particle_1Up, Particle_Giant;
    public GameObject Particle_CoinNumber, Particle_CoinFromBlock;
    public GameObject Particle_StarCollect;
    public RespawnParticle Particle_Respawn;
    public GameObject Particle_Groundpound, Particle_Walljump;
    public GameObject Particle_EnemySpecialKill;
    public GameObject Particle_IceBreak;

}
