using NSMB.Extensions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ReplayUITab : Selectable {

    //---Serialized Variables
    [SerializeField] private ReplayUI parent;
    [SerializeField] protected List<TMP_Text> selectables;
    [SerializeField] private bool horizontalMovement;
    [SerializeField] public GameObject defaultSelection, selectOnClose;

    [SerializeField] private Color deselectedColor = Color.gray, selectedColor = Color.white;

    //---Private Variables
    private int cursor;

    protected override void OnValidate() {
        base.OnValidate();
        this.SetIfNull(ref parent, UnityExtensions.GetComponentType.Parent);
    }

    protected override void OnEnable() {
        base.OnEnable();

#if UNITY_EDITOR
        if (Application.isEditor) {
            return;
        }
#endif

        cursor = 0;

        Settings.Controls.UI.Submit.performed += OnSubmit;
        Settings.Controls.UI.Cancel.performed += OnCancel;
        GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_WindowOpen);
        IncrementOption(0);
    }

    protected override void OnDisable() {
        base.OnDisable();

#if UNITY_EDITOR
        if (Application.isEditor) {
            return;
        }
#endif

        Settings.Controls.UI.Submit.performed -= OnSubmit;
        Settings.Controls.UI.Cancel.performed -= OnCancel;
        GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_WindowClose);
    }

    public override void OnMove(AxisEventData eventData) {
        switch (eventData.moveDir) {
        case MoveDirection.Left:
            if (!horizontalMovement) {
                break;
            }
            IncrementOption(-1);
            break;
        case MoveDirection.Right:
            if (!horizontalMovement) {
                break;
            }
            IncrementOption(1);
            break;
        case MoveDirection.Up:
            if (horizontalMovement) {
                break;
            }
            IncrementOption(-1);
            break;
        case MoveDirection.Down:
            if (horizontalMovement) {
                break;
            }
            IncrementOption(1);
            break;
        }
    }

    public void IncrementOption(int value) {
        int newPos = cursor + value;
        if (newPos < 0 && newPos >= selectables.Count) {
            return;
        }
        
        for (int i = 0; i < selectables.Count; i++) {
            selectables[i].color = deselectedColor;
        }
        selectables[newPos].color = selectedColor;
        cursor = newPos;
    }

    private void OnSubmit(InputAction.CallbackContext context) {
        
    }

    private void OnCancel(InputAction.CallbackContext context) {
        parent.CloseTab();
    }
}
