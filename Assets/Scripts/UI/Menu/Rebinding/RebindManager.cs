using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class RebindManager : MonoBehaviour {

    public InputActionAsset controls;
    public GameObject headerTemplate, buttonTemplate, axisTemplate;
    
    public void Start() {
        buttonTemplate.SetActive(false);
        axisTemplate.SetActive(false);
        headerTemplate.SetActive(false);
        CreateActions();
    }

    void CreateActions() {
        foreach (InputActionMap map in controls.actionMaps) {

            if (map.name.StartsWith("!"))
                continue;

            GameObject newHeader = Instantiate(headerTemplate);
            newHeader.name = map.name;
            newHeader.SetActive(true);
            newHeader.transform.SetParent(transform, true);
            newHeader.GetComponentInChildren<TMP_Text>().text = map.name;

            foreach (InputAction action in map.actions) {

                if (action.name.StartsWith("!"))
                    continue;

                if (action.bindings[0].isComposite) {
                    //axis
                    GameObject newTemplate = Instantiate(axisTemplate);
                    newTemplate.name = action.name;
                    newTemplate.transform.SetParent(transform, true);
                    newTemplate.SetActive(true);
                    RebindControl control = newTemplate.GetComponent<RebindControl>();
                    control.text.text = action.name;

                    int buttonIndex = 0;
                    for (int i = 0; i < action.bindings.Count; i++) {
                        InputBinding binding = action.bindings[i];
                        if (binding.isComposite)
                            continue;

                        RebindButton button = control.buttons[buttonIndex];
                        button.targetAction = action;
                        button.targetBinding = binding;
                        button.index = i;
                        buttonIndex++;
                    }

                } else {
                    //button
                    GameObject newTemplate = Instantiate(buttonTemplate);
                    newTemplate.name = action.name;
                    newTemplate.transform.SetParent(transform, true);
                    newTemplate.SetActive(true);
                    RebindControl control = newTemplate.GetComponent<RebindControl>();
                    control.text.text = action.name;
                    for (int i = 0; i < control.buttons.Length; i++) {
                        RebindButton button = control.buttons[i];
                        button.targetAction = action;
                        button.targetBinding = action.bindings[i];
                        button.index = i;
                    }
                }
            }
        }
    }
}