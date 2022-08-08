using System.Net.Http;

using Photon.Pun;
using Photon.Realtime;

public class AuthenticationHandler {

    private static readonly string URL = "https://mariovsluigi.azurewebsites.net/auth/init";

    public async static void Authenticate(string userid, string token) {

        HttpClient client = new();
        try {
            string request = URL + "?";
            if (userid != null)
                request += "&userid=" + userid;
            if (token != null)
                request += "&token=" + token;

            string signedData = await client.GetStringAsync(request);

            AuthenticationValues values = new();
            values.AuthType = CustomAuthenticationType.Custom;
            values.UserId = userid;
            values.AddAuthParameter("data", signedData.Trim());
            PhotonNetwork.AuthValues = values;

            PhotonNetwork.NetworkingClient.ConnectToNameServer();

        } catch (HttpRequestException e) {
            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.OpenErrorBox(e.Message);
            }
            return;
        }

    }
}