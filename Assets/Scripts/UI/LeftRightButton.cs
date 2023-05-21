using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using NSMB.Game;

public class LeftRightButton : Selectable {

    //---Serialized Variables
    [SerializeField] private GameObject leftArrow;
    [SerializeField] private GameObject rightArrow;
    [SerializeField] private float cutoff = 0.6f;

    //---Private Variables
    private bool leftSelected, rightSelected;
    private float leftOffset, rightOffset;
    private RectTransform leftTransform, rightTransform;

    protected override void OnEnable() {
        base.OnEnable();
        ControlSystem.controls.Player.Movement.performed += OnNavigation;
        ControlSystem.controls.Player.Movement.canceled +=  OnNavigation;
    }

    protected override void OnDisable() {
        base.OnDisable();
        ControlSystem.controls.Player.Movement.performed -= OnNavigation;
        ControlSystem.controls.Player.Movement.canceled -=  OnNavigation;
    }

    protected override void Start() {
        base.Start();
        leftTransform = leftArrow.GetComponent<RectTransform>();
        rightTransform = rightArrow.GetComponent<RectTransform>();
        leftOffset = leftTransform.anchoredPosition.x;
        rightOffset = rightTransform.anchoredPosition.x;
        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void OnNavigation(InputAction.CallbackContext context) {
        if (GameManager.Instance.paused) {
            leftSelected = false;
            rightSelected = false;
            SetOffset(false, false);
            SetOffset(true, false);
            return;
        }

        Vector2 direction = context.ReadValue<Vector2>();
        if (Vector2.Dot(direction, Vector2.left) > cutoff) {
            //select left
            if (!leftSelected) {
                GameManager.Instance.spectationManager.SpectatePreviousPlayer();
                SetOffset(false, true);
            }
            leftSelected = true;
        } else if (leftSelected) {
            SetOffset(false, false);
            leftSelected = false;
        }

        if (Vector2.Dot(direction, Vector2.right) > cutoff) {
            //select left
            if (!rightSelected) {
                GameManager.Instance.spectationManager.SpectateNextPlayer();
                SetOffset(true, true);
            }
            rightSelected = true;
        } else if (rightSelected) {
            SetOffset(true, false);
            rightSelected = false;
        }
    }

    private void SetOffset(bool right, bool selected) {
        RectTransform transform = right ? rightTransform: leftTransform;
        float offset = right ? rightOffset : leftOffset;
        float selOffset = selected ? offset : 0;
        transform.anchoredPosition = new Vector2(offset + selOffset, transform.anchoredPosition.y);
    }
}
