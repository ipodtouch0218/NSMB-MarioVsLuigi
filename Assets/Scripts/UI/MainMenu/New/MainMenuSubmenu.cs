using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu {
    public class MainMenuSubmenu : MonoBehaviour {

        //---Properties
        public virtual string Header => GlobalController.Instance.translationManager.GetTranslation(header);
        public virtual float BackHoldTime => backHoldTime;

        //---Serialized Variables
        [SerializeField] public bool ShowHeader = true;
        [SerializeField] public bool HideInBackground = true;
        [SerializeField] public bool RequiresMainPanel = true;
        [SerializeField] private string header;
        [SerializeField] private float backHoldTime = 0;
        [SerializeField] private GameObject defaultSelection;

        [Header("Events")]
        [SerializeField] private UnityAction OnInitialize;
        [SerializeField] private UnityAction<bool> OnShow, OnHide;

        //---Private Variables
        protected MainMenuCanvas canvas;
        private GameObject savedSelection;

        public virtual void Initialize(MainMenuCanvas canvas) {
            this.canvas = canvas;
            OnInitialize?.Invoke();
        }

        public virtual void Show(bool first) {
            gameObject.SetActive(true);
            EventSystem.current.SetSelectedGameObject(first ? defaultSelection : savedSelection);

            OnShow?.Invoke(first);
        }

        public virtual void Hide(bool background) {
            savedSelection = EventSystem.current.currentSelectedGameObject;
            if (!background || HideInBackground) {
                gameObject.SetActive(false);
            }

            OnHide?.Invoke(background);
        }

        public virtual bool TryGoBack(out bool playSound) {
            gameObject.SetActive(false);
            playSound = true;
            return true;
        }
    }
}
