using JimmysUnityUtilities;
using NSMB.Addons;
using NSMB.Networking;
using NSMB.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace NSMB.UI.MainMenu.Submenus.Prompts.Addons {
    public class DownloadAddonsSubmenu : PromptSubmenu {

        //---Properites
        public override GameObject BackButton => askPanel.activeSelf ? backButton : downloadingCancelButton;
        
        //---Serialized Variables
        [SerializeField] private GameObject askPanel, downloadingPanel;
        [SerializeField] private TMP_Text askText, singleFileProgressText, allFilesProgressText;
        [SerializeField] private RectTransform singleFileProgressBar, allFilesProgressBar;
        [SerializeField] private GameObject downloadingCancelButton;

        //---Private Variables
        private List<AddonCatalogEntry> addons;
        private Action<AddonManager.AddonDownloadResult> callback;
        private Coroutine downloadingCoroutine;

        public override void Initialize() {
            base.Initialize();
            AddonManager.OnRequestingAddonDownloads += AskToDownload;
        }

        public override void OnDestroy() {
            base.OnDestroy();
            AddonManager.OnRequestingAddonDownloads -= AskToDownload;
        }

        public void AskToDownload(List<AddonCatalogEntry> addons, Action<AddonManager.AddonDownloadResult> callback) {
            this.addons = addons;
            this.callback = callback;

            askPanel.SetActive(true);
            downloadingPanel.SetActive(false);

            askText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.addons.download.request",
                "addons", addons.Count.ToString(),
                "filesize", Utils.BytesToString(addons.Select(ace => ace.Artifacts[AddonManager.PlatformName]).Sum(ace => ace.Size)));

            Canvas.OpenMenu(this);
        }

        public void StartDownload() {
            askPanel.SetActive(false);
            downloadingPanel.SetActive(true);
            Canvas.EventSystem.SetSelectedGameObject(downloadingCancelButton);
            downloadingCoroutine = StartCoroutine(UpdateDownloadProgress());
        }

        private IEnumerator UpdateDownloadProgress() {
            int downloadedAddons = 0;
            UpdateProgressBars(0, 0, 0);

            foreach (var addonCatalogEntry in addons) {
                var artifact = addonCatalogEntry.Artifacts[AddonManager.PlatformName];
                Debug.Log($"[Addon] Attempting to download addon with ID {addonCatalogEntry.ReleaseGuid} from URL ({artifact.Url})");

                using var addonRequest = UnityWebRequest.Get(artifact.Url);
                addonRequest.SetRequestHeader("Accept", "*/*");
                //addonRequest.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");
                addonRequest.certificateHandler = new MvLCertificateHandler();
                addonRequest.disposeCertificateHandlerOnDispose = true;
                addonRequest.disposeDownloadHandlerOnDispose = true;
                addonRequest.disposeUploadHandlerOnDispose = true;
                addonRequest.timeout = 10;
                _ = addonRequest.SendWebRequest();

                string sizeString = Utils.BytesToString(artifact.Size);
                do {
                    UpdateProgressBars(artifact.Size, addonRequest.downloadProgress, downloadedAddons);
                    yield return null;
                } while (!addonRequest.isDone && addonRequest.downloadProgress < 1);

                UpdateProgressBars(artifact.Size, addonRequest.downloadProgress, downloadedAddons);

                if (addonRequest.responseCode != 200) {
                    Debug.Log($"[Addon] Download failed: {addonRequest.error} ({addonRequest.responseCode})");
                    Error();
                    yield break;
                }

                byte[] addonBytes = addonRequest.downloadHandler.data;
                MemoryStream ms = new(addonBytes);
                var addonStreamTask = GlobalController.Instance.addonManager.LoadAddonStream(ms).GetAwaiter();
                yield return addonStreamTask;

                var loadResult = addonStreamTask.GetResult();
                if (!loadResult.Success) {
                    Error();
                    yield break;
                }

                downloadedAddons++;

                UpdateProgressBars(artifact.Size, 1, downloadedAddons);

                _ = GlobalController.Instance.addonManager.SaveAddonToCache(addonCatalogEntry.ReleaseGuid, addonBytes);
            }

            // Success!
            callback(AddonManager.AddonDownloadResult.Success);
            Canvas.CloseSubmenu(this);
        }

        private void UpdateProgressBars(long downloadBytes, float downloadProgress, int downloadedAddons) {
            singleFileProgressText.text = $"{Utils.BytesToString((long) (downloadProgress * downloadBytes))} / {Utils.BytesToString(downloadBytes)}";
            singleFileProgressBar.SetAnchorMaxX(downloadProgress);
            singleFileProgressBar.SetMarginRight(0);

            allFilesProgressText.text = $"{downloadedAddons} / {addons.Count}";
            allFilesProgressBar.SetAnchorMaxX(0);
            allFilesProgressBar.SetMarginRight(0);
        }

        public void RejectDownload() {
            callback(AddonManager.AddonDownloadResult.Cancelled);
            Canvas.CloseSubmenu(this);
        }

        public void CancelDownload() {
            StopCoroutine(downloadingCoroutine);
            callback(AddonManager.AddonDownloadResult.Cancelled);
            Canvas.CloseSubmenu(this);
        }

        private void Error() {
            callback(AddonManager.AddonDownloadResult.Failure);
            Canvas.CloseSubmenu(this);
        }

        public override bool TryGoBack(out bool playSound) {
            var result = base.TryGoBack(out playSound);
            if (result) {
                StopCoroutine(downloadingCoroutine);
                callback(AddonManager.AddonDownloadResult.Cancelled);
            }
            return result;
        }
    }
}