using NSMB.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace NSMB.UI.Elements {
    public class ContextMenuTeamSelector : Button {

        //---Serialized Variables
        [SerializeField] private TMP_Text label;
        [SerializeField] private string teamTranslationKey, clearTranslationKey;
        [SerializeField] private GameObject[] showOnSelect;

        //---Properties
        private int _teamIndex;
        public int TeamIndex {
            get => _teamIndex;
            set {
                _teamIndex = Mathf.Clamp(value, 0, QuantumViewUtils.Teams.Length + 1);
                UpdateLabel();
            }
        }

        protected override void OnEnable() {
            base.OnEnable();
            TeamIndex = 0;
        }

        protected override void OnDisable() {
            base.OnDisable();
            foreach (var go in showOnSelect) {
                go.SetActive(false);
            }
        }

        public override void OnMove(AxisEventData eventData) {
            if (eventData.moveDir == MoveDirection.Left) {
                TeamIndex--;
                eventData.Use();
            } else if (eventData.moveDir == MoveDirection.Right) {
                TeamIndex++;
                eventData.Use();
            } else {
                base.OnMove(eventData);
            }
        }

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
            foreach (var go in showOnSelect) {
                go.SetActive(true);
            }
        }

        public override void OnDeselect(BaseEventData eventData) {
            base.OnDeselect(eventData);
            foreach (var go in showOnSelect) {
                go.SetActive(false);
            }
        }

        [Preserve]
        public void AddIndex(int value) {
            TeamIndex += value;
        }

        public void UpdateLabel() {
#if UNITY_EDITOR
            if (!this || !Application.IsPlaying(this)) {
                return;
            }
#endif
            var tm = GlobalController.Instance.translationManager;
            string text;

            var teams = QuantumViewUtils.Teams;
            if (TeamIndex < teams.Length) {
                var team = teams[TeamIndex];
                string teamName = tm.GetTranslation(team.nameTranslationKey);
                text = tm.GetTranslationWithReplacements("ui.inroom.player.changeteam",
                    "team", (Settings.Instance.GraphicsColorblind ? team.textSpriteColorblind : team.textSpriteNormal) + teamName);
            } else {
                text = tm.GetTranslation("ui.inroom.player.changeteam.unlock");
            }
            
            label.text = text;
        }
    }
}
