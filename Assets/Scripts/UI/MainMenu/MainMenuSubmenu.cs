using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.Events;

namespace NSMB.UI.MainMenu {
    public class MainMenuSubmenu : MonoBehaviour {

        //---Properties
        public MainMenuCanvas Canvas => canvas;
        public virtual string Header => GlobalController.Instance.translationManager.GetTranslation(header);
        public virtual Color? HeaderColor => useHeaderColor ? headerColor : null;
        public virtual float BackHoldTime => backHoldTime;
        public virtual GameObject DefaultSelection => defaultSelection;

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] public bool ShowHeader = true;
        [SerializeField] public bool IsOverlay = false;
        [SerializeField] public bool RequiresMainPanel = true;
        [SerializeField] private string header;
        [SerializeField] private float backHoldTime = 0;
        [SerializeField] private GameObject defaultSelection;
        [SerializeField] private bool useHeaderColor;
        [SerializeField] private Color headerColor;

        [Header("Events")]
        [SerializeField] private UnityAction OnInitialize;
        [SerializeField] private UnityAction<bool> OnShow;
        [SerializeField] private UnityAction<SubmenuHideReason> OnHide;

        //---Private Variables
        private GameObject savedSelection;

        public virtual void OnValidate() {
            this.SetIfNull(ref canvas, UnityExtensions.GetComponentType.Parent);
        }

        public virtual void Initialize() {
            OnInitialize?.Invoke();
        }

        public virtual void OnDestroy() {

        }

        internal void InternalShow(bool first) {
            Show(first);
            Canvas.EventSystem.SetSelectedGameObject(first ? DefaultSelection : savedSelection);
        }

        public virtual void Show(bool first) {
            gameObject.SetActive(true);
            OnShow?.Invoke(first);
        }

        public virtual void Hide(SubmenuHideReason hideReason) {
            savedSelection = Canvas.EventSystem.currentSelectedGameObject;
            gameObject.SetActive(hideReason == SubmenuHideReason.Overlayed);

            OnHide?.Invoke(hideReason);
        }

        public virtual bool TryGoBack(out bool playSound) {
            gameObject.SetActive(false);
            playSound = true;
            return true;
        }
    }
}
