using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

using Photon.Pun;
using Photon.Realtime;
using NSMB.Utils;

public class PlayerListEntry : MonoBehaviour {

    private static readonly Dictionary<string, string> SPECIAL_PLAYERS = new() {
        ["cf03abdb5d2ef1b6f0d30ae40303936f9ab22f387f8a1072e2849c8292470af1"] = "ipodtouch0218",
        ["d5ba21667a5da00967cc5ebd64c0d648e554fb671637adb3d22a688157d39bf6"] = "mindnomad",
        ["95962949aacdbb42a6123732dabe9c7200ded59d7eeb39c889067bafeebecc72"] = "MPS64",
    };

    public Player player;

    [SerializeField] private TMP_Text nameText, pingText;
    [SerializeField] private Image colorStrip;

    [SerializeField] private RectTransform background, options;
    [SerializeField] private GameObject blockerTemplate, firstButton;

    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private LayoutElement layout;

    [SerializeField] private GameObject[] adminOnlyOptions;

    private GameObject blockerInstance;
    private bool checkedHash;

    public void Update() {
        double time = PhotonNetwork.Time * 0.1;
        time %= 1;
        nameText.color = Color.HSVToRGB((float) time, 1, 1);
    }

    public void UpdateText() {
        colorStrip.color = Utils.GetPlayerColor(player, 1f, 1f);

        if (!checkedHash) {
            checkedHash = true;
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(player.UserId));
            StringBuilder sb = new();
            foreach (byte b in bytes)
                sb.Append(b.ToString("X2"));

            string hash = sb.ToString().ToLower();
            if (SPECIAL_PLAYERS.ContainsKey(hash) && player.NickName == SPECIAL_PLAYERS[hash])
                enabled = true;
        }

        string permissionSymbol = "";
        if (player.IsMasterClient)
            permissionSymbol += "<sprite=5>";

        Utils.GetCustomProperty(Enums.NetPlayerProperties.Status, out bool status, player.CustomProperties);
        if (status)
            permissionSymbol += "<sprite=26>";

        string characterSymbol = Utils.GetCharacterData(player).uistring;
        Utils.GetCustomProperty(Enums.NetPlayerProperties.Ping, out int ping, player.CustomProperties);

        string pingColor;
        if (ping < 0) {
            pingColor = "black";
        } else if (ping < 80) {
            pingColor = "#00b900";
        } else if (ping < 120) {
            pingColor = "orange";
        } else {
            pingColor = "red";
        }

        nameText.text = permissionSymbol + characterSymbol + player.GetUniqueNickname();
        pingText.text = $"<color={pingColor}>{ping}";

        Transform parent = transform.parent;
        int childIndex = 0;
        for (int i = 0; i < parent.childCount; i++) {
            if (parent.GetChild(i) != gameObject)
                continue;

            childIndex = i;
            break;
        }

        layout.layoutPriority = transform.parent.childCount - childIndex;
    }

    public void ShowDropdown() {
        if (blockerInstance)
            Destroy(blockerInstance);

        bool admin = PhotonNetwork.IsMasterClient && !player.IsMasterClient;
        foreach (GameObject option in adminOnlyOptions) {
            option.SetActive(admin);
        }

        Canvas.ForceUpdateCanvases();

        blockerInstance = Instantiate(blockerTemplate, rootCanvas.transform);
        RectTransform blockerTransform = blockerInstance.GetComponent<RectTransform>();
        blockerTransform.offsetMax = blockerTransform.offsetMin = Vector2.zero;
        blockerInstance.SetActive(true);

        background.offsetMin = new(background.offsetMin.x, -options.rect.height);
        options.anchoredPosition = new(options.anchoredPosition.x, -options.rect.height);

        EventSystem.current.SetSelectedGameObject(firstButton);
        MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor.GetClip());
    }

    public void HideDropdown(bool didAction) {
        Destroy(blockerInstance);

        background.offsetMin = new(background.offsetMin.x, 0);
        options.anchoredPosition = new(options.anchoredPosition.x, 0);

        MainMenuManager.Instance.sfx.PlayOneShot((didAction ? Enums.Sounds.UI_Decide : Enums.Sounds.UI_Back).GetClip());
    }

    public void BanPlayer() {
        MainMenuManager.Instance.Ban(player);
        HideDropdown(true);
    }

    public void KickPlayer() {
        MainMenuManager.Instance.Kick(player);
        HideDropdown(true);
    }

    public void MutePlayer() {
        MainMenuManager.Instance.Mute(player);
        HideDropdown(true);
    }

    public void PromotePlayer() {
        MainMenuManager.Instance.Promote(player);
        HideDropdown(true);
    }

    public void CopyPlayerId() {
        TextEditor te = new();
        te.text = player.UserId;
        te.SelectAll();
        te.Copy();
        HideDropdown(true);
    }
}