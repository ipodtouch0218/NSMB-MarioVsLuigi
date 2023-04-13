using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Collections;

namespace NSMB.Translation {

    public class TranslationManager : MonoBehaviour {

        //---Public Events
        public event Action<TranslationManager> OnLanguageChanged;

        //---Properties
        public string CurrentLocale { get; private set; } = "en-us";

        //---Serialized Variables
        [SerializeField] private TextAsset defaultLocale;

        //---Private Variables
        private Dictionary<string, string> translations;
        private Dictionary<string, string> defaultTranslations;
        private TextAsset[] locales;
        private bool instantiated;

        public void Instantiate() {
            // Load default (english, unmodified) translations as a fallback
            defaultTranslations = LoadLocaleFromJson(defaultLocale.text);
            locales = Resources.LoadAll<TextAsset>("Data/lang");

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

            if (IsDesktopPlatform()) {
                // Load the new language file from the filesystem
                string path = Path.Combine(Application.streamingAssetsPath, "lang", newLocale + ".json");
                if (!File.Exists(path))
                    return;

                StreamReader file = File.OpenText(path);
                string json = file.ReadToEnd();
                translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            } else {
                // Load the new language file from the resources (since we can't read from the filesystem)
                translations = LoadLocaleFromJson(Resources.Load<TextAsset>("lang/" + newLocale + ".json").text);
            }

            CurrentLocale = newLocale;
            // Call the change event
            OnLanguageChanged?.Invoke(this);
        }

        public LocaleData[] GetLocaleData() {
            LocaleData[] results;

            if (IsDesktopPlatform()) {
                // Any new language can be added, so we need to check the filesystem
                string[] files = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "lang"), "*.json");
                results = files.Select(path => new LocaleData() {
                    Name = File.ReadAllText(path),
                    Locale = Path.GetFileNameWithoutExtension(path),
                }).ToArray();
            } else {
                // Return only the default languages
                results = locales.Select(ta => new LocaleData() {
                    Name = ta.text,
                    Locale = ta.name,
                }).ToArray();
            }

            // Open the files and get the locale name from the "lang" key
            foreach (LocaleData data in results) {
                string json = data.Name;
                Dictionary<string, string> keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                data.Name = keys["lang"];
            }

            Array.Sort(results, (a, b) => a.Locale.CompareTo(b.Locale));
            return results;
        }

        public string[] GetLocaleCodes() {
            if (IsDesktopPlatform()) {
                // Any new language can be added, so we need to check the filesystem
                string[] files = Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, "lang"), "*.json");
                return files.Select(Path.GetFileNameWithoutExtension).ToArray();
            } else {
                // Return only the default languages
                return locales.Select(ta => ta.name).ToArray();
            }
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
