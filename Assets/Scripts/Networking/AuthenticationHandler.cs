using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

using Fusion.Photon.Realtime;

public class AuthenticationHandler {

    private static readonly string URL = "https://mariovsluigi.azurewebsites.net/auth/init";

    public async static Task<AuthenticationValues> Authenticate(string userid, string token) {

        string request = URL + "?";
        if (userid != null)
            request += "&userid=" + userid;
        if (token != null)
            request += "&token=" + token;

        UnityWebRequest client = UnityWebRequest.Get(request);

        client.certificateHandler = new MvLCertificateHandler();
        client.disposeCertificateHandlerOnDispose = true;
        client.disposeDownloadHandlerOnDispose = true;
        client.disposeUploadHandlerOnDispose = true;

        await client.SendWebRequest();

        if (client.result != UnityWebRequest.Result.Success) {
            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.OpenErrorBox(client.error + " - " + client.responseCode);
                //MainMenuManager.Instance.OnDisconnected(DisconnectCause.CustomAuthenticationFailed);
            }
            return null;
        }

        AuthenticationValues values = new();
        values.AuthType = CustomAuthenticationType.Custom;
        values.UserId = userid;
        values.AddAuthParameter("data", client.downloadHandler.text.Trim());

        client.Dispose();

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

    public bool IsCompleted { get { return asyncOp.isDone; } }

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