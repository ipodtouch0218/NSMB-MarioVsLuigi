using UnityEngine;

using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {
    public class PauseOptionLoader : MonoBehaviour {
        public virtual void LoadOptions(PauseOption option) { }
        public virtual void OnValueChanged(PauseOption option, object newValue) { }
    }
}
