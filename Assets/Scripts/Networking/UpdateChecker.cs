using Newtonsoft.Json.Linq;
using NSMB.Utilities;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace NSMB.Networking {
    public class UpdateChecker {

        private static readonly string ApiURL = "https://api.github.com/repos/ipodtouch0218/NSMB-MarioVsLuigi/releases/latest";

        public delegate void UpdateCallback(bool upToDate, string latestGithubVersion);

        /// <summary>
        /// Returns if we're up to date, OR newer, compared to the latest GitHub release version number
        /// </summary>
        public async static void CheckIfUpToDate(UpdateCallback callback) {
            // Get http results from the GitHub API
            using UnityWebRequest request = UnityWebRequest.Get(ApiURL);
            request.SetRequestHeader("Accept", "application/json");
            //request.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");

            Debug.Log($"[Updater] Checking for updates at {ApiURL}");
            await request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200) {
                Debug.Log($"[Updater] Failed to connect to the GitHub API: {request.error} | {request.responseCode} | {request.downloadHandler.text}");
                return;
            }

            try {
                // Parse the latest release version number
                string json = request.downloadHandler.text;
                JObject data = JObject.Parse(json);

                string tag = data.Value<string>("tag_name");
                GameVersion remoteVersion = GameVersion.Parse(tag);
                GameVersion localVersion = GameVersion.Parse(Application.version);

                bool upToDate = localVersion >= remoteVersion;
                Debug.Log($"[Updater] Local version: {localVersion} / Remote version: {remoteVersion}. Up to date: {upToDate}");

                callback(upToDate, tag);
            } catch (Exception e) {
                Debug.Log($"[Updater] Failed to parse API response with thrown exception: {e.Message}");
                Debug.LogError(e);
            }
        }
    }
}
