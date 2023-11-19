#if !FUSION_DEV

#region Assets/Photon/Fusion/Editor/AssetObjectEditor.cs

namespace Fusion.Editor {
  using UnityEditor;

  [CustomEditor(typeof(AssetObject), true)]
  public class AssetObjectEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
    }
  }  
}


#endregion


#region Assets/Photon/Fusion/Editor/BehaviourEditor.cs

namespace Fusion.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;

  [CustomEditor(typeof(Fusion.Behaviour), true)]
  [CanEditMultipleObjects]
  public partial class BehaviourEditor : FusionEditor {
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/ChildLookupEditor.cs

// removed July 12 2021


#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/FixedBufferPropertyAttributeDrawer.cs

namespace Fusion.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using Fusion.Internal;
  using Unity.Collections.LowLevel.Unsafe;
  using UnityEditor;
  using UnityEditor.Compilation;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FixedBufferPropertyAttribute))]
  unsafe class FixedBufferPropertyAttributeDrawer : PropertyDrawerWithErrorHandling {
    public const string FixedBufferFieldName = "Data";
    public const string WrapperSurrogateDataPath = "Surrogate.Data";

    private const float SpacingSubLabel = 2;
    private static readonly int _multiFieldPrefixId = "MultiFieldPrefixId".GetHashCode();
    private static int[] _buffer = Array.Empty<int>();

    private static SurrogatePool _pool = new SurrogatePool();
    private static GUIContent[] _vectorProperties = new[] {
      new GUIContent("X"),
      new GUIContent("Y"),
      new GUIContent("Z"),
      new GUIContent("W"),
    };

    private Dictionary<string, bool> _needsSurrogateCache = new Dictionary<string, bool>();
    private Dictionary<Type, UnitySurrogateBase> _optimisedReaderWriters = new Dictionary<Type, UnitySurrogateBase>();

    private Type ActualFieldType => ((FixedBufferPropertyAttribute)attribute).Type;
    private int Capacity         => ((FixedBufferPropertyAttribute)attribute).Capacity;
    private Type SurrogateType   => ((FixedBufferPropertyAttribute)attribute).SurrogateType;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      if (SurrogateType == null) {
        return EditorGUIUtility.singleLineHeight;
      }

      if (NeedsSurrogate(property)) {
        var fixedBufferProperty = GetFixedBufferProperty(property);
        var firstElement = fixedBufferProperty.GetFixedBufferElementAtIndex(0);
        if (!firstElement.IsArrayElement()) {
          // it seems that with multiple seclection child elements are not accessible
          Debug.Assert(property.serializedObject.targetObjects.Length > 1);
          return EditorGUIUtility.singleLineHeight;
        }

        var wrapper = _pool.Acquire(fieldInfo, Capacity, property, SurrogateType);
        try {
          return EditorGUI.GetPropertyHeight(wrapper.Property);
        } catch (Exception ex) {
          FusionEditorLog.ErrorInspector($"Error in GetPropertyHeight for {property.propertyPath}: {ex}");
          return EditorGUIUtility.singleLineHeight;
        }

      } else {
        int count = 1;
        if (!EditorGUIUtility.wideMode) {
          count++;
        }
        return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
      }
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      if (NeedsSurrogate(property)) {
        if (SurrogateType == null) {
          this.SetInfo($"[Networked] properties of type {ActualFieldType.FullName} in structs are not yet supported");
          EditorGUI.LabelField(position, label, GUIContent.none);
        } else {
          int capacity = Capacity;
          var fixedBufferProperty = GetFixedBufferProperty(property);

          Array.Resize(ref _buffer, Math.Max(_buffer.Length, fixedBufferProperty.fixedBufferSize));

          var firstElement = fixedBufferProperty.GetFixedBufferElementAtIndex(0);
          if (!firstElement.IsArrayElement()) {
            Debug.Assert(property.serializedObject.targetObjects.Length > 1);
            SetInfo($"Type does not support multi-edit");
            EditorGUI.LabelField(position, label);
          } else {
            var wrapper = _pool.Acquire(fieldInfo, Capacity, property, SurrogateType);
            
            {
              bool surrogateOutdated = false;
              var targetObjects = property.serializedObject.targetObjects;
              if (targetObjects.Length > 1) {
                for (int i = 0; i < targetObjects.Length; ++i) {
                  using (var so = new SerializedObject(targetObjects[i])) {
                    using (var sp = so.FindPropertyOrThrow($"{property.propertyPath}.Data")) {
                      if (UpdateSurrogateFromFixedBuffer(sp, wrapper.Surrogates[i], false, _pool.Flush)) {
                        surrogateOutdated = true;
                      }
                    }
                  }
                }

                if (surrogateOutdated) {
                  // it seems that a mere Update won't do here
                  wrapper.Property = new SerializedObject(wrapper.Wrappers).FindPropertyOrThrow(WrapperSurrogateDataPath);
                }
              } else {
                // an optimised path, no alloc needed
                Debug.Assert(wrapper.Surrogates.Length == 1);
                if (UpdateSurrogateFromFixedBuffer(fixedBufferProperty, wrapper.Surrogates[0], false, _pool.Flush)) {
                  wrapper.Property.serializedObject.Update();
                }
              }
            }

            // check if there has been any chagnes
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginProperty(position, label, property);

            try {
              EditorGUI.PropertyField(position, wrapper.Property, label, true);
            } catch (Exception ex) {
              FusionEditorLog.ErrorInspector($"Error in OnGUIInternal for {property.propertyPath}: {ex}");
            }

            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck()) {
              wrapper.Property.serializedObject.ApplyModifiedProperties();

              // when not having multiple different values, just write the whole thing
              if (UpdateSurrogateFromFixedBuffer(fixedBufferProperty, wrapper.Surrogates[0], true, !fixedBufferProperty.hasMultipleDifferentValues)) {
                fixedBufferProperty.serializedObject.ApplyModifiedProperties();

                // refresh?
                wrapper.Property.serializedObject.Update();
              }
            }
          }
        }
      } else {
        if (!_optimisedReaderWriters.TryGetValue(SurrogateType, out var surrogate)) {
          surrogate = (UnitySurrogateBase)Activator.CreateInstance(SurrogateType);
          _optimisedReaderWriters.Add(SurrogateType, surrogate);
        }

        if (ActualFieldType == typeof(float)) {
          DoFloatField(position, property, label, (IUnityValueSurrogate<float>)surrogate);
        } else if (ActualFieldType == typeof(Vector2)) {
          DoFloatVectorProperty(position, property, label, 2, (IUnityValueSurrogate<Vector2>)surrogate);
        } else if (ActualFieldType == typeof(Vector3)) {
          DoFloatVectorProperty(position, property, label, 3, (IUnityValueSurrogate<Vector3>)surrogate);
        } else if (ActualFieldType == typeof(Vector4)) {
          DoFloatVectorProperty(position, property, label, 4, (IUnityValueSurrogate<Vector4>)surrogate);
        }
      }
    }

    private void DoFloatField(Rect position, SerializedProperty property, GUIContent label, IUnityValueSurrogate<float> surrogate) {
      var fixedBuffer = GetFixedBufferProperty(property);
      Debug.Assert(1 == fixedBuffer.fixedBufferSize);

      var valueProp = fixedBuffer.GetFixedBufferElementAtIndex(0);
      int value = valueProp.intValue;
      surrogate.Read(&value, 1);

      EditorGUI.BeginProperty(position, label, property);
      EditorGUI.BeginChangeCheck();
      surrogate.DataProperty = EditorGUI.FloatField(position, label, surrogate.DataProperty);
      if (EditorGUI.EndChangeCheck()) {
        surrogate.Write(&value, 1);
        valueProp.intValue = value;
        property.serializedObject.ApplyModifiedProperties();
      }
      EditorGUI.EndProperty();
    }

    private unsafe void DoFloatVectorProperty<T>(Rect position, SerializedProperty property, GUIContent label, int count, IUnityValueSurrogate<T> readerWriter) where T : unmanaged {
      EditorGUI.BeginProperty(position, label, property);
      try {
        var fixedBuffer = GetFixedBufferProperty(property);
        Debug.Assert(count == fixedBuffer.fixedBufferSize);

        int* raw = stackalloc int[count];
        for (int i = 0; i < count; ++i) {
          raw[i] = fixedBuffer.GetFixedBufferElementAtIndex(i).intValue;
        }

        readerWriter.Read(raw, 1);

        int changed = 0;

        var data = readerWriter.DataProperty;
        float* pdata = (float*)&data;

        int id = GUIUtility.GetControlID(_multiFieldPrefixId, FocusType.Keyboard, position);
        position = UnityInternal.EditorGUI.MultiFieldPrefixLabel(position, id, label, count);
        if (position.width > 1) {
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            float w = (position.width - (count - 1) * SpacingSubLabel) / count;
            var nestedPosition = new Rect(position) { width = w };

            for (int i = 0; i < count; ++i) {
              var propLabel = _vectorProperties[i];
              float prefixWidth = EditorStyles.label.CalcSize(propLabel).x;
              using (new FusionEditorGUI.LabelWidthScope(prefixWidth)) {
                EditorGUI.BeginChangeCheck();
                var newValue = propLabel == null ? EditorGUI.FloatField(nestedPosition, pdata[i]) : EditorGUI.FloatField(nestedPosition, propLabel, pdata[i]);
                if (EditorGUI.EndChangeCheck()) {
                  changed |= (1 << i);
                  pdata[i] = newValue;
                }
              }
              nestedPosition.x += w + SpacingSubLabel;
            }
          }
        }

        if (changed != 0) {
          readerWriter.DataProperty = data;
          readerWriter.Write(raw, 1);

          for (int i = 0; i < count; ++i) {
            if ((changed & (1 << i)) != 0) {
              fixedBuffer.GetFixedBufferElementAtIndex(i).intValue = raw[i];
            }
          }
          property.serializedObject.ApplyModifiedProperties();
        }
      } finally {
        EditorGUI.EndProperty();
      }
    }

    private SerializedProperty GetFixedBufferProperty(SerializedProperty prop) {
      var result = prop.FindPropertyRelativeOrThrow(FixedBufferFieldName);
      Debug.Assert(result.isFixedBuffer);
      return result;
    }

    private bool NeedsSurrogate(SerializedProperty property) {
      if (_needsSurrogateCache.TryGetValue(property.propertyPath, out var result)) {
        return result;
      }

      result = true;
      if (ActualFieldType == typeof(float) || ActualFieldType == typeof(Vector2) || ActualFieldType == typeof(Vector3) || ActualFieldType == typeof(Vector4)) {
        var attributes = UnityInternal.ScriptAttributeUtility.GetFieldAttributes(fieldInfo);
        if (attributes == null || attributes.Count == 0) {
          // fast drawers do not support any additional attributes
          result = false;
        }
      }

      _needsSurrogateCache.Add(property.propertyPath, result);
      return result;
    }

    private bool UpdateSurrogateFromFixedBuffer(SerializedProperty sp, UnitySurrogateBase surrogate, bool write, bool force) {
      int count = sp.fixedBufferSize;
      Array.Resize(ref _buffer, Math.Max(_buffer.Length, count));

      // need to get to the first property... `GetFixedBufferElementAtIndex` is slow and allocs

      var element = sp.Copy();
      element.Next(true); // .Array
      element.Next(true); // .Array.size
      element.Next(true); // .Array.data[0]

      fixed (int* p = _buffer) {
        UnsafeUtility.MemClear(p, count * sizeof(int));

        try {
          surrogate.Write(p, Capacity);
        } catch (Exception ex) {
          SetError($"Failed writing: {ex}");
        }

        int i = 0;
        if (!force) {
          // find first difference
          for (; i < count; ++i, element.Next(true)) {
            Debug.Assert(element.propertyType == SerializedPropertyType.Integer);
            if (element.intValue != p[i]) {
              break;
            }
          }
        }

        if (i < count) {
          // update data
          if (write) {
            for (; i < count; ++i, element.Next(true)) {
              element.intValue = p[i];
            }
          } else {
            for (; i < count; ++i, element.Next(true)) {
              p[i] = element.intValue;
            }
          }
          // update surrogate
          surrogate.Read(p, Capacity);
          return true;
        } else {
          return false;
        }
      }
    }

    private class SurrogatePool {

      private const int MaxTTL = 10;

      private FieldInfo _surrogateField = typeof(FusionUnitySurrogateBaseWrapper).GetField(nameof(FusionUnitySurrogateBaseWrapper.Surrogate));
      private Dictionary<(Type, string, int), PropertyEntry> _used = new Dictionary<(Type, string, int), PropertyEntry>();
      private Dictionary<Type, Stack<FusionUnitySurrogateBaseWrapper>> _wrappersPool = new Dictionary<Type, Stack<FusionUnitySurrogateBaseWrapper>>();

      public SurrogatePool() {
        Undo.undoRedoPerformed += () => Flush = true;

        EditorApplication.update += () => {
          Flush = false;
          if (!WasUsed) {
            return;
          }
          WasUsed = false;

          var keysToRemove = new List<(Type, string, int)>();

          foreach (var kv in _used) {
            var entry = kv.Value;
            if (--entry.TTL < 0) {
              // return to pool
              keysToRemove.Add(kv.Key);
              foreach (var wrapper in entry.Wrappers) {
                _wrappersPool[wrapper.Surrogate.GetType()].Push(wrapper);
              }
            }
          }

          // make all the wrappers available again
          foreach (var key in keysToRemove) {
            FusionEditorLog.TraceInspector($"Cleaning up {key}");
            _used.Remove(key);
          }
        };

        CompilationPipeline.compilationFinished += obj => {
          // destroy SO's, we don't want them to hold on to the surrogates

          var wrappers = _wrappersPool.Values.SelectMany(x => x)
            .Concat(_used.Values.SelectMany(x => x.Wrappers));

          foreach (var wrapper in wrappers) {
            UnityEngine.Object.DestroyImmediate(wrapper);
          }
        };
      }

      public bool Flush { get; private set; }

      public bool WasUsed { get; private set; }

      public PropertyEntry Acquire(FieldInfo field, int capacity, SerializedProperty property, Type type) {
        WasUsed = true;

        bool hadNulls = false;

        var key = (type, property.propertyPath, property.serializedObject.targetObjects.Length);
        if (_used.TryGetValue(key, out var entry)) {
          var countValid = entry.Wrappers.Count(x => x);
          if (countValid != entry.Wrappers.Length) {
            // something destroyed wrappers
            Debug.Assert(countValid == 0);
            _used.Remove(key);
            hadNulls = true;
          } else {
            entry.TTL = MaxTTL;
            return entry;
          }
        }

        // acquire new entry
        var wrappers = new FusionUnitySurrogateBaseWrapper[key.Item3];
        if (!_wrappersPool.TryGetValue(type, out var pool)) {
          pool = new Stack<FusionUnitySurrogateBaseWrapper>();
          _wrappersPool.Add(type, pool);
        }

        for (int i = 0; i < wrappers.Length; ++i) {

          // pop destroyed ones
          while (pool.Count > 0 && !pool.Peek()) {
            pool.Pop();
            hadNulls = true;
          }

          if (pool.Count > 0) {
            wrappers[i] = pool.Pop();
          } else {
            FusionEditorLog.TraceInspector($"Allocating surrogate {type}");
            wrappers[i] = ScriptableObject.CreateInstance<FusionUnitySurrogateBaseWrapper>();
          }

          if (wrappers[i].SurrogateType != type) {
            FusionEditorLog.TraceInspector($"Replacing type {wrappers[i].Surrogate?.GetType()} with {type}");
            wrappers[i].Surrogate = (UnitySurrogateBase)Activator.CreateInstance(type);
            wrappers[i].Surrogate.Init(capacity);
            wrappers[i].SurrogateType = type;
          }
        }

        FusionEditorLog.TraceInspector($"Created entry for {property.propertyPath}");

        entry = new PropertyEntry() {
          Property = new SerializedObject(wrappers).FindPropertyOrThrow(WrapperSurrogateDataPath),
          Surrogates = wrappers.Select(x => x.Surrogate).ToArray(),
          TTL = MaxTTL,
          Wrappers = wrappers
        };

        _used.Add(key, entry);

        if (hadNulls) {
          GUIUtility.ExitGUI();
        }

        return entry;
      }

      public class PropertyEntry {
        public SerializedProperty Property;
        public UnitySurrogateBase[] Surrogates;
        public int TTL;
        public FusionUnitySurrogateBaseWrapper[] Wrappers;
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/INetworkPrefabSourceDrawer.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;
  
  [CustomPropertyDrawer(typeof(INetworkPrefabSource), true)]
  class INetworkPrefabSourceDrawer : PropertyDrawerWithErrorHandling {

    const int ThumbnailWidth = 20;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        
        EditorGUI.BeginChangeCheck();
        
        var source = property.managedReferenceValue as INetworkPrefabSource;
        position = DrawThumbnailPrefix(position, source);
        source = DrawSourceObjectPicker(position, GUIContent.none, source);

        if (EditorGUI.EndChangeCheck()) {
          // see how it can be loaded
          property.managedReferenceValue = source;
          property.serializedObject.ApplyModifiedProperties();
        } 
      }
    }

    public static Rect DrawThumbnailPrefix(Rect position, INetworkPrefabSource source) {
      if (source == null) {
        return position;
      }
      
      var pos = position;
      pos.width = ThumbnailWidth;
      FusionEditorGUI.DrawTypeThumbnail(pos, source.GetType(), "NetworkPrefabSource", source.Description);
      position.xMin += ThumbnailWidth;
      return position;
    }

    public static void DrawThumbnail(Rect position, INetworkPrefabSource source) {
      if (source == null) {
        return;
      }
      var pos = position;
      pos.x += (pos.width - ThumbnailWidth) / 2;
      pos.width = ThumbnailWidth;
      FusionEditorGUI.DrawTypeThumbnail(pos, source.GetType(), "NetworkPrefabSource", source.Description);
    }
    
    public static INetworkPrefabSource DrawSourceObjectPicker(Rect position, GUIContent label, INetworkPrefabSource source) {
      NetworkProjectConfigUtilities.TryGetPrefabEditorInstance(source?.AssetGuid ?? default, out var target);
      
      EditorGUI.BeginChangeCheck();
      target = NetworkPrefabRefDrawer.DrawNetworkPrefabPicker(position, label, target);
      if (EditorGUI.EndChangeCheck()) {
        if (target) {
          var factory = new NetworkAssetSourceFactory();
          return factory.TryCreatePrefabSource(new NetworkAssetSourceFactoryContext(target));
        } else {
          return null;
        }
      } else {
        return source;
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return EditorGUIUtility.singleLineHeight;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/NetworkBoolDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkBool))]
  public class NetworkBoolDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        var valueProperty = property.FindPropertyRelativeOrThrow("_value");
        EditorGUI.BeginChangeCheck();
        bool isChecked = EditorGUI.Toggle(position, label, valueProperty.intValue > 0);
        if (EditorGUI.EndChangeCheck()) {
          valueProperty.intValue = isChecked ? 1 : 0;
          valueProperty.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/NetworkObjectGuidDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkObjectGuid))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  class NetworkObjectGuidDrawer : PropertyDrawerWithErrorHandling {

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var guid = GetValue(property);

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        if (!GUI.enabled) {
          GUI.enabled = true;
          EditorGUI.SelectableLabel(position, $"{(System.Guid)guid}");
          GUI.enabled = false;
        } else {
          EditorGUI.BeginChangeCheck();

          var text = EditorGUI.TextField(position, ((System.Guid)guid).ToString());
          ClearErrorIfLostFocus();

          if (EditorGUI.EndChangeCheck()) {
            if (NetworkObjectGuid.TryParse(text, out guid)) {
              SetValue(property, guid);
              property.serializedObject.ApplyModifiedProperties();
            } else {
              SetError($"Unable to parse {text}");
            }
          }
        }
      }
    }

    public static unsafe NetworkObjectGuid GetValue(SerializedProperty property) {
      var guid = new NetworkObjectGuid();
      var prop = property.FindPropertyRelativeOrThrow(nameof(NetworkObjectGuid.RawGuidValue));
        guid.RawGuidValue[0] = prop.GetFixedBufferElementAtIndex(0).longValue;
        guid.RawGuidValue[1] = prop.GetFixedBufferElementAtIndex(1).longValue;
      return guid;
    }

    public static unsafe void SetValue(SerializedProperty property, NetworkObjectGuid guid) {
      var prop = property.FindPropertyRelativeOrThrow(nameof(NetworkObjectGuid.RawGuidValue));
        prop.GetFixedBufferElementAtIndex(0).longValue = guid.RawGuidValue[0];
        prop.GetFixedBufferElementAtIndex(1).longValue = guid.RawGuidValue[1];
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/NetworkPrefabAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkPrefabAttribute))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  class NetworkPrefabAttributeDrawer : PropertyDrawerWithErrorHandling {

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var leafType = fieldInfo.FieldType.GetUnityLeafType();
      if (leafType != typeof(GameObject) && leafType != typeof(NetworkObject) && !leafType.IsSubclassOf(typeof(NetworkObject))) {
        SetError($"{nameof(NetworkPrefabAttribute)} only works for {typeof(GameObject)} and {typeof(NetworkObject)} fields");
        return;
      }

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        GameObject prefab;
        if (leafType == typeof(GameObject)) {
          prefab = (GameObject)property.objectReferenceValue;
        } else {
          var component = (NetworkObject)property.objectReferenceValue;
          prefab = component != null ? component.gameObject : null;
        }

        EditorGUI.BeginChangeCheck();

        prefab = (GameObject)EditorGUI.ObjectField(position, prefab, typeof(GameObject), false);

        // ensure the results are filtered
        if (UnityInternal.ObjectSelector.isVisible) {
          var selector = UnityInternal.ObjectSelector.get;
          if (UnityInternal.EditorGUIUtility.LastControlID == selector.objectSelectorID) {
            var filter = selector.searchFilter;
            if (!filter.Contains(NetworkProjectConfigImporter.FusionPrefabTagSearchTerm)) {
              if (string.IsNullOrEmpty(filter)) {
                filter = NetworkProjectConfigImporter.FusionPrefabTagSearchTerm;
              } else {
                filter = NetworkProjectConfigImporter.FusionPrefabTagSearchTerm + " " + filter;
              }
              selector.searchFilter = filter;
            }
          }
        }

        if (EditorGUI.EndChangeCheck()) {
          UnityEngine.Object result;
          if (!prefab) {
            result = null;
          } else { 
            if (leafType == typeof(GameObject)) {
              result = prefab;
            } else { 
              result = prefab.GetComponent(leafType);
              if (!result) {
                SetError($"Prefab {prefab} does not have a {leafType} component");
                return;
              }
            }
          }

          property.objectReferenceValue = prefab;
          property.serializedObject.ApplyModifiedProperties();
        }

        if (prefab) {
          var no = prefab.GetComponent<NetworkObject>();
          if (!no) {
            SetError($"Prefab {prefab} does not have a {nameof(NetworkObject)} component");
          }
          if (!AssetDatabaseUtils.HasLabel(prefab, NetworkProjectConfigImporter.FusionPrefabTag)) {
            SetError($"Prefab {prefab} is not tagged as a Fusion prefab. Try reimporting.");
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/NetworkPrefabRefDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkPrefabRef))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  class NetworkPrefabRefDrawer : PropertyDrawerWithErrorHandling {

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var prefabRef = NetworkObjectGuidDrawer.GetValue(property);

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        NetworkObject prefab = null;
        if (prefabRef.IsValid && !NetworkProjectConfigUtilities.TryGetPrefabEditorInstance(prefabRef, out prefab)) {
          SetError($"Prefab with guid {prefabRef} not found.");
        }

        EditorGUI.BeginChangeCheck();

        prefab = DrawNetworkPrefabPicker(position, GUIContent.none, prefab);

        if (EditorGUI.EndChangeCheck()) {
          if (prefab) {
            prefabRef = NetworkObjectEditor.GetPrefabGuid(prefab);
          } else {
            prefabRef = default;
          }
          NetworkObjectGuidDrawer.SetValue(property, prefabRef);
          property.serializedObject.ApplyModifiedProperties();
        }

        SetInfo($"{prefabRef}");


        if (prefab) {
          var expectedPrefabRef = NetworkObjectEditor.GetPrefabGuid(prefab);
          if (!prefabRef.Equals(expectedPrefabRef)) {
            SetError($"Resolved {prefab} has a different guid ({expectedPrefabRef}) than expected ({prefabRef}). " +
              $"This can happen if prefabs are incorrectly resolved, e.g. when there are multiple resources of the same name.");
          } else if (!expectedPrefabRef.IsValid) {
            SetError($"Prefab {prefab} needs to be reimported.");
          } else if (!AssetDatabaseUtils.HasLabel(prefab, NetworkProjectConfigImporter.FusionPrefabTag)) {
            SetError($"Prefab {prefab} is not tagged as a Fusion prefab. Try reimporting.");
          } else {
            // ClearError();
          }
        }
      }
    }

    public static NetworkObject DrawNetworkPrefabPicker(Rect position, GUIContent label, NetworkObject prefab) {
      var prefabGo = (GameObject)EditorGUI.ObjectField(position, label, prefab ? prefab.gameObject : null, typeof(GameObject), false);

      // ensure the results are filtered
      if (UnityInternal.ObjectSelector.isVisible) {
        var selector = UnityInternal.ObjectSelector.get;
        if (UnityInternal.EditorGUIUtility.LastControlID == selector.objectSelectorID) {
          var filter = selector.searchFilter;
          if (!filter.Contains(NetworkProjectConfigImporter.FusionPrefabTagSearchTerm)) {
            if (string.IsNullOrEmpty(filter)) {
              filter = NetworkProjectConfigImporter.FusionPrefabTagSearchTerm;
            } else {
              filter = NetworkProjectConfigImporter.FusionPrefabTagSearchTerm + " " + filter;
            }

            selector.searchFilter = filter;
          }
        }
      }

      if (prefabGo) {
        return prefabGo.GetComponent<NetworkObject>();
      } else {
        return null;
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/NetworkStringDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkString<>))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  class NetworkStringDrawer : PropertyDrawerWithErrorHandling {

    private string _str = "";
    private Action<int[], int> _write;
    private Action<int[], int> _read;
    private int _expectedLength;

    public NetworkStringDrawer() {
      _write = (buffer, count) => {
        unsafe {
          fixed (int* p = buffer) {
            _str = new string((sbyte*)p, 0, Mathf.Clamp(_expectedLength, 0, count) * 4, Encoding.UTF32);
          }
        }
      };

      _read = (buffer, count) => {
        unsafe {
          fixed (int* p = buffer) {
            var charCount = UTF32Tools.Convert(_str, (uint*)p, count).CharacterCount;
            if (charCount < _str.Length) {
              _str = _str.Substring(0, charCount);
            }
          }
        }
      };
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var length = property.FindPropertyRelativeOrThrow(nameof(NetworkString<_2>._length));
      var data = property.FindPropertyRelativeOrThrow($"{nameof(NetworkString<_2>._data)}.Data");

      _expectedLength = length.intValue;
      data.UpdateFixedBuffer(_read, _write, false);

      EditorGUI.BeginChangeCheck();

      using (new FusionEditorGUI.ShowMixedValueScope(data.hasMultipleDifferentValues)) {
        _str = EditorGUI.TextField(position, label, _str);
      }

      if (EditorGUI.EndChangeCheck()) {
        _expectedLength = _str.Length;
        if (data.UpdateFixedBuffer(_read, _write, true, data.hasMultipleDifferentValues)) {
          length.intValue = Encoding.UTF32.GetByteCount(_str) / 4;
          data.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/NormalizedRectAttributeDrawer.cs


namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

#if UNITY_EDITOR
  [CustomPropertyDrawer(typeof(NormalizedRectAttribute))]
  public class NormalizedRectAttributeDrawer : PropertyDrawer {

    bool isDragNewRect;
    bool isDragXMin, isDragXMax, isDragYMin, isDragYMax, isDragAll;
    MouseCursor lockCursorStyle;

    Vector2 mouseDownStart;
    static GUIStyle _compactLabelStyle;
    static GUIStyle _compactValueStyle;

    const float EXPANDED_HEIGHT = 140;
    const float COLLAPSE_HEIGHT = 48;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      if (property.propertyType == SerializedPropertyType.Rect) {
        return property.isExpanded ? EXPANDED_HEIGHT : COLLAPSE_HEIGHT;
      } else {
        return base.GetPropertyHeight(property, label);
      }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      EditorGUI.BeginProperty(position, label, property);

      bool hasChanged = false;

      EditorGUI.LabelField(new Rect(position) { height = 17 }, label);

      var value = property.rectValue;

      if (property.propertyType == SerializedPropertyType.Rect) {

        var dragarea = new Rect(position) {
          yMin = position.yMin + 16 + 3,
          yMax = position.yMax - 2,
          //xMin = position.xMin + 16,
          //xMax = position.xMax - 4
        };

        // lower foldout box
        GUI.Box(dragarea, GUIContent.none, EditorStyles.helpBox);

        property.isExpanded = GUI.Toggle(new Rect(position) { xMin = dragarea.xMin + 2, yMin = dragarea.yMin + 2, width = 12, height = 16 }, property.isExpanded, GUIContent.none, EditorStyles.foldout);
        bool isExpanded = property.isExpanded;

        float border = isExpanded ? 4 : 2;
        dragarea.xMin += 18;
        dragarea.yMin += border;
        dragarea.xMax -= border;
        dragarea.yMax -= border;

        // Reshape the inner box to the correct aspect ratio
        if (isExpanded) {
          var ratio = (attribute as NormalizedRectAttribute).AspectRatio;
          if (ratio == 0) {
            var currentRes = UnityEditor.Handles.GetMainGameViewSize();
            ratio = currentRes.x / currentRes.y;
          }

          // Don't go any wider than the inspector box.
          var width = (dragarea.height * ratio);
          if (width < dragarea.width) {
            var x = (dragarea.width - width) / 2;
            dragarea.x = dragarea.xMin + (int)x;
            dragarea.width = (int)(width);
          }
        }


        // Simulated desktop rect
        GUI.Box(dragarea, GUIContent.none, EditorStyles.helpBox);

        var invertY = (attribute as NormalizedRectAttribute).InvertY;

        Event e = Event.current;
        
        const int HANDLE_SIZE = 8;

        var normmin = new Vector2(value.xMin, invertY ? 1f - value.yMin : value.yMin);
        var normmax = new Vector2(value.xMax, invertY ? 1f - value.yMax : value.yMax);
        var minreal = Rect.NormalizedToPoint(dragarea, normmin);
        var maxreal = Rect.NormalizedToPoint(dragarea, normmax);
        var lowerleftrect = new Rect(minreal.x              , minreal.y - (invertY ? HANDLE_SIZE : 0), HANDLE_SIZE, HANDLE_SIZE);
        var upperrghtrect = new Rect(maxreal.x - HANDLE_SIZE, maxreal.y - (invertY ? 0 : HANDLE_SIZE), HANDLE_SIZE, HANDLE_SIZE);
        var upperleftrect = new Rect(minreal.x              , maxreal.y - (invertY ? 0 : HANDLE_SIZE), HANDLE_SIZE, HANDLE_SIZE);
        var lowerrghtrect = new Rect(maxreal.x - HANDLE_SIZE, minreal.y - (invertY ? HANDLE_SIZE : 0), HANDLE_SIZE, HANDLE_SIZE);

        var currentrect = Rect.MinMaxRect(minreal.x, invertY ? maxreal.y : minreal.y, maxreal.x, invertY ? minreal.y : maxreal.y);

        if (lockCursorStyle == MouseCursor.Arrow) {
          if (isExpanded) {
            EditorGUIUtility.AddCursorRect(lowerleftrect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(upperrghtrect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(upperleftrect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(lowerrghtrect, MouseCursor.Link);
          }
          EditorGUIUtility.AddCursorRect(currentrect, MouseCursor.MoveArrow);
        } else {
          // Lock cursor to a style while dragging, otherwise the slow inspector update causes rapid mouse icon changes.
          EditorGUIUtility.AddCursorRect(dragarea, lockCursorStyle);
        }

        EditorGUI.DrawRect(lowerleftrect, Color.yellow);
        EditorGUI.DrawRect(upperrghtrect, Color.yellow);
        EditorGUI.DrawRect(upperleftrect, Color.yellow);
        EditorGUI.DrawRect(lowerrghtrect, Color.yellow);

        var mousepos = e.mousePosition;
        if (e.button == 0) {
          if (e.type == EventType.MouseUp) {
            isDragXMin = false;
            isDragYMin = false;
            isDragXMax = false;
            isDragYMax = false;
            isDragAll  = false;
            lockCursorStyle = MouseCursor.Arrow;
            isDragNewRect   = false;

            hasChanged = true;
          }

          if (e.type == EventType.MouseDown ) {
            if (isExpanded && lowerleftrect.Contains(mousepos)) {
              isDragXMin = true;
              isDragYMin = true;
              lockCursorStyle = MouseCursor.Link;
            } else if (isExpanded && upperrghtrect.Contains(mousepos)) {
              isDragXMax = true;
              isDragYMax = true;
              lockCursorStyle = MouseCursor.Link;
            } else if (isExpanded && upperleftrect.Contains(mousepos)) {
              isDragXMin = true;
              isDragYMax = true;
              lockCursorStyle = MouseCursor.Link;
            } else if (isExpanded && lowerrghtrect.Contains(mousepos)) {
              isDragXMax = true;
              isDragYMin = true;
              lockCursorStyle = MouseCursor.Link;
            } else if (currentrect.Contains(mousepos)) {
              isDragAll = true;
              // mouse start is stored as a normalized offset from the Min values.
              mouseDownStart = Rect.PointToNormalized(dragarea, mousepos) - normmin;
              lockCursorStyle = MouseCursor.MoveArrow;
            } else if (isExpanded && dragarea.Contains(mousepos)) {
              mouseDownStart = mousepos;
              isDragNewRect = true;
            }
          }
        }

        if (e.type == EventType.MouseDrag) {

          Rect rect;
          if (isDragNewRect) {
            var start = Rect.PointToNormalized(dragarea, mouseDownStart);
            var end = Rect.PointToNormalized(dragarea, e.mousePosition);

            if (invertY) {
              rect = Rect.MinMaxRect(
                  Math.Max(0f,      Math.Min(start.x, end.x)),
                  Math.Max(0f, 1f - Math.Max(start.y, end.y)),
                  Math.Min(1f,      Math.Max(start.x, end.x)),
                  Math.Min(1f, 1f - Math.Min(start.y, end.y))
                  );
            } else {
              rect = Rect.MinMaxRect(
                  Math.Max(0f, Math.Min(start.x, end.x)),
                  Math.Max(0f, Math.Min(start.y, end.y)),
                  Math.Min(1f, Math.Max(start.x, end.x)),
                  Math.Min(1f, Math.Max(start.y, end.y))
                  );
            }
            property.rectValue = rect;
            hasChanged = true;


          } else if (isDragAll){
            var normmouse = Rect.PointToNormalized(dragarea, e.mousePosition);
            rect = new Rect(value) {
              x = Math.Max(normmouse.x - mouseDownStart.x, 0),
              y = Math.Max(invertY ? (1 - normmouse.y + mouseDownStart.y) : (normmouse.y - mouseDownStart.y), 0)
            };

            if (rect.xMax > 1) {
              rect = new Rect(rect) { x = rect.x + (1f - rect.xMax)};
            }
            if (rect.yMax > 1) {
              rect = new Rect(rect) { y = rect.y + (1f - rect.yMax) };
            }

            property.rectValue = rect;
            hasChanged = true;

          } else if (isDragXMin || isDragXMax || isDragYMin || isDragYMax) {

            const float VERT_HANDLE_MIN_DIST = .2f;
            const float HORZ_HANDLE_MIN_DIST = .05f;
            var normmouse = Rect.PointToNormalized(dragarea, e.mousePosition);
            if (invertY) {
              rect = Rect.MinMaxRect(
                isDragXMin ? Math.Min(     normmouse.x, value.xMax - HORZ_HANDLE_MIN_DIST) : value.xMin,
                isDragYMin ? Math.Min(1f - normmouse.y, value.yMax - VERT_HANDLE_MIN_DIST) : value.yMin,
                isDragXMax ? Math.Max(     normmouse.x, value.xMin + HORZ_HANDLE_MIN_DIST) : value.xMax,
                isDragYMax ? Math.Max(1f - normmouse.y, value.yMin + VERT_HANDLE_MIN_DIST) : value.yMax 
                );
            } else {
              rect = Rect.MinMaxRect(
                isDragXMin ? Math.Min(normmouse.x, value.xMax - HORZ_HANDLE_MIN_DIST) : value.xMin,
                isDragYMin ? Math.Min(normmouse.y, value.yMax - VERT_HANDLE_MIN_DIST) : value.yMin,
                isDragXMax ? Math.Max(normmouse.x, value.xMin + HORZ_HANDLE_MIN_DIST) : value.xMax,
                isDragYMax ? Math.Max(normmouse.y, value.yMin + VERT_HANDLE_MIN_DIST) : value.yMax
                );
            }

            property.rectValue = rect;
            hasChanged = true;
          }
        }

        const float SPACING = 4f;
        const int LABELS_WIDTH = 16;
        const float COMPACT_THRESHOLD = 340f;

        bool useCompact = position.width < COMPACT_THRESHOLD;

        var labelwidth = EditorGUIUtility.labelWidth;
        var fieldwidth = (position.width - labelwidth- 3 * SPACING) * 0.25f ;
        var fieldbase = new Rect(position) { xMin = position.xMin + labelwidth, height = 16, width = fieldwidth - (useCompact ? 0 : LABELS_WIDTH) };
        
        if (_compactValueStyle == null) {
          _compactLabelStyle = new GUIStyle(EditorStyles.miniLabel)     { fontSize = 9, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(2, 0, 1, 0) };
          _compactValueStyle = new GUIStyle(EditorStyles.miniTextField) { fontSize = 9, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(2, 0, 1, 0) };
        }
        GUIStyle valueStyle = _compactValueStyle;

        //if (useCompact) {
        //  if (_compactStyle == null) {
        //    _compactStyle = new GUIStyle(EditorStyles.miniTextField) { fontSize = 9, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(2, 0, 1, 0) };
        //  }
        //  valueStyle = _compactStyle;
        //} else {
        //  valueStyle = EditorStyles.textField;
        //}

        // Only draw labels when not in compact
        if (!useCompact) {
          Rect l1 = new Rect(fieldbase) { x = fieldbase.xMin };
          Rect l2 = new Rect(fieldbase) { x = fieldbase.xMin + 1 * (fieldwidth + SPACING) };
          Rect l3 = new Rect(fieldbase) { x = fieldbase.xMin + 2 * (fieldwidth + SPACING) };
          Rect l4 = new Rect(fieldbase) { x = fieldbase.xMin + 3 * (fieldwidth + SPACING) };
          GUI.Label(l1, "L:", _compactLabelStyle);
          GUI.Label(l2, "R:", _compactLabelStyle);
          GUI.Label(l3, "T:", _compactLabelStyle);
          GUI.Label(l4, "B:", _compactLabelStyle);
        }

        // Draw value fields
        Rect f1 = new Rect(fieldbase) { x = fieldbase.xMin + 0 * fieldwidth + (useCompact ? 0 : LABELS_WIDTH) };
        Rect f2 = new Rect(fieldbase) { x = fieldbase.xMin + 1 * fieldwidth + (useCompact ? 0 : LABELS_WIDTH) + 1 * SPACING };
        Rect f3 = new Rect(fieldbase) { x = fieldbase.xMin + 2 * fieldwidth + (useCompact ? 0 : LABELS_WIDTH) + 2 * SPACING };
        Rect f4 = new Rect(fieldbase) { x = fieldbase.xMin + 3 * fieldwidth + (useCompact ? 0 : LABELS_WIDTH) + 3 * SPACING };

        using (var check = new EditorGUI.ChangeCheckScope()) {
          float newxmin, newxmax, newymin, newymax;
          if (invertY) {
            newxmin = EditorGUI.DelayedFloatField(f1, (float)Math.Round(value.xMin, useCompact ? 2 : 3), valueStyle);
            newxmax = EditorGUI.DelayedFloatField(f2, (float)Math.Round(value.xMax, useCompact ? 2 : 3), valueStyle);
            newymax = EditorGUI.DelayedFloatField(f3, (float)Math.Round(value.yMax, useCompact ? 2 : 3), valueStyle);
            newymin = EditorGUI.DelayedFloatField(f4, (float)Math.Round(value.yMin, useCompact ? 2 : 3), valueStyle);
          } else {
            newxmin = EditorGUI.DelayedFloatField(f1, (float)Math.Round(value.xMin, useCompact ? 2 : 3), valueStyle);
            newxmax = EditorGUI.DelayedFloatField(f2, (float)Math.Round(value.xMax, useCompact ? 2 : 3), valueStyle);
            newymin = EditorGUI.DelayedFloatField(f3, (float)Math.Round(value.yMin, useCompact ? 2 : 3), valueStyle);
            newymax = EditorGUI.DelayedFloatField(f4, (float)Math.Round(value.yMax, useCompact ? 2 : 3), valueStyle);
          }

          if (check.changed) {
            if (newxmin != value.xMin) value.xMin = Math.Min(newxmin, value.xMax - .05f);
            if (newxmax != value.xMax) value.xMax = Math.Max(newxmax, value.xMin + .05f);
            if (newymax != value.yMax) value.yMax = Math.Max(newymax, value.yMin + .05f);
            if (newymin != value.yMin) value.yMin = Math.Min(newymin, value.yMax - .05f);
            property.rectValue = value;
            property.serializedObject.ApplyModifiedProperties();
          }
        }

        var nmins = new Vector2(value.xMin, invertY ? 1f - value.yMin : value.yMin);
        var nmaxs = new Vector2(value.xMax, invertY ? 1f - value.yMax : value.yMax);
        var mins = Rect.NormalizedToPoint(dragarea, nmins);
        var maxs = Rect.NormalizedToPoint(dragarea, nmaxs);
        var area = Rect.MinMaxRect(minreal.x, invertY ? maxreal.y : minreal.y, maxreal.x, invertY ? minreal.y : maxreal.y);

        EditorGUI.DrawRect(area, new Color(1f, 1f, 1f, .1f));
        //GUI.DrawTexture(area, GUIContent.none, EditorStyles.helpBox);
        //GUI.Box(area, GUIContent.none, EditorStyles.helpBox);

      } else {
        Debug.LogWarning($"{nameof(NormalizedRectAttribute)} only valid on UnityEngine.Rect fields. Will use default rendering for '{property.type} {property.name}' in class '{fieldInfo.DeclaringType}'.");
        EditorGUI.PropertyField(position, property, label);
      }

      if (hasChanged) {
        GUI.changed = true;
        property.serializedObject.ApplyModifiedProperties();
       }

      EditorGUI.EndProperty();
    }
  }
#endif

}


#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/SceneRefDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(SceneRef))]
  public class SceneRefDrawer : PropertyDrawer {

    public const int CheckboxWidth = 16;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        var valueProperty = property.FindPropertyRelativeOrThrow(nameof(SceneRef.RawValue));
        long rawValue = valueProperty.longValue;

        var togglePos = position;
        togglePos.width = CheckboxWidth;
        bool hasValue = rawValue > 0;

        EditorGUI.BeginChangeCheck();

        if (EditorGUI.Toggle(togglePos, hasValue) != hasValue) {
          rawValue = valueProperty.longValue = hasValue ? 0 : 1;
          valueProperty.serializedObject.ApplyModifiedProperties();
        }

        if (rawValue > 0) {
          position.xMin += togglePos.width;

          rawValue = EditorGUI.LongField(position, rawValue - 1);
          rawValue = Math.Max(0, rawValue) + 1;

          if (EditorGUI.EndChangeCheck()) {
            valueProperty.longValue = rawValue;
            valueProperty.serializedObject.ApplyModifiedProperties();
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/SerializableDictionaryDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEditorInternal;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(SerializableDictionary), true)]
  class SerializableDictionaryDrawer : PropertyDrawerWithErrorHandling {
    const string ItemsPropertyPath    = SerializableDictionary<int,int>.ItemsPropertyPath;
    const string EntryKeyPropertyPath = SerializableDictionary<int, int>.EntryKeyPropertyPath;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var entries = property.FindPropertyRelativeOrThrow(ItemsPropertyPath);
      entries.isExpanded = property.isExpanded;
      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.PropertyField(position, entries, label, true);
        property.isExpanded = entries.isExpanded;

        string error = VerifyDictionary(entries, EntryKeyPropertyPath);
        if (error != null) {
          SetError(error);
        } else {
          ClearError();
        }
      }
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var entries = property.FindPropertyRelativeOrThrow(ItemsPropertyPath);
      return EditorGUI.GetPropertyHeight(entries, label, true);
    }

    private static HashSet<SerializedProperty> _dictionaryKeyHash = new HashSet<SerializedProperty>(new SerializedPropertyUtilities.SerializedPropertyEqualityComparer());

    private static string VerifyDictionary(SerializedProperty prop, string keyPropertyName) {
      Debug.Assert(prop.isArray);
      try {
        for (int i = 0; i < prop.arraySize; ++i) {
          var keyProperty = prop.GetArrayElementAtIndex(i).FindPropertyRelativeOrThrow(keyPropertyName);
          if (!_dictionaryKeyHash.Add(keyProperty)) {

            var groups = Enumerable.Range(0, prop.arraySize)
                .GroupBy(x => prop.GetArrayElementAtIndex(x).FindPropertyRelative(keyPropertyName), x => x, _dictionaryKeyHash.Comparer)
                .Where(x => x.Count() > 1)
                .ToList();

            // there are duplicates - take the slow and allocating path now
            return string.Join("\n", groups.Select(x => $"Duplicate keys for elements: {string.Join(", ", x)}"));
          }
        }

        return null;

      } finally {
        _dictionaryKeyHash.Clear();
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/CustomTypes/TickRateDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(TickRate.Selection))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  public class TickRateDrawer : PropertyDrawer { 
    private const int PAD = 0;
    
    // Cached pop items for client rate
    private static GUIContent[] _clientRateOptions;
    private static GUIContent[] ClientRateOptions {
      get {
        if (_clientRateOptions != null) {
          return _clientRateOptions;
        }
        ExtractClientRates();
        return _clientRateOptions;
      }
    }
    
    // Cached pop items for client rate
    private static int[] _clientRateValues;
    private static int[] ClientRateValues {
      get {
        if (_clientRateValues != null) {
          return _clientRateValues;
        }
        ExtractClientRates();
        return _clientRateValues;
      }
    }
    //
    // private static GUIContent[] _ratioOptions = new GUIContent[4];
    // private static int[] _ratioValues  = new int[] { 0, 1, 2, 3 };

    private static readonly GUIContent[][] _reusableRatioGUIArrays = new GUIContent[4][] { new GUIContent[1], new GUIContent[2], new GUIContent[3], new GUIContent[4] };
    private static readonly int[][]        _reusableRatioIntArrays = new int[4][] { new int[1], new int[2], new int[3], new int[4] };
    
    private static readonly GUIContent[][] _reusableServerGUIArrays = new GUIContent[4][] { new GUIContent[1], new GUIContent[2], new GUIContent[3], new GUIContent[4] };
    private static readonly int[][]        _reusableServerIntArrays = new int[4][] { new int[1], new int[2], new int[3], new int[4] };
    
    private static readonly LazyGUIStyle _buttonStyle = LazyGUIStyle.Create(_ => new GUIStyle(EditorStyles.miniButton) {
      fontSize  = 9,
      alignment = TextAnchor.MiddleCenter
    });

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return (base.GetPropertyHeight(property, label) + EditorGUIUtility.standardVerticalSpacing) * 4 + PAD * 2;
    }

    private int DrawPopup(ref Rect labelRect, ref Rect fieldRect, float rowHeight, GUIContent guiContent, int[] sliderValues, int[] values, GUIContent[] options, int currentValue, int offset = 0) {
      
      EditorGUI.LabelField(labelRect, guiContent);
      int indentHold = EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;
      int  value;
      Rect dropRect = fieldRect;
      
      if (fieldRect.width > 120) {
        var slideRect   = new Rect(fieldRect) { xMax = fieldRect.xMax - 64 };
        dropRect = new Rect(fieldRect) { xMin = fieldRect.xMax - 64 };
      
        var sliderRange = Math.Max(3, sliderValues.Length             - 1);
        if (sliderRange == 3) {
          var dividerRect = new Rect(slideRect);
          // dividerRect.yMin += 2;
          // dividerRect.yMax -= 2;
          var quarter     = slideRect.width * 1f /4;
          
          using (new EditorGUI.DisabledScope(!(options.Length + offset >= 4))) {
            if (GUI.Toggle(new Rect(dividerRect) { width = quarter }, currentValue == 3, new GUIContent("1/8"), _buttonStyle )) {
               currentValue = 3;
            }
          }
          using (new EditorGUI.DisabledScope(!(options.Length + offset >= 3 && offset <= 2))) {
            if (GUI.Toggle(new Rect(dividerRect) { width = quarter, x = dividerRect.x + quarter }, currentValue == 2, new GUIContent("1/4"), _buttonStyle)) {
               currentValue = 2;
            }
          }
          using (new EditorGUI.DisabledScope(!(options.Length + offset >= 2 && offset <= 1))) {
            if (GUI.Toggle(new Rect(dividerRect) { width = quarter, x = dividerRect.x + quarter * 2 }, currentValue == 1, new GUIContent("1/2"), _buttonStyle)) {
               currentValue = 1;
            }
          }
          using (new EditorGUI.DisabledScope(!(options.Length + offset >= 1 && offset == 0))) {
            if (GUI.Toggle(new Rect(dividerRect) { width = quarter, x = dividerRect.x + quarter * 3 }, currentValue == 0, new GUIContent("1:1"), _buttonStyle)) {
              currentValue = 0;
            }
          }
          EditorGUI.LabelField(dropRect, options[currentValue - offset], new GUIStyle(EditorStyles.label){padding = new RectOffset(4, 0, 0, 0)});
          value = values[currentValue - offset];

        } else {
          currentValue = (int)GUI.HorizontalSlider(slideRect, (float)currentValue, sliderRange, 0);
          
          // Clamp slider ranges into valid enum ranges
          if (currentValue - offset < 0) {
            currentValue = offset;
          }
          else if (currentValue - offset >= values.Length) {
            currentValue = values.Length - 1 + offset;
          }
          value = values[EditorGUI.Popup(dropRect, GUIContent.none, currentValue - offset, options)];
          // value = values[EditorGUI.Popup(fieldRect, GUIContent.none, currentValue - offset, options)];

        }

      } else {
      
        // Handling for very narrow window. Falls back to just a basic popup for each value.
        dropRect = fieldRect;
        value = values[EditorGUI.Popup(dropRect, GUIContent.none, currentValue - offset, options)];
      }
      
      EditorGUI.indentLevel = indentHold;
      labelRect.y += rowHeight + EditorGUIUtility.standardVerticalSpacing;
      fieldRect.y += rowHeight + EditorGUIUtility.standardVerticalSpacing;

      return value;
    }
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {

        var rowHeight = base.GetPropertyHeight(property, label);

        // using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        // EditorGUI.LabelField(new Rect(position){ yMin = position.yMin + rowHeight}, GUIContent.none, FusionGUIStyles.GroupBoxType.Gray.GetStyle());

        position = new Rect(position) {
          xMin = position.xMin + PAD, 
          xMax = position.xMax - PAD, 
          yMin = position.yMin + PAD, 
          yMax = position.yMax - PAD
        };

        var clientRateProperty = property.FindPropertyRelativeOrThrow(nameof(TickRate.Selection.Client));
        var serverRateProperty = property.FindPropertyRelativeOrThrow(nameof(TickRate.Selection.ServerIndex));
        var clientSendProperty = property.FindPropertyRelativeOrThrow(nameof(TickRate.Selection.ClientSendIndex));
        var serverSendProperty = property.FindPropertyRelativeOrThrow(nameof(TickRate.Selection.ServerSendIndex));
        
        var selection = GetSelectionValue(property);
        
        var hold            = selection;
        var tickRate        = TickRate.Get(TickRate.IsValid(selection.Client) ? selection.Client : TickRate.Default.Client);
        var clientRateIndex = GetIndexForClientRate(tickRate.Client);

        var rect = new Rect(position) { height = base.GetPropertyHeight(property, label) };

        //var fieldWidth =  Math.Max(Math.Min(position.width * .33f, MAX_FIELD_WIDTH), MIN_FIELD_WIDTH);
        var labelWidth = EditorGUIUtility.labelWidth;

        
        var labelRect = new Rect(rect) { width = labelWidth}; // { xMax = rect.xMax - fieldWidth }};
        //var fieldRect = new Rect(rect) { xMin  = rect.xMax -fieldWidth };
        var fieldRect = new Rect(rect) { xMin = rect.xMin  + labelWidth};
        
        // CLIENT SIM RATE

        selection.Client = DrawPopup(ref labelRect, ref fieldRect, rowHeight, new GUIContent("Client Tick Rate"), _clientRateValues,_clientRateValues, ClientRateOptions, clientRateIndex);
  
        // TODO: This validates every tick without checking for changes. May be good, may not.
        selection = tickRate.ClampSelection(selection);

        // CLIENT SEND RATE
        var ratioOptions = _reusableRatioGUIArrays[tickRate.Count - 1]; // _ratioOptions;
        var ratioValues  = _reusableRatioIntArrays[tickRate.Count - 1]; //_ratioValues;
        for (var i = 0; i < tickRate.Count; ++i) {
          ratioOptions[i] = new GUIContent(tickRate.GetTickRate(i).ToString());
          ratioValues[i]  = i;
        }

        selection.ClientSendIndex = DrawPopup(ref labelRect, ref fieldRect, rowHeight, new GUIContent("Client Send Rate"), ratioValues, ratioValues, ratioOptions, selection.ClientSendIndex);
        
        // SERVER SIM RATE
        selection.ServerIndex = DrawPopup(ref labelRect, ref fieldRect, rowHeight, new GUIContent("Server Tick Rate"), ratioValues, ratioValues, ratioOptions, selection.ServerIndex);
        
        selection = tickRate.ClampSelection(selection);
        
        // SERVER SEND RATE - uses a subset of ratio - since it CANNOT be higher than Server Rate.
        var sOffset      = selection.ServerIndex;
        var sLen         = ratioOptions.Length - sOffset;
        var sSendOptions = _reusableServerGUIArrays[sLen - 1]; // new GUIContent[sLen];
        var sSendValues  = _reusableServerIntArrays[sLen - 1]; // new int[sLen];

        for (var i = 0; i < sLen; ++i) {
          sSendOptions[i] = ratioOptions[i + sOffset];
          sSendValues[i]  = ratioValues[i  + sOffset];
        }

        selection.ServerSendIndex = DrawPopup(ref labelRect, ref fieldRect, rowHeight, new GUIContent("Server Send Rate"), ratioValues, sSendValues, sSendOptions, selection.ServerSendIndex, sOffset);

        if (hold.Equals(selection) == false) {
          selection = tickRate.ClampSelection(selection);

          // FIELD INFO SET VALUE ALTERNATIVE
          // fieldInfo.SetValue(targetObject, selection);

          clientRateProperty.intValue = selection.Client;
          clientSendProperty.intValue = selection.ClientSendIndex;
          serverRateProperty.intValue = selection.ServerIndex;
          serverSendProperty.intValue = selection.ServerSendIndex;
          property.serializedObject.ApplyModifiedProperties();
        }
      }
    }
    
    private int GetIndexForClientRate(int clientRate) {
      for (var i = ClientRateValues.Length - 1; i >= 0; --i)
        if (_clientRateValues[i] == clientRate) {
          return i;
        }
      return -1;
    }
    
    
    // Extract in reverse order so all the popups are consistent.
    private static void ExtractClientRates() {
      int cnt = TickRate.Available.Count;
      
      _clientRateOptions = new GUIContent[cnt];
      _clientRateValues  = new int[cnt];
      for (int i = 0, reverse = cnt -1; i < cnt; ++i, --reverse) {
        _clientRateOptions[i] = new GUIContent(TickRate.Available[reverse].Client.ToString());
        _clientRateValues[i]  = TickRate.Available[reverse].Client;
      }
    }
    
    // Wacky reflection to locate the value
    private static TickRate.Selection GetSelectionValue (SerializedProperty property) {
      object   obj   = property.serializedObject.targetObject;
      string   path  = property.propertyPath;
      string[] parts = path.Split ('.');
      foreach (var t in parts) {
        obj = GetValueFromFieldName (t, obj);
      }
      return (TickRate.Selection)obj;
    }

    private static object GetValueFromFieldName(string name, object obj, BindingFlags bindings = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) {
      FieldInfo field = obj.GetType().GetField(name, bindings);
      if (field != null) {
        return field.GetValue(obj);
      }

      return TickRate.Default;
    }

  }
}

#endregion


#region Assets/Photon/Fusion/Editor/DebugDllToggle.cs

namespace Fusion.Editor {
  using System;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public static class DebugDllToggle {

    const string FusionRuntimeDllGuid = "e725a070cec140c4caffb81624c8c787";

    public static string[] FileList = new[] {
      "Fusion.Common.dll",
      "Fusion.Common.pdb",
      "Fusion.Runtime.dll",
      "Fusion.Runtime.pdb",
      "Fusion.Realtime.dll",
      "Fusion.Realtime.pdb",
      "Fusion.Sockets.dll",
      "Fusion.Sockets.pdb"};

    [MenuItem("Tools/Fusion/Toggle Debug Dlls")]
    public static void Toggle() {

      // find the root
      string dir;
      {
        var fusionRuntimeDllPath = AssetDatabase.GUIDToAssetPath(FusionRuntimeDllGuid);
        if (string.IsNullOrEmpty(fusionRuntimeDllPath)) {
          Debug.LogError($"Cannot locate assemblies directory");
          return;
        } else {
          dir = PathUtils.Normalize(Path.GetDirectoryName(fusionRuntimeDllPath));
        }
      }

      var dllsAvailable       = FileList.All(f => File.Exists($"{dir}/{f}"));
      var debugFilesAvailable = FileList.All(f => File.Exists($"{dir}/{f}.debug"));

      if (dllsAvailable == false) {
        Debug.LogError("Cannot find all fusion dlls");
        return;
      }

      if (debugFilesAvailable == false) {
        Debug.LogError("Cannot find all specially marked .debug dlls");
        return;
      }

      if (FileList.Any(f => new FileInfo($"{dir}/{f}.debug").Length == 0)) { 
        Debug.LogError("Debug dlls are not valid");
        return;
      }

      try {
        foreach (var f in FileList) {
          var tempFile = FileUtil.GetUniqueTempPathInProject();
          FileUtil.MoveFileOrDirectory($"{dir}/{f}",        tempFile);
          FileUtil.MoveFileOrDirectory($"{dir}/{f}.debug",  $"{dir}/{f}");
          FileUtil.MoveFileOrDirectory(tempFile,            $"{dir}/{f}.debug");
          File.Delete(tempFile);
        }

        if (new FileInfo($"{dir}/{FileList[0]}").Length >
            new FileInfo($"{dir}/{FileList[0]}.debug").Length) {
          Debug.Log("Activated Fusion DEBUG dlls");
        }
        else  {
          Debug.Log("Activated Fusion RELEASE dlls");
        }
      } catch (Exception e) {
        Debug.LogAssertion(e);
        Debug.LogError($"Failed to rename files");
      }

      AssetDatabase.Refresh();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/EditorRecompileHook.cs

namespace Fusion.Editor {
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEditor.Compilation;
  using UnityEngine;

  [InitializeOnLoad]
  public static class EditorRecompileHook {
    static EditorRecompileHook() {
      
      EditorApplication.update += delegate {
        if (PlayerSettings.allowUnsafeCode == false) {
          PlayerSettings.allowUnsafeCode = true;
          
          // request re-compile
          CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
        }
      };
      
      AssemblyReloadEvents.beforeAssemblyReload += ShutdownRunners;

      CompilationPipeline.compilationStarted    += _ => ShutdownRunners();
      CompilationPipeline.compilationStarted    += _ => StoreConfigPath();
    }

    static void ShutdownRunners() {
      var runners = NetworkRunner.GetInstancesEnumerator();

      while (runners.MoveNext()) {
        if (runners.Current) {
          runners.Current.Shutdown();
        }
      }
    }

    static void StoreConfigPath() {
      const string ConfigPathCachePath = "Temp/FusionILWeaverConfigPath.txt";

      var configPath = NetworkProjectConfigUtilities.GetGlobalConfigPath();
      if (string.IsNullOrEmpty(configPath)) {
        // delete
        try {
          File.Delete(ConfigPathCachePath);
        } catch (FileNotFoundException) {
          // ok
        } catch (Exception ex) {
          FusionEditorLog.ErrorConfig($"Error when clearing the config path file for the Weaver. Weaving results may be invalid: {ex}");
        }
      } else {
        try {
          System.IO.File.WriteAllText(ConfigPathCachePath, configPath);
        } catch (Exception ex) {
          FusionEditorLog.ErrorConfig($"Error when writing the config path file for the Weaver. Weaving results may be invalid: {ex}");
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/FusionAssistants.cs

namespace Fusion.Editor {
  using UnityEngine;
  using System;

  static class FusionAssistants {
    public const int PRIORITY = 0;
    public const int PRIORITY_LOW = 1000;

    /// <summary>
    /// Ensure GameObject has component T. Will create as needed and return the found/created component.
    /// </summary>
    public static T EnsureComponentExists<T>(this GameObject go) where T : Component {
      if (go.TryGetComponent<T>(out var t))
        return t;

      else
        return go.AddComponent<T>();
    }

    public static GameObject EnsureComponentsExistInScene(string preferredGameObjectName, params Type[] components) {

      GameObject go = null;

      foreach(var c in components) {
        var found = UnityEngine.Object.FindObjectOfType(c);
        if (found)
          continue;

        if (go == null)
          go = new GameObject(preferredGameObjectName);

        go.AddComponent(c);
      }

      return go;
    }

    public static T EnsureExistsInScene<T>(string preferredGameObjectName = null, GameObject onThisObject = null, params Type[] otherRequiredComponents) where T : Component {

      if (preferredGameObjectName == null)
        preferredGameObjectName = typeof(T).Name;

      T comp;
      comp = UnityEngine.Object.FindObjectOfType<T>();
      if (comp == null) {
        // T was not found in scene, create a new gameobject and add T, as well as other required components
        if (onThisObject == null)
          onThisObject = new GameObject(preferredGameObjectName);
        comp = onThisObject.AddComponent<T>();
        foreach (var add in otherRequiredComponents) {
          onThisObject.AddComponent(add);
        }
      } else {
        // Make sure existing found T has the indicated extra components as well.
        foreach (var add in otherRequiredComponents) {
          if (comp.GetComponent(add) == false)
            comp.gameObject.AddComponent(add);
        }
      }
      return comp;
    }

    /// <summary>
    /// Create a scene object with all of the supplied arguments and parameters applied.
    /// </summary>
    public static GameObject CreatePrimitive(
      PrimitiveType? primitive,
      string name,
      Vector3? position,
      Quaternion? rotation,
      Vector3? scale,
      Transform parent,
      Material material,
      params Type[] addComponents) {

      GameObject go;
      if (primitive.HasValue) {
        go = GameObject.CreatePrimitive(primitive.Value);

        go.name = name;

        if (material != null)
          go.GetComponent<Renderer>().material = material;

        foreach (var type in addComponents) {
          go.AddComponent(type);
        }

      } else {
        go = new GameObject(name, addComponents);
      }

      if (position.HasValue)
        go.transform.position = position.Value;

      if (rotation.HasValue)
        go.transform.rotation = rotation.Value;

      if (scale.HasValue)
        go.transform.localScale = scale.Value;

      if (parent)
        go.transform.parent = parent;

      return go;
    }

    internal static EnableOnSingleRunner EnsureComponentHasVisibilityNode(this Component component) {
      var allExistingNodes = component.GetComponents<EnableOnSingleRunner>();
      foreach (var existingNodes in allExistingNodes) {
        foreach (var comp in existingNodes.Components) {
          if (comp == component) {
            return existingNodes;
          }
        }
      }

      // Component is not represented yet. If there is a VisNodes already, use it. Otherwise make one.
      EnableOnSingleRunner targetNodes = component.GetComponent<EnableOnSingleRunner>();
      if (targetNodes == null) {
        targetNodes = component.gameObject.AddComponent<EnableOnSingleRunner>();
      }

      // Add this component to the collection.
      int newArrayPos = targetNodes.Components.Length;
      Array.Resize(ref targetNodes.Components, newArrayPos + 1);
      targetNodes.Components[newArrayPos] = component;
      return targetNodes;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/FusionBootstrapEditor.cs

namespace Fusion.Editor {
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  [CustomEditor(typeof(FusionBootstrap))]
  public class FusionBootstrapEditor : BehaviourEditor {

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      if (Application.isPlaying)
        return;

      var currentScene = SceneManager.GetActiveScene();
      if (!currentScene.IsAddedToBuildSettings()) {
        using (new FusionEditorGUI.WarningScope("Current scene is not added to Build Settings list.")) {
          if (GUILayout.Button("Add Scene To Build Settings")) {
            if (currentScene.name == "") {
              UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
        
            if (currentScene.name != "") {
              EditorBuildSettings.scenes = EditorBuildSettings.scenes
               .Concat(new[] { new EditorBuildSettingsScene(currentScene.path, true) })
               .ToArray();
            }  
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/FusionBuildTriggers.cs

namespace Fusion.Editor {
 
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditor.Build.Reporting;


  public class FusionBuildTriggers : IPreprocessBuildWithReport {

    public const int CallbackOrder = 1000;

    public int callbackOrder => CallbackOrder;

    public void OnPreprocessBuild(BuildReport report) {
      if (report.summary.platformGroup != BuildTargetGroup.Standalone) {
        return;
      }

      if (!PlayerSettings.runInBackground) {
        FusionEditorLog.Warn($"Standalone builds should have {nameof(PlayerSettings)}.{nameof(PlayerSettings.runInBackground)} enabled. " +
          $"Otherwise, loss of application focus may result in connection termination.");
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/FusionEditor.Common.cs

// merged Editor

#region AssetDatabaseUtils.Addressables.cs

#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor.AddressableAssets.Settings;
  using UnityEngine;

  public partial class AssetDatabaseUtils {
    public static void AddAddressableAssetsWithLabelMonitor(string label, Action<Hash128> handler) {
      AddressableAssetSettings.OnModificationGlobal += (settings, modificationEvent, data) => {
        switch (modificationEvent) {
          case AddressableAssetSettings.ModificationEvent.EntryAdded:
          case AddressableAssetSettings.ModificationEvent.EntryCreated:
          case AddressableAssetSettings.ModificationEvent.EntryModified:
          case AddressableAssetSettings.ModificationEvent.EntryMoved:

            IEnumerable<AddressableAssetEntry> entries;
            if (data is AddressableAssetEntry singleEntry) {
              entries = Enumerable.Repeat(singleEntry, 1);
            } else {
              entries = (IEnumerable<AddressableAssetEntry>)data;
            }

            List<AddressableAssetEntry> allEntries = new List<AddressableAssetEntry>();
            foreach (var entry in entries) {
              entry.GatherAllAssets(allEntries, true, true, true);
              if (allEntries.Any(x => HasLabel(x.AssetPath, label))) {
                handler(settings.currentHash);
                break;
              }

              allEntries.Clear();
            }

            break;

          case AddressableAssetSettings.ModificationEvent.EntryRemoved:
            // TODO: check what has been removed
            handler(settings.currentHash);
            break;
        }
      };
    }
  }
}
#endif

#endregion


#region AssetDatabaseUtils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.PackageManager;
  using UnityEngine;

  public static partial class AssetDatabaseUtils {
    
    public static void SetAssetAndTheMainAssetDirty(UnityEngine.Object obj) {
      EditorUtility.SetDirty(obj);
      
      var assetPath = AssetDatabase.GetAssetPath(obj);
      if (string.IsNullOrEmpty(assetPath)) {
        return;
      }
      var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
      if (!mainAsset || mainAsset == obj) {
        return;
      }
      EditorUtility.SetDirty(mainAsset);
    }
    
    public static string GetAssetPathOrThrow(int instanceID) {
      var result = AssetDatabase.GetAssetPath(instanceID);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset with InstanceID {instanceID} not found");
      }
      return result;
    }
    
    public static string GetAssetPathOrThrow(string assetGuid) {
      var result = AssetDatabase.GUIDToAssetPath(assetGuid);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset with Guid {assetGuid} not found");
      }

      return result;
    }

    public static string GetAssetGuidOrThrow(string assetPath) {
      var result = AssetDatabase.AssetPathToGUID(assetPath);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset with path {assetPath} not found");
      }

      return result;
    }
    
    
    public static (string, long) GetGUIDAndLocalFileIdentifierOrThrow<T>(LazyLoadReference<T> reference) where T : UnityEngine.Object {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out var guid, out long localId)) {
        throw new ArgumentException($"Asset with instanceId {reference} not found");
      }

      return (guid, localId);
    }

    public static (string, long) GetGUIDAndLocalFileIdentifierOrThrow(int instanceId) {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceId, out var guid, out long localId)) {
        throw new ArgumentException($"Asset with instanceId {instanceId} not found");
      }

      return (guid, localId);
    }

    public static bool HasLabel(string assetPath, string label) {
      var guidStr = AssetDatabase.AssetPathToGUID(assetPath);
      if (!GUID.TryParse(guidStr, out var guid)) {
        return false;
      }

      var labels = AssetDatabase.GetLabels(guid);
      var index  = Array.IndexOf(labels, label);
      return index >= 0;
    }

    public static bool HasLabel(UnityEngine.Object obj, string label) {
      var labels = AssetDatabase.GetLabels(obj);
      var index  = Array.IndexOf(labels, label);
      return index >= 0;
    }
    
    public static bool HasLabel(GUID guid, string label) {
      var labels = AssetDatabase.GetLabels(guid);
      var index  = Array.IndexOf(labels, label);
      return index >= 0;
    }
    
    public static bool HasAnyLabel(string assetPath, params string[] labels) {
      var guidStr = AssetDatabase.AssetPathToGUID(assetPath);
      if (!GUID.TryParse(guidStr, out var guid)) {
        return false;
      }

      var assetLabels = AssetDatabase.GetLabels(guid);
      foreach (var label in labels) {
        if (Array.IndexOf(assetLabels, label) >= 0) {
          return true;
        }
      }

      return false;
    }
    
    public static bool SetLabel(string assetPath, string label, bool present) {
      var guid = AssetDatabase.GUIDFromAssetPath(assetPath);
      if (guid.Empty()) {
        return false;
      }
      
      var labels = AssetDatabase.GetLabels(guid);
      var index  = Array.IndexOf(labels, label);
      if (present) {
        if (index >= 0) {
          return false;
        }
        ArrayUtility.Add(ref labels, label);
      } else {
        if (index < 0) {
          return false;
        }
        ArrayUtility.RemoveAt(ref labels, index);
      }

      var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
      if (obj == null) {
        return false;
      }
      
      AssetDatabase.SetLabels(obj, labels);
      return true;
    }

    public static bool SetLabel(UnityEngine.Object obj, string label, bool present) {
      var labels = AssetDatabase.GetLabels(obj);
      var index  = Array.IndexOf(labels, label);
      if (present) {
        if (index >= 0) {
          return false;
        }
        ArrayUtility.Add(ref labels, label);
      } else {
        if (index < 0) {
          return false;
        }
        ArrayUtility.RemoveAt(ref labels, index);
      }

      AssetDatabase.SetLabels(obj, labels);
      return true;
    }
    
    public static bool SetLabels(string assetPath, string[] labels) {
      var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
      if (obj == null) {
        return false;
      }
      
      AssetDatabase.SetLabels(obj, labels);
      return true;
    }
    
    public static bool HasScriptingDefineSymbol(BuildTargetGroup group, string value) {
      var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';');
      return System.Array.IndexOf(defines, value) >= 0;
    }
    
    private static bool IsEnumValueObsolete<T>(string valueName) where T : System.Enum {
      var fi         = typeof(T).GetField(valueName);
      var attributes = fi.GetCustomAttributes(typeof(System.ObsoleteAttribute), false);
      return attributes?.Length > 0;
    }
    
    private static IEnumerable<BuildTargetGroup> ValidBuildTargetGroups {
      get {
        foreach (var name in System.Enum.GetNames(typeof(BuildTargetGroup))) {
          if (IsEnumValueObsolete<BuildTargetGroup>(name))
            continue;
          var group = (BuildTargetGroup)System.Enum.Parse(typeof(BuildTargetGroup), name);
          if (group == BuildTargetGroup.Unknown)
            continue;

          yield return group;
        }
      }
    }
    
    public static bool? HasScriptingDefineSymbol(string value) {
      bool anyDefined = false;
      bool anyUndefined = false;
      foreach (BuildTargetGroup group in ValidBuildTargetGroups) {
        if (HasScriptingDefineSymbol(group, value)) {
          anyDefined = true;
        } else {
          anyUndefined = true;
        }
      }

      return (anyDefined && anyUndefined) ? (bool?)null : anyDefined;
    }

    public static void UpdateScriptingDefineSymbol(BuildTargetGroup group, string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(new[] { group },
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }

    public static void UpdateScriptingDefineSymbol(string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }

    public static void UpdateScriptingDefineSymbol(BuildTargetGroup group, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      UpdateScriptingDefineSymbolInternal(new[] { group },
        definesToAdd,
        definesToRemove);
    }

    public static void UpdateScriptingDefineSymbol(IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        definesToAdd,
        definesToRemove);
    }

    private static void UpdateScriptingDefineSymbolInternal(IEnumerable<BuildTargetGroup> groups, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      EditorApplication.LockReloadAssemblies();
      try {
        foreach (var group in groups) {
          var originalDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
          var defines = originalDefines.Split(';').ToList();

          if (definesToRemove != null) {
            foreach (var d in definesToRemove) {
              defines.Remove(d);
            }
          }

          if (definesToAdd != null) {
            foreach (var d in definesToAdd) {
              defines.Remove(d);
              defines.Add(d);
            }
          }

          var newDefines = string.Join(";", defines);
          PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
        }
      } finally {
        EditorApplication.UnlockReloadAssemblies();
      }
    }
    
    public static AssetEnumerable IterateAssets<T>(string root = null, string label = null) where T : UnityEngine.Object {
      return IterateAssets(root, label, typeof(T));
    }
    
    public static AssetEnumerable IterateAssets(string root = null, string label = null, Type type = null) {
      return new AssetEnumerable(root, label, type);
    }

    public struct AssetEnumerator : IEnumerator<HierarchyProperty> {

      private HierarchyProperty _hierarchyProperty;
      private int               _rootFolderIndex;

      private readonly string[] _rootFolders;
      private readonly string   _searchFilter;

      private static bool IsPackageHidden(UnityEditor.PackageManager.PackageInfo info) => info.type == "module" || info.type == "feature" && info.source != PackageSource.Embedded;

      public AssetEnumerator(string root, string label, Type type) {

        {
          string searchFilter = "";
          if (type != null) {
            searchFilter += "t:" + type.Name;
          }

          if (!string.IsNullOrEmpty(label)) {
            if (searchFilter.Length > 0) {
              searchFilter += " ";
            }

            searchFilter += "l:" + label;
          }

          _searchFilter = searchFilter;
        }

        _rootFolderIndex = 0;
        if (string.IsNullOrEmpty(root)) {
          // search everywhere
          _rootFolders = new[] { "Assets" }.Concat(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
             .Where(x => !IsPackageHidden(x))
             .Select(x => x.assetPath))
           .ToArray();
          _hierarchyProperty = new HierarchyProperty(_rootFolders[0]);
        } else {
          _rootFolders       = null;
          _hierarchyProperty = new HierarchyProperty(root);
        }

        _hierarchyProperty.SetSearchFilter(_searchFilter, (int)SearchableEditorWindow.SearchMode.All);
      }

      public bool MoveNext() {
        if (_hierarchyProperty.Next(null)) {
          return true;
        }

        if (_rootFolders == null || _rootFolderIndex + 1 >= _rootFolders.Length) {
          return false;
        }

        var newHierarchyProperty = new HierarchyProperty(_rootFolders[++_rootFolderIndex]);
        newHierarchyProperty.SetSearchFilter(_searchFilter, (int)SearchableEditorWindow.SearchMode.All);
        _hierarchyProperty = newHierarchyProperty;

        // try again
        return MoveNext();
      }

      public void Reset() {
        throw new System.NotImplementedException();
      }

      public HierarchyProperty Current => _hierarchyProperty;

      object IEnumerator.Current => Current;

      public void Dispose() {
      }
    }

    public struct AssetEnumerable : IEnumerable<HierarchyProperty> {

      private readonly string _root;
      private readonly string _label;
      private readonly Type   _type;

      public AssetEnumerable(string root, string label, Type type) {
        _type  = type;
        _root  = root;
        _label = label;
      }

      public AssetEnumerator GetEnumerator() => new AssetEnumerator(_root, _label, _type);

      IEnumerator<HierarchyProperty> IEnumerable<HierarchyProperty>.GetEnumerator() => GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
  }
}

#endregion


#region EditorButtonDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public struct EditorButtonDrawer {

    private struct ButtonEntry {
      public MethodInfo                                Method;
      public GUIContent                                Content;
      public EditorButtonAttribute                     Attribute;
      public (DoIfAttributeBase, Func<object, object>)[] DoIfs;
    }
    
    private Editor            _lastEditor;
    private List<ButtonEntry> _buttons;

    public void Draw(Editor editor) {
      var targets    = editor.targets;

      if (_lastEditor != editor) {
        _lastEditor = editor;
        Refresh(editor);
      }

      if (_buttons == null || targets == null || targets.Length == 0) {
        return;
      }

      foreach (var entry in _buttons) {

        if (entry.Attribute.Visibility == EditorButtonVisibility.PlayMode && !EditorApplication.isPlaying) {
          continue;
        }

        if (entry.Attribute.Visibility == EditorButtonVisibility.EditMode && EditorApplication.isPlaying) {
          continue;
        }
        
        if (!entry.Attribute.AllowMultipleTargets && editor.targets.Length > 1) {
          continue;
        }
        
        bool   readOnly       = false;
        bool   hidden         = false;
        string warningMessage = null;
        bool warningAsBox = false;
        
        foreach (var (doIf, getter) in entry.DoIfs) {

          bool checkResult;
          
          if (getter == null) {
            // use the property
            var rootProperty = editor.serializedObject.GetIterator();
            checkResult = DoIfAttributeDrawer.CheckDraw(doIf, rootProperty);
          } else {
            var value = getter(targets[0]);
            checkResult = DoIfAttributeDrawer.CheckCondition(doIf, value);
          }
          
          if (!checkResult) {
            if (doIf is DrawIfAttribute drawIf) {
              if (drawIf.Hide) {
                hidden = true;
                break;
              } else {
                readOnly = true;
              }
            } else if (doIf is WarnIfAttribute warnIf) {
              warningMessage = warnIf.Message;
              warningAsBox   = warnIf.AsBox;
            }
          }
        }
        
        if (hidden) {
          continue;
        }

        using (warningMessage == null ? null : (IDisposable)new FusionEditorGUI.WarningScope(warningMessage)) {
          var rect = FusionEditorGUI.LayoutHelpPrefix(editor, entry.Method);
          using (new EditorGUI.DisabledScope(readOnly)) {
            if (GUI.Button(rect, entry.Content)) {
              EditorGUI.BeginChangeCheck();
              
              if (entry.Method.IsStatic) {
                entry.Method.Invoke(null, null);
              } else {
                foreach (var target in targets) {
                  entry.Method.Invoke(target, null);
                  if (entry.Attribute.DirtyObject) {
                    EditorUtility.SetDirty(target);
                  }
                }
              }

              if (EditorGUI.EndChangeCheck()) {
                editor.serializedObject.Update();
              }
            }
          }
        }
      }
    }

    private void Refresh(Editor editor) {
      if (editor == null) {
        throw new ArgumentNullException(nameof(editor));
      }
      
      var targetType = editor.target.GetType();

      _buttons = targetType
       .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
       .Where(x => x.GetParameters().Length == 0 && x.IsDefined(typeof(EditorButtonAttribute)))
       .Select(method => {
          var attribute = method.GetCustomAttribute<EditorButtonAttribute>();
          var label     = new GUIContent(attribute.Label ?? ObjectNames.NicifyVariableName(method.Name));
          var drawIfs = method.GetCustomAttributes<DoIfAttributeBase>()
           .Select(x => {
              var prop = editor.serializedObject.FindProperty(x.ConditionMember);
              return prop != null ? (x, null) : (x, targetType.CreateGetter(x.ConditionMember));
            })
             .ToArray();
          return new ButtonEntry() {
            Attribute = attribute,
            Content   = label,
            Method    = method,
            DoIfs     = drawIfs,
          };
        })
       .OrderBy(x => x.Attribute.Priority)
       .ToList();
    }
  }
}

#endregion


#region EnumDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public struct EnumDrawer {
    private Mask256[]   _values;
    private string[]    _names;
    private bool        _isFlags;
    private Type        _enumType;
    private Mask256     _allBitMask;
    private FieldInfo[] _fields;
    
    [NonSerialized]
    private List<int> _selectedIndices;

    public Mask256[]   Values   => _values;
    public string[]    Names    => _names;
    public bool        IsFlags  => _isFlags;
    public Type        EnumType => _enumType;
    public Mask256     BitMask  => _allBitMask;
    public FieldInfo[] Fields   => _fields;
    
    public bool EnsureInitialized(Type enumType, bool includeFields) {

      if (enumType == null) {
        throw new ArgumentNullException(nameof(enumType));
      }

      bool isEnum = enumType.IsEnum;
      
      if (!isEnum && !typeof(FieldsMask).IsAssignableFrom(enumType)) {
        throw new ArgumentException("Type must be an enum or FieldsMask", nameof(enumType));
      }
      
      // Already initialized
      if (_enumType == enumType) {
        return false;
      }

      if (isEnum) {
        var enumUnderlyingType = Enum.GetUnderlyingType(enumType);
        var rawValues          = Enum.GetValues(enumType);

        _fields   = includeFields ? new FieldInfo[rawValues.Length] : null;
        _names    = Enum.GetNames(enumType);
        _values   = new Mask256[rawValues.Length];
        _isFlags  = enumType.GetCustomAttribute<FlagsAttribute>() != null;
        _enumType = enumType;
      
        for (int i = 0; i < rawValues.Length; ++i) {
          if (enumUnderlyingType == typeof(int)   || 
              enumUnderlyingType == typeof(long)  || 
              enumUnderlyingType == typeof(short) ||
              enumUnderlyingType == typeof(byte)) {
            _values[i] = Convert.ToInt64(rawValues.GetValue(i));
          } else {
            _values[i] = unchecked((long)Convert.ToUInt64(rawValues.GetValue(i)));
          }
          
          _allBitMask[0] |= _values[i][0];
          if (includeFields) {
            _fields[i] = enumType.GetField(_names[i], BindingFlags.Static | BindingFlags.Public);
          } 
        }

      } else {
        // Handling for FieldsMask
        var tType = enumType.GenericTypeArguments[0];

        _fields   = tType.GetFields();
        _names    = new string[_fields.Length];
        _values   = new Mask256[_fields.Length];
        _isFlags  = true;
        _enumType = enumType;
        
        for (int i = 0; i < _values.Length; i++) {
          long value = (long)1 << i;
          _allBitMask.SetBit(i, true);;
          _values[i].SetBit(i, true); //  =   (long)1 << i;
          _names[i]   =  _fields[i].Name;
        }
      }

      for (int i = 0; i < _names.Length; ++i) {
        _names[i] = ObjectNames.NicifyVariableName(_names[i]);
      }

      return true;
    }

    public void Draw(Rect position, SerializedProperty property, Type enumType, bool isEnum) {
      
      if (property == null) {
        throw new ArgumentNullException(nameof(property));
      }

      EnsureInitialized(enumType, false);
      Mask256 currentValue;
      
      if (isEnum) {
        currentValue = new Mask256(
          property.longValue
        );
      } else {
        currentValue = new Mask256(
          property.GetFixedBufferElementAtIndex(0).longValue,
          property.GetFixedBufferElementAtIndex(1).longValue,
          property.GetFixedBufferElementAtIndex(2).longValue,
          property.GetFixedBufferElementAtIndex(3).longValue
        );
      }

      _selectedIndices ??= new List<int>();
      _selectedIndices.Clear();
      
      // find out what to show
      for (int i = 0; i < _values.Length; ++i) {
        var value = _values[i];
        if (_isFlags == false) {
          if (currentValue[0]== value[0]) {
            _selectedIndices.Add(i);
            break;
          }
        } else if (Equals(currentValue & value, value)) {
          _selectedIndices.Add(i);
        }
      }
      
      string labelValue;
      if (_selectedIndices.Count == 0) {
        if (_isFlags && currentValue.IsNothing()) {
          labelValue = "Nothing";
        } else {
          labelValue = "";
        }
      } else if (_selectedIndices.Count == 1) {
        labelValue = _names[_selectedIndices[0]];
      } else {
        Debug.Assert(_isFlags);
        if (_selectedIndices.Count == _values.Length) {
          labelValue = "Everything";
        } else {
          var names = _names;
          labelValue = string.Join(", ", _selectedIndices.Select(x => names[x]));
        }
      }
      
      if (EditorGUI.DropdownButton(position, new GUIContent(labelValue), FocusType.Keyboard)) {
        var values  = _values;
        var indices = _selectedIndices;
        
        if (_isFlags) {
          var       allOptions = new[] { "Nothing", "Everything" }.Concat(_names).ToArray();
          List<int> allIndices = new List<int>();
          if (_selectedIndices.Count == 0) {
            allIndices.Add(0); // nothing
          }
          else if (_selectedIndices.Count == _values.Length) {
            allIndices.Add(1); // everything
          }
          allIndices.AddRange(_selectedIndices.Select(x => x + 2));
          
          UnityInternal.EditorUtility.DisplayCustomMenu(position, allOptions, allIndices.ToArray(), (userData, options, selected) => {
            if (selected == 0) {
              // Clicked None
              if (isEnum) {
                property.longValue = 0;
              }
              else {
                property.GetFixedBufferElementAtIndex(0).longValue = 0;
                property.GetFixedBufferElementAtIndex(1).longValue = 0;
                property.GetFixedBufferElementAtIndex(2).longValue = 0;
                property.GetFixedBufferElementAtIndex(3).longValue = 0;
              }
            } else if (selected == 1) {
              // Selected Everything
              if (isEnum) {
                property.longValue = 0;
              } else {
                property.GetFixedBufferElementAtIndex(0).longValue = 0;
                property.GetFixedBufferElementAtIndex(1).longValue = 0;
                property.GetFixedBufferElementAtIndex(2).longValue = 0;
                property.GetFixedBufferElementAtIndex(3).longValue = 0;
              }
              foreach (var value in values) {
                if (isEnum) {
                  property.longValue |= value[0];
                } else{
                  property.GetFixedBufferElementAtIndex(0).longValue |= value[0];
                  property.GetFixedBufferElementAtIndex(1).longValue |= value[1];
                  property.GetFixedBufferElementAtIndex(2).longValue |= value[2];
                  property.GetFixedBufferElementAtIndex(3).longValue |= value[3];
                }
              }
            } else {
              // Toggled a value
              selected -= 2;
              if (indices.Contains(selected)) {
                if (isEnum) {
                  property.longValue &= ~values[selected][0];
                } else {
                  property.GetFixedBufferElementAtIndex(0).longValue &= ~values[selected][0];
                  property.GetFixedBufferElementAtIndex(1).longValue &= ~values[selected][1];
                  property.GetFixedBufferElementAtIndex(2).longValue &= ~values[selected][2];
                  property.GetFixedBufferElementAtIndex(3).longValue &= ~values[selected][3];
                }
              } else {
                if (isEnum) {
                  property.longValue |= (long)values[selected][0];
                } else {
                  property.GetFixedBufferElementAtIndex(0).longValue |= (long)values[selected][0];
                  property.GetFixedBufferElementAtIndex(1).longValue |= (long)values[selected][1];
                  property.GetFixedBufferElementAtIndex(2).longValue |= (long)values[selected][2];
                  property.GetFixedBufferElementAtIndex(3).longValue |= (long)values[selected][3];
                }
              }
            }

            property.serializedObject.ApplyModifiedProperties();
          }, null);
        } else {
          // non-flags enum
          UnityInternal.EditorUtility.DisplayCustomMenu(position, _names, _selectedIndices.ToArray(), (userData, options, selected) => {
            if (!indices.Contains(selected)) {
              property.longValue = values[selected][0];
              property.serializedObject.ApplyModifiedProperties();
            }
          }, null);
        }
      }
    }
  }
}

#endregion


#region HashCodeUtilities.cs

namespace Fusion.Editor {
  internal static class HashCodeUtilities {
    public const int InitialHash = (5381 << 16) + 5381;


    /// <summary>
    ///   This may only be deterministic on 64 bit systems.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="initialHash"></param>
    /// <returns></returns>
    public static int GetHashDeterministic(this string str, int initialHash = InitialHash) {
      unchecked {
        var hash1 = initialHash;
        var hash2 = initialHash;

        for (var i = 0; i < str.Length; i += 2) {
          hash1 = ((hash1 << 5) + hash1) ^ str[i];
          if (i == str.Length - 1) {
            break;
          }

          hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
        }

        return hash1 + hash2 * 1566083941;
      }
    }

    public static int CombineHashCodes(int a, int b) {
      return ((a << 5) + a) ^ b;
    }

    public static int CombineHashCodes(int a, int b, int c) {
      var t = ((a << 5) + a) ^ b;
      return ((t << 5) + t) ^ c;
    }

    public static unsafe int GetArrayHashCode<T>(T* ptr, int length, int initialHash = InitialHash) where T : unmanaged {
      var hash = initialHash;
      for (var i = 0; i < length; ++i) {
        hash = hash * 31 + ptr[i].GetHashCode();
      }

      return hash;
    }

    public static int GetHashCodeDeterministic(byte[] data, int initialHash = 0) {
      var hash = initialHash;
      for (var i = 0; i < data.Length; ++i) {
        hash = hash * 31 + data[i];
      }

      return hash;
    }

    public static int GetHashCodeDeterministic(string data, int initialHash = 0) {
      var hash = initialHash;
      for (var i = 0; i < data.Length; ++i) {
        hash = hash * 31 + data[i];
      }

      return hash;
    }


    public static unsafe int GetHashCodeDeterministic<T>(T data, int initialHash = 0) where T : unmanaged {
      return GetHashCodeDeterministic(&data, initialHash);
    }

    public static unsafe int GetHashCodeDeterministic<T>(T* data, int initialHash = 0) where T : unmanaged {
      var hash = initialHash;
      var ptr  = (byte*)data;
      for (var i = 0; i < sizeof(T); ++i) {
        hash = hash * 31 + ptr[i];
      }

      return hash;
    }
  }
}

#endregion


#region LazyAsset.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using Object = UnityEngine.Object;

  public class LazyAsset<T> {
    private T _value;
    private Func<T> _factory;

    public LazyAsset(Func<T> factory) {
      _factory = factory;
    }
    
    public T Value {
      get {
        if (NeedsUpdate) {
          lock (_factory) {
            if (NeedsUpdate) {
              _value = _factory();
            }
          }
        }
        return _value;
      }
    }
    
    public static implicit operator T(LazyAsset<T> lazyAsset) {
      return lazyAsset.Value;
    }

    public bool NeedsUpdate {
      get {
        if (_value is UnityEngine.Object obj) {
          return !obj;
        } else {
          return _value == null;
        }
      }
    }
  }

  public class LazyGUIStyle {
    private Func<List<Object>, GUIStyle> _factory;
    private GUIStyle                     _value;
    private List<Object>                 _dependencies = new List<Object>();
    
    public LazyGUIStyle(Func<List<Object>, GUIStyle> factory) {
      _factory = factory;
    }
    
    public static LazyGUIStyle Create(Func<List<Object>, GUIStyle> factory) {
      return new LazyGUIStyle(factory);
    }
    
    public static implicit operator GUIStyle(LazyGUIStyle lazyAsset) {
      return lazyAsset.Value;
    }
    
    public GUIStyle Value {
      get {
        if (NeedsUpdate) {
          lock (_factory) {
            if (NeedsUpdate) {
              _dependencies.Clear();
              _value = _factory(_dependencies);
            }
          }
        }
        return _value;
      }
    }
    
    public bool NeedsUpdate {
      get {
        if (_value == null) {
          return true;
        }
        foreach (var dependency in _dependencies) {
          if (!dependency) {
            return true;
          }
        }

        return false;
      }
    }
    
    public Vector2 CalcSize(GUIContent content)                                                                         => Value.CalcSize(content);
    public void    Draw(Rect position, GUIContent content, bool isHover, bool isActive, bool on, bool hasKeyboardFocus) => Value.Draw(position, content, isHover, isActive, on, hasKeyboardFocus);
    public void    Draw(Rect position, bool isHover, bool isActive, bool on, bool hasKeyboardFocus)                     => Value.Draw(position, isHover, isActive, on, hasKeyboardFocus);
  }
  
  public class LazyGUIContent {
    private Func<List<Object>, GUIContent> _factory;
    private GUIContent                     _value;
    private List<Object>                   _dependencies = new List<Object>();
    
    public LazyGUIContent(Func<List<Object>, GUIContent> factory) {
      _factory = factory;
    }
    
    public static LazyGUIContent Create(Func<List<Object>, GUIContent> factory) {
      return new LazyGUIContent(factory);
    }
    
    public static implicit operator GUIContent(LazyGUIContent lazyAsset) {
      return lazyAsset.Value;
    }
    
    public GUIContent Value {
      get {
        if (NeedsUpdate) {
          lock (_factory) {
            if (NeedsUpdate) {
              _dependencies.Clear();
              _value = _factory(_dependencies);
            }
          }
        }
        return _value;
      }
    }
    
    public bool NeedsUpdate {
      get {
        if (_value == null) {
          return true;
        }
        foreach (var dependency in _dependencies) {
          if (!dependency) {
            return true;
          }
        }

        return false;
      }
    }
  }
  
  public static class LazyAsset {
    public static LazyAsset<T> Create<T>(Func<T> factory) {
      return new LazyAsset<T>(factory);
    }
  }
}

#endregion


#region PathUtils.cs

namespace Fusion.Editor {
  using System;

  // TODO: this should be moved to the runtime part
  static partial class PathUtils {
    
    public static bool TryMakeRelativeToFolder(string path, string folderWithSlashes, out string result) {
      var index = path.IndexOf(folderWithSlashes, StringComparison.Ordinal);

      if (index < 0) {
        result = string.Empty;
        return false;
      }
      
      if (folderWithSlashes[0] != '/' && index > 0) {
        result = string.Empty;
        return false;
      }

      result = path.Substring(index + folderWithSlashes.Length);
      return true;
    }
    
    [Obsolete("Use " + nameof(TryMakeRelativeToFolder) + " instead")]
    public static bool MakeRelativeToFolder(string path, string folder, out string result) {
      result = string.Empty;
      var formattedPath = Normalize(path);
      if (formattedPath.Equals(folder, StringComparison.Ordinal) ||
          formattedPath.EndsWith("/" + folder)) {
        return true;
      }
      var index = formattedPath.IndexOf(folder + "/", StringComparison.Ordinal);
      var size  = folder.Length + 1;
      if (index >= 0 && formattedPath.Length >= size) {
        result = formattedPath.Substring(index + size, formattedPath.Length - index - size);
        return true;
      }
      return false;
    }

    [Obsolete("Use Normalize instead")]
    public static string MakeSane(string path) {
      return Normalize(path);
    }

    public static string Normalize(string path) {
      return path.Replace("\\", "/").Replace("//", "/").TrimEnd('\\', '/').TrimStart('\\', '/');
    }

    public static string GetPathWithoutExtension(string path) {
      if (path == null)
        return null;
      int length;
      if ((length = path.LastIndexOf('.')) == -1)
        return path;
      return path.Substring(0, length);
    }

  }
}

#endregion


#region FusionCodeDoc.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Text.RegularExpressions;
  using System.Xml;
  using UnityEditor;
  using UnityEngine;

  public static class FusionCodeDoc {
    public const string Label            = "FusionCodeDoc";
    public const string Extension        = "xml";
    public const string ExtensionWithDot = "." + Extension;

    private static Dictionary<string, CodeDoc> _parsedCodeDocs = new();
    
    private static Dictionary<(string assemblyName, string memberKey), (GUIContent withoutType, GUIContent withType)> _guiContentCache = new();
    
    private static string CrefColor => EditorGUIUtility.isProSkin ? "#FFEECC" : "#664400";

    public static GUIContent FindEntry(MemberInfo member, bool addTypeInfo = true) {
      switch (member) {
        case FieldInfo field:
          return FindEntry(field, addTypeInfo);
        case PropertyInfo property:
          return FindEntry(property);
        case MethodInfo method:
          return FindEntry(method);
        case Type type:
          return FindEntry(type);
        default:
          throw new ArgumentOutOfRangeException(nameof(member));
      }
    }
    
    public static GUIContent FindEntry(FieldInfo field, bool addTypeInfo = true) {
      if (field == null) {
        throw new ArgumentNullException(nameof(field));
      }
      return FindEntry(field, $"F:{SanitizeTypeName(field.DeclaringType)}.{field.Name}", addTypeInfo);
    }

    public static GUIContent FindEntry(PropertyInfo property) {
      if (property == null) {
        throw new ArgumentNullException(nameof(property));
      }
      return FindEntry(property, $"P:{SanitizeTypeName(property.DeclaringType)}.{property.Name}");
    }

    public static GUIContent FindEntry(MethodInfo method) {
      if (method == null) {
        throw new ArgumentNullException(nameof(method));
      }
      return FindEntry(method, $"M:{SanitizeTypeName(method.DeclaringType)}.{method.Name}");
    }

    public static GUIContent FindEntry(Type type) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      return FindEntry(type, $"T:{SanitizeTypeName(type)}");
    }

    private static GUIContent FindEntry(MemberInfo member, string key, bool addTypeInfo = true) {

      Assembly assembly;
      if (member is Type type) {
        assembly = type.Assembly;
      } else {
        assembly = member.DeclaringType.Assembly;
      }

      var assemblyName = assembly.GetName().Name;

      if (_guiContentCache.TryGetValue((assemblyName, key), out var content)) {
        return addTypeInfo ? content.withType : content.withoutType;
      }

      foreach (var path in AssetDatabase.FindAssets($"l:{Label} t:TextAsset")
                .Select(x => AssetDatabase.GUIDToAssetPath(x))
                .Where(x => Path.GetFileNameWithoutExtension(x).Contains(assemblyName, StringComparison.OrdinalIgnoreCase))) {

        // has this path been parsed already?
        if (!_parsedCodeDocs.TryGetValue(path, out var parsedCodeDoc)) {
          _parsedCodeDocs.Add(path, null);

          if (!string.IsNullOrEmpty(path)) {
            FusionEditorLog.Trace($"Trying to parse {path} for {key}");
            if (TryParseCodeDoc(path, out parsedCodeDoc)) {
              _parsedCodeDocs[path] = parsedCodeDoc;
            }
          } else {
            FusionEditorLog.Trace($"Code doc for {assemblyName} not found.");
          }
        }

        
        if (parsedCodeDoc?.AssemblyName == assemblyName) {
          // code doc entries are sorted, so binary search is possible
          if (TryFindEntry(parsedCodeDoc, key, out var entry)) {
            content.withoutType = new GUIContent(entry.Summary, entry.Tooltip);
            content.withType    = content.withoutType;
            _guiContentCache.Add((assemblyName, key), content);

            Type returnType = null;

            if (member is FieldInfo field) {
              returnType = field.FieldType;
            } else if (member is PropertyInfo property) {
              returnType = property.PropertyType;
            }

            if (returnType != null) {
              var  typeEntry  = FindEntry(returnType);
              string typeSummary = "";
              
              if (typeEntry != null) {
                typeSummary += $"\n\n<color={CrefColor}>[{returnType.Name}]</color> {typeEntry}";
              }
              if (returnType.IsEnum) {
                // find all the enum values
                foreach (var enumValue in returnType.GetFields(BindingFlags.Static | BindingFlags.Public)) {
                  var enumValueEntry = FindEntry(enumValue, addTypeInfo: false);
                  if (enumValueEntry != null) {
                    typeSummary += $"\n\n<color={CrefColor}>[{returnType.Name}.{enumValue.Name}]</color> {enumValueEntry}";
                  }
                }
              }

              if (typeSummary.Length > 0) {
                content.withType = new GUIContent((entry.Summary + typeSummary).Trim('\n'), entry.Tooltip);
                _guiContentCache[(assemblyName, key)] = content;
              }
            }
            
            return addTypeInfo ? content.withType : content.withoutType;;
          }
        }
      }

      _guiContentCache.Add((assemblyName, key), default);
      return addTypeInfo ? content.withType : content.withoutType;
      
      bool TryFindEntry(CodeDoc codeDoc, string key, out Entry entry) {
        var searchEntry = EntryKeyComparer.DummyEntry;
        searchEntry.Key = key;
        var index = Array.BinarySearch(codeDoc.Entries, searchEntry, EntryKeyComparer.Instance);
        if (index < 0) {
          entry = null;
          return false;
        }
        entry = codeDoc.Entries[index];
        return true;
      }
    }

    private static string SanitizeTypeName(Type type) {
      return type.FullName.Replace('+', '.');
    }

    public static void InvalidateCache() {
      _parsedCodeDocs.Clear();
      _guiContentCache.Clear();
    }

    private static bool TryParseCodeDoc(string path, out CodeDoc result) {
      var xmlDoc = new XmlDocument();

      try {
        xmlDoc.Load(path);
      } catch (Exception e) {
        FusionEditorLog.Error($"Failed to load {path}: {e}");
        result = null;
        return false;
      }

      var assemblyName = xmlDoc.DocumentElement.SelectSingleNode("assembly")
       ?.SelectSingleNode("name")
       ?.FirstChild
       ?.Value;

      if (assemblyName == null) {
        result = null;
        return false;
      }

      var members = xmlDoc.DocumentElement.SelectSingleNode("members")
       .SelectNodes("member");
      
      var buffer = new List<Entry>();

      foreach (XmlNode node in members) {
        var key     = node.Attributes["name"].Value;
        var summary = node.SelectSingleNode("summary")?.InnerXml.Trim();
        if (summary == null) {
          continue;
        }

        // remove generic indicator
        summary = summary.Replace("`1", "");

        // fork tooltip and help summaries
        var help    = Reformat(summary, false);
        var tooltip = Reformat(summary, true);

        buffer.Add(new FusionCodeDoc.Entry() {
          Key     = key,
          Summary = help,
          Tooltip = tooltip
        });
      }

      result = new CodeDoc() {
        AssemblyName = assemblyName,
        Entries      = buffer.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray()
      };
      return true;
    }

    private static string Reformat(string summary, bool forTooltip) {
      // Tooltips don't support formatting tags. Inline help does.
      if (forTooltip) {
        summary = Regexes.SeeWithCref.Replace(summary, "$1");
        summary = Regexes.See.Replace(summary, "$1");
        summary = Regexes.XmlEmphasizeBrackets.Replace(summary, "$1");
      } else {
        var colorstring = $"<color={CrefColor}>$1</color>";
        summary = Regexes.SeeWithCref.Replace(summary, colorstring);
        summary = Regexes.See.Replace(summary, colorstring);
      }

      summary = Regexes.XmlCodeBracket.Replace(summary, "$1");

      // Reduce all sequential whitespace characters into a single space.
      summary = Regexes.WhitespaceString.Replace(summary, " ");

      // Turn <para> and <br> into line breaks
      summary = Regex.Replace(summary, @"</para>\s?<para>", "\n\n"); // prevent back to back paras from producing 4 line returns.
      summary = Regex.Replace(summary, @"</?para>\s?", "\n\n");
      summary = Regex.Replace(summary, @"</?br\s?/?>\s?", "\n\n");
      
      // unescape <>
      summary = summary.Replace("&lt;", "<");
      summary = summary.Replace("&gt;", ">");
      summary = summary.Replace("&amp;", "&");

      summary = summary.Trim();

      return summary;
    }

    private class Entry {
      public string Key;
      public string Summary;
      public string Tooltip;
    }

    private class CodeDoc {
      public string  AssemblyName;
      public Entry[] Entries;
    }

    private class Postprocessor : AssetPostprocessor {
      private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        foreach (var path in importedAssets) {
          if (!path.StartsWith("Assets/") || !path.EndsWith(ExtensionWithDot)) {
            continue;
          } 
          
          if (AssetDatabaseUtils.HasLabel(path, Label)) {
            FusionEditorLog.Trace($"Code doc {path} was imported, refreshing");
            InvalidateCache();
          }

          // is there a dll with the same name?
          if (File.Exists(path.Substring(0, path.Length - ExtensionWithDot.Length) + ".dll")) {
            FusionEditorLog.Trace($"Detected a dll next to {path}, applying label and refreshing.");
            AssetDatabaseUtils.SetLabel(path, Label, true);
            InvalidateCache();
          }
        }
      }
    }

    private class EntryKeyComparer : IComparer<Entry> {
      public static readonly EntryKeyComparer Instance   = new EntryKeyComparer();
      public static readonly Entry            DummyEntry = new Entry();

      public int Compare(Entry x, Entry y) {
        return string.Compare(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
      }
    }

    private static class Regexes {
      public static readonly Regex SeeWithCref          = new(@"<see\w* cref=""(?:\w: ?)?([\w\.\d]*)(?:\(.*\))?"" ?\/>", RegexOptions.None);
      public static readonly Regex See                  = new(@"<see\w* .*>([\w\.\d]*)<\/see\w*>", RegexOptions.None);
      public static readonly Regex WhitespaceString     = new(@"\s+");
      public static readonly Regex XmlCodeBracket       = new(@"<code>([\s\S]*?)</code>");
      public static readonly Regex XmlEmphasizeBrackets = new(@"<\w>([\s\S]*?)</\w>");
    }
  }
}

#endregion


#region FusionEditor.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public abstract class FusionEditor :
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    Sirenix.OdinInspector.Editor.OdinEditor
#else
    UnityEditor.Editor
#endif
  {
    private EditorButtonDrawer _buttonDrawer;
    
    protected void PrepareOnInspectorGUI() {
      FusionEditorGUI.InjectScriptHeaderDrawer(this);
    }

    protected void DrawEditorButtons() {
      _buttonDrawer.Draw(this);
    }
    
    public override void OnInspectorGUI() {
      PrepareOnInspectorGUI();
      base.OnInspectorGUI();
      DrawEditorButtons();
    }

    protected void DrawScriptPropertyField() {
      FusionEditorGUI.ScriptPropertyField(this);
    }
    
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    public new bool DrawDefaultInspector() {
      EditorGUI.BeginChangeCheck();
      base.DrawDefaultInspector();
      return EditorGUI.EndChangeCheck();
    } 
#else
    protected virtual void OnEnable() {
    }
#endif
  }
}


#endregion


#region FusionEditorGUI.InlineHelp.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {
    private const float SCROLL_WIDTH     = 16f;
    private const float LEFT_HELP_INDENT = 8f;
    
    private static (object, string) s_expandedHelp;
    
    internal static Rect GetInlineHelpButtonRect(Rect position, bool expectFoldout = true, bool forScriptHeader = false) {
      var style = FusionEditorSkin.HelpButtonStyle;

      float width = style.fixedWidth <= 0 ? 16.0f : style.fixedWidth;
      float height = style.fixedHeight <= 0 ? 16.0f : style.fixedHeight;

      // this 2 lower than line height, but makes it look better
      const float FirstLineHeight = 16;
      
      int offsetY    = forScriptHeader ? -1 : 1;
      
      var buttonRect = new Rect(position.x - width, position.y + (FirstLineHeight - height) / 2 + + offsetY, width, height);
      using (new EditorGUI.IndentLevelScope(expectFoldout ? -1 : 0)) {
        buttonRect.x = EditorGUI.IndentedRect(buttonRect).x;
        // give indented items a little extra padding - no need for them to be so crammed
        if (buttonRect.x > 8) {
          buttonRect.x -= 2;
        }
      }

      return buttonRect;
    }

    
    internal static bool DrawInlineHelpButton(Rect buttonRect, bool state, bool doButton = true, bool doIcon = true) {

      var style = FusionEditorSkin.HelpButtonStyle;
      
      var result = false;
      if (doButton) {
        EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
        using (new EnabledScope(true)) {
          result = GUI.Button(buttonRect, state ? InlineHelpStyle.HideInlineContent : InlineHelpStyle.ShowInlineContent, GUIStyle.none);
        }
      }

      if (doIcon) {
        // paint over what the inspector has drawn
        if (Event.current.type == EventType.Repaint) {
          style.Draw(buttonRect, false, false, state, false);
        }
      }

      return result;
    }

    internal static Vector2 GetInlineBoxSize(GUIContent content) {

      // const int InlineBoxExtraHeight = 4;
      
      var outerStyle = FusionEditorSkin.InlineBoxFullWidthStyle;
      var innerStyle = FusionEditorSkin.RichLabelStyle;
      
      var outerMargin  = outerStyle.margin;
      var outerPadding = outerStyle.padding;

      var width = UnityInternal.EditorGUIUtility.contextWidth - outerMargin.left - outerMargin.right;
      
      // well... we do this, because there's no way of knowing the indent and scroll bar existence
      // when property height is calculated
      width -= 25.0f;

      if (content == null || width <= 0) {
        return default;
      }
      
      width -= outerPadding.left + outerPadding.right;
      
      var height = innerStyle.CalcHeight(content, width);
      
      // assume min height
      height = Mathf.Max(height, EditorGUIUtility.singleLineHeight);
      
      // add back all the padding
      height += outerPadding.top + outerPadding.bottom;
      height += outerMargin.top + outerMargin.bottom;
      
      return new Vector2(width, Mathf.Max(0, height));
    }

    internal static Rect DrawInlineBoxUnderProperty(GUIContent content, Rect propertyRect, Color color, bool drawSelector = false, bool hasFoldout = false) {
      using (new EnabledScope(true)) {

        var boxSize = GetInlineBoxSize(content);
        
        if (Event.current.type == EventType.Repaint && boxSize.y > 0) {
          var boxMargin = FusionEditorSkin.InlineBoxFullWidthStyle.margin;
          
          var boxRect = new Rect() {
            x      = boxMargin.left,
            y      = propertyRect.yMax - boxSize.y,
            width  = UnityInternal.EditorGUIUtility.contextWidth - boxMargin.horizontal,
            height = boxSize.y,
          };

          using (new BackgroundColorScope(color)) {
            FusionEditorSkin.InlineBoxFullWidthStyle.Draw(boxRect, false, false, false, false);

            var labelRect = boxRect;
            labelRect = FusionEditorSkin.InlineBoxFullWidthStyle.padding.Remove(labelRect);
            FusionEditorSkin.RichLabelStyle.Draw(labelRect, content, false, false, false, false);
            
            if (drawSelector) {
              var selectorMargin = FusionEditorSkin.InlineSelectorStyle.margin;

              var selectorRect = new Rect() {
                x      = selectorMargin.left,
                y      = propertyRect.y - selectorMargin.top,
                width  = propertyRect.x - selectorMargin.horizontal,
                height = propertyRect.height - boxSize.y - selectorMargin.bottom,
              };

              if (hasFoldout) {
                selectorRect.width -= 20.0f;
              }

              FusionEditorSkin.InlineSelectorStyle.Draw(selectorRect, false, false, false, false);
            }
          }
        }

        propertyRect.height -= boxSize.y;
        return propertyRect;
      }
    }


    internal static void DrawScriptHeaderBackground(Rect position, Color color) {
      if (Event.current.type != EventType.Repaint) {
        return;
      }
      
      var style     = FusionEditorSkin.ScriptHeaderBackgroundStyle;
      var boxMargin = style.margin;

      var boxRect = new Rect() {
        x      = boxMargin.left,
        y      = position.y - boxMargin.top,
        width  = UnityInternal.EditorGUIUtility.contextWidth - boxMargin.horizontal,
        height = position.height + boxMargin.bottom,
      };

      using (new BackgroundColorScope(color)) {
        style.Draw(boxRect, false, false, false, false);
      }
    }

    public static void DrawScriptHeaderIcon(Rect position) {
      if (Event.current.type != EventType.Repaint) {
        return;
      }

      var style     = FusionEditorSkin.ScriptHeaderIconStyle;
      var boxMargin = style.margin;
      var boxRect   = boxMargin.Remove(position);

      style.Draw(boxRect, false, false, false, false);
    }

    public static   bool InjectScriptHeaderDrawer(Editor editor)                               => InjectScriptHeaderDrawer(editor, out _);
    internal static bool InjectScriptHeaderDrawer(Editor editor, out ScriptFieldDrawer drawer) => InjectScriptHeaderDrawer(editor.serializedObject, out drawer);
    public static   bool InjectScriptHeaderDrawer(SerializedObject serializedObject)           => InjectScriptHeaderDrawer(serializedObject, out _);
    
    internal static bool InjectScriptHeaderDrawer(SerializedObject serializedObject, out ScriptFieldDrawer drawer) {
      var sp       = serializedObject.FindPropertyOrThrow(ScriptPropertyName);
      var rootType = serializedObject.targetObject.GetType();
      
      var injected = TryInjectDrawer(sp, null, () => null, () => new ScriptFieldDrawer(), out drawer);
      if (drawer.attribute == null) {
        UnityInternal.PropertyDrawer.SetAttribute(drawer, rootType.GetCustomAttributes<ScriptHelpAttribute>(true).SingleOrDefault() ?? new ScriptHelpAttribute());
      }

      return injected;
    }
    
    public static void SetScriptFieldHidden(Editor editor, bool hidden) {
      var sp = editor.serializedObject.FindPropertyOrThrow(ScriptPropertyName);
      TryInjectDrawer(sp, null, () => null, () => new ScriptFieldDrawer(), out var drawer);
      drawer.ForceHide = hidden;
    }

    public static Rect LayoutHelpPrefix(Editor editor, SerializedProperty property) {
      var fieldInfo = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out _);
      if (fieldInfo == null) {
        return EditorGUILayout.GetControlRect(true);
      }
      
      var help = FusionCodeDoc.FindEntry(fieldInfo);
      return LayoutHelpPrefix(editor, property.propertyPath, help);
    }
    
    public static Rect LayoutHelpPrefix(Editor editor, MemberInfo memberInfo) {
      var help = FusionCodeDoc.FindEntry(memberInfo);
      return LayoutHelpPrefix(editor, memberInfo.Name, help);
    }
    
    public static Rect LayoutHelpPrefix(Editor editor, string path, GUIContent help) {
      var rect = EditorGUILayout.GetControlRect(true);
      
      if (help == null) {
        return rect;
      }
      
      var buttonRect  = GetInlineHelpButtonRect(rect, false);
      var wasExpanded = IsHelpExpanded(editor, path);

      if (wasExpanded) {
        var helpSize = GetInlineBoxSize(help);
        var r        = EditorGUILayout.GetControlRect(false, helpSize.y);
        r.y      =  rect.y;
        r.height += rect.height;
        DrawInlineBoxUnderProperty(help, r, FusionEditorSkin.HelpInlineBoxColor, true);
      }
      
      if (DrawInlineHelpButton(buttonRect, wasExpanded, doButton: true, doIcon: true)) {
        SetHelpExpanded(editor, path, !wasExpanded);
      }
      
      return rect;
    }

    private static void AddDrawer(SerializedProperty property, PropertyDrawer drawer) {
      var handler = UnityInternal.ScriptAttributeUtility.GetHandler(property);
      
      if (handler.m_PropertyDrawers == null) {
        handler.m_PropertyDrawers = new List<PropertyDrawer>();
      }

      InsertPropertyDrawerByAttributeOrder(handler.m_PropertyDrawers, drawer);
    }

    private static bool TryInjectDrawer<DrawerType>(SerializedProperty property, FieldInfo field, Func<PropertyAttribute> attributeFactory, Func<DrawerType> drawerFactory, out DrawerType drawer)
      where DrawerType : PropertyDrawer {

      var handler = UnityInternal.ScriptAttributeUtility.GetHandler(property);
      
      drawer = GetPropertyDrawer<DrawerType>(handler.m_PropertyDrawers);
      if (drawer != null) {
        return false;
      }

      if (handler.Equals(UnityInternal.ScriptAttributeUtility.sharedNullHandler)) {
        // need to add one?
        handler = UnityInternal.PropertyHandler.New();
        UnityInternal.ScriptAttributeUtility.propertyHandlerCache.SetHandler(property, handler);
      }

      var attribute = attributeFactory();

      drawer = drawerFactory();
      FusionEditorLog.Assert(drawer != null, "drawer != null");
      UnityInternal.PropertyDrawer.SetAttribute(drawer, attribute);
      UnityInternal.PropertyDrawer.SetFieldInfo(drawer, field);

      AddDrawer(property, drawer);

      return true;
    }

    internal static bool IsHelpExpanded(object id, string path) {
      return s_expandedHelp == (id, path);
    }

    internal static void SetHelpExpanded(object id, string path, bool value) {
      if (value) {
        s_expandedHelp = (id, path);
      } else {
        s_expandedHelp = default;
      }
    }
    
    private static bool HasPropertyDrawer<T>(IEnumerable<PropertyDrawer> orderedDrawers) where T : PropertyDrawer {
      return orderedDrawers?.Any(x => x is T) ?? false;
    }
    
    private static T GetPropertyDrawer<T>(IEnumerable<PropertyDrawer> orderedDrawers) where T : PropertyDrawer {
      return orderedDrawers?.OfType<T>().FirstOrDefault();
    }

    internal static int InsertPropertyDrawerByAttributeOrder<T>(List<T> orderedDrawers, T drawer) where T : PropertyDrawer {
      if (orderedDrawers == null) {
        throw new ArgumentNullException(nameof(orderedDrawers));
      }

      if (drawer == null) {
        throw new ArgumentNullException(nameof(drawer));
      }

      var index = orderedDrawers.BinarySearch(drawer, PropertyDrawerOrderComparer.Instance);
      if (index < 0) {
        index = ~index;
      }

      orderedDrawers.Insert(index, drawer);
      return index;
    }

    internal static class InlineHelpStyle {
      public const  float      MarginOuter       = 16.0f;
      public static GUIContent HideInlineContent = new("", "Hide");
      public static GUIContent ShowInlineContent = new("", "");
    }

    internal static class LazyAuto {
      public static LazyAuto<T> Create<T>(Func<T> valueFactory) {
        return new LazyAuto<T>(valueFactory);
      }
    }

    internal class LazyAuto<T> : Lazy<T> {
      public LazyAuto(Func<T> valueFactory) : base(valueFactory) {
      }

      public static implicit operator T(LazyAuto<T> lazy) {
        return lazy.Value;
      }
    }
    

    private class PropertyDrawerOrderComparer : IComparer<PropertyDrawer> {
      public static readonly PropertyDrawerOrderComparer Instance = new();

      public int Compare(PropertyDrawer x, PropertyDrawer y) {
        var ox = x.attribute?.order ?? int.MaxValue;
        var oy = y.attribute?.order ?? int.MaxValue;
        return ox - oy;
      }
    }
  }
}

#endregion


#region FusionEditorGUI.Odin.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
  using System.Collections.Generic;
  using Sirenix.Utilities.Editor;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Utilities;
#endif

  public static partial class FusionEditorGUI {
    public static T IfOdin<T>(T ifOdin, T ifNotOdin) {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
      return ifOdin;
#else
      return ifNotOdin;
#endif
    }

    public static UnityEngine.Object ForwardObjectField(Rect position, UnityEngine.Object value, Type objectType, bool allowSceneObjects) {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
      return SirenixEditorFields.UnityObjectField(position, value, objectType, allowSceneObjects);
#else
      return EditorGUI.ObjectField(position, value, objectType, allowSceneObjects);
#endif
    }
    
    public static UnityEngine.Object ForwardObjectField(Rect position, GUIContent label, UnityEngine.Object value, Type objectType, bool allowSceneObjects) {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
      return SirenixEditorFields.UnityObjectField(position, label, value, objectType, allowSceneObjects);
#else
      return EditorGUI.ObjectField(position, label, value, objectType, allowSceneObjects);
#endif
    }

    
    public static bool ForwardPropertyField(Rect position, SerializedProperty property, GUIContent label, bool includeChildren, bool lastDrawer = true) {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
      if (lastDrawer) {
        switch (property.propertyType) {
          case SerializedPropertyType.ObjectReference: {
              EditorGUI.BeginChangeCheck();
              UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out var fieldType);
              var value = SirenixEditorFields.UnityObjectField(position, label, property.objectReferenceValue, fieldType ?? typeof(UnityEngine.Object), true);
              if (EditorGUI.EndChangeCheck()) {
                property.objectReferenceValue = value;
              }
              return false;
            }

          case SerializedPropertyType.Integer: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.IntField(position, label, property.intValue);
              if (EditorGUI.EndChangeCheck()) {
                property.intValue = value;
              }
              return false;
            }

          case SerializedPropertyType.Float: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.FloatField(position, label, property.floatValue);
              if (EditorGUI.EndChangeCheck()) {
                property.floatValue = value;
              }
              return false;
            }

          case SerializedPropertyType.Color: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.ColorField(position, label, property.colorValue);
              if (EditorGUI.EndChangeCheck()) {
                property.colorValue = value;
              }
              return false;
            }

          case SerializedPropertyType.Vector2: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.Vector2Field(position, label, property.vector2Value);
              if (EditorGUI.EndChangeCheck()) {
                property.vector2Value = value;
              }
              return false;
            }

          case SerializedPropertyType.Vector3: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.Vector3Field(position, label, property.vector3Value);
              if (EditorGUI.EndChangeCheck()) {
                property.vector3Value = value;
              }
              return false;
            }

          case SerializedPropertyType.Vector4: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.Vector4Field(position, label, property.vector4Value);
              if (EditorGUI.EndChangeCheck()) {
                property.vector4Value = value;
              }
              return false;
            }

          case SerializedPropertyType.Quaternion: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.RotationField(position, label, property.quaternionValue, GlobalConfig<GeneralDrawerConfig>.Instance.QuaternionDrawMode);
              if (EditorGUI.EndChangeCheck()) {
                property.quaternionValue = value;
              }
              return false;
            }

          case SerializedPropertyType.String: {
              EditorGUI.BeginChangeCheck();
              var value = SirenixEditorFields.TextField(position, label, property.stringValue);
              if (EditorGUI.EndChangeCheck()) {
                property.stringValue = value;
              }
              return false;
            }

          case SerializedPropertyType.Enum: {
              UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out var type);
              if (type != null && type.IsEnum) {
                EditorGUI.BeginChangeCheck();
                bool flags = type.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
                Enum value = SirenixEditorFields.EnumDropdown(position, label, (Enum)Enum.ToObject(type, property.intValue));
                if (EditorGUI.EndChangeCheck()) {
                  property.intValue = Convert.ToInt32(Convert.ChangeType(value, Enum.GetUnderlyingType(type)));
                }
                return false;
              }

              break;
            }

          default:
            break; 
        }
      }
#endif
      return EditorGUI.PropertyField(position, property, label, includeChildren);
    }
  }
}

#endregion


#region FusionEditorGUI.Scopes.cs

namespace Fusion.Editor {
  using System;
  using System.Runtime.CompilerServices;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {
 
    public sealed class CustomEditorScope : IDisposable {

      private SerializedObject serializedObject;
      public  bool             HadChanges { get; private set; }

      public CustomEditorScope(SerializedObject so) {
        serializedObject = so;
        EditorGUI.BeginChangeCheck();
        so.UpdateIfRequiredOrScript();
        ScriptPropertyField(so);
      }

      public void Dispose() {
        HadChanges = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();
      }
    }
    
    public struct EnabledScope: IDisposable {
      private readonly bool value;

      public EnabledScope(bool enabled) {
        value       = GUI.enabled;
        GUI.enabled = enabled;
      }

      public void Dispose() {
        GUI.enabled = value;
      }
    }

    public readonly struct BackgroundColorScope : IDisposable {
      private readonly Color value;

      public BackgroundColorScope(Color color) {
        value               = GUI.backgroundColor;
        GUI.backgroundColor = color;
      }

      public void Dispose() {
        GUI.backgroundColor = value;
      }
    }

    public struct ColorScope: IDisposable {
      private readonly Color value;

      public ColorScope(Color color) {
        value     = GUI.color;
        GUI.color = color;
      }

      public void Dispose() {
        GUI.color = value;
      }
    }

    public struct ContentColorScope: IDisposable {
      private readonly Color value;

      public ContentColorScope(Color color) {
        value            = GUI.contentColor;
        GUI.contentColor = color;
      }

      public void Dispose() {
        GUI.contentColor = value;
      }
    }

    public struct FieldWidthScope: IDisposable {
      private readonly float value;

      public FieldWidthScope(float fieldWidth) {
        value                       = EditorGUIUtility.fieldWidth;
        EditorGUIUtility.fieldWidth = fieldWidth;
      }

      public void Dispose() {
        EditorGUIUtility.fieldWidth = value;
      }
    }

    public struct HierarchyModeScope: IDisposable {
      private readonly bool value;

      public HierarchyModeScope(bool value) {
        this.value                     = EditorGUIUtility.hierarchyMode;
        EditorGUIUtility.hierarchyMode = value;
      }

      public void Dispose() {
        EditorGUIUtility.hierarchyMode = value;
      }
    }

    public struct IndentLevelScope: IDisposable {
      private readonly int value;

      public IndentLevelScope(int indentLevel) {
        value                 = EditorGUI.indentLevel;
        EditorGUI.indentLevel = indentLevel;
      }

      public void Dispose() {
        EditorGUI.indentLevel = value;
      }
    }

    public struct LabelWidthScope: IDisposable {
      private readonly float value;

      public LabelWidthScope(float labelWidth) {
        value                       = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = labelWidth;
      }

      public void Dispose() {
        EditorGUIUtility.labelWidth = value;
      }
    }

    public struct ShowMixedValueScope: IDisposable {
      private readonly bool value;

      public ShowMixedValueScope(bool show) {
        value                    = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = show;
      }

      public void Dispose() {
        EditorGUI.showMixedValue = value;
      }
    }

    public struct PropertyScope : IDisposable {
      public PropertyScope(Rect position, GUIContent label, SerializedProperty property) {
        EditorGUI.BeginProperty(position, label, property);
      }

      public void Dispose() {
        EditorGUI.EndProperty();
      }
    }

    public readonly struct PropertyScopeWithPrefixLabel : IDisposable {
      private readonly int indent;

      public PropertyScopeWithPrefixLabel(Rect position, GUIContent label, SerializedProperty property, out Rect indentedPosition) {
        EditorGUI.BeginProperty(position, label, property);
        indentedPosition      = EditorGUI.PrefixLabel(position, label);
        indent                = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
      }

      public void Dispose() {
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
      }
    }

    public readonly struct BoxScope: IDisposable {
      
      private readonly int _indent;
      
      /// <summary>
      ///if fields include inline help (?) buttons, use indent : 1 
      /// </summary>
      public BoxScope(string message, int indent = 0, float space = 0.0f) {
        EditorGUILayout.BeginVertical(FusionEditorSkin.OutlineBoxStyle);

        if (!string.IsNullOrEmpty(message)) {
          EditorGUILayout.LabelField(message, EditorStyles.boldLabel);
        }

        if (space > 0.0f) {
          GUILayout.Space(space);
        }

        _indent = EditorGUI.indentLevel;
        if (indent != 0) {
          EditorGUI.indentLevel += indent;
        }
      }
      
      public void Dispose() {
        EditorGUI.indentLevel = _indent;
        EditorGUILayout.EndVertical();
      }
    }
    public struct WarningScope: IDisposable {
      public WarningScope(string message, float space = 0.0f) {

        var backgroundColor = GUI.backgroundColor;
        
        GUI.backgroundColor = FusionEditorSkin.WarningInlineBoxColor;
        EditorGUILayout.BeginVertical(FusionEditorSkin.InlineBoxFullWidthScopeStyle);
        GUI.backgroundColor = backgroundColor;
        
        EditorGUILayout.LabelField(new GUIContent(message, FusionEditorSkin.WarningIcon), FusionEditorSkin.RichLabelStyle);
        if (space > 0.0f) {
          GUILayout.Space(space);
        }
      }
      
      public void Dispose() {
        EditorGUILayout.EndVertical();
      }
    }

    public struct ErrorScope : IDisposable {
      public ErrorScope(string message, float space = 0.0f) {
        var backgroundColor = GUI.backgroundColor;
        
        GUI.backgroundColor = FusionEditorSkin.ErrorInlineBoxColor;
        EditorGUILayout.BeginVertical(FusionEditorSkin.InlineBoxFullWidthScopeStyle);
        GUI.backgroundColor = backgroundColor;
        
        EditorGUILayout.LabelField(new GUIContent(message, FusionEditorSkin.ErrorIcon), FusionEditorSkin.RichLabelStyle);
        if (space > 0.0f) {
          GUILayout.Space(space);
        }
      }
      
      public void Dispose() {
        EditorGUILayout.EndVertical();
      }
    }

    public readonly struct GUIContentScope : IDisposable {

      private readonly string     _text;
      private readonly string     _tooltip;
      private readonly GUIContent _content;

      public GUIContentScope(GUIContent content) {
        _content = content;
        _text    = content?.text;
        _tooltip = content?.tooltip;
      }

      public void Dispose() {
        if (_content != null) {
          _content.text    = _text;
          _content.tooltip = _tooltip;
        }
      }
    }
  }
}

#endregion


#region FusionEditorGUI.Utils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {
    public const string ScriptPropertyName = "m_Script";

    private const int IconHeight = 14;

    public static readonly GUIContent WhitespaceContent = new(" ");

    public static Color PrefebOverridenColor => new(1f / 255f, 153f / 255f, 235f / 255f, 0.75f);

    public static float FoldoutWidth => 16.0f;

    public static Rect Decorate(Rect rect, string tooltip, MessageType messageType, bool hasLabel = false, bool drawBorder = true, bool drawButton = true) {
      if (hasLabel) {
        rect.xMin += EditorGUIUtility.labelWidth;
      }

      var content  = EditorGUIUtility.TrTextContentWithIcon(string.Empty, tooltip, messageType);
      var iconRect = rect;
      iconRect.width =  Mathf.Min(16, rect.width);
      iconRect.xMin  -= iconRect.width;

      iconRect.y      += (iconRect.height - IconHeight) / 2;
      iconRect.height =  IconHeight;

      if (drawButton) {
        using (new EnabledScope(true)) {
          GUI.Label(iconRect, content, GUIStyle.none);
        }
      }

      //GUI.Label(iconRect, content, new GUIStyle());

      if (drawBorder) {
        Color borderColor;
        switch (messageType) {
          case MessageType.Warning:
            borderColor = new Color(1.0f, 0.5f, 0.0f);
            break;
          case MessageType.Error:
            borderColor = new Color(1.0f, 0.0f, 0.0f);
            break;
          default:
            borderColor = new Color(1f, 1f, 1f, .0f);
            break;
        }

        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, borderColor, 1.0f, 1.0f);
      }

      return iconRect;
    }

    public static void AppendTooltip(string tooltip, ref GUIContent label) {
      if (!string.IsNullOrEmpty(tooltip)) {
        label = new GUIContent(label);
        if (string.IsNullOrEmpty(label.tooltip)) {
          label.tooltip = tooltip;
        } else {
          label.tooltip += "\n\n" + tooltip;
        }
      }
    }

    public static void ScriptPropertyField(Editor editor) {
      ScriptPropertyField(editor.serializedObject);
    }
    
    public static void ScriptPropertyField(SerializedObject obj) {
      var scriptProperty = obj.FindProperty(ScriptPropertyName);
      if (scriptProperty != null) {
        using (new EditorGUI.DisabledScope(true)) {
          EditorGUILayout.PropertyField(scriptProperty);
        }
      }
    }

    public static void Overlay(Rect position, string label) {
      GUI.Label(position, label, FusionEditorSkin.OverlayLabelStyle);
    }
    
    public static void Overlay(Rect position, GUIContent label) {
      GUI.Label(position, label, FusionEditorSkin.OverlayLabelStyle);
    }
    
    public static float GetLinesHeight(int count) {
      return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public static float GetLinesHeightWithNarrowModeSupport(int count) {
      if (!EditorGUIUtility.wideMode) {
        count++;
      }
      return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
    }
    
    public static System.Type GetDrawerTypeIncludingWorkarounds(System.Attribute attribute) {
      var drawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(attribute.GetType());
      if (drawerType == null) {
        return null;
      }

      if (drawerType == typeof(PropertyDrawerForArrayWorkaround)) {
        drawerType = PropertyDrawerForArrayWorkaround.GetDrawerType(attribute.GetType());
      }
      return drawerType;
    }
    
    public static void DisplayTypePickerMenu(Rect position, Type baseType, Action<Type> callback, string noneOptionLabel = "[None]", bool groupByNamespace = true, Type selectedType = null, bool enableAbstract = false, bool enableGenericTypeDefinitions = false) {
      var types = TypeCache.GetTypesDerivedFrom(baseType)
       .Where(x =>
          (enableAbstract || !x.IsAbstract) && 
          (enableGenericTypeDefinitions || !x.IsGenericTypeDefinition)
        )
       .OrderBy(x => x.FullName)
       .ToList();

      List<GUIContent> menuOptions = new List<GUIContent>();
      var actualTypes = new Dictionary<string, System.Type>();

      menuOptions.Add(new GUIContent(noneOptionLabel));
      actualTypes.Add(noneOptionLabel, null);

      int selectedIndex = -1;

      foreach (var ns in types.GroupBy(x => string.IsNullOrEmpty(x.Namespace) ? "[Global Namespace]" : x.Namespace)) {
        foreach (var t in ns) {
          var nameWithoutNamespace = t.FullName;
          if (string.IsNullOrEmpty(nameWithoutNamespace)) {
            continue;
          }

          if (t.Namespace != null && t.Namespace.Length > 0) {
            nameWithoutNamespace = nameWithoutNamespace.Substring(t.Namespace.Length + 1);
          }
          
          string path;
          if (groupByNamespace) {
            path = ns.Key + "/" + nameWithoutNamespace;
          } else {
            path = t.FullName;
          }

          if (actualTypes.ContainsKey(path)) {
            continue;
          }

          menuOptions.Add(new GUIContent(path));
          actualTypes.Add(path, t);

          if (selectedType == t) {
            selectedIndex = menuOptions.Count - 1;
          }
        }
      }

      EditorUtility.DisplayCustomMenu(position, menuOptions.ToArray(), selectedIndex, (userData, options, selected) => {
        var path = options[selected];
        var newType = ((Dictionary<string, System.Type>)userData)[path];
        callback(newType);
      }, actualTypes);
    }
  }
}

#endregion


#region FusionGlobalScriptableObjectUtils.cs

namespace Fusion.Editor {
  using System;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public static class FusionGlobalScriptableObjectUtils {

    public const string GlobalAssetLabel = "FusionDefaultGlobal";

    public static void SetDirty(this FusionGlobalScriptableObject obj) {
      EditorUtility.SetDirty(obj);
    }
    
    public static string GetGlobalAssetPath<T>() where T : FusionGlobalScriptableObject<T> {
      return FindDefaultAssetPath(typeof(T), fallbackToSearchWithoutLabel: false);
    }

    private static FusionGlobalScriptableObjectAttribute GetAttributeOrThrow(Type type) {
      var attribute = type.GetCustomAttribute<FusionGlobalScriptableObjectAttribute>();
      if (attribute == null) {
        throw new InvalidOperationException($"Type {type.FullName} needs to be decorated with {nameof(FusionGlobalScriptableObjectAttribute)}");
      }

      return attribute;
    }
    
    [FusionGlobalScriptableObjectLoaderMethod(Order = int.MaxValue)]
    public static FusionGlobalScriptableObjectLoadResult LoadEditorInstance(Type type) {
      
      var defaultAssetPath = FindDefaultAssetPath(type, fallbackToSearchWithoutLabel: true);
      if (string.IsNullOrEmpty(defaultAssetPath)) {
        return default;
      }

      var result = (FusionGlobalScriptableObject)AssetDatabase.LoadAssetAtPath(defaultAssetPath, type);
      FusionEditorLog.Assert(result);
      return result;
    }

    public static bool EnsureAssetExists<T>() where T : FusionGlobalScriptableObject<T> {
      var defaultAssetPath = FindDefaultAssetPath(typeof(T), fallbackToSearchWithoutLabel: true);
      if (!string.IsNullOrEmpty(defaultAssetPath)) {
        // already exists
        return false;
      }
      
      // need to create a new asset
      var obj = CreateDefaultAsset(typeof(T));
      Debug.Assert(obj != null);
      return true;
    }
    
    private static FusionGlobalScriptableObject CreateDefaultAsset(Type type) {
      const string InstanceFieldName = "s_instance";
      
      var instanceField = type.BaseType.GetField(InstanceFieldName, BindingFlags.Static | BindingFlags.NonPublic);
      if (instanceField == null) {
        throw new InvalidOperationException($"Type {type.FullName} needs to have a static field named {InstanceFieldName}");
      }

      var attribute = GetAttributeOrThrow(type);

      var directoryPath = Path.GetDirectoryName(attribute.DefaultPath);
      if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath)) {
        Directory.CreateDirectory(directoryPath);
        AssetDatabase.Refresh();
      }

      if (File.Exists(attribute.DefaultPath)) {
        throw new InvalidOperationException($"Asset file already exists at '{attribute.DefaultPath}'");
      }

      // is this a regular asset?
      if (attribute.DefaultPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) {
        var instance = (FusionGlobalScriptableObject)ScriptableObject.CreateInstance(type);

        AssetDatabase.CreateAsset(instance, attribute.DefaultPath);
        AssetDatabase.SaveAssets();

        SetDefault(instance);

        // set the instance right away, in case something tries to obtain it
        instanceField.SetValue(null, instance);

        EditorUtility.SetDirty(instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Created new global {type.Name} instance", instance);
        
        return instance;
      } else {
        string defaultContents = null;
        if (!string.IsNullOrEmpty(attribute.DefaultContentsGeneratorMethod)) {
          var method = type.GetMethod(attribute.DefaultContentsGeneratorMethod, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
          if (method == null) {
            throw new InvalidOperationException($"Generator method '{attribute.DefaultContentsGeneratorMethod}' not found on type {type.FullName}");
          }
          defaultContents = (string)method.Invoke(null, null);
        }

        if (defaultContents == null) {
          defaultContents = attribute.DefaultContents;
        }
        
        File.WriteAllText(attribute.DefaultPath, defaultContents ?? string.Empty);
        AssetDatabase.ImportAsset(attribute.DefaultPath, ImportAssetOptions.ForceUpdate);

        var instance = (FusionGlobalScriptableObject)AssetDatabase.LoadAssetAtPath(attribute.DefaultPath, type);
        if (!instance) {
          throw new InvalidOperationException($"Failed to load a newly created asset at '{attribute.DefaultPath}'");
        }
        
        instanceField.SetValue(null, instance);
        return instance;
      }
    }
    
    private static bool IsDefault(this FusionGlobalScriptableObject obj) {
      return Array.IndexOf(AssetDatabase.GetLabels(obj), GlobalAssetLabel) >= 0;
    }

    private static bool SetDefault(FusionGlobalScriptableObject obj) {
      var labels = AssetDatabase.GetLabels(obj);
      if (Array.IndexOf(labels, GlobalAssetLabel) >= 0) {
        return false;
      }

      Array.Resize(ref labels, labels.Length + 1);
      labels[^1] = GlobalAssetLabel;
      AssetDatabase.SetLabels(obj, labels);
      return true;
    }
    
    private static string FindDefaultAssetPath(Type type, bool fallbackToSearchWithoutLabel = false) {
      
      // first try to locate assets of the type, with the default label
      var defaults = AssetDatabaseUtils.IterateAssets(type: type, label: GlobalAssetLabel)
       .Select(x => AssetDatabase.GUIDToAssetPath(x.guid))
       .ToList();
      
      if (defaults.Count == 1) {
        return defaults[0];
      }

      if (defaults.Count > 1) {
        throw new InvalidOperationException($"There are multiple assets of type '{type.Name}' marked as default: '{string.Join("', '", defaults)}'. Remove all labels but one.");
      }

      if (fallbackToSearchWithoutLabel) {
        // now as a fallback, locate all assets of the type and apply the label only if there is only one
        var candidates = AssetDatabaseUtils.IterateAssets(type: type)
         .Select(x => AssetDatabase.GUIDToAssetPath(x.guid))
         .ToList();

        if (candidates.Count == 1) {
          // mark as default
          AssetDatabaseUtils.SetLabel(candidates[0], GlobalAssetLabel, true);
          EditorUtility.SetDirty(AssetDatabase.LoadMainAssetAtPath(candidates[0]));
          FusionEditorLog.Log($"Set '{candidates[0]}' as the default asset for '{type.Name}'");
          return candidates[0];
        }

        if (candidates.Count > 1) {
          throw new InvalidOperationException($"There are no assets of type '{type.Name}' with {GlobalAssetLabel}, but there are multiple candidates: '{string.Join("', '", candidates)}'. Assign label manually or remove all but one.");
        }
      }

      // no candidates
      return string.Empty;
    }
    
    public static bool TryImportGlobal<T>() where T : FusionGlobalScriptableObject<T> {
      var globalPath = GetGlobalAssetPath<T>();
      if (string.IsNullOrEmpty(globalPath)) {
        return false;
      }
      AssetDatabase.ImportAsset(globalPath);
      return true;
    }
  }
}

#endregion


#region FusionGrid.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using Object = UnityEngine.Object;

 [Serializable]
  public class FusionGridState : TreeViewState {
    public MultiColumnHeaderState HeaderState;
    public bool                   SyncSelection;
  }
  
  public class FusionGridItem : TreeViewItem {
    public virtual Object TargetObject => null;
  }
  
  public abstract class FusionGrid<TItem> : FusionGrid<TItem, FusionGridState> 
    where TItem : FusionGridItem {
  }
  
  [Serializable]
  public abstract class FusionGrid<TItem, TState> 
    where TState : FusionGridState, new() 
    where TItem : FusionGridItem
  {
    [SerializeField] public bool   HasValidState;
    [SerializeField] public TState State;
    [SerializeField] public float  UpdatePeriod = 1.0f;
    
    class GUIState {
      public InternalTreeView  TreeView;
      public MultiColumnHeader MultiColumnHeader;
      public SearchField       SearchField;
    }

    [NonSerialized] private Lazy<GUIState> _gui;
    [NonSerialized] private Lazy<Column[]> _columns;
    [NonSerialized] private float          _nextUpdateTime;
    [NonSerialized] private int            _lastContentHash;

    public virtual int GetContentHash() {
      return 0;
    }

    public FusionGrid() {
      ResetColumns();
      ResetGUI();
    }

    void ResetColumns() {
      _columns = new Lazy<Column[]>(() => {
        var columns = CreateColumns().ToArray();
        for (int i = 0; i < columns.Length; ++i) {
          ((MultiColumnHeaderState.Column)columns[i]).userData = i;
        }

        return columns;
      });
    }
    
    void ResetGUI() {
      _gui = new Lazy<GUIState>(() => {

        var result = new GUIState();

        result.MultiColumnHeader = new MultiColumnHeader(State.HeaderState);
        result.MultiColumnHeader.sortingChanged += _ => result.TreeView.Reload();
        result.MultiColumnHeader.ResizeToFit();
        result.SearchField = new SearchField();
        result.SearchField.downOrUpArrowKeyPressed += () => result.TreeView.SetFocusAndEnsureSelectedItem();
        result.TreeView = new InternalTreeView(this, result.MultiColumnHeader);

        return result;
      });
    }
    
    
    public void OnInspectorUpdate() {
      if (!HasValidState) {
        return;
      }

      if (!_gui.IsValueCreated) {
        return;
      }
      
      if (_nextUpdateTime > Time.realtimeSinceStartup) {
        return;
      }
      
      _nextUpdateTime = Time.realtimeSinceStartup + UpdatePeriod;
      
      var hash = GetContentHash();
      if (_lastContentHash == hash) {
        return;
      }

      _lastContentHash = hash;
      _gui.Value.TreeView.Reload();
    }
    
    public void OnEnable() {
      if (HasValidState) {
        return;
      }
      
      var visibleColumns = new List<int>();
      int sortingColumn = -1;

      for (int i = 0; i < _columns.Value.Length; ++i) {
        var column = _columns.Value[i];

        if (!column.initiallyVisible) {
          continue;
        }
        
        visibleColumns.Add(i);
        if (sortingColumn < 0 && column.initiallySorted) {
          sortingColumn = i;
          column.sortedAscending = true;
        }
      }
      
      var headerState = new MultiColumnHeaderState(_columns.Value.Cast<MultiColumnHeaderState.Column>().ToArray()) {
        visibleColumns = visibleColumns.ToArray(),
        sortedColumnIndex = sortingColumn,
      };

      State = new TState() { HeaderState = headerState };
      HasValidState = true;
      ResetGUI();
    }
    
    public void OnGUI(Rect rect) {
      _gui.Value.TreeView.OnGUI(rect);
    }
    
    public void DrawToolbarReloadButton() {
      if (GUILayout.Button(new GUIContent(FusionEditorSkin.RefreshIcon, "Refresh"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) {
        _gui.Value.TreeView.Reload();
      }
    }

    public void DrawToolbarSyncSelectionButton() {
      EditorGUI.BeginChangeCheck();
      State.SyncSelection = GUILayout.Toggle(State.SyncSelection, "Sync Selection", EditorStyles.toolbarButton);
      if (EditorGUI.EndChangeCheck()) {
        if (State.SyncSelection) {
          _gui.Value.TreeView.SyncSelection();
        }
      }
    }

    public void DrawToolbarSearchField() {
      _gui.Value.TreeView.searchString = _gui.Value.SearchField.OnToolbarGUI(_gui.Value.TreeView.searchString);
    }

    public void DrawToolbarResetView() {
      if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) {
        HasValidState = false;
        ResetColumns();
      }
    }
    
    public void ResetTree() {
      ResetGUI();
    }

    protected abstract IEnumerable<Column> CreateColumns();
    protected abstract IEnumerable<TItem>  CreateRows();

    protected virtual GenericMenu CreateContextMenu(TItem item, TreeView treeView) {
      return null;
    }

    protected static Column MakeSimpleColumn<T>(Expression<Func<TItem, T>> propertyExpression, Column column) where T : IComparable {

      string propertyName;
      if (propertyExpression.Body is MemberExpression memberExpression) {
        propertyName = memberExpression.Member.Name;
      } else {
        throw new ArgumentException("Expression is not a member access expression.");
      }
      
      var accessor = propertyExpression.Compile();
      Func<TItem, string> toString = item => $"{accessor(item)}";

      column.getSearchText ??= toString;
      column.getComparer ??= order => (a, b) => EditorUtility.NaturalCompare(toString(a), toString(b)) * order;
      column.cellGUI ??= (item, rect, selected, focused) => TreeView.DefaultGUI.Label(rect, toString(item), selected, focused);
      column.headerContent = new GUIContent(propertyName);
        
      return column;
    }
    
    public class Column  : MultiColumnHeaderState.Column {
      public Func<TItem, string>             getSearchText;
      public Func<int, Comparison<TItem>>    getComparer;
      public Action<TItem, Rect, bool, bool> cellGUI;
      public bool                            initiallyVisible = true;
      public bool                            initiallySorted;

      //
      // [Obsolete("Do not use", true)]
      // public new int userData => throw new NotImplementedException();
    }
    
    class InternalTreeView : TreeView {
      public InternalTreeView(FusionGrid<TItem, TState> grid, MultiColumnHeader header) : base(grid.State, header) {
        Grid = grid;
        showAlternatingRowBackgrounds = true;
        this.Reload();
      }
      
      public new TState state => (TState)base.state;
      
      public FusionGrid<TItem, TState> Grid { get; }

      
      protected override void SelectionChanged(IList<int> selectedIds) {
        base.SelectionChanged(selectedIds);
        if (state.SyncSelection) {
          SyncSelection();
        }
      }
      
      protected override void SingleClickedItem(int id) {
        var item = (TItem)FindItem(id, rootItem);
        var obj = item.TargetObject;
        if (obj) {
          EditorGUIUtility.PingObject(obj);
        }
        base.SingleClickedItem(id);
      }
      
      public void SyncSelection() {
        List<Object> selection = new List<Object>();
        foreach (var id in this.state.selectedIDs) {
          if (id == 0) {
            continue;
          }
          var item = (TItem)FindItem(id, rootItem);
          var obj = item.TargetObject;
          if (obj) {
            selection.Add(obj);
          }
        }
        Selection.objects = selection.ToArray();
      }
      
      
      private Column GetColumnForIndex(int index) {
        var column = multiColumnHeader.GetColumn(index);
        var ud = column.userData;
        return Grid._columns.Value[ud];
      }
      
      protected override TreeViewItem BuildRoot() {
        var allItems = new List<TItem>();

        var root = new TreeViewItem {
          id          = 0,
          depth       = -1,
          displayName = "Root"
        };
        
        foreach (var row in Grid.CreateRows()) {
          allItems.Add(row);
        }
        
        SetupParentsAndChildrenFromDepths(root, allItems.Cast<TreeViewItem>().ToList());
        return root;
      }
      
      private class ComparisonComparer : IComparer<TItem> {
        public Comparison<TItem> Comparison;
        public int Compare(TItem x, TItem y) => Comparison(x, y);
      }

      private Comparison<TItem> GetComparision() {
        if (multiColumnHeader.sortedColumnIndex < 0) {
          return null;
        }
        var column = GetColumnForIndex(multiColumnHeader.sortedColumnIndex);
        var isSortedAscending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);
        return column.getComparer(isSortedAscending ? 1 : -1);
      }

      protected override IList<TreeViewItem> BuildRows(TreeViewItem root) {
        var comparision = GetComparision();
        if (comparision == null) {
          return base.BuildRows(root);
        }
        
        // stable sort
        return base.BuildRows(root).OrderBy(x => (TItem)x, new ComparisonComparer() { Comparison = comparision }).ToArray();
      }

      protected override void ContextClickedItem(int id) {
        var item = (TItem)FindItem(id, rootItem);
        if (item == null) {
          return;
        }

        var menu = Grid.CreateContextMenu(item, this);
        if (menu != null) {
          menu.ShowAsContext();
        }
      }

      protected override void RowGUI(RowGUIArgs args) {
        for (var i = 0; i < args.GetNumVisibleColumns(); ++i) {
          var cellRect = args.GetCellRect(i);
          CenterRectUsingSingleLineHeight(ref cellRect);
          var item = (TItem)args.item;
          var column = GetColumnForIndex(args.GetColumn(i));
          column.cellGUI?.Invoke(item, cellRect, args.selected, args.focused);
        }
      }
      
      protected override bool DoesItemMatchSearch(TreeViewItem item_, string search) {
        var item = item_ as TItem;
        if (item == null) {
          return base.DoesItemMatchSearch(item_, search);
        }

        var searchParts = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (searchParts.Length == 0) {
          return true;
        }

        var columns = multiColumnHeader.state.columns;

        for (var i = 0; i < columns.Length; ++i) {
          if (!multiColumnHeader.IsColumnVisible(i)) {
            continue;
          }

          
          var column = GetColumnForIndex(i);
          var text = column.getSearchText?.Invoke(item);

          if (text == null) {
            continue;
          }

          bool columnMatchesSearch = true;
          foreach (var part in searchParts) {
            if (!text.Contains(part, StringComparison.OrdinalIgnoreCase)) {
              columnMatchesSearch = false;
              break;
            }
          }

          if (columnMatchesSearch) {
            return true;
          }
        }

        return false;
      }
    }
    
    class InternalTreeViewItem : TreeViewItem {
      
    }
  }
}

#endregion


#region FusionMonoBehaviourDefaultEditor.cs

namespace Fusion.Editor {
  using UnityEditor;

  [CustomEditor(typeof(FusionMonoBehaviour), true)]
  internal class FusionMonoBehaviourDefaultEditor : FusionEditor {
  }
}

#endregion


#region FusionPropertyDrawerMetaAttribute.cs

namespace Fusion.Editor {
  using System;

  [AttributeUsage(AttributeTargets.Class)]
  public class FusionPropertyDrawerMetaAttribute : Attribute {
    public bool HasFoldout   { get; set; }
    public bool HandlesUnits { get; set; }
  }
}

#endregion


#region FusionScriptableObjectDefaultEditor.cs

namespace Fusion.Editor {
  using UnityEditor;

  [CustomEditor(typeof(FusionScriptableObject), true)]
  internal class FusionScriptableObjectDefaultEditor : FusionEditor {
  }
}

#endregion


#region RawDataDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  public struct RawDataDrawer {
    private StringBuilder _builder;
    private GUIContent    _lastValue;
    private int           _lastHash;

    public void Clear() {
      _builder?.Clear();
      _lastHash = 0;
      _lastValue = GUIContent.none;
    }

    public bool HasContent => _lastValue != null && _lastValue.text.Length > 0;

    public unsafe void Refresh<T>(Span<T> data, int maxLength = 2048, bool addSpaces = true) where T : unmanaged {

      int charactersPerElement = 2 * sizeof(T); 
        
      int arrayHash = 0;
      int effectiveArraySize;
      {
        int length = 0;
        int i;
        for (i = 0; i < data.Length && length < maxLength; ++i) {
          arrayHash = arrayHash * 31 + data[i].GetHashCode();
          length += charactersPerElement;
          if (addSpaces) {
            length += 1;
          }
        }

        effectiveArraySize = i;
      }

      if (_builder == null || arrayHash != _lastHash) {
        var format = "{0:x" + charactersPerElement + "}" + (addSpaces ? " " : "");

        _builder ??= new StringBuilder();
        _builder.Clear();


        for (int i = 0; i < effectiveArraySize; ++i) {
          _builder.AppendFormat(format, data[i]);
        }

        if (effectiveArraySize < data.Length) {
          _builder.AppendLine("...");
        }

        _lastValue = new GUIContent(_builder.ToString());
      } else {
        Debug.Assert(_lastValue != null);
      }
    }

    public void Refresh(SerializedProperty property, int maxLength = 2048) {
      Debug.Assert(property.isArray);

      int charactersPerElement;
      switch (property.arrayElementType) {
        case "long":
        case "ulong":
          charactersPerElement = 16;
          break;
        case "int":
        case "uint":
          charactersPerElement = 8;
          break;
        case "short":
        case "ushort":
          charactersPerElement = 4;
          break;
        case "sbyte":
        case "byte":
          charactersPerElement = 2;
          break;
        default:
          throw new NotImplementedException(property.arrayElementType);
      }

      int arrayHash = 0;
      int effectiveArraySize;
      {
        int length = 0;
        int i;
        for (i = 0; i < property.arraySize && length < maxLength; ++i) {
          arrayHash = arrayHash * 31 + property.GetArrayElementAtIndex(i).longValue.GetHashCode();
          length += charactersPerElement + 1;
        }

        effectiveArraySize = i;
      }

      if (_builder == null || arrayHash != _lastHash) {
        var format = "{0:x" + charactersPerElement + "} ";

        _builder ??= new StringBuilder();
        _builder.Clear();

        for (int i = 0; i < effectiveArraySize; ++i) {
          _builder.AppendFormat(format, property.GetArrayElementAtIndex(i).longValue);
        }

        if (effectiveArraySize < property.arraySize) {
          _builder.AppendLine("...");
        }

        _lastValue = new GUIContent(_builder.ToString());
      } else {
        Debug.Assert(_lastValue != null);
      }
    }


    public float GetHeight(float width) {
      return FusionEditorSkin.RawDataStyle.Value.CalcHeight(_lastValue ?? GUIContent.none, width);
    }

    public string Draw(Rect position) => Draw(GUIContent.none, position);
    
    public string Draw(GUIContent label, Rect position) {
      var id = GUIUtility.GetControlID(UnityInternal.EditorGUI.DelayedTextFieldHash, FocusType.Keyboard, position);
      return UnityInternal.EditorGUI.DelayedTextFieldInternal(position, id, label, _lastValue.text ?? string.Empty, "0123456789abcdefABCDEF ", FusionEditorSkin.RawDataStyle);
    }
  }
}

#endregion


#region ReflectionUtils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Reflection;
  using UnityEditor;

  static partial class ReflectionUtils {
    public const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static Type GetUnityLeafType(this Type type) {
      if (type.HasElementType) {
        type = type.GetElementType();
      } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
        type = type.GetGenericArguments()[0];
      }

      return type;
    }

    public static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      try {
        return CreateMethodDelegateInternal<T>(type, methodName, flags);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(type.Assembly, type.FullName, methodName, flags), ex);
      }
    }

    public static Delegate CreateMethodDelegate(this Type type, string methodName, BindingFlags flags, Type delegateType) {
      try {
        return CreateMethodDelegateInternal(type, methodName, flags, delegateType);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage(type.Assembly, type.FullName, methodName, flags, delegateType), ex);
      }
    }

    public static T CreateMethodDelegate<T>(Assembly assembly, string typeName, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      try {
        var type = assembly.GetType(typeName, true);
        return CreateMethodDelegateInternal<T>(type, methodName, flags);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(assembly, typeName, methodName, flags), ex);
      }
    }

    public static Delegate CreateMethodDelegate(Assembly assembly, string typeName, string methodName, BindingFlags flags, Type delegateType) {
      try {
        var type = assembly.GetType(typeName, true);
        return CreateMethodDelegateInternal(type, methodName, flags, delegateType);
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage(assembly, typeName, methodName, flags, delegateType), ex);
      }
    }

    public static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags, Type delegateType, params DelegateSwizzle[] fallbackSwizzles) where T : Delegate {
      try {
        var method = GetMethodOrThrow(type, methodName, flags, delegateType, fallbackSwizzles, out var swizzle);

        var delegateParameters = typeof(T).GetMethod("Invoke").GetParameters();
        var parameters         = new List<ParameterExpression>();

        for (var i = 0; i < delegateParameters.Length; ++i) {
          parameters.Add(Expression.Parameter(delegateParameters[i].ParameterType, $"param_{i}"));
        }

        var convertedParameters = new List<Expression>();
        {
          var methodParameters = method.GetParameters();
          if (swizzle == null) {
            for (int i = 0, j = method.IsStatic ? 0 : 1; i < methodParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(parameters[j], methodParameters[i].ParameterType));
            }
          } else {
            var swizzledParameters = swizzle.Swizzle(parameters.ToArray());
            for (int i = 0, j = method.IsStatic ? 0 : 1; i < methodParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(swizzledParameters[j], methodParameters[i].ParameterType));
            }
          }
        }

        MethodCallExpression callExpression;
        if (method.IsStatic) {
          callExpression = Expression.Call(method, convertedParameters);
        } else {
          var instance = Expression.Convert(parameters[0], method.DeclaringType);
          callExpression = Expression.Call(instance, method, convertedParameters);
        }

        var l   = Expression.Lambda(typeof(T), callExpression, parameters);
        var del = l.Compile();
        return (T)del;
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(type.Assembly, type.FullName, methodName, flags), ex);
      }
    }

    public static T CreateConstructorDelegate<T>(this Type type, BindingFlags flags, Type delegateType, params DelegateSwizzle[] fallbackSwizzles) where T : Delegate {
      try {
        var constructor = GetConstructorOrThrow(type, flags, delegateType, fallbackSwizzles, out var swizzle);

        var delegateParameters = typeof(T).GetMethod("Invoke").GetParameters();
        var parameters         = new List<ParameterExpression>();

        for (var i = 0; i < delegateParameters.Length; ++i) {
          parameters.Add(Expression.Parameter(delegateParameters[i].ParameterType, $"param_{i}"));
        }

        var convertedParameters = new List<Expression>();
        {
          var constructorParameters = constructor.GetParameters();
          if (swizzle == null) {
            for (int i = 0, j = 0; i < constructorParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(parameters[j], constructorParameters[i].ParameterType));
            }
          } else {
            var swizzledParameters = swizzle.Swizzle(parameters.ToArray());
            for (int i = 0, j = 0; i < constructorParameters.Length; ++i, ++j) {
              convertedParameters.Add(Expression.Convert(swizzledParameters[j], constructorParameters[i].ParameterType));
            }
          }
        }

        var newExpression = Expression.New(constructor, convertedParameters);
        var l             = Expression.Lambda(typeof(T), newExpression, parameters);
        var del           = l.Compile();
        return (T)del;
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateConstructorExceptionMessage(type.Assembly, type.FullName, flags), ex);
      }
    }

    /// <summary>
    ///   Returns the first found member of the given name. Includes private members.
    /// </summary>
    public static MemberInfo GetMemberIncludingBaseTypes(this Type type, string memberName, BindingFlags flags = DefaultBindingFlags, Type stopAtType = null) {
      var members = type.GetMember(memberName, flags);
      if (members.Length > 0) {
        return members[0];
      }

      type = type.BaseType;

      // loop as long as we have a parent class to search.
      while (type != null) {
        // No point recursing into the abstracts.
        if (type == stopAtType) {
          break;
        }

        members = type.GetMember(memberName, flags);
        if (members.Length > 0) {
          return members[0];
        }

        type = type.BaseType;
      }

      return null;
    }

    /// <summary>
    ///   Normal reflection GetField() won't find private fields in parents (only will find protected). So this recurses the
    ///   hard to find privates.
    ///   This is needed since Unity serialization does find inherited privates.
    /// </summary>
    public static FieldInfo GetFieldIncludingBaseTypes(this Type type, string fieldName, BindingFlags flags = DefaultBindingFlags, Type stopAtType = null) {
      var field = type.GetField(fieldName, flags);
      if (field != null) {
        return field;
      }

      type = type.BaseType;

      // loop as long as we have a parent class to search.
      while (type != null) {
        // No point recursing into the abstracts.
        if (type == stopAtType) {
          break;
        }

        field = type.GetField(fieldName, flags);
        if (field != null) {
          return field;
        }

        type = type.BaseType;
      }

      return null;
    }

    public static FieldInfo GetFieldOrThrow(this Type type, string fieldName, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetField(fieldName, flags);
      if (field == null) {
        throw new ArgumentOutOfRangeException(nameof(fieldName), CreateFieldExceptionMessage(type.Assembly, type.FullName, fieldName, flags));
      }

      return field;
    }

    public static FieldInfo GetFieldOrThrow<T>(this Type type, string fieldName, BindingFlags flags = DefaultBindingFlags) {
      return GetFieldOrThrow(type, fieldName, typeof(T), flags);
    }

    public static FieldInfo GetFieldOrThrow(this Type type, string fieldName, Type fieldType, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetField(fieldName, flags);
      if (field == null) {
        throw new ArgumentOutOfRangeException(nameof(fieldName), CreateFieldExceptionMessage(type.Assembly, type.FullName, fieldName, flags));
      }

      if (fieldType != null) {
        if (field.FieldType != fieldType) {
          throw new InvalidProgramException($"Field {type.FullName}.{fieldName} is of type {field.FieldType}, not expected {fieldType}");
        }
      }

      return field;
    }

    public static PropertyInfo GetPropertyOrThrow<T>(this Type type, string propertyName, BindingFlags flags = DefaultBindingFlags) {
      return GetPropertyOrThrow(type, propertyName, typeof(T), flags);
    }

    public static PropertyInfo GetPropertyOrThrow(this Type type, string propertyName, Type propertyType, BindingFlags flags = DefaultBindingFlags) {
      var property = type.GetProperty(propertyName, flags);
      if (property == null) {
        throw new ArgumentOutOfRangeException(nameof(propertyName), CreateFieldExceptionMessage(type.Assembly, type.FullName, propertyName, flags));
      }

      if (property.PropertyType != propertyType) {
        throw new InvalidProgramException($"Property {type.FullName}.{propertyName} is of type {property.PropertyType}, not expected {propertyType}");
      }

      return property;
    }

    public static PropertyInfo GetPropertyOrThrow(this Type type, string propertyName, BindingFlags flags = DefaultBindingFlags) {
      var property = type.GetProperty(propertyName, flags);
      if (property == null) {
        throw new ArgumentOutOfRangeException(nameof(propertyName), CreateFieldExceptionMessage(type.Assembly, type.FullName, propertyName, flags));
      }

      return property;
    }

    public static ConstructorInfo GetConstructorInfoOrThrow(this Type type, Type[] types, BindingFlags flags = DefaultBindingFlags) {
      var constructor = type.GetConstructor(flags, null, types, null);
      if (constructor == null) {
        throw new ArgumentOutOfRangeException(nameof(types), CreateConstructorExceptionMessage(type.Assembly, type.FullName, types, flags));
      }

      return constructor;
    }

    public static Type GetNestedTypeOrThrow(this Type type, string name, BindingFlags flags) {
      var result = type.GetNestedType(name, flags);
      if (result == null) {
        throw new ArgumentOutOfRangeException(nameof(name), CreateFieldExceptionMessage(type.Assembly, type.FullName, name, flags));
      }

      return result;
    }

    public static Func<object, object> CreateGetter(this Type type, string memberName, BindingFlags flags = DefaultBindingFlags) {
      return CreateGetter<object>(type, memberName, flags);
    }
    
    public static Func<object, T> CreateGetter<T>(this Type type, string memberName, BindingFlags flags = DefaultBindingFlags) {
      var candidates = type.GetMembers(flags).Where(x => x.Name == memberName)
       .ToList();
      
      if (candidates.Count > 1) {
        throw new InvalidOperationException($"Multiple members with name {memberName} found in type {type.FullName}");
      }
      if (candidates.Count == 0) {
        throw new ArgumentOutOfRangeException(nameof(memberName),$"No members with name {memberName} found in type {type.FullName}");
      }

      var  candidate = candidates[0];
      bool isStatic  = false;
      switch (candidate) {
        case FieldInfo field:
          isStatic = field.IsStatic;
          break;
        case PropertyInfo property:
          isStatic = property.GetMethod.IsStatic;
          break;
        case MethodInfo method:
          isStatic = method.IsStatic;
          break;
      }

      if (isStatic) {
        var getter = CreateStaticAccessorInternal<T>(candidate).GetValue;
        return _ => getter();
      } else {
        return CreateAccessorInternal<T>(candidate).GetValue;
      }
    }

    public static InstanceAccessor<object> CreateFieldAccessor(this Type type, string fieldName, Type expectedFieldType = null, BindingFlags flags = DefaultBindingFlags) {
      return CreateFieldAccessor<object>(type, fieldName, expectedFieldType);
    }
    
    public static InstanceAccessor<FieldType> CreateFieldAccessor<FieldType>(this Type type, string fieldName, Type expectedFieldType = null, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetFieldOrThrow(fieldName, expectedFieldType ?? typeof(FieldType), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateAccessorInternal<FieldType>(field);
    }

    public static StaticAccessor<object> CreateStaticFieldAccessor(this Type type, string fieldName, Type expectedFieldType = null) {
      return CreateStaticFieldAccessor<object>(type, fieldName, expectedFieldType);
    }

    public static StaticAccessor<FieldType> CreateStaticFieldAccessor<FieldType>(this Type type, string fieldName, Type expectedFieldType = null) {
      var field = type.GetFieldOrThrow(fieldName, expectedFieldType ?? typeof(FieldType), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateStaticAccessorInternal<FieldType>(field);
    }

    public static InstanceAccessor<PropertyType> CreatePropertyAccessor<PropertyType>(this Type type, string fieldName, Type expectedPropertyType = null, BindingFlags flags = DefaultBindingFlags) {
      var field = type.GetPropertyOrThrow(fieldName, expectedPropertyType ?? typeof(PropertyType), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateAccessorInternal<PropertyType>(field);
    }

    public static StaticAccessor<object> CreateStaticPropertyAccessor(this Type type, string fieldName, Type expectedFieldType = null) {
      return CreateStaticPropertyAccessor<object>(type, fieldName, expectedFieldType);
    }

    public static StaticAccessor<FieldType> CreateStaticPropertyAccessor<FieldType>(this Type type, string fieldName, Type expectedFieldType = null) {
      var field = type.GetPropertyOrThrow(fieldName, expectedFieldType ?? typeof(FieldType), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      return CreateStaticAccessorInternal<FieldType>(field);
    }

    private static string CreateMethodExceptionMessage<T>(Assembly assembly, string typeName, string methodName, BindingFlags flags) {
      return CreateMethodExceptionMessage(assembly, typeName, methodName, flags, typeof(T));
    }

    private static string CreateMethodExceptionMessage(Assembly assembly, string typeName, string methodName, BindingFlags flags, Type delegateType) {
      return $"{assembly.FullName}.{typeName}.{methodName} with flags: {flags} and type: {delegateType}";
    }

    private static string CreateFieldExceptionMessage(Assembly assembly, string typeName, string fieldName, BindingFlags flags) {
      return $"{assembly.FullName}.{typeName}.{fieldName} with flags: {flags}";
    }

    private static string CreateConstructorExceptionMessage(Assembly assembly, string typeName, BindingFlags flags) {
      return $"{assembly.FullName}.{typeName}() with flags: {flags}";
    }

    private static string CreateConstructorExceptionMessage(Assembly assembly, string typeName, Type[] types, BindingFlags flags) {
      return $"{assembly.FullName}.{typeName}({string.Join(", ", types.Select(x => x.FullName))}) with flags: {flags}";
    }

    private static T CreateMethodDelegateInternal<T>(this Type type, string name, BindingFlags flags) where T : Delegate {
      return (T)CreateMethodDelegateInternal(type, name, flags, typeof(T));
    }

    private static Delegate CreateMethodDelegateInternal(this Type type, string name, BindingFlags flags, Type delegateType) {
      var method = GetMethodOrThrow(type, name, flags, delegateType);
      return Delegate.CreateDelegate(delegateType, null, method);
    }

    private static MethodInfo GetMethodOrThrow(Type type, string name, BindingFlags flags, Type delegateType) {
      return GetMethodOrThrow(type, name, flags, delegateType, Array.Empty<DelegateSwizzle>(), out _);
    }

    private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, Type returnType, params Type[] parameters) {
      var method = type.GetMethod(name, flags, null, parameters, null);

      if (method == null) {
        return null;
      }

      if (method.ReturnType != returnType) {
        return null;
      }

      return method;
    }

    private static ConstructorInfo GetConstructorOrThrow(Type type, BindingFlags flags, Type delegateType, DelegateSwizzle[] swizzles, out DelegateSwizzle firstMatchingSwizzle) {
      var delegateMethod = delegateType.GetMethod("Invoke");

      var allDelegateParameters = delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();

      var constructor = type.GetConstructor(flags, null, allDelegateParameters, null);
      if (constructor != null) {
        firstMatchingSwizzle = null;
        return constructor;
      }

      if (swizzles != null) {
        foreach (var swizzle in swizzles) {
          var swizzled = swizzle.Swizzle(allDelegateParameters);
          constructor = type.GetConstructor(flags, null, swizzled, null);
          if (constructor != null) {
            firstMatchingSwizzle = swizzle;
            return constructor;
          }
        }
      }

      var constructors = type.GetConstructors(flags);
      throw new ArgumentOutOfRangeException(nameof(delegateType), $"No matching constructor found for {type}, " +
        $"signature \"{delegateType}\", " +
        $"flags \"{flags}\" and " +
        $"params: {string.Join(", ", allDelegateParameters.Select(x => x.FullName))}" +
        $", candidates are\n: {string.Join("\n", constructors.Select(x => x.ToString()))}");
    }

    private static MethodInfo GetMethodOrThrow(Type type, string name, BindingFlags flags, Type delegateType, DelegateSwizzle[] swizzles, out DelegateSwizzle firstMatchingSwizzle) {
      var delegateMethod = delegateType.GetMethod("Invoke");

      var allDelegateParameters = delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();

      var method = FindMethod(type, name, flags, delegateMethod.ReturnType, flags.HasFlag(BindingFlags.Static) ? allDelegateParameters : allDelegateParameters.Skip(1).ToArray());
      if (method != null) {
        firstMatchingSwizzle = null;
        return method;
      }

      if (swizzles != null) {
        foreach (var swizzle in swizzles) {
          var swizzled = swizzle.Swizzle(allDelegateParameters);
          if (!flags.HasFlag(BindingFlags.Static) && swizzled[0] != type) {
            throw new InvalidOperationException();
          }

          method = FindMethod(type, name, flags, delegateMethod.ReturnType, flags.HasFlag(BindingFlags.Static) ? swizzled : swizzled.Skip(1).ToArray());
          if (method != null) {
            firstMatchingSwizzle = swizzle;
            return method;
          }
        }
      }

      var methods = type.GetMethods(flags);
      throw new ArgumentOutOfRangeException(nameof(name), $"No method found matching name \"{name}\", " +
        $"signature \"{delegateType}\", " +
        $"flags \"{flags}\" and " +
        $"params: {string.Join(", ", allDelegateParameters.Select(x => x.FullName))}" +
        $", candidates are\n: {string.Join("\n", methods.Select(x => x.ToString()))}");
    }

    public static bool IsArrayOrList(this Type listType) {
      if (listType.IsArray) {
        return true;
      }

      if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) {
        return true;
      }

      return false;
    }

    public static Type GetArrayOrListElementType(this Type listType) {
      if (listType.IsArray) {
        return listType.GetElementType();
      }

      if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) {
        return listType.GetGenericArguments()[0];
      }

      return null;
    }

    public static Type MakeFuncType(params Type[] types) {
      return GetFuncType(types.Length).MakeGenericType(types);
    }

    private static Type GetFuncType(int argumentCount) {
      switch (argumentCount) {
        case 1:  return typeof(Func<>);
        case 2:  return typeof(Func<,>);
        case 3:  return typeof(Func<,,>);
        case 4:  return typeof(Func<,,,>);
        case 5:  return typeof(Func<,,,,>);
        case 6:  return typeof(Func<,,,,,>);
        default: throw new ArgumentOutOfRangeException(nameof(argumentCount));
      }
    }

    public static Type MakeActionType(params Type[] types) {
      if (types.Length == 0) {
        return typeof(Action);
      }

      return GetActionType(types.Length).MakeGenericType(types);
    }

    private static Type GetActionType(int argumentCount) {
      switch (argumentCount) {
        case 1:  return typeof(Action<>);
        case 2:  return typeof(Action<,>);
        case 3:  return typeof(Action<,,>);
        case 4:  return typeof(Action<,,,>);
        case 5:  return typeof(Action<,,,,>);
        case 6:  return typeof(Action<,,,,,>);
        default: throw new ArgumentOutOfRangeException(nameof(argumentCount));
      }
    }

    private static StaticAccessor<T> CreateStaticAccessorInternal<T>(MemberInfo member) {
      try {
        var valueParameter = Expression.Parameter(typeof(T), "value");
        var canWrite       = true;

        UnaryExpression  valueExpression;
        Expression memberExpression;
        
        switch (member) {
          case PropertyInfo property:
            valueExpression  = Expression.Convert(valueParameter, property.PropertyType);
            memberExpression = Expression.Property(null, property);
            canWrite         = property.CanWrite;
            break;
          case FieldInfo field:
            valueExpression  = Expression.Convert(valueParameter, field.FieldType);
            memberExpression = Expression.Field(null, field);
            canWrite         = field.IsInitOnly == false;
            break;
          case MethodInfo method when method.GetParameters().Length == 0:
            valueExpression  = null;
            memberExpression = Expression.Call(method);
            canWrite         = false;
            break;
          default:
            throw new InvalidOperationException($"Unsupported member type {member.GetType().Name}");
        }
        
        Func<T> getter;
        var     getExpression = Expression.Convert(memberExpression, typeof(T));
        var     getLambda     = Expression.Lambda<Func<T>>(getExpression);
        getter = getLambda.Compile();

        Action<T> setter = null;
        if (canWrite) {
          var setExpression = Expression.Assign(memberExpression, valueExpression);
          var setLambda     = Expression.Lambda<Action<T>>(setExpression, valueParameter);
          setter = setLambda.Compile();
        }

        return new StaticAccessor<T> {
          GetValue = getter,
          SetValue = setter
        };
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to create accessor for {member.DeclaringType}.{member.Name}", ex);
      }
    }

    private static InstanceAccessor<T> CreateAccessorInternal<T>(MemberInfo member) {
      try {
        var instanceParameter  = Expression.Parameter(typeof(object), "instance");
        var instanceExpression = Expression.Convert(instanceParameter, member.DeclaringType);

        var valueParameter = Expression.Parameter(typeof(T), "value");
        var canWrite       = true;

        UnaryExpression  valueExpression;
        Expression memberExpression;

        switch (member) {
          case PropertyInfo property:
            valueExpression  = Expression.Convert(valueParameter, property.PropertyType);
            memberExpression = Expression.Property(instanceExpression, property);
            canWrite         = property.CanWrite;
            break;
          case FieldInfo field:
            valueExpression  = Expression.Convert(valueParameter, field.FieldType);
            memberExpression = Expression.Field(instanceExpression, field);
            canWrite         = field.IsInitOnly == false;
            break;
          case MethodInfo method when method.GetParameters().Length == 0:
            valueExpression  = null;
            memberExpression = Expression.Call(instanceExpression, method);
            canWrite         = false;
            break;
          default:
            throw new InvalidOperationException($"Unsupported member type {member.GetType().Name}");
        }

        var getExpression = Expression.Convert(memberExpression, typeof(T));
        var getLambda     = Expression.Lambda<Func<object, T>>(getExpression, instanceParameter);
        var getter = getLambda.Compile();

        Action<object, T> setter = null;
        if (canWrite) {
          var setExpression = Expression.Assign(memberExpression, valueExpression);
          var setLambda     = Expression.Lambda<Action<object, T>>(setExpression, instanceParameter, valueParameter);
          setter = setLambda.Compile();
        }

        return new InstanceAccessor<T> {
          GetValue = getter,
          SetValue = setter
        };
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to create accessor for {member.DeclaringType}.{member.Name}", ex);
      }
    }

    public struct InstanceAccessor<TValue> {
      public Func<object, TValue>   GetValue;
      public Action<object, TValue> SetValue;
    }

    public struct StaticAccessor<TValue> {
      public Func<TValue>   GetValue;
      public Action<TValue> SetValue;
    }

    public class DelegateSwizzle {
      private readonly int[] _args;

      public DelegateSwizzle(params int[] args) {
        _args = args;
      }

      public int Count => _args.Length;

      public T[] Swizzle<T>(T[] inputTypes) {
        var result = new T[_args.Length];

        for (var i = 0; i < _args.Length; ++i) {
          result[i] = inputTypes[_args[i]];
        }

        return result;
      }
    }

#if UNITY_EDITOR

    public static T CreateEditorMethodDelegate<T>(string editorAssemblyTypeName, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      return CreateMethodDelegate<T>(typeof(Editor).Assembly, editorAssemblyTypeName, methodName, flags);
    }

    public static Delegate CreateEditorMethodDelegate(string editorAssemblyTypeName, string methodName, BindingFlags flags, Type delegateType) {
      return CreateMethodDelegate(typeof(Editor).Assembly, editorAssemblyTypeName, methodName, flags, delegateType);
    }

#endif
  }
}

#endregion


#region SerializedPropertyUtilities.cs

namespace Fusion.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Text.RegularExpressions;
  using UnityEditor;

  static partial class SerializedPropertyUtilities {
    private static readonly Regex _arrayElementRegex = new(@"\.Array\.data\[\d+\]$", RegexOptions.Compiled);

    public static SerializedProperty FindPropertyOrThrow(this SerializedObject so, string propertyPath) {
      var result = so.FindProperty(propertyPath);
      if (result == null) {
        throw new ArgumentOutOfRangeException(nameof(propertyPath), $"Property not found: {propertyPath} on {so.targetObject}");
      }

      return result;
    }

    public static SerializedProperty FindPropertyRelativeOrThrow(this SerializedProperty sp, string relativePropertyPath) {
      var result = sp.FindPropertyRelative(relativePropertyPath);
      if (result == null) {
        throw new ArgumentOutOfRangeException(nameof(relativePropertyPath), $"Property not found: {relativePropertyPath} (relative to {sp.propertyPath} of {sp.serializedObject.targetObject}");
      }

      return result;
    }

    public static SerializedProperty FindPropertyRelativeToParentOrThrow(this SerializedProperty property, string relativePath) {
      var result = FindPropertyRelativeToParent(property, relativePath);
      if (result == null) {
        throw new ArgumentOutOfRangeException(nameof(relativePath), $"Property not found: {relativePath} (relative to the parent of \"{property.propertyPath}\" of {property.serializedObject.targetObject}");
      }

      return result;
    }

    public static SerializedProperty FindPropertyRelativeToParent(this SerializedProperty property, string relativePath) {
      
      var parentPath = property.propertyPath;
      int startIndex = 0;

      do {
        // array element?
        if (parentPath.EndsWith("]")) {
          var match = _arrayElementRegex.Match(parentPath);
          if (match.Success) {
            parentPath = parentPath.Substring(0, match.Index);
          }
        }

        var lastDotIndex = parentPath.LastIndexOf('.');
        if (lastDotIndex < 0) {
          if (string.IsNullOrEmpty(parentPath)) {
            return null;
          }

          parentPath = string.Empty;
        } else {
          parentPath = parentPath.Substring(0, lastDotIndex);
        }

      } while (relativePath[startIndex++] == '^');

      if (startIndex > 1) {
        relativePath = relativePath.Substring(startIndex - 1);
      }
      
      if (string.IsNullOrEmpty(parentPath)) {
        return property.serializedObject.FindProperty(relativePath);
      } else {
        return property.serializedObject.FindProperty(parentPath + "." + relativePath);
      }
    }

    public static bool IsArrayElement(this SerializedProperty sp) {
      var propertyPath = sp.propertyPath;
      if (!propertyPath.EndsWith("]", StringComparison.Ordinal)) {
        return false;
      }

      return true;
    }

    public static bool IsArrayElement(this SerializedProperty sp, out int index) {
      var propertyPath = sp.propertyPath;
      if (!propertyPath.EndsWith("]", StringComparison.Ordinal)) {
        index = -1;
        return false;
      }

      var indexStart = propertyPath.LastIndexOf("[", StringComparison.Ordinal);
      if (indexStart < 0) {
        index = -1;
        return false;
      }

      index = int.Parse(propertyPath.Substring(indexStart + 1, propertyPath.Length - indexStart - 2));
      return true;
    }

    public static SerializedProperty GetArrayFromArrayElement(this SerializedProperty sp) {
      var path  = sp.propertyPath;
      var match = _arrayElementRegex.Match(path);
      if (!match.Success) {
        throw new ArgumentException($"Property is not an array element: {path}");
      }

      var arrayPath = path.Substring(0, match.Index);
      return sp.serializedObject.FindProperty(arrayPath);
    }

    public static bool IsArrayProperty(this SerializedProperty sp) {
      return sp.isArray && sp.propertyType != SerializedPropertyType.String;
    }

    public static SerializedPropertyEnumerable GetChildren(this SerializedProperty property, bool visibleOnly = true) {
      return new SerializedPropertyEnumerable(property, visibleOnly);
    }

    public class SerializedPropertyEqualityComparer : IEqualityComparer<SerializedProperty> {
      public static SerializedPropertyEqualityComparer Instance = new();

      public bool Equals(SerializedProperty x, SerializedProperty y) {
        return SerializedProperty.DataEquals(x, y);
      }

      public int GetHashCode(SerializedProperty p) {
        bool enterChildren;
        var  isFirst  = true;
        var  hashCode = 0;
        var  minDepth = p.depth + 1;

        do {
          enterChildren = false;

          switch (p.propertyType) {
            case SerializedPropertyType.Integer:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue);
              break;
            case SerializedPropertyType.Boolean:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.boolValue.GetHashCode());
              break;
            case SerializedPropertyType.Float:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.floatValue.GetHashCode());
              break;
            case SerializedPropertyType.String:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.stringValue.GetHashCode());
              break;
            case SerializedPropertyType.Color:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.colorValue.GetHashCode());
              break;
            case SerializedPropertyType.ObjectReference:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.objectReferenceInstanceIDValue);
              break;
            case SerializedPropertyType.LayerMask:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue);
              break;
            case SerializedPropertyType.Enum:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue);
              break;
            case SerializedPropertyType.Vector2:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.vector2Value.GetHashCode());
              break;
            case SerializedPropertyType.Vector3:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.vector3Value.GetHashCode());
              break;
            case SerializedPropertyType.Vector4:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.vector4Value.GetHashCode());
              break;
            case SerializedPropertyType.Vector2Int:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.vector2IntValue.GetHashCode());
              break;
            case SerializedPropertyType.Vector3Int:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.vector3IntValue.GetHashCode());
              break;
            case SerializedPropertyType.Rect:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.rectValue.GetHashCode());
              break;
            case SerializedPropertyType.RectInt:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.rectIntValue.GetHashCode());
              break;
            case SerializedPropertyType.ArraySize:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue);
              break;
            case SerializedPropertyType.Character:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue.GetHashCode());
              break;
            case SerializedPropertyType.AnimationCurve:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.animationCurveValue.GetHashCode());
              break;
            case SerializedPropertyType.Bounds:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.boundsValue.GetHashCode());
              break;
            case SerializedPropertyType.BoundsInt:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.boundsIntValue.GetHashCode());
              break;
            case SerializedPropertyType.ExposedReference:
              hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.exposedReferenceValue.GetHashCode());
              break;
            default: {
              enterChildren = true;
              break;
            }
          }

          if (isFirst) {
            if (!enterChildren)
              // no traverse needed
            {
              return hashCode;
            }

            // since property is going to be traversed, a copy needs to be made
            p       = p.Copy();
            isFirst = false;
          }
        } while (p.Next(enterChildren) && p.depth >= minDepth);

        return hashCode;
      }
    }
    
    public struct SerializedPropertyEnumerable : IEnumerable<SerializedProperty> {
      private SerializedProperty property;
      private bool               visible;

      public SerializedPropertyEnumerable(SerializedProperty property, bool visible) {
        this.property = property;
        this.visible  = visible;
      }

      public SerializedPropertyEnumerator GetEnumerator() {
        return new SerializedPropertyEnumerator(property, visible);
      }

      IEnumerator<SerializedProperty> IEnumerable<SerializedProperty>.GetEnumerator() {
        return GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }
    }

    public struct SerializedPropertyEnumerator : IEnumerator<SerializedProperty> {
      private SerializedProperty current;
      private bool               enterChildren;
      private bool               visible;
      private int                parentDepth;

      public SerializedPropertyEnumerator(SerializedProperty parent, bool visible) {
        current       = parent.Copy();
        enterChildren = true;
        parentDepth   = parent.depth;
        this.visible  = visible;
      }

      public SerializedProperty Current => current;

      SerializedProperty IEnumerator<SerializedProperty>.Current => current;

      object IEnumerator.Current => current;

      public void Dispose() {
        current.Dispose();
      }

      public bool MoveNext() {
        bool entered = visible ? current.NextVisible(enterChildren) : current.Next(enterChildren);
        enterChildren = false;
        if (!entered) {
          return false;
        }
        if (current.depth <= parentDepth) {
          return false;
        }
        return true;
      }

      public void Reset() {
        throw new NotImplementedException();
      }
    }
    
    private static int[] _updateFixedBufferTemp = Array.Empty<int>();

    internal static bool UpdateFixedBuffer(this SerializedProperty sp, Action<int[], int> fill, Action<int[], int> update, bool write, bool force = false) {
      int count = sp.fixedBufferSize;
      Array.Resize(ref _updateFixedBufferTemp, Math.Max(_updateFixedBufferTemp.Length, count));

      // need to get to the first property... `GetFixedBufferElementAtIndex` is slow and allocates

      var element = sp.Copy();
      element.Next(true); // .Array
      element.Next(true); // .Array.size
      element.Next(true); // .Array.data[0]

      unsafe {
        fixed (int* p = _updateFixedBufferTemp) {
          Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemClear(p, count * sizeof(int));
        }

        fill(_updateFixedBufferTemp, count);

        int i = 0;
        if (!force) {
          // find the first difference
          for (; i < count; ++i, element.Next(true)) {
            FusionEditorLog.Assert(element.propertyType == SerializedPropertyType.Integer, "Invalid property type, expected integer");
            if (element.intValue != _updateFixedBufferTemp[i]) {
              break;
            }
          }
        }

        if (i < count) {
          // update data
          if (write) {
            for (; i < count; ++i, element.Next(true)) {
              element.intValue = _updateFixedBufferTemp[i];
            }
          } else {
            for (; i < count; ++i, element.Next(true)) {
              _updateFixedBufferTemp[i] = element.intValue;
            }
          }
          
          update(_updateFixedBufferTemp, count);
          return true;
        } else {
          return false;
        }
      }
    }
  }
}

#endregion


#region UnityInternal.cs

namespace Fusion.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;
  using static ReflectionUtils;


  static partial class UnityInternal {
    [UnityEditor.InitializeOnLoad]
    public static class Editor {
      public delegate bool DoDrawDefaultInspectorDelegate(SerializedObject obj);
      public delegate void BoolSetterDelegate(UnityEditor.Editor editor, bool value);
      
      public static readonly DoDrawDefaultInspectorDelegate DoDrawDefaultInspector = typeof(UnityEditor.Editor).CreateMethodDelegate<DoDrawDefaultInspectorDelegate>(nameof(DoDrawDefaultInspector));
      public static readonly BoolSetterDelegate             InternalSetHidden      = typeof(UnityEditor.Editor).CreateMethodDelegate<BoolSetterDelegate>(nameof(InternalSetHidden), BindingFlags.NonPublic | BindingFlags.Instance);
    }


    [UnityEditor.InitializeOnLoad]
    public static class EditorGUI {
      public delegate string DelayedTextFieldInternalDelegate(Rect position, int id, GUIContent label, string value, string allowedLetters, GUIStyle style);
      public delegate Rect   MultiFieldPrefixLabelDelegate(Rect totalPosition, int id, GUIContent label, int columns);
      public delegate string TextFieldInternalDelegate(int id, Rect position, string text, GUIStyle style);
      public delegate string ToolbarSearchFieldDelegate(int id, Rect position, string text, bool showWithPopupArrow);
      public delegate bool   DefaultPropertyFieldDelegate(Rect position, UnityEditor.SerializedProperty property, GUIContent label);
      
      
      public static readonly MultiFieldPrefixLabelDelegate    MultiFieldPrefixLabel    = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<MultiFieldPrefixLabelDelegate>(nameof(MultiFieldPrefixLabel));
      public static readonly TextFieldInternalDelegate        TextFieldInternal        = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<TextFieldInternalDelegate>(nameof(TextFieldInternal));
      public static readonly ToolbarSearchFieldDelegate       ToolbarSearchField       = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<ToolbarSearchFieldDelegate>(nameof(ToolbarSearchField));
      public static readonly DelayedTextFieldInternalDelegate DelayedTextFieldInternal = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<DelayedTextFieldInternalDelegate>(nameof(DelayedTextFieldInternal));
      public static readonly DefaultPropertyFieldDelegate     DefaultPropertyField     = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<DefaultPropertyFieldDelegate>(nameof(DefaultPropertyField));
      
      private static readonly FieldInfo             s_TextFieldHash           = typeof(UnityEditor.EditorGUI).GetFieldOrThrow(nameof(s_TextFieldHash));
      private static readonly FieldInfo             s_DelayedTextFieldHash    = typeof(UnityEditor.EditorGUI).GetFieldOrThrow(nameof(s_DelayedTextFieldHash));
      private static readonly StaticAccessor<float> s_indent                  = typeof(UnityEditor.EditorGUI).CreateStaticPropertyAccessor<float>(nameof(indent));
      public static readonly  Action                EndEditingActiveTextField = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<Action>(nameof(EndEditingActiveTextField));
      
      public static   int   TextFieldHash        => (int)s_TextFieldHash.GetValue(null);
      public static   int   DelayedTextFieldHash => (int)s_DelayedTextFieldHash.GetValue(null);
      internal static float indent               => s_indent.GetValue();
    }
    
    [UnityEditor.InitializeOnLoad]
    public static class EditorUtility {
      public delegate void DisplayCustomMenuDelegate(Rect position, string[] options, int[] selected, UnityEditor.EditorUtility.SelectMenuItemFunction callback, object userData);

      public static DisplayCustomMenuDelegate DisplayCustomMenu = typeof(UnityEditor.EditorUtility).CreateMethodDelegate<DisplayCustomMenuDelegate>(nameof(DisplayCustomMenu), BindingFlags.NonPublic | BindingFlags.Static);
    }

    [UnityEditor.InitializeOnLoad]
    public static class HandleUtility {
      public static readonly Action ApplyWireMaterial = typeof(UnityEditor.HandleUtility).CreateMethodDelegate<Action>(nameof(ApplyWireMaterial));
    }


    [UnityEditor.InitializeOnLoad]
    public static class LayerMatrixGUI {

      private static readonly Type InternalType             = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LayerMatrixGUI", true);
      private static readonly Type InternalGetValueFuncType = InternalType.GetNestedTypeOrThrow(nameof(GetValueFunc), BindingFlags.Public);
      private static readonly Type InternalSetValueFuncType = InternalType.GetNestedTypeOrThrow(nameof(SetValueFunc), BindingFlags.Public);
      
      private static readonly Delegate _doGUI = InternalType.CreateMethodDelegate(nameof(DoGUI), BindingFlags.Public | BindingFlags.Static,
        typeof(Ref2Action<,,,>).MakeGenericType(
          typeof(GUIContent), typeof(bool), InternalGetValueFuncType, InternalSetValueFuncType)
      );

      private delegate void Ref2Action<T1, T2, T3, T4>(T1 t1, ref T2 t2, T3 t3, T4 t4);

      public delegate bool GetValueFunc(int layerA, int layerB);

      public delegate void SetValueFunc(int layerA, int layerB, bool val);

      public static void DoGUI(string title, ref bool show, GetValueFunc getValue, SetValueFunc setValue) {
        var getter = Delegate.CreateDelegate(InternalGetValueFuncType, getValue.Target, getValue.Method);
        var setter = Delegate.CreateDelegate(InternalSetValueFuncType, setValue.Target, setValue.Method);

        var args = new object[] { title, show, getter, setter };
        args[0] = new GUIContent(title);
        _doGUI.DynamicInvoke(args);
        show = (bool)args[1];
      }
    }


    [UnityEditor.InitializeOnLoad]
    public static class DecoratorDrawer {
      private static InstanceAccessor<PropertyAttribute> m_Attribute = typeof(UnityEditor.DecoratorDrawer).CreateFieldAccessor<PropertyAttribute>(nameof(m_Attribute));

      public static void SetAttribute(UnityEditor.DecoratorDrawer drawer, PropertyAttribute attribute) {
        m_Attribute.SetValue(drawer, attribute);
      }
    }

    [UnityEditor.InitializeOnLoad]
    public static class PropertyDrawer {
      private static InstanceAccessor<PropertyAttribute> m_Attribute = typeof(UnityEditor.PropertyDrawer).CreateFieldAccessor<PropertyAttribute>(nameof(m_Attribute));
      private static InstanceAccessor<FieldInfo>         m_FieldInfo = typeof(UnityEditor.PropertyDrawer).CreateFieldAccessor<FieldInfo>(nameof(m_FieldInfo));

      public static void SetAttribute(UnityEditor.PropertyDrawer drawer, PropertyAttribute attribute) {
        m_Attribute.SetValue(drawer, attribute);
      }

      public static void SetFieldInfo(UnityEditor.PropertyDrawer drawer, FieldInfo fieldInfo) {
        m_FieldInfo.SetValue(drawer, fieldInfo);
      }
    }

    [UnityEditor.InitializeOnLoad]
    public static class EditorGUIUtility {
      private static readonly StaticAccessor<int> s_LastControlID = typeof(UnityEditor.EditorGUIUtility).CreateStaticFieldAccessor<int>(nameof(s_LastControlID));

      private static readonly StaticAccessor<float> _contentWidth = typeof(UnityEditor.EditorGUIUtility).CreateStaticPropertyAccessor<float>(nameof(contextWidth));
      public static           int                   LastControlID => s_LastControlID.GetValue();
      public static           float                 contextWidth  => _contentWidth.GetValue();
      
      public delegate UnityEngine.Object GetScriptDelegate(string scriptClass);
      public delegate Texture2D          GetIconForObjectDelegate(UnityEngine.Object obj);
      public delegate GUIContent         TempContentDelegate(string text);
      public delegate Texture2D          GetHelpIconDelegate(MessageType type);
      
      public static readonly GetScriptDelegate        GetScript        = typeof(UnityEditor.EditorGUIUtility).CreateMethodDelegate<GetScriptDelegate>(nameof(GetScript));
      public static readonly GetIconForObjectDelegate GetIconForObject = typeof(UnityEditor.EditorGUIUtility).CreateMethodDelegate<GetIconForObjectDelegate>(nameof(GetIconForObject));
      public static readonly TempContentDelegate      TempContent      = typeof(UnityEditor.EditorGUIUtility).CreateMethodDelegate<TempContentDelegate>(nameof(TempContent));
      public static readonly GetHelpIconDelegate      GetHelpIcon      = typeof(UnityEditor.EditorGUIUtility).CreateMethodDelegate<GetHelpIconDelegate>(nameof(GetHelpIcon));
    }

    [UnityEditor.InitializeOnLoad]
    public static class ScriptAttributeUtility {
      public delegate Type GetDrawerTypeForTypeDelegate(Type type);

      public delegate List<PropertyAttribute> GetFieldAttributesDelegate(FieldInfo field);

      public delegate FieldInfo GetFieldInfoFromPropertyDelegate(SerializedProperty property, out Type type);

      public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ScriptAttributeUtility", true);

      public static readonly GetFieldInfoFromPropertyDelegate GetFieldInfoFromProperty =
        CreateEditorMethodDelegate<GetFieldInfoFromPropertyDelegate>(
          "UnityEditor.ScriptAttributeUtility",
          "GetFieldInfoFromProperty",
          BindingFlags.Static | BindingFlags.NonPublic);

      public static readonly GetDrawerTypeForTypeDelegate GetDrawerTypeForType =
        CreateEditorMethodDelegate<GetDrawerTypeForTypeDelegate>(
          "UnityEditor.ScriptAttributeUtility",
          "GetDrawerTypeForType",
          BindingFlags.Static | BindingFlags.NonPublic);

      private static readonly GetHandlerDelegate _GetHandler = InternalType.CreateMethodDelegate<GetHandlerDelegate>("GetHandler", BindingFlags.NonPublic | BindingFlags.Static,
        MakeFuncType(typeof(SerializedProperty), PropertyHandler.InternalType)
      );

      public static readonly GetFieldAttributesDelegate GetFieldAttributes = InternalType.CreateMethodDelegate<GetFieldAttributesDelegate>(nameof(GetFieldAttributes));

      private static readonly StaticAccessor<object> _propertyHandlerCache = InternalType.CreateStaticPropertyAccessor(nameof(propertyHandlerCache), PropertyHandlerCache.InternalType);

      private static readonly StaticAccessor<object> s_SharedNullHandler = InternalType.CreateStaticFieldAccessor("s_SharedNullHandler", PropertyHandler.InternalType);
      private static readonly StaticAccessor<object> s_NextHandler       = InternalType.CreateStaticFieldAccessor("s_NextHandler", PropertyHandler.InternalType);

      public static PropertyHandlerCache propertyHandlerCache => new() {
        _instance = _propertyHandlerCache.GetValue()
      };

      public static PropertyHandler sharedNullHandler => PropertyHandler.Wrap(s_SharedNullHandler.GetValue());
      public static PropertyHandler nextHandler => PropertyHandler.Wrap(s_NextHandler.GetValue());
      
      public static PropertyHandler GetHandler(SerializedProperty property) {
        return PropertyHandler.Wrap(_GetHandler(property));
      }

      private delegate object GetHandlerDelegate(SerializedProperty property);
    }

    public struct PropertyHandlerCache {
      [UnityEditor.InitializeOnLoad]
      private static class Statics {
        public static readonly Type                    InternalType    = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyHandlerCache", true);
        public static readonly GetPropertyHashDelegate GetPropertyHash = InternalType.CreateMethodDelegate<GetPropertyHashDelegate>(nameof(GetPropertyHash));

        public static readonly GetHandlerDelegate GetHandler = InternalType.CreateMethodDelegate<GetHandlerDelegate>(nameof(GetHandler), BindingFlags.NonPublic | BindingFlags.Instance,
          MakeFuncType(InternalType, typeof(SerializedProperty), PropertyHandler.InternalType));

        public static readonly SetHandlerDelegate SetHandler = InternalType.CreateMethodDelegate<SetHandlerDelegate>(nameof(SetHandler), BindingFlags.NonPublic | BindingFlags.Instance,
          MakeActionType(InternalType, typeof(SerializedProperty), PropertyHandler.InternalType));
        
        public static readonly FieldInfo m_PropertyHandlers = InternalType.GetFieldOrThrow(nameof(m_PropertyHandlers));
      }

      public static Type InternalType => Statics.InternalType;

      public delegate int GetPropertyHashDelegate(SerializedProperty property);

      public delegate object GetHandlerDelegate(object instance, SerializedProperty property);

      public delegate void SetHandlerDelegate(object instance, SerializedProperty property, object handlerInstance);

      public object _instance;

      public PropertyHandler GetHandler(SerializedProperty property) {
        return new PropertyHandler {
          _instance = Statics.GetHandler(_instance, property)
        };
      }

      public void SetHandler(SerializedProperty property, PropertyHandler newHandler) {
        Statics.SetHandler(_instance, property, newHandler._instance);
      }

      public IEnumerable<(int, PropertyHandler)> PropertyHandlers {
        get {
          var dict = (IDictionary)Statics.m_PropertyHandlers.GetValue(_instance);
          foreach (DictionaryEntry entry in dict) {
            yield return ((int)entry.Key, PropertyHandler.Wrap(entry.Value));
          }
        }
      }
    }

    public struct PropertyHandler : IEquatable<PropertyHandler> {
      [UnityEditor.InitializeOnLoad]
      private static class Statics {
        public static readonly Type                                                InternalType       = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyHandler", true);
        public static readonly InstanceAccessor<List<UnityEditor.DecoratorDrawer>> m_DecoratorDrawers = InternalType.CreateFieldAccessor<List<UnityEditor.DecoratorDrawer>>(nameof(m_DecoratorDrawers));
        public static readonly InstanceAccessor<List<UnityEditor.PropertyDrawer>> m_PropertyDrawers = InternalType.CreateFieldAccessor<List<UnityEditor.PropertyDrawer>>(nameof(m_PropertyDrawers));
      }


      public static Type InternalType => Statics.InternalType;

      public object _instance;

      internal static PropertyHandler Wrap(object instance) {
        return new() {
          _instance = instance
        };
      }

      public static PropertyHandler New() {
        return Wrap(Activator.CreateInstance(InternalType));
      }
      
      public List<UnityEditor.PropertyDrawer> m_PropertyDrawers {
        get => Statics.m_PropertyDrawers.GetValue(_instance);
        set => Statics.m_PropertyDrawers.SetValue(_instance, value);
      }

      public bool Equals(PropertyHandler other) {
        return _instance == other._instance;
      }

      public override int GetHashCode() {
        return _instance?.GetHashCode() ?? 0;
      }

      public override bool Equals(object obj) {
        return obj is PropertyHandler h ? Equals(h) : false;
      }

      public List<UnityEditor.DecoratorDrawer> decoratorDrawers {
        get => Statics.m_DecoratorDrawers.GetValue(_instance);
        set => Statics.m_DecoratorDrawers.SetValue(_instance, value);
      }
    }

    [UnityEditor.InitializeOnLoad]
    public static class EditorApplication {
      public static readonly Action Internal_CallAssetLabelsHaveChanged = typeof(UnityEditor.EditorApplication).CreateMethodDelegate<Action>(nameof(Internal_CallAssetLabelsHaveChanged));
    }

    public struct ObjectSelector {
      [UnityEditor.InitializeOnLoad]
      private static class Statics {
        public static readonly Type                         InternalType  = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ObjectSelector", true);
        public static readonly StaticAccessor<bool>         _tooltip      = InternalType.CreateStaticPropertyAccessor<bool>(nameof(isVisible));
        public static readonly StaticAccessor<EditorWindow> _get          = InternalType.CreateStaticPropertyAccessor<EditorWindow>(nameof(get), InternalType);
        public static readonly InstanceAccessor<string>     _searchFilter = InternalType.CreatePropertyAccessor<string>(nameof(searchFilter));
      }

      private EditorWindow _instance;

      public static bool isVisible => Statics._tooltip.GetValue();

      public static ObjectSelector get => new() {
        _instance = Statics._get.GetValue()
      };

      public string searchFilter {
        get => Statics._searchFilter.GetValue(_instance);
        set => Statics._searchFilter.SetValue(_instance, value);
      }

      private static readonly InstanceAccessor<int> _objectSelectorID = Statics.InternalType.CreateFieldAccessor<int>(nameof(objectSelectorID));
      public                  int                   objectSelectorID => _objectSelectorID.GetValue(_instance);
    }

    [UnityEditor.InitializeOnLoad]
    public class InspectorWindow {
      public static readonly Type                   InternalType      = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow", true);
      public static readonly InstanceAccessor<bool> _isLockedAccessor = InternalType.CreatePropertyAccessor<bool>(nameof(isLocked));

      private readonly EditorWindow _instance;

      public InspectorWindow(EditorWindow instance) {
        if (instance == null) {
          throw new ArgumentNullException(nameof(instance));
        }

        _instance = instance;
      }

      public bool isLocked {
        get => _isLockedAccessor.GetValue(_instance);
        set => _isLockedAccessor.SetValue(_instance, value);
      }
    }
    
    [UnityEditor.InitializeOnLoad]
    public static class SplitterGUILayout {
      public static readonly Action EndHorizontalSplit = CreateMethodDelegate<Action>(typeof(UnityEditor.Editor).Assembly,
        "UnityEditor.SplitterGUILayout", "EndHorizontalSplit", BindingFlags.Public | BindingFlags.Static
      );

      public static readonly Action EndVerticalSplit = CreateMethodDelegate<Action>(typeof(UnityEditor.Editor).Assembly,
        "UnityEditor.SplitterGUILayout", "EndVerticalSplit", BindingFlags.Public | BindingFlags.Static
      );

      public static void BeginHorizontalSplit(SplitterState splitterState, GUIStyle style, params GUILayoutOption[] options) {
        _beginHorizontalSplit.DynamicInvoke(splitterState.InternalState, style, options);
      }

      public static void BeginVerticalSplit(SplitterState splitterState, GUIStyle style, params GUILayoutOption[] options) {
        _beginVerticalSplit.DynamicInvoke(splitterState.InternalState, style, options);
      }

      private static readonly Delegate _beginHorizontalSplit = CreateMethodDelegate(typeof(UnityEditor.Editor).Assembly,
        "UnityEditor.SplitterGUILayout", "BeginHorizontalSplit", BindingFlags.Public | BindingFlags.Static,
        typeof(Action<,,>).MakeGenericType(SplitterState.InternalType, typeof(GUIStyle), typeof(GUILayoutOption[]))
      );

      private static readonly Delegate _beginVerticalSplit = CreateMethodDelegate(typeof(UnityEditor.Editor).Assembly,
        "UnityEditor.SplitterGUILayout", "BeginVerticalSplit", BindingFlags.Public | BindingFlags.Static,
        typeof(Action<,,>).MakeGenericType(SplitterState.InternalType, typeof(GUIStyle), typeof(GUILayoutOption[]))
      );
    }

    [UnityEditor.InitializeOnLoad]
    [Serializable]
    public class SplitterState : ISerializationCallbackReceiver {

      public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.SplitterState", true);
      private static readonly FieldInfo _relativeSizes = InternalType.GetFieldOrThrow("relativeSizes");
      private static readonly FieldInfo _realSizes = InternalType.GetFieldOrThrow("realSizes");
      private static readonly FieldInfo _splitSize = InternalType.GetFieldOrThrow("splitSize");

      public string Json = "{}";

      [NonSerialized]
      public object InternalState = FromRelativeInner(new[] { 1.0f });

      void ISerializationCallbackReceiver.OnAfterDeserialize() {
        InternalState = JsonUtility.FromJson(Json, InternalType);
      }

      void ISerializationCallbackReceiver.OnBeforeSerialize() {
        Json = JsonUtility.ToJson(InternalState);
      }

      public static SplitterState FromRelative(float[] relativeSizes, int[] minSizes = null, int[] maxSizes = null, int splitSize = 0) {
        var result = new SplitterState();
        result.InternalState = FromRelativeInner(relativeSizes, minSizes, maxSizes, splitSize);
        return result;
      }


      private static object FromRelativeInner(float[] relativeSizes, int[] minSizes = null, int[] maxSizes = null, int splitSize = 0) {
        return Activator.CreateInstance(InternalType, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
          null,
          new object[] { relativeSizes, minSizes, maxSizes, splitSize },
          null, null);
      }

      public float[] realSizes => ConvertArray((Array)_realSizes.GetValue(InternalState));
      public float[] relativeSizes => ConvertArray((Array)_relativeSizes.GetValue(InternalState));
      public float splitSize => Convert.ToSingle(_splitSize.GetValue(InternalState));

      private static float[] ConvertArray(Array value) {
        float[] result = new float[value.Length];
        for (int i = 0; i < value.Length; ++i) {
          result[i] = Convert.ToSingle(value.GetValue(i));
        }
        return result;
      }
    }
    
    public sealed class InternalStyles {
      public static InternalStyles Instance = new InternalStyles();
      
      public LazyGUIStyle InspectorTitlebar                => LazyGUIStyle.Create(_ => GetStyle("IN Title"));
      public LazyGUIStyle FoldoutTitlebar                  => LazyGUIStyle.Create(_ => GetStyle("Titlebar Foldout", "Foldout"));
      public LazyGUIStyle BoxWithBorders                   => LazyGUIStyle.Create(_ => GetStyle("OL Box"));
      public LazyGUIStyle HierarchyTreeViewLine            => LazyGUIStyle.Create(_ => GetStyle("TV Line"));
      public LazyGUIStyle HierarchyTreeViewSceneBackground => LazyGUIStyle.Create(_ => GetStyle("SceneTopBarBg", "ProjectBrowserTopBarBg"));
      public LazyGUIStyle OptionsButtonStyle               => LazyGUIStyle.Create(_ => GetStyle("PaneOptions"));
      public LazyGUIStyle AddComponentButton               => LazyGUIStyle.Create(_ => GetStyle("AC Button"));
      public LazyGUIStyle AnimationEventTooltip            => LazyGUIStyle.Create(_ => GetStyle("AnimationEventTooltip"));
      public LazyGUIStyle AnimationEventTooltipArrow       => LazyGUIStyle.Create(_ => GetStyle("AnimationEventTooltipArrow"));
      
      private static GUIStyle GetStyle(params string[] names) {
        var skin = GUI.skin;

        foreach (var name in names) {
          var result = skin.FindStyle(name);
          if (result != null) {
            return result;
          }
        }

        throw new ArgumentOutOfRangeException($"Style not found: {string.Join(", ", names)}", nameof(names));
      }
    }
    
    public static InternalStyles Styles => InternalStyles.Instance;
  }
}

#endregion


#region ArrayLengthAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class ArrayLengthAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {

    private GUIStyle _style;

    private GUIStyle GetStyle() {
      if (_style == null) {
        _style                  = new GUIStyle(EditorStyles.miniLabel);
        _style.alignment        = TextAnchor.MiddleRight;
        _style.contentOffset    = new Vector2(-2, 0);
        _style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(255f / 255f, 221 / 255f, 0 / 255f, 1f) : Color.blue;
      }

      return _style;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      base.OnGUIInternal(position, property, label);
      if (!property.isArray) {
        return;
      }
      
      var overlayRect = position;
      overlayRect.height = EditorGUIUtility.singleLineHeight;

      var attrib = (ArrayLengthAttribute)attribute;

      // draw length overlay
      GUI.Label(overlayRect, $"[{attrib.MaxLength}]", GetStyle());

      if (property.arraySize > attrib.MaxLength) {
        property.arraySize = attrib.MaxLength;
        property.serializedObject.ApplyModifiedProperties();
      } else if (property.arraySize < attrib.MinLength) {
        property.arraySize = attrib.MinLength;
        property.serializedObject.ApplyModifiedProperties();
      }
    }
  }
  
  [CustomPropertyDrawer(typeof(ArrayLengthAttribute))]
  [RedirectCustomPropertyDrawer(typeof(ArrayLengthAttribute), typeof(ArrayLengthAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region AssemblyNameAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssemblyNameAttribute))]
  internal class AssemblyNameAttributeDrawer : PropertyDrawerWithErrorHandling {
    const float DropdownWidth = 20.0f;

    static GUIContent DropdownContent = new GUIContent("");

    string _lastCheckedAssemblyName;

    [Flags]
    enum AsmDefType {
      Predefined = 1 << 0,
      InPackages = 1 << 1,
      InAssets   = 1 << 2,
      Editor     = 1 << 3,
      Runtime    = 1 << 4,
      All        = Predefined | InPackages | InAssets | Editor | Runtime,
    }

    HashSet<string> _allAssemblies;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var  assemblyName = property.stringValue;
      bool notFound     = false;
      
      if (!string.IsNullOrEmpty(assemblyName)) {
        if (_allAssemblies == null) {
          _allAssemblies = new HashSet<string>(GetAssemblies(AsmDefType.All), StringComparer.OrdinalIgnoreCase);
        }

        if (!_allAssemblies.Contains(assemblyName, StringComparer.OrdinalIgnoreCase)) {
          SetInfo($"Assembly not found: {assemblyName}");
          notFound = true;
        }
      }

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.BeginChangeCheck();

        assemblyName = EditorGUI.TextField(new Rect(position) { xMax = position.xMax - DropdownWidth }, 
          label, 
          assemblyName,
          notFound ? 
            new GUIStyle(EditorStyles.textField) {
              fontStyle = FontStyle.Italic, 
              normal    = new GUIStyleState() { textColor = Color.gray }
            } : EditorStyles.textField
        );

        var dropdownRect = EditorGUI.IndentedRect(new Rect(position) {
          xMin = position.xMax - DropdownWidth
        });

        if (EditorGUI.DropdownButton(dropdownRect, DropdownContent, FocusType.Passive)) {
          GenericMenu.MenuFunction2 onClicked = (userData) => {
            property.stringValue = (string)userData;
            property.serializedObject.ApplyModifiedProperties();
            UnityInternal.EditorGUI.EndEditingActiveTextField();
            ClearError(property);
          };

          var menu = new GenericMenu();

          foreach (var (flag, prefix) in new[] {
                     (AsmDefType.Editor, "Editor/"),
                     (AsmDefType.Runtime, "")
                   }) {
            if (menu.GetItemCount() != 0) {
              menu.AddSeparator(prefix);
            }

            foreach (var name in GetAssemblies(flag | AsmDefType.InPackages)) {
              menu.AddItem(new GUIContent($"{prefix}Packages/{name}"), string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase), onClicked, name);
            }

            menu.AddSeparator(prefix);

            foreach (var name in GetAssemblies(flag | AsmDefType.InAssets | AsmDefType.Predefined)) {
              menu.AddItem(new GUIContent($"{prefix}{name}"), string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase), onClicked, name);
            }
          }

          menu.DropDown(dropdownRect);
        }

        if (EditorGUI.EndChangeCheck()) {
          property.stringValue = assemblyName;
          property.serializedObject.ApplyModifiedProperties();
          base.ClearError();
        }
      }
    }

    static IEnumerable<string> GetAssemblies(AsmDefType types) {
      IEnumerable<string> query = Enumerable.Empty<string>();

      if (types.HasFlag(AsmDefType.Predefined)) {
        if (types.HasFlag(AsmDefType.Runtime)) {
          query = query.Concat(new[] {
            "Assembly-CSharp-firstpass",
            "Assembly-CSharp"
          });
        }

        if (types.HasFlag(AsmDefType.Editor)) {
          query = query.Concat(new[] {
            "Assembly-CSharp-Editor-firstpass",
            "Assembly-CSharp-Editor"
          });
        }
      }

      if (types.HasFlag(AsmDefType.InAssets) || types.HasFlag(AsmDefType.InPackages)) {
        query = query.Concat(
          AssetDatabase.FindAssets("t:asmdef")
           .Select(x => AssetDatabase.GUIDToAssetPath(x))
           .Where(x => {
              if (types.HasFlag(AsmDefType.InAssets) && x.StartsWith("Assets/")) {
                return true;
              } else if (types.HasFlag(AsmDefType.InPackages) && x.StartsWith("Packages/")) {
                return true;
              } else {
                return false;
              }
            })
           .Select(x => JsonUtility.FromJson<AsmDefData>(File.ReadAllText(x)))
           .Where(x => {
              bool editorOnly = x.includePlatforms.Length == 1 && x.includePlatforms[0] == "Editor";
              if (types.HasFlag(AsmDefType.Runtime) && !editorOnly) {
                return true;
              } else if (types.HasFlag(AsmDefType.Editor) && editorOnly) {
                return true;
              } else {
                return false;
              }
            })
           .Select(x => x.name)
           .Distinct()
        );
      }

      return query;
    }

    [Serializable]
    private class AsmDefData {
      public string[] includePlatforms = Array.Empty<string>();
      public string   name             = string.Empty;
    }
  }
}

#endregion


#region BinaryDataAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Text;
  using UnityEngine;
  using UnityEditor;
  
  internal partial class BinaryDataAttributeDrawer : PropertyDrawerWithErrorHandling, INonApplicableOnArrayElements {
    
    private int           MaxLength = 2048;
    private int           MaxLines  = 16;
    private RawDataDrawer _drawer   = new RawDataDrawer();

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        bool wasExpanded = property.isExpanded;
        
        var foldoutPosition = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
        property.isExpanded = EditorGUI.Foldout(foldoutPosition, property.isExpanded, label);

        if (property.hasMultipleDifferentValues) {
          FusionEditorGUI.Overlay(foldoutPosition, $"---");
        } else {
          FusionEditorGUI.Overlay(foldoutPosition, $"{property.arraySize}");
        }

        if (!wasExpanded) {
          return;
        }
        
        position.yMin += foldoutPosition.height + EditorGUIUtility.standardVerticalSpacing;
        using (new FusionEditorGUI.EnabledScope(true)) {
          _drawer.Draw(GUIContent.none, position);
        }
      }
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      if (!property.isExpanded) {
        return EditorGUIUtility.singleLineHeight;
      }
      
      _drawer.Refresh(property);

      // space for scrollbar and indent
      var width  = UnityInternal.EditorGUIUtility.contextWidth - 32.0f;
      var height = _drawer.GetHeight(width);
      
      return EditorGUIUtility.singleLineHeight +
        EditorGUIUtility.standardVerticalSpacing +
        Mathf.Min(FusionEditorGUI.GetLinesHeight(MaxLines), height);
    }
  }
  
    
  [CustomPropertyDrawer(typeof(BinaryDataAttribute))]
  [RedirectCustomPropertyDrawer(typeof(BinaryDataAttribute), typeof(BinaryDataAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region BitSetAttributeDrawer.cs

// namespace Fusion.Editor {
//   using System;
//   using UnityEditor;
//   using UnityEngine;
//
//   [CustomPropertyDrawer(typeof(BitSetAttribute))]
//   public class BitSetAttributeDrawer : PropertyDrawer {
//     
//     public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
//
//       if (property.IsArrayElement()) {
//         throw new NotSupportedException();
//       }
//
//       var longValue = property.longValue;
//
//       int bitStart = 0;
//       int bitEnd   = ((BitSetAttribute)attribute).BitCount;
//
//       using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out var valueRect)) {
//         var pos = valueRect;
//
//         DrawAndMeasureLabel(valueRect, bitStart, FusionEditorSkin.instance.MiniLabelLowerRight);
//         DrawAndMeasureLabel(valueRect, bitEnd, FusionEditorSkin.instance.MiniLabelLowerLeft);
//         
//         var tmpContent = new GUIContent();
//         tmpContent.text = $"{bitStart}";
//         var bitStartSize = EditorStyles.miniLabel.CalcSize(tmpContent);
//         
//         
//         tmpContent.text = $"{bitEnd}";
//         var bitEndSize = EditorStyles.miniLabel.CalcSize(tmpContent);
//         valueRect.width = bitEndSize.x;
//         GUI.Label(valueRect, tmpContent, EditorStyles.miniLabel);
//         valueRect.x += bitEndSize.x;
//         var availableWidth = valueRect.width - bitStartSize.x - bitEndSize.x;
//         
//         
//         // how may per one line?
//         const float ToggleWidth = 15.0f;
//         
//         valueRect.width = ToggleWidth;
//         for (int i = 0; i < 16; ++i) {
//           EditorGUI.Toggle(valueRect, false);
//           valueRect.x += ToggleWidth;
//         }  
//       }
//
//       float DrawAndMeasureLabel(Rect position, int label, GUIStyle style) {
//         var tmpContent = new GUIContent($"{bitEnd}");
//         var contentSize = style.CalcSize(tmpContent);
//         GUI.Label(position, tmpContent, style);
//         return contentSize.x;
//       }
//       
//       //base.OnGUI(position, property, label);
//     }
//   }
// }

#endregion


#region DecoratingPropertyAttributeDrawer.cs

//#define FUSION_EDITOR_TRACE
namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  
  public abstract class DecoratingPropertyAttributeDrawer : PropertyDrawer {
    private bool _isLastDrawer;
    private int _nestingLevel;
    
    /// <summary>
    ///   The drawer that's been chosen by Unity; its job is to
    ///   iterate all ForwardingPropertyDrawerBase drawers
    ///   that'd be created had Unity 2020.3 supported multiple
    ///   property drawers - including self.
    /// </summary>
    protected DecoratingPropertyAttributeDrawer MainDrawer { get; private set; }

    public List<DecoratingPropertyAttributeDrawer> PropertyDrawers { get; private set; }

    public PropertyDrawer NextDrawer { get; private set; }

    public DecoratingPropertyAttributeDrawer() {
      TraceField("constructor");
    }
    
    [Obsolete("Derived classes should override and call OnGUIInternal", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
      TraceField($"OnGUI({position}, {property.propertyPath}, {label})");
      EnsureInitialized(property);
      FusionEditorLog.Assert(MainDrawer == this);
      FusionEditorLog.Assert(PropertyDrawers != null);
      FusionEditorLog.Assert(PropertyDrawers.Count > 0);
      PropertyDrawers[0].InvokeOnGUIInternal(position, property, label);
    }

    [Obsolete("Derived classes should override and call GetPropertyHeightInternal", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public sealed override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
      TraceField($"GetPropertyHeight({property.propertyPath}, {label})");
      EnsureInitialized(property);
      FusionEditorLog.Assert(MainDrawer == this);
      FusionEditorLog.Assert(PropertyDrawers != null);
      FusionEditorLog.Assert(PropertyDrawers.Count > 0);
      return PropertyDrawers[0].InvokeGetPropertyHeightInternal(property, label);
    }

    protected virtual float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      FusionEditorLog.Assert(MainDrawer != null);
      return MainDrawer.InvokeGetPropertyHeightOnNextDrawer(this, property, label);
    }

    protected virtual void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      TraceField($"OnGUIInternal({position}, {property.propertyPath}, {label})");
      FusionEditorLog.Assert(MainDrawer != null);
      
      FusionEditorLog.Assert(_nestingLevel == 0, $"{property.propertyPath} {GetType().FullName}");
      _nestingLevel++;
      try {
        MainDrawer.InvokeOnGUIOnNextDrawer(this, position, property, label);
      } finally {
        _nestingLevel--;
      }
    }

    private void InvokeOnGUIOnNextDrawer(DecoratingPropertyAttributeDrawer current, Rect position, SerializedProperty prop, GUIContent label) {
      FusionEditorLog.Assert(MainDrawer == this);
      var index = PropertyDrawers.IndexOf(current);
      if (index < PropertyDrawers.Count - 1) {
        PropertyDrawers[index + 1].InvokeOnGUIInternal(position, prop, label);
      } else {
        if (NextDrawer != null) {
          NextDrawer.OnGUI(position, prop, label);
        } else {
          FusionEditorGUI.ForwardPropertyField(position, prop, label, prop.IsArrayProperty() ? true : prop.isExpanded, _isLastDrawer);
        }
      }
    }

    private void InvokeOnGUIInternal(Rect position, SerializedProperty prop, GUIContent label) {
      if (prop.IsArrayElement() && this is INonApplicableOnArrayElements) {
        MainDrawer.InvokeOnGUIOnNextDrawer(this, position, prop, label);
      } else {
        OnGUIInternal(position, prop, label);
      }
    }

    private float InvokeGetPropertyHeightOnNextDrawer(DecoratingPropertyAttributeDrawer current, SerializedProperty prop, GUIContent label) {
      FusionEditorLog.Assert(MainDrawer == this);
      var index = PropertyDrawers.IndexOf(current);
      if (index < PropertyDrawers.Count - 1) {
        return PropertyDrawers[index + 1].InvokeGetPropertyHeightInternal(prop, label);
      }

      return NextDrawer?.GetPropertyHeight(prop, label) ?? EditorGUI.GetPropertyHeight(prop, label);
    }

    private float InvokeGetPropertyHeightInternal(SerializedProperty prop, GUIContent label) {
      if (prop.IsArrayElement() && this is INonApplicableOnArrayElements) {
        return MainDrawer.InvokeGetPropertyHeightOnNextDrawer(this, prop, label);
      } else {
        return GetPropertyHeightInternal(prop, label);
      }
    }
    
    protected virtual bool EnsureInitialized(SerializedProperty property) {
      if (MainDrawer != null || PropertyDrawers != null) {
        return false;
      }

      if (fieldInfo == null) {
        // this might happen if this drawer is created dynamically
        var field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out _);
        FusionEditorLog.Assert(field != null, $"Could not find field for property {property.propertyPath} of type {property.serializedObject.targetObject.GetType().FullName} (I'm {GetType().FullName} {GetHashCode()})");
        UnityInternal.PropertyDrawer.SetFieldInfo(this, field);
      }
      
      FusionEditorLog.Assert(attribute != null);
      FusionEditorLog.Assert(attribute is DecoratingPropertyAttribute, $"Expected attribute to be of type {nameof(DecoratingPropertyAttribute)} but it's {attribute.GetType().FullName}");

      PropertyDrawers = new List<DecoratingPropertyAttributeDrawer>();
      MainDrawer     = this;
      NextDrawer      = null;
      
      var isLastDrawer = false;
      var foundSelf    = false;

      var fieldAttributes = fieldInfo != null ? UnityInternal.ScriptAttributeUtility.GetFieldAttributes(fieldInfo) : null;

      if (fieldAttributes != null) {
        FusionEditorLog.Assert(fieldAttributes.OrderBy(x => x.order).SequenceEqual(fieldAttributes), "Expected field attributes to be sorted");
        FusionEditorLog.Assert(fieldAttributes.Count > 0);

        for (var i = 0; i < fieldAttributes.Count; ++i) {
          var fieldAttribute = fieldAttributes[i];

          var attributeDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(fieldAttribute.GetType());
          if (attributeDrawerType == null) {
            TraceField($"No drawer for {attributeDrawerType}");
            continue;
          }
          
          if (attributeDrawerType == typeof(PropertyDrawerForArrayWorkaround)) {
            attributeDrawerType = PropertyDrawerForArrayWorkaround.GetDrawerType(fieldAttribute.GetType());
          }
          
          if (attributeDrawerType.IsSubclassOf(typeof(DecoratorDrawer))) {
            // decorators are their own thing
            continue;
          }
          
          if (property.IsArrayElement() && attributeDrawerType.GetInterface(typeof(INonApplicableOnArrayElements).FullName) != null) {
            // skip drawers that are not meant to be used on array elements
            continue;
          }

          FusionEditorLog.Assert(attributeDrawerType.IsSubclassOf(typeof(PropertyDrawer)));

          if (fieldAttribute.Equals(attribute)) {
            // self
            PropertyDrawers.Add(this);
            FusionEditorLog.Assert(foundSelf == false);
            foundSelf    = true;
            isLastDrawer = true;
            TraceField($"Found self at {i} ({this})");
            continue;
          }

          isLastDrawer = false;
        }
      }

      if (!foundSelf) {
        TraceField("Force-adding self");
        PropertyDrawers.Add(this);
      }

      if (NextDrawer == null && isLastDrawer && fieldInfo != null) {
        // try creating type drawer instead
        var fieldType      = fieldInfo.FieldType;
        if (property.IsArrayElement()) {
          fieldType = fieldType.GetUnityLeafType();
        }
        
        var typeDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(fieldType);
        if (typeDrawerType != null) {
          var drawer = (PropertyDrawer)Activator.CreateInstance(typeDrawerType);
          UnityInternal.PropertyDrawer.SetFieldInfo(drawer, fieldInfo);
          TraceField($"Found final drawer is type drawer ({drawer})");
          NextDrawer = drawer;
        }
      }

      if (isLastDrawer) {
        _isLastDrawer = true;
      }

      return true;
    }

    internal void InitInjected(PropertyDrawer next) {
      MainDrawer = this;
      PropertyDrawers = new List<DecoratingPropertyAttributeDrawer> {
        this
      };
      NextDrawer = next;
    }

    public PropertyDrawer GetNextDrawer(SerializedProperty property) {
      if (NextDrawer != null) {
        return NextDrawer;
      }
      
      var handler = UnityInternal.ScriptAttributeUtility.propertyHandlerCache.GetHandler(property);
      var drawers = handler.m_PropertyDrawers;
      var index   = drawers.IndexOf(this);
      if (index >= 0 && index < drawers.Count - 1) {
        return drawers[index + 1];
      }

      return null;
    }
  

    [Conditional("FUSION_EDITOR_TRACE")]
    private void TraceField(string message) {
      FusionEditorLog.TraceInspector($"[{GetType().FullName}] [{GetHashCode():X8}] [{fieldInfo?.DeclaringType.Name}.{fieldInfo?.Name}] {message}");
    }
  }
}

#endregion


#region DisplayAsEnumAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(DisplayAsEnumAttribute))]
  internal class DisplayAsEnumAttributeDrawer : PropertyDrawerWithErrorHandling {

    private EnumDrawer                 _enumDrawer;
    private Dictionary<(Type, string), Func<object, Type>> _cachedGetters = new Dictionary<(Type, string), Func<object, Type>>();

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attr     = (DisplayAsEnumAttribute)attribute;
      var enumType = attr.EnumType;

      if (enumType == null && !string.IsNullOrEmpty(attr.EnumTypeMemberName)) {
      
        var objType = property.serializedObject.targetObject.GetType();
        if (!_cachedGetters.TryGetValue((objType, attr.EnumTypeMemberName), out var getter)) {
          // maybe this is a top-level property then and we can use reflection?
          if (property.depth != 0) {
            FusionEditorLog.ErrorInspector($"Can't get enum type for {property.propertyPath}: non-SerializedProperty checks only work for top-level properties");
          } else {
            try {
              getter = objType.CreateGetter<Type>(attr.EnumTypeMemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            } catch (Exception e) {
              FusionEditorLog.ErrorInspector($"Can't get enum type for {property.propertyPath}: unable to create getter for {attr.EnumTypeMemberName} with exception {e}");
            }
          }
      
          _cachedGetters.Add((objType, attr.EnumTypeMemberName), getter);
        }
      
        enumType = getter(property.serializedObject.targetObject);
      }

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out var valueRect)) {
        if (enumType == null) {
          SetError($"Unable to get enum type for {property.propertyPath}");
        } else if (!enumType.IsEnum) {
          SetError($"Type {enumType} is not an enum type");
        } else {
          ClearError();
          _enumDrawer.Draw(valueRect, property, enumType, true);
        }
      }
    }
  }
}

#endregion


#region DisplayNameAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  //[CustomPropertyDrawer(typeof(DisplayNameAttribute))]
  internal class DisplayNameAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    private GUIContent _label = new GUIContent();
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      if (((DisplayNameAttribute)attribute).Name == null) {
        base.OnGUIInternal(position, property, label);
        return;
      }
      if (label.text == string.Empty && label.image == null || property.IsArrayElement()) {
        base.OnGUIInternal(position, property, label);
        return;
      }
      _label.text    = ((DisplayNameAttribute)attribute).Name;
      _label.image   = label.image;
      _label.tooltip = label.tooltip;
      base.OnGUIInternal(position, property, _label);
    }
    
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, DisplayNameAttribute attribute) {
      return new[] { new Sirenix.OdinInspector.LabelTextAttribute(attribute.Name) };
    }
#endif
  }
  
  [CustomPropertyDrawer(typeof(DisplayNameAttribute))]
  [RedirectCustomPropertyDrawer(typeof(DisplayNameAttribute), typeof(DisplayNameAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region DoIfAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;

  internal abstract partial class DoIfAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    
    private static Dictionary<(Type, string), Func<object, object>> _cachedGetters = new Dictionary<(Type, string), Func<object, object>>();
    
    internal static bool CheckDraw(DoIfAttributeBase doIf, SerializedObject serializedObject) {
      var compareProperty = serializedObject.FindProperty(doIf.ConditionMember);

      if (compareProperty != null) {
        return CheckProperty(doIf, compareProperty);
      }
      
      return CheckGetter(doIf, serializedObject, 0, string.Empty) == true;
    }
    
    internal static bool CheckDraw(DoIfAttributeBase doIf, SerializedProperty property) {
      var compareProperty = property.depth < 0 ? property.FindPropertyRelative(doIf.ConditionMember) : property.FindPropertyRelativeToParent(doIf.ConditionMember);

      if (compareProperty != null) {
        return CheckProperty(doIf, compareProperty);
      }
      
      return CheckGetter(doIf, property.serializedObject, property.depth, property.propertyPath) == true;
    }

    private static bool CheckProperty(DoIfAttributeBase doIf, SerializedProperty compareProperty) {
      switch (compareProperty.propertyType) {
        case SerializedPropertyType.Boolean:
        case SerializedPropertyType.Integer:
        case SerializedPropertyType.Enum:
        case SerializedPropertyType.Character:
          return CheckCondition(doIf, compareProperty.longValue);

        case SerializedPropertyType.ObjectReference:
          return CheckCondition(doIf, compareProperty.objectReferenceInstanceIDValue);

        case SerializedPropertyType.Float:
          return CheckCondition(doIf, compareProperty.doubleValue);

        default:
          FusionEditorLog.ErrorInspector($"Can't check condition for {compareProperty.propertyPath}: unsupported property type {compareProperty.propertyType}");
          return true;
      }
    }
    
    private static bool? CheckGetter(DoIfAttributeBase doIf, SerializedObject serializedObject, int depth, string referencePath) {
      var objType = serializedObject.targetObject.GetType();
      if (!_cachedGetters.TryGetValue((objType, doIf.ConditionMember), out var getter)) {
        // maybe this is a top-level property then and we can use reflection?
        if (depth != 0) {
          if (doIf.ErrorOnConditionMemberNotFound) {
            FusionEditorLog.ErrorInspector($"Can't check condition for {referencePath}: non-SerializedProperty checks only work for top-level properties");
          }
        } else {
          try {
            getter = objType.CreateGetter(doIf.ConditionMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
          } catch (Exception e) {
            FusionEditorLog.ErrorInspector($"Can't check condition for {referencePath}: unable to create getter for {doIf.ConditionMember} with exception {e}");
          }
        }

        _cachedGetters.Add((objType, doIf.ConditionMember), getter);
      }
      
      if (getter != null) {
        bool? result = null;
        foreach (var target in serializedObject.targetObjects) {
          bool targetResult = CheckCondition(doIf, getter(target));
          if (result.HasValue && result.Value != targetResult) {
            return null;
          } else {
            result = targetResult;
          }
        }

        return result;
      } else {
        return true;
      }
    }
    
    public static bool CheckCondition(DoIfAttributeBase attribute, double value) {
      if (!attribute._isDouble) throw new InvalidOperationException();

      var doubleValue = attribute._doubleValue;
      switch (attribute.Compare) {
        case CompareOperator.Equal:                  return value == doubleValue;
        case CompareOperator.NotEqual:               return value != doubleValue;
        case CompareOperator.Less:                   return value < doubleValue;
        case CompareOperator.LessOrEqual:            return value <= doubleValue;
        case CompareOperator.GreaterOrEqual:         return value >= doubleValue;
        case CompareOperator.Greater:                return value > doubleValue;
        case CompareOperator.NotZero:                return value != 0;
        case CompareOperator.IsZero:                 return value == 0;
        case CompareOperator.BitwiseAndNotEqualZero: throw new NotSupportedException();
        default:                                     throw new ArgumentOutOfRangeException();
      }
    }

    public static bool CheckCondition(DoIfAttributeBase attribute, long value) {
      if (attribute._isDouble) throw new InvalidOperationException();

      var _longValue = attribute._longValue;
      switch (attribute.Compare) {
        case CompareOperator.Equal:                  return value == _longValue;
        case CompareOperator.NotEqual:               return value != _longValue;
        case CompareOperator.Less:                   return value < _longValue;
        case CompareOperator.LessOrEqual:            return value <= _longValue;
        case CompareOperator.GreaterOrEqual:         return value >= _longValue;
        case CompareOperator.Greater:                return value > _longValue;
        case CompareOperator.NotZero:                return value != 0;
        case CompareOperator.IsZero:                 return value == 0;
        case CompareOperator.BitwiseAndNotEqualZero: return (value & _longValue) != 0;
        default:                                     throw new ArgumentOutOfRangeException();
      }
    }

    public static bool CheckCondition(DoIfAttributeBase attribute, object value) {
      if (attribute._isDouble) {
        double converted = 0.0;
        if (value != null) {
          if (value is UnityEngine.Object o && !o) {
            // treat as 0
          } else if (value.GetType().IsValueType) {
            converted = Convert.ToDouble(value);
          } else {
            converted = 1.0;
          }
        }

        return CheckCondition(attribute, converted);
      } else {
        long converted = 0;
        if (value != null) {
          if (value is UnityEngine.Object o && !o) {
            // treat as 0
          } else if (value.GetType().IsValueType) {
            converted = Convert.ToInt64(value);
          } else {
            converted = 1;
          }
        }

        return CheckCondition(attribute, converted);
      }
    }
  }
}

#endregion


#region DrawIfAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class DrawIfAttributeDrawer : DoIfAttributeDrawer {
    public DrawIfAttribute Attribute => (DrawIfAttribute)attribute;

    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      if (Attribute.Mode == DrawIfMode.ReadOnly || CheckDraw(Attribute, property)) {
        return base.GetPropertyHeightInternal(property, label);
      }
      
      return -EditorGUIUtility.standardVerticalSpacing;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var readOnly = Attribute.Mode == DrawIfMode.ReadOnly;
      var draw     = CheckDraw(Attribute, property);

      if (readOnly || draw) {
        EditorGUI.BeginDisabledGroup(!draw);

        base.OnGUIInternal(position, property, label);

        EditorGUI.EndDisabledGroup();
      }
    }
  }
  
  [CustomPropertyDrawer(typeof(DrawIfAttribute))]
  [RedirectCustomPropertyDrawer(typeof(DrawIfAttribute), typeof(DrawIfAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region DrawInlineAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(DrawInlineAttribute))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  internal partial class DrawInlineAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      EditorGUI.BeginProperty(position, label, property);
      
      foreach (var childProperty in property.GetChildren()) {
        position.height = EditorGUI.GetPropertyHeight(childProperty, true);
        EditorGUI.PropertyField(position, childProperty, true);
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
      }

      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      float height = 0f;

      foreach (var childProperty in property.GetChildren()) {
        height += EditorGUI.GetPropertyHeight(childProperty, true) + EditorGUIUtility.standardVerticalSpacing;
      }

      height -= EditorGUIUtility.standardVerticalSpacing;
      return height;
    }
  }
}

#endregion


#region ErrorIfAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class ErrorIfAttributeDrawer : MessageIfDrawerBase {
    private new ErrorIfAttribute Attribute => (ErrorIfAttribute)attribute;

    protected override bool        IsBox          => Attribute.AsBox;
    protected override string      Message        => Attribute.Message;
    protected override MessageType MessageType    => MessageType.Error;
    override protected Color       InlineBoxColor => FusionEditorSkin.ErrorInlineBoxColor;
    protected override Texture     MessageIcon    => FusionEditorSkin.ErrorIcon;
  }
  
  [CustomPropertyDrawer(typeof(ErrorIfAttribute))]
  [RedirectCustomPropertyDrawer(typeof(ErrorIfAttribute), typeof(ErrorIfAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}


#endregion


#region ExpandableEnumAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ExpandableEnumAttribute))]
  internal class ExpandableEnumAttributeDrawer : PropertyDrawerWithErrorHandling {
    
    private const float ToggleIndent = 5;

    private readonly GUIContent[]            _gridOptions = new[] { new GUIContent("Nothing"), new GUIContent("Everything") };
    private          EnumDrawer              _enumDrawer;
    private readonly LazyGUIStyle            _buttonStyle = LazyGUIStyle.Create(_ => new GUIStyle(EditorStyles.miniButton) { fontSize = EditorStyles.miniButton.fontSize - 1 });
    
    private new    ExpandableEnumAttribute attribute => (ExpandableEnumAttribute)base.attribute;
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      bool wasExpanded = attribute.AlwaysExpanded || property.isExpanded;

      var rowRect = new Rect(position) {
        height = EditorGUIUtility.singleLineHeight,
      };

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        var  valueRect = EditorGUI.PrefixLabel(rowRect, label);
        
        bool isEnum        = property.propertyType == SerializedPropertyType.Enum;
        var  maskProperty  = isEnum ? property : property.FindPropertyRelative("Mask").FindPropertyRelative("values");

        Mask256 rawValue;
        if (isEnum) {
          rawValue = new Mask256(maskProperty.longValue);
          
        } else {
          rawValue = new Mask256(
            maskProperty.GetFixedBufferElementAtIndex(0).longValue, 
            maskProperty.GetFixedBufferElementAtIndex(1).longValue, 
            maskProperty.GetFixedBufferElementAtIndex(2).longValue, 
            maskProperty.GetFixedBufferElementAtIndex(3).longValue 
            );
        }
        var foldoutRect = new Rect(valueRect) { width = FusionEditorGUI.FoldoutWidth };
        valueRect.xMin += foldoutRect.width;

        EditorGUI.BeginChangeCheck();
        if (wasExpanded) {
          if (_enumDrawer.IsFlags && attribute.ShowFlagsButtons) {
            int gridValue = -1;
            if (rawValue.IsNothing()) {
              // nothing
              gridValue = 0;
            } else if (Equals(_enumDrawer.BitMask & rawValue, _enumDrawer.BitMask)) {

              var test = _enumDrawer.BitMask & rawValue;
              if (Equals(test, _enumDrawer.BitMask))
              // everything
              gridValue = 1;
            }

            // traverse values in reverse; make sure the first alias is used in case there are multiple
            if (isEnum) {
              for (int i = _enumDrawer.Values.Length; i-- > 0;) {
                if (_enumDrawer.Values[i] == 0) {
                  _gridOptions[0].text = _enumDrawer.Names[i];
                } else if ( _enumDrawer.Values[i] == _enumDrawer.BitMask[0]) {
                  // Unity's drawer does not replace "Everything"
                  _gridOptions[1].text = _enumDrawer.Names[i];
                }
              }              
            }

            var gridSelection = GUI.SelectionGrid(valueRect, gridValue, _gridOptions, _gridOptions.Length, _buttonStyle);
            if (gridSelection != gridValue) {
              if (gridSelection == 0) {
                rawValue = default;
              } else if (gridSelection == 1) {
                rawValue = _enumDrawer.BitMask;
              }
            }
          } else {
            // draw a dummy field to consume the prefix
            EditorGUI.LabelField(valueRect, GUIContent.none);
          }
        } else {
          if (isEnum) {
            var enumValue = (Enum)Enum.ToObject(_enumDrawer.EnumType, rawValue[0]);
            if (_enumDrawer.IsFlags) {
              enumValue = EditorGUI.EnumFlagsField(valueRect, enumValue);
            } else {
              enumValue = EditorGUI.EnumPopup(valueRect, enumValue);
            }

            rawValue[0] = Convert.ToInt64(enumValue);            
          } else {
            // Droplist for FieldsMask<T>
            _enumDrawer.Draw(valueRect, maskProperty, fieldInfo.FieldType, false);
          }
        }

        if (EditorGUI.EndChangeCheck()) {
          if (isEnum) {
            maskProperty.longValue = rawValue[0];
          } else {
            maskProperty.GetFixedBufferElementAtIndex(0).longValue = rawValue[0];
            maskProperty.GetFixedBufferElementAtIndex(1).longValue = rawValue[1];
            maskProperty.GetFixedBufferElementAtIndex(2).longValue = rawValue[2];
            maskProperty.GetFixedBufferElementAtIndex(3).longValue = rawValue[3];
          }
          property.serializedObject.ApplyModifiedProperties();
        }

        if (!attribute.AlwaysExpanded) {
          using (new FusionEditorGUI.EnabledScope(true)) {
            property.isExpanded = EditorGUI.Toggle(foldoutRect, wasExpanded, EditorStyles.foldout);
          }
        }

        if (wasExpanded) {
          if (Event.current.type == EventType.Repaint) {
            EditorStyles.helpBox.Draw(new Rect(position) { yMin = rowRect.yMax }, GUIContent.none, false, false, false, false);
          }

          EditorGUI.BeginChangeCheck();

          rowRect.xMin += ToggleIndent;

          for (int i = 0; i < _enumDrawer.Values.Length; ++i) {
            if (_enumDrawer.IsFlags && _enumDrawer.Values[i].IsNothing()) {
              continue;
            }

            rowRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var toggleRect = rowRect;
            var buttonRect = new Rect();
            if (attribute.ShowInlineHelp) {
              // move the button to keep it in the box
              buttonRect      =  FusionEditorGUI.GetInlineHelpButtonRect(rowRect);
              toggleRect.xMin += buttonRect.width + 0;
              buttonRect.x    += buttonRect.width - 3;
            }

            bool wasSelected = _enumDrawer.IsFlags
              ? Equals(rawValue & _enumDrawer.Values[i], _enumDrawer.Values[i])
              : Equals(rawValue, _enumDrawer.Values[i]);
            if (EditorGUI.ToggleLeft(toggleRect, _enumDrawer.Names[i], wasSelected) != wasSelected) {
              if (_enumDrawer.IsFlags) {
                if (wasSelected) {
                  rawValue &= ~_enumDrawer.Values[i];
                } else {
                  rawValue |= _enumDrawer.Values[i];
                }
              } else if (!wasSelected) {
                rawValue = _enumDrawer.Values[i];
              }
            }

            if (attribute.ShowInlineHelp) {
              var helpContent = FusionCodeDoc.FindEntry(_enumDrawer.Fields[i], false);
              if (helpContent != null) {
                var helpPath = GetHelpPath(property, _enumDrawer.Fields[i]);
                
                var wasHelpExpanded = FusionEditorGUI.IsHelpExpanded(this, helpPath);
                if (wasHelpExpanded) {
                  var helpSize = FusionEditorGUI.GetInlineBoxSize(helpContent);
                  var helpRect = rowRect;
                  helpRect.y      += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                  helpRect.height =  helpSize.y;
                  
                  rowRect.y += helpSize.y;
                  
                  FusionEditorGUI.DrawInlineBoxUnderProperty(helpContent, helpRect, FusionEditorSkin.HelpInlineBoxColor, true);
                }
                
                buttonRect.x += buttonRect.width;
                if (FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasHelpExpanded, doButton: true, doIcon: true)) {
                  FusionEditorGUI.SetHelpExpanded(this, helpPath, !wasHelpExpanded);
                }
              }
            }
          }

          if (EditorGUI.EndChangeCheck()) {
            if (isEnum) {
              maskProperty.longValue = rawValue[0];
            } else {
              maskProperty.GetFixedBufferElementAtIndex(0).longValue = rawValue[0];
              maskProperty.GetFixedBufferElementAtIndex(1).longValue = rawValue[1];
              maskProperty.GetFixedBufferElementAtIndex(2).longValue = rawValue[2];
              maskProperty.GetFixedBufferElementAtIndex(3).longValue = rawValue[3];
            }
            property.serializedObject.ApplyModifiedProperties();
          }
        }
      }
    }


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      var enumType = property.propertyType == SerializedPropertyType.Enum ? fieldInfo.FieldType.GetUnityLeafType() : fieldInfo.FieldType;
      _enumDrawer.EnsureInitialized(enumType, attribute.ShowInlineHelp);

      int rowCount = 0;

      float height;
      
      var forceExpand = attribute.AlwaysExpanded;
      var showHelp    = attribute.ShowInlineHelp;

      if (forceExpand || property.isExpanded) {
        if (_enumDrawer.IsFlags) {
          foreach (var value in _enumDrawer.Values) {
            if (value.IsNothing()) {
              continue;
            }

            ++rowCount;
          }
        } else {
          rowCount = _enumDrawer.Values.Length;
        }

        height = (rowCount + 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

        if (showHelp) {
          foreach (var field in _enumDrawer.Fields) {
            if (FusionEditorGUI.IsHelpExpanded(this, GetHelpPath(property, field))) {
              var helpContent = FusionCodeDoc.FindEntry(field, false);
              if (helpContent != null) {
                height += FusionEditorGUI.GetInlineBoxSize(helpContent).y;
              }
            }
          }
        }
        
      } else {
        height = EditorGUIUtility.singleLineHeight;
      }

      return height;
    }

    private static string GetHelpPath(SerializedProperty property, FieldInfo field) {
      return property.propertyPath + "/" + field.Name;
    }
  }
}

#endregion


#region FieldEditorButtonAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;
  using Object = UnityEngine.Object;

  internal partial class FieldEditorButtonAttributeDrawer : DecoratingPropertyAttributeDrawer {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var propertyPosition = position;
      propertyPosition.height -= EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

      base.OnGUIInternal(propertyPosition, property, label);

      var buttonPosition = position;
      buttonPosition.yMin = position.yMax - EditorGUIUtility.singleLineHeight;

      var attribute        = (FieldEditorButtonAttribute)this.attribute;
      var targetObjects    = property.serializedObject.targetObjects;
      var targetObjectType = property.serializedObject.targetObject.GetType();

      if (DrawButton(buttonPosition, attribute, targetObjectType, targetObjects)) {
        property.serializedObject.Update();
        property.serializedObject.ApplyModifiedProperties();
      }
    }

    private static bool DrawButton(Rect buttonPosition, FieldEditorButtonAttribute attribute, Type targetObjectType, Object[] targetObjects) {
      using (new EditorGUI.DisabledGroupScope(!attribute.AllowMultipleTargets && targetObjects.Length > 1)) {
        if (GUI.Button(buttonPosition, attribute.Label, EditorStyles.miniButton)) {
          var targetMethod = targetObjectType.GetMethod(attribute.TargetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
          if (targetMethod == null) {
            FusionEditorLog.ErrorInspector($"Unable to find method {attribute.TargetMethod} on type {targetObjectType}");
          } else {
            if (targetMethod.IsStatic) {
              targetMethod.Invoke(null, null);
            } else {
              foreach (var targetObject in targetObjects) {
                targetMethod.Invoke(targetObject, null);
              }
            }

            return true;
          }
        }

        return false;
      }
    }

    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      return base.GetPropertyHeightInternal(property, label) + EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
    }
  }
  
  [CustomPropertyDrawer(typeof(FieldEditorButtonAttribute))]
  [RedirectCustomPropertyDrawer(typeof(FieldEditorButtonAttribute), typeof(FieldEditorButtonAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround { 
  }
}

#endregion


#region HideArrayElementLabelAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(HideArrayElementLabelAttribute))]
  partial class HideArrayElementLabelAttributeDrawer : DecoratingPropertyAttributeDrawer {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      if (property.IsArrayElement()) {
        label = GUIContent.none;
      }
      base.OnGUIInternal(position, property, label);
    }
  }
}

#endregion


#region InlineHelpAttributeDrawer.cs

namespace Fusion.Editor {
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  //[CustomPropertyDrawer(typeof(InlineHelpAttribute))]
  internal partial class InlineHelpAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    
    private bool       _initialized;
    private GUIContent _helpContent;
    private GUIContent _labelContent;
    
    protected new InlineHelpAttribute attribute => (InlineHelpAttribute)base.attribute; 

    
    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      
      var height = base.GetPropertyHeightInternal(property, label);
      if (height <= 0) {
        return height;
      }

      if (FusionEditorGUI.IsHelpExpanded(this, property.propertyPath)) {
        var helpContent = GetHelpContent(property);
        if (helpContent != null) {
          height += FusionEditorGUI.GetInlineBoxSize(helpContent).y;
        }
      }

      return height;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      var helpContent = GetHelpContent(property);
      
      if (position.height <= 0 || helpContent == null) {
        // ignore
        base.OnGUIInternal(position, property, label);
        return;
      }

      var nextDrawer = GetNextDrawer(property);
      var hasFoldout = HasFoldout(nextDrawer, property);

      using (new FusionEditorGUI.GUIContentScope(label)) {
        var (wasExpanded, buttonRect) = DrawInlineHelpBeforeProperty(label, helpContent, position, property.propertyPath, EditorGUI.indentLevel, hasFoldout, this);

        var propertyRect = position;
        if (wasExpanded) {
          propertyRect.height -= FusionEditorGUI.GetInlineBoxSize(helpContent).y;
        }
        base.OnGUIInternal(propertyRect, property, label);
        
        DrawInlineHelpAfterProperty(buttonRect, wasExpanded, helpContent, position);
      }
    }
    
    private GUIContent GetHelpContent(SerializedProperty property) {
      if (_initialized) {
        return _helpContent;
      }

      _initialized = true;
      
      if (property.IsArrayElement()) {
        return null;
      }

      if (fieldInfo == null) {
        return null;
      }
      
      _helpContent = FusionCodeDoc.FindEntry(fieldInfo, attribute.ShowTypeHelp);
      return _helpContent;
    }
    
    private bool HasFoldout(PropertyDrawer nextDrawer, SerializedProperty property) {
      var drawerMeta = nextDrawer?.GetType().GetCustomAttribute<FusionPropertyDrawerMetaAttribute>();
      if (drawerMeta != null) {
        return drawerMeta.HasFoldout;
      }

      if (property.IsArrayProperty()) {
        return true;
      }

      if (property.propertyType == SerializedPropertyType.Generic) {
        return true;
      }

      return false;
    }
    
    public static (bool expanded, Rect buttonRect) DrawInlineHelpBeforeProperty(GUIContent label, GUIContent helpContent, Rect propertyRect, string propertyPath, int depth, bool hasFoldout, object context, bool drawHelp = false) {
      
      if (label != null) {
        if (!string.IsNullOrEmpty(label.tooltip)) {
          label.tooltip += "\n\n";
        }
        label.tooltip += helpContent.tooltip;
      }

      if (propertyRect.width > 1 && propertyRect.height > 1) {
        var buttonRect = FusionEditorGUI.GetInlineHelpButtonRect(propertyRect, hasFoldout);

        if (depth == 0 && hasFoldout) {
          buttonRect.x = 16;
          if (label != null) {
            label.text = "    " + label.text;
          }
        }

        var wasExpanded = FusionEditorGUI.IsHelpExpanded(context, propertyPath);
        
        if (FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded, doButton: true, doIcon: false)) {
          FusionEditorGUI.SetHelpExpanded(context, propertyPath, !wasExpanded);
        }

        return (wasExpanded, buttonRect);
      }

      return default;
    }
    
    public static void DrawInlineHelpAfterProperty(Rect buttonRect, bool wasExpanded, GUIContent helpContent, Rect propertyRect) {

      if (buttonRect.width <= 0 && buttonRect.height <= 0) {
        return;
      }

      using (new FusionEditorGUI.EnabledScope(true)) {
        FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded, doButton: false, doIcon: true);
      }

      if (!wasExpanded) {
        return;
      }
      
      FusionEditorGUI.DrawInlineBoxUnderProperty(helpContent, propertyRect, FusionEditorSkin.HelpInlineBoxColor, true);
    }
  }
  
  
  [CustomPropertyDrawer(typeof(InlineHelpAttribute))]
  [RedirectCustomPropertyDrawer(typeof(InlineHelpAttribute), typeof(InlineHelpAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region INonApplicableOnArrayElements.cs

namespace Fusion.Editor {
  public interface INonApplicableOnArrayElements {
  }
}

#endregion


#region LayerAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(LayerAttribute))]
  internal class LayerAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      EditorGUI.BeginChangeCheck();

      int value;

      using (new FusionEditorGUI.ShowMixedValueScope(prop.hasMultipleDifferentValues)) {
        value = EditorGUI.LayerField(p, label, prop.intValue);
      }

      if (EditorGUI.EndChangeCheck()) {
        prop.intValue = value;
        prop.serializedObject.ApplyModifiedProperties();
      }
    }
  }
}

#endregion


#region LayerMatrixAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;
  
  internal partial class LayerMatrixAttributeDrawer : PropertyDrawerWithErrorHandling, INonApplicableOnArrayElements {

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out var valueRect)) {
        if (GUI.Button(valueRect, "Edit", EditorStyles.miniButton)) {
          PopupWindow.Show(valueRect, new LayerMatrixPopup(label?.text ?? property.displayName,
            (layerA, layerB) => {
              if (layerA >= property.arraySize) {
                return false;
              }
              
              return (property.GetArrayElementAtIndex(layerA).intValue & (1 << layerB)) != 0;
            },
            (layerA, layerB, val) => {
              if (Mathf.Max(layerA, layerB) >= property.arraySize) {
                property.arraySize = Mathf.Max(layerA, layerB) + 1;
              }
              if (val) {
                property.GetArrayElementAtIndex(layerA).intValue |= (1 << layerB);
                property.GetArrayElementAtIndex(layerB).intValue |= (1 << layerA);
              } else {
                property.GetArrayElementAtIndex(layerA).intValue &= ~(1 << layerB);
                property.GetArrayElementAtIndex(layerB).intValue &= ~(1 << layerA);
              }
              property.serializedObject.ApplyModifiedProperties();
            }));
        }
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return EditorGUIUtility.singleLineHeight;
    }

    class LayerMatrixPopup : PopupWindowContent {
      private const int checkboxSize = 16;
      private const int margin       = 30;
      private const int MaxLayers    = 32;

      private readonly string _name;
      private readonly int    _numLayers;
      private readonly float  _labelWidth;
      
      private readonly UnityInternal.LayerMatrixGUI.GetValueFunc _getter;
      private readonly UnityInternal.LayerMatrixGUI.SetValueFunc _setter;
      
      private bool  _show = true;
      
      public LayerMatrixPopup(string name, UnityInternal.LayerMatrixGUI.GetValueFunc getter, UnityInternal.LayerMatrixGUI.SetValueFunc setter) {
        _name       = name;
        _getter     = getter;
        _setter     = setter;
        _labelWidth = 110;
        _numLayers  = 0;
        for (int i = 0; i < MaxLayers; i++) {
          string layerName = LayerMask.LayerToName(i);
          if (string.IsNullOrEmpty(layerName)) {
            continue;
          }
          
          _numLayers++;
          _labelWidth = Mathf.Max(_labelWidth, GUI.skin.label.CalcSize(new GUIContent(layerName)).x);
        }
      }
      
      public override void OnGUI(Rect rect) {
        GUILayout.BeginArea(rect);
        
        UnityInternal.LayerMatrixGUI.DoGUI(_name, ref _show, _getter, _setter);
        _show = true;

        GUILayout.EndArea();
      }

      public override Vector2 GetWindowSize() {
        int   matrixWidth = checkboxSize * _numLayers;
        float width       = matrixWidth + _labelWidth + margin * 2;
        float heigth      = matrixWidth + _labelWidth + 15 + FusionEditorGUI.GetLinesHeight(3);
        return new Vector2(Mathf.Max(width, 350), heigth);
      }
      
    }
  }
  
  [CustomPropertyDrawer(typeof(LayerMatrixAttribute))]
  [RedirectCustomPropertyDrawer(typeof(LayerMatrixAttribute), typeof(LayerMatrixAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround { 
  }
}

#endregion


#region MaxStringByteCountAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(MaxStringByteCountAttribute))]
  internal class MaxStringByteCountAttributeDrawer : PropertyDrawerWithErrorHandling {
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attribute = (MaxStringByteCountAttribute)this.attribute;
      
      var encoding  = System.Text.Encoding.GetEncoding(attribute.Encoding);
      var byteCount = encoding.GetByteCount(property.stringValue);

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        FusionEditorGUI.ForwardPropertyField(position, property, label, true);
      }

      FusionEditorGUI.Overlay(position, $"({byteCount} B)");
      if (byteCount > attribute.ByteCount) {
        FusionEditorGUI.Decorate(position, $"{attribute.Encoding} string max size ({attribute.ByteCount} B) exceeded: {byteCount} B", MessageType.Error, hasLabel: true);
      }
    }
  }
}

#endregion


#region MessageIfDrawerBase.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  internal abstract class MessageIfDrawerBase : DoIfAttributeDrawer {
    protected abstract bool        IsBox          { get; }
    protected abstract string      Message        { get; }
    protected abstract MessageType MessageType    { get; }
    protected abstract Color       InlineBoxColor { get; }
    protected abstract Texture     MessageIcon    { get; }

    public DoIfAttributeBase Attribute => (DoIfAttributeBase)attribute;

    private GUIContent _messageContent;
    private GUIContent MessageContent {
      get {
        if (_messageContent == null) {
          _messageContent = new GUIContent(Message, MessageIcon, Message);
        }
        return _messageContent;
      }
    }

    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      var height = base.GetPropertyHeightInternal(property, label);

      if (IsBox) {
        if (CheckDraw(Attribute, property)) {
          float extra = CalcBoxHeight();
          height += extra;
        }
      }

      return height;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      if (!CheckDraw(Attribute, property)) {
        base.OnGUIInternal(position, property, label);
      } else {
        if (!IsBox) {
          
          var decorateRect = position;
          decorateRect.height =  EditorGUIUtility.singleLineHeight;
          decorateRect.xMin   += EditorGUIUtility.labelWidth;
          
          // TODO: should the border be resized for arrays?
          // if (property.IsArrayProperty()) {
          //   decorateRect.xMin = decorateRect.xMax - 48f;
          // }

          FusionEditorGUI.AppendTooltip(MessageContent.text, ref label);
          
          base.OnGUIInternal(position, property, label);
          
          FusionEditorGUI.Decorate(decorateRect, MessageContent.text, MessageType);
        } else {

          position = FusionEditorGUI.DrawInlineBoxUnderProperty(MessageContent, position, InlineBoxColor);
          base.OnGUIInternal(position, property, label);
          
          //position.y      += position.height;
          //position.height =  extra;
          //EditorGUI.HelpBox(position, MessageContent.text, MessageType);
          
        }
      }
    }
    
    private float CalcBoxHeight() {
      // const float SCROLL_WIDTH     = 16f;
      // const float LEFT_HELP_INDENT = 8f;
      //
      // var width = UnityInternal.EditorGUIUtility.contextWidth - /*InlineHelpStyle.MarginOuter -*/ SCROLL_WIDTH - LEFT_HELP_INDENT;
      // return EditorStyles.helpBox.CalcHeight(MessageContent, width);
      
      return FusionEditorGUI.GetInlineBoxSize(MessageContent).y;
    }
  }
}

#endregion


#region PropertyDrawerForArrayWorkaround.cs

//#define FUSION_EDITOR_TRACE
namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;

  internal partial class PropertyDrawerForArrayWorkaround : DecoratorDrawer {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class RedirectCustomPropertyDrawerAttribute : Attribute {
      public RedirectCustomPropertyDrawerAttribute(Type attributeType, Type drawerType) {
        AttributeType = attributeType;
        DrawerType    = drawerType;
      }
    
      public Type AttributeType { get; }
      public Type DrawerType    { get; }
    }
    
    
    private static Dictionary<Type, Type> _attributeToDrawer = typeof(PropertyDrawerForArrayWorkaround)
     .GetCustomAttributes<RedirectCustomPropertyDrawerAttribute>()
     .ToDictionary(x => x.AttributeType, x => x.DrawerType);
    
    private UnityInternal.PropertyHandler _handler;
    private PropertyDrawer                _drawer;
    private bool                          _initialized;
    
    public PropertyDrawerForArrayWorkaround() {
      _handler = UnityInternal.ScriptAttributeUtility.nextHandler;
    }
    
    public override float GetHeight() {
      if (!_initialized) {
        _initialized = true;

        if (!_attributeToDrawer.TryGetValue(attribute.GetType(), out var drawerType)) {
          FusionEditorLog.ErrorInspector($"No drawer for {attribute.GetType()}");
        } else if (_handler.decoratorDrawers?.Contains(this) != true) {
          FusionEditorLog.Warn($"Unable to forward to {drawerType}.");
        } else {
          var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);

          UnityInternal.PropertyDrawer.SetAttribute(drawer, attribute);
          
          // if (_handler.decoratorDrawers.Contains(this)) {
          // }
          
          if (_handler.m_PropertyDrawers == null) {
            _handler.m_PropertyDrawers = new List<PropertyDrawer>();
          }

          var insertPosition = _handler.m_PropertyDrawers.TakeWhile(x => x.attribute != null && x.attribute.order < attribute.order)
           .Count();
          
          FusionEditorLog.Trace($"Inserting {drawerType} at {insertPosition}");
          _handler.m_PropertyDrawers.Insert(insertPosition, drawer);
        }
      }
      
      return 0;
    }

    public static Type GetDrawerType(Type attributeDrawerType) {
      return _attributeToDrawer[attributeDrawerType];
    }
  }

  // [CustomPropertyDrawer(typeof(Attrib))]
  // public class DummyDrawer : ForwardingPropertyDrawer {
  //   public class Attrib : PropertyAttribute {
  //   }
  //
  //   public DummyDrawer() {
  //     //ReadOnlyAttribute
  //   }
  //
  //   protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
  //     base.OnGUIInternal(position, property, label);
  //   }
  //
  //   protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
  //     return base.GetPropertyHeightInternal(property, label);
  //   }
  // }
  //
  // [CustomPropertyDrawer(typeof(Attrib))]
  // public class FooPropertyDrawer : PropertyDrawer {
  //   public class Attrib : PropertyAttribute {
  //     public PropertyAttribute OtherAttribute;
  //     public Type              OtherDrawerType;
  //   }
  //
  //   private PropertyDrawer _otherDrawer;
  //   
  //   private void EnsureOtherDrawer(SerializedProperty property) {
  //     if (_otherDrawer == null) {
  //       var attrib = (Attrib)attribute;
  //       _otherDrawer = (PropertyDrawer)Activator.CreateInstance(attrib.OtherDrawerType);
  //       UnityInternal.PropertyDrawer.SetAttribute(_otherDrawer, attrib.OtherAttribute);
  //       UnityInternal.PropertyDrawer.SetFieldInfo(_otherDrawer, fieldInfo);
  //     }
  //   }
  //   
  //   public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
  //     EnsureOtherDrawer(property);
  //     return _otherDrawer.GetPropertyHeight(property, label);
  //   }
  //
  //   public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
  //     EnsureOtherDrawer(property);
  //     _otherDrawer.OnGUI(position, property, label);
  //   }
  // }

  
}

#endregion


#region PropertyDrawerWithErrorHandling.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  internal abstract class PropertyDrawerWithErrorHandling : PropertyDrawer {
    private SerializedProperty _currentProperty;

    private readonly Dictionary<string, Entry> _errors = new();
    private          bool                      _hadError;
    private          string                    _info;
    public           float                     IconOffset;

    public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      FusionEditorLog.Assert(_currentProperty == null);

      var decoration = GetDecoration(property);

      if (decoration != null) {
        DrawDecoration(position, decoration.Value, label != GUIContent.none, true, false);
      }


      _currentProperty = property;
      _hadError        = false;
      _info            = null;

      EditorGUI.BeginChangeCheck();

      try {
        OnGUIInternal(position, property, label);
      } catch (ExitGUIException) {
        // pass through
      } catch (Exception ex) {
        SetError(ex.ToString());
      } finally {
        // if there was a change but no error clear
        if (EditorGUI.EndChangeCheck() && !_hadError) {
          ClearError();
        }

        _currentProperty = null;
      }

      if (decoration != null) {
        DrawDecoration(position, decoration.Value, label != GUIContent.none, false, true);
      }
    }

    private void DrawDecoration(Rect position, (string, MessageType, bool) decoration, bool hasLabel, bool drawButton = true, bool drawIcon = true) {
      var iconPosition = position;
      iconPosition.height =  EditorGUIUtility.singleLineHeight;
      iconPosition.x      -= IconOffset;
      FusionEditorGUI.Decorate(iconPosition, decoration.Item1, decoration.Item2, hasLabel, drawButton: drawButton, drawBorder: decoration.Item3);
    }

    private (string, MessageType, bool)? GetDecoration(SerializedProperty property) {
      if (_errors.TryGetValue(property.propertyPath, out var error)) {
        return (error.message, error.type, true);
      }

      if (_info != null) {
        return (_info, MessageType.Info, false);
      }

      return null;
    }

    protected abstract void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label);

    protected void ClearError() {
      ClearError(_currentProperty);
    }

    protected void ClearError(SerializedProperty property) {
      _hadError = false;
      _errors.Remove(property.propertyPath);
    }

    protected void ClearErrorIfLostFocus() {
      if (GUIUtility.keyboardControl != UnityInternal.EditorGUIUtility.LastControlID) {
        ClearError();
      }
    }

    protected void SetError(string error) {
      _hadError = true;
      _errors[_currentProperty.propertyPath] = new Entry {
        message = error,
        type    = MessageType.Error
      };
    }
    
    protected void SetError(Exception error) {
      SetError(error.ToString());
    }

    protected void SetWarning(string warning) {
      if (_errors.TryGetValue(_currentProperty.propertyPath, out var entry) && entry.type == MessageType.Error) {
        return;
      }

      _errors[_currentProperty.propertyPath] = new Entry {
        message = warning,
        type    = MessageType.Warning
      };
    }

    protected void SetInfo(string message) {
      if (_errors.TryGetValue(_currentProperty.propertyPath, out var entry) && entry.type == MessageType.Error || entry.type == MessageType.Warning ) {
        return;
      }
      
      _errors[_currentProperty.propertyPath] = new Entry {
        message = message,
        type    = MessageType.Info
      };
    }

    private struct Entry {
      public string      message;
      public MessageType type;
    }
  }
}

#endregion


#region RangeAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(RangeExAttribute))]
  internal class RangeAttributeDrawer : PropertyDrawer {

    const float FieldWidth = 130.0f;
    const float Spacing    = 5.0f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      
      var attrib = (RangeExAttribute)this.attribute;
      var min    = attrib.Min;
      var max    = attrib.Max;

      int   intValue   = 0;
      float floatValue = 0.0f;

      if (property.propertyType == SerializedPropertyType.Float) {
        floatValue = property.floatValue;
      } else if (property.propertyType == SerializedPropertyType.Integer) {
        intValue = property.intValue;
      } else {
        EditorGUI.LabelField(position, label.text, "Use RangeEx with float or int.");
        return;
      }
      
      EditorGUI.BeginChangeCheck();

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        if (attrib.UseSlider) {
          if (!attrib.ClampMin || !attrib.ClampMax) {

            var sliderRect = new Rect(position) {
              xMin = position.xMin + EditorGUIUtility.labelWidth,
              xMax = position.xMax - FieldWidth - Spacing
            };
            
            using (new FusionEditorGUI.LabelWidthScope(position.width - FieldWidth)) {
              if (property.propertyType == SerializedPropertyType.Float) {
                if (sliderRect.width > FieldWidth + Spacing) {
                  using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                    floatValue = GUI.HorizontalSlider(sliderRect, property.floatValue, (float)min, (float)max);
                  }
                }

                floatValue = EditorGUI.FloatField(position, label, floatValue);

              } else {
                if (sliderRect.width > FieldWidth) {
                  using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                    intValue = Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect, property.intValue, (float)min, (float)max));
                  }
                }

                intValue = EditorGUI.IntField(position, label, intValue);
              }
            }
          } else {
            var fieldWidth = EditorGUIUtility.fieldWidth;
            try {
              EditorGUIUtility.fieldWidth = FieldWidth;
              if (property.propertyType == SerializedPropertyType.Float) {
                floatValue = EditorGUI.Slider(position, label, property.floatValue, (float)min, (float)max);
              } else {
                intValue = EditorGUI.IntSlider(position, label, property.intValue, (int)min, (int)max);
              }
            } finally {
              EditorGUIUtility.fieldWidth = fieldWidth;
            }
          }
        } else {
          if (property.propertyType == SerializedPropertyType.Float) {
            floatValue = EditorGUI.FloatField(position, label, property.floatValue);
          } else {
            intValue = EditorGUI.IntField(position, label, property.intValue);
          }
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        if (property.propertyType == SerializedPropertyType.Float) {
          property.floatValue = Clamp(floatValue, attrib);
        } else if (property.propertyType == SerializedPropertyType.Integer) {
          property.intValue = Clamp(intValue, attrib);
        }

        property.serializedObject.ApplyModifiedProperties();
      }
    }

    private float Clamp(float value, RangeExAttribute attrib) {
      return Mathf.Clamp(value, 
        attrib.ClampMin ? (float)attrib.Min : float.MinValue,
        attrib.ClampMax ? (float)attrib.Max : float.MaxValue);
    }
    
    private int Clamp(int value, RangeExAttribute attrib) {
      return Mathf.Clamp(value, 
        attrib.ClampMin ? (int)attrib.Min : int.MinValue,
        attrib.ClampMax ? (int)attrib.Max : int.MaxValue);
    }
  }
}

#endregion


#region ReadOnlyAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class ReadOnlyAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var  attribute  = (ReadOnlyAttribute)this.attribute;
      bool isPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;
      
      using (new EditorGUI.DisabledGroupScope(isPlayMode ? attribute.InPlayMode : attribute.InEditMode)) {
        base.OnGUIInternal(position, property, label);
      }
    }
  }

  [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
  [RedirectCustomPropertyDrawer(typeof(ReadOnlyAttribute), typeof(ReadOnlyAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region ScenePathAttributeDrawer.cs

namespace Fusion.Editor {
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ScenePathAttribute))]
  internal class ScenePathAttributeDrawer : PropertyDrawerWithErrorHandling {
    private SceneAsset[] _allScenes;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var oldScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
      if (oldScene == null && !string.IsNullOrEmpty(property.stringValue)) {
        // well, maybe by name then?
        _allScenes = _allScenes ?? AssetDatabase.FindAssets("t:scene")
         .Select(x => AssetDatabase.GUIDToAssetPath(x))
         .Select(x => AssetDatabase.LoadAssetAtPath<SceneAsset>(x))
         .ToArray();

        var matchedByName = _allScenes.Where(x => x.name == property.stringValue).ToList();
        ;

        if (matchedByName.Count == 0) {
          SetError($"Scene not found: {property.stringValue}");
        } else {
          oldScene = matchedByName[0];
          if (matchedByName.Count > 1) {
            SetWarning("There are multiple scenes with this name");
          }
        }
      }

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.BeginChangeCheck();
        var newScene = EditorGUI.ObjectField(position, label, oldScene, typeof(SceneAsset), false) as SceneAsset;
        if (EditorGUI.EndChangeCheck()) {
          var assetPath = AssetDatabase.GetAssetPath(newScene);
          property.stringValue = assetPath;
          property.serializedObject.ApplyModifiedProperties();
          ClearError();
        }
      }
    }
  }
}

#endregion


#region ScriptFieldDrawer.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  internal class ScriptFieldDrawer : PropertyDrawer {
    
    private new ScriptHelpAttribute attribute => (ScriptHelpAttribute)base.attribute;

    public bool ForceHide = false;

    private bool       _initialized;
    private GUIContent _helpContent;
    private GUIContent _headerContent;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      if (ForceHide || attribute?.Hide == true) {
        return;
      }

      if (attribute == null) {
        EditorGUI.PropertyField(position, property, label);
        return;
      }
      
      
      EnsureInitialized(property);

      var  helpButtonRect  = FusionEditorGUI.GetInlineHelpButtonRect(position, false);
      bool wasHelpExpanded = _helpContent != null && FusionEditorGUI.IsHelpExpanded(this, property.propertyPath);
      
      if (wasHelpExpanded) {
        position = FusionEditorGUI.DrawInlineBoxUnderProperty(_helpContent, position, FusionEditorSkin.HelpInlineBoxColor);
      }

      if (_helpContent != null) {
        using (new FusionEditorGUI.EnabledScope(true)) {
          if (FusionEditorGUI.DrawInlineHelpButton(helpButtonRect, wasHelpExpanded, true, false)) {
            FusionEditorGUI.SetHelpExpanded(this, property.propertyPath, !wasHelpExpanded);
          }
        }
      }
      
      if (attribute.Style == ScriptHeaderStyle.Unity) {
        EditorGUI.PropertyField(position, property, label);
      } else {
        using (new FusionEditorGUI.EnabledScope(true)) {
          if (attribute.BackColor != ScriptHeaderBackColor.None) {
            FusionEditorGUI.DrawScriptHeaderBackground(position, FusionEditorSkin.GetScriptHeaderColor(attribute.BackColor));
          }

          var labelPosition = FusionEditorSkin.ScriptHeaderLabelStyle.margin.Remove(position);
          EditorGUIUtility.AddCursorRect(labelPosition, MouseCursor.Link);
          EditorGUI.LabelField(labelPosition, _headerContent, FusionEditorSkin.ScriptHeaderLabelStyle);

          var e = Event.current;
          if (e.type == EventType.MouseDown && position.Contains(e.mousePosition)) {
            if (e.clickCount == 1) {
              if (!string.IsNullOrEmpty(attribute.Url)) {
                Application.OpenURL(attribute.Url);
              }

              EditorGUIUtility.PingObject(property.objectReferenceValue);
            } else {
              AssetDatabase.OpenAsset(property.objectReferenceValue);
            }
          }

          FusionEditorGUI.DrawScriptHeaderIcon(position);
        }
      }
      
      if (_helpContent != null) {
        using (new FusionEditorGUI.EnabledScope(true)) {
          // paint over what the inspector has drawn
          FusionEditorGUI.DrawInlineHelpButton(helpButtonRect, wasHelpExpanded, false, true);
        }
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      if (ForceHide || attribute?.Hide == true) {
        return -EditorGUIUtility.standardVerticalSpacing;
      }

      if (attribute == null) {
        return EditorGUIUtility.singleLineHeight;
      }
      
      var height = EditorGUIUtility.singleLineHeight;

      if (FusionEditorGUI.IsHelpExpanded(this, property.propertyPath) && _helpContent != null) {
        height += FusionEditorGUI.GetInlineBoxSize(_helpContent).y;
      }
      
      return height;
    }

    private void EnsureInitialized(SerializedProperty property) {
      if (_initialized) {
        return;
      }

      _initialized = true;
      
      var type     = property.serializedObject.targetObject.GetType();
      
      _headerContent = new GUIContent(ObjectNames.NicifyVariableName(type.Name).ToUpper());
      _helpContent   = FusionCodeDoc.FindEntry(type);
    }
  }
}

#endregion


#region SerializableTypeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(SerializableType<>))]
  [CustomPropertyDrawer(typeof(SerializableType))]
  [CustomPropertyDrawer(typeof(SerializableTypeAttribute))]
  internal class SerializableTypeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      var attr = (SerializableTypeAttribute)attribute;

      SerializedProperty valueProperty;
      if (property.propertyType == SerializedPropertyType.String) {
        FusionEditorLog.Assert(attr != null);
        valueProperty = property;
      } else {
        FusionEditorLog.Assert(property.propertyType == SerializedPropertyType.Generic);
        valueProperty = property.FindPropertyRelativeOrThrow(nameof(SerializableType.AssemblyQualifiedName));
      }
      
      var assemblyQualifiedName = valueProperty.stringValue;
      
      var baseType     = typeof(object);
      var leafType     = fieldInfo.FieldType.GetUnityLeafType();
      if (leafType.IsGenericType && leafType.GetGenericTypeDefinition() == typeof(SerializableType<>)) {
        baseType = leafType.GetGenericArguments()[0];
      }
      if (attr?.BaseType != null) {
        baseType = attr.BaseType;
      }

      position = EditorGUI.PrefixLabel(position, label);
      
      string content = "[None]";
      if (!string.IsNullOrEmpty(assemblyQualifiedName)) {
        try {
          var type = Type.GetType(assemblyQualifiedName, true);
          content = type.FullName;
        } catch (Exception e) {
          SetError(e);
          content = assemblyQualifiedName;
        }
      }
      
      if (EditorGUI.DropdownButton(position, new GUIContent(content), FocusType.Keyboard)) {
        ClearError();
        FusionEditorGUI.DisplayTypePickerMenu(position, baseType, t => {
          string typeName = string.Empty;
          if (t != null) {
            typeName = t.AssemblyQualifiedName;
            if (attr?.UseFullAssemblyQualifiedName == false) {
              var versionIndex = typeName.IndexOf(", Version=", StringComparison.Ordinal);
              if (versionIndex > 0) {
                typeName = typeName.Substring(0, versionIndex);
              }
            }
          }
          
          valueProperty.stringValue = typeName;
          valueProperty.serializedObject.ApplyModifiedProperties();
        });
      }
    }
  }
}

#endregion


#region SerializeReferenceTypePickerAttributeDrawer.cs

namespace Fusion.Editor {
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(SerializeReferenceTypePickerAttribute))]
  partial class SerializeReferenceTypePickerAttributeDrawer : DecoratingPropertyAttributeDrawer {
    
    const string NullContent = "Null";
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var attribute = (SerializeReferenceTypePickerAttribute)this.attribute;

      Rect pickerRect;
      if (label == GUIContent.none) {
        pickerRect = position;
      } else {
        pickerRect = EditorGUI.PrefixLabel(new Rect(position) { height = EditorGUIUtility.singleLineHeight }, FusionEditorGUI.WhitespaceContent);
      }
      
      object instance = property.managedReferenceValue;
      var instanceType = instance?.GetType();
      
      if (EditorGUI.DropdownButton(pickerRect, new GUIContent(instanceType?.FullName ?? NullContent), FocusType.Keyboard)) {
        FusionEditorGUI.DisplayTypePickerMenu(pickerRect, fieldInfo.FieldType.GetUnityLeafType(), t => {
          if (t == null) {
            instance = null;
          } else if (t.IsInstanceOfType(instance)) {
            // do nothing
            return;
          } else {
            instance = System.Activator.CreateInstance(t);
          }
          property.managedReferenceValue = instance;
          property.serializedObject.ApplyModifiedProperties();
        }, noneOptionLabel: NullContent, groupByNamespace: attribute.GroupTypesByNamespace, selectedType: instanceType);
      }
      
      base.OnGUIInternal(position, property, label);
    }
  }
}

#endregion


#region ToggleLeftAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
  internal class ToggleLeftAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      EditorGUI.BeginProperty(position, label, property);

      EditorGUI.BeginChangeCheck();
      var val = EditorGUI.ToggleLeft(position, label, property.boolValue);

      if (EditorGUI.EndChangeCheck()) {
        property.boolValue = val;
      }

      EditorGUI.EndProperty();
    }
  }
}

#endregion


#region UnitAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnitAttribute))]
  [FusionPropertyDrawerMeta(HandlesUnits = true)]
  internal partial class UnitAttributeDrawer : DecoratingPropertyAttributeDrawer {
    private GUIContent _label;

    private void EnsureInitialized() {
      if (_label == null) {
        _label = new GUIContent(UnitToLabel(((UnitAttribute)attribute).Unit));
      }
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      base.OnGUIInternal(position, property, label);
      
      // check if any of the next drawers handles the unit
      for (var nextDrawer = GetNextDrawer(property); nextDrawer != null; nextDrawer = (nextDrawer as DecoratingPropertyAttributeDrawer)?.GetNextDrawer(property)) {
        var meta = nextDrawer.GetType().GetCustomAttribute<FusionPropertyDrawerMetaAttribute>();
        if (meta?.HandlesUnits == true) {
          return;
        }
      }

      EnsureInitialized();

      var propertyType = property.propertyType;
      var isExpanded  = property.isExpanded;
      
      DrawUnitOverlay(position, _label, propertyType, isExpanded);
    }

    public static void DrawUnitOverlay(Rect position, GUIContent label, SerializedPropertyType propertyType, bool isExpanded, bool odinStyle = false) {
      switch (propertyType) {

        case SerializedPropertyType.Vector2 when odinStyle:
        case SerializedPropertyType.Vector3 when odinStyle:
        case SerializedPropertyType.Vector4 when odinStyle: {
          var pos = position;
          int memberCount = (propertyType == SerializedPropertyType.Vector2) ? 2 : 
                            (propertyType == SerializedPropertyType.Vector3) ? 3 : 4;
          pos.xMin   += EditorGUIUtility.labelWidth;
          pos.yMin   =  pos.yMax - EditorGUIUtility.singleLineHeight;
          pos.width  /= memberCount;
          pos.height =  EditorGUIUtility.singleLineHeight;
          
          for (int i = 0; i < memberCount; ++i) {
            FusionEditorGUI.Overlay(pos, label);
            pos.x += pos.width;
          }
          
          break;
        }

        case SerializedPropertyType.Vector2:
        case SerializedPropertyType.Vector3: {
          Rect pos = position;
          // vector properties get broken down into two lines when there's not enough space
          if (EditorGUIUtility.wideMode) {
            pos.xMin  += EditorGUIUtility.labelWidth;
            pos.width /= 3;
          } else {
            pos.xMin  += 12;
            pos.yMin  =  pos.yMax - EditorGUIUtility.singleLineHeight;
            pos.width /= (propertyType == SerializedPropertyType.Vector2) ? 2 : 3;
          }

          pos.height = EditorGUIUtility.singleLineHeight;
          FusionEditorGUI.Overlay(pos, label);
          pos.x += pos.width;
          FusionEditorGUI.Overlay(pos, label);
          if (propertyType == SerializedPropertyType.Vector3) {
            pos.x += pos.width;
            FusionEditorGUI.Overlay(pos, label);
          }

          break;
        }
        case SerializedPropertyType.Vector4:
          if (isExpanded) {
            Rect pos = position;
            pos.yMin   = pos.yMax - 4 * EditorGUIUtility.singleLineHeight - 3 * EditorGUIUtility.standardVerticalSpacing;
            pos.height = EditorGUIUtility.singleLineHeight;
            for (int i = 0; i < 4; ++i) {
              FusionEditorGUI.Overlay(pos, label);
              pos.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
          }

          break;
        default: {
          var pos = position;
          pos.height = EditorGUIUtility.singleLineHeight;
          FusionEditorGUI.Overlay(pos, label);
        }
          break;
      }
    }

    public static string UnitToLabel(Units units) {
      switch (units) {
        case Units.None:                 return string.Empty;
        case Units.Ticks:                return "ticks";
        case Units.Seconds:              return "s";
        case Units.MilliSecs:            return "ms";
        case Units.Kilobytes:            return "kB";
        case Units.Megabytes:            return "MB";
        case Units.Normalized:           return "normalized";
        case Units.Multiplier:           return "multiplier";
        case Units.Percentage:           return "%";
        case Units.NormalizedPercentage: return "n%";
        case Units.Degrees:              return "\u00B0";
        case Units.PerSecond:            return "hz";
        case Units.DegreesPerSecond:     return "\u00B0/sec";
        case Units.Radians:              return "rad";
        case Units.RadiansPerSecond:     return "rad/s";
        case Units.TicksPerSecond:       return "ticks/s";
        case Units.Units:                return "units";
        case Units.Bytes:                return "B";
        case Units.Count:                return "count";
        case Units.Packets:              return "packets";
        case Units.Frames:               return "frames";
        case Units.FramesPerSecond:      return "fps";
        case Units.SquareMagnitude:      return "mag\u00B2";
        default:                         throw new ArgumentOutOfRangeException(nameof(units), $"{units}");
      }
    }
  }
}

#endregion


#region UnityAssetGuidAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnityAssetGuidAttribute))]
  [FusionPropertyDrawerMeta(HasFoldout = false)]
  internal class UnityAssetGuidAttributeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      string guid;
      position.width -= 40;

      if (property.propertyType == SerializedPropertyType.Generic) {
        guid = DrawMangledRawGuid(position, property, label);
      } else if (property.propertyType == SerializedPropertyType.String) {
        using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
          EditorGUI.PropertyField(position, property, GUIContent.none, false);
          guid = property.stringValue;
        }
      } else {
        throw new InvalidOperationException();
      }

      string assetPath = string.Empty;

      bool parsable = GUID.TryParse(guid, out _);
      if (parsable) {
        ClearError();
        assetPath = AssetDatabase.GUIDToAssetPath(guid);
      }

      using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(assetPath))) {
        position.x     += position.width;
        position.width =  40;

        if (GUI.Button(position, "Ping")) {
          EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
        }
      }

      if (string.IsNullOrEmpty(assetPath)) {
        if (!parsable && !string.IsNullOrEmpty(guid)) {
          SetError($"Invalid GUID: {guid}");
        } else if (!string.IsNullOrEmpty(guid)) {
          SetWarning($"GUID not found");
        }
      } else {
        var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset == null) {
          SetError($"Asset with this guid does not exist. Last path:\n{assetPath}");
        } else {
          SetInfo($"Asset path:\n{assetPath}");
        }
      }
    }

    private unsafe string DrawMangledRawGuid(Rect position, SerializedProperty property, GUIContent label) {
      var inner = property.Copy();
      inner.Next(true);
      if (inner.depth != property.depth + 1 || !inner.isFixedBuffer || inner.fixedBufferSize != 2) {
        throw new InvalidOperationException();
      }

      var prop0 = inner.GetFixedBufferElementAtIndex(0);
      var prop1 = inner.GetFixedBufferElementAtIndex(1);

      string guid;
      unsafe {
        var rawMangled = stackalloc long[2];
        rawMangled[0] = prop0.longValue;
        rawMangled[1] = prop1.longValue;

        Guid guidStruct = default;
        CopyAndMangleGuid((byte*)rawMangled, (byte*)&guidStruct);

        using (new FusionEditorGUI.PropertyScope(position, label, property)) {
          EditorGUI.BeginChangeCheck();
          guid = EditorGUI.TextField(position, label, guidStruct.ToString("N"));
          if (EditorGUI.EndChangeCheck()) {
            if (Guid.TryParse(guid, out guidStruct)) {
              CopyAndMangleGuid((byte*)&guidStruct, (byte*)rawMangled);
              prop0.longValue = rawMangled[0];
              prop1.longValue = rawMangled[1];
            } else {
              SetError($"Unable to parse {guid}");
            }
          }
        }
      }

      return guid;
    }

    public static unsafe void CopyAndMangleGuid(byte* src, byte* dst) {
      dst[0] = src[3];
      dst[1] = src[2];
      dst[2] = src[1];
      dst[3] = src[0];

      dst[4] = src[5];
      dst[5] = src[4];

      dst[6] = src[7];
      dst[7] = src[6];

      dst[8]  = src[8];
      dst[9]  = src[9];
      dst[10] = src[10];
      dst[11] = src[11];
      dst[12] = src[12];
      dst[13] = src[13];
      dst[14] = src[14];
      dst[15] = src[15];
    }

    public bool HasFoldout(SerializedProperty property) {
      return false;
    }
  }
}

#endregion


#region UnityResourcePathAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnityResourcePathAttribute))]
  internal class UnityResourcePathAttributeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attrib = (UnityResourcePathAttribute)attribute;

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        position.width -= 40;
        EditorGUI.PropertyField(position, property, GUIContent.none, false);
        Object asset = null;

        var path = property.stringValue;
        if (string.IsNullOrEmpty(path)) {
          ClearError();
        } else {
          asset = Resources.Load(path, attrib.ResourceType);
          if (asset == null) {
            SetError($"Resource of type {attrib.ResourceType} not found at {path}");
          } else {
            SetInfo(AssetDatabase.GetAssetPath(asset));
          }
        }

        using (new EditorGUI.DisabledScope(asset == null)) {
          position.x     += position.width;
          position.width =  40;
          if (GUI.Button(position, "Ping"))
            // ping the main asset
          {
            EditorGUIUtility.PingObject(Resources.Load(path));
          }
        }
      }
    }
  }
}

#endregion


#region WarnIfAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  partial class WarnIfAttributeDrawer : MessageIfDrawerBase {
    private new WarnIfAttribute Attribute   => (WarnIfAttribute)attribute;

    protected override bool        IsBox          => Attribute.AsBox;
    protected override string      Message        => Attribute.Message;
    protected override MessageType MessageType    => MessageType.Warning;
    protected override Color       InlineBoxColor => FusionEditorSkin.WarningInlineBoxColor;
    protected override Texture     MessageIcon    => FusionEditorSkin.WarningIcon;
  }
  
  [CustomPropertyDrawer(typeof(WarnIfAttribute))]
  [RedirectCustomPropertyDrawer(typeof(WarnIfAttribute), typeof(WarnIfAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}


#endregion


#region ArrayLengthAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Collections;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class ArrayLengthAttributeDrawer {
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, ArrayLengthAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }

    class OdinAttributeProxy : Attribute {
      public ArrayLengthAttribute SourceAttribute;
    }

    class OdinDrawer : OdinAttributeDrawer<OdinAttributeProxy> {
      protected override bool CanDrawAttributeProperty(InspectorProperty property) {
        return property.GetUnityPropertyType() == SerializedPropertyType.ArraySize;
      }

      protected override void DrawPropertyLayout(GUIContent label) {
        var valEntry = Property.ValueEntry;

        var weakValues = valEntry.WeakValues;
        for (int i = 0; i < weakValues.Count; ++i) {
          var values = (IList)weakValues[i];
          if (values == null) {
            continue;
          }

          var arraySize = values.Count;
          var attr      = Attribute.SourceAttribute;
          if (arraySize < attr.MinLength) {
            arraySize = attr.MinLength;
          } else if (arraySize > attr.MaxLength) {
            arraySize = attr.MaxLength;
          }

          if (values.Count != arraySize) {
            if (values is Array array) {
              var newArr = Array.CreateInstance(array.GetType().GetElementType(), arraySize);
              Array.Copy(array, newArr, Math.Min(array.Length, arraySize));
              weakValues.ForceSetValue(i, newArr);
            } else {
              while (values.Count > arraySize) {
                values.RemoveAt(values.Count - 1);
              }

              while (values.Count < arraySize) {
                values.Add(null);
              }
            }

            weakValues.ForceMarkDirty();
          }
        }

        CallNextDrawer(label);
      }
    }
  }
}
#endif

#endregion


#region BinaryDataAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class BinaryDataAttributeDrawer {
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, BinaryDataAttribute attribute) {
      return Array.Empty<System.Attribute>();
    }
  }
}
#endif

#endregion


#region DoIfAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Linq;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEngine;

  partial class DoIfAttributeDrawer {

    protected abstract class OdinProxyAttributeBase : Attribute {
      public DoIfAttributeBase SourceAttribute;
    }

    protected abstract class OdinDrawerBase<T> : OdinAttributeDrawer<T> where T : OdinProxyAttributeBase {
      protected override bool CanDrawAttributeProperty(InspectorProperty property) {
        if (property.IsArrayElement(out _)) {
          return false;
        }

        return true;
      }

      protected override void DrawPropertyLayout(GUIContent label) {

        var doIf = this.Attribute.SourceAttribute;

        bool allPassed = true;
        bool anyPassed = false;

        var targetProp = Property.FindPropertyRelativeToParent(doIf.ConditionMember);
        if (targetProp == null) {
          var objType = Property.ParentType;
          if (!_cachedGetters.TryGetValue((objType, doIf.ConditionMember), out var getter)) {
            // maybe this is a top-level property then and we can use reflection?
            if (Property.GetDepth() != 0) {
              if (doIf.ErrorOnConditionMemberNotFound) {
                FusionEditorLog.ErrorInspector($"Can't check condition for {Property.Path}: non-SerializedProperty checks only work for top-level properties");
              }
            } else {
              try {
                _cachedGetters.Add((objType, doIf.ConditionMember), Property.ParentType.CreateGetter(doIf.ConditionMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy));
              } catch (Exception e) {
                FusionEditorLog.ErrorInspector($"Can't check condition for {Property.Path}: unable to create getter for {doIf.ConditionMember} with exception {e}");
              }
            }
          }

          if (getter != null) {
            foreach (var obj in Property.Parent.ValueEntry.WeakValues) {
              var value = getter(obj);
              if (DoIfAttributeDrawer.CheckCondition(doIf, value)) {
                anyPassed = true;
              } else {
                allPassed = false;
              }  
            }
          }
        } else {
          foreach (var value in targetProp.ValueEntry.WeakValues) {
            if (DoIfAttributeDrawer.CheckCondition(doIf, value)) {
              anyPassed = true;
            } else {
              allPassed = false;
            }
          }
        }

        DrawPropertyLayout(label, allPassed, anyPassed);
      }

      protected abstract void DrawPropertyLayout(GUIContent label, bool allPassed, bool anyPassed);
    }
  }
}
#endif

#endregion


#region DrawIfAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class DrawIfAttributeDrawer {

    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, DrawIfAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : OdinProxyAttributeBase {
    }

    class OdinDrawer : OdinDrawerBase<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label, bool allPassed, bool anyPassed) {
        var attribute = (DrawIfAttribute)Attribute.SourceAttribute;
        if (!allPassed) {
          if (attribute.Hide) {
            return;
          }
        }

        using (new EditorGUI.DisabledGroupScope(!allPassed)) {
          base.CallNextDrawer(label);
        }
      }
    }
  }
}
#endif

#endregion


#region DrawInlineAttributeDrawer.Odin.cs

namespace Fusion.Editor {
  partial class DrawInlineAttributeDrawer {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, DrawInlineAttribute attribute) {
      return new System.Attribute[] { new Sirenix.OdinInspector.InlinePropertyAttribute(), new Sirenix.OdinInspector.HideLabelAttribute() };
    }
#endif
  }
}

#endregion


#region ErrorIfAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;
  
  partial class ErrorIfAttributeDrawer {

    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, ErrorIfAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : OdinProxyAttributeBase {
    }
    
    class OdinDrawer : OdinDrawerBase<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label, bool allPassed, bool anyPassed) {
        var attribute = (ErrorIfAttribute)Attribute.SourceAttribute;
        
        base.CallNextDrawer(label);

        if (anyPassed) {
          using (new FusionEditorGUI.ErrorScope(attribute.Message)) {
          }
        }
      }
    }
  }
}
#endif

#endregion


#region FieldEditorButtonAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Linq;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class FieldEditorButtonAttributeDrawer {

    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, FieldEditorButtonAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public FieldEditorButtonAttribute SourceAttribute;
    }

    class OdinDrawer : OdinAttributeDrawer<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label) {
        CallNextDrawer(label);

        var buttonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
        var attribute  = Attribute.SourceAttribute;
        var root       = this.Property.SerializationRoot;
        var targetType = root.ValueEntry.TypeOfValue;
        var targetObjects = root.ValueEntry.WeakValues
         .OfType<UnityEngine.Object>()
         .ToArray();

        if (DrawButton(buttonRect, attribute, targetType, targetObjects)) {
          this.Property.MarkSerializationRootDirty();
        }
      }
    }
  }
}
#endif

#endregion


#region HideArrayElementLabelAttributeDrawer.Odin.cs

namespace Fusion.Editor {
  partial class HideArrayElementLabelAttributeDrawer {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, HideArrayElementLabelAttribute attribute) {
      // not yet supported
      return System.Array.Empty<System.Attribute>();
    }
#endif
  }
}

#endregion


#region InlineHelpAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Linq;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class InlineHelpAttributeDrawer {
    
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, InlineHelpAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public InlineHelpAttribute SourceAttribute;
    }

    class OdinDrawer : OdinAttributeDrawer<OdinAttributeProxy> {
      protected override bool CanDrawAttributeProperty(InspectorProperty property) {
        if (property.IsArrayElement(out _)) {
          return false;
        }

        var helpContent = GetHelpContent(property, true);
        if (helpContent == GUIContent.none) {
          return false;
        }

        return true;
      }
      
      private Rect _lastRect;

      private bool GetHasFoldout() {

        var (meta, _) = Property.GetNextPropertyDrawerMetaAttribute(Attribute);
        if (meta != null) {
          return meta.HasFoldout;
        }

        return Property.GetUnityPropertyType() == SerializedPropertyType.Generic;
      }

      protected override void DrawPropertyLayout(GUIContent label) {

        Rect buttonRect  = default;
        bool wasExpanded = false;

        bool hasFoldout   = GetHasFoldout();
        Rect propertyRect = _lastRect;
        var  helpContent  = GetHelpContent(Property, Attribute.SourceAttribute.ShowTypeHelp);

        using (new FusionEditorGUI.GUIContentScope(label)) {

          (wasExpanded, buttonRect) = InlineHelpAttributeDrawer.DrawInlineHelpBeforeProperty(label, helpContent, _lastRect, Property.Path, EditorGUI.indentLevel, hasFoldout, Property.SerializationRoot);

          EditorGUILayout.BeginVertical();
          this.CallNextDrawer(label);
          EditorGUILayout.EndVertical();
        }

        if (Event.current.type == EventType.Repaint) {
          _lastRect = GUILayoutUtility.GetLastRect();
        }

        if (propertyRect.width > 1 && propertyRect.height > 1) {

          if (wasExpanded) {
            var height = FusionEditorGUI.GetInlineBoxSize(helpContent).y;
            EditorGUILayout.GetControlRect(false, height);
            propertyRect.height += FusionEditorGUI.GetInlineBoxSize(helpContent).y;
          }

          DrawInlineHelpAfterProperty(buttonRect, wasExpanded, helpContent, propertyRect);
        }
      }

      private GUIContent GetHelpContent(InspectorProperty property, bool includeTypeHelp) {
        var parentType = property.ValueEntry.ParentType;
        var memberInfo = parentType.GetField(property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return FusionCodeDoc.FindEntry(memberInfo, includeTypeHelp) ?? GUIContent.none;
      }

    }
  }
}
#endif

#endregion


#region LayerMatrixAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Collections;
  using System.Linq;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEditor.Animations;
  using UnityEngine;

  partial class LayerMatrixAttributeDrawer {

    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, LayerMatrixAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public LayerMatrixAttribute SourceAttribute;
    }

    class OdinDrawer : OdinAttributeDrawer<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label) {

        var rect = EditorGUILayout.GetControlRect();
        var valueRect = EditorGUI.PrefixLabel(rect, label);
        if (GUI.Button(valueRect, "Edit")) {
          int[] values = (int[])this.Property.ValueEntry.WeakValues[0];

          PopupWindow.Show(valueRect, new LayerMatrixPopup(label.text, (layerA, layerB) => {
            if (layerA >= values.Length) {
              return false;
            }
            return (values[layerA] & (1 << layerB)) != 0;
          }, (layerA, layerB, val) => {
            if (Mathf.Max(layerA, layerB) >= values.Length) {
              Array.Resize(ref values, Mathf.Max(layerA, layerB) + 1);
            }
            
            if (val) {
              values[layerA] |= (1 << layerB);
              values[layerB] |= (1 << layerA);
            } else {
              values[layerA] &= ~(1 << layerB);
              values[layerB] &= ~(1 << layerA);
            }
            
            // sync other values
            for (int i = 1; i < this.Property.ValueEntry.ValueCount; ++i) {
              this.Property.ValueEntry.WeakValues.ForceSetValue(i, values.Clone());
            }
            
            Property.MarkSerializationRootDirty();
          }));
        }
      }
    }
  }
}
#endif

#endregion


#region FusionOdinAttributeConverterAttribute.cs

namespace Fusion.Editor {
  using System;

  [AttributeUsage(AttributeTargets.Method)]
  public class FusionOdinAttributeConverterAttribute : Attribute {
  }
}

#endregion


#region FusionOdinAttributeProcessor.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using Sirenix.Utilities;
  using UnityEditor;
  using UnityEngine;

  public partial class FusionOdinAttributeProcessor : Sirenix.OdinInspector.Editor.OdinAttributeProcessor {
    public override void ProcessChildMemberAttributes(Sirenix.OdinInspector.Editor.InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes) {
      for (int i = 0; i < attributes.Count; ++i) {
        var attribute = attributes[i];
        if (attribute is PropertyAttribute) {
          
          var drawerType = FusionEditorGUI.GetDrawerTypeIncludingWorkarounds(attribute);
          if (drawerType != null) {

            var method = drawerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
             .FirstOrDefault(x => x.IsDefined(typeof(FusionOdinAttributeConverterAttribute)));

            if (method != null) {
              var replacementAttributes = (System.Attribute[])method.Invoke(null, new object[] { member, attribute }) ?? Array.Empty<Attribute>();

              attributes.RemoveAt(i);
              FusionEditorLog.TraceInspector($"Replacing attribute {attribute.GetType().FullName} of {member.ToString()} with {string.Join(", ", replacementAttributes.Select(x => x.GetType().FullName))}");

              if (replacementAttributes.Length > 0) {
                attributes.InsertRange(i, replacementAttributes);
              }

              i += replacementAttributes.Length - 1;
              continue;
            }
          }

          if (attribute is DecoratingPropertyAttribute) {
            FusionEditorLog.Warn($"Unable to replace {nameof(DecoratingPropertyAttribute)}-derived attribute: {attribute.GetType().FullName}");
            attributes[i] = null;
          }
        }
      }
    }
  }
}
#endif

#endregion


#region FusionOdinExtensions.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  public static class FusionOdinExtensions {
    public static bool IsArrayElement(this InspectorProperty property, out int index) {
      var propertyPath = property.UnityPropertyPath;

      if (!propertyPath.EndsWith("]", StringComparison.Ordinal)) {
        index = -1;
        return false;
      }

      var indexStart = propertyPath.LastIndexOf("[", StringComparison.Ordinal);
      if (indexStart < 0) {
        index = -1;
        return false;
      }

      index = int.Parse(propertyPath.Substring(indexStart + 1, propertyPath.Length - indexStart - 2));
      return true;
    }

    public static bool IsArrayProperty(this InspectorProperty property) {
      var memberType = property.Info.TypeOfValue;
      if (!memberType.IsArrayOrList()) {
        return false;
      }

      return true;
    }

    public static int GetDepth(this InspectorProperty inspectorProperty) {
      int               depth           = 0;
      InspectorProperty currentProperty = inspectorProperty;

      while (currentProperty.Parent != null && !currentProperty.Parent.IsTreeRoot) {
        depth++;
        currentProperty = currentProperty.Parent;
      }

      return depth;
    }

    public static SerializedPropertyType GetUnityPropertyType(this InspectorProperty inspectorProperty) {
      if (inspectorProperty == null) {
        throw new ArgumentNullException(nameof(inspectorProperty));
      }

      var valueType = inspectorProperty.ValueEntry.TypeOfValue;

      if (valueType == typeof(bool)) {
        return SerializedPropertyType.Boolean;
      } else if (valueType == typeof(int) || valueType == typeof(long) || valueType == typeof(short) || valueType == typeof(byte) || valueType == typeof(uint) || valueType == typeof(ulong) || valueType == typeof(ushort) || valueType == typeof(sbyte)) {
        return SerializedPropertyType.Integer;
      } else if (valueType == typeof(float) || valueType == typeof(double)) {
        return SerializedPropertyType.Float;
      } else if (valueType == typeof(string)) {
        return SerializedPropertyType.String;
      } else if (valueType == typeof(Color)) {
        return SerializedPropertyType.Color;
      } else if (valueType == typeof(LayerMask)) {
        return SerializedPropertyType.LayerMask;
      } else if (valueType == typeof(Vector2)) {
        return SerializedPropertyType.Vector2;
      } else if (valueType == typeof(Vector3)) {
        return SerializedPropertyType.Vector3;
      } else if (valueType == typeof(Vector4)) {
        return SerializedPropertyType.Vector4;
      } else if (valueType == typeof(Vector2Int)) {
        return SerializedPropertyType.Vector2Int;
      } else if (valueType == typeof(Vector3Int)) {
        return SerializedPropertyType.Vector3Int;
      } else if (valueType == typeof(Rect)) {
        return SerializedPropertyType.Rect;
      } else if (valueType == typeof(RectInt)) {
        return SerializedPropertyType.RectInt;
      } else if (valueType == typeof(AnimationCurve)) {
        return SerializedPropertyType.AnimationCurve;
      } else if (valueType == typeof(Bounds)) {
        return SerializedPropertyType.Bounds;
      } else if (valueType == typeof(BoundsInt)) {
        return SerializedPropertyType.BoundsInt;
      } else if (valueType == typeof(Gradient)) {
        return SerializedPropertyType.Gradient;
      } else if (valueType == typeof(Quaternion)) {
        return SerializedPropertyType.Quaternion;
      } else if (valueType.IsEnum) {
        return SerializedPropertyType.Enum;
      } else if (typeof(UnityEngine.Object).IsAssignableFrom(valueType)) {
        return SerializedPropertyType.ObjectReference;
      } else if (valueType.IsArrayOrList()) {
        return SerializedPropertyType.ArraySize;
      }

      return SerializedPropertyType.Generic;
    }

    public static InspectorProperty FindPropertyRelativeToParent(this InspectorProperty property, string path) {

      InspectorProperty referenceProperty = property;

      int parentIndex = 0;
      do {
        if (referenceProperty.Parent == null) {
          return null;
        }
        
        referenceProperty = referenceProperty.Parent;
      } while (path[parentIndex++] == '^');

      if (parentIndex > 1) {
        path = path.Substring(parentIndex - 1);
      }
      
      var parts = path.Split('.');
      if (parts.Length == 0) {
        return null;
      }

      foreach (var part in parts) {
        var child = referenceProperty.Children[part];
        if (child != null) {
          referenceProperty = child;
        } else {
          return null;
        }
      }

      return referenceProperty;
    }

    public static (FusionPropertyDrawerMetaAttribute, Attribute) GetNextPropertyDrawerMetaAttribute(this InspectorProperty property, Attribute referenceAttribute) {

      var attributeIndex = referenceAttribute == null ? -1 : property.Attributes.IndexOf(referenceAttribute);

      for (int i = attributeIndex + 1; i < property.Attributes.Count; ++i) {
        var otherAttribute = property.Attributes[i];
        if (otherAttribute is DrawerPropertyAttribute == false) {
          continue;
        }

        var attributeDrawerType = FusionEditorGUI.GetDrawerTypeIncludingWorkarounds(otherAttribute);
        if (attributeDrawerType == null) {
          continue;
        }

        var meta = attributeDrawerType.GetCustomAttribute<FusionPropertyDrawerMetaAttribute>();
        if (meta != null) {
          return (meta, otherAttribute);
        }
      }


      var propertyDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(property.ValueEntry.TypeOfValue);

      if (propertyDrawerType != null) {
        var meta = propertyDrawerType.GetCustomAttribute<FusionPropertyDrawerMetaAttribute>();
        if (meta != null) {
          return (meta, null);
        }
      }

      return (null, null);
    }
  }
}
#endif

#endregion


#region ReadOnlyAttributeDrawer.Odin.cs

namespace Fusion.Editor {
  partial class ReadOnlyAttributeDrawer {
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, ReadOnlyAttribute attribute) {
      if (attribute.InEditMode && attribute.InPlayMode) {
        return new[] { new Sirenix.OdinInspector.ReadOnlyAttribute() };
      }
      if (attribute.InEditMode) {
        return new[] { new Sirenix.OdinInspector.DisableInEditorModeAttribute() };
      }
      if (attribute.InPlayMode) {
        return new[] { new Sirenix.OdinInspector.DisableInPlayModeAttribute() };
      }
      return System.Array.Empty<System.Attribute>();
    }
#endif
  }
}

#endregion


#region SerializeReferenceTypePickerDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;

  partial class SerializeReferenceTypePickerAttributeDrawer {
    [FusionOdinAttributeConverter]
      static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, SerializeReferenceTypePickerAttribute attribute) {
        return Array.Empty<System.Attribute>();
      }
  }
}
#endif

#endregion


#region UnitAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class UnitAttributeDrawer {

    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, UnitAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public UnitAttribute SourceAttribute;
    }
  
    class OdinUnitAttributeDrawer :  Sirenix.OdinInspector.Editor.OdinAttributeDrawer<OdinAttributeProxy> {
      private GUIContent _label;
      private Rect       _lastRect;
    
      protected override bool CanDrawAttributeProperty(Sirenix.OdinInspector.Editor.InspectorProperty property) {

        for (Attribute attrib = null;;) {
          var (meta, nextAttribute) = property.GetNextPropertyDrawerMetaAttribute(attrib);
          attrib                    = nextAttribute;
          if (meta?.HandlesUnits == true) {
            if (attrib is OdinAttributeProxy == false) {
              return false;
            }
          }

          if (meta == null || attrib == null) {
            break;
          }
        }

        switch (property.GetUnityPropertyType()) {
          case SerializedPropertyType.ArraySize:
            return false;
          default:
            return true;
        }
      }
    
      protected sealed override void DrawPropertyLayout(GUIContent label) {

        using (new EditorGUILayout.VerticalScope()) {
          this.CallNextDrawer(label);
        }

        if (Event.current.type == EventType.Repaint) {
          _lastRect = GUILayoutUtility.GetLastRect();
        }

        if (_lastRect.width > 1 && _lastRect.height > 1) {
          _label      ??= new GUIContent();
          _label.text =   UnitToLabel(this.Attribute.SourceAttribute.Unit);
          DrawUnitOverlay(_lastRect, _label, Property.GetUnityPropertyType(), false, odinStyle: true);
        }
      }
    }
  }
}
#endif

#endregion


#region WarnIfAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
namespace Fusion.Editor {
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class WarnIfAttributeDrawer {

    [FusionOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, WarnIfAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : OdinProxyAttributeBase {
    }
    
    class OdinDrawer : OdinDrawerBase<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label, bool allPassed, bool anyPassed) {
        var attribute = (WarnIfAttribute)Attribute.SourceAttribute;
        
        base.CallNextDrawer(label);

        if (anyPassed) {
          using (new FusionEditorGUI.WarningScope(attribute.Message)) {
          }
        }
      }
    }
  }
}
#endif

#endregion


#region INetworkAssetSourceFactory.cs

namespace Fusion.Editor {
  using UnityEditor;

  public partial interface INetworkAssetSourceFactory {
    int Order { get; }
  }
  
  public readonly partial struct NetworkAssetSourceFactoryContext {
    public readonly int    InstanceID;
    public readonly string AssetGuid;
    public readonly string AssetName;
    public readonly bool   IsMainAsset;

    public string AssetPath => AssetDatabaseUtils.GetAssetPathOrThrow(InstanceID);

    public NetworkAssetSourceFactoryContext(string assetGuid, int instanceID, string assetName, bool isMainAsset) {
      AssetGuid = assetGuid;
      InstanceID = instanceID;
      AssetName = assetName;
      IsMainAsset = isMainAsset;
    }

    public NetworkAssetSourceFactoryContext(HierarchyProperty hierarchyProperty) {
      AssetGuid = hierarchyProperty.guid;
      InstanceID = hierarchyProperty.instanceID;
      AssetName = hierarchyProperty.name;
      IsMainAsset = hierarchyProperty.isMainRepresentation;
    }
    
    public NetworkAssetSourceFactoryContext(UnityEngine.Object obj) {
      if (!obj) {
        throw new System.ArgumentNullException(nameof(obj));
      }
      
      var instanceId = obj.GetInstanceID();
      (AssetGuid, _) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(instanceId);
      InstanceID = instanceId;
      AssetName = obj.name;
      IsMainAsset = AssetDatabase.IsMainAsset(instanceId);
    } 
  }
}

#endregion


#region NetworkAssetSourceFactoryAddressable.cs

#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor.AddressableAssets;
  using UnityEditor.AddressableAssets.Settings;

  public partial class NetworkAssetSourceFactoryAddressable : INetworkAssetSourceFactory {
    public const int Order = 800;

    int INetworkAssetSourceFactory.Order => Order;

    protected bool TryCreateInternal<TSource, TAsset>(in NetworkAssetSourceFactoryContext context, out TSource result) 
      where TSource : NetworkAssetSourceAddressable<TAsset>, new()
      where TAsset : UnityEngine.Object {
      var addressableEntry = _guidToParentAddressable.Value[context.AssetGuid].SingleOrDefault();
      if (addressableEntry == null) {
        result = default;
        return false;
      }

      result = new TSource() {
        Address = new(addressableEntry.guid) {
          SubObjectName = context.IsMainAsset ? string.Empty : context.AssetName,
        },
      };
      return true;
    }
    
    readonly Lazy<ILookup<string, AddressableAssetEntry>> _guidToParentAddressable = new(() => CreateAddressablesLookup());
    
    static ILookup<string, AddressableAssetEntry> CreateAddressablesLookup() {
      var assetList      = new List<AddressableAssetEntry>();
      var assetsSettings = AddressableAssetSettingsDefaultObject.Settings;

      if (assetsSettings == null) {
        throw new System.InvalidOperationException("Unable to load Addressables settings. This may be due to an outdated Addressables version.");
      }

      foreach (var settingsGroup in assetsSettings.groups) {
        if (settingsGroup.ReadOnly)
          continue;
        settingsGroup.GatherAllAssets(assetList, true, true, true);
      }

      return assetList.Where(x => !string.IsNullOrEmpty(x.guid)).ToLookup(x => x.guid);
    }
  }
}
#endif

#endregion


#region NetworkAssetSourceFactoryResource.cs

namespace Fusion.Editor {
  public partial class NetworkAssetSourceFactoryResource : INetworkAssetSourceFactory {
    public const int Order = 1000;

    int INetworkAssetSourceFactory.Order => Order;

    protected bool TryCreateInternal<TSource, TAsset>(in NetworkAssetSourceFactoryContext context, out TSource result) 
      where TSource : NetworkAssetSourceResource<TAsset>, new()
      where TAsset : UnityEngine.Object {
      if (!PathUtils.TryMakeRelativeToFolder(context.AssetPath, "/Resources/", out var resourcePath)) {
        result = default;
        return false;
      }

      var withoutExtension = PathUtils.GetPathWithoutExtension(resourcePath);
      result = new TSource() {
        ResourcePath = withoutExtension,
        SubObjectName = context.IsMainAsset ? string.Empty : context.AssetName,
      };
      return true;
    }
  }
}

#endregion


#region NetworkAssetSourceFactoryStatic.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  public partial  class NetworkAssetSourceFactoryStatic : INetworkAssetSourceFactory {
    public const int Order = int.MaxValue;

    int INetworkAssetSourceFactory.Order => Order;
    
    protected bool TryCreateInternal<TSource, TAsset>(in NetworkAssetSourceFactoryContext context, out TSource result)
      where TSource : NetworkAssetSourceStaticLazy<TAsset>, new()
      where TAsset : UnityEngine.Object {
      
      if (typeof(TAsset).IsSubclassOf(typeof(Component))) {
        var prefab = (GameObject)EditorUtility.InstanceIDToObject(context.InstanceID);

        result = new TSource() {
          Prefab = prefab.GetComponent<TAsset>()
        };
        
      } else {
        result = new TSource() {
          Prefab = new(context.InstanceID)
        };
      }
      return true;
    }
  }
}

#endregion



#endregion


#region Assets/Photon/Fusion/Editor/FusionEditorConfigImporter.cs

namespace Fusion.Editor {
  using System.IO;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(0, "editorconfig")]
  public class FusionEditorConfigImporter : ScriptedImporter {
    public override void OnImportAsset(AssetImportContext ctx) {
      var path      = ctx.assetPath;
      var contents  = File.ReadAllText(path);
      
      // create internal text asset for convenience
      var mainAsset = new TextAsset(contents);
      ctx.AddObjectToAsset("main", mainAsset);
      ctx.SetMainObject(mainAsset);

      // write the actual editorconfig for editors to consume
      var editorConfigPath = Path.Combine(Path.GetDirectoryName(path), ".editorconfig");
      File.WriteAllText(editorConfigPath, contents);
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/FusionHierarchyWindowOverlay.cs

﻿namespace Fusion.Editor {
  using System;
  using Fusion.Analyzer;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  internal class FusionHierarchyWindowOverlay {

    [RuntimeInitializeOnLoadMethod]
    public static void Initialize() {
      UnityEditor.EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowOverlay;
      UnityEditor.EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowOverlay;
    }

    [StaticField(StaticFieldResetMode.None)]
    private static Lazy<GUIStyle> s_hierarchyOverlayLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(UnityEditor.EditorStyles.miniButton);
      result.alignment = TextAnchor.MiddleCenter;
      result.fontSize = 9;
      result.padding = new RectOffset(4, 4, 0, 0);
      result.fixedHeight = 13f;
      return result;
    });

    [StaticField(StaticFieldResetMode.None)]
    private static GUIContent s_multipleInstancesContent = EditorGUIUtility.IconContent("Warning", "multiple");

    private static void HierarchyWindowOverlay(int instanceId, Rect position) {
      var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
      if (obj != null) {
        return;
      }

      // find a scene for this id
      Scene scene = default;
      for (int i = 0; i < SceneManager.sceneCount; ++i) {
        var s = SceneManager.GetSceneAt(i);
        if (s.handle == instanceId) {
          scene = s;
          break;
        }
      }

      if (!scene.IsValid()) {
        return;
      }

      var instances = NetworkRunner.Instances;

      NetworkRunner matchingRunner = null;
      bool multipleRunners = false;
      
      for (int i = 0; i < instances.Count; ++i) {
        var runner = instances[i];

        if (runner.SimulationUnityScene == scene) {
          if (matchingRunner == null) {
            matchingRunner = runner;
          } else {
            multipleRunners = true;
            break;
          }
        }
      }

      if (!matchingRunner) {
        return;
      }

      var rect = new Rect(position) {
        xMin = position.xMax - 56,
        xMax = position.xMax - 2,
        yMin = position.yMin + 1,
      };

      {
        if (multipleRunners) {
          if (EditorGUI.DropdownButton(rect, s_multipleInstancesContent, FocusType.Passive, s_hierarchyOverlayLabelStyle.Value)) {
            var menu = new GenericMenu();
            for (int i = 0; i < instances.Count; ++i) {
              var runner = instances[i];
              var otherScene = runner.SimulationUnityScene;
              if (!otherScene.IsValid()) {
                continue;
              }
              if (otherScene.handle == instanceId) {
                menu.AddItem(MakeRunnerContent(runner), false, () => {
                  EditorGUIUtility.PingObject(runner);
                  Selection.activeObject = runner;
                });
              }
            }
            menu.ShowAsContext();
          }
        } else {
          var runner = matchingRunner;
          if (GUI.Button(rect, MakeRunnerContent(runner), s_hierarchyOverlayLabelStyle.Value)) {
            EditorGUIUtility.PingObject(runner);
            Selection.activeGameObject = runner.gameObject;
          }
        }
      }

      GUIContent MakeRunnerContent(NetworkRunner runner) {
        return new GUIContent($"{runner.Mode} {(runner.LocalPlayer.IsRealPlayer ? "P" + runner.LocalPlayer.PlayerId.ToString() : "")}");
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/FusionHubWindowUtils.cs

// ----------------------------------------------------------------------------
// <copyright file="WizardWindowUtils.cs" company="Exit Games GmbH">
//   PhotonNetwork Framework for Unity - Copyright (C) 2021 Exit Games GmbH
// </copyright>
// <summary>
//   MenuItems and in-Editor scripts for PhotonNetwork.
// </summary>
// <author>developer@exitgames.com</author>
// ----------------------------------------------------------------------------

namespace Fusion.Editor {
#if FUSION_WEAVER && UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using System.ComponentModel;
  using System.IO;
  using System.Text.RegularExpressions;
  using Photon.Realtime;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.UI;

  public partial class FusionHubWindow {
    /// <summary>
    /// Section Definition.
    /// </summary>
    internal class Section {
      public string Title;
      public string Description;
      public Action DrawMethod;
      public Icon Icon;

      public Section(string title, string description, Action drawMethod, Icon icon) {
        Title = title;
        Description = description;
        DrawMethod = drawMethod;
        Icon = icon;
      }
    }

    public enum Icon {
      Setup,
      Documentation,
      Samples,
      Community,
      ProductLogo,
      PhotonCloud,
      FusionIcon,
    }

    private static class Constants {
      public const string UrlFusionDocsOnline = "https://doc.photonengine.com/fusion/v2/";
      public const string UrlFusionIntro = "https://doc.photonengine.com/fusion/v2/getting-started/fusion-introduction";
      public const string UrlFusionSDK = "https://doc.photonengine.com/fusion/v2/getting-started/sdk-download";
      public const string UrlCloudDashboard = "https://id.photonengine.com/account/signin?email=";
      public const string UrlDiscordGeneral = "https://discord.gg/qP6XVe3XWK";
      public const string UrlDashboard = "https://dashboard.photonengine.com/";
      public const string UrlSampleSection = "https://doc.photonengine.com/fusion/v2/current/samples/overview";
      public const string UrlFusion100 = "https://doc.photonengine.com/fusion/v2/tutorials/shared-mode-basics/overview";
      public const string UrlFusionLoop = "https://doc.photonengine.com/fusion/v2/current/samples/fusion-application-loop";
      public const string UrlHelloFusion = "https://doc.photonengine.com/fusion/v2/current/hello-fusion/hello-fusion";
      public const string UrlHelloFusionVr = "https://doc.photonengine.com/fusion/v2/current/hello-fusion/hello-fusion-vr";
      public const string UrlTanks = "https://doc.photonengine.com/fusion/v2/current/samples/fusion-tanknarok";
      public const string UrlKarts = "https://doc.photonengine.com/fusion/v2/current/samples/fusion-karts";
      public const string UrlDragonHuntersVR = "https://doc.photonengine.com/fusion/v2/current/samples/fusion-dragonhunters-vr";

      public const string UrlFusionDocApi = "https://doc-api.photonengine.com/en/fusion/v2/index.html";
      public const string WindowTitle = "Photon Fusion 2 Hub";
      public const string Support = "You can contact the Photon Team using one of the following links. You can also go to Photon Documentation in order to get started.";
      public const string DiscordText = "Join the Discord.";
      public const string DiscordHeader = "Community";
      public const string DocumentationText = "Open the documentation.";
      public const string DocumentationHeader = "Documentation";

      public const string WelcomeText = "Thank you for installing Photon Fusion 2.\n\n" +
                                        "Once you have set up your Fusion 2 App Id, explore the sections on the left to get started. " +
                                        "More samples, tutorials, and documentation are being added regularly - so check back often.";

      public const string RealtimeAppidSetupInstructions =
        @"<b>An Fusion App Id Version 2 is required for networking.</b>

  To acquire an Fusion App Id:
  - Open the Photon Dashboard (Log-in as required).
  - Select an existing Fusion 2 App Id, or;
  - Create a new one (make sure to select SDK Version 2).
  - Copy the App Id and paste into the field below (or into the PhotonAppSettings.asset).
  ";

      public const string GettingStartedInstructions =
        @"Links to demos, tutorials, API references and other information can be found on the PhotonEngine.com website.";
    }

    public Texture2D SetupIcon;
    public Texture2D DocumentationIcon;
    public Texture2D SamplesIcon;
    public Texture2D CommunityIcon;
    public Texture2D ProductLogo;
    public Texture2D PhotonCloudIcon;
    public Texture2D FusionIcon;
    public Texture2D CorrectIcon;

    private Texture2D GetIcon(Icon icon) {
      switch (icon) {
        case Icon.Setup: return SetupIcon;
        case Icon.Documentation: return DocumentationIcon;
        case Icon.Samples: return SamplesIcon;
        case Icon.Community: return CommunityIcon;
        case Icon.ProductLogo: return ProductLogo;
        case Icon.PhotonCloud: return PhotonCloudIcon;
        case Icon.FusionIcon: return FusionIcon;
        default: return null;
      }
    }

    [NonSerialized] private Section[] _sections;

    private static string releaseHistoryHeader;
    private static List<string> releaseHistoryTextAdded;
    private static List<string> releaseHistoryTextChanged;
    private static List<string> releaseHistoryTextFixed;
    private static List<string> releaseHistoryTextRemoved;
    private static List<string> releaseHistoryTextInternal;

    private static string fusionReleaseHistory;

    public GUISkin FusionHubSkin;

    private static GUIStyle _navbarHeaderGraphicStyle;
    private static GUIStyle textLabelStyle;
    private static GUIStyle headerLabelStyle;
    private static GUIStyle releaseNotesStyle;
    private static GUIStyle headerTextStyle;
    private static GUIStyle buttonActiveStyle;

    private bool InitContent() {
      if (_ready.HasValue && _ready.Value) {
        return _ready.Value;
      }

      // skip while being loaded
      if (FusionHubSkin == null) { return false; }

      _sections = new[] {
        new Section("Welcome", "Welcome to Photon Fusion 2", DrawWelcomeSection, Icon.Setup), new Section("Fusion 2 Setup", "Setup Photon Fusion 2", DrawSetupSection, Icon.PhotonCloud),
        new Section("Tutorials & Samples", "Fusion Tutorials and Samples", DrawSamplesSection, Icon.Samples),
        new Section("Documentation", "Photon Fusion Documentation", DrawDocumentationSection, Icon.Documentation),
        new Section("Fusion Release Notes", "Fusion Release Notes", DrawFusionReleaseSection, Icon.Documentation),
        new Section("Support", "Support and Community Links", DrawSupportSection, Icon.Community),
      };

      Color commonTextColor = Color.white;

      var _guiSkin = FusionHubSkin;

      _navbarHeaderGraphicStyle = new GUIStyle(_guiSkin.button) { alignment = TextAnchor.MiddleCenter };

      headerTextStyle = new GUIStyle(_guiSkin.label) { fontSize = 18, padding = new RectOffset(12, 8, 8, 8), fontStyle = FontStyle.Bold, normal = { textColor = commonTextColor } };

      buttonActiveStyle = new GUIStyle(_guiSkin.button) { fontStyle = FontStyle.Bold, normal = { background = _guiSkin.button.active.background, textColor = Color.white } };


      textLabelStyle = new GUIStyle(_guiSkin.label) { wordWrap = true, normal = { textColor = commonTextColor }, richText = true, };
      headerLabelStyle = new GUIStyle(textLabelStyle) { fontSize = 15, };

      releaseNotesStyle = new GUIStyle(textLabelStyle) { richText = true, };

      return (_ready = true).Value;
    }

    private static Action OpenURL(string url, params object[] args) {
      return () => {
        if (args.Length > 0) {
          url = string.Format(url, args);
        }

        Application.OpenURL(url);
      };
    }

    protected static bool IsAppIdValid() {
      if (PhotonAppSettings.TryGetGlobal(out var global) && Guid.TryParse(global.AppSettings.AppIdFusion, out var guid)) {
        return true;
      }

      return false;
    }

    static string titleVersionReformat, sectionReformat, header1Reformat, header2Reformat, header3Reformat, classReformat;

    void InitializeFormatters() {
      titleVersionReformat = "<size=22><color=white>$1</color></size>";
      sectionReformat = "<i><color=lightblue>$1</color></i>";
      header1Reformat = "<size=22><color=white>$1</color></size>";
      header2Reformat = "<size=18><color=white>$1</color></size>";
      header3Reformat = "<b><color=#ffffaaff>$1</color></b>";
      classReformat = "<color=#FFDDBB>$1</color>";
    }

    /// <summary>
    /// Converts readme files into Unity RichText.
    /// </summary>
    private void PrepareReleaseHistoryText() {
      if (sectionReformat == null || sectionReformat == "") {
        InitializeFormatters();
      }

      // Fusion
      {
        try {
          var filePath = BuildPath(Application.dataPath, "Photon", "Fusion", "release_history.txt");
          var text = (TextAsset)AssetDatabase.LoadAssetAtPath(filePath, typeof(TextAsset));
          var baseText = text.text;

          // #
          baseText = Regex.Replace(baseText, @"^# (.*)", titleVersionReformat);
          baseText = Regex.Replace(baseText, @"(?<=\n)# (.*)", header1Reformat);
          // ##
          baseText = Regex.Replace(baseText, @"(?<=\n)## (.*)", header2Reformat);
          // ###
          baseText = Regex.Replace(baseText, @"(?<=\n)### (.*)", header3Reformat);
          // **Changes**
          baseText = Regex.Replace(baseText, @"(?<=\n)\*\*(.*)\*\*", sectionReformat);
          // `Class`
          baseText = Regex.Replace(baseText, @"\`([^\`]*)\`", classReformat);

          fusionReleaseHistory = baseText;
        } catch {
          fusionReleaseHistory = "Unable to load Release History.";
        }
      }

      // Realtime
      {
        try {
          var filePath = BuildPath(Application.dataPath, "Photon", "PhotonRealtime", "Code", "changes-realtime.txt");

          var text = (TextAsset)AssetDatabase.LoadAssetAtPath(filePath, typeof(TextAsset));

          var baseText = text.text;

          var regexVersion = new Regex(@"Version (\d+\.?)*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexAdded = new Regex(@"\b(Added:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexChanged = new Regex(@"\b(Changed:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexUpdated = new Regex(@"\b(Updated:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexFixed = new Regex(@"\b(Fixed:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexRemoved = new Regex(@"\b(Removed:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexInternal = new Regex(@"\b(Internal:)(.*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);

          var matches = regexVersion.Matches(baseText);

          if (matches.Count > 0) {
            var currentVersionMatch = matches[0];
            var lastVersionMatch = currentVersionMatch.NextMatch();

            if (currentVersionMatch.Success && lastVersionMatch.Success) {
              Func<MatchCollection, List<string>> itemProcessor = (match) => {
                List<string> resultList = new List<string>();
                for (int index = 0; index < match.Count; index++) {
                  resultList.Add(match[index].Groups[2].Value.Trim());
                }

                return resultList;
              };

              string mainText = baseText.Substring(currentVersionMatch.Index + currentVersionMatch.Length,
                lastVersionMatch.Index - lastVersionMatch.Length - 1).Trim();

              releaseHistoryHeader = currentVersionMatch.Value.Trim();
              releaseHistoryTextAdded = itemProcessor(regexAdded.Matches(mainText));
              releaseHistoryTextChanged = itemProcessor(regexChanged.Matches(mainText));
              releaseHistoryTextChanged.AddRange(itemProcessor(regexUpdated.Matches(mainText)));
              releaseHistoryTextFixed = itemProcessor(regexFixed.Matches(mainText));
              releaseHistoryTextRemoved = itemProcessor(regexRemoved.Matches(mainText));
              releaseHistoryTextInternal = itemProcessor(regexInternal.Matches(mainText));
            }
          }
        } catch {
          releaseHistoryHeader = "\nPlease look the file changes-realtime.txt";
          releaseHistoryTextAdded = new List<string>();
          releaseHistoryTextChanged = new List<string>();
          releaseHistoryTextFixed = new List<string>();
          releaseHistoryTextRemoved = new List<string>();
          releaseHistoryTextInternal = new List<string>();
        }
      }
    }

    public static bool Toggle(bool value) {
      var toggle = new GUIStyle("Toggle") { margin = new RectOffset(), padding = new RectOffset() };

      return EditorGUILayout.Toggle(value, toggle, GUILayout.Width(15));
    }

    private static string BuildPath(params string[] parts) {
      var basePath = "";

      foreach (var path in parts) {
        basePath = Path.Combine(basePath, path);
      }

      return basePath.Replace(Application.dataPath, Path.GetFileName(Application.dataPath));
    }
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Editor/FusionInstaller.cs

namespace Fusion.Editor {
#if !FUSION_DEV
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEditor.PackageManager;
  using UnityEngine;

  [InitializeOnLoad]
  class FusionInstaller {
    const string DEFINE_VERSION = "FUSION2";
    const string DEFINE = "FUSION_WEAVER";
    const string PACKAGE_TO_SEARCH = "nuget.mono-cecil";
    const string PACKAGE_TO_INSTALL = "com.unity.nuget.mono-cecil@1.10.2";
    const string PACKAGES_DIR = "Packages";
    const string MANIFEST_FILE = "manifest.json";

    static FusionInstaller() {

#if UNITY_SERVER
      var defines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Server);
#else
      var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
      var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif

      // Check for Defines
      // change based on https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2249
      if (defines.Contains(DEFINE) && defines.Contains(DEFINE_VERSION)) {
        return;
      }

      if (!PlayerSettings.runInBackground) {
        FusionEditorLog.LogInstaller($"Setting {nameof(PlayerSettings)}.{nameof(PlayerSettings.runInBackground)} to true");
        PlayerSettings.runInBackground = true;
      }

      var manifest = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, PACKAGES_DIR, MANIFEST_FILE);

      if (File.ReadAllText(manifest).Contains(PACKAGE_TO_SEARCH)) {
        FusionEditorLog.LogInstaller($"Setting '{DEFINE}' & '{DEFINE_VERSION}' Define");

        // append defines
        if (defines.Contains(DEFINE) == false) { defines = $"{defines};{DEFINE}"; }
        if (defines.Contains(DEFINE_VERSION) == false) { defines = $"{defines};{DEFINE_VERSION}"; }
        
#if UNITY_SERVER
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Server, defines);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
#endif
      } else {
        FusionEditorLog.LogInstaller($"Installing '{PACKAGE_TO_INSTALL}' package");
        Client.Add(PACKAGE_TO_INSTALL);
      }
    }
  }
#endif
}


#endregion


#region Assets/Photon/Fusion/Editor/FusionSceneSetupAssistants.cs

namespace Fusion.Editor {
  using UnityEditor;

  using UnityEngine;
  using UnityEngine.SceneManagement;
  using System.Collections.Generic;

  public static class FusionSceneSetupAssistants {

    [MenuItem("Tools/Fusion/Scene/Setup Networking in the Scene", false, FusionAssistants.PRIORITY_LOW + 1)]
    [MenuItem("GameObject/Fusion/Scene/Setup Networking in the Scene", false, FusionAssistants.PRIORITY + 1)]
    public static void AddNetworkingToScene() {
      (FusionBootstrap nds, NetworkRunner nr) n = AddNetworkStartup();
      n.nr.gameObject.EnsureComponentExists<NetworkEvents>();

      // Get scene and mark scene as dirty.
      DirtyAndSaveScene(n.nds.gameObject.scene);
    }

    public static (FusionBootstrap, NetworkRunner) AddNetworkStartup() {
      // Restrict to single AudioListener to disallow multiple active in shared instance mode (preventing log spam)
      HandleAudioListeners();
      
      // Restrict lights to single active instances node to Lights 
      HandleLights();

      // Add NetworkDebugRunner if missing
      var nds = FusionAssistants.EnsureExistsInScene<FusionBootstrap>("Prototype Network Start");

      NetworkRunner nr = nds.RunnerPrefab == null ? null : nds.RunnerPrefab.TryGetComponent<NetworkRunner>(out var found) ? found : null;
      // Add NetworkRunner to scene if the DebugStart doesn't have one as a prefab set already.
      if (nr == null) {

        // Add NetworkRunner to scene if NetworkDebugStart doesn't have one set as a prefab already.
        nr = FusionAssistants.EnsureExistsInScene<NetworkRunner>("Prototype Runner");

        nds.RunnerPrefab = nr;
        // The runner go is also our fallback spawn point... so raise it into the air a bit
        nr.transform.position = new Vector3(0, 3, 0);
      }

      return (nds, nr);
    }

    [MenuItem("Tools/Fusion/Scene/Add Current Scene To Build Settings", false, FusionAssistants.PRIORITY_LOW)]
    [MenuItem("GameObject/Fusion/Scene/Add Current Scene To Build Settings", false, FusionAssistants.PRIORITY)]
    public static void AddCurrentSceneToSettings() { DirtyAndSaveScene(SceneManager.GetActiveScene()); }
    public static void DirtyAndSaveScene(Scene scene) {

      UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
      var scenename = scene.path;

      // Give chance to save - required in order to build out. If users cancel will only be able to run in the editor.
      if (scenename == "") {
        UnityEditor.SceneManagement.EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new Scene[] { scene });
        scenename = scene.path;
      }

      // Add scene to Build and Fusion settings
      if (scenename != "") {
        scene.AddSceneToBuildSettings();
      }
    }

    [MenuItem("Tools/Fusion/Scene/Setup Multi-Peer AudioListener Handling", false, FusionAssistants.PRIORITY_LOW + 1)]
    [MenuItem("GameObject/Fusion/Scene/Setup Multi-Peer AudioListener Handling", false, FusionAssistants.PRIORITY + 1)]
    public static void HandleAudioListeners() {
      int count = 0;
      foreach (var listener in Object.FindObjectsOfType<AudioListener>()) {
        count++;
        listener.EnsureComponentHasVisibilityNode();
      }
      Debug.Log($"{count} {nameof(AudioListener)}(s) found and given a {nameof(RunnerVisibilityLink)} component.");
    }
    
    [MenuItem("Tools/Fusion/Scene/Setup Multi-Peer Lights Handling", false, FusionAssistants.PRIORITY_LOW + 1)]
    [MenuItem("GameObject/Fusion/Scene/Setup Multi-Peer Lights Handling", false, FusionAssistants.PRIORITY + 1)]
    public static void HandleLights() {
      int count = 0;
      foreach (var listener in Object.FindObjectsOfType<Light>()) {
        count++;
        listener.EnsureComponentHasVisibilityNode();
      }
      Debug.Log($"{count} {nameof(Light)}(s) found and given a {nameof(RunnerVisibilityLink)} component.");
    }

    public static void AddSceneToBuildSettings(this Scene scene) {
      var buildScenes = EditorBuildSettings.scenes;
      bool isInBuildScenes = false;
      foreach (var bs in buildScenes) {
        if (bs.path == scene.path) {
          isInBuildScenes = true;
          break;
        }
      }
      if (isInBuildScenes == false) {
        var buildList = new List<EditorBuildSettingsScene>();
        buildList.Add(new EditorBuildSettingsScene(scene.path, true));
        buildList.AddRange(buildScenes);
        Debug.Log($"Added '{scene.path}' as first entry in Build Settings.");
        EditorBuildSettings.scenes = buildList.ToArray();
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/FusionUnitySurrogateBaseWrapper.cs

﻿namespace Fusion.Editor {
  using System;
  using Internal;
  using UnityEditor;
  using UnityEngine;

  internal class FusionUnitySurrogateBaseWrapper : ScriptableObject {
    [SerializeReference]
    public UnitySurrogateBase Surrogate;
    [NonSerialized]
    public SerializedProperty SurrogateProperty;
    [NonSerialized]
    public Type SurrogateType;
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/FusionWeaverTriggerImporter.cs

namespace Fusion.Editor {
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(1, ExtensionWithoutDot, NetworkProjectConfigImporter.ImportQueueOffset + 1)]
  public class FusionWeaverTriggerImporter : ScriptedImporter {
    public const string DependencyName = "FusionILWeaverTriggerImporter/ConfigHash";
    public const string Extension = "." + ExtensionWithoutDot;
    public const string ExtensionWithoutDot = "fusionweavertrigger";

    public override void OnImportAsset(AssetImportContext ctx) {
      ctx.DependsOnCustomDependency(DependencyName);
      ILWeaverUtils.RunWeaver();
    }

    private static void RefreshDependencyHash() {
      var configPath = NetworkProjectConfigUtilities.GetGlobalConfigPath();
      if (string.IsNullOrEmpty(configPath)) {
        return;
      }

      try {
        var cfg = NetworkProjectConfigImporter.LoadConfigFromFile(configPath);
        var hash = new Hash128();

        foreach (var path in cfg.AssembliesToWeave) {
          hash.Append(path);
        }

        hash.Append(cfg.UseSerializableDictionary ? 1 : 0);
        hash.Append(cfg.NullChecksForNetworkedProperties ? 1 : 0);
        hash.Append(cfg.CheckRpcAttributeUsage ? 1 : 0);
        hash.Append(cfg.CheckNetworkedPropertiesBeingEmpty ? 1 : 0);

        AssetDatabase.RegisterCustomDependency(DependencyName, hash);
        AssetDatabase.Refresh();
      } catch {
        // ignore the error
      }
    }

    private class Postprocessor : AssetPostprocessor {
      private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        foreach (var path in importedAssets) {
          if (path.EndsWith(NetworkProjectConfigImporter.Extension)) {
            EditorApplication.delayCall -= RefreshDependencyHash;
            EditorApplication.delayCall += RefreshDependencyHash;
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/ILWeaverUtils.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEditor.Compilation;
  
  [InitializeOnLoad]
  public static class ILWeaverUtils {
    [MenuItem("Tools/Fusion/Run Weaver")]
    public static void RunWeaver() {

      CompilationPipeline.RequestScriptCompilation(
#if UNITY_2021_1_OR_NEWER
        RequestScriptCompilationOptions.CleanBuildCache
#endif
      );
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/NetworkBehaviourEditor.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(NetworkBehaviour), true)]
  [CanEditMultipleObjects]
  public class NetworkBehaviourEditor : BehaviourEditor {

    internal const string NETOBJ_REQUIRED_WARN_TEXT = "This <b>" + nameof(NetworkBehaviour) + "</b> requires a <b>" + nameof(NetworkObject) + "</b> component to function.";

    IEnumerable<NetworkBehaviour> ValidTargets => targets
      .Cast<NetworkBehaviour>()
      .Where(x => x.Object && x.Object.IsValid && x.Object.gameObject.activeInHierarchy);

    [NonSerialized]
    int[] _buffer = Array.Empty<int>();

    
    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();

      bool hasBeenApplied = false;
#if !FUSION_DISABLE_NBEDITOR_PRESERVE_BACKING_FIELDS
      // serialize unchanged serialized state into zero-initialized memory;
      // this makes sure defaults are preserved
      TransferBackingFields(backingFieldsToState: true);
#endif
      try {

        // after the original values have been saved, they can be overwritten with
        // whatever is in the state
        foreach (var target in ValidTargets) {
          target.CopyStateToBackingFields();
        }

        // move C# fields to SerializedObject
        serializedObject.UpdateIfRequiredOrScript();

        EditorGUI.BeginChangeCheck();

        base.DrawDefaultInspector();
        
        if (EditorGUI.EndChangeCheck()) {
          // serialized properties -> C# fields
          serializedObject.ApplyModifiedProperties();
          hasBeenApplied = true;

          // C# fields -> state
          foreach (var target in ValidTargets) {
            if (target.Object.HasStateAuthority) {
              target.CopyBackingFieldsToState(false);
            }
          }
          
        }
      } finally {
#if !FUSION_DISABLE_NBEDITOR_PRESERVE_BACKING_FIELDS
        // now restore the default values
        TransferBackingFields(backingFieldsToState: false);
        serializedObject.Update();
        if (hasBeenApplied) {
          serializedObject.ApplyModifiedProperties();
        }
      }
#endif

      DrawNetworkObjectCheck();
      DrawEditorButtons();
    }

    unsafe bool TransferBackingFields(bool backingFieldsToState) {

      if (Allocator.REPLICATE_WORD_SIZE == sizeof(int)) {
        int offset = 0;
        bool hadChanges = false;

        int requiredSize = ValidTargets.Sum(x => x.WordCount);
        if (backingFieldsToState) {
          if (_buffer.Length >= requiredSize) {
            Array.Clear(_buffer, 0, _buffer.Length);
          } else {
            _buffer = new int[requiredSize];
          }
        } else {
          if (_buffer.Length < requiredSize) {
            throw new InvalidOperationException("Buffer is too small");
          }
        }

        fixed (int* p = _buffer) {
          foreach (var target in ValidTargets) {
            var ptr = target.Ptr;

            try {
              target.Ptr = p + offset;
              if (backingFieldsToState) {
                target.CopyBackingFieldsToState(false);
              } else {
                target.CopyStateToBackingFields();
              }
              
              if (!hadChanges) {
                if (Native.MemCmp(target.Ptr, ptr, target.WordCount * Allocator.REPLICATE_WORD_SIZE) != 0) {
                  hadChanges = true;
                }
              }

            } finally {
              target.Ptr = ptr;
            }

            offset += target.WordCount;
          }
        }

        return hadChanges;
      }
    }
    

    /// <summary>
    /// Checks if GameObject or parent GameObject has a NetworkObject, and draws a warning and buttons for adding one if not.
    /// </summary>
    /// <param name="nb"></param>
    void DrawNetworkObjectCheck() {
      var targetsWithoutNetworkObjects = targets.Cast<NetworkBehaviour>().Where(x => x.transform.GetParentComponent<NetworkObject>() == false).ToList();
      if (targetsWithoutNetworkObjects.Any()) {

        using (new FusionEditorGUI.WarningScope(NETOBJ_REQUIRED_WARN_TEXT, 6f)) {
          IEnumerable<GameObject> gameObjects = null;

          if (GUI.Button(EditorGUILayout.GetControlRect(false, 22), "Add Network Object")) {
            gameObjects = targetsWithoutNetworkObjects.Select(x => x.gameObject).Distinct();
          }

          if (GUI.Button(EditorGUILayout.GetControlRect(false, 22), "Add Network Object to Root")) {
            gameObjects = targetsWithoutNetworkObjects.Select(x => x.transform.root.gameObject).Distinct();
          }

          if (gameObjects != null) {
            foreach (var go in gameObjects) {
              Undo.AddComponent<NetworkObject>(go);
            }
          }
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/NetworkMecanimAnimatorEditor.cs

namespace Fusion.Editor {

  using UnityEditor;

  [CustomEditor(typeof(NetworkMecanimAnimator))]

  public class NetworkMecanimAnimatorEditor : BehaviourEditor {
    public override void OnInspectorGUI() {

      var na = target as NetworkMecanimAnimator;

      if (na != null) {
        AnimatorControllerTools.GetHashesAndNames(na, null, null, ref na.TriggerHashes, ref na.StateHashes);
        na.TotalWords = na.GetWordCount().words;
      }

      base.OnInspectorGUI();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/NetworkObjectBakerEditTime.cs

﻿namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  public class NetworkObjectBakerEditTime : NetworkObjectBaker {
    private Dictionary<Type, int?> _executionOrderCache = new Dictionary<Type, int?>();

    protected override bool TryGetExecutionOrder(MonoBehaviour obj, out int order) {
      // is there a cached value?
      if (_executionOrderCache.TryGetValue(obj.GetType(), out var orderNullable)) {
        order = orderNullable ?? default;
        return orderNullable != null;
      }

      var monoScript = UnityEditor.MonoScript.FromMonoBehaviour(obj);
      if (monoScript) {
        orderNullable = UnityEditor.MonoImporter.GetExecutionOrder(monoScript);
      } else {
        orderNullable = null;
      }

      _executionOrderCache.Add(obj.GetType(), orderNullable);
      order = orderNullable ?? default;
      return orderNullable != null;
    }

    protected override void SetDirty(MonoBehaviour obj) {
      EditorUtility.SetDirty(obj);
    }

    protected override uint GetSortKey(NetworkObject obj) {
      var  globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
      int hash     = 0;

      hash = HashCodeUtilities.GetHashCodeDeterministic(globalId.identifierType, hash);
      hash = HashCodeUtilities.GetHashCodeDeterministic(globalId.assetGUID, hash);
      hash = HashCodeUtilities.GetHashCodeDeterministic(globalId.targetObjectId, hash);
      hash = HashCodeUtilities.GetHashCodeDeterministic(globalId.targetPrefabId, hash);
      
      return (uint)hash;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/NetworkObjectEditor.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;
#if UNITY_2021_2_OR_NEWER
  using UnityEditor.SceneManagement;

#else
  using UnityEditor.Experimental.SceneManagement;
#endif

  [CustomEditor(typeof(NetworkObject), true)]
  [InitializeOnLoad]
  [CanEditMultipleObjects]
  public unsafe class NetworkObjectEditor : BehaviourEditor {
    private bool _runtimeInfoFoldout;

    private static PropertyInfo _isSpawnable   = typeof(NetworkObject).GetPropertyOrThrow(nameof(NetworkObject.IsSpawnable));
    private static FieldInfo    _networkTypeId = typeof(NetworkObject).GetFieldOrThrow(nameof(NetworkObject.NetworkTypeId));
    private static PropertyInfo _networkId     = typeof(NetworkObject).GetPropertyOrThrow<NetworkId>(nameof(NetworkObject.Id));
    private static FieldInfo    _nestingRoot   = typeof(NetworkObjectHeader).GetFieldOrThrow(nameof(NetworkObjectHeader.NestingRoot));
    private static FieldInfo    _nestingKey    = typeof(NetworkObjectHeader).GetFieldOrThrow(nameof(NetworkObjectHeader.NestingKey));
    private static PropertyInfo _InputAuthority   = typeof(NetworkObject).GetPropertyOrThrow(nameof(NetworkObject.InputAuthority));
    private static PropertyInfo _StateAuthority   = typeof(NetworkObject).GetPropertyOrThrow(nameof(NetworkObject.StateAuthority));
    private static PropertyInfo _HasInputAuthority = typeof(NetworkObject).GetPropertyOrThrow(nameof(NetworkObject.HasInputAuthority));
    private static PropertyInfo _HasStateAuthority = typeof(NetworkObject).GetPropertyOrThrow(nameof(NetworkObject.HasStateAuthority));


    [Obsolete("Use " + nameof(NetworkObjectBaker) + " instead", true)]
    public static bool BakeHierarchy(GameObject root, NetworkObjectGuid? prefabGuid, Action<object> setDirty = null, Func<NetworkObject, NetworkObjectGuid> guidProvider = null) {
      throw new NotImplementedException();
    }

    static string GetLoadInfoString(NetworkObjectGuid guid) {
      if (NetworkProjectConfigUtilities.TryGetGlobalPrefabSource(guid, out INetworkPrefabSource prefabSource)) {
        return prefabSource.Description;
      }

      return "Null";
    }
    


    public override void OnInspectorGUI() {
      FusionEditorGUI.InjectScriptHeaderDrawer(serializedObject);
      FusionEditorGUI.ScriptPropertyField(serializedObject);

      // these properties' isExpanded are going to be used for foldouts; that's the easiest
      // way to get quasi-persistent foldouts

      var flagsProperty = serializedObject.FindPropertyOrThrow(nameof(NetworkObject.Flags));
      var obj           = (NetworkObject)base.target;
      var netObjType    = typeof(NetworkObject);

      if (targets.Length == 1) {
        if (AssetDatabase.IsMainAsset(obj.gameObject) || PrefabStageUtility.GetPrefabStage(obj.gameObject)?.prefabContentsRoot == obj.gameObject) {
          Debug.Assert(!AssetDatabaseUtils.IsSceneObject(obj.gameObject));

          if (!obj.Flags.IsVersionCurrent()) {
            using (new FusionEditorGUI.WarningScope("Prefab needs to be re-imported.")) {
              if (GUILayout.Button("Reimport")) {
                string assetPath = PrefabStageUtility.GetPrefabStage(obj.gameObject)?.assetPath ?? AssetDatabase.GetAssetPath(obj.gameObject);
                Debug.Assert(!string.IsNullOrEmpty(assetPath));
                AssetDatabase.ImportAsset(assetPath);
              }
            }
          } else {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefab Settings", EditorStyles.boldLabel);

            // Is Spawnable
            {
              EditorGUI.BeginChangeCheck();

              bool spawnable = EditorGUI.Toggle(FusionEditorGUI.LayoutHelpPrefix(this, _isSpawnable), _isSpawnable.Name, !obj.Flags.IsIgnored());
              if (EditorGUI.EndChangeCheck()) {
                var value = obj.Flags.SetIgnored(!spawnable);
                serializedObject.FindProperty(nameof(NetworkObject.Flags)).intValue = (int)value;
                serializedObject.ApplyModifiedProperties();
              }

              string loadInfo = "---";
              if (spawnable) {
                string assetPath = PrefabStageUtility.GetPrefabStage(obj.gameObject)?.assetPath ?? AssetDatabase.GetAssetPath(obj.gameObject);
                if (!string.IsNullOrEmpty(assetPath)) {
                  var guid = AssetDatabase.AssetPathToGUID(assetPath);
                  loadInfo = GetLoadInfoString(NetworkObjectGuid.Parse(guid));
                }
              }

              EditorGUILayout.LabelField("Prefab Source", loadInfo);
            }
          }
        } else if (AssetDatabaseUtils.IsSceneObject(obj.gameObject)) {
          if (!obj.Flags.IsVersionCurrent()) {
            if (!EditorApplication.isPlaying) {
              using (new FusionEditorGUI.WarningScope("This object hasn't been baked yet. Save the scene or enter playmode.")) {
              }
            }
          }
        }
      }


      if (EditorApplication.isPlaying && targets.Length == 1) {
        EditorGUILayout.Space();
        flagsProperty.isExpanded = EditorGUILayout.Foldout(flagsProperty.isExpanded, "Runtime Info");
        if (flagsProperty.isExpanded) {
          using (new FusionEditorGUI.BoxScope(null, 1)) {
            EditorGUI.LabelField(FusionEditorGUI.LayoutHelpPrefix(this, _networkTypeId), _networkTypeId.Name, obj.NetworkTypeId.ToString());
            EditorGUILayout.Toggle("Is Valid", obj.IsValid);
            if (obj.IsValid) {
              EditorGUI.LabelField(FusionEditorGUI.LayoutHelpPrefix(this, _networkId), _networkId.Name, obj.Id.ToString());
              EditorGUILayout.IntField("Word Count", NetworkObject.GetWordCount(obj));


              bool headerIsNull = obj.Header == null;
              EditorGUI.LabelField(FusionEditorGUI.LayoutHelpPrefix(this, _nestingRoot), _nestingRoot.Name, headerIsNull ? "---" : obj.Header->NestingRoot.ToString());
              EditorGUI.LabelField(FusionEditorGUI.LayoutHelpPrefix(this, _nestingKey), _nestingKey.Name, headerIsNull ? "---" : obj.Header->NestingKey.ToString());

              EditorGUI.LabelField(FusionEditorGUI.LayoutHelpPrefix(this, _InputAuthority), _InputAuthority.Name, obj.InputAuthority.ToString());
              EditorGUI.LabelField(FusionEditorGUI.LayoutHelpPrefix(this, _StateAuthority), _StateAuthority.Name, obj.StateAuthority.ToString());

              EditorGUI.Toggle(FusionEditorGUI.LayoutHelpPrefix(this, _HasInputAuthority), _InputAuthority.Name, obj.HasInputAuthority);
              EditorGUI.Toggle(FusionEditorGUI.LayoutHelpPrefix(this, _HasStateAuthority), _StateAuthority.Name, obj.HasStateAuthority);

              EditorGUILayout.Toggle("Is Simulated", obj.Runner.Simulation.IsSimulated(obj));
              EditorGUILayout.Toggle("Is Local PlayerObject", ReferenceEquals(obj.Runner.GetPlayerObject(obj.Runner.LocalPlayer), obj));
              EditorGUILayout.Toggle("Has Main TRSP", obj.Meta.HasMainTRSP);
              
              EditorGUILayout.LabelField("Runtime Flags", obj.RuntimeFlags.ToString());
              EditorGUILayout.LabelField("Header Flags", obj.Header->Flags.ToString());
              

              if (obj.Runner.IsClient) {
                EditorGUILayout.IntField("Last Received Tick", obj.LastReceiveTick);
              }
            }
          }
        }
      }

      EditorGUI.BeginChangeCheck();

      var config    = NetworkProjectConfig.Global;
      var isPlaying = EditorApplication.isPlaying;

      void DrawToggleFlag(NetworkObjectFlags flag, string name, bool? force = null) {
        var x = (obj.Flags & flag) == flag;

        var r = EditorGUILayout.Toggle(name, x);
        if (r != x || (force.HasValue && r != force.Value)) {
          if (force.HasValue) {
            r = force.Value;
          }

          if (r) {
            obj.Flags |= flag;
          } else {
            obj.Flags &= ~flag;
          }

          EditorUtility.SetDirty(obj);
        }
      }

      using (new EditorGUI.DisabledScope(isPlaying)) {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shared Mode Settings", EditorStyles.boldLabel);

        DrawToggleFlag(NetworkObjectFlags.MasterClientObject, "Is Master Client Object");

        EditorGUI.BeginDisabledGroup((obj.Flags & NetworkObjectFlags.MasterClientObject) == NetworkObjectFlags.MasterClientObject);
        if ((obj.Flags & NetworkObjectFlags.MasterClientObject) == NetworkObjectFlags.MasterClientObject) {
          DrawToggleFlag(NetworkObjectFlags.AllowStateAuthorityOverride, "Allow State Authority Override", false);
        } else {
          DrawToggleFlag(NetworkObjectFlags.AllowStateAuthorityOverride, "Allow State Authority Override");
        }

        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup((obj.Flags & NetworkObjectFlags.AllowStateAuthorityOverride) == default);

        if ((obj.Flags & NetworkObjectFlags.MasterClientObject) == NetworkObjectFlags.MasterClientObject) {
          DrawToggleFlag(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves, "Destroy When State Authority Leaves", false);
        } else {
          if ((obj.Flags & NetworkObjectFlags.AllowStateAuthorityOverride) == NetworkObjectFlags.AllowStateAuthorityOverride) {
            DrawToggleFlag(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves, "Destroy When State Authority Leaves");
          } else {
            DrawToggleFlag(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves, "Destroy When State Authority Leaves", true);
          }
        }

        EditorGUI.EndDisabledGroup();

        //var destroyWhenStateAuthLeaves = serializedObject.FindProperty(nameof(NetworkObject.DestroyWhenStateAuthorityLeaves));
        //EditorGUILayout.PropertyField(destroyWhenStateAuthLeaves);
        //
        //var allowStateAuthorityOverride = serializedObject.FindProperty(nameof(NetworkObject.AllowStateAuthorityOverride));
        //EditorGUILayout.PropertyField(allowStateAuthorityOverride);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Interest Management Settings", EditorStyles.boldLabel);


        var objectInterest = serializedObject.FindProperty(nameof(NetworkObject.ObjectInterest));
        EditorGUILayout.PropertyField(objectInterest);

        if (objectInterest.intValue == (int)NetworkObject.ObjectInterestModes.AreaOfInterest) {
          //EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetworkObject.AreaOfInterestTransform)));
        }

        //using (new EditorGUI.IndentLevelScope()) {
        //  EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.DefaultInterestGroups)));
        //}
      }

      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }

      EditorGUILayout.Space();
      EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
      EditorGUILayout.Space();

      EditorGUILayout.LabelField("Baked Data", EditorStyles.boldLabel);
      using (new FusionEditorGUI.BoxScope(null, 1)) {
        using (new EditorGUI.DisabledScope(true)) {
          using (new FusionEditorGUI.ShowMixedValueScope(flagsProperty.hasMultipleDifferentValues)) {
            FusionEditorGUI.LayoutSelectableLabel(EditorGUIUtility.TrTextContent(nameof(obj.Flags)), obj.Flags.ToString());
            FusionEditorGUI.LayoutSelectableLabel(EditorGUIUtility.TrTextContent(nameof(obj.SortKey)), obj.SortKey.ToString("X8"));
          }

          using (new EditorGUI.IndentLevelScope()) {
            EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.NestedObjects)));
            EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.NetworkedBehaviours)));
          }
        }
      }

      // Runtime buttons
      
      if (obj.Runner && obj.Runner.IsRunning) {
        
        EditorGUILayout.Space();
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
        EditorGUILayout.Space();
        
        // Input Authority Popup
        using (new EditorGUI.DisabledScope(obj.HasStateAuthority == false)) {
          var elements = GetInputAuthorityPopupContent(obj);
          
          var index = EditorGUILayout.Popup(_guiContentInputAuthority, elements.currentIndex, elements.content);
          if (index != elements.currentIndex) {
            obj.AssignInputAuthority(PlayerRef.FromIndex(elements.ids[index]));
          }
        }
        
        if (obj.Runner.GameMode == GameMode.Shared) {
          if (GUILayout.Button("Request State Authority")) {
            obj.RequestStateAuthority();
          }
        }
      
        if (GUILayout.Button("Despawn")) {
          obj.Runner.Despawn(obj);
        }
      }
    }
    
    private static bool Set<T>(UnityEngine.Object host, ref T field, T value, Action<object> setDirty) {
      if (!EqualityComparer<T>.Default.Equals(field, value)) {
        Trace($"Object dirty: {host} ({field} vs {value})");
        setDirty?.Invoke(host);
        field = value;
        return true;
      } else {
        return false;
      }
    }

    private static bool Set<T>(UnityEngine.Object host, ref T[] field, List<T> value, Action<object> setDirty) {
      var comparer = EqualityComparer<T>.Default;
      if (field == null || field.Length != value.Count || !field.SequenceEqual(value, comparer)) {
        Trace($"Object dirty: {host} ({field} vs {value})");
        setDirty?.Invoke(host);
        field = value.ToArray();
        return true;
      } else {
        return false;
      }
    }

    [System.Diagnostics.Conditional("FUSION_EDITOR_TRACE")]
    private static void Trace(string msg) {
      Debug.Log($"[Fusion/NetworkObjectEditor] {msg}");
    }

    public static NetworkObjectGuid GetPrefabGuid(NetworkObject prefab) {
      if (prefab == null) {
        throw new ArgumentNullException(nameof(prefab));
      }

      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out var guidStr, out long _)) {
        throw new ArgumentException($"No guid for {prefab}", nameof(prefab));
      }

      return NetworkObjectGuid.Parse(guidStr);
    }
    
    private static          GUIContent[] _reusableContent;
    private static          int[]        _reusablePlayerIds;
    private static readonly GUIContent   _guiContentEmpty = new GUIContent("");
    private static readonly GUIContent   _guiContentNone  = new GUIContent("None");
    private static readonly GUIContent   _guiContentInputAuthority  = new GUIContent("Input Authority");
    
    private static (int[] ids, GUIContent[] content, int currentIndex) GetInputAuthorityPopupContent(NetworkObject obj) {
      int requiredLength = obj.Runner.ActivePlayers.Count() + 2;
      if (_reusableContent == null || requiredLength > _reusableContent.Length) {
        _reusablePlayerIds    = new int[requiredLength];
        _reusablePlayerIds[0] = -1;
        _reusablePlayerIds[1] = 0;
        _reusableContent      = new GUIContent[requiredLength];
        _reusableContent[0]   = _guiContentNone;
        _reusableContent[1]   = _guiContentEmpty;
      }

      int indexOfCurrentPlayer = 0;
        
      // clear
      for (int i = 2; i < _reusableContent.Length; i++) {
        _reusableContent[i] = _guiContentEmpty;
      }
        
      int index = 2;
        
      foreach (var player in obj.Runner.ActivePlayers) {
        _reusablePlayerIds[index] = player.PlayerId;
        _reusableContent[index]   = new GUIContent($"Player {player.PlayerId}");
        if (player.PlayerId == obj.InputAuthority.PlayerId) {
          indexOfCurrentPlayer = index;
        }
        index++;
      }
      return (_reusablePlayerIds, _reusableContent, indexOfCurrentPlayer);
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/NetworkObjectPostprocessor.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditor.Build.Reporting;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  public class NetworkObjectPostprocessor : AssetPostprocessor {

    public static event Action<NetworkObjectBakePrefabArgs> OnBakePrefab;
    public static event Action<NetworkObjectBakeSceneArgs> OnBakeScene;
    
    private static HashSet<string> s_knownSpawnablePrefabs = new HashSet<string>();

    static NetworkObjectPostprocessor() {
      EditorSceneManager.sceneSaving += OnSceneSaving;
      EditorApplication.playModeStateChanged += OnPlaymodeChange;
    }
    
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      FusionEditorLog.TraceImport($"Postprocessing imported assets [{importedAssets.Length}]:\n{string.Join("\n", importedAssets)}");

      bool configPossiblyDirty = false;

      foreach (var path in importedAssets) {
        if (!path.EndsWith(".prefab")) {
          continue;
        }

#if UNITY_2023_1_OR_NEWER
        if (Array.IndexOf(movedAssets, path) >= 0) {
          // attempting to bake a prefab that has been moved would hang the editor
          // TODO: add the bug tracker link once Unity has assigned one
          continue;
        }
#endif
        
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!go) {
          continue;
        }

        var no = go.GetComponent<NetworkObject>();
        if (no) {
          FusionEditorLog.TraceImport(path, "Was marked as dirty in OnPostprocessPrefab, need to rebake");
          if (BakePrefab(path, out var newRoot)) {
#if FUSION_DEV
              Debug.Assert(newRoot != null && newRoot == AssetDatabase.LoadMainAssetAtPath(path));
#endif
            go = newRoot;
            no = go.GetComponent<NetworkObject>();
          }
        }

        var isSpawnable = no && no.Flags.IsIgnored() == false;
        var isKnownSpawnablePrefab = isSpawnable && !s_knownSpawnablePrefabs.Add(path);
        
        if (AssetDatabaseUtils.SetLabel(go, NetworkProjectConfigImporter.FusionPrefabTag, isSpawnable)) {
          configPossiblyDirty = true;
          AssetDatabase.ImportAsset(path);
          FusionEditorLog.TraceImport(path, "Labels dirty, going to reimport the config, too");
        } else if (no) {
          if (!isKnownSpawnablePrefab) {
            // there's still a possible case of prefab being duplicated with the label already in place;
            configPossiblyDirty = true;
          }
          FusionEditorLog.TraceImport(path, "Labels up to date");
        }
      }

      if (configPossiblyDirty) {
        NetworkProjectConfigUtilities.ImportGlobalConfig();
      }
    }

    void OnPostprocessPrefab(GameObject prefab) {
      var no = prefab.GetComponent<NetworkObject>();

      if (no && no.IsSpawnable) {
        var existing = prefab.GetComponent<NetworkObjectPrefabData>();
        if (existing != null) {
          // this is likely a variant prefab, can't add the next one
          // also, component loses hide flags at this point, so they need to be restored
          // weirdly, this is the only case where altering a component in OnPostprocessPrefab works
          // without causing an import warning
          existing.Guid      = NetworkObjectGuid.Parse(AssetDatabase.AssetPathToGUID(context.assetPath));
          existing.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInInspector | HideFlags.NotEditable;
        } else {
          var indirect = prefab.AddComponent<NetworkObjectPrefabData>();
          indirect.Guid      =  NetworkObjectGuid.Parse(AssetDatabase.AssetPathToGUID(context.assetPath));
          indirect.hideFlags |= HideFlags.HideInInspector | HideFlags.NotEditable;
        }
      }
    }


    static bool BakePrefab(string prefabPath, out GameObject root) {

      root = null;

      var assetGuid = AssetDatabase.AssetPathToGUID(prefabPath);
      if (!NetworkObjectGuid.TryParse(assetGuid, out var guid)) {
        FusionEditorLog.ErrorImport(prefabPath, $"Unable to parse guid: \"{assetGuid}\", not going to bake");
        return false;
      }

      var stageGo = PrefabUtility.LoadPrefabContents(prefabPath);
      if (!stageGo) {
        FusionEditorLog.ErrorImport(prefabPath, $"Unable to load prefab contents");
        return false;
      }

      var sw = System.Diagnostics.Stopwatch.StartNew();

      try {
        bool dirty = false;
        bool baked = false;
        
        if (OnBakePrefab != null) {
          var args = new NetworkObjectBakePrefabArgs(_baker, stageGo, prefabPath);
          OnBakePrefab(args);
          if (args.Handled) {
            baked = true;
            dirty = args.IsPrefabDirty;
          } 
        }

        if (!baked) {
          dirty = _baker.Bake(stageGo).HadChanges;
        }
        
        FusionEditorLog.TraceImport(prefabPath, $"Baking took {sw.Elapsed}, changed: {dirty}");

        if (dirty) {
          root = PrefabUtility.SaveAsPrefabAsset(stageGo, prefabPath);
        }

        return root;
      } finally {
        PrefabUtility.UnloadPrefabContents(stageGo);
      }
    }

    private static NetworkObjectBaker _baker = new NetworkObjectBakerEditTime();

    private static void OnPlaymodeChange(PlayModeStateChange change) {
      if (change != PlayModeStateChange.ExitingEditMode) {
        return;
      }
      for (int i = 0; i < EditorSceneManager.sceneCount; ++i) {
        BakeScene(EditorSceneManager.GetSceneAt(i));
      }
    }

    private static void OnSceneSaving(Scene scene, string path) {
      BakeScene(scene);
    }


    [Obsolete("Use " + nameof(BakeScene) + " instead")]
    public static void BakeSceneObjects(Scene scene) => BakeScene(scene);

    [Obsolete("Use " + nameof(BakeAllOpenScenes) + " instead")]
    public static void BakeSceneObjects() => BakeAllOpenScenes();

    [MenuItem("Tools/Fusion/Scene/Bake Scene Objects", false, FusionAssistants.PRIORITY_LOW - 1)]
    [MenuItem("GameObject/Fusion/Scene/Bake Scene Objects", false, FusionAssistants.PRIORITY - 1)]
    public static void BakeAllOpenScenes() {
      for (int i = 0; i < SceneManager.sceneCount; ++i) {
        var scene = SceneManager.GetSceneAt(i);
        try {
          BakeScene(scene);
        } catch (Exception ex) {
          Debug.LogError($"Failed to bake scene {scene}: {ex}");
        }
      }
    }

    public static void BakeScene(Scene scene) {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      try {

        if (OnBakeScene != null) {
          var args = new NetworkObjectBakeSceneArgs(_baker, scene);
          OnBakeScene(args);
          if (args.Handled) {
            return;
          }
        }

        foreach (var root in scene.GetRootGameObjects()) {
          _baker.Bake(root);
        }
        
      } finally {
        FusionEditorLog.TraceImport(scene.path, $"Baking {scene} took: {sw.Elapsed}");
      }
    }
  }

  public class NetworkObjectBakePrefabArgs {
    public bool IsPrefabDirty { get; set; }
    public bool Handled { get; set; }
    public GameObject LoadedPrefabRoot { get; }
    public string Path { get; }
    public NetworkObjectBaker Baker { get; }

    public NetworkObjectBakePrefabArgs(NetworkObjectBaker baker, GameObject loadedPrefabRoot, string path) {
      LoadedPrefabRoot = loadedPrefabRoot;
      Path = path;
      Baker = baker;
    }
  }

  public class NetworkObjectBakeSceneArgs {
    public bool Handled { get; set; }
    public Scene Scene { get; }
    public NetworkObjectBaker Baker { get; }

    public NetworkObjectBakeSceneArgs(NetworkObjectBaker baker, Scene scene) {
      Scene = scene;
      Baker = baker;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/NetworkPrefabsInspector.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using Object = UnityEngine.Object;

  public class NetworkPrefabsInspector : EditorWindow {

    private Grid _grid = new Grid();
    
    [MenuItem("Tools/Fusion/Windows/Network Prefabs Inspector")]
    [MenuItem("Window/Fusion/Network Prefabs Inspector")]
    public static void ShowWindow() {
      var window = GetWindow<NetworkPrefabsInspector>(false, "Network Prefabs Inspector");
      window.Show();
    }
    
    private void OnEnable() {
      _grid.PrefabTable = NetworkProjectConfig.Global.PrefabTable;
      _grid.OnEnable();
    }

    private void OnInspectorUpdate() {
      _grid.OnInspectorUpdate();
    }

    private void OnGUI() {
      using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
        _grid.DrawToolbarReloadButton();
        _grid.DrawToolbarSyncSelectionButton();
        GUILayout.FlexibleSpace();
        
        EditorGUI.BeginChangeCheck();
        _grid.OnlyLoaded = GUILayout.Toggle(_grid.OnlyLoaded, "Loaded Only", EditorStyles.toolbarButton);
        if (EditorGUI.EndChangeCheck()) {
          _grid.ResetTree();
        }

        _grid.DrawToolbarSearchField();
      }

      var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
      _grid.OnGUI(rect);
    }
    

    private enum LoadState {
      NotLoaded,
      Loading,
      LoadedNoInstances,
      Loaded
    }

    [Serializable]
    private class InspectorTreeViewState : TreeViewState {
      public MultiColumnHeaderState HeaderState;
      public bool                   SyncSelection;
    }

    private class GridItem : FusionGridItem {
      private readonly NetworkPrefabId    _prefabId;
      private readonly NetworkPrefabTable _prefabTable;

      public GridItem(NetworkPrefabTable prefabTable, NetworkPrefabId prefabId) {
        _prefabId = prefabId;
        _prefabTable = prefabTable;
      }
      
      public int InstanceCount => _prefabTable.GetInstancesCount(_prefabId);

      public string Path => AssetDatabase.GUIDToAssetPath(Guid);

      public string Guid => Source?.AssetGuid.ToUnityGuidString() ?? "Null";

      public override Object TargetObject {
        get {
          if (Source?.AssetGuid.IsValid == true) {
            if (NetworkProjectConfigUtilities.TryGetPrefabEditorInstance(Source.AssetGuid, out var result)) {
              return result.gameObject;
            }
          }

          return null;
        }
      }

      public INetworkPrefabSource Source => _prefabTable.GetSource(_prefabId);

      public string Description {
        get => Source?.Description ?? "Null";
      }

      public LoadState LoadState {
        get {
          if (!_prefabTable.IsAcquired(_prefabId)) {
            return LoadState.NotLoaded;
          }

          if (!_prefabTable.GetSource(_prefabId).IsCompleted) {
            return LoadState.Loading;
          }
          
          if (_prefabTable.GetInstancesCount(_prefabId) == 0) {
            return LoadState.LoadedNoInstances;
          }

          return LoadState.Loaded;
        }
      }

      public NetworkPrefabId PrefabId => _prefabId;
    }

    [Serializable]
    class Grid : FusionGrid<GridItem> {

      [SerializeField]
      public NetworkPrefabTable PrefabTable;
      [SerializeField]
      public bool OnlyLoaded;

      public override int GetContentHash() {
        return PrefabTable?.Version ?? 0;
      }

      protected override IEnumerable<Column> CreateColumns() {
        yield return new() {
          headerContent = new GUIContent("State"),
          width = 40,
          autoResize = false,
          cellGUI = (item, rect, _, _) => {
            var icon = FusionEditorSkin.LoadStateIcon;
            string label = "";
            Color color;
            switch (item.LoadState) {
              case LoadState.Loaded:
                color = Color.green;
                label = item.InstanceCount.ToString();
                break;
              case LoadState.LoadedNoInstances:
                color = Color.yellow;
                label = "0";
                break;
              case LoadState.Loading:
                color = Color.yellow;
                color.a = 0.5f;
                label = "0";
                break;
              default:
                color = Color.gray;
                break;
            }

            using (new FusionEditorGUI.ContentColorScope(color)) {
              EditorGUI.LabelField(rect, new GUIContent(label, icon, item.LoadState.ToString()));
            }
          },
          getComparer = order => (a, b) => {
            var result = a.LoadState.CompareTo(b.LoadState) * order;
            if (result != 0) {
              return result;
            }
            return a.InstanceCount.CompareTo(b.InstanceCount) * order;
          },
        };
        yield return new() {
          headerContent = new GUIContent("Type"),
          width = 40,
          maxWidth = 40,
          minWidth = 40,
          cellGUI = (item, rect, _, _) => INetworkPrefabSourceDrawer.DrawThumbnail(rect, item.Source),
          getComparer = order => (a, b) => EditorUtility.NaturalCompare(a.Source?.GetType().Name ?? "", b.Source?.GetType().Name ?? "") * order,
        };
        yield return MakeSimpleColumn(x => x.PrefabId, new() {
          cellGUI = (item, rect, selected, focused) => TreeView.DefaultGUI.Label(rect, item.PrefabId.ToString(false, false), selected , focused),
          width = 50,
          autoResize = false
        });
        yield return MakeSimpleColumn(x => x.Path, new() {
          initiallySorted = true,
        });
        yield return MakeSimpleColumn(x => x.Guid, new() {
          initiallyVisible = false
        });
        yield return MakeSimpleColumn(x => x.Description, new() {
          initiallyVisible = false
        });
      }

      protected override IEnumerable<GridItem> CreateRows() {
        if (PrefabTable == null) {
          yield break;
        }

        for (int i = 0; i < PrefabTable.Prefabs.Count; ++i) {
          var prefabId = NetworkPrefabId.FromIndex(i);
          if (OnlyLoaded && !PrefabTable.IsAcquired(prefabId)) {
            continue;
          }
          yield return new GridItem(PrefabTable, NetworkPrefabId.FromIndex(i)) { id = (int)(i + 1) };
        }
      }

      protected override GenericMenu CreateContextMenu(GridItem item, TreeView treeView) {
        
        var menu = new GenericMenu();

        var selection = treeView.GetSelection()
         .Select(x => NetworkPrefabId.FromIndex(x-1))
         .ToList();

        var anyLoaded = selection.Any(x => PrefabTable.IsAcquired(x));
        var anyNotLoaded = selection.Any(x => !PrefabTable.IsAcquired(x));
        var anyInstances = selection.Any(x => PrefabTable.GetInstancesCount(x) > 0);
        var spawnerRunners = NetworkRunner.Instances.Where(x => x && x.IsRunning && x.CanSpawn).ToArray();
        
        var loadContent = new GUIContent("Load");
        var loadAsyncContent = new GUIContent("Load (async)");
        var unloadContent = new GUIContent("Unload");
        var selectInstancesContent = new GUIContent("Select Instances");
        var spawnContent = new GUIContent("Spawn");
        var spawnAsyncContent = new GUIContent("Spawn (async)");

        if (anyNotLoaded) {
          menu.AddItem(loadContent, false, () => {
            foreach (var id in selection) {
              PrefabTable.Load(id, isSynchronous: true);
            }
          });
          menu.AddItem(loadAsyncContent, false, () => {
            foreach (var id in selection) {
              PrefabTable.Load(id, isSynchronous: false);
            }
          });
        } else {
          menu.AddDisabledItem(loadContent);
          menu.AddDisabledItem(loadAsyncContent);
        }
        
        if (anyLoaded) {
          menu.AddItem(unloadContent, false, () => {
            foreach (var id in selection) {
              PrefabTable.Unload(id);
            }
          });
        } else {
          menu.AddDisabledItem(unloadContent);
        }

        if (anyInstances) {
          menu.AddItem(selectInstancesContent, false, () => {
            var lookup = new HashSet<NetworkObjectTypeId>(selection.Select(x => NetworkObjectTypeId.FromPrefabId(x)));
            Selection.objects = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
             .Where(x => x.NetworkTypeId.IsValid && lookup.Contains(x.NetworkTypeId))
             .Select(x => x.gameObject)
             .ToArray();
          });
        } else {
          menu.AddDisabledItem(selectInstancesContent);
        }

        menu.AddSeparator("");
        
        if (spawnerRunners.Any()) {
          if (spawnerRunners.Length > 1) {
            foreach (var runner in spawnerRunners.Where(x => x.CanSpawn)) {
              AddSpawnItems($"/{runner.name}", runner);
            }
          } else {
            AddSpawnItems($"", spawnerRunners[0]);
          }
        } else {
          menu.AddDisabledItem(spawnContent);
          menu.AddDisabledItem(spawnAsyncContent);
        }

        void AddSpawnItems(string s, NetworkRunner networkRunner) {
          menu.AddItem(new GUIContent($"{spawnContent.text}{s}"), false, () => {
            foreach (var id in selection) {
              networkRunner.TrySpawn(id, out _);
            }
          });
          menu.AddItem(new GUIContent($"{spawnAsyncContent.text}{s}"), false, () => {
            foreach (var id in selection) {
              networkRunner.SpawnAsync(id);
            }
          });
        }

        return menu;
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/NetworkPrefabSourceFactories.cs

﻿namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  partial interface INetworkAssetSourceFactory {
    INetworkPrefabSource           TryCreatePrefabSource(in NetworkAssetSourceFactoryContext context);
  }

  public class NetworkAssetSourceFactory {
    private readonly List<INetworkAssetSourceFactory> _factories = TypeCache.GetTypesDerivedFrom<INetworkAssetSourceFactory>()
     .Select(x => (INetworkAssetSourceFactory)Activator.CreateInstance(x))
     .OrderBy(x => x.Order)
     .ToList();

    public INetworkPrefabSource TryCreatePrefabSource(in NetworkAssetSourceFactoryContext context, bool removeFaultedFactories = true) {
      for (int i = 0; i < _factories.Count; ++i) {
        var factory = _factories[i];

        try {
          var source = factory.TryCreatePrefabSource(in context);
          if (source != null) {
            return source;
          }
        } catch (Exception ex) when(removeFaultedFactories) {
          FusionEditorLog.Error($"Prefab source factory {factory.GetType().Name} failed for {context.AssetPath}. " +
            $"This factory will be removed from the list of available factories during this import." +
            $"Reimport of fix the underlying issue: {ex}");
        }
      }

      return null;   
    }
  }

  partial class NetworkAssetSourceFactoryStatic {
    public INetworkPrefabSource TryCreatePrefabSource(in NetworkAssetSourceFactoryContext context) {
      if (TryCreateInternal<NetworkPrefabSourceStaticLazy, NetworkObject>(context, out var result)) {
        result.AssetGuid = NetworkObjectGuid.Parse(context.AssetGuid);
      };
      return result;
    }
  }
  
  partial class NetworkAssetSourceFactoryResource {
    public INetworkPrefabSource TryCreatePrefabSource(in NetworkAssetSourceFactoryContext context) {
      if (TryCreateInternal<NetworkPrefabSourceResource, NetworkObject>(context, out var result)) {
        result.AssetGuid = NetworkObjectGuid.Parse(context.AssetGuid);
      };
      return result;
    }
  }
  
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
  partial class NetworkAssetSourceFactoryAddressable {
    public INetworkPrefabSource TryCreatePrefabSource(in NetworkAssetSourceFactoryContext context) {
      if (TryCreateInternal<NetworkPrefabSourceAddressable, NetworkObject>(context, out var result)) {
        result.AssetGuid = NetworkObjectGuid.Parse(context.AssetGuid);
      };
      return result;
    }
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Editor/NetworkRunnerEditor.cs

namespace Fusion.Editor {
  using System;
  using System.Linq;
  using System.Runtime.InteropServices;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(NetworkRunner))]
  public class NetworkRunnerEditor : BehaviourEditor {

    void Label<T>(string label, T value) {
      EditorGUILayout.LabelField(label, (value != null ? value.ToString() : "null"));
    }

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      var runner = target as NetworkRunner;
      if (runner && EditorApplication.isPlaying) {
        Label("State", runner.IsRunning ? "Running" : (runner.IsShutdown ? "Shutdown" : "None"));

        if (runner.IsRunning) {
          Label("Game Mode", runner.GameMode);
          Label("Simulation Mode", runner.Mode);
          Label("Is Player", runner.IsPlayer);
          Label("Local Player", runner.LocalPlayer);
          Label("Has Connection Token?", runner.GetPlayerConnectionToken() != null);

          var localplayerobj = runner.LocalPlayer.IsRealPlayer ? runner.GetPlayerObject(runner.LocalPlayer) : null;
          EditorGUILayout.ObjectField("Local PlayerObject", localplayerobj, typeof(NetworkObject), true);

          Label("Is SinglePlayer", runner.IsSinglePlayer);
 
          if (runner.TryGetSceneInfo(out var sceneInfo)) {
            Label("Scene Info", sceneInfo);
          } else {
            Label("Scene Info", $"Invalid");
          }
          
          var playerCount = runner.ActivePlayers.Count();
          Label("Active Players", playerCount);

          if (runner.IsServer && playerCount > 0) {
            foreach (var item in runner.ActivePlayers) {

              // skip local player
              if (runner.LocalPlayer == item) { continue; }

              Label("Player:PlayerId", item.PlayerId);
              Label("Player:ConnectionType", runner.GetPlayerConnectionType(item));
              Label("Player:UserId", runner.GetPlayerUserId(item));
              Label("Player:RTT", runner.GetPlayerRtt(item));
            }
          }

          if (runner.IsClient) {
            Label("Is Connected To Server", runner.IsConnectedToServer);
            Label("Current Connection Type", runner.CurrentConnectionType);
          }
        }

        Label("Is Cloud Ready", runner.IsCloudReady);

        if (runner.IsCloudReady) {

          Label("Is Shared Mode Master Client", runner.IsSharedModeMasterClient);
          Label("UserId", runner.UserId);
          Label("AuthenticationValues", runner.AuthenticationValues);

          Label("SessionInfo:IsValid", runner.SessionInfo.IsValid);

          if (runner.SessionInfo.IsValid) {
            Label("SessionInfo:Name", runner.SessionInfo.Name);
            Label("SessionInfo:IsVisible", runner.SessionInfo.IsVisible);
            Label("SessionInfo:IsOpen", runner.SessionInfo.IsOpen);
            Label("SessionInfo:Region", runner.SessionInfo.Region);
          }

          Label("LobbyInfo:IsValid", runner.LobbyInfo.IsValid);

          if (runner.LobbyInfo.IsValid) {
            Label("LobbyInfo:Name", runner.LobbyInfo.Name);
            Label("LobbyInfo:Region", runner.LobbyInfo.Region);
          }
        }
      } else {
        if (runner.TryGetComponent<RunnerEnableVisibility>(out var _) == false) {
          EditorGUILayout.Space(2);
          if (GUI.Button(EditorGUILayout.GetControlRect(), $"Add {nameof(RunnerEnableVisibility)}")) {
            runner.gameObject.AddComponent<RunnerEnableVisibility>();
          }
        }

      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/NetworkSceneDebugStartEditor.cs

// file deleted

#endregion


#region Assets/Photon/Fusion/Editor/NetworkTRSPEditor.cs

namespace Fusion.Editor {

  using UnityEditor;

  [CustomEditor(typeof(NetworkTRSP), true)]
  public unsafe class NetworkTRSPEditor : NetworkBehaviourEditor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      var t = (NetworkTRSP)target;
      using (new EditorGUI.DisabledScope(true)) {
        if (t && t.StateBufferIsValid && t.CanReceiveRenderCallback) {
          var    found = t.Runner.TryFindObject(t.Data.Parent.Object, out var parent);
          EditorGUILayout.LabelField("Parent",  $"'{(found ? parent.name : "Not Available")}' : {t.Data.Parent.Object.ToString()}");
        }        
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Editor/PhotonAppSettingsEditor.cs

namespace Fusion.Editor {
  using System.Collections;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEditor;
  using Photon.Realtime;

  [CustomEditor(typeof(PhotonAppSettings))]
  public class PhotonAppSettingsEditor : Editor {

    public override void OnInspectorGUI() {
      FusionEditorGUI.InjectScriptHeaderDrawer(serializedObject);
      base.DrawDefaultInspector();
    }

    [MenuItem("Tools/Fusion/Realtime Settings", priority = 200)]
    public static void PingNetworkProjectConfigAsset() {
      EditorGUIUtility.PingObject(PhotonAppSettings.Global);
      Selection.activeObject = PhotonAppSettings.Global;
    }
  }

}



#endregion


#region Assets/Photon/Fusion/Editor/Utilities/AnimatorControllerTools.cs

// ---------------------------------------------------------------------------------------------
// <copyright>PhotonNetwork Framework for Unity - Copyright (C) 2020 Exit Games GmbH</copyright>
// <author>developer@exitgames.com</author>
// ---------------------------------------------------------------------------------------------

namespace Fusion.Editor {
  using System.Collections.Generic;
  using UnityEngine;

  using UnityEditor.Animations;
  using UnityEditor;

  /// <summary>
  /// Storage type for AnimatorController cached transition data, which is a bit different than basic state hashes
  /// </summary>
  [System.Serializable]
  public class TransitionInfo {
    public int index;
    public int hash;
    public int state;
    public int destination;
    public float duration;
    public float offset;
    public bool durationIsFixed;

    public TransitionInfo(int index, int hash, int state, int destination, float duration, float offset, bool durationIsFixed) {
      this.index = index;
      this.hash = hash;
      this.state = state;
      this.destination = destination;
      this.duration = duration;
      this.offset = offset;
      this.durationIsFixed = durationIsFixed;
    }
  }

  public static class AnimatorControllerTools {

    //// Attach methods to Fusion.Runtime NetworkedAnimator
    //[InitializeOnLoadMethod]
    //public static void RegisterFusionDelegates() {
    //  NetworkedAnimator.GetWordCountDelegate = GetWordCount;
    //}

    private static AnimatorController GetController(this Animator a) {
      
      RuntimeAnimatorController rac = a.runtimeAnimatorController;
      AnimatorOverrideController overrideController = rac as AnimatorOverrideController;

      /// recurse until no override controller is found
      while (overrideController != null) {
        rac = overrideController.runtimeAnimatorController;
        overrideController = rac as AnimatorOverrideController;
      }

      return rac as AnimatorController;
    }

    private static void GetTriggerNames(this AnimatorController ctr, List<string> namelist) {
      namelist.Clear();

      foreach (var p in ctr.parameters)
        if (p.type == AnimatorControllerParameterType.Trigger) {
          if (namelist.Contains(p.name)) {
            Debug.LogWarning("Identical Trigger Name Found.  Check animator on '" + ctr.name + "' for repeated trigger names.");
          } else
            namelist.Add(p.name);
        }
    }

    private static void GetTriggerNames(this AnimatorController ctr, List<int> hashlist) {
      hashlist.Clear();

      foreach (var p in ctr.parameters)
        if (p.type == AnimatorControllerParameterType.Trigger) {
          hashlist.Add(Animator.StringToHash(p.name));
        }
    }

    /// ------------------------------ STATES --------------------------------------

    private static void GetStatesNames(this AnimatorController ctr, List<string> namelist) {
      namelist.Clear();

      foreach (var l in ctr.layers) {
        var states = l.stateMachine.states;
        ExtractNames(ctr, l.name, states, namelist);

        var substates = l.stateMachine.stateMachines;
        ExtractSubNames(ctr, l.name, substates, namelist);
      }
    }

    private static void ExtractSubNames(AnimatorController ctr, string path, ChildAnimatorStateMachine[] substates, List<string> namelist) {
      foreach (var s in substates) {
        var sm = s.stateMachine;
        var subpath = path + "." + sm.name;

        ExtractNames(ctr, subpath, s.stateMachine.states, namelist);
        ExtractSubNames(ctr, subpath, s.stateMachine.stateMachines, namelist);
      }
    }

    private static void ExtractNames(AnimatorController ctr, string path, ChildAnimatorState[] states, List<string> namelist) {
      foreach (var st in states) {
        string name = st.state.name;
        string layerName = path + "." + st.state.name;
        if (!namelist.Contains(name)) {
          namelist.Add(name);
        }
        if (namelist.Contains(layerName)) {
          Debug.LogWarning("Identical State Name <i>'" + st.state.name + "'</i> Found.  Check animator on '" + ctr.name + "' for repeated State names as they cannot be used nor networked.");
        } else
          namelist.Add(layerName);
      }

    }

    private static void GetStatesNames(this AnimatorController ctr, List<int> hashlist) {
      hashlist.Clear();

      foreach (var l in ctr.layers) {
        var states = l.stateMachine.states;
        ExtractHashes(ctr, l.name, states, hashlist);

        var substates = l.stateMachine.stateMachines;
        ExtractSubtHashes(ctr, l.name, substates, hashlist);
      }

    }

    private static void ExtractSubtHashes(AnimatorController ctr, string path, ChildAnimatorStateMachine[] substates, List<int> hashlist) {
      foreach (var s in substates) {
        var sm = s.stateMachine;
        var subpath = path + "." + sm.name;

        ExtractHashes(ctr, subpath, sm.states, hashlist);
        ExtractSubtHashes(ctr, subpath, sm.stateMachines, hashlist);
      }
    }

    private static void ExtractHashes(AnimatorController ctr, string path, ChildAnimatorState[] states, List<int> hashlist) {
      foreach (var st in states) {
        int hash = Animator.StringToHash(st.state.name);
        string fullname = path + "." + st.state.name;
        int layrhash = Animator.StringToHash(fullname);
        if (!hashlist.Contains(hash)) {
          hashlist.Add(hash);
        }
        if (hashlist.Contains(layrhash)) {
          Debug.LogWarning("Identical State Name <i>'" + st.state.name + "'</i> Found.  Check animator on '" + ctr.name + "' for repeated State names as they cannot be used nor networked.");
        } else
          hashlist.Add(layrhash);
      }
    }

    //public static void GetTransitionNames(this AnimatorController ctr, List<string> transInfo)
    //{
    //	transInfo.Clear();

    //	transInfo.Add("0");

    //	foreach (var l in ctr.layers)
    //	{
    //		foreach (var st in l.stateMachine.states)
    //		{
    //			string sname = l.name + "." + st.state.name;

    //			foreach (var t in st.state.transitions)
    //			{
    //				string dname = l.name + "." + t.destinationState.name;
    //				string name = (sname + " -> " + dname);
    //				transInfo.Add(name);
    //				//Debug.Log(sname + " -> " + dname + "   " + Animator.StringToHash(sname + " -> " + dname));
    //			}
    //		}
    //	}

    //}


    //public static void GetTransitions(this AnimatorController ctr, List<TransitionInfo> transInfo)
    //{
    //	transInfo.Clear();

    //	transInfo.Add(new TransitionInfo(0, 0, 0, 0, 0, 0, false));

    //	int index = 1;

    //	foreach (var l in ctr.layers)
    //	{
    //		foreach (var st in l.stateMachine.states)
    //		{
    //			string sname = l.name + "." + st.state.name;
    //			int shash = Animator.StringToHash(sname);

    //			foreach (var t in st.state.transitions)
    //			{
    //				string dname = l.name + "." + t.destinationState.name;
    //				int dhash = Animator.StringToHash(dname);
    //				int hash = Animator.StringToHash(sname + " -> " + dname);
    //				TransitionInfo ti = new TransitionInfo(index, hash, shash, dhash, t.duration, t.offset, t.hasFixedDuration);
    //				transInfo.Add(ti);
    //				//Debug.Log(index + " " + sname + " -> " + dname + "   " + Animator.StringToHash(sname + " -> " + dname));
    //				index++;
    //			}
    //		}
    //	}
    //}

    const double AUTO_REBUILD_RATE = 10f;
    private static List<string> tempNamesList = new List<string>();
    private static List<int> tempHashList = new List<int>();
    
    // This method is a near copy of the code used in NMA for determining WordCount, but uses GetController instead.
    internal static (int paramCount, int boolCount, int layerCount, int words) GetWordCount(this NetworkMecanimAnimator netAnim) {
      // always get new Animator in case it has changed.
      Animator animator = netAnim.Animator;
      if (animator == null) {
        animator = netAnim.GetComponent<Animator>();
        if (animator == null) {
          return default;
        }

        // Add the animator we found
        netAnim.Animator = animator;
      }

      AnimatorController ac = animator.GetController();

      if (ac == null) {
        return default;
      }
      
      var settings       = netAnim.SyncSettings;
      int param32Count   = 0;
      int paramBoolCount = 0;

      bool includeI    = (settings & AnimatorSyncSettings.ParameterInts)     == AnimatorSyncSettings.ParameterInts;
      bool includeF    = (settings & AnimatorSyncSettings.ParameterFloats)   == AnimatorSyncSettings.ParameterFloats;
      bool includeB    = (settings & AnimatorSyncSettings.ParameterBools)    == AnimatorSyncSettings.ParameterBools;
      bool includeT    = (settings & AnimatorSyncSettings.ParameterTriggers) == AnimatorSyncSettings.ParameterTriggers;
      var  includeStat = (settings & AnimatorSyncSettings.StateRoot)         == AnimatorSyncSettings.StateRoot;
      var  includeWght = (settings & AnimatorSyncSettings.LayerWeights)      == AnimatorSyncSettings.LayerWeights;
      var  includeLyrs = (settings & AnimatorSyncSettings.StateLayers)       == AnimatorSyncSettings.StateLayers;
      
      var parameters = ac.parameters;
      for (int i = 0; i < parameters.Length; ++i) {
        var param = parameters[i];

        switch (param.type) {
          case AnimatorControllerParameterType.Int:
            if (includeI)
              param32Count++;
            break;
          case AnimatorControllerParameterType.Float:
            if (includeF)
              param32Count++;
            break;
          case AnimatorControllerParameterType.Bool:
            if (includeB)
              paramBoolCount++;
            break;
          case AnimatorControllerParameterType.Trigger:
            if (includeT)
              paramBoolCount++;
            break;
        }
      }

      int layerCount          = ac.layers.Length;
      int syncedLayerCount    = includeLyrs ? layerCount : 1;
      int stateWordCount      = includeStat ? 2 * syncedLayerCount : 0;
      int weightWordCount     = (includeWght && layerCount > 0) ? (layerCount - 1) : 0;
      int paramBoolsWordCount = (paramBoolCount                               + 31) >> 5;
      int words               = param32Count + paramBoolsWordCount + stateWordCount + weightWordCount;

      return (param32Count, paramBoolCount, layerCount, words);
    }
    
    /// <summary>
    /// Re-index all of the State and Trigger names in the current AnimatorController. Never hurts to run this (other than hanging the editor for a split second).
    /// </summary>
    internal static void GetHashesAndNames(this NetworkMecanimAnimator netAnim,
        List<string> sharedTriggNames,
        List<string> sharedStateNames,
        ref int[] sharedTriggIndexes,
        ref int[] sharedStateIndexes
        //ref double lastRebuildTime
        ) {

      // always get new Animator in case it has changed.
      Animator animator = netAnim.Animator;
      if (animator == null)
        animator = netAnim.GetComponent<Animator>();

      if (animator == null) {
        return;
      }
      //if (animator && EditorApplication.timeSinceStartup - lastRebuildTime > AUTO_REBUILD_RATE) {
      //  lastRebuildTime = EditorApplication.timeSinceStartup;

      AnimatorController ac = animator.GetController();
      if (ac != null) {
        if (ac.animationClips == null || ac.animationClips.Length == 0)
          Debug.LogWarning("'" + animator.name + "' has an Animator with no animation clips. Some Animator Controllers require a restart of Unity, or for a Build to be made in order to initialize correctly.");

        bool haschanged = false;

        ac.GetTriggerNames(tempHashList);
        tempHashList.Insert(0, 0);
        if (!CompareIntArray(sharedTriggIndexes, tempHashList)) {
          sharedTriggIndexes = tempHashList.ToArray();
          haschanged = true;
        }

        ac.GetStatesNames(tempHashList);
        tempHashList.Insert(0, 0);
        if (!CompareIntArray(sharedStateIndexes, tempHashList)) {
          sharedStateIndexes = tempHashList.ToArray();
          haschanged = true;
        }

        if (sharedTriggNames != null) {
          ac.GetTriggerNames(tempNamesList);
          tempNamesList.Insert(0, null);
          if (!CompareNameLists(tempNamesList, sharedTriggNames)) {
            CopyNameList(tempNamesList, sharedTriggNames);
            haschanged = true;
          }
        }

        if (sharedStateNames != null) {
          ac.GetStatesNames(tempNamesList);
          tempNamesList.Insert(0, null);
          if (!CompareNameLists(tempNamesList, sharedStateNames)) {
            CopyNameList(tempNamesList, sharedStateNames);
            haschanged = true;
          }
        }

        if (haschanged) {
          Debug.Log(animator.name + " has changed. SyncAnimator indexes updated.");
          EditorUtility.SetDirty(netAnim);
        }
      }
      //}
    }

    private static bool CompareNameLists(List<string> one, List<string> two) {
      if (one.Count != two.Count)
        return false;

      for (int i = 0; i < one.Count; i++)
        if (one[i] != two[i])
          return false;

      return true;
    }

    private static bool CompareIntArray(int[] old, List<int> temp) {
      if (ReferenceEquals(old, null))
        return false;

      if (old.Length != temp.Count)
        return false;

      for (int i = 0; i < old.Length; i++)
        if (old[i] != temp[i])
          return false;

      return true;
    }

    private static void CopyNameList(List<string> src, List<string> trg) {
      trg.Clear();
      for (int i = 0; i < src.Count; i++)
        trg.Add(src[i]);
    }

  }

}



#endregion


#region Assets/Photon/Fusion/Editor/Utilities/AssetDatabaseUtils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
#if UNITY_2021_2_OR_NEWER
  using UnityEditor.SceneManagement;
#else
  using UnityEditor.Experimental.SceneManagement;
#endif

  using UnityEngine;

  public static partial class AssetDatabaseUtils {
    public static T GetSubAsset<T>(GameObject prefab) where T : ScriptableObject {

      if (!AssetDatabase.IsMainAsset(prefab)) {
        throw new InvalidOperationException($"Not a main asset: {prefab}");
      }

      string path = AssetDatabase.GetAssetPath(prefab);
      if (string.IsNullOrEmpty(path)) {
        throw new InvalidOperationException($"Empty path for prefab: {prefab}");
      }

      var subAssets = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().ToList();
      if (subAssets.Count > 1) {
        Debug.LogError($"More than 1 asset of type {typeof(T)} on {path}, clean it up manually");
      }

      return subAssets.Count == 0 ? null : subAssets[0];
    }

    public static bool IsSceneObject(GameObject go) {
      return ReferenceEquals(PrefabStageUtility.GetPrefabStage(go), null) && (PrefabUtility.IsPartOfPrefabAsset(go) == false || PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab);
    }

    public static T SetScriptableObjectType<T>(ScriptableObject obj) where T : ScriptableObject {
      if (obj.GetType() == typeof(T)) {
        return (T)obj;
      }

      var tmp = ScriptableObject.CreateInstance(typeof(T));
      try {
        using (var dst = new SerializedObject(obj)) {
          using (var src = new SerializedObject(tmp)) {
            var scriptDst = dst.FindPropertyOrThrow(FusionEditorGUI.ScriptPropertyName);
            var scriptSrc = src.FindPropertyOrThrow(FusionEditorGUI.ScriptPropertyName);
            Debug.Assert(scriptDst.objectReferenceValue != scriptSrc.objectReferenceValue);
            dst.CopyFromSerializedProperty(scriptSrc);
            dst.ApplyModifiedPropertiesWithoutUndo();
            return (T)dst.targetObject;
          }
        }
      } finally {
        UnityEngine.Object.DestroyImmediate(tmp);
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/Utilities/FusionEditorGUI.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {

    public static void LayoutSelectableLabel(GUIContent label, string contents) {
      var rect = EditorGUILayout.GetControlRect();
      rect = EditorGUI.PrefixLabel(rect, label);
      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        EditorGUI.SelectableLabel(rect, contents);
      }
    }

    public static bool DrawDefaultInspector(SerializedObject obj, bool drawScript = true) {
      EditorGUI.BeginChangeCheck();
      obj.UpdateIfRequiredOrScript();

      // Loop through properties and create one field (including children) for each top level property.
      SerializedProperty property = obj.GetIterator();
      bool expanded = true;
      while (property.NextVisible(expanded)) {
        if ( ScriptPropertyName == property.propertyPath ) {
          if (drawScript) {
            using (new EditorGUI.DisabledScope("m_Script" == property.propertyPath)) {
              EditorGUILayout.PropertyField(property, true);
            }
          }
        } else {
          EditorGUILayout.PropertyField(property, true);
        }
        expanded = false;
      }

      obj.ApplyModifiedProperties();
      return EditorGUI.EndChangeCheck();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/Utilities/FusionEditorGUI.Thumbnail.cs

namespace Fusion.Editor {
  using System;
  using System.Text;
  using UnityEngine;

  public static partial class FusionEditorGUI {

    static readonly int _thumbnailFieldHash = "Thumbnail".GetHashCode();
    static Texture2D _thumbnailBackground;
    static GUIStyle _thumbnailStyle;

    public static void DrawTypeThumbnail(Rect position, Type type, string prefixToSkip, string tooltip = null) {
      EnsureThumbnailStyles();

      var acronym = GenerateAcronym(type, prefixToSkip);
      var content = new GUIContent(acronym, tooltip ?? type.FullName);
      int controlID = GUIUtility.GetControlID(_thumbnailFieldHash, FocusType.Passive, position);

      if (Event.current.type == EventType.Repaint) {
        var originalColor = GUI.backgroundColor;
        try {
          GUI.backgroundColor = GetPersistentColor(type.FullName);
          _thumbnailStyle.fixedWidth = position.width;
          _thumbnailStyle.Draw(position, content, controlID);
        } finally {
          GUI.backgroundColor = originalColor;
        }
      }
    }

    static Color GetPersistentColor(string str) {
      return GeneratePastelColor(HashCodeUtilities.GetHashDeterministic(str));
    }

    static Color GeneratePastelColor(int seed) {
      var rng = new System.Random(seed);
      int r = rng.Next(256) + 128;
      int g = rng.Next(256) + 128;
      int b = rng.Next(256) + 128;

      r = Mathf.Min(r / 2, 255);
      g = Mathf.Min(g / 2, 255);
      b = Mathf.Min(b / 2, 255);

      var result = new Color32((byte)r, (byte)g, (byte)b, 255);
      return result;
    }

    static string GenerateAcronym(Type type, string prefixToStrip) {
      StringBuilder acronymBuilder = new StringBuilder();

      var str = type.Name;
      if (!string.IsNullOrEmpty(prefixToStrip)) {
        if (str.StartsWith(prefixToStrip)) {
          str = str.Substring(prefixToStrip.Length);
        }
      }

      for (int i = 0; i < str.Length; ++i) {
        var c = str[i];
        if (i != 0 && char.IsLower(c)) {
          continue;
        }
        acronymBuilder.Append(c);
      }

      return acronymBuilder.ToString();
    }

    static void EnsureThumbnailStyles() {
      if (_thumbnailBackground != null) {
        return;
      }

      byte[] data = {
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x14,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x8d, 0x89, 0x1d, 0x0d, 0x00, 0x00, 0x00,
        0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xae, 0xce, 0x1c, 0xe9, 0x00, 0x00,
        0x00, 0x04, 0x67, 0x41, 0x4d, 0x41, 0x00, 0x00, 0xb1, 0x8f, 0x0b, 0xfc,
        0x61, 0x05, 0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00,
        0x0e, 0xc3, 0x00, 0x00, 0x0e, 0xc3, 0x01, 0xc7, 0x6f, 0xa8, 0x64, 0x00,
        0x00, 0x00, 0xf2, 0x49, 0x44, 0x41, 0x54, 0x38, 0x4f, 0xed, 0x95, 0x31,
        0x0a, 0x83, 0x30, 0x14, 0x86, 0x63, 0x11, 0x74, 0x50, 0x74, 0x71, 0xf1,
        0x34, 0x01, 0x57, 0x6f, 0xe8, 0xe0, 0xd0, 0xa5, 0x07, 0x10, 0x0a, 0xbd,
        0x40, 0x0f, 0xe2, 0xa8, 0x9b, 0xee, 0xf6, 0x7d, 0x69, 0x4a, 0xa5, 0xd2,
        0x2a, 0xa6, 0x4b, 0xa1, 0x1f, 0x04, 0x5e, 0xc2, 0xff, 0xbe, 0x68, 0x90,
        0xa8, 0x5e, 0xd0, 0x69, 0x9a, 0x9e, 0xc3, 0x30, 0x1c, 0xa4, 0x9e, 0x3e,
        0x0d, 0x32, 0x64, 0xa5, 0xd6, 0x32, 0x16, 0xf8, 0x51, 0x14, 0x1d, 0xb3,
        0x2c, 0x1b, 0xab, 0xaa, 0x9a, 0xda, 0xb6, 0x9d, 0xd6, 0x20, 0x43, 0x96,
        0x1e, 0x7a, 0x71, 0xdc, 0x55, 0x02, 0x0b, 0x45, 0x51, 0x0c, 0x82, 0x8d,
        0x6f, 0x87, 0x1e, 0x7a, 0xad, 0xd4, 0xa0, 0xd9, 0x65, 0x8f, 0xec, 0x01,
        0xbd, 0x38, 0x70, 0x29, 0xce, 0x81, 0x47, 0x77, 0x05, 0x87, 0x39, 0x53,
        0x0e, 0x77, 0xcb, 0x99, 0xad, 0x81, 0x03, 0x97, 0x27, 0x8f, 0xc9, 0xdc,
        0xbc, 0xbb, 0x2b, 0x9e, 0xe7, 0xa9, 0x83, 0xad, 0xbf, 0xc6, 0x5f, 0xe8,
        0xce, 0x0f, 0x08, 0xe5, 0x63, 0x1c, 0xfb, 0xbe, 0xb7, 0xd3, 0xfd, 0xe0,
        0xc0, 0x75, 0x08, 0x82, 0xe0, 0xda, 0x34, 0x8d, 0x5d, 0xde, 0x0f, 0x0e,
        0x5c, 0xd4, 0x3a, 0xcf, 0x73, 0xe7, 0xcb, 0x01, 0x07, 0x2e, 0x84, 0x2a,
        0x8e, 0xe3, 0x53, 0x59, 0x96, 0xbb, 0xa4, 0xf4, 0xd0, 0x8b, 0xc3, 0xc8,
        0x2c, 0x3e, 0x0b, 0xec, 0x52, 0xd7, 0xf5, 0xd4, 0x75, 0x9d, 0x8d, 0xbf,
        0x87, 0x0c, 0x59, 0x7a, 0xac, 0xec, 0x79, 0xc1, 0xce, 0xd0, 0x49, 0x92,
        0x5c, 0xb8, 0x35, 0xa4, 0x5e, 0x5c, 0xfb, 0xf3, 0x41, 0x86, 0xac, 0xd4,
        0xb3, 0x5f, 0x80, 0x52, 0x37, 0xfd, 0x56, 0x1b, 0x09, 0x40, 0x56, 0xe4,
        0x85, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60,
        0x82
      };

      var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
      if (!texture.LoadImage(data)) {
        throw new InvalidOperationException();
      }

      _thumbnailBackground = texture;

      _thumbnailStyle = new GUIStyle() {
        normal = new GUIStyleState { background = _thumbnailBackground, textColor = Color.white },
        border = new RectOffset(6, 6, 6, 6),
        padding = new RectOffset(2, 1, 1, 1),
        imagePosition = ImagePosition.TextOnly,
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Clip,
        wordWrap = true,
        stretchWidth = false,
        fontSize = 8,
        fontStyle = FontStyle.Bold,
        fixedWidth = texture.width,
      };

    }

  }
}


#endregion


#region Assets/Photon/Fusion/Editor/Utilities/NetworkProjectConfigUtilities.cs

namespace Fusion.Editor {

  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using System.Collections.Generic;
  using Fusion.Photon.Realtime;
  using System.Linq;
  using System.IO;
  using System;
  
  /// <summary>
  /// Editor utilities for creating and managing the <see cref="NetworkProjectConfigAsset"/> singleton.
  /// </summary>
  [InitializeOnLoad]
  public static class NetworkProjectConfigUtilities {

    // Constructor runs on project load, allows for startup check for existence of NPC asset.
    static NetworkProjectConfigUtilities() {
      EditorApplication.playModeStateChanged += (change) => {
        if (change == PlayModeStateChange.EnteredEditMode) {
          NetworkProjectConfig.UnloadGlobal();
        }
      };
    }

    [MenuItem("Tools/Fusion/Network Project Config", priority = 200)]
    static void PingNetworkProjectConfigAsset() {
      NetworkProjectConfigUtilities.PingGlobalConfigAsset(true);
    }

    [Obsolete("Use " + nameof(RebuildPrefabTable) + " instead")]
    public static void RebuildObjectTable() => RebuildPrefabTable();

    [MenuItem("Tools/Fusion/Rebuild Prefab Table", priority = 100)]
    public static void RebuildPrefabTable() {
      foreach (var prefab in AssetDatabase.FindAssets($"t:prefab")
        .Select(AssetDatabase.GUIDToAssetPath)
        .Select(x => (GameObject)AssetDatabase.LoadMainAssetAtPath(x))) {
        if (prefab.TryGetComponent<NetworkObject>(out var networkObject) && !networkObject.Flags.IsIgnored()) {
          AssetDatabaseUtils.SetLabel(prefab, NetworkProjectConfigImporter.FusionPrefabTag, true);
        } else {
          AssetDatabaseUtils.SetLabel(prefab, NetworkProjectConfigImporter.FusionPrefabTag, false);
        }
      }

      AssetDatabase.Refresh();
      ImportGlobalConfig();
    }

    public static void PingGlobalConfigAsset(bool select = false) {
      if (NetworkProjectConfigAsset.TryGetGlobal(out var config)) {
        EditorGUIUtility.PingObject(config);
        if (select) {
          Selection.activeObject = config;
        }
      }
    }
    
    public static bool TryGetGlobalPrefabSource<T>(NetworkObjectGuid guid, out T source) where T : class, INetworkPrefabSource {
      if (NetworkProjectConfigAsset.TryGetGlobal(out var global)) {
        if (global.Config.PrefabTable.GetSource(guid) is T sourceT) {
          source = sourceT;
          return true;
        }
      }
      source = null;
      return false;
    }
    
    public static bool TryGetPrefabId(NetworkObjectGuid guid, out NetworkPrefabId id) {
      id = NetworkProjectConfig.Global.PrefabTable.GetId(guid);
      return id.IsValid;
    }

    public static bool TryGetPrefabId(string prefabPath, out NetworkPrefabId id) {
      var guidStr = AssetDatabase.AssetPathToGUID(prefabPath);
      if (NetworkObjectGuid.TryParse(guidStr, out var guid)) {
        return TryGetPrefabId(guid, out id);
      }

      id = default;
      return false;
    }

    // public static bool TryResolvePrefab(NetworkObjectGuid guid, out NetworkObject prefab) {
    //   if (TryGetPrefabSource(guid, out NetworkPrefabSourceBase source)) {
    //     try {
    //       prefab = NetworkPrefabSourceFactory.ResolveOrThrow(source);
    //       return true;
    //     } catch (Exception ex) {
    //       FusionEditorLog.Trace(ex.ToString());
    //     }
    //   }
    //
    //   prefab = null;
    //   return false;
    // }

    internal static bool TryGetPrefabEditorInstance(NetworkObjectGuid guid, out NetworkObject prefab) {
      if (!guid.IsValid) {
        prefab = null;
        return false;
      }
      
      var path = AssetDatabase.GUIDToAssetPath(guid.ToUnityGuidString());
      if (string.IsNullOrEmpty(path)) {
        prefab = null;
        return false;
      }
      
      var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
      if (!gameObject) {
        prefab = null;
        return false;
      }
      
      prefab = gameObject.GetComponent<NetworkObject>();
      return prefab;
    }

    internal static string GetGlobalConfigPath() {
      return FusionGlobalScriptableObjectUtils.GetGlobalAssetPath<NetworkProjectConfigAsset>();
    }

    public static bool ImportGlobalConfig() {
      return FusionGlobalScriptableObjectUtils.TryImportGlobal<NetworkProjectConfigAsset>();
    }
    
    public static string SaveGlobalConfig() {
      if (NetworkProjectConfigAsset.TryGetGlobal(out var global)) {
        return SaveGlobalConfig(global.Config);
      } else {
        return SaveGlobalConfig(new NetworkProjectConfig());
      }
    }

    public static string SaveGlobalConfig(NetworkProjectConfig config) {
      FusionGlobalScriptableObjectUtils.EnsureAssetExists<NetworkProjectConfigAsset>();
      string path = GetGlobalConfigPath();
      
      var json = EditorJsonUtility.ToJson(config, true);
      string existingJson = File.ReadAllText(path);
      
      if (!string.Equals(json, existingJson)) {
        AssetDatabase.MakeEditable(path);
        File.WriteAllText(path, json);
      }

      AssetDatabase.ImportAsset(path);
      return PathUtils.Normalize(path);
    }
    
    private static string[] GetEnabledBuildScenes() {
      var scenes = new List<string>();

      for (int i = 0; i < EditorBuildSettings.scenes.Length; ++i) {
        var scene = EditorBuildSettings.scenes[i];
        if (scene.enabled && string.IsNullOrEmpty(scene.path) == false) {
          scenes.Add(scene.path);
        }
      }

      return scenes.ToArray();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Editor/Utilities/NetworkRunnerUtilities.cs

namespace Fusion.Editor {

  using System.Collections.Generic;
  using UnityEngine;
  using UnityEditor;

  public static class NetworkRunnerUtilities {

    static List<NetworkRunner> reusableRunnerList = new List<NetworkRunner>();

    public static NetworkRunner[] FindActiveRunners() {
      var runners = Object.FindObjectsOfType<NetworkRunner>();
      reusableRunnerList.Clear();
      for (int i = 0; i < runners.Length; ++i) {
        if (runners[i].IsRunning)
          reusableRunnerList.Add(runners[i]);
      }
      if (reusableRunnerList.Count == runners.Length)
        return runners;

      return reusableRunnerList.ToArray();
    }

    public static void FindActiveRunners(List<NetworkRunner> nonalloc) {
      var runners = Object.FindObjectsOfType<NetworkRunner>();
      nonalloc.Clear();
      for (int i = 0; i < runners.Length; ++i) {
        if (runners[i].IsRunning)
          nonalloc.Add(runners[i]);
      }
    }

  }
}



#endregion


#region Assets/Photon/Fusion/Editor/VSAttribution.cs

﻿namespace Fusion.Editor
{
  using System;
  using UnityEngine.Analytics;
  using UnityEditor;

  public static class VSAttribution
	{
		const int k_VersionId = 4;
		const int k_MaxEventsPerHour = 10;
		const int k_MaxNumberOfElements = 1000;

		const string k_VendorKey = "<PARTNER KEY>";
		const string k_EventName = "FUSION_SETUP";

		static bool RegisterEvent()
		{
			AnalyticsResult result = EditorAnalytics.RegisterEventWithLimit(k_EventName, k_MaxEventsPerHour,
				k_MaxNumberOfElements, k_VendorKey, k_VersionId);

			var isResultOk = result == AnalyticsResult.Ok;
			return isResultOk;
		}

		[Serializable]
		struct VSAttributionData
		{
			public string actionName;
			public string partnerName;
			public string customerUid;
			public string extra;
		}

		/// <summary>
		/// Registers and attempts to send a Verified Solutions Attribution event.
		/// </summary>
		/// <param name="actionName">Name of the action, identifying a place this event was called from.</param>
		/// <param name="partnerName">Identifiable Verified Solutions Partner's name.</param>
		/// <param name="customerUid">Unique identifier of the customer using Partner's Verified Solution.</param>
		public static AnalyticsResult SendAttributionEvent(string customerUid, string actionName = k_EventName, string partnerName = k_VendorKey)
		{
			try
			{
				// Are Editor Analytics enabled ? (Preferences)
				if (!EditorAnalytics.enabled)
					return AnalyticsResult.AnalyticsDisabled;

				if (!RegisterEvent())
					return AnalyticsResult.InvalidData;

				// Create an expected data object
				var eventData = new VSAttributionData
				{
					actionName = actionName,
					partnerName = partnerName,
					customerUid = customerUid,
					extra = "{}"
				};

				return EditorAnalytics.SendEventWithLimit(k_EventName, eventData, k_VersionId);
			}
			catch
			{
				// Fail silently
				return AnalyticsResult.AnalyticsDisabled;
			}
		}
	}
}


#endregion

#endif
