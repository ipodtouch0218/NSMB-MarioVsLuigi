using NSMB.Utils;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NSMB.UI.MainMenu;
using Newtonsoft.Json;
using JetBrains.Annotations;

public class AuthenticationHandler {

    //---Static Variables
    private static readonly string URL = "https://mariovsluigi.azurewebsites.net/auth/init";

    public static bool IsAuthenticating { get; set; }

    public static async Task<AuthenticationValues> Authenticate() {

        string userid = PlayerPrefs.GetString("id", null);
        string token = PlayerPrefs.GetString("token", null);

        IsAuthenticating = true;

        string requestUrl = URL + "?";
        if (userid != null) {
            requestUrl += "&userid=" + userid;
        }
        if (token != null) {
            requestUrl += "&token=" + token;
        }
        byte args = 0;
        Utils.BitSet(ref args, 0, !Settings.Instance.generalUseNicknameColor);
        requestUrl += "&args=" + args;
        
        UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl);
        webRequest.certificateHandler = new MvLCertificateHandler();
        webRequest.disposeCertificateHandlerOnDispose = true;
        webRequest.disposeDownloadHandlerOnDispose = true;
        webRequest.disposeUploadHandlerOnDispose = true;
        webRequest.timeout = 10;

        await webRequest.SendWebRequest();

        string result = webRequest.downloadHandler.text.Trim();
        while (result.StartsWith('"')) {
            result = result[1..];
        }
        while (result.EndsWith('"')) {
            result = result[..^1];
        }
         
        if (webRequest.responseCode >= 300) {
            BanMessage ban = JsonConvert.DeserializeObject<BanMessage>(result);
            if (ban != null && MainMenuManager.Instance) {
                string reason = string.IsNullOrWhiteSpace(ban.Message) ? GlobalController.Instance.translationManager.GetTranslation("ui.error.noreason") : ban.Message;
                string template = ban.Expiration.HasValue ? "ui.error.gamebanned.temporary" : "ui.error.gamebanned.permanent";
                MainMenuManager.Instance.OpenErrorBox(template,
                    "banreason", reason, 
                    "banid", ban.Id.ToString(), 
                    "expiration", ban.Expiration.HasValue ? DateTimeOffset.FromUnixTimeSeconds(ban.Expiration.Value).LocalDateTime.ToString() : "");
            }
            IsAuthenticating = false;
            return null;
        }

        AuthenticationValues values = new() {
            AuthType = CustomAuthenticationType.Custom,
            UserId = userid,
        };
        values.AddAuthParameter("data", result);

        webRequest.Dispose();

        IsAuthenticating = false;
        return values;
    }
}

public class BanMessage {
    [JsonProperty("Id")]
    public int Id;
    [JsonProperty("Message")]
    public string Message;
    [JsonProperty("Expiration")]
    public long? Expiration;
}

public class UnityWebRequestAwaiter : INotifyCompletion {
    private UnityWebRequestAsyncOperation asyncOp;
    private Action continuation;

    public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp) {
        this.asyncOp = asyncOp;
        asyncOp.completed += OnRequestCompleted;
    }

    public bool IsCompleted => asyncOp.isDone;

    public void GetResult() { }

    public void OnCompleted(Action continuation) {
        this.continuation = continuation;
    }

    private void OnRequestCompleted(AsyncOperation obj) {
        continuation();
    }
}

public static class ExtensionMethods {
    public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp) {
        return new UnityWebRequestAwaiter(asyncOp);
    }
}
