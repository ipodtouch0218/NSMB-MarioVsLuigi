using Newtonsoft.Json.Linq;
using NSMB.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Main {
    public class NewsBoardManager : MonoBehaviour {

        //---Static Variables
        private const string URL = "https://mariovsluigi.azurewebsites.net/news";

        //---Serialized Variables
        [SerializeField] private NewsBoardEntry template;
        [SerializeField] private GameObject loading;

        //---Private Variables
        private List<NewsBoardEntry> posts = new();
#if USE_CACHE
        private Dictionary<int, NewsBoardEntry.NewsBoardData> cachedPosts;
#endif
        private bool gotPosts;

        public void OnEnable() {
            if (!gotPosts) {
#if USE_CACHE
                try {
                    cachedPosts = JArray.Parse(PlayerPrefs.GetString("NewsCache", "")).Select(jt => jt.ToObject<NewsBoardEntry.NewsBoardData>()).ToDictionary(d => d.Id);
                } catch (Exception e) {
                    cachedPosts = new();
                }
#endif
                _ = FetchPosts();
            }
        }

#if USE_CACHE
        public void OnDestroy() {
            var cacheAsArray = cachedPosts.Values.ToArray();
            string output = JArray.FromObject(cacheAsArray).ToString(Formatting.None);
            PlayerPrefs.SetString("NewsCache", output);
            PlayerPrefs.Save();
        }
#endif

        private async Task FetchPosts() {
            loading.SetActive(true);
#if USE_CACHE
            using UnityWebRequest webRequest = UnityWebRequest.Get(URL);
#else
            using UnityWebRequest webRequest = UnityWebRequest.Get(URL + "/all");
#endif
            webRequest.certificateHandler = new MvLCertificateHandler();
            webRequest.disposeCertificateHandlerOnDispose = true;
            webRequest.disposeDownloadHandlerOnDispose = true;
            webRequest.disposeUploadHandlerOnDispose = true;
            webRequest.timeout = 10;

            Debug.Log($"[News] Checking for news posts at {webRequest.url}");
            await webRequest.SendWebRequest();

            string response = Encoding.UTF8.GetString(webRequest.downloadHandler.data);
            if (webRequest.result != UnityWebRequest.Result.Success || webRequest.responseCode != 200) {
                Debug.LogError($"[News] News request failed with error: {webRequest.error} | response code: {webRequest.responseCode} | response: {response}");
                return;
            }

            JArray responseArray = JArray.Parse(response);
            Debug.Log($"[News] Found {responseArray.Count} news post(s).");

            foreach (var post in responseArray) {
#if USE_CACHE
                int id = post.Value<int>();

                if (cachedPosts.TryGetValue(id, out NewsBoardEntry.NewsBoardData postData)) {
                    Debug.Log($"[News] Post w/ id {id} found in cache.");
                } else {
                    // Request from website.
                    Debug.Log($"[News] Post w/ id {id} not found in cache. Fetching post w/ id {id}");
                    webRequest.Dispose();
                    webRequest = UnityWebRequest.Get($"{URL}/{id}");
                    yield return webRequest.SendWebRequest();   

                    if (webRequest.result == UnityWebRequest.Result.Success && webRequest.responseCode == 200) {
                        // Success.
                        response = Encoding.UTF8.GetString(webRequest.downloadHandler.data);
                        postData = JsonConvert.DeserializeObject<NewsBoardEntry.NewsBoardData>(response);
                        cachedPosts[id] = postData;
                    } else {
                        Debug.LogWarning($"[News] Failed to get post at '{webRequest.url}' (Response Code: {webRequest.responseCode})");
                        continue;
                    }
                }
#else
                var postData = post.ToObject<NewsBoardEntry.NewsBoardData>();
#endif

                // Instantiate post
                NewsBoardEntry newPost = Instantiate(template, template.transform.parent);
                newPost.Initialize(postData);
                posts.Add(newPost);
            }

            gotPosts = true;
            loading.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) template.transform.parent);
        }
    }
}
