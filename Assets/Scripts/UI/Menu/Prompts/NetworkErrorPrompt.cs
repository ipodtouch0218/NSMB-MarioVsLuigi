namespace NSMB.UI.Prompts {

    public class NetworkErrorPrompt : ErrorPrompt {
        public void Reconnect() {
            _ = NetworkHandler.ConnectToSameRegion();
            gameObject.SetActive(false);
        }
    }
}
