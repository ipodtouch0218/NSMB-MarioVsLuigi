using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using NSMB.Utils;

public class UIUpdater : MonoBehaviour {

    public static UIUpdater Instance;
    public GameObject playerTrackTemplate, starTrackTemplate;
    public PlayerController player;
    public Sprite storedItemNull;
    public TMP_Text uiStars, uiCoins, uiDebug, uiLives, uiCountdown;
    public Image itemReserve, itemColor;
    public float pingSample = 0;

    private Material timerMaterial;
    private GameObject starsParent, coinsParent, livesParent, timerParent;
    private readonly List<Image> backgrounds = new();
    private bool uiHidden;

    public void Start() {
        Instance = this;
        pingSample = PhotonNetwork.GetPing();

        starsParent = uiStars.transform.parent.gameObject;
        coinsParent = uiCoins.transform.parent.gameObject;
        livesParent = uiLives.transform.parent.gameObject;
        timerParent = uiCountdown.transform.parent.gameObject;

        backgrounds.Add(starsParent.GetComponentInChildren<Image>());
        backgrounds.Add(coinsParent.GetComponentInChildren<Image>());
        backgrounds.Add(livesParent.GetComponentInChildren<Image>());
        backgrounds.Add(timerParent.GetComponentInChildren<Image>());

        foreach (Image bg in backgrounds)
            bg.color = GameManager.Instance.levelUIColor;
        itemColor.color = new(GameManager.Instance.levelUIColor.r - 0.2f, GameManager.Instance.levelUIColor.g - 0.2f, GameManager.Instance.levelUIColor.b - 0.2f, GameManager.Instance.levelUIColor.a);
    }

    public void Update() {
        pingSample = Mathf.Lerp(pingSample, PhotonNetwork.GetPing(), Time.unscaledDeltaTime * 0.5f);
        if (pingSample == float.NaN)
            pingSample = 0;

        uiDebug.text = $"<mark=#000000b0 padding=\"20, 20, 20, 20\"><font=\"defaultFont\">Ping: {(int) pingSample}ms</font>";

        //Player stuff update.
        if (!player && GameManager.Instance.localPlayer) {
            player = GameManager.Instance.localPlayer.GetComponent<PlayerController>();
        }
        if (!player) {
            if (!uiHidden)
                ToggleUI(true);
            return;
        }
        if (uiHidden)
            ToggleUI(false);

        UpdateStoredItemUI();
        UpdateTextUI();
    }

    private void ToggleUI(bool hidden) {
        uiHidden = hidden;

        starsParent.SetActive(!hidden);
        livesParent.SetActive(!hidden);
        coinsParent.SetActive(!hidden);
        timerParent.SetActive(!hidden);
    }

    private void UpdateStoredItemUI() {
        if (!player)
            return;

        itemReserve.sprite = player.storedPowerup?.reserveSprite ?? storedItemNull;
    }

    private void UpdateTextUI() {
        if (!player || GameManager.Instance.gameover)
            return;

        uiStars.text = Utils.GetSymbolString($"Sx{player.stars}/{GameManager.Instance.starRequirement}");
        uiCoins.text = Utils.GetSymbolString($"Cx{player.coins}/{GameManager.Instance.coinRequirement}");

        if (player.lives < 0) {
            livesParent.SetActive(false);
        } else {
            livesParent.SetActive(true);
            uiLives.text = Utils.GetCharacterData(player.photonView.Owner).uistring + Utils.GetSymbolString("x" + player.lives);
        }

        if (GameManager.Instance.timedGameDuration > 0) {
            int seconds = (GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp) / 1000;
            seconds = Mathf.Clamp(seconds, 0, GameManager.Instance.timedGameDuration);
            uiCountdown.text = Utils.GetSymbolString($"cx{seconds / 60}:{seconds % 60:00}");
            timerParent.SetActive(true);
            if (GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp < 0) {
                if (timerMaterial == null)
                    timerMaterial = uiCountdown.transform.GetChild(0).GetComponent<CanvasRenderer>().GetMaterial();

                float partialSeconds = (GameManager.Instance.endServerTime - PhotonNetwork.ServerTimestamp) / 1000f % 2f;
                byte gb = (byte) (Mathf.PingPong(partialSeconds, 1f) * 255);
                timerMaterial.SetColor("_Color", new Color32(255, gb, gb, 255));
            }
        } else {
            timerParent.SetActive(false);
        }
    }

    public GameObject CreatePlayerIcon(PlayerController player) {
        GameObject trackObject = Instantiate(playerTrackTemplate, playerTrackTemplate.transform.parent);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = player.gameObject;

        trackObject.SetActive(true);

        return trackObject;
    }
}
