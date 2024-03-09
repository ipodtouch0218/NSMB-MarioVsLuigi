using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

using NSMB.Extensions;

namespace NSMB.UI.MainMenu {

    public class SoundInputField : TMP_InputField {

        //---Private Variables
        private readonly Event processingEvent = new();
        private FieldInfo compositionActive;
        private PropertyInfo compositionLength;
        private new FieldInfo wasCanceled;
        private FieldInfo isTextComponentUpdateRequired;

        protected override void Start() {
            // This is bs, why are they not protected...
            compositionActive = typeof(TMP_InputField).GetField("m_IsCompositionActive", BindingFlags.Instance | BindingFlags.NonPublic);
            compositionLength = typeof(TMP_InputField).GetProperty("compositionLength", BindingFlags.Instance | BindingFlags.NonPublic);
            wasCanceled = typeof(TMP_InputField).GetField("m_WasCanceled", BindingFlags.Instance | BindingFlags.NonPublic);
            isTextComponentUpdateRequired = typeof(TMP_InputField).GetField("m_IsTextComponentUpdateRequired", BindingFlags.Instance | BindingFlags.NonPublic);

            base.Start();
        }

        public override void OnUpdateSelected(BaseEventData eventData) {
            if (!isFocused) {
                return;
            }

            bool consumedEvent = false;
            EditState shouldContinue;

            while (Event.PopEvent(processingEvent)) {
                //Debug.Log("Event: " + m_ProcessingEvent.ToString() + "  IsCompositionActive= " + m_IsCompositionActive + "  Composition Length: " + compositionLength);

                switch (processingEvent.rawType) {
                case EventType.KeyUp:
                    // TODO: Figure out way to handle navigation during IME Composition.
                    MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Chat_KeyUp, null, 0, 0.3f);

                    break;


                case EventType.KeyDown:
                    consumedEvent = true;
                    if (processingEvent.keyCode != KeyCode.Return) {
                        MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Chat_KeyDown, null, 0, 0.3f);
                    }

                    // Special handling on OSX which produces more events which need to be suppressed.
                    if ((bool) compositionActive.GetValue(this) && (int) compositionLength.GetValue(this) == 0) {
                        //if (m_ProcessingEvent.keyCode == KeyCode.Backspace && m_ProcessingEvent.modifiers == EventModifiers.None)
                        //{
                        //    int eventCount = Event.GetEventCount();

                        //    // Suppress all subsequent events
                        //    for (int i = 0; i < eventCount; i++)
                        //        Event.PopEvent(m_ProcessingEvent);

                        //    break;
                        //}

                        // Suppress other events related to navigation or termination of composition sequence.
                        if (processingEvent.character == 0 && processingEvent.modifiers == EventModifiers.None) {
                            break;
                        }
                    }

                    shouldContinue = KeyPressed(processingEvent);
                    if (shouldContinue == EditState.Finish) {
                        if (!(bool) wasCanceled.GetValue(this)) {
                            SendOnSubmit();
                        }

                        DeactivateInputField();
                        break;
                    }

                    isTextComponentUpdateRequired.SetValue(this, true);
                    UpdateLabel();

                    break;

                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                    switch (processingEvent.commandName) {
                    case "SelectAll":
                        SelectAll();
                        consumedEvent = true;
                        break;
                    }
                    break;
                }
            }

            if (consumedEvent) {
                UpdateLabel();
            }

            eventData.Use();
        }

    }
}
