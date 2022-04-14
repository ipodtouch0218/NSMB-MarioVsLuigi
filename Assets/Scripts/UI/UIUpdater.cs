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

        if (!player.photonView.IsMine)
            trackObject.transform.localScale = new Vector3(2f / 3f, 2f / 3f, 1f);

        trackObject.SetActive(true);

        return trackObject;
    }

    void Update() {
        pingSample = Mathf.Lerp(pingSample, PhotonNetwork.GetPing(), Time.unscaledDeltaTime * 0.5f);
        fpsSample = Mathf.Lerp(fpsSample, 1.0f / Time.unscaledDeltaTime, Time.unscaledDeltaTime * 0.5f);
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
        itemReserve.sprite = player.storedPowerup switch {
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
    }
    void UpdateTextUI() {
        if (!player)
            return;

        uiStars.text = "<sprite=0>" + player.stars + "/" + GameManager.Instance.starRequirement;
        uiCoins.text = "<sprite=1>" + player.coins + "/8";
        uiLives.text = player.lives > 0 ? (Utils.GetCharacterIndex(player.photonView.Owner) == 0 ? "<sprite=3>" : "<sprite=4>") + player.lives : "";
        uiCountdown.text = GameManager.Instance.timeRemaining > -0.9 ? "<sprite=6>" + GameManager.Instance.timeRemaining.ToString("0s") : "";
    }
}
