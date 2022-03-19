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
    public Sprite storedItemNull, storedItemMushroom, storedItemFireFlower, storedItemMiniMushroom, storedItemMegaMushroom, storedItemBlueShell; 
    public TMP_Text uiStars, uiCoins, uiPing;
    public Image itemReserve;
    private float pingSample = 0;

    void Start() {
        Instance = this;
        pingSample = PhotonNetwork.GetPing();
    }
    
    public void GivePlayersIcons() {
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player")) {
            GameObject trackObject = GameObject.Instantiate(playerTrackTemplate, playerTrackTemplate.transform.position, Quaternion.identity, transform);
            TrackIcon icon = trackObject.GetComponent<TrackIcon>();
            icon.target = player.gameObject;

            if (!player.GetPhotonView().IsMine)
                trackObject.transform.localScale = new Vector3(2f/3f, 2f/3f, 1f);

            trackObject.SetActive(true);
        }
    }

    void Update() {
        pingSample = PhotonNetwork.GetPing() * Time.deltaTime + (1-Time.deltaTime) * pingSample;
        uiPing.text = "<sprite=2>" + (int) pingSample + "ms";
        
        //Player stuff update.
        if (!player && GameManager.Instance.localPlayer)
            player = GameManager.Instance.localPlayer.GetComponent<PlayerController>();

        UpdateStoredItemUI();
        UpdateTextUI();
    }
    
    void UpdateStoredItemUI() {
        if (!player)
            return;

        itemReserve.sprite = player.storedPowerup switch {
            "Mushroom" => storedItemMushroom,
            "FireFlower" => storedItemFireFlower,
            "MiniMushroom" => storedItemMiniMushroom,
            "MegaMushroom" => storedItemMegaMushroom,
            "BlueShell" => storedItemBlueShell,
            _ => storedItemNull,
        };
    }
    void UpdateTextUI() {
        if (!player)
            return;

        uiStars.text = "<sprite=0>" + player.stars + "/" + GameManager.Instance.starRequirement;
        uiCoins.text = "<sprite=1>" + player.coins + "/8";
    }
}
