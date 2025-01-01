using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus {
    public class ReplaySubmenu : MainMenuSubmenu {

        //---Properties
        public override GameObject DefaultSelection {
            get {
                ReplayListEntry selected = replayList.GetFirstReplayEntry();
                if (selected) {
                    return selected.button.gameObject;
                }
                return base.DefaultSelection;
            }
        }
        public override float BackHoldTime => replayList.Selected ? 0 : 1;

        //---Serialized Variables
        [SerializeField] private ReplayListManager replayList;

        public override void Initialize(MainMenuCanvas canvas) {
            base.Initialize(canvas);
            replayList.FindReplays();
        }

        public override void Show(bool first) {
            base.Show(first);
            
            if (first) {
                replayList.Show();
            }
        }

        public override bool TryGoBack(out bool playSound) {
            ReplayListEntry selected = replayList.Selected;
            if (selected) {
                replayList.Select(null);
                Canvas.EventSystem.SetSelectedGameObject(selected.button.gameObject);
                playSound = true;
                return false;
            }

            return base.TryGoBack(out playSound);
        }
    }
}
