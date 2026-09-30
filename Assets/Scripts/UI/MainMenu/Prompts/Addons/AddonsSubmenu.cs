using NSMB.Addons;
using NSMB.UI.Translation;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class AddonsSubmenu : PromptSubmenu {

        //---Static Variables
        private static readonly ScannedPath ParentFolder = new() {
            Name = "..",
            Type = ScannedPath.AddonType.Folder,
        };

        //---Serialized Variables
        [SerializeField] public GameObject loadingGraphic;
        [SerializeField] public ScrollRect scrollRect;
        [SerializeField] private bool hideNonAddons;
        [SerializeField] private AddonFileSystemEntry template;
        [SerializeField] private TMP_Text folderLabel, selectedAddonText, loadedAddonsText;
        [SerializeField] private RawImage selectedAddonIcon;

        //---Private Variables
        private List<AddonFileSystemEntry> entries = new();
        private string currentPath = "", currentRelativePath = "";
#if UNITY_STANDALONE
        private FileSystemWatcher watcher;
#endif

        public override void Initialize() {
            base.Initialize();
        }

        public override void Show(bool first) {
            base.Show(first);

            if (first) {
                currentRelativePath = "";
                UpdateLoadedAddonsText();
                TranslationManager.OnLanguageChanged += OnLanguageChanged;
                AddonManager.OnAddonLoaded += OnAddonLoaded;
                AddonManager.OnAddonUnloaded += OnAddonUnloaded;

#if UNITY_STANDALONE
                watcher = new();
                if (hideNonAddons) {
                    watcher.Filter = "*.mvladdon";
                }
                //watcher.Changed += (_, _) => _ = OpenFolder(currentRelativePath);
                //watcher.Created += (_, _) => _ = OpenFolder(currentRelativePath);
                //watcher.Deleted += (_, _) => _ = OpenFolder(currentPath);
#endif
                _ = OpenFolder(".");
            }
        }

        public override void Hide(SubmenuHideReason hideReason) {
            base.Hide(hideReason);
            if (hideReason == SubmenuHideReason.Closed) {
                foreach (var entry in entries) {
                    Destroy(entry.gameObject);
                }
                entries.Clear();
#if UNITY_STANDALONE
                watcher?.Dispose();
                watcher = null;
#endif

                TranslationManager.OnLanguageChanged -= OnLanguageChanged;
                AddonManager.OnAddonLoaded -= OnAddonLoaded;
                AddonManager.OnAddonUnloaded -= OnAddonUnloaded;
            }
        }
        
        public void UpdateLoadedAddonsText() {
            int addons = GlobalController.Instance.addonManager.LoadedAddons.Count;
            if (addons == 0) {
                loadedAddonsText.text = GlobalController.Instance.translationManager.GetTranslation("ui.addons.manage.notenabled");
            } else {
                loadedAddonsText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.addons.manage.enabled", "addons", addons.ToString());
            }
        }

        public async Awaitable OpenFolder(string newPath) {
            await Awaitable.MainThreadAsync();
            loadingGraphic.SetActive(true);
            List<ScannedPath> results = new();
            string previousPath = currentPath;

            try {
                string fullNewPath = new DirectoryInfo($"{AddonManager.LocalFolderPath}/{currentRelativePath}/{newPath}").FullName;
                currentPath = fullNewPath;
                currentRelativePath = Path.GetRelativePath(AddonManager.LocalFolderPath, fullNewPath).Replace(@"\", "/");
                if (currentRelativePath == ".") {
                    folderLabel.text = Path.GetFileName(AddonManager.LocalFolderPath) + "/";
                } else {
                    folderLabel.text = Path.GetFileName(AddonManager.LocalFolderPath) + "/" + currentRelativePath + "/";
                }

                if (Directory.Exists(fullNewPath)) {
                    // Prepare paths in background thread
                    await Awaitable.BackgroundThreadAsync();

                    foreach (string subdirectoryPath in Directory.EnumerateDirectories(fullNewPath)) {
                        string subdirectoryName = Path.GetFileName(subdirectoryPath);
                        results.Add(new ScannedPath {
                            Type = ScannedPath.AddonType.Folder,
                            FullPath = subdirectoryPath,
                            Name = subdirectoryName,
                        });
                    }

                    foreach (string filePath in Directory.EnumerateFiles(fullNewPath)) {
                        AddonBuildDefinition addon = null;
                        try {
                            using FileStream fs = new(filePath, FileMode.Open);
                            using ZipArchive zipArchive = new(fs);
                            addon = await AddonManager.GetAddonBuildDefinition(zipArchive, true);
                        } catch { }

                        string fileName = Path.GetFileName(filePath);
                        if (addon != null) {
                            // This is an addon.
                            results.Add(new ScannedPath {
                                Type = ScannedPath.AddonType.AddonFile,
                                Addon = addon,
                                FullPath = filePath,
                                Name = fileName,
                            });
                        } else if (!hideNonAddons) {
                            results.Add(new ScannedPath {
                                Type = ScannedPath.AddonType.NonAddonFile,
                                FullPath = filePath,
                                Name = fileName,
                            });
                        }
                    }
                    results.Sort();
                }
            } catch (Exception e) {
                Debug.LogError($"[Addon] Failed to open folder \"{AddonManager.LocalFolderPath}/{currentRelativePath}/{newPath}\": {e.Message}");
                Debug.LogError(e);
            }

            // Create gameobjects in main thread
            await Awaitable.MainThreadAsync();
            foreach (var entry in entries) {
                Destroy(entry.gameObject);
            }
            entries.Clear();

            bool isRoot = currentRelativePath == ".";
            if (!isRoot) {
                // Create "up" entry.
                var newEntry = Instantiate(template, template.transform.parent);
                newEntry.Initialize(this, ParentFolder);
                entries.Add(newEntry);
            }

            foreach (var result in results) {
                var newEntry = Instantiate(template, template.transform.parent);
                newEntry.Initialize(this, result);
                entries.Add(newEntry);
            }

            for (int i = 0; i < entries.Count; i++) {
                Navigation nav = new() { mode = Navigation.Mode.Explicit };
                if (i - 1 >= 0) {
                    // Previous
                    nav.selectOnUp = entries[i - 1].button;
                }
                if (i + 1 < entries.Count) {
                    //Next
                    nav.selectOnDown = entries[i + 1].button;
                } else {
                    Selectable backButtonSelectable = BackButton.GetComponentInChildren<Selectable>();
                    nav.selectOnDown = backButtonSelectable;

                    var backButtonNav = backButtonSelectable.navigation;
                    backButtonNav.selectOnUp = entries[i].button;
                    backButtonSelectable.navigation = backButtonNav;
                }
                entries[i].button.navigation = nav;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) scrollRect.transform);
            UnityEngine.Canvas.ForceUpdateCanvases();

            bool selected = false;
            string previousParent = Path.GetFileName(previousPath);
            foreach (var entry in entries) {
                if (entry.Path.Name == previousParent) {
                    Canvas.EventSystem.SetSelectedGameObject(entry.button.gameObject);
                    selected = true;
                    break;
                }
            }
            if (!selected) {
                if (entries.Count > 0) {
                    Canvas.EventSystem.SetSelectedGameObject(entries[0].button.gameObject);
                } else {
                    Canvas.EventSystem.SetSelectedGameObject(BackButton);
                }
            }
#if UNITY_STANDALONE
            try {
                watcher.Path = currentPath;
                watcher.EnableRaisingEvents = true;
            } catch { }
#endif
            loadingGraphic.SetActive(false);
        }

        public override bool TryGoBack(out bool playSound) {
            if (loadingGraphic.activeSelf) {
                playSound = false;
                return false;
            }
            return base.TryGoBack(out playSound);
        }

        public class ScannedPath : IComparable<ScannedPath> {
            public AddonType Type;
            public AddonBuildDefinition Addon;
            public string FullPath;
            public string Name;
            public bool IsFolder => Type == AddonType.Folder;

            public int CompareTo(ScannedPath other) {
                if (!IsFolder && other.IsFolder) {
                    return 1;
                } else if (IsFolder && !other.IsFolder) {
                    return -1;
                } else {
                    return string.Compare(Name, other.Name, true);
                }
            }

            public enum AddonType {
                AddonFile,
                Folder,
                NonAddonFile,
            }
        }

        public void UpdateSelectedAddonText(UnityEngine.Object obj) {
            if (obj is AddonFileSystemEntry entry && entry.Path.Type == ScannedPath.AddonType.AddonFile) {
                var addonDef = entry.Path.Addon;
                selectedAddonText.text = $"<line-height=0%><align=left>{addonDef.FullName}<br><align=right>{addonDef.Author}<line-height=100%><br><align=left><color=#adadad><line-height=75%><size=66.6%\n>{addonDef.Description}";
                if (addonDef.IconTexture) {
                    selectedAddonIcon.texture = addonDef.IconTexture;
                    selectedAddonIcon.gameObject.SetActive(true);
                } else {
                    selectedAddonIcon.texture = null;
                    selectedAddonIcon.gameObject.SetActive(false);
                }
            } else {
                selectedAddonText.text = "-";
                selectedAddonIcon.texture = null;
                selectedAddonIcon.gameObject.SetActive(false);
            }
        }

        private void OnAddonUnloaded(LoadedAddon obj) {
            UpdateLoadedAddonsText();
        }

        private void OnAddonLoaded(LoadedAddon obj) {
            UpdateLoadedAddonsText();
        }

        private void OnLanguageChanged(TranslationManager obj) {
            UpdateLoadedAddonsText();
        }
    }
}