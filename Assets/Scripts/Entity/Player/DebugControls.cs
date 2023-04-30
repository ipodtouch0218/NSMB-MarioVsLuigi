using UnityEngine;
using UnityEngine.InputSystem;

using Fusion;
using NSMB.Utils;

public class DebugControls : MonoBehaviour {

//#if UNITY_EDITOR
    public void Start() {
        //if (!Debug.isDebugBuild && !Application.isEditor) {
        //    enabled = false;
        //    return;
        //}
    }

    public void Update() {
        Keyboard kb = Keyboard.current;
        DebugItem(Key.Numpad0, NetworkPrefabRef.Empty);
        DebugItem(Key.Numpad1, PrefabList.Instance.Powerup_Mushroom);
        DebugItem(Key.Numpad2, PrefabList.Instance.Powerup_FireFlower);
        DebugItem(Key.Numpad3, PrefabList.Instance.Powerup_BlueShell);
        DebugItem(Key.Numpad4, PrefabList.Instance.Powerup_MiniMushroom);
        DebugItem(Key.Numpad5, PrefabList.Instance.Powerup_MegaMushroom);
        DebugItem(Key.Numpad6, PrefabList.Instance.Powerup_Starman);
        DebugItem(Key.Numpad7, PrefabList.Instance.Powerup_PropellerMushroom);
        DebugItem(Key.Numpad8, PrefabList.Instance.Powerup_IceFlower);
        DebugItem(Key.Numpad9, PrefabList.Instance.Powerup_1Up);
        DebugEntity(Key.F1, PrefabList.Instance.Enemy_GreenKoopa);
        DebugEntity(Key.F2, PrefabList.Instance.Enemy_RedKoopa);
        DebugEntity(Key.F3, PrefabList.Instance.Enemy_BlueKoopa);
        DebugEntity(Key.F4, PrefabList.Instance.Enemy_Goomba);
        DebugEntity(Key.F5, PrefabList.Instance.Enemy_Bobomb);
        DebugEntity(Key.F6, PrefabList.Instance.Enemy_BulletBill);
        DebugEntity(Key.F7, PrefabList.Instance.Enemy_Spiny);
        DebugEntity(Key.F8, PrefabList.Instance.Obj_BigStar, (runner, obj) => {
            obj.GetComponent<StarBouncer>().OnBeforeSpawned(2, false, false);
        });

        FreezePlayer(Key.F9);

        if (kb[Key.U].wasPressedThisFrame) {
            GameManager.Instance.musicManager.Restart();
        }
        if (kb[Key.I].wasPressedThisFrame) {
            GameManager.Instance.musicManager.Pause();
        }
        if (kb[Key.O].wasPressedThisFrame) {
            GameManager.Instance.localPlayer.cameraController.IsControllingCamera = !GameManager.Instance.localPlayer.GetComponent<PlayerController>().cameraController.IsControllingCamera;
        }
        if (kb[Key.P].wasPressedThisFrame) {
            GameManager.Instance.localPlayer.Death(false, false);
        }
    }

    private void FreezePlayer(Key key) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame)
            return;

        PlayerController p = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        if (!p.IsFrozen && !p.FrozenCube && p.State != Enums.PowerupState.MegaMushroom && !p.CurrentPipe && !p.IsInKnockback && !p.IsDamageable) {
            NetworkHandler.Instance.runner.Spawn(PrefabList.Instance.Obj_FrozenCube, p.body.position, onBeforeSpawned: (runner, obj) => {
                FrozenCube cube = obj.GetComponent<FrozenCube>();
                cube.OnBeforeSpawned(p);
            });
        }
    }

    private void DebugItem(Key key, NetworkPrefabRef item) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame)
            return;

        // get random item if none is specified
        if (item == NetworkPrefabRef.Empty)
            item = Utils.GetRandomItem(GameManager.Instance.localPlayer).prefab;

        NetworkHandler.Instance.runner.Spawn(item, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<MovingPowerup>().OnBeforeSpawned(GameManager.Instance.localPlayer);
        });
    }

    private void DebugEntity(Key key, NetworkPrefabRef enemy, NetworkRunner.OnBeforeSpawned spawned = null) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame)
            return;

        Vector3 pos = GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().FacingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0);
        NetworkHandler.Instance.runner.Spawn(enemy, pos, onBeforeSpawned: spawned ?? DefaultEntitySpawned);

    }

    private void DefaultEntitySpawned(NetworkRunner runner, NetworkObject obj) {
        obj.GetComponent<BasicEntity>().isRespawningEntity = false;
    }
//#endif
}
