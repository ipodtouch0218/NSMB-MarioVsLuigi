using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

using Fusion;
using NSMB.Utils;

public class DebugControls : MonoBehaviour {

    public bool editMode;
    public ScriptableRendererFeature feature;

#if UNITY_EDITOR

    public void Start() {
        if (!Debug.isDebugBuild && !Application.isEditor) {
            enabled = false;
            return;
        }
    }

    public void Update() {
        Keyboard kb = Keyboard.current;
        //if (kb[Key.LeftBracket].wasPressedThisFrame) {
        //    Time.timeScale /= 2;
        //    Debug.Log($"[DEBUG] Timescale set to {Time.timeScale}x");
        //}
        //if (kb[Key.RightBracket].wasPressedThisFrame) {
        //    Time.timeScale *= 2;
        //    Debug.Log($"[DEBUG] Timescale set to {Time.timeScale}x");
        //}
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

        FreezePlayer(Key.F9);

        if (kb[Key.R].wasPressedThisFrame) {
            GameObject nametag = GameManager.Instance.transform.Find("NametagCanvas").gameObject;
            nametag.SetActive(!nametag.activeSelf);
        }
        if (kb[Key.T].wasPressedThisFrame) {
            CanvasGroup group = GameManager.Instance.transform.Find("New HUD").GetComponent<CanvasGroup>();
            group.alpha = 1f - group.alpha;
        }
        if (kb[Key.Y].wasPressedThisFrame) {
            Settings.Instance.ndsResolution = !Settings.Instance.ndsResolution;
        }
        if (kb[Key.U].wasPressedThisFrame) {
            Settings.Instance.fourByThreeRatio = !Settings.Instance.fourByThreeRatio;
        }
        if (kb[Key.I].wasPressedThisFrame) {
            feature.SetActive(!feature.isActive);
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
        if (!p.IsFrozen && !p.frozenObject && p.State != Enums.PowerupState.MegaMushroom && !p.pipeEntering && !p.IsInKnockback && !p.IsDamageable) {
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
            obj.GetComponent<MovingPowerup>().OnBeforeSpawned(GameManager.Instance.localPlayer, 0f);
        });
    }

    private void DebugEntity(Key key, NetworkPrefabRef enemy) {
        if (!GameManager.Instance.localPlayer || !Keyboard.current[key].wasPressedThisFrame)
            return;

        Vector3 pos = GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().FacingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0);
        NetworkHandler.Instance.runner.Spawn(enemy, pos);
    }
#endif
}
