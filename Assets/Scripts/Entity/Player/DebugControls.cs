using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class DebugControls : MonoBehaviour {

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
            Debug.Log("new timescale = " + Time.timeScale);
        }
        if (kb[Key.RightBracket].wasPressedThisFrame) {
            Time.timeScale *= 2;
            Debug.Log("new timescale = " + Time.timeScale);
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
        DebugEntity(Key.Digit1, "Koopa");
        DebugEntity(Key.Digit2, "RedKoopa");
        DebugEntity(Key.Digit3, "BlueKoopa");
        DebugEntity(Key.Digit4, "Goomba");
        DebugEntity(Key.Digit5, "Bobomb");
        DebugEntity(Key.Digit6, "BulletBill");
        DebugEntity(Key.Digit7, "Spiny");
        DebugItem(Key.Digit8, "IceFlower");
        FreezePlayer(Key.Digit9);
    }

    private void FreezePlayer(Key key) {
        PlayerController p = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        if (Keyboard.current[key].wasPressedThisFrame && (!p.frozen && !p.FrozenObject && p.state != Enums.PowerupState.Giant && !p.pipeEntering && !p.knockback && p.hitInvincibilityCounter <= 0)) {
            
            GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", p.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
            frozenBlock.gameObject.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, "Player", p.photonView.ViewID);
        }
    }
    private void DebugItem(Key key, string item) {
        if (Keyboard.current[key].wasPressedThisFrame)
            GameManager.Instance.localPlayer.GetComponent<PlayerController>().SpawnItem(item);
    }
    private void DebugEntity(Key key, string entity) {
        if (Keyboard.current[key].wasPressedThisFrame)
            PhotonNetwork.Instantiate("Prefabs/Enemy/" + entity, GameManager.Instance.localPlayer.transform.position + (GameManager.Instance.localPlayer.GetComponent<PlayerController>().facingRight ? Vector3.right : Vector3.left), Quaternion.identity);
    }

}