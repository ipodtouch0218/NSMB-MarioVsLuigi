using UnityEngine;

namespace NSMB.UI.Options.Loaders {
    public class PauseOptionLoader : MonoBehaviour {
        public virtual void LoadOptions(PauseOption option) { }
        public virtual void OnValueChanged(PauseOption option, object newValue) { }
    }
}
