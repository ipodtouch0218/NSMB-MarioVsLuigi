using UnityEngine;
using UnityEngine.InputSystem;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Collectable;
using NSMB.Entities.Collectable.Powerups;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Utils;

public class DebugControls : MonoBehaviour {

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD

    public void Start() {
        enabled = false;
    }

#else
    public void Start() {
        if (!Debug.isDebugBuild && !Application.isEditor) {
            enabled = false;
            return;
        }
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
            obj.GetComponent<BigStar>().OnBeforeSpawned(2, false, false);
        });

        FreezePlayer(Key.F9);
        KnockbackPlayer(Key.F10, false);
        KnockbackPlayer(Key.F11, true);

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
        if (kb[Key.LeftBracket].wasPressedThisFrame) {
            GameManager.Instance.localPlayer.DoKnockback(GameManager.Instance.localPlayer.FacingRight, 1, false, null);
        }
    }

    private void FreezePlayer(Key key) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame) {
            return;
        }

        PlayerController p = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        if (!p.IsFrozen && !p.FrozenCube && p.State != Enums.PowerupState.MegaMushroom && !p.CurrentPipe && !p.IsInKnockback && p.IsDamageable) {
            FrozenCube.FreezeEntity(NetworkHandler.Runner, p);
        }
    }

    private void KnockbackPlayer(Key key, bool weak) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame) {
            return;
        }

        PlayerController p = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        p.DoKnockback(p.FacingRight, 0, weak, null);
    }


    private void DebugItem(Key key, NetworkPrefabRef item) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame) {
            return;
        }

        // get random item if none is specified
        if (item == NetworkPrefabRef.Empty) {
            item = Utils.GetRandomItem(GameManager.Instance.localPlayer).prefab;
        }

        NetworkHandler.Runner.Spawn(item, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<Powerup>().OnBeforeSpawned(GameManager.Instance.localPlayer);
        });
    }

    private void DebugEntity(Key key, NetworkPrefabRef enemy, NetworkRunner.OnBeforeSpawned spawned = null) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame) {
            return;
        }

        Vector3 pos = GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().FacingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0);
        NetworkHandler.Runner.Spawn(enemy, pos, onBeforeSpawned: spawned);

    }
#endif
}
