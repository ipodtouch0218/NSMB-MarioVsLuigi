using System;
using System.Linq;

using static NSMB.Translation.TranslationManager;
using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {

    public class LanguageLoader : PauseOptionLoader {

        //---Private Variables
        private LocaleData[] locales;

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            spo.options.Clear();
            locales = GlobalController.Instance.translationManager.GetLocaleData();
            spo.options.AddRange(locales.Select(ld => {
                return ld.Locale.ToLower().StartsWith("ar") ? ArabicSupport.ArabicFixer.Fix(ld.Name, false) : ld.Name;
            }));

            string current = GlobalController.Instance.translationManager.CurrentLocale;
            int currentIndex = Array.IndexOf(locales.Select(ld => ld.Locale).ToArray(), current);
            spo.SetValue(currentIndex);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            GlobalController.Instance.translationManager.ChangeLanguage(locales[spo.value].Locale);
            option.manager.RequireReconnect |= option.requireReconnect;
        }
    }
}
