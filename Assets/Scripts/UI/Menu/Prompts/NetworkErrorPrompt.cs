namespace NSMB.UI.Prompts {
    public class NetworkErrorPrompt : UIPrompt {

        protected override void SetDefaults() {
            base.SetDefaults();
        }

        public void Reconnect() {
            _ = NetworkHandler.ConnectToSameRegion();
            gameObject.SetActive(false);
        }
    }
}
