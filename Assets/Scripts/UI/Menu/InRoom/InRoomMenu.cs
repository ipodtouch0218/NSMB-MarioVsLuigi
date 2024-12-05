using Quantum;
using UnityEngine;
using UnityEngine.InputSystem;

public class InRoomMenu : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private InRoomPanel defaultSelectedPanel;

    //---Private Variables
    private InRoomPanel selectedPanel;

    public void Initialize() {
        QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
    }

    public void OnEnable() {
        ControlSystem.controls.UI.Next.performed += OnNextPerformed;
        ControlSystem.controls.UI.Previous.performed += OnPreviousPerformed;

        selectedPanel = defaultSelectedPanel;
        selectedPanel.Select();
    }

    public void OnDisable() {
        ControlSystem.controls.UI.Next.performed -= OnNextPerformed;
        ControlSystem.controls.UI.Previous.performed -= OnPreviousPerformed;
    }

    private void OnNextPerformed(InputAction.CallbackContext context) {
        if (!context.performed) {
            return;
        }

        if (selectedPanel.rightPanel) {
            selectedPanel.Deselect();
            selectedPanel = selectedPanel.rightPanel;
            selectedPanel.Select();
        }
    }

    private void OnPreviousPerformed(InputAction.CallbackContext context) {
        if (!context.performed) {
            return;
        }

        if (selectedPanel.leftPanel) {
            selectedPanel.Deselect();
            selectedPanel = selectedPanel.leftPanel;
            selectedPanel.Select();
        }
    }

    private void OnGameStarted(CallbackGameStarted e) {

    }
}