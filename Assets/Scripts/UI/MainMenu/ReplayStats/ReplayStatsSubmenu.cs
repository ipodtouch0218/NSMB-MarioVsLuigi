using NSMB.Replay.Stats;
using NSMB.UI.MainMenu.Submenus.Replays;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class ReplayStatsSubmenu : MainMenuSubmenu {
        public override bool TryGoBack(out bool playSound) {
            ReplayStatsRecorder.Instance.MurderRunner();
            ReplayStatsManager.Instance.PurgeObjects();

            return base.TryGoBack(out playSound);
        }
    }
}
