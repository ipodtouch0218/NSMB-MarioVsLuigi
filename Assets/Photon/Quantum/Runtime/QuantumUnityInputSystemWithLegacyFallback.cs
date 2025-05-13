namespace Quantum {
  using System;
  using UnityEditor;

  /// <summary>
  /// This script simplifies the transition between the new Unity Input System and the legacy Input Manager.
  /// </summary>
  public class QuantumUnityInputSystemWithLegacyFallback :
#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
    UnityEngine.InputSystem.UI.InputSystemUIInputModule
#elif ENABLE_LEGACY_INPUT_MANAGER
    UnityEngine.EventSystems.StandaloneInputModule
#else 
    UnityEngine.MonoBehaviour
#endif
  {

#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
    /// <summary>
    /// Overwriting OnEnable only to workaround a bug in com.unity.inputsystem.
    /// </summary>
    protected override void OnEnable() {
      try {
        if (HasNoActions()) {
          AssignDefaultActions();
        }
      } catch (InvalidOperationException) {
        // Catching a bug in the input system (1.8.0 to 1.11.2+)
        // https://issuetracker.unity3d.com/issues/invalidoperationexception-is-thrown-when-re-entering-the-play-mode-with-reload-domain-disabled
        UnassignActions();
      }

      base.OnEnable();
    }

    private bool HasNoActions() {
      if (actionsAsset != null)
        return false;

      return point?.action == null
          && leftClick?.action == null
          && rightClick?.action == null
          && middleClick?.action == null
          && submit?.action == null
          && cancel?.action == null
          && scrollWheel?.action == null
          && trackedDeviceOrientation?.action == null
          && trackedDevicePosition?.action == null;
    }
#endif

#if UNITY_EDITOR
    [CustomEditor(typeof(QuantumUnityInputSystemWithLegacyFallback))]
    class FallbackEditor : Editor {
#if (ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM || ENABLE_LEGACY_INPUT_MANAGER)
      static readonly Type FallbackType;
      [NonSerialized]
      Editor _fallbackEditor;

      static FallbackEditor() {
        var editorTypes = TypeCache.GetTypesWithAttribute<CustomEditor>();
        foreach (var type in editorTypes) {
          if (!type.IsSubclassOf(typeof(Editor))) {
            continue;
          }
          foreach (var attr in type.GetCustomAttributesData()) {
            if (attr.AttributeType != typeof(CustomEditor)) {
              continue;
            }
            QuantumEditorLog.Assert(attr.ConstructorArguments.Count >= 1);
            QuantumEditorLog.Assert(attr.ConstructorArguments[0].ArgumentType == typeof(Type));
            if ((Type)attr.ConstructorArguments[0].Value == typeof(QuantumUnityInputSystemWithLegacyFallback).BaseType) {
              FallbackType = type;
              return;
            }
          }
        }
      }
      
      void OnEnable() {
        if (!_fallbackEditor && FallbackType != null) {
          _fallbackEditor = Editor.CreateEditor(targets, FallbackType);
        }
      }

      void OnDisable() {
        DestroyImmediate(_fallbackEditor);
      }

      public override void OnInspectorGUI() {
        if (_fallbackEditor) {
          _fallbackEditor.OnInspectorGUI();
        } else {
          base.DrawDefaultInspector();
        }
      }
#else
      public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        EditorGUILayout.HelpBox($"Install new Input System package or enable legacy Input Module", MessageType.Warning);
      }
#endif
    }
#endif
  }
}
