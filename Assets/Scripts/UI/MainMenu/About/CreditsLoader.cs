using JimmysUnityUtilities;
using NSMB.UI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.About {
    public class CreditsLoader : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;
        [SerializeField] private TextAsset credits;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private float moveAfterSecondsOfInactivity = 10, acceleration = 4, maxMoveSpeed = 4;

        //---Private Variables
        private float lastTouchTime = float.MinValue;
        private float previousPosition;
        private float velocity;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);

            lastTouchTime = Time.time;
            velocity = 0;
            scrollRect.verticalNormalizedPosition = 1;
            previousPosition = scrollRect.verticalNormalizedPosition;
        }

        private void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public void Update() {
            if (previousPosition == scrollRect.verticalNormalizedPosition) {
                // Hasn't moved
                if (Time.time - lastTouchTime > moveAfterSecondsOfInactivity) {
                    // Scroll
                    velocity = Mathf.MoveTowards(velocity, maxMoveSpeed, acceleration * Time.deltaTime);
                    scrollRect.verticalNormalizedPosition += (velocity * Time.deltaTime) / scrollRect.GetRectTransform().rect.height;
                }
            } else {
                // Moved
                lastTouchTime = Time.time;
                velocity = 0;
            }
            previousPosition = scrollRect.verticalNormalizedPosition;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            text.text = tm.GetSubTranslations(credits.text);
        }
    }
}
