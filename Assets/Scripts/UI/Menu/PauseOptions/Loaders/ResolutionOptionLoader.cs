using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {

    public class ResolutionOptionLoader : PauseOptionLoader {
        public override void LoadOptions(PauseOption option) {
            if (option is not ScrollablePauseOption spo)
                return;

            spo.options.Clear();

            IEnumerable<string> newOptions =
                Screen.resolutions
                .Select(res => res.width + "x" + res.height)
                .Distinct();

            spo.options.AddRange(newOptions);

            int index = Screen.resolutions.Length;
            for (int i = 0; i < Screen.resolutions.Length; i++) {
                var res = Screen.resolutions[i];
                if (Screen.currentResolution.Equals(res)) {
                    index = i;
                    break;
                }
            }

            spo.SetValue(index, false);
        }

        public override void OnValueChanged(PauseOption option, object previous) {
            if (option is not ScrollablePauseOption spo)
                return;

            var res = Screen.resolutions[spo.value];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }
}
