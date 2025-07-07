using System;
using System.IO;
using System.Net;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace NSMB.Networking {
    public class UpdateChecker {

        private static readonly string ApiURL = "http://api.github.com/repos/ipodtouch0218/NSMB-MarioVsLuigi/releases/latest";

        /// <summary>
        /// Returns if we're up to date, OR newer, compared to the latest GitHub release version number
        /// </summary>
        public async static void IsUpToDate(Action<bool, string> callback) {

            // Get http results from the GitHub API
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(ApiURL);
            request.Accept = "application/json";
            request.UserAgent = "ipodtouch0218/NSMB-MarioVsLuigi";

            HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync();

            if (response.StatusCode != HttpStatusCode.OK) {
                Debug.Log($"[Updater] Failed to connect to the GitHub API: {response.StatusCode} - {response.StatusDescription}");
                return;
            }

            try {
                // Parse the latest release version number
                string json = new StreamReader(response.GetResponseStream()).ReadToEnd();
                JObject data = JObject.Parse(json);

                string tag = data.Value<string>("tag_name");
                GameVersion remoteVersion = GameVersion.Parse(tag);
                GameVersion localVersion = GameVersion.Parse(Application.version);

                bool upToDate = localVersion >= remoteVersion;
                Debug.Log($"[Updater] Local version: {localVersion} / Remote version: {remoteVersion}. Up to date: {upToDate}");

                callback(upToDate, tag);
            } catch { }
        }
    }
}
