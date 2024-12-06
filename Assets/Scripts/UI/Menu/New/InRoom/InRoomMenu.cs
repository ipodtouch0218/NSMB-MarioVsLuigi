using NSMB.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InRoomMenu : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private InRoomPanel defaultSelectedPanel;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private List<InRoomPanel> allPanels;
    [SerializeField] private GameObject colorPalettePicker;

    //---Private Variables
    private InRoomPanel selectedPanel;

    public void Initialize() {
        QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
    }

    public void OnValidate() {
        this.SetIfNull(ref sfx);
    }

    public void OnEnable() {
        ControlSystem.controls.UI.Next.performed += OnNextPerformed;
        ControlSystem.controls.UI.Previous.performed += OnPreviousPerformed;

        foreach (var panel in allPanels) {
            panel.Deselect();
        }
        selectedPanel = defaultSelectedPanel;
        selectedPanel.Select(true);
    }

    public void OnDisable() {
        ControlSystem.controls.UI.Next.performed -= OnNextPerformed;
        ControlSystem.controls.UI.Previous.performed -= OnPreviousPerformed;
    }

    public void SelectPanel(InRoomPanel panel) {
        SelectPanel(panel, true);
    }

    public void SelectPanel(InRoomPanel panel, bool setDefault) {
        if (panel == selectedPanel) {
            return;
        }

        if (selectedPanel) {
            selectedPanel.Deselect();
        }
        selectedPanel = panel;
        selectedPanel.Select(setDefault);
        
        // sfx.PlayOneShot(SoundEffect.UI_Cursor);
    }

    public void SelectPreviousPanel() {
        if (selectedPanel && selectedPanel.leftPanel) {
            SelectPanel(selectedPanel.leftPanel, true);
        }
    }

    public void SelectNextPanel() {
        if (selectedPanel && selectedPanel.rightPanel) {
            SelectPanel(selectedPanel.rightPanel, true);
        }
    }

    //---Buttons
    public void OnCharacterButtonClicked() {
        
    }

    //---Callbacks
    private void OnPreviousPerformed(InputAction.CallbackContext context) {
        if (!context.performed) {
            return;
        }

        SelectPreviousPanel();
    }

    private void OnNextPerformed(InputAction.CallbackContext context) {
        if (!context.performed) {
            return;
        }

        SelectNextPanel();
    }

    private void OnGameStarted(CallbackGameStarted e) {

    }
}