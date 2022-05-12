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
    public float pingSample = 0;

    private Material timerMaterial;

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

        if (pingSample == float.NaN)
            pingSample = 0;
        
        uiDebug.text = $"<mark=#000000b0 padding=\"20, 20, 20, 20\"><font=\"defaultFont\">Ping: {(int) pingSample}ms</font>";
        
        //Player stuff update.
        if (GameManager.Instance.localPlayer) { 
            player = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        } else if (GameManager.Instance.SpectationManager.Spectating) {
            player = GameManager.Instance.SpectationManager.TargetPlayer;
        }

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
        if (!player || GameManager.Instance.gameover)
            return;

        uiStars.text = Utils.GetSymbolString($"Sx{player.stars}/{GameManager.Instance.starRequirement}");
        uiCoins.text = Utils.GetSymbolString($"Cx{player.coins}/8");
        
        if (player.lives < 0) {
            uiLives.gameObject.SetActive(false);
        } else {
            uiLives.gameObject.SetActive(true);
            uiLives.text = Utils.GetCharacterData(player.photonView.Owner).uistring + Utils.GetSymbolString("x" + player.lives);
        }

        if (GameManager.Instance.timedGameDuration > 0) {
            int seconds = (GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp) / 1000;
            seconds = Mathf.Clamp(seconds, 0, GameManager.Instance.timedGameDuration);
            uiCountdown.text = Utils.GetSymbolString($"cx{seconds / 60}:{seconds % 60:00}");
            uiCountdown.gameObject.SetActive(true);
            if (seconds == 0) {
                if (timerMaterial == null)
                    timerMaterial = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>().GetMaterial();

                float partialSeconds = (GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp) / 1000f % 2f;
                byte gb = (byte) (Mathf.PingPong(partialSeconds, 1) * 255);
                timerMaterial.SetColor("_Color", new Color32(255, gb, gb, 255));
            }
        } else {
            uiCountdown.gameObject.SetActive(false);
        }
    }

}
