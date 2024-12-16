using System.Collections.Generic;
using System.Linq;

using NSMB.UI.Pause.Options;

namespace NSMB.UI.Pause.Loaders {
    public class SimpleOptionsLoader : SimpleLoader<ScrollablePauseOption, int> {

        // --- Private Variables
        private readonly List<string> options = new();

        public override void LoadOptions(PauseOption option) {
            ScrollablePauseOption spo = (ScrollablePauseOption) option;

            if (options.Count == 0) {
                options.AddRange(spo.options);
            }

            spo.options.Clear();
            spo.options.AddRange(options.Select(GlobalController.Instance.translationManager.GetTranslation));

            base.LoadOptions(option);
        }

        public override int GetValue(ScrollablePauseOption pauseOption) {
            return pauseOption.value;
        }

        public override void SetValue(ScrollablePauseOption pauseOption, int value) {
            pauseOption.SetValue(value);
        }
    }
}
