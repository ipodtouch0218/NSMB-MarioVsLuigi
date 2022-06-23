using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using UnityEngine.Tilemaps;

public class DebugControls : MonoBehaviour {

    public bool editMode;
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
        //DebugWorldEntity(Key.Digit0, "FrozenCube");
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

        if (Keyboard.current[key].wasPressedThisFrame)
            if (item == null)
                GameManager.Instance.localPlayer.GetComponent<PlayerController>().SpawnItem();
            else
                GameManager.Instance.localPlayer.GetComponent<PlayerController>().SpawnItem(item);
    }
    private void DebugEntity(Key key, string entity) {
        if (!GameManager.Instance.localPlayer)
            return;

        if (Keyboard.current[key].wasPressedThisFrame)
            PhotonNetwork.Instantiate("Prefabs/Enemy/" + entity, GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().facingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0), Quaternion.identity);
    }
    private void DebugWorldEntity(Key key, string entity) {
        if (!GameManager.Instance.localPlayer)
            return;

        if (Keyboard.current[key].wasPressedThisFrame) {
            GameObject en = PhotonNetwork.Instantiate("Prefabs/Enemy/Goomba", GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().facingRight ? Vector3.right : Vector3.left) + new Vector3(0, 0.2f, 0), Quaternion.identity);
            GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", en.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
            frozenBlock.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, en.tag, en.GetComponent<KillableEntity>().photonView.ViewID);
        }
    }

    /*
    // The event zone 

    public void specialSettings() {
        if (Keyboard.current[Key.P].wasPressedThisFrame)
            editMode = !editMode;
        Vector3 pos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        if (editMode == true) {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            GameManager.Instance.localPlayer.GetComponent<PlayerController>().photonView.RPC("PlaceTile", RpcTarget.All, pos.x, pos.y, pos.z);

            if (Mouse.current.leftButton.wasPressedThisFrame)
                GameManager.Instance.localPlayer.GetComponent<PlayerController>().photonView.RPC("RemoveTile", RpcTarget.All, pos.x, pos.y, pos.z);
        }
    }
    */
}