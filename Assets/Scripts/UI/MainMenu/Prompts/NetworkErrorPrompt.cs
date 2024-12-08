namespace NSMB.UI.Prompts {

    public class NetworkErrorPrompt : ErrorPrompt {
        public void Reconnect() {
            _ = NetworkHandler.ConnectToRegion(NetworkHandler.Region);
            gameObject.SetActive(false);
        }
    }
}
