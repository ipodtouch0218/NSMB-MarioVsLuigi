using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FluidMidi
{
    [CustomPropertyDrawer(typeof(BitFieldAttribute))]
    public class BitFieldPropertyDrawer : PropertyDrawer
    {
        const int BYTES = 2;
        const int BITS = 8;
        const string BIT_CONTROL_PREFIX = "bit";

        class State
        {
            public bool open;
        }

        readonly Dictionary<string, State> states = new Dictionary<string, State>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (GetState(property).open)
            {
                height *= 3;
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            State state = GetState(property);
            EditorGUI.BeginProperty(position, label, property);
            position.yMax = position.yMin + EditorGUIUtility.singleLineHeight;
            state.open = EditorGUI.Foldout(position, state.open, label, false);
            if (state.open)
            {
                position.yMin += EditorGUIUtility.singleLineHeight;
                position.yMax += EditorGUIUtility.singleLineHeight;
                int value = property.intValue;
                EditorGUI.BeginChangeCheck();
                float xMin = position.xMin;
                float height = EditorGUIUtility.singleLineHeight;
                float width = position.width / BITS;
                position.height = height;
                position.width = width;
                int index = 1;
                int bit = 1;
                for (int row = 0; row < BYTES; ++row)
                {
                    for (int col = 0; col < BITS; ++col)
                    {
                        GUI.SetNextControlName(BIT_CONTROL_PREFIX + index);
                        if (EditorGUI.ToggleLeft(position, index.ToString(), (value & bit) != 0))
                        {
                            value |= bit;
                        }
                        else
                        {
                            value &= ~bit;
                        }
                        position.xMax += width;
                        position.xMin += width;
                        ++index;
                        bit <<= 1;
                    }
                    position.xMin = xMin;
                    position.xMax = xMin + width;
                    position.yMax += height;
                    position.yMin += height;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    property.intValue = value;
                }
                HandleArrowKeys();
            }
            EditorGUI.EndProperty();
        }

        private State GetState(SerializedProperty property)
        {
            string path = property.propertyPath;
            State state;
            if (!states.TryGetValue(path, out state))
            {
                state = new State();
                states.Add(path, state);
            }
            return state;
        }

        private void HandleArrowKeys()
        {
            string focusedControlName = GUI.GetNameOfFocusedControl();
            if (focusedControlName.StartsWith(BIT_CONTROL_PREFIX))
            {
                if (Event.current.type == EventType.KeyDown)
                {
                    int focusedIndex = int.Parse(focusedControlName.Substring(BIT_CONTROL_PREFIX.Length));
                    switch (Event.current.keyCode)
                    {
                        case KeyCode.UpArrow:
                            if (focusedIndex > BITS)
                            {
                                GUI.FocusControl(BIT_CONTROL_PREFIX + (focusedIndex - BITS));
                                GUI.changed = true;
                                Event.current.Use();
                            }
                            break;
                        case KeyCode.DownArrow:
                            if (focusedIndex <= BITS)
                            {
                                GUI.FocusControl(BIT_CONTROL_PREFIX + (focusedIndex + BITS));
                                GUI.changed = true;
                                Event.current.Use();
                            }
                            break;
                        case KeyCode.LeftArrow:
                            if (focusedIndex % BITS != 1)
                            {
                                GUI.FocusControl(BIT_CONTROL_PREFIX + (focusedIndex - 1));
                                GUI.changed = true;
                                Event.current.Use();
                            }
                            break;
                        case KeyCode.RightArrow:
                            if (focusedIndex % BITS != 0)
                            {
                                GUI.FocusControl(BIT_CONTROL_PREFIX + (focusedIndex + 1));
                                GUI.changed = true;
                                Event.current.Use();
                            }
                            break;
                    }
                }
            }
        }
    }
}
