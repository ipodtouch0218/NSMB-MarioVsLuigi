using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

public class UserNametag : MonoBehaviour {

    public TMP_Text usernameText, starText;
    private PlayerController parent;

    void Start() {
        parent = GetComponentInParent<PlayerController>();

        gameObject.SetActive(!parent.photonView.IsMine);
    }
    
    void Update() {
        float y;
        switch (parent.state) {
        case PlayerController.PlayerState.Small:
            y = 1.2f;
            break;
        default:
            y = 1.6f;
            break;
        }

        transform.localPosition = new Vector2(0, y);

        usernameText.text = (parent.photonView.Owner.IsMasterClient ? "(H) " : "") + parent.photonView.Owner.NickName;
        starText.text = "<sprite=0>" + parent.stars;
    }
}
