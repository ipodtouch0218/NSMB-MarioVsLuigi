using NSMB.UI.Elements;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NSMB.UI.MainMenu {
    public class OnScreenKeyboard : MonoBehaviour {

        //---Properties
        public bool IsOpen => keyboardPanel.activeSelf;

        //---Serialized Variables
        [SerializeField] private GameObject keyboardPanel; 
        [SerializeField] private GameObject rowTemplate, letterTemplate;
        [SerializeField] private Color selectedColor = Color.white, deselectedColor = Color.gray, disabledColor = Color.black;
        [SerializeField] private EventSystem eventSystem;

        //---Private Variables
        private InputActionAsset actionAsset;
        private List<KeyboardRow> rows = new();
        private TMP_InputField inputField;
        private string disabledChars = "";
        private Vector2Int selectedCharacter;
        private bool usingGamepad;
        private bool up, right, down, left;

        public void OnValidate() {
            this.SetIfNull(ref eventSystem, UnityExtensions.GetComponentType.Parent);
        }

        public void OnEnable() {
            actionAsset = Settings.Controls.asset;
            foreach (var actionMap in actionAsset.actionMaps) {
                actionMap.actionTriggered += OnActionTriggered;
            }
            Settings.Controls.UI.Submit.performed += OnSubmit;
            Settings.Controls.UI.Cancel.performed += OnCancel;
        }

        public void OnDisable() {
            foreach (var actionMap in actionAsset.actionMaps) {
                actionMap.actionTriggered -= OnActionTriggered;
            }
            Settings.Controls.UI.Submit.performed -= OnSubmit;
            Settings.Controls.UI.Cancel.performed -= OnCancel;
        }

        public void Update() {
            if (IsOpen) {
                if (!inputField.isFocused) {
                    inputField.ActivateInputField();
                }
                //eventSystem.SetSelectedGameObject(inputField.gameObject);
            }
            GameObject selection = eventSystem.currentSelectedGameObject;
            OnScreenKeyboardTrigger trigger = null;
            if (selection) {
                trigger = selection.GetComponent<OnScreenKeyboardTrigger>();
            }

            if (IsOpen && !trigger) {
                Close();
            }
        }

        public void OpenIfNeeded(TMP_InputField inputField, string[] newRows, string disabledChars) {
            if (!usingGamepad || IsOpen) {
                return;
            }

            this.inputField = inputField;
            this.disabledChars = disabledChars;

            foreach (var row in rows) {
                Destroy(row.GameObject);
            }
            rows.Clear();

            float totalWidth = ((RectTransform) (rowTemplate.transform.parent)).rect.width;
            int maxCharCount = 0;

            for (int y = 0; y < newRows.Length; y++) {
                string row = newRows[y];
                KeyboardRow newRow = new KeyboardRow {
                    GameObject = Instantiate(rowTemplate, rowTemplate.transform.parent),
                    Characters = new(),
                };
                rows.Add(newRow);
                newRow.GameObject.SetActive(true);

                int rowCharCount = 0;
                for (int x = 0; x < row.Length; x++) {
                    char character = row[x];
                    GameObject newLetter = Instantiate(letterTemplate, newRow.GameObject.transform);
                    newLetter.SetActive(true);
                    newRow.Characters.Add(new KeyboardCharacter {
                        GameObject = newLetter,
                        Character = character,
                    });

                    var text = newLetter.GetComponentInChildren<TMP_Text>();
                    text.text = GetDisplayString(character);
                    if (disabledChars.Contains(character)) {
                        text.color = disabledColor;
                    } else {
                        text.color = deselectedColor;
                    }

                    var clickable = newLetter.GetComponentInChildren<Clickable>();
                    Vector2Int position = new(x, y);
                    clickable.OnClick.AddListener(() => {
                        TypeCharacter(position);
                    });

                    rowCharCount++;
                }
                maxCharCount = Mathf.Max(maxCharCount, rowCharCount);
            }

            if (maxCharCount > 0) {
                float widthPerCharacter = totalWidth / maxCharCount;
                Vector2 sizeDelta = new(widthPerCharacter, 0);
                for (int i = 0; i < rows.Count; i++) {
                    var row = rows[i];

                    RectTransform spacer = (RectTransform) row.GameObject.transform.GetChild(0);
                    spacer.sizeDelta = new(widthPerCharacter * (i / 3f), 0);
                    spacer.gameObject.SetActive(true);

                    foreach (var character in row.Characters) {
                        RectTransform characterRect = (RectTransform) character.GameObject.transform;
                        characterRect.sizeDelta = sizeDelta;
                    }
                }
            }

            Settings.Controls.UI.Navigate.performed += OnNavigate;
            Settings.Controls.UI.Navigate.canceled += OnNavigate;
            keyboardPanel.SetActive(true);
            SetSelection(Vector2Int.zero);
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_WindowOpen);
        }

        public void Close() {
            if (!IsOpen) {
                return;
            }
            inputField = null;
            keyboardPanel.SetActive(false);
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_WindowClose);
        }

        public void SetSelection(Vector2Int newSelection) {
            KeyboardCharacter newCharacter = rows[newSelection.y].Characters[newSelection.x];
            if (disabledChars.Contains(newCharacter.Character)) {
                return;
            }

            KeyboardCharacter previousCharacter = rows[selectedCharacter.y].Characters[selectedCharacter.x];
            previousCharacter.GameObject.GetComponent<TMP_Text>().color = deselectedColor;

            newCharacter.GameObject.GetComponent<TMP_Text>().color = selectedColor;
            selectedCharacter = newSelection;
        }

        public void MoveHorizontally(int x) {
            int newX = selectedCharacter.x;

            KeyboardCharacter previousCharacter = rows[selectedCharacter.y].Characters[selectedCharacter.x];
            KeyboardCharacter newCharacter = null;

            int newY = selectedCharacter.y;
            do {
                newX += x;
                if (newX < 0) {
                    return;
                }
                if (newX >= rows[newY].Characters.Count) {
                    if (--newY < 0) {
                        return;
                    }
                }
                newCharacter = rows[newY].Characters[newX];
            } while (disabledChars.Contains(newCharacter.Character));

            previousCharacter.GameObject.GetComponent<TMP_Text>().color = deselectedColor;
            newCharacter.GameObject.GetComponent<TMP_Text>().color = selectedColor;
            selectedCharacter = new(newX, newY);
        }

        public void MoveVertically(int y) {
            int newY = selectedCharacter.y;

            KeyboardRow row = rows[selectedCharacter.y];
            KeyboardCharacter previousCharacter = row.Characters[selectedCharacter.x];
            KeyboardCharacter newCharacter = null;

            int newX = selectedCharacter.x;
            do {
                newY += y;
                if (newY < 0 || newY >= rows.Count) {
                    return;
                }
                newX = Mathf.Min(selectedCharacter.x, rows[newY].Characters.Count - 1);
                newCharacter = rows[newY].Characters[newX];
            } while (disabledChars.Contains(newCharacter.Character));

            previousCharacter.GameObject.GetComponent<TMP_Text>().color = deselectedColor;
            newCharacter.GameObject.GetComponent<TMP_Text>().color = selectedColor;
            selectedCharacter = new(newX, newY);
        }

        public void TypeCharacter(Vector2Int pos) {
            string text = inputField.text;
            char c = rows[pos.y].Characters[pos.x].Character;

            if (c == '\b') {
                if (text.Length == 0) {
                    return;
                }
                text = text[0..(text.Length-1)];
            } else {
                if (inputField.onValidateInput != null) {
                    c = inputField.onValidateInput(text, text.Length, c);
                }

                if (c != 0) {
                    text += c;
                }
                if (inputField.characterLimit > 0) {
                    text = text[0..Mathf.Min(text.Length, inputField.characterLimit)];
                }
            }

            if (text != inputField.text) {
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Chat_FullType);
            }
            inputField.text = text;
            inputField.caretPosition = text.Length;
        }

        public void OnSubmit(InputAction.CallbackContext context) {
            if (!IsOpen) {
                return;
            }

            TypeCharacter(selectedCharacter);
        }

        public void OnNavigate(InputAction.CallbackContext context) {
            if (!IsOpen) {
                return;
            }

            Vector2 vec = context.ReadValue<Vector2>();
            float deadzone = 0.35f;

            if (vec.magnitude < deadzone) {
                up = false;
                right = false;
                down = false;
                left = false;
                return;
            }

            float sqrt2over2 = Mathf.Sqrt(2f) / 2f;
            bool u = Vector2.Dot(vec, Vector2.up) >= sqrt2over2;
            bool r = Vector2.Dot(vec, Vector2.right) > sqrt2over2;
            bool d = Vector2.Dot(vec, Vector2.down) > sqrt2over2;
            bool l = Vector2.Dot(vec, Vector2.left) > sqrt2over2;

            if (u && !up) {
                MoveVertically(-1);
            }
            up = u;

            if (r && !right) {
                MoveHorizontally(1);
            }
            right = r;

            if (d && !down) {
                MoveVertically(1);
            }
            down = d;

            if (l && !left) {
                MoveHorizontally(-1);
            }
            left = l;
        }

        public void OnCancel(InputAction.CallbackContext context) {
            if (!IsOpen) {
                return;
            }

            Close();
        }

        private void OnActionTriggered(InputAction.CallbackContext obj) {
            if (!obj.control.noisy && obj.control.device.name != "Mouse") {;
                usingGamepad = obj.control.device.name != "Keyboard";
            }
        }

        private static string GetDisplayString(char c) {
            return c switch {
                '\b' => "<<pos=20%>-",
                _ => c.ToString()
            };
        }

        private class KeyboardRow {
            public GameObject GameObject;
            public List<KeyboardCharacter> Characters;
        }

        private class KeyboardCharacter {
            public GameObject GameObject;
            public char Character;
        }
    }
}