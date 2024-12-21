using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus {
    public class ReplaySubmenu : MainMenuSubmenu {

        //---Serialized Variables
        [SerializeField] private ReplayListManager replayList;

        public override void Show(bool first) {
            base.Show(first);

            if (NetworkHandler.Client.IsConnected) {
                NetworkHandler.Client.Disconnect();
            }
        }

        public override bool TryGoBack(out bool playSound) {
            return base.TryGoBack(out playSound);
        }
    }
}
