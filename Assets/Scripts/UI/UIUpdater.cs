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

        uiStars.text = GetSymbolString($"Sx{player.stars}/{GameManager.Instance.starRequirement}");
        uiCoins.text = GetSymbolString($"Cx{player.coins}/8");
        
        if (player.lives == -1) {
            uiLives.gameObject.SetActive(false);
        } else {
            uiLives.text = Utils.GetCharacterData(player.photonView.Owner).uistring + GetSymbolString("x" + player.lives);
        }

        if (GameManager.Instance.timedGameDuration != -1) {
            uiCountdown.gameObject.SetActive(false);
        } else {
            int seconds = (GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp) / 1000;
            uiCountdown.text = GetSymbolString($"cx{seconds/60}:{seconds%60:00}");
        }
    }

    private static readonly string charString = "     c     0123456789xCS/:";
    private static string GetSymbolString(string str) {
        string ret = "";
        int index;
        foreach (char c in str) {
            if ((index = charString.IndexOf(c)) != -1) {
                ret += $"<sprite={index}>";
            } else {
                ret += c;
            }
        }
        return ret;
    }
}
