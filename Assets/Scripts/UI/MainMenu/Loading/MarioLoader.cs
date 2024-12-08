using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
                if (flashRoutine != null) {
                    StopCoroutine(flashRoutine);
                    flashRoutine = null;
                }

                if (isActiveAndEnabled) {
                    StartCoroutine(DoGrowShrinkFlash());
                }
            }
        }

        //---Public Variables
        public int previousScale;

        //---Private Variables
        private Coroutine flashRoutine;
        private CharacterAsset character;

        private IEnumerator DoGrowShrinkFlash() {
            float halfBlink = blinkSpeed * 0.5f;
            float scaleTimer = 0.5f;

            while (scaleTimer > 0) {
                scaleTimer -= Time.deltaTime;
                int scaleToDisplay = (scaleTimer % blinkSpeed) < halfBlink ? Scale : previousScale;

                switch (scaleToDisplay) {
                case 0: {
                    transform.localScale = Vector3.one;
                    image.sprite = character.LoadingSmallSprite;
                    break;
                }
                case 1: {
                    transform.localScale = Vector3.one;
                    image.sprite = character.LoadingLargeSprite;
                    break;
                }
                case 2: {
                    transform.localScale = two;
                    image.sprite = character.LoadingLargeSprite;
                    break;
                }
                }

                yield return null;
            }
            flashRoutine = null;
        }

        public void Initialize(CharacterAsset character) {
            this.character = character;
            Scale = 0;
            previousScale = 0;
            image.sprite = character.LoadingSmallSprite;

            if (flashRoutine != null) {
                StopCoroutine(flashRoutine);
            }
        }
    }
}
