using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class UIUpdater : MonoBehaviour {
    
    public static UIUpdater Instance;
    public GameObject playerTrackTemplate, starTrackTemplate;
    PlayerController player;
    //TODO: refactor
    public Sprite storedItemNull, storedItemMushroom, storedItemFireFlower, storedItemMiniMushroom, storedItemMegaMushroom, storedItemBlueShell, storedItemPropellerMushroom, storedItemIceFlower; 
    public TMP_Text uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    public Image itemReserve;
    public float pingSample = 0, fpsSample = 60;

    void Start() {
        Instance = this;
        pingSample = PhotonNetwork.GetPing();
    }
    
    public GameObject CreatePlayerIcon(PlayerController player) {
        GameObject trackObject = Instantiate(playerTrackTemplate, playerTrackTemplate.transform.position, Quaternion.identity, transform);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = player.gameObject;

        trackObject.SetActive(true);

        return trackObject;
    }

    void Update() {
        pingSample = Mathf.Lerp(pingSample, PhotonNetwork.GetPing(), Time.unscaledDeltaTime * 0.5f);
        fpsSample = Mathf.Lerp(fpsSample, 1.0f / Time.unscaledDeltaTime, Time.unscaledDeltaTime * 0.5f);

        if (pingSample == float.NaN)
            pingSample = 0;
        if (fpsSample == float.NaN)
            fpsSample = 60;
        
        uiDebug.text = $"<mark=#000000b0 padding=\"20, 20, 20, 20\"><font=\"defaultFont\">{Mathf.RoundToInt(fpsSample)}FPS | Ping: {(int) pingSample}ms</font>";
        
        //Player stuff update.
        if (!player && GameManager.Instance.localPlayer)
            player = GameManager.Instance.localPlayer.GetComponent<PlayerController>();

        UpdateStoredItemUI();
        UpdateTextUI();
    }
    
    void UpdateStoredItemUI() {
        if (!player)
            return;

        //TODO: refactor
        if (player.storedPowerup) {
            itemReserve.sprite = player.storedPowerup.state switch {
                Enums.PowerupState.MiniMushroom => storedItemMiniMushroom,
                //Enums.powerupstae.Small => null
                Enums.PowerupState.Large => storedItemMushroom,
                Enums.PowerupState.FireFlower => storedItemFireFlower,
                Enums.PowerupState.MegaMushroom => storedItemMegaMushroom,
                Enums.PowerupState.BlueShell => storedItemBlueShell,
                Enums.PowerupState.PropellerMushroom => storedItemPropellerMushroom,
                Enums.PowerupState.IceFlower => storedItemIceFlower,
                _ => storedItemNull,
            };
        } else {
            itemReserve.sprite = storedItemNull;
        }
    }
    void UpdateTextUI() {
        if (!player)
            return;

        uiStars.text = "<sprite=0>" + player.stars + "/" + GameManager.Instance.starRequirement;
        uiCoins.text = "<sprite=1>" + player.coins + "/8";
        uiLives.text = player.lives > 0 ? (Utils.GetCharacterIndex(player.photonView.Owner) == 0 ? "<sprite=3>" : "<sprite=4>") + player.lives : "";
        uiCountdown.text = GameManager.Instance.endServerTime != -1 ? "<sprite=6>" + ((GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp) / 1000).ToString("0s") : "";
    }
}
