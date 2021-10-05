using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Photon.Pun;
using TMPro;

public class UIUpdater : MonoBehaviour {
    
    PlayerController player;
    public Sprite storedItemNull, storedItemMushroom, storedItemFireFlower, storedItemMiniMushroom, storedItemMegaMushroom, storedItemBlueShell; 
    public TMP_Text uiStars, uiCoins, uiPing;
    public Image itemReserve;

    void Update() {
        uiPing.text = "<sprite=2>" + PhotonNetwork.GetPing() + "ms";
        
        //Player stuff update.
        if (!player && GameManager.Instance.localPlayer) {
            player = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        }
        if (!player) 
            return;
        
        uiStars.text = "<sprite=0>" + player.stars + "/" + GlobalController.Instance.starRequirement;
        uiCoins.text = "<sprite=1>" + player.coins + "/8";

        switch (player.storedPowerup) {
        case "Mushroom":
            itemReserve.sprite = storedItemMushroom;
            break;
        case "FireFlower":
            itemReserve.sprite = storedItemFireFlower;
            break;
        case "MiniMushroom":
            itemReserve.sprite = storedItemMiniMushroom;
            break;
        case "MegaMushroom":
            itemReserve.sprite = storedItemMegaMushroom;
            break;
        case "BlueShell":
            itemReserve.sprite = storedItemBlueShell;
            break;
        default:
            itemReserve.sprite = storedItemNull;
            break;
        }
    }
    
}
