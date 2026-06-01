using NSMB.Replay.Stats;
using NSMB.UI.MainMenu.Submenus.Replays;

namespace NSMB.UI.MainMenu.Submenus.ReplayStats {
    public class ReplayStatsSubmenu : MainMenuSubmenu {
        public override bool TryGoBack(out bool playSound) {
            ReplayStatsRecorder.Instance.MurderRunner();
            /*ReplayListEntry selected = ReplayListManager.Instance.Selected;
            if (selected && selected.IsOpen) {
                selected.HideButtons();
                Canvas.EventSystem.SetSelectedGameObject(selected.button.gameObject);
                playSound = true;
                return false;
            }*/

            return base.TryGoBack(out playSound);
        }
    }
}
