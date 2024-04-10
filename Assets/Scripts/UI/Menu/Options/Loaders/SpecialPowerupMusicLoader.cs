using System.Linq;

using NSMB.Translation;
using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {
    public class SpecialPowerupMusicLoader : PauseOptionLoader {

        private static readonly string[] MusicDisplayKeys = {
            "ui.generic.none",
            "ui.options.audio.specialpowerupmusic.starman",
            "ui.options.audio.specialpowerupmusic.megamushroom",
            "ui.options.audio.specialpowerupmusic.both"
        };

        //---Private Variables
        private PauseOption option;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            this.option = option;
            spo.options.Clear();
            spo.options.AddRange(MusicDisplayKeys.Select(GlobalController.Instance.translationManager.GetTranslation));

            int index = (int) Settings.Instance.audioSpecialPowerupMusic;
            spo.SetValue(index, false);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            Settings.Instance.audioSpecialPowerupMusic = (Enums.SpecialPowerupMusic) newValue;
            option.manager.RequireReconnect |= option.requireReconnect;
        }

        private void OnLanguageChanged(TranslationManager tm) {
            if (option) {
                LoadOptions(option);
            }
        }
    }
}
