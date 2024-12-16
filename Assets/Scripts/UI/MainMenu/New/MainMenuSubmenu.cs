using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu {
    public class MainMenuSubmenu : MonoBehaviour {

        //---Properties
        public MainMenuCanvas Canvas { get; private set; }
        public virtual string Header => GlobalController.Instance.translationManager.GetTranslation(header);
        public virtual Color? HeaderColor => useHeaderColor ? headerColor : null;
        public virtual float BackHoldTime => backHoldTime;

        //---Serialized Variables
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

        public virtual void Initialize(MainMenuCanvas canvas) {
            Canvas = canvas;
            OnInitialize?.Invoke();
        }

        public virtual void Show(bool first) {
            gameObject.SetActive(true);
            EventSystem.current.SetSelectedGameObject(first ? defaultSelection : savedSelection);

            OnShow?.Invoke(first);
        }

        public virtual void Hide(SubmenuHideReason hideReason) {
            savedSelection = EventSystem.current.currentSelectedGameObject;
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
