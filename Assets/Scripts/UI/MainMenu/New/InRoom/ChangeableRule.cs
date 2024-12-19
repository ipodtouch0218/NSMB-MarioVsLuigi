using NSMB.Translation;
using NSMB.UI.MainMenu;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChangeableRule : Selectable, ISubmitHandler, IPointerClickHandler {

    //---Properties
    public bool Editing {
        get => _editing;
        set {
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
    [SerializeField] protected CommandChangeRules.Rules ruleType;
    [SerializeField] private Color editingColor, inactiveColor;
    [SerializeField] private TMP_Text leftArrow, rightArrow;

    //---Private Variables
    protected bool _editing;
    protected object value;

    public void Initialize() {
        Editing = false;

        TranslationManager.OnLanguageChanged += OnLanguageChanged;
        QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
        QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public unsafe void OnSubmit(BaseEventData eventData) {
        if (Editing) {
            Editing = false;
            canvas.PlaySound(SoundEffect.UI_Back);
            return;
        }

        QuantumGame game = NetworkHandler.Game;
        PlayerRef host = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);
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
            /*
        default:
            base.OnMove(eventData);
            break;
            */
        }
    }

    public void OnPointerClick(PointerEventData eventData) {
        Editing = true;
    }

    public void IncreaseValue() {
        IncreaseValueInternal();
        UpdateState();
    }

    public void DecreaseValue() {
        DecreaseValueInternal();
        UpdateState();
    }

    protected virtual void IncreaseValueInternal() { }

    protected virtual void DecreaseValueInternal() { }

    public override void OnDeselect(BaseEventData eventData) {
        base.OnDeselect(eventData);
        Editing = false;
    }

    public void UpdateState() {
        UpdateLabel();
        leftArrow.enabled = Editing && CanDecreaseValue;
        rightArrow.enabled = Editing && CanIncreaseValue;
    }

    protected virtual void UpdateLabel() {
        label.text = labelPrefix + value.ToString();
    }

    private void FindValue(in GameRules rules) {
        value = ruleType switch {
            CommandChangeRules.Rules.Level => rules.Level,
            CommandChangeRules.Rules.StarsToWin => rules.StarsToWin,
            CommandChangeRules.Rules.CoinsForPowerup => rules.CoinsForPowerup,
            CommandChangeRules.Rules.Lives => rules.Lives,
            CommandChangeRules.Rules.TimerSeconds => rules.TimerSeconds,
            CommandChangeRules.Rules.DrawOnTimeUp => rules.DrawOnTimeUp,
            CommandChangeRules.Rules.CustomPowerupsEnabled => (bool) rules.CustomPowerupsEnabled,
            CommandChangeRules.Rules.TeamsEnabled => (bool) rules.TeamsEnabled,
            _ => null
        };

        UpdateState();
    }

    private unsafe void OnRulesChanged(EventRulesChanged e) {
        FindValue(e.Frame.Global->Rules);
    }

    private unsafe void OnGameStarted(CallbackGameStarted e) {
        FindValue(e.Game.Frames.Predicted.Global->Rules);
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateState();
    }
}
