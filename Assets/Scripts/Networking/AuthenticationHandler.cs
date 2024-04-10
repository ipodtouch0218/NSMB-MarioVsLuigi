using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

using Fusion.Photon.Realtime;
using NSMB.Utils;

public class AuthenticationHandler {

    //---Static Variables
    private static readonly string URL = "https://mariovsluigi.azurewebsites.net/auth/init";

    public static bool IsAuthenticating { get; set; }

    public static async Task<AuthenticationValues> Authenticate(string userid, string token) {

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

        if (webRequest.result != UnityWebRequest.Result.Success) {
            IsAuthenticating = false;
            return null;
        }

        AuthenticationValues values = new() {
            AuthType = CustomAuthenticationType.Custom,
            UserId = userid,
        };
        values.AddAuthParameter("data", webRequest.downloadHandler.text.Trim().Replace("\"", ""));

        webRequest.Dispose();

        IsAuthenticating = false;
        return values;
    }
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
