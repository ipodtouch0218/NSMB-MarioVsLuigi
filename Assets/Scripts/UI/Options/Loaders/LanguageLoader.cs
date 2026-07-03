using System.Collections.Generic;
using System.Linq;

namespace NSMB.UI.Options.Loaders {
    public class LanguageLoader : PauseOptionLoader {

        //---Private Variables
        private List<string> locales;

        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }

            var tm = GlobalController.Instance.translationManager;

            spo.options.Clear();
            locales = tm.GetAllLocales().ToList();
            locales.Sort();

            spo.options.AddRange(locales.Select(locale => {
                if (tm.TryGetTranslationForLocale(locale, "lang", out string name)) {
                    return name;
                } else {
                    return $"'lang' missing for '{locale}'";
                }
            }));

            int currentIndex = locales.IndexOf(tm.CurrentLocale);
            spo.SetValue(currentIndex);
        }

        public override void OnValueChanged(PauseOption option, object newValue) {
            if (option is not ScrollablePauseOption spo) {
                return;
            }
            
            GlobalController.Instance.translationManager.ChangeLanguage(locales[spo.value]);
            option.manager.RequireReconnect |= option.requireReconnect;
        }
    }
}
