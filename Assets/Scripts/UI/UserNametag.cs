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
        float y = 1.6f;
        if (parent.state == Enums.PowerupState.Small || parent.inShell || parent.crouching) {
            y = 1.2f;
        }

        transform.localPosition = new Vector2(0, y);

        usernameText.text = (parent.photonView.Owner.IsMasterClient ? "<sprite=5>" : "") + parent.photonView.Owner.NickName;
        starText.text = "<sprite=0>" + parent.stars;
    }
}
