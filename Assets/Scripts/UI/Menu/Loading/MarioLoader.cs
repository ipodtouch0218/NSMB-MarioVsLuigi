using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

namespace NSMB.Loading {
    public class MarioLoader : MonoBehaviour {

        private static readonly Vector3 two = Vector3.one * 2;

        //---Serailzied Variables
        [SerializeField] private float blinkSpeed = 0.5f;
        [SerializeField] private Image image;

        //---Public Properties
        private int _scale;
        public int Scale {
            get => _scale;
            set {
                previousScale = _scale;
                _scale = value;
                if (flashRoutine != null)
                    StopCoroutine(flashRoutine);

                StartCoroutine(DoGrowShrinkFlash());
            }
        }

        //---Public Variables
        public int previousScale;

        //---Private Variables
        private Coroutine flashRoutine;
        private CharacterData data;

        private IEnumerator DoGrowShrinkFlash() {
            float halfBlink = blinkSpeed * 0.5f;
            float scaleTimer = 0.5f;

            while (scaleTimer > 0) {
                scaleTimer -= Time.deltaTime;
                int scaleToDisplay = (scaleTimer % blinkSpeed) < halfBlink ? Scale : previousScale;

                switch (scaleToDisplay) {
                case 0: {
                    transform.localScale = Vector3.one;
                    image.sprite = data.loadingSmallSprite;
                    break;
                }
                case 1: {
                    transform.localScale = Vector3.one;
                    image.sprite = data.loadingBigSprite;
                    break;
                }
                case 2: {
                    transform.localScale = two;
                    image.sprite = data.loadingBigSprite;
                    break;
                }
                }

                yield return null;
            }
            flashRoutine = null;
        }

        public void Initialize() {
            data = NetworkHandler.Runner.GetLocalPlayerData().GetCharacterData();
            Scale = 0;
            previousScale = 0;
            image.sprite = data.loadingSmallSprite;

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
        }
    }
}
