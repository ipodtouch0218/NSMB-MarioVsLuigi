using NSMB.Utilities.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu {
    [RequireComponent(typeof(TMP_InputField))]
    public class OnScreenKeyboardTrigger : MonoBehaviour, ISubmitHandler {

        public TMP_InputField InputField;
        public string DisabledCharacters;

        public void OnValidate() {
            this.SetIfNull(ref InputField);
        }

        public void Start() {
            InputField.shouldActivateOnSelect = false;
        }

        public void OnSubmit(BaseEventData eventData) {
            OnScreenKeyboard kb = FindFirstObjectByType<OnScreenKeyboard>();
            kb.OpenIfNeeded(InputField, new string[] { "QWERTYUIOP\b", "ASDFGHJKL", "ZXCVBNM" }, DisabledCharacters);
        }
    }
}