using UnityEngine;
using UnityEditor;

//https://forum.unity.com/threads/copy-and-paste-curves.162557/#post-1277055
[CustomPropertyDrawer(typeof(AnimationCurve))]
public class CurvePropertyDrawer : PropertyDrawer {

    private const int _buttonWidth = 12;

    private static Keyframe[] _buffer;
    private static WrapMode _preWrapMode;
    private static WrapMode _postWrapMode;

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label) {
        prop.animationCurveValue = EditorGUI.CurveField(
            new Rect(pos.x, pos.y, pos.width - _buttonWidth * 2, pos.height),
            label,
            prop.animationCurveValue
        );
        // Copy
        if (
            GUI.Button(
                new Rect(pos.x + pos.width - _buttonWidth * 2, pos.y, _buttonWidth, pos.height),
                ""
            )
        ) {
            _buffer = prop.animationCurveValue.keys;
            _preWrapMode = prop.animationCurveValue.preWrapMode;
            _postWrapMode = prop.animationCurveValue.postWrapMode;
        }
        GUI.Label(
            new Rect(pos.x + pos.width - _buttonWidth * 2, pos.y, _buttonWidth, pos.height),
            "C"
        );
        // Paste
        if (_buffer == null) return;
        if (
            GUI.Button(
                new Rect(pos.x + pos.width - _buttonWidth, pos.y, _buttonWidth, pos.height),
                ""
            )
        ) {
            AnimationCurve newAnimationCurve = new AnimationCurve(_buffer);
            newAnimationCurve.preWrapMode = _preWrapMode;
            newAnimationCurve.postWrapMode = _postWrapMode;
            prop.animationCurveValue = newAnimationCurve;
        }
        GUI.Label(
            new Rect(pos.x + pos.width - _buttonWidth, pos.y, _buttonWidth, pos.height),
            "P"
        );
    } // OnGUI()

} // class CurvePropertyDrawer

