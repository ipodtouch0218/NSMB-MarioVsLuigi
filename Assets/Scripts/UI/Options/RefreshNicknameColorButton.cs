using NSMB.Networking;
using UnityEngine;
using UnityEngine.Scripting;

namespace NSMB.UI.Options {
    public class RefreshNicknameColorButton : MonoBehaviour {

        [Preserve]
        public void Click() {
            if (AuthenticationHandler.TryUpdateNicknameColor()) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
            } else {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Error);
            }
        }
    }
}