using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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
        if (kb[Key.LeftBracket].wasPressedThisFrame) {
            Time.timeScale /= 2;
            Debug.Log($"[DEBUG] Timescale set to {Time.timeScale}x");
        }
        if (kb[Key.RightBracket].wasPressedThisFrame) {
            Time.timeScale *= 2;
            Debug.Log($"[DEBUG] Timescale set to {Time.timeScale}x");
        }
        DebugItem(Key.Numpad0, null);
        DebugItem(Key.Numpad1, "Mushroom");
        DebugItem(Key.Numpad2, "FireFlower");
        DebugItem(Key.Numpad3, "BlueShell");
        DebugItem(Key.Numpad4, "MiniMushroom");
        DebugItem(Key.Numpad5, "MegaMushroom");
        DebugItem(Key.Numpad6, "Star");
        DebugItem(Key.Numpad7, "PropellerMushroom");
        DebugItem(Key.Numpad8, "IceFlower");
        DebugItem(Key.Numpad9, "1-Up");
        DebugEntity(Key.F1, "Koopa");
        DebugEntity(Key.F2, "RedKoopa");
        DebugEntity(Key.F3, "BlueKoopa");
        DebugEntity(Key.F4, "Goomba");
        DebugEntity(Key.F5, "Bobomb");
        DebugEntity(Key.F6, "BulletBill");
        DebugEntity(Key.F7, "Spiny");

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
        if (!GameManager.Instance.localPlayer)
            return;

        PlayerController p = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        if (Keyboard.current[key].wasPressedThisFrame && !p.IsFrozen && !p.frozenObject && p.State != Enums.PowerupState.MegaMushroom && !p.pipeEntering && !p.knockback && !p.IsDamageable) {
            //PhotonNetwork.Instantiate("Prefabs/FrozenCube", p.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { p.photonView.ViewID });
        }
    }
    private void DebugItem(Key key, string item) {
        if (!GameManager.Instance.localPlayer)
            return;

        if (Keyboard.current[key].wasPressedThisFrame) {
            SpawnDebugItem(item);
        }
    }

    private void SpawnDebugItem(string prefab) {
        GameObject prefabObj;

        if (prefab == null) {
            prefabObj = Utils.GetRandomItem(NetworkHandler.Instance.runner, GameManager.Instance.localPlayer).prefab;
        } else {
            prefabObj = (GameObject) Resources.Load("Prefabs/Powerup/" + prefab);
        }

        NetworkHandler.Instance.runner.Spawn(prefabObj, onBeforeSpawned: (runner, obj) => {
            obj.GetComponent<MovingPowerup>().OnBeforeSpawned(GameManager.Instance.localPlayer, 0f);
        });
    }

    private void DebugEntity(Key key, string entity) {
        if (!GameManager.Instance.localPlayer)
            return;

        if (Keyboard.current[key].wasPressedThisFrame) {
            Vector3 pos = GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().FacingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0);
            NetworkHandler.Instance.runner.Spawn((GameObject) Resources.Load("Prefabs/Enemy/" + entity), pos);
        }
    }

#endif
}