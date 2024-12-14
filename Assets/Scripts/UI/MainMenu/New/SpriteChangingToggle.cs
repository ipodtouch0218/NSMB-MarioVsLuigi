using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/SpriteChangingToggle", 30)]
[RequireComponent(typeof(RectTransform))]
public class SpriteChangingToggle : Selectable, IPointerClickHandler, IEventSystemHandler, ISubmitHandler, ICanvasElement {
    public enum ToggleTransition {
        None,
        Fade
    }

    [Serializable]
    public class ToggleEvent : UnityEvent<bool> {
    }

    public ToggleTransition toggleTransition = ToggleTransition.Fade;

    public Image graphic;

    public Sprite onSprite, offSprite;

    public ToggleEvent onValueChanged = new ToggleEvent();

    [Tooltip("Is the toggle currently on or off?")]
    [SerializeField]
    private bool m_IsOn;

    public bool isOn {
        get {
            return m_IsOn;
        }
        set {
            Set(value);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate() {
        base.OnValidate();
        if (!PrefabUtility.IsPartOfPrefabAsset(this) && !Application.isPlaying) {
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }
    }
#endif

    public virtual void Rebuild(CanvasUpdate executing) {
        if (executing == CanvasUpdate.Prelayout) {
            // onValueChanged.Invoke(m_IsOn);
        }
    }

    public virtual void LayoutComplete() {
    }

    public virtual void GraphicUpdateComplete() {
    }

    protected override void OnEnable() {
        base.OnEnable();
        PlayEffect(instant: true);
    }

    protected override void OnDisable() {
        base.OnDisable();
    }

    protected override void OnDidApplyAnimationProperties() {
        if (graphic != null) {
            bool flag = !Mathf.Approximately(graphic.canvasRenderer.GetColor().a, 0f);
            if (m_IsOn != flag) {
                m_IsOn = flag;
                Set(!flag);
            }
        }

        base.OnDidApplyAnimationProperties();
    }

    public void SetIsOnWithoutNotify(bool value) {
        Set(value, sendCallback: false);
    }

    private void Set(bool value, bool sendCallback = true) {
        if (m_IsOn != value) {
            m_IsOn = value;

            PlayEffect(toggleTransition == ToggleTransition.None);
            if (sendCallback) {
                UISystemProfilerApi.AddMarker("Toggle.value", this);
                onValueChanged.Invoke(m_IsOn);
            }
        }
    }

    private void PlayEffect(bool instant) {
        if (!(graphic == null)) {
            graphic.canvasRenderer.SetAlpha(1);
            graphic.sprite = m_IsOn ? onSprite : offSprite;
        }
    }

    protected override void Start() {
        PlayEffect(instant: true);
    }

    private void InternalToggle() {
        if (IsActive() && IsInteractable()) {
            isOn = !isOn;
        }
    }

    public virtual void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Left) {
            InternalToggle();
        }
    }

    public virtual void OnSubmit(BaseEventData eventData) {
        InternalToggle();
    }
}