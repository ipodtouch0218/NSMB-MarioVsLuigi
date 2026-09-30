using Newtonsoft.Json;
using NSMB.Networking;
using NSMB.Sound;
using NSMB.UI.Translation;
using NSMB.Utilities;
using Photon.Client;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NSMB.Addons {
    public class AddonManager : MonoBehaviour, IOnEventCallback, IMatchmakingCallbacks {

        //---Static Variables

        public static event Action<LoadedAddon> OnAddonLoaded, OnAddonUnloaded;
        public static event Action OnAvailableAddonListLoaded;

        public enum AddonDownloadResult {
            Failure,
            Cancelled,
            Success
        };
        public delegate void RequestingAddonDownloadsDelegate(List<AddonCatalogEntry> addons, Action<AddonDownloadResult> callback);
        public static event RequestingAddonDownloadsDelegate OnRequestingAddonDownloads;

        private static readonly byte EventBroadcastAddonList = 101;
        public static readonly DisconnectCause DisconnectCauseMissingAddon = (DisconnectCause) 101;

        private static readonly string RemoteRepoUrl = "https://raw.githubusercontent.com/ipodtouch0218/NSMB-MarioVsLuigi-AddonRepository/main/";
        private static readonly string RemoteRepoCatalogUrl = RemoteRepoUrl + "catalog.json";
        public static readonly string AddonExtension = ".mvladdon";

        public static string LocalFolderPath;
        public static string PlatformName { get; private set; }

        //---Properties
        public List<LoadedAddon> LoadedAddons { get; private set; } = new();

        //---Private Variables
        private Dictionary<AssetGuid, LoadedAddon> allRegisteredAssets = new();
        private List<AddonFile> availableAddons = new();
        private bool waitingForAddons;
#if UNITY_STANDALONE
        private FileSystemWatcher watcher1;
#endif

        public void Start() {
#if UNITY_WEBGL
            LocalFolderPath = Application.persistentDataPath + "/addons";
#elif UNITY_EDITOR
            LocalFolderPath = Path.GetFullPath(Application.dataPath + "/../addons");
#else
            LocalFolderPath = Application.dataPath + "/addons";
#endif
            if (!Directory.Exists(LocalFolderPath)) {
                Directory.CreateDirectory(LocalFolderPath);
            }

            PlatformName = GetPlatformName();
            _ = FindAvailableAddons();

#if UNITY_STANDALONE
            try {
                watcher1 = new(LocalFolderPath);
                watcher1.Filter = "*.mvladdon";
                watcher1.EnableRaisingEvents = true;

                watcher1.Changed += (_, file) => {
                    UnregisterAddon(file.FullPath);
                    _ = RegisterAddon(file.FullPath);
                };
                watcher1.Deleted += (_, file) => {
                    UnregisterAddon(file.FullPath);
                };
                watcher1.Created += (_, file) => {
                    _ = RegisterAddon(file.FullPath);
                };
            } catch (Exception e) {
                Debug.LogError(e);
            }
#endif

            NetworkHandler.Client.AddCallbackTarget(this);
        }

#if UNITY_STANDALONE
        public void OnDestroy() {
            watcher1?.Dispose();
        }
#endif

        public async Awaitable FindAvailableAddons() {
            // Background thread this sh*t
            await Awaitable.BackgroundThreadAsync();
            List<AddonFile> results = new();

            string filter = "*" + AddonExtension;
            foreach (var filepath in Directory.EnumerateFiles(LocalFolderPath, filter, new EnumerationOptions { RecurseSubdirectories = true })) {
                _ = await RegisterAddon(filepath, results);
            }

            // Main thread the events
            await Awaitable.MainThreadAsync();
            availableAddons = results;
            OnAvailableAddonListLoaded?.Invoke();
        }

        public async Task<AllAddonsLoadResult> LoadAllAddons(List<Guid> requestedAddons) {
            // Unload *ALL* addons. this is important as the order MATTERS.
            foreach (var addon in LoadedAddons.ToList()) {
                await UnloadAddon(addon);
            }

            Dictionary<string, AddonCatalogEntry> remoteCatalog = null;

            // Load addons
            List<AddonCatalogEntry> tryDownloading = new();
            List<Guid> notDownloadable = new();

            foreach (var addonGuid in requestedAddons) {
                var loadAddonResult = await LoadAddon(addonGuid);
                if (!loadAddonResult.Success && loadAddonResult.NeedsDownload) {
                    // Check if this is downloadable.
                    if (remoteCatalog == null) {
                        using UnityWebRequest catalogRequest = UnityWebRequest.Get(RemoteRepoCatalogUrl);
                        catalogRequest.SetRequestHeader("Accept", "application/json");
                        //catalogRequest.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");
                        catalogRequest.certificateHandler = new MvLCertificateHandler();
                        catalogRequest.disposeCertificateHandlerOnDispose = true;
                        catalogRequest.disposeDownloadHandlerOnDispose = true;
                        catalogRequest.disposeUploadHandlerOnDispose = true;
                        catalogRequest.timeout = 10;
                        await catalogRequest.SendWebRequest();

                        if (catalogRequest.result == UnityWebRequest.Result.Success && catalogRequest.responseCode == 200) {
                            try {
                                remoteCatalog = JsonConvert.DeserializeObject<Dictionary<string, AddonCatalogEntry>>(catalogRequest.downloadHandler.text);
                            } catch (Exception e) {
                                Debug.LogError($"[Addon] Failed to deserialize catalog.json: {e.Message}");
                                Debug.LogError(e);
                                return new AllAddonsLoadResult {
                                    Result = LoadAllAddonsResult.Failure,
                                };
                            }
                        } else {
                            Debug.LogError($"[Addon] Request to download catalog.json failed: {catalogRequest.result} - {catalogRequest.error}");
                            // We can't download this. Abort.
                            return new AllAddonsLoadResult {
                                Result = LoadAllAddonsResult.Failure,
                            };
                        }
                    }

                    if (remoteCatalog.TryGetValue(addonGuid.ToString(), out var catalogEntry) && catalogEntry.Artifacts.ContainsKey(PlatformName)) {
                        catalogEntry.ReleaseGuid = addonGuid;
                        tryDownloading.Add(catalogEntry);
                    } else {
                        notDownloadable.Add(addonGuid);
                    }
                }
            }

            // We can't download something
            if (notDownloadable.Count > 0) {
                Debug.Log($"[Addon] Unable to load all requested addons. Missing {notDownloadable.Count + tryDownloading.Count} addon(s).\nDownloadable: {tryDownloading.Count} [{string.Join(", ", tryDownloading.Select(x => x.ReleaseGuid))}]\nNot downloadable: {notDownloadable.Count} [{string.Join(", ", notDownloadable)}]");
                return new AllAddonsLoadResult {
                    Result = LoadAllAddonsResult.Failure
                };
            }

            // We can download everything that remains
            if (tryDownloading.Count > 0) {
                Debug.Log($"[Addon] Missing {tryDownloading.Count} downloadable addons [{string.Join(", ", tryDownloading.Select(x => x.ReleaseGuid))}], asking user...");
                return new AllAddonsLoadResult {
                    Result = LoadAllAddonsResult.DownloadRequired,
                    RequiredDownloads = tryDownloading,
                };
            }

            // Good to go.
            return new AllAddonsLoadResult {
                Result = LoadAllAddonsResult.Success
            };
        }

        public async Awaitable<AddonLoadResult> LoadAddon(Guid addonGuid) {
            var alreadyLoadedAddon = LoadedAddons.FirstOrDefault(la => la.Definition.ReleaseGuid == addonGuid);
            if (alreadyLoadedAddon != null) {
                return new AddonLoadResult {
                    Result = AddonLoadResultEnum.AlreadyLoaded,
                    NewAddon = alreadyLoadedAddon,
                };
            }

            var availableAddon = availableAddons.FirstOrDefault(addon => addon.Definition.ReleaseGuid == addonGuid);
            if (availableAddon == null) {
                return new AddonLoadResult {
                    Result = AddonLoadResultEnum.UnknownGuid,
                };
            }

            try {
                using FileStream fs = new(availableAddon.FilePath, FileMode.Open);
                return await LoadAddonStream(fs);
            } catch (Exception e) {
                Debug.Log($"[Addon] Failed to load addon {availableAddon.Definition.FullName} ({addonGuid}) from file \"{availableAddon.FilePath}\": {e.Message}");
                return new AddonLoadResult {
                    Result = AddonLoadResultEnum.ReadFailure
                };
            }
        }

        public async Awaitable<AddonLoadResult> LoadAddonStream(Stream stream) {
            await Awaitable.BackgroundThreadAsync();
            using ZipArchive zipFile = new(stream, ZipArchiveMode.Read);
            var addonDef = await GetAddonBuildDefinition(zipFile, false);

            if (!addonDef.GameVersion.EqualsIgnoreHotfix(GameVersion.Current)) {
                return new AddonLoadResult {
                    Result = AddonLoadResultEnum.IncompatibleGameVersion
                };
            }

            Debug.Log($"[Addon] Loading addon {addonDef.FullName} ({addonDef.ReleaseGuid})");

            List<AssetBundle> loadedBundles = new();
            List<(string, MemoryStream)> decompressedBundles = new();
            List<UnityEngine.Object> newlyRegisteredAssets = new();

            void UnloadAndCleanup() {
                foreach (var asset in newlyRegisteredAssets) {
                    UnloadAsset(asset);
                }
                foreach (var bundle in loadedBundles) {
                    bundle.Unload(true);
                }
            }

            try {
                // Load bundles
                var zippedBundles = zipFile.Entries.Where(zae => zae.FullName.StartsWith($"{PlatformName}/"));
                foreach (var zippedBundle in zippedBundles) {
                    using Stream bundleStream = zippedBundle.Open();
                    MemoryStream memoryStream = new((int) zippedBundle.Length);
                    bundleStream.CopyTo(memoryStream);
                    decompressedBundles.Add((zippedBundle.Name, memoryStream));
                }

                // Load into asset database
                await Awaitable.MainThreadAsync();

                foreach (var bundle in decompressedBundles) {
                    var loadTask = AssetBundle.LoadFromStreamAsync(bundle.Item2);
                    await loadTask;
                    if (!loadTask.assetBundle) {
                        // failure?
                        Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): loading AssetBundle \"{bundle.Item1}\" failed");
                        UnloadAndCleanup();
                        return new AddonLoadResult {
                            Result = AddonLoadResultEnum.ReadFailure
                        };
                    }
                    loadedBundles.Add(loadTask.assetBundle);
                }

                if (loadedBundles.Count == 0) {
                    Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): it does not support our platform ({PlatformName})");
                    UnloadAndCleanup();
                    return new AddonLoadResult {
                        Result = AddonLoadResultEnum.IncompatbilePlatform
                    };
                }

                var newAddon = new LoadedAddon {
                    Definition = addonDef,
                    LoadedAssetBundles = loadedBundles,
                    RegisteredAssets = newlyRegisteredAssets,
                };

                foreach (var assetBundle in loadedBundles) {
                    if (assetBundle.isStreamedSceneAssetBundle) {
                        continue;
                    }

                    var loadTask = assetBundle.LoadAllAssetsAsync<ScriptableObject>();
                    await loadTask;

                    foreach (ScriptableObject so in loadTask.allAssets.Cast<ScriptableObject>()) {
                        // Register to Quantum DB
                        if (so is AssetObject assetObject) {
                            try {
                                QuantumUnityDB.Global.AddAsset(assetObject);
                                newlyRegisteredAssets.Add(assetObject);
                                allRegisteredAssets.Add(assetObject.Guid, newAddon);
                            } catch (Exception e) {
                                Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): registering AssetObject {so.name} ({assetObject.Guid}) failed");
                                Debug.LogError(e);
                                UnloadAndCleanup();
                                if (allRegisteredAssets.TryGetValue(assetObject.Guid, out var incompatibleAddon)) {
                                    return new AddonLoadResult {
                                        Result = AddonLoadResultEnum.IncompatibleWithOtherAddon,
                                        IncompatibleWith = incompatibleAddon,
                                    };
                                } else {
                                    return new AddonLoadResult {
                                        Result = AddonLoadResultEnum.IncompatibleGameVersion
                                    };
                                }
                            }
                        } else if (so is GlobalSoundEffectOverrides sfxOverride) {
                            SoundEffectResolver.Instance.GlobalProviders.Add(sfxOverride);
                            newlyRegisteredAssets.Add(sfxOverride);
                        } else if (so is ScriptableTranslationSource translationSource) {
                            GlobalController.Instance.translationManager.RegisterTranslationSource(translationSource.LocaleCode, translationSource);
                            newlyRegisteredAssets.Add(translationSource);
                        }
                    }
                }

                if (newlyRegisteredAssets.Count > 0) {
                    Debug.Log($"[Addon] Registered {newlyRegisteredAssets.Count} assets");
                }

                LoadedAddons.Add(newAddon);
                OnAddonLoaded?.Invoke(newAddon);

                return new AddonLoadResult {
                    Result = AddonLoadResultEnum.Success,
                    NewAddon = newAddon
                };
            } catch (Exception e) {
                Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): An exception was thrown {e.Message}");
                Debug.LogError(e);
                UnloadAndCleanup();
                return new AddonLoadResult {
                    Result = AddonLoadResultEnum.ReadFailure
                };
            } finally {
                foreach (var bundleStream in decompressedBundles) {
                    bundleStream.Item2?.Dispose();
                }
            }
        }

        public async Task UnloadAddon(Guid addonGuid) {
            await UnloadAddon(LoadedAddons.FirstOrDefault(la => la.Definition.ReleaseGuid == addonGuid));
        }

        public async Task UnloadAddon(LoadedAddon addon) {
            if (addon == null) {
                throw new ArgumentNullException("Tried to unload a null addon!");
            }

            foreach (var asset in addon.RegisteredAssets) {
                UnloadAsset(asset);
            }
            if (addon.RegisteredAssets.Count > 0) {
                Debug.Log($"[Addon] Unloaded {addon.RegisteredAssets.Count} assets");
            }

            foreach (var assetBundle in addon.LoadedAssetBundles) {
                await assetBundle.UnloadAsync(true);
            }

            LoadedAddons.Remove(addon);
            OnAddonUnloaded?.Invoke(addon);
        }

        private void UnloadAsset(UnityEngine.Object obj) {
            if (obj is AssetObject assetObject) {
                QuantumUnityDB.Global.DisposeAsset(assetObject.Guid, true);
                QuantumUnityDB.Global.RemoveSource(assetObject.Guid);
                allRegisteredAssets.Remove(assetObject.Guid);
            } else if (obj is GlobalSoundEffectOverrides sfxOverride) {
                SoundEffectResolver.Instance.GlobalProviders.Remove(sfxOverride);
            } else if (obj is ScriptableTranslationSource translationSource) {
                GlobalController.Instance.translationManager.UnregisterTranslationSource(translationSource.LocaleCode, translationSource);
            }
        }

        public bool IsAddonLoaded(Guid guid) {
            return LoadedAddons.Any(la => la.Definition.ReleaseGuid == guid);
        }

        public async Awaitable<AddonFile> RegisterAddon(string fullPath, List<AddonFile> results = null) {
            // Parse file to see if we need to add this.
            await Awaitable.BackgroundThreadAsync();
            try {
                fullPath = new FileInfo(fullPath).FullName; // Clean up file paths.
                using var zipFile = ZipFile.OpenRead(fullPath);
                var addonDef = await GetAddonBuildDefinition(zipFile, false);
                if (addonDef == null) {
                    return null;
                }

                if (!zipFile.Entries.Any(en => en.FullName.StartsWith($"{PlatformName}/"))) {
                    Debug.Log($"[Addon] Incompatible addon found \"{addonDef.FullName}\" ({addonDef.ReleaseGuid}) at \"{fullPath}\"");
                    return null;
                }

                if (availableAddons.Any(af => af.Definition.ReleaseGuid == addonDef.ReleaseGuid)
                    || (results != null && results.Any(af => af.Definition.ReleaseGuid == addonDef.ReleaseGuid))) {
                    Debug.Log($"[Addon] Duplicate addon found \"{addonDef.FullName}\" ({addonDef.ReleaseGuid}) at \"{fullPath}\"");
                    return null;
                }

                AddonFile addon = new() {
                    Definition = addonDef,
                    FilePath = fullPath
                };

                await Awaitable.MainThreadAsync();
                results?.Add(addon);
                Debug.Log($"[Addon] Registered addon \"{addonDef.FullName}\" ({addonDef.ReleaseGuid}) at \"{fullPath}\"");
                return addon;
            } catch (Exception e) {
                Debug.LogWarning($"[Addon] Failed to read addon file {fullPath}: {e.Message}");
            }
            return null;
        }

        public void UnregisterAddon(string fullPath) {
            fullPath = new FileInfo(fullPath).FullName;
            availableAddons.RemoveAll(af => new FileInfo(af.FilePath).FullName == fullPath);
        }

        public async Awaitable SaveAddonToCache(string filename, byte[] data) {
            try {
                await Awaitable.BackgroundThreadAsync();
                Directory.CreateDirectory($"{LocalFolderPath}/download");
                string path = $"{LocalFolderPath}/download/{filename}";

                Debug.Log($"[Addon] Saving addon to download folder: {path}");
                await File.WriteAllBytesAsync(path, data);

                var addonFile = await RegisterAddon(path);
                if (addonFile != null) {
                    availableAddons.Add(addonFile);
                }
            } catch (Exception e) {
                Debug.LogError($"[Addon] Failed to save addon to download folder: {e.Message}");
                Debug.LogError(e);
            }
        }

        public static async Awaitable<AddonBuildDefinition> GetAddonBuildDefinition(ZipArchive zipFile, bool loadIcon) {
            try {
                var entry = zipFile.GetEntry("addon.json");
                if (entry == null) {
                    return null;
                }
                using StreamReader reader = new(entry.Open());
                var addonDefJson = await reader.ReadToEndAsync();
                var addonDef = JsonConvert.DeserializeObject<AddonBuildDefinition>(addonDefJson);

                if (loadIcon) {
                    var iconEntry = zipFile.GetEntry("icon.png");
                    if (iconEntry != null) {
                        await Awaitable.MainThreadAsync();
                        using Stream iconStream = iconEntry.Open();
                        addonDef.IconTexture = new Texture2D(1, 1);
                        addonDef.IconTexture.LoadImage(ReadStreamToArray(iconStream));
                    }
                }

                return addonDef;
            } catch (Exception e) {
                Debug.LogError(e);
                return null;
            }
        }

        private static byte[] ReadStreamToArray(Stream stream) {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        public static string GetPlatformName() {
#if UNITY_EDITOR
            var buildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            if (buildTarget == UnityEditor.BuildTarget.StandaloneWindows64) {
                // Win64 and 32 are compatible, default to 32.
                buildTarget = UnityEditor.BuildTarget.StandaloneWindows;
            }

            return buildTarget.ToString();
#else
            switch (Application.platform) {
            case RuntimePlatform.WindowsPlayer:
                return "StandaloneWindows"; // Win64 and 32 are compatible, default to 32.
            case RuntimePlatform.OSXPlayer:
                return "StandaloneOSX";
            case RuntimePlatform.LinuxPlayer:
                return "StandaloneLinux64";
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.WebGLPlayer:
                return "WebGL";
            default:
                throw new Exception("Unsupported platform");
            }
#endif
        }

        public static string GetDownloadUrl(Guid guid) {
            return CombineUrl(RemoteRepoUrl, guid + AddonExtension);
        }

        private static string CombineUrl(string url1, string url2) {
            if (url1.Length == 0) {
                return url2;
            }

            if (url2.Length == 0) {
                return url1;
            }

            url1 = url1.TrimEnd('/', '\\');
            url2 = url2.TrimStart('/', '\\');

            return $"{url1}/{url2}";
        }

        public static void RequestDownloadAddons(List<AddonCatalogEntry> addons, Action<AddonDownloadResult> callback) {
            OnRequestingAddonDownloads?.Invoke(addons, callback);
        }

        public async void OnEvent(EventData photonEvent) {
            if (photonEvent.Code == EventBroadcastAddonList && waitingForAddons) {
                waitingForAddons = false;
                try {
                    List<Guid> guids = ((string[]) photonEvent.CustomData).Select(Guid.Parse).ToList();
                    Debug.Log($"[Addon] Got addon list of {guids.Count} addons: [{string.Join(", ", guids)}]");
                    var loadAddonResult = await GlobalController.Instance.addonManager.LoadAllAddons(guids);

                    if (loadAddonResult.Result == LoadAllAddonsResult.Success) {
                        _ = NetworkHandler.Instance.StartQuantum();
                    } else if (loadAddonResult.Result == LoadAllAddonsResult.DownloadRequired) {
                        GlobalController.Instance.connecting.SetActive(false);
                        RequestDownloadAddons(loadAddonResult.RequiredDownloads, async (result) => {
                            if (result == AddonDownloadResult.Success) {
                                GlobalController.Instance.connecting.SetActive(true);
                                _ = NetworkHandler.Instance.StartQuantum();
                            } else if (result == AddonDownloadResult.Cancelled) {
                                await NetworkHandler.Disconnect();
                                await NetworkHandler.ConnectToRegion(null);
                            } else {
                                NetworkHandler.ThrowError("ui.error.join.addons.downloadfailed", false);
                                await NetworkHandler.Disconnect();
                                await NetworkHandler.ConnectToRegion(null);
                            }
                        });
                    } else {
                        NetworkHandler.ThrowError("ui.error.join.addons.downloadfailed", false);
                        NetworkHandler.Client.Disconnect(DisconnectCauseMissingAddon);
                    }
                } catch (Exception e) {
                    Debug.LogError($"[Addon] Failed to activate proper addons! Disconnecting. ({e.Message})");
                    NetworkHandler.Client.Disconnect(DisconnectCauseMissingAddon);
                    throw;
                }
            }
        }

        public void OnFriendListUpdate(List<FriendInfo> friendList) { }

        public void OnCreatedRoom() {
            // Send addon list
            NetworkHandler.Client.OpRaiseEvent(EventBroadcastAddonList,
                GlobalController.Instance.addonManager.LoadedAddons.Select(la => la.Definition.ReleaseGuid.ToString()).ToArray(),
                new RaiseEventArgs {
                    CachingOption = EventCaching.AddToRoomCacheGlobal
                }, SendOptions.SendReliable);
        }

        public void OnCreateRoomFailed(short returnCode, string message) { }

        public void OnJoinedRoom() {
            waitingForAddons = true;
        }

        public void OnJoinRoomFailed(short returnCode, string message) { }

        public void OnJoinRandomFailed(short returnCode, string message) { }

        public void OnLeftRoom() {
            waitingForAddons = false;
        }
    }


    public class LoadedAddon {
        public AddonBuildDefinition Definition;
        public List<AssetBundle> LoadedAssetBundles;
        public List<UnityEngine.Object> RegisteredAssets;
    }

    public class AddonFile {
        public AddonBuildDefinition Definition;
        public string FilePath;
    }

    public class AddonCatalogEntry {
        public Guid ReleaseGuid;
        public string DisplayName;
        public string Author;
        public string Version;
        public Dictionary<string, AddonCatalogArtifactEntry> Artifacts;
    }

    public class AddonCatalogArtifactEntry {
        public string Url;
        public long Size;
        public string Sha256;
    }

    public struct AddonLoadResult {
        public readonly bool Success => Result == AddonLoadResultEnum.Success || Result == AddonLoadResultEnum.AlreadyLoaded;
        public readonly bool NeedsDownload => Result == AddonLoadResultEnum.UnknownGuid || Result == AddonLoadResultEnum.ReadFailure;
        public AddonLoadResultEnum Result;
        public LoadedAddon NewAddon;
        public LoadedAddon IncompatibleWith;
    }

    public struct AllAddonsLoadResult {
        public LoadAllAddonsResult Result;
        public List<AddonCatalogEntry> RequiredDownloads;
    }

    public enum AddonLoadResultEnum {
        UnknownGuid,
        ReadFailure,
        IncompatbilePlatform,
        IncompatibleGameVersion,
        IncompatibleWithOtherAddon,
        AlreadyLoaded,
        Success
    }

    public enum LoadAllAddonsResult {
        Failure,
        DownloadRequired,
        Success,
    }

}