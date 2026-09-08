using NSMB.Chat;
using NSMB.UI.MainMenu.Submenus.InRoom;
using NSMB.UI.Translation;
using Quantum;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Elements {
    public class ContextMenuMuteButton : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private string muteTranslationKey, unmuteTranslationKey;
        [SerializeField] private TMP_Text text;
        [SerializeField] private PlayerListEntry parent;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public unsafe void UpdateLabel() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            RuntimePlayer runtimePlayer = f.GetPlayerData(parent.player);
            bool muted = ChatManager.Instance.mutedPlayers.Contains(runtimePlayer.UserId);
            text.text = GlobalController.Instance.translationManager.GetTranslation(muted ? unmuteTranslationKey : muteTranslationKey);
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateLabel();
        }
    }
}
