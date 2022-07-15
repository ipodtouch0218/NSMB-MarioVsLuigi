using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Photon.Pun;
using NSMB.Utils;

public class DebugControls : MonoBehaviour {

    public bool editMode;
    public ScriptableRendererFeature feature;
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
        DebugEntity(Key.Digit1, "Koopa");
        DebugEntity(Key.Digit2, "RedKoopa");
        DebugEntity(Key.Digit3, "BlueKoopa");
        DebugEntity(Key.Digit4, "Goomba");
        DebugEntity(Key.Digit5, "Bobomb");
        DebugEntity(Key.Digit6, "BulletBill");
        DebugEntity(Key.Digit7, "Spiny");

        FreezePlayer(Key.Digit9);

        if (kb[Key.F1].wasPressedThisFrame) {
            GameObject nametag = GameManager.Instance.transform.Find("NametagCanvas").gameObject;
            nametag.SetActive(!nametag.activeSelf);
        }
        if (kb[Key.F2].wasPressedThisFrame) {
            CanvasGroup group = GameManager.Instance.transform.Find("New HUD").GetComponent<CanvasGroup>();
            group.alpha = 1f - group.alpha;
        }
        if (kb[Key.F3].wasPressedThisFrame) {
            Settings.Instance.ndsResolution = !Settings.Instance.ndsResolution;
        }
        if (kb[Key.F4].wasPressedThisFrame) {
            Settings.Instance.fourByThreeRatio = !Settings.Instance.fourByThreeRatio;
        }
        if (kb[Key.F5].wasPressedThisFrame) {
            feature.SetActive(!feature.isActive);
        }
        if (kb[Key.F6].wasPressedThisFrame) {
            GameManager.Instance.localPlayer.GetComponent<PlayerController>().cameraController.controlCamera = !GameManager.Instance.localPlayer.GetComponent<PlayerController>().cameraController.controlCamera;
        }
        if (kb[Key.F12].wasPressedThisFrame) {
            GameManager.Instance.localPlayer.GetPhotonView().RPC("Death", RpcTarget.All, false, false);
        }
    }

    private void FreezePlayer(Key key) {
        if (!GameManager.Instance.localPlayer)
            return;

        PlayerController p = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        if (Keyboard.current[key].wasPressedThisFrame && !p.Frozen && !p.frozenObject && p.state != Enums.PowerupState.MegaMushroom && !p.pipeEntering && !p.knockback && p.hitInvincibilityCounter <= 0) {
            PhotonNetwork.Instantiate("Prefabs/FrozenCube", p.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { p.photonView.ViewID });
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
        if (!PhotonNetwork.IsMasterClient)
            return;

        PlayerController local = GameManager.Instance.localPlayer.GetComponent<PlayerController>();

        if (prefab == null)
            prefab = Utils.GetRandomItem(local).prefab;

        PhotonNetwork.Instantiate("Prefabs/Powerup/" + prefab, local.body.position + Vector2.up * 5f, Quaternion.identity, 0, new object[] { local.photonView.ViewID });
    }

    private void DebugEntity(Key key, string entity) {
        if (!GameManager.Instance.localPlayer)
            return;

        if (Keyboard.current[key].wasPressedThisFrame)
            PhotonNetwork.Instantiate("Prefabs/Enemy/" + entity, GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().facingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0), Quaternion.identity);
    }
}