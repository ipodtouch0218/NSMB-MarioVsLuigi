using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Newtonsoft.Json;

namespace NSMB.Translation {

    public class TranslationManager : MonoBehaviour {

        //---Public Events
        public event Action<TranslationManager> OnLanguageChanged;

        //---Properties
        public string CurrentLocale { get; private set; }

        //---Serialized Variables
        [SerializeField] private TextAsset defaultLocale;

        //---Private Variables
        private Dictionary<string, string> translations;
        private Dictionary<string, string> defaultTranslations;
        private TextAsset[] defaultLocales;
        private bool instantiated;

        public void Instantiate() {
            // Load default (english, unmodified) translations as a fallback
            defaultTranslations = LoadLocaleFromJson(defaultLocale.text);
            defaultLocales = Resources.LoadAll<TextAsset>("Data/lang");

            /*
            // (NON-WEBGL / NON-MOBILE) Copy all languages from assets to streaming assets
            if (IsDesktopPlatform()) {
                Directory.CreateDirectory(Application.streamingAssetsPath + "/lang");
                foreach (TextAsset locale in locales) {
                    string path = Path.Combine(Application.streamingAssetsPath, "lang", locale.name + ".json");
                    if (!File.Exists(path)) {
                        File.WriteAllText(path, locale.text);
                    }
                }
            }
            */
            instantiated = true;
        }

        public void ChangeLanguage(string newLocale) {
            if (!instantiated)
                Instantiate();

            if (string.IsNullOrEmpty(newLocale))
                return;

            newLocale = newLocale.ToLower();
            if (CurrentLocale == newLocale)
                return;

            bool foundTranslations = false;

            if (IsDesktopPlatform()) {
                // Find the language file from the filesystem
                string path = Path.Combine(Application.streamingAssetsPath, "lang", newLocale + ".json");
                if (File.Exists(path)) {
                    StreamReader file = File.OpenText(path);
                    string json = file.ReadToEnd();
                    translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    foundTranslations = true;
                }
            }

            if (!foundTranslations) {
                // Load the new language file from the resources (since we can't read from the filesystem)

                foreach (TextAsset locale in defaultLocales) {
                    if (locale.name == newLocale) {
                        translations = LoadLocaleFromJson(locale.text);
                        foundTranslations = true;
                        break;
                    }
                }
            }

            if (!foundTranslations) {
                Debug.Log($"Couldn't find language data in both Resources/Data/lang/{newLocale}");
                return;
            }

            CurrentLocale = newLocale;
            // Call the change event
            OnLanguageChanged?.Invoke(this);
        }

        public LocaleData[] GetLocaleData() {
            List<LocaleData> results = new();

            // Add the default languages
            results.AddRange(
                defaultLocales.Select(ta => new LocaleData() {
                    Name = ta.text,
                    Locale = ta.name,
                })
            );

            if (IsDesktopPlatform()) {
                // Any new language can be added, so we need to check the filesystem
                string[] files = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "lang"), "*.json");
                results.AddRange(
                    files.Select(path => new LocaleData() {
                        Name = File.ReadAllText(path),
                        Locale = Path.GetFileNameWithoutExtension(path),
                    })
                );
            }

            // Open the files and get the locale name from the "lang" key
            foreach (LocaleData data in results) {
                string json = data.Name;
                Dictionary<string, string> keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                data.Name = keys["lang"];
            }

            results.Sort((a, b) => a.Locale.CompareTo(b.Locale));
            return results.ToArray();
        }

        public string[] GetLocaleCodes() {
            return GetLocaleData().Select(ld => ld.Locale).ToArray();
        }

        public string GetTranslation(string key) {
            key = key?.ToLower();

            if (translations != null && translations.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
                return value;

            if (defaultTranslations != null && defaultTranslations.TryGetValue(key, out string valueDef) && !string.IsNullOrWhiteSpace(valueDef))
                return valueDef;

            return key;
        }

        public string GetTranslationWithReplacements(string key, params string[] replacements) {
            string translation = GetTranslation(key);
            for (int i = 0; i < replacements.Length - 1; i += 2) {
                translation = translation.Replace("{" + replacements[i] + "}", replacements[i + 1]);
            }
            return translation;
        }

        public string GetSubTranslations(string text) {
            return Regex.Replace(text, "{[a-zA-Z0-9.]+}", match => GetTranslation(match.Value[1..^1]));
        }

        private Dictionary<string, string> LoadLocaleFromJson(string json) {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }

        private bool IsDesktopPlatform() {
#if PLATFORM_WEBGL
            return false;
#else
            return !Application.isMobilePlatform && !Application.isConsolePlatform;
#endif
        }

        public class LocaleData {
            public string Name, Locale;
        }
    }
}
