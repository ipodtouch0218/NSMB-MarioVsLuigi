using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Newtonsoft.Json;

namespace NSMB.Translation {

    public class TranslationManager : MonoBehaviour {

        //---Events
        public static event Action<TranslationManager> OnLanguageChanged;

        //---Properties
        public string CurrentLocale { get; private set; }
        public bool RightToLeft { get; private set; }

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
                try {
                    // Find the language files from the streaming assets
                    string path = Path.Combine(Application.streamingAssetsPath, "lang", newLocale + ".json");
                    if (File.Exists(path)) {
                        StreamReader file = File.OpenText(path);
                        string json = file.ReadToEnd();
                        translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        foundTranslations = true;
                    }
                } catch { }

                if (!foundTranslations) {
                    try {
                        // Find the language files from the appdata folder
                        string path2 = Path.Combine(Application.persistentDataPath, "lang", newLocale + ".json");
                        if (File.Exists(path2)) {
                            StreamReader file = File.OpenText(path2);
                            string json = file.ReadToEnd();
                            translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                            foundTranslations = true;
                        }

                    } catch { }
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

            if (newLocale.ToLower().StartsWith("ar")) {
                // Arabic. Needs special case.
                foreach (string key in translations.Keys.ToList()) {
                    if (key == "rtl") continue;
                    try {
                        translations[key] = ArabicSupport.ArabicFixer.Fix(translations[key], true);
                    } catch { }
                }
            }

            CurrentLocale = newLocale;
            RightToLeft = GetTranslation("rtl") == "true";
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
                try {
                    // Any new language can be added, so we need to check the filesystem
                    string path = Path.Combine(Application.streamingAssetsPath, "lang");
                    string[] files = Directory.GetFiles(path, "*.json");
                    results.AddRange(
                        files.Select(ld => new LocaleData() {
                            Name = File.ReadAllText(ld),
                            Locale = Path.GetFileNameWithoutExtension(ld).ToLower(),
                        })
                    );
                } catch { }

                try {
                    string path2 = Path.Combine(Application.persistentDataPath, "lang");
                    string[] files = Directory.GetFiles(path2, "*.json");
                    results.AddRange(
                        files.Select(ld => new LocaleData() {
                            Name = File.ReadAllText(ld),
                            Locale = Path.GetFileNameWithoutExtension(ld).ToLower(),
                        })
                    );
                } catch { }
            }

            // Open the files and get the locale name from the "lang" key
            foreach (LocaleData data in results) {
                string json = data.Name;
                Dictionary<string, string> keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                data.Name = keys["lang"];
            }

            return results
                .Distinct()
                .OrderBy(ld => ld.Locale)
                .ToArray();
        }

        public string[] GetLocaleCodes() {
            return GetLocaleData().Select(ld => ld.Locale).ToArray();
        }

        public bool TryGetTranslation(string key, out string translated) {
            key ??= "null";

            if (translations != null && translations.TryGetValue(key.ToLower(), out string value) && !string.IsNullOrWhiteSpace(value)) {
                translated = value;
                return true;
            }

            if (defaultTranslations != null && defaultTranslations.TryGetValue(key.ToLower(), out string valueDef) && !string.IsNullOrWhiteSpace(valueDef)) {
                translated = valueDef;
                return true;
            }

            translated = default;
            return false;
        }

        public string GetTranslation(string key) {
            key ??= "null";

            if (translations != null && translations.TryGetValue(key.ToLower(), out string value) && !string.IsNullOrWhiteSpace(value))
                return value;

            if (defaultTranslations != null && defaultTranslations.TryGetValue(key.ToLower(), out string valueDef) && !string.IsNullOrWhiteSpace(valueDef))
                return valueDef;

            return key;
        }

        public string GetTranslationWithReplacements(string key, params string[] replacements) {
            string translation = GetTranslation(key);
            for (int i = 0; i < replacements.Length - 1; i += 2) {
                translation = translation.Replace("{" + replacements[i] + "}", GetTranslation(replacements[i + 1]));
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

        public class LocaleData : IEquatable<LocaleData> {
            public string Name, Locale;

            public bool Equals(LocaleData other) {
                return Locale == other.Locale;
            }

            public override bool Equals(object obj) {
                if (obj == null || GetType() != obj.GetType()) {
                    return false;
                }

                return Locale == ((LocaleData) obj).Locale;
            }

            public override int GetHashCode() {
                return Locale.GetHashCode();
            }
        }
    }
}
