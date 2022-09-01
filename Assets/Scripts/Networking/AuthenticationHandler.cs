using System.Net.Http;

using Photon.Pun;
using Photon.Realtime;

public class AuthenticationHandler {

    private static readonly string URL = "https://mariovsluigi.azurewebsites.net/auth/init";

    public async static void Authenticate(string userid, string token) {
        try {
            HttpClient client = new();
            string request = URL + "?";
            if (userid != null)
                request += "&userid=" + userid;
            if (token != null)
                request += "&token=" + token;

            HttpResponseMessage resp = await client.GetAsync(request);
            string responseString = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode) {
                if (MainMenuManager.Instance) {
                    MainMenuManager.Instance.OpenErrorBox(responseString);
                    MainMenuManager.Instance.OnDisconnected(DisconnectCause.CustomAuthenticationFailed);
                }
                return;
            }


            AuthenticationValues values = new();
            values.AuthType = CustomAuthenticationType.Custom;
            values.UserId = userid;
            values.AddAuthParameter("data", responseString.Trim());
            PhotonNetwork.AuthValues = values;

            PhotonNetwork.NetworkingClient.ConnectToNameServer();

        } catch (HttpRequestException e) {

            if (MainMenuManager.Instance)
                MainMenuManager.Instance.OpenErrorBox(e.Message);
            return;
        }
    }
}