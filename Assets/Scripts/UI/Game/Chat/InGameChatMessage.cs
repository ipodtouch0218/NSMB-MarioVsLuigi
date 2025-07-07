using JimmysUnityUtilities;
using NSMB.Chat;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using System;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Game.Chat {
    public class InGameChatMessage : MonoBehaviour {

        //---Events
        public event Action<InGameChatMessage> OnChatMessageDestroyed;

        //---Serialized Variables
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float lifetime = 5f, fadeTime = 1f, moveTime = 0.25f, maxPosition = 300;
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private float position, moveVelocity;
        private ChatMessageData data;

        public void OnValidate() {
            this.SetIfNull(ref rectTransform);
            this.SetIfNull(ref group, UnityExtensions.GetComponentType.Parent);
            this.SetIfNull(ref text, UnityExtensions.GetComponentType.Children);
        }

        public void Initialize(ChatMessageData data) {
            this.data = data;

            if (data.isSystemMessage) {
                TranslationManager.OnLanguageChanged += OnLanguageChanged; ;
                OnLanguageChanged(GlobalController.Instance.translationManager);
            } else {
                text.richText = false;
                text.text = data.message;
            }
            // text.color = data.color;

            group.alpha = 1;
            Destroy(gameObject, lifetime);
        }

        public void OnDestroy() {
            OnChatMessageDestroyed?.Invoke(this);
        }

        public void Update() {
            lifetime -= Time.deltaTime;

            // Move to position
            rectTransform.SetAnchoredPositionY(Mathf.SmoothDamp(rectTransform.anchoredPosition.y, position, ref moveVelocity, moveTime));

            // And fade over time
            if (lifetime < fadeTime) {
                group.alpha = Mathf.Clamp01(lifetime / fadeTime);
            }
        }

        public void AdjustPosition(float y) {
            position += y;
            if (position >= maxPosition && lifetime > fadeTime) {
                lifetime = fadeTime;
            }
        }

        private void OnLanguageChanged(TranslationManager tm) {
            text.text = tm.GetTranslationWithReplacements(data.message, data.replacements);
            text.horizontalAlignment = tm.RightToLeft ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        }
    }
}