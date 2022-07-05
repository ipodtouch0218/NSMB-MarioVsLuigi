using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FluidMidi
{
    [CustomPropertyDrawer(typeof(ToggleInt))]
    public class ToggleIntPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty enabledProperty = property.FindPropertyRelative("Enabled");
            SerializedProperty valueProperty = property.FindPropertyRelative("Value");
            bool enabled = enabledProperty.boolValue;
            int value = valueProperty.intValue;
            EditorGUI.BeginChangeCheck();
            float oldMax = position.xMax;
            position.xMax = position.xMin + EditorGUIUtility.labelWidth + 21;
            TooltipAttribute tooltipAttribute =
                Attribute.GetCustomAttribute(
                    property.serializedObject.targetObject.GetType().GetField(
                        property.name, BindingFlags.NonPublic | BindingFlags.Instance),
                    typeof(TooltipAttribute)) as TooltipAttribute;
            enabled = EditorGUI.Toggle(position, new GUIContent(label.text, tooltipAttribute?.tooltip), enabled);
            EditorGUI.BeginDisabledGroup(!enabled);
            position.xMin = position.xMax;
            position.xMax = oldMax;
            value = EditorGUI.IntField(position, value);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                enabledProperty.boolValue = enabled;
                valueProperty.intValue = value;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            return height;
        }
    }
}