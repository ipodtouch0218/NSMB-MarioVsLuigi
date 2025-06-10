using NSMB.Extensions;
using UnityEngine;

namespace NSMB.UI.Pause {
    public class RefreshNicknameColorButton : MonoBehaviour {
        public void Click() {
            if (AuthenticationHandler.TryUpdateNicknameColor()) {
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
            } else {
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Error);
            }
        }
    }
}