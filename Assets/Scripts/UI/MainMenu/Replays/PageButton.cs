using TMPro;
using UnityEngine;
using UnityEngine.Scripting;

namespace NSMB.UI.MainMenu.Submenus.Replays {
    public class PageButton : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private ReplayListManager replayList;
        [SerializeField] private TMP_Text text;

        [Preserve]
        public void OnClick() {
            if (!int.TryParse(text.text, out int page)
                || replayList.CurrentPage == page - 1) {
                return;
            }

            // page is 0-indexed, so -1
            replayList.canvas.PlayCursorSound();
            _ = replayList.CreateReplayListEntries(default, page - 1);
        }

        [Preserve]
        public void NextPage() {
            ChangeToPage(replayList.CurrentPage + 1);
        }

        [Preserve]
        public void PreviousPage() {
            ChangeToPage(replayList.CurrentPage - 1);
        }

        public void ChangeToPage(int newPage) {
            if (newPage < 0 || newPage >= replayList.PageCount) {
                return;
            }
            replayList.canvas.PlayCursorSound();
            _ = replayList.CreateReplayListEntries(default, newPage);
        }
    }
}