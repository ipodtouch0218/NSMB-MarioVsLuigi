using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

public class MarioLoader : MonoBehaviour {

    //---Serailzied Variables
    [SerializeField] private float blinkSpeed = 0.5f;

    //---Public Variables
    public float scaleTimer;
    public int scale = 0, previousScale;

    //---Components
    private Image image;

    //---Private Variables
    private CharacterData data;

    public void Start() {
        image = GetComponent<Image>();
        data = NetworkHandler.Instance.runner.GetLocalPlayerData().GetCharacterData();
    }

    public void Update() {
        int scaleDisplay = scale;

        if ((scaleTimer += Time.deltaTime) < 0.5f) {
            if (scaleTimer % blinkSpeed < blinkSpeed / 2f)
                scaleDisplay = previousScale;
        } else {
            previousScale = scale;
        }

        if (scaleDisplay == 0) {
            transform.localScale = Vector3.one;
            image.sprite = data.loadingSmallSprite;
        } else if (scaleDisplay == 1) {
            transform.localScale = Vector3.one;
            image.sprite = data.loadingBigSprite;
        } else {
            transform.localScale = Vector3.one * 2;
            image.sprite = data.loadingBigSprite;
        }
    }
}
