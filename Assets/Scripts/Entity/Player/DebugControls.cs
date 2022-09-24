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
        if (kb[Key.LeftBracket].wasPressedThisFrame) {
            Time.timeScale /= 2;
            Debug.Log($"[DEBUG] Timescale set to {Time.timeScale}x");
        }
        if (kb[Key.RightBracket].wasPressedThisFrame) {
            Time.timeScale *= 2;
            Debug.Log($"[DEBUG] Timescale set to {Time.timeScale}x");
        }
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
        if (Keyboard.current[key].wasPressedThisFrame && !p.IsFrozen && !p.frozenObject && p.State != Enums.PowerupState.MegaMushroom && !p.pipeEntering && !p.IsInKnockback && !p.IsDamageable) {
            //PhotonNetwork.Instantiate("Prefabs/FrozenCube", p.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { p.photonView.ViewID });
        }
    }
    private void DebugItem(Key key, NetworkPrefabRef item) {
        if (!GameManager.Instance.localPlayer)
            return;

        if (Keyboard.current[key].wasPressedThisFrame) {
            SpawnDebugItem(item);
        }
    }

    private void SpawnDebugItem(NetworkPrefabRef prefab) {
        if (prefab == NetworkPrefabRef.Empty)
            prefab = Utils.GetRandomItem(NetworkHandler.Instance.runner, GameManager.Instance.localPlayer).prefab;

        NetworkHandler.Instance.runner.Spawn(prefab, onBeforeSpawned: (runner, obj) => {
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