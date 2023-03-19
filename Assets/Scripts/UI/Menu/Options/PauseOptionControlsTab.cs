using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Pause.Options {
    public class PauseOptionControlsTab : PauseOptionTab {

        //---Serialized Variables
        [SerializeField] private NonselectableOption spacerTemplate;
        [SerializeField] private RebindOption controlTemplate;
        [SerializeField] private NonselectableOption headerTemplate;
        [SerializeField] private InputActionAsset controls;

        [SerializeField] private Transform scrollPaneContent;

        //---Private Variables
        private bool initialized;

        public override void Selected() {
            if (!initialized) {
                // Create options from controls
                CreateActions();
                initialized = true;
            }

            base.Selected();
        }

        public override void Deselected() {
            if (!initialized) {
                // Create options from controls
                CreateActions();
                initialized = true;
            }

            base.Deselected();
        }

        private void CreateActions() {

            for (int i = 0; i < controls.actionMaps.Count; i++) {
                InputActionMap map = controls.actionMaps[i];

                // Controls starting with '!' are not rebindable
                if (map.name.StartsWith("!"))
                    continue;

                NonselectableOption newHeader = Instantiate(headerTemplate);
                newHeader.name = "ControlsHeader (" + map.name + ")";
                newHeader.gameObject.SetActive(true);
                newHeader.transform.SetParent(scrollPaneContent, false);
                newHeader.label.text = map.name;

                options.Add(newHeader);

                foreach (InputAction action in map.actions) {

                    // Controls starting with '!' are not rebindable
                    if (action.name.StartsWith("!"))
                        continue;

                    RebindOption newOption = Instantiate(controlTemplate);
                    newOption.name = "ControlsButton (" + map.name + ", " + action.name + ")";
                    newOption.transform.SetParent(scrollPaneContent, false);
                    newOption.action = action;
                    newOption.gameObject.SetActive(true);

                    options.Add(newOption);
                }


                if (i < controls.actionMaps.Count - 1) {
                    NonselectableOption newSpacer = Instantiate(spacerTemplate);
                    newSpacer.name = "ControlsSpacer";
                    newSpacer.transform.SetParent(scrollPaneContent, false);
                    newSpacer.gameObject.SetActive(true);

                    options.Add(newSpacer);
                }

                //if (map.name == "Player")
                //    playerSettings.transform.SetAsLastSibling();
            }
        }

        public void SaveRebindings() {
            string json = controls.SaveBindingOverridesAsJson();

            if (!ControlSystem.file.Exists)
                ControlSystem.file.Directory.Create();

            //File.WriteAllText(ControlSystem.file.FullName, json);
            GlobalController.Instance.controlsJson = json;
        }
    }
}

