using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace NSMB.UI.Translation {

    public class TranslationManager : MonoBehaviour {

        //---Events
        public static event Action<TranslationManager> OnLanguageChanged;

        //---Properties
        public string CurrentLocale { get; private set; }
        public bool RightToLeft => IsLocaleRTL(CurrentLocale);

        //---Serialized Variables
        [SerializeField] private string fallbackLocale = "en-us";

        //---Private Variables
        private readonly Dictionary<string, List<ITranslationSource>> allTranslations = new(StringComparer.InvariantCultureIgnoreCase);
        private bool initialized;

        public void Start() {
            Initialize();
        }

        public void Initialize() {
            if (initialized) {
                return;
            }

            RegisterBuiltinLocales();
            RegisterCustomLocales();
            initialized = true;
        }

        public void Update() {
            if (Keyboard.current[Key.F5].wasPressedThisFrame) {
                Reload();
                GlobalController.Instance.PlaySound(SoundEffect.Player_Sound_PowerupCollect);
            }
        }

        public string GetTranslation(string key) {
            return GetTranslation(key, fixRtl: true);
        }

        public string GetTranslation(string key, bool fixRtl) {
            _ = TryGetTranslation(key, out var result, fixRtl);
            return result;
        }

        public bool TryGetTranslation(string key, out string result, bool fixRtl = true) {
            Initialize();

            if (TryGetTranslationForLocale(CurrentLocale, key, out result, fixRtl)) {
                return true;
            }
            if (TryGetTranslationForLocale(fallbackLocale, key, out result, fixRtl)) {
                return true;
            }
            // Default to returning the key.
            result = key;
            return false;
        }

        public string GetTranslationWithReplacements(string key, params string[] replacements) {
            string translation = GetTranslation(key);
            for (int i = 0; i < replacements.Length - 1; i += 2) {
                translation = translation.Replace("{" + replacements[i] + "}", GetTranslation(replacements[i + 1]));
            }
            return translation;
        }

        public string GetSubTranslations(string text) {
            return Regex.Replace(text, @"{[^{}]+}", match => GetTranslation(match.Value[1..^1]));
        }

        public bool ChangeLanguage(string locale) {
            Initialize();

            locale = locale?.ToLowerInvariant();

            if (!allTranslations.ContainsKey(locale)) {
                Debug.LogWarning($"[Translation] Unknown locale '{locale}' selected... ignoring.");
                return false;
            }

            CurrentLocale = locale;
            Reload();
            OnLanguageChanged?.Invoke(this);
            return true;
        }

        public void Reload() {
            Initialize();

            foreach ((var locale, var sourceList) in allTranslations) {
                foreach (var source in sourceList) {
                    try {
                        source.Reload();
                    } catch (Exception e) {
                        // Something happened to this source.
                        // It's old state still should be loaded, so it's ok...
                        // ...maybe
                        Debug.LogWarning($"[Translation] Failed to reload translation source for locale '{locale}': {source} (priority {source.Priority})");
                        Debug.LogWarning(e);
                    }
                }
            }
        }

        public void RegisterTranslationSource(string locale, ITranslationSource source) {
            if (!allTranslations.TryGetValue(locale, out var sourceList)) {
                allTranslations[locale] = sourceList = new();
            }

            if (sourceList.Contains(source)) {
                return;
            }

            sourceList.Add(source);
            sourceList.Sort();
        }

        public bool UnregisterTranslationSource(string locale, ITranslationSource source) {
            if (!allTranslations.TryGetValue(locale, out var sourceList)) {
                return false;
            }

            return sourceList.Remove(source);
        }

        public bool TryGetTranslationForLocale(string locale, string key, out string result, bool fixRtl = true) {
            key ??= "null";
            key = key.ToLowerInvariant();

            if (allTranslations.TryGetValue(locale, out var sources)) {
                for (int i = sources.Count - 1; i >= 0; i--) {
                    // No foreach, we want backwards iteration- list is ascending sorted by priority.
                    if (sources[i].TryGetTranslation(key, out result)) {
                        if (IsLocaleRTL(locale) && fixRtl) {
                            result = ArabicFixerTool.FixLine(result);
                        }
                        return true;
                    }
                }
            }

            // Default to returning the key
            result = key;
            return false;
        }

        public bool IsLocaleRTL(string locale) {
            if (!allTranslations.TryGetValue(locale, out var sources)) {
                // Default to LTR
                return false;
            }

            // Highest priority source is trusted
            return sources[^1].IsRTL;
        }

        public ICollection<string> GetAllLocales() {
            return allTranslations.Keys;
        }

        public string DateTimeToLocalizedString(DateTime dt, bool shortDisplay, bool dateOnly) {
            dt = dt.ToLocalTime();
            try {
                CultureInfo culture = new(CurrentLocale);
                if (dateOnly) {
                    if (shortDisplay) {
                        return dt.ToString(culture.DateTimeFormat.ShortDatePattern);
                    } else {
                        return dt.ToString(culture.DateTimeFormat.LongDatePattern);
                    }
                } else {
                    return dt.ToString(culture.DateTimeFormat);
                }
            } catch (CultureNotFoundException) {
                if (dateOnly) {
                    if (shortDisplay) {
                        return dt.ToLocalTime().ToShortDateString();
                    } else {
                        return dt.ToLocalTime().ToLongDateString();
                    }
                } else {
                    return dt.ToLocalTime().ToString();
                }
            }
        }

        private void RegisterBuiltinLocales() {
            var textAssets = Resources.LoadAll<TextAsset>("data/lang");

            foreach (var textAsset in textAssets) {
                try {
                    string locale = textAsset.name;
                    var source = new TextAssetJsonTranslationSource(textAsset);
                    source.Priority = -1;
                    RegisterTranslationSource(locale, source);
                } catch (Exception e) {
                    Debug.LogWarning($"[Translation] Failed to load translation from TextAsset {textAsset.name}. Is it malformed?");
                    Debug.LogWarning(e);
                }
            }
        }

        [Conditional("UNITY_STANDALONE")]
        private void RegisterCustomLocales() {
            string[] paths = { $"{Application.streamingAssetsPath}/lang", $"{Application.persistentDataPath}/lang" };
            
            foreach (var folder in paths) {
                if (Directory.Exists(folder)) {
                    foreach (var filePath in Directory.EnumerateFiles(folder, "*.json")) {
                        try {
                            string locale = Path.GetFileNameWithoutExtension(filePath);
                            var source = new FileJsonTranslationSource(filePath);
                            RegisterTranslationSource(locale, source);
                        } catch (Exception e) {
                            Debug.LogWarning($"[Translation] Failed to load translation from file '{filePath}'. Is it malformed?");
                            Debug.LogWarning(e);
                        }
                    }
                }
            }
        }
    }
}
