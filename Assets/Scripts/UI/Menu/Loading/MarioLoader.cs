using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

namespace NSMB.Loading {
    public class MarioLoader : MonoBehaviour {

        private static readonly Vector3 two = Vector3.one * 2;

        //---Serailzied Variables
        [SerializeField] private float blinkSpeed = 0.5f;
        [SerializeField] private Image image;

        //---Public Variables
        public float scaleTimer;
        public int scale = 0, previousScale;

        //---Private Variables
        private CharacterData data;

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
                transform.localScale = two;
                image.sprite = data.loadingBigSprite;
            }
        }

        public void Initialize() {
            data = NetworkHandler.Instance.runner.GetLocalPlayerData().GetCharacterData();
            scaleTimer = 0;
            scale = 0;
            previousScale = 0;
            image.sprite = data.loadingSmallSprite;
        }
    }
}
