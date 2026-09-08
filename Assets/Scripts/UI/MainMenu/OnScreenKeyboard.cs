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
        public static OnScreenKeyboard Instance { get; private set; }
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
        private Vector2Int selectedCharacter;
        private bool usingGamepad;
        private bool up, right, down, left;

        public void OnValidate() {
            this.SetIfNull(ref eventSystem, UnityExtensions.GetComponentType.Parent);
        }

        public void OnEnable() {
            Instance = this;
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
            if (!IsOpen) {
                return;
            }

            if (!inputField.isFocused) {
                inputField.ActivateInputField();
            }

            GameObject selection = eventSystem.currentSelectedGameObject;
            if (selection && !selection.TryGetComponent(out OnScreenKeyboardTrigger _)) {
                Close();
            }
        }

        public void OpenIfNeeded(TMP_InputField inputField, string[] newRows, string disabledChars) {
            if (!usingGamepad || IsOpen) {
                return;
            }

            this.inputField = inputField;

            foreach (var row in rows) {
                Destroy(row.GameObject);
            }
            rows.Clear();

            RectTransform parent = (RectTransform) rowTemplate.transform.parent;
            float totalWidth = parent.rect.width;
            int maxCharCount = 0;

            for (int y = 0; y < newRows.Length; y++) {
                string row = newRows[y];
                KeyboardRow newRow = new KeyboardRow {
                    GameObject = Instantiate(rowTemplate, parent),
                    Characters = new(),
                };
                rows.Add(newRow);
                newRow.GameObject.SetActive(true);

                int rowCharCount = 0;
                for (int x = 0; x < row.Length; x++) {
                    char character = row[x];
                    GameObject newLetter = Instantiate(letterTemplate, newRow.GameObject.transform);
                    newLetter.SetActive(true);
                    KeyboardCharacter keyboardCharacter = new KeyboardCharacter {
                        GameObject = newLetter,
                        Character = character,
                        IsDisabled = disabledChars.Contains(character),
                    };
                    newRow.Characters.Add(keyboardCharacter);

                    var text = newLetter.GetComponentInChildren<TMP_Text>();
                    text.text = GetDisplayString(character);
                    text.color = keyboardCharacter.IsDisabled ? disabledColor : deselectedColor;

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
            SetSelection(0, 0);
            GlobalController.Instance.PlaySound(SoundEffect.UI_WindowOpen);
        }

        public void Close() {
            if (!IsOpen) {
                return;
            }
            inputField = null;
            keyboardPanel.SetActive(false);
            Settings.Controls.UI.Navigate.performed -= OnNavigate;
            Settings.Controls.UI.Navigate.canceled -= OnNavigate;
            GlobalController.Instance.PlaySound(SoundEffect.UI_WindowClose);
        }

        public void SetSelection(int x, int y) {
            KeyboardCharacter previousCharacter = rows[selectedCharacter.y].Characters[selectedCharacter.x];
            previousCharacter.GameObject.GetComponent<TMP_Text>().color = previousCharacter.IsDisabled ? disabledColor : deselectedColor;

            KeyboardCharacter newCharacter = rows[y].Characters[x];
            newCharacter.GameObject.GetComponent<TMP_Text>().color = selectedColor;

            selectedCharacter = new Vector2Int(x, y);
        }

        public void MoveHorizontally(int x) {
            int newX = selectedCharacter.x;
            int newY = selectedCharacter.y;
            newX += x;
            if (newX < 0 || newX >= rows[newY].Characters.Count) {
                return;
            }

            SetSelection(newX, newY);
        }

        public void MoveVertically(int y) {
            int newX = selectedCharacter.x;
            int newY = selectedCharacter.y;
            newY += y;
            if (newY < 0 || newY >= rows.Count || newX >= rows[newY].Characters.Count) {
                return;
            }

            SetSelection(newX, newY);
        }

        public void TypeCharacter(Vector2Int pos) {
            string text = inputField.text;
            KeyboardCharacter keyboardCharacter = rows[pos.y].Characters[pos.x];
            if (keyboardCharacter.IsDisabled) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Error);
                return;
            }

            char c = keyboardCharacter.Character;

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
                GlobalController.Instance.PlaySound(SoundEffect.UI_Chat_FullType);
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
            try {
                if (!obj.control.noisy && obj.control.device.name != "Mouse") {
                    usingGamepad = obj.control.device.name != "Keyboard";
                }
            } catch { }
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
            public bool IsDisabled;
        }
    }
}