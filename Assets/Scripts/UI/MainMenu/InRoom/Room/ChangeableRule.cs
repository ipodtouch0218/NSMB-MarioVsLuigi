using NSMB.UI.Translation;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ChangeableRule : Selectable, ISubmitHandler, IPointerClickHandler, IScrollHandler {

        //---Properties
        public bool Editing {
            get => _editing || (!clickToEdit && canvas.EventSystem.currentSelectedGameObject == gameObject);
            set {
                if (!clickToEdit) {
                    return;
                }
                label.color = value ? editingColor : inactiveColor;
                _editing = value;
                UpdateState();
            }
        }
        public virtual bool CanIncreaseValue => true;
        public virtual bool CanDecreaseValue => true;

        //---Serialized Variables
        [SerializeField] protected MainMenuCanvas canvas;
        [SerializeField] protected TMP_Text label;
        [SerializeField] protected string labelPrefix;
        [SerializeField] public CommandChangeRules.Rules ruleType;
        [SerializeField] private bool clickToEdit;
        [SerializeField] protected bool dontAutosave;
        [SerializeField] private Color editingColor, inactiveColor;
        [SerializeField] private TMP_Text leftArrow, rightArrow;
        [SerializeField] protected AudioSource cursorSfx;

        //---Private Variables
        protected bool _editing;
        protected object value;

        public void Initialize() {
            Editing = false;

            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        protected override void OnDisable() {
            base.OnDisable();
            OnDeselect(null);
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            if (clickToEdit && Editing) {
                Editing = false;
                canvas.PlaySound(SoundEffect.UI_Back);
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                return;
            }

            canvas.PlayConfirmSound();
            Editing = true;
        }

        public override void OnMove(AxisEventData eventData) {
            if (!Editing) {
                base.OnMove(eventData);
                return;
            }

            switch (eventData.moveDir) {
            case MoveDirection.Right:
                IncreaseValue();
                break;
            case MoveDirection.Left:
                DecreaseValue();
                break;
            default:
                if (!clickToEdit) {
                    base.OnMove(eventData);
                }
                break;
            }
        }

        public unsafe void OnPointerClick(PointerEventData eventData) {
            if (!interactable) {
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                canvas.PlaySound(SoundEffect.UI_Error);
                return;
            }

            Editing = true;
        }

        public void OnScroll(PointerEventData eventData) {
            if (!Editing) {
                return;
            }

            if (eventData.scrollDelta.y > 0) {
                IncreaseValue(false);
            } else if (eventData.scrollDelta.y < 0) {
                DecreaseValue(false);
            }
        }

        public unsafe void IncreaseValue(bool playSound = true) {
            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                if (playSound) {
                    canvas.PlaySound(SoundEffect.UI_Error);
                }
                return;
            }

            IncreaseValueInternal();
            UpdateState();
        }

        public unsafe void DecreaseValue(bool playSound = true) {
            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                if (playSound) {
                    canvas.PlaySound(SoundEffect.UI_Error);
                }
                return;
            }

            DecreaseValueInternal();
            UpdateState();
        }

        protected virtual void IncreaseValueInternal() { }

        protected virtual void DecreaseValueInternal() { }

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
            UpdateState();
        }

        public override void OnDeselect(BaseEventData eventData) {
            base.OnDeselect(eventData);
            Editing = false;
            UpdateState();
            if (leftArrow) {
                leftArrow.enabled = false;
            }
            if (rightArrow) {
                rightArrow.enabled = false;
            }
        }

        public void UpdateState() {
#if UNITY_EDITOR
            if (!this) {
                return;
            }
#endif

            UpdateLabel();
            try {
                leftArrow.enabled = Editing && CanDecreaseValue;
                rightArrow.enabled = Editing && CanIncreaseValue;
            } catch { /* bodge */ }
        }

        protected virtual void UpdateLabel() {
            label.text = labelPrefix + value.ToString();
        }

        private void FindValue(in GameRules rules) {
            value = ruleType switch {
                CommandChangeRules.Rules.Stage => rules.Stage,
                CommandChangeRules.Rules.Gamemode => rules.Gamemode,
                CommandChangeRules.Rules.StarsToWin => rules.StarsToWin,
                CommandChangeRules.Rules.CoinsForPowerup => rules.CoinsForPowerup,
                CommandChangeRules.Rules.Lives => rules.Lives,
                CommandChangeRules.Rules.TimerMinutes => rules.TimerMinutes,
                CommandChangeRules.Rules.DrawOnTimeUp => rules.DrawOnTimeUp,
                CommandChangeRules.Rules.CustomPowerupsEnabled => (bool) rules.CustomPowerupsEnabled,
                CommandChangeRules.Rules.TeamsEnabled => (bool) rules.TeamsEnabled,
                _ => null
            };

            UpdateState();
        }

        private unsafe void OnRulesChanged(EventRulesChanged e) {
            FindValue(e.Game.Frames.Predicted.Global->Rules);
        }

        private unsafe void OnGameStarted(CallbackGameStarted e) {
            FindValue(e.Game.Frames.Predicted.Global->Rules);
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateState();
        }
    }
}
