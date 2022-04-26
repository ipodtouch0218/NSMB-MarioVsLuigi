using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputSystem : MonoBehaviour {

    GameManager manager;
    PlayerController controller;

    public void Start() {
        manager = GameManager.Instance;
        PlayerInput input = GetComponent<PlayerInput>();

        if (GlobalController.Instance.controlsJson != null)
            input.actions.LoadBindingOverridesFromJson(GlobalController.Instance.controlsJson);
    }

    private bool CheckForPlayer() {
        if (!controller && manager.localPlayer)
            controller = manager.localPlayer.GetComponent<PlayerController>();
        return controller;
    }

    protected void OnMovement(InputValue value) {
        if (!CheckForPlayer())
            return;

        controller.OnMovement(value);
    }

    protected void OnJump(InputValue value) {
        if (!CheckForPlayer())
            return;

        controller.OnJump(value);
    }

    protected void OnSprint(InputValue value) {
        if (!CheckForPlayer())
            return;

        controller.OnSprint(value);
    }

    protected void OnPowerupAction(InputValue value) {
        if (!CheckForPlayer())
            return;

        controller.OnPowerupAction(value);
    }


    protected void OnReserveItem() {
        if (!CheckForPlayer())
            return;

        controller.OnReserveItem();
    }

    protected void OnPause() {
        GameManager.Instance.Pause();
    }
}