#if !FUSION_DEV

#region Assets/Photon/Fusion/Scripts/Editor/AssetObjectEditor.cs

namespace Fusion.Editor {
  using Fusion;
  using UnityEditor;

  [CustomEditor(typeof(AssetObject), true)]
  public class AssetObjectEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
    }
  }  
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/AssetPipeline/INetworkPrefabSourceFactory.cs

namespace Fusion.Editor {
  using System;

  public interface INetworkPrefabSourceFactory {
    int Order { get; }
    Type SourceType { get; }

    NetworkPrefabSourceUnityBase TryCreate(string assetPath);
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/AssetPipeline/INetworkPrefabSourceFactoryCustomEditorResolve.cs

namespace Fusion.Editor {
  using System;

  public interface INetworkPrefabSourceFactoryCustomEditorResolve {
    UnityEngine.GameObject EditorResolveSource(NetworkPrefabSourceUnityBase prefabAsset);
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/AssetPipeline/NetworkPrefabSourceFactory.cs

namespace Fusion.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  internal static class NetworkPrefabSourceFactory {

    private static readonly Lazy<INetworkPrefabSourceFactory[]> _factories = new Lazy<INetworkPrefabSourceFactory[]>(() => {
      return UnityEditor.TypeCache.GetTypesDerivedFrom<INetworkPrefabSourceFactory>()
        .Select(x => (INetworkPrefabSourceFactory)Activator.CreateInstance(x))
        .OrderBy(x => x.Order)
        .ToArray();
    });

    public static NetworkPrefabSourceUnityBase Create(string assetPath) {
      foreach (var factory in _factories.Value) {
        var source = factory.TryCreate(assetPath);
        if (source != null) {
          if (source.GetType() != factory.SourceType) {
            throw new InvalidOperationException($"Factory {factory} is expected to return {factory.SourceType}, returned {source.GetType()} instead");
          }
          if (source is INetworkPrefabSource == false) {
            throw new InvalidOperationException($"Type {source.GetType()} does not implement {nameof(INetworkPrefabSource)}");
          }
          return source;
        }
      }

      throw new InvalidOperationException($"No factory could create info for prefab {assetPath}");
    }

    public static NetworkObject ResolveOrThrow(NetworkPrefabSourceUnityBase source) {
      foreach (var factory in _factories.Value) {
        if (factory.SourceType == source.GetType()) {
          GameObject prefab;
          if (factory is INetworkPrefabSourceFactoryCustomEditorResolve customResolve) {
            prefab = customResolve.EditorResolveSource(source);
            if (prefab == null) {
              throw new InvalidOperationException($"Factory {factory} returned null for {source}");
            }
          } else {
            var assetPath = AssetDatabase.GUIDToAssetPath(source.AssetGuid.ToUnityGuidString());
            if (string.IsNullOrEmpty(assetPath)) {
              throw new InvalidOperationException($"Unable to find asset path for {source}");
            }
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) {
              throw new InvalidOperationException($"Unable to load prefab at {assetPath} for {source}");
            }
          }

          var networkObject = prefab.GetComponent<NetworkObject>();
          if (networkObject == null) {
            throw new InvalidOperationException($"Prefab {prefab} does not contain {nameof(NetworkObject)} component");
          }
          return networkObject;
        }
      }

      throw new InvalidOperationException($"No factory could resolve {source}");
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/AssetPipeline/NetworkPrefabSourceFactoryResource.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  public class NetworkPrefabAssetFactoryResource: INetworkPrefabSourceFactory, INetworkPrefabSourceFactoryCustomEditorResolve {

    public const int DefaultOrder = 1000;

    Type INetworkPrefabSourceFactory.SourceType => typeof(NetworkPrefabSourceUnityResource);

    int INetworkPrefabSourceFactory.Order => DefaultOrder;

    NetworkPrefabSourceUnityBase INetworkPrefabSourceFactory.TryCreate(string assetPath) {
      if (PathUtils.MakeRelativeToFolder(assetPath, "Resources", out var resourcesPath)) {
        var result = ScriptableObject.CreateInstance<NetworkPrefabSourceUnityResource>();
        result.ResourcePath = PathUtils.GetPathWithoutExtension(resourcesPath);
        return result;
      } else {
        return null;
      }
    }

    GameObject INetworkPrefabSourceFactoryCustomEditorResolve.EditorResolveSource(NetworkPrefabSourceUnityBase prefabAsset) {
      var resource = (NetworkPrefabSourceUnityResource)prefabAsset;
      return Resources.Load<GameObject>(resource.ResourcePath);
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/AssetPipeline/NetworkPrefabSourceFactoryStatic.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  public class NetworkPrefabAssetFactoryStatic : INetworkPrefabSourceFactory, INetworkPrefabSourceFactoryCustomEditorResolve {

    public const int DefaultOrder = 2000;

    Type INetworkPrefabSourceFactory.SourceType => typeof(NetworkPrefabSourceUnityStatic);

    int INetworkPrefabSourceFactory.Order => DefaultOrder;

    NetworkPrefabSourceUnityBase INetworkPrefabSourceFactory.TryCreate(string assetPath) {
      var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
      if (gameObject == null) {
        throw new InvalidOperationException($"Unable to load {assetPath}");
      }

      var networkObject = gameObject.GetComponent<NetworkObject>();
      if (networkObject == null) {
        throw new InvalidOperationException($"Unable to get {nameof(NetworkObject)} from {assetPath}");
      }

      var result = ScriptableObject.CreateInstance<NetworkPrefabSourceUnityStatic>();
      result.PrefabReference = gameObject;
      return result;
    }

    GameObject INetworkPrefabSourceFactoryCustomEditorResolve.EditorResolveSource(NetworkPrefabSourceUnityBase prefabAsset) {
      return ((NetworkPrefabSourceUnityStatic)prefabAsset).PrefabReference;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/BehaviourEditor.cs

namespace Fusion.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;

  [CustomEditor(typeof(Fusion.Behaviour), true)]
  [CanEditMultipleObjects]
  public partial class BehaviourEditor :
#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
    Sirenix.OdinInspector.Editor.OdinEditor
#else
    UnityEditor.Editor
#endif
  {
    protected string _expandedHelpName;
    protected BehaviourActionInfo[] behaviourActions;

    public override void OnInspectorGUI() {
      PrepareInspectorGUI();
      base.DrawDefaultInspector();
      DrawBehaviourActions();
    }

    protected void PrepareInspectorGUI() {
      FusionEditorGUI.InjectPropertyDrawers(serializedObject);
    }

    protected void DrawBehaviourActions() {
      // Draw any BehaviourActionAttributes for this Component
      if (behaviourActions == null) {
        behaviourActions = target.GetActionAttributes();
      }

      target.DrawAllBehaviourActionAttributes(behaviourActions, ref _expandedHelpName);
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/BehaviourEditorUtils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public class BehaviourActionInfo {
    public string MemberName;
    public string Summary;
    public Attribute Attribute;
    public Action Action;
    public MemberInfo MemberInfo;
    public Func<object, object> Condition;
    public BehaviourActionInfo(string memberName, string summary, Attribute attribute, Action action, Func<object, object> condition) {
      Summary = summary;
      MemberName = memberName;
      Attribute = attribute;
      Action = action;
      Condition = condition;
    }
  }

  public static class BehaviourEditorUtils {

    // Getter to Delegate conversion magic for method that returns any kind of object and has no arguments
    private static readonly MethodInfo CallPropertyDelegateMethod = typeof(BehaviourEditorUtils).GetMethod(nameof(CallPropertyDelegate), BindingFlags.NonPublic | BindingFlags.Static);
    private static Func<object, object> CallPropertyDelegate<TDeclared, TProperty>(Func<TDeclared, TProperty> deleg) => instance => deleg((TDeclared)instance);

    public static Dictionary<Type, Dictionary<string, Func<object, object>>> GetValueDelegateLookups = new Dictionary<Type, Dictionary<string, Func<object, object>>>();
    public static Dictionary<Type, Dictionary<string,Action>> ActionDelegateLookups = new Dictionary<Type, Dictionary<string, Action>>();

    /// <summary>
    /// Find member by name, and if compatible extracts a delegate for (object)value = Func(object targetInstance).
    /// </summary>
    public static Func<object, object> GetDelegateFromMember(this Type type, string memberName) {
      if (memberName == null || memberName == "")
        return null;

      // See if we already have a cached delegate for this type and member name.
      if (GetValueDelegateLookups.TryGetValue(type, out var delegateLookup)) {
        if (delegateLookup.TryGetValue(memberName, out var getValueDelegate)) {
          return getValueDelegate;
        }
      } else {
        delegateLookup = new Dictionary<string, Func<object, object>>();
        GetValueDelegateLookups.Add(type, delegateLookup);
      }

      // No delegate exists, brute force find one.

      var members = type.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
      foreach (MemberInfo member in members) {
        if (member is FieldInfo) {
          var finfo = (FieldInfo)member;
          var getValDelegate = (Func<object, object>)((object targ) => finfo.GetValue(targ));
          delegateLookup.Add(memberName, getValDelegate);
          return getValDelegate;
        }
        if (member is PropertyInfo) {
          var p = (PropertyInfo)member;
          var getMethod = p.GetMethod;
          var declaring = p.DeclaringType;
          var typeOfResult = p.PropertyType;
          var getMethodDelegateType = typeof(Func<,>).MakeGenericType(declaring, typeOfResult);
          var getMethodDelegate = getMethod.CreateDelegate(getMethodDelegateType);
          var getMethodGeneric = CallPropertyDelegateMethod.MakeGenericMethod(declaring, typeOfResult);
          var getValDelegate = (Func<object, object>)getMethodGeneric.Invoke(null, new[] { getMethodDelegate });
          delegateLookup.Add(memberName, getValDelegate);
          return getValDelegate;
        }
        if (member is MethodInfo) {
          var m = member as MethodInfo;
          var getMethod = (MethodInfo)member;
          var declaring = member.DeclaringType;
          var typeOfResult = m.ReturnType;
          var getMethodDelegateType = typeof(Func<,>).MakeGenericType(declaring, typeOfResult);
          var getMethodDelegate = getMethod.CreateDelegate(getMethodDelegateType);
          var getMethodGeneric = CallPropertyDelegateMethod.MakeGenericMethod(declaring, typeOfResult);
          var getValDelegate = (Func<object, object>)getMethodGeneric.Invoke(null, new[] { getMethodDelegate });
          delegateLookup.Add(memberName, getValDelegate);
          return getValDelegate;
        }
      }

      delegateLookup.Add(memberName, null);
      return null;
     }

    private static List<BehaviourActionInfo> reusableAttributeList = new List<BehaviourActionInfo>();

    /// <summary>
    /// Collect all BehaviourActionAttributes on a target's type, and cache as much of the heavy reflection out as possible.
    /// </summary>
    internal static BehaviourActionInfo[] GetActionAttributes(this UnityEngine.Object target) {

      var targetType = target.GetType();
      if (!typeof(Behaviour).IsAssignableFrom(targetType))
        return null;

      reusableAttributeList.Clear();
        
      var methods = targetType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

      foreach (var member in methods) {
        // Rendering action attributes at the end of the inspector only if they are on for Methods and Property. 
        // Field attributes are rendered in-line elsewhere.
        MethodInfo method;
        if (member is MethodInfo minfo)
          method = minfo;
        else if (member is PropertyInfo pinfo)
          method = null;
        else
          continue;

        var attrs = member.GetCustomAttributes();
        foreach (var attr in attrs) {

          if (attr is BehaviourWarnAttribute warnAttr) {
            var condName = warnAttr.ConditionMember;
            Func<object, object> condMethod = targetType.GetDelegateFromMember(warnAttr.ConditionMember);
            reusableAttributeList.Add(new BehaviourActionInfo(member.Name, method.GetXmlDocSummary(), attr, null, condMethod));
            continue;
          }

          if (attr is BehaviourButtonActionAttribute buttonAttr) {
            var condName = buttonAttr.ConditionMember;
            Func<object, object> condMethod = targetType.GetDelegateFromMember(buttonAttr.ConditionMember);
            reusableAttributeList.Add(new BehaviourActionInfo(member.Name, method.GetXmlDocSummary(), attr, (Action)method.CreateDelegate(typeof(Action), target), condMethod));
            continue;
          }

          if (attr is BehaviourToggleAttribute toggleAttr) {
            Func<object, object> condMethod = targetType.GetDelegateFromMember(toggleAttr.ConditionMember);
            var actioninfo = new BehaviourActionInfo(member.Name, (member as PropertyInfo).GetXmlDocSummary(), attr, null, condMethod);
            actioninfo.MemberInfo = member;
            reusableAttributeList.Add(actioninfo);
            continue;
          }

          if (attr is BehaviourActionAttribute actionAttr) {
            Func<object, object> condMethod = targetType.GetDelegateFromMember(actionAttr.ConditionMember);
            reusableAttributeList.Add(new BehaviourActionInfo(member.Name, method.GetXmlDocSummary(), attr, (Action)method.CreateDelegate(typeof(Action), target), condMethod));
            continue;
          }
        }
      }
     
      return reusableAttributeList.ToArray();
    }

    /// <summary>
    /// Draw all special editor method attributes specific to Fusion.Behaviour rendering.
    /// </summary>
    /// <param name="target"></param>
    internal static void DrawAllBehaviourActionAttributes(this UnityEngine.Object target, BehaviourActionInfo[] behaviourActions, ref string expandedHelpName) {

      if (behaviourActions == null)
        return;

      foreach (var ba in behaviourActions) {

        var attr = ba.Attribute;
        var action = ba.Action;
        var condition = ba.Condition;

        if (attr is BehaviourWarnAttribute warnAttr) {
          warnAttr.DrawEditorWarnAttribute(target, condition);
          continue;
        }

        if (attr is BehaviourButtonActionAttribute buttonAttr) {
          buttonAttr.DrawEditorButtonAttribute(ba, target, ref expandedHelpName);
          continue;
        }

        if (attr is BehaviourToggleAttribute toggleAttr) {
          toggleAttr.DrawEditorToggleAttribute(ba, target, ref expandedHelpName);
          continue;
        }

        // Action is the base class, so this needs to be last always if more derived classes are added
        if (attr is BehaviourActionAttribute actionAttr) {
          action.Invoke();
          continue;
        }
      }
    }

    internal static Action GetActionDelegate(this Type targetType, string actionMethodName, UnityEngine.Object target) {

      if (ActionDelegateLookups.TryGetValue(targetType, out var lookup)) {
        if (lookup.TryGetValue(actionMethodName, out var found))
          return found;
      } else {
        lookup = new Dictionary<string, Action>();
        ActionDelegateLookups.Add(targetType, lookup);
      }
      var executeMethod = target.GetType().GetMethod(actionMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
      var action = (Action)executeMethod.CreateDelegate(typeof(Action), target);
      lookup.Add(actionMethodName, action);
      return action;
    }

    internal static void NoActionWarning() {
      Debug.LogWarning($"<B>No action defined for {nameof(BehaviourButtonActionAttribute)}.</b> Be sure to either add one to the attribute arguments, or place the attribute on a method.");
    }

    internal static void DrawEditorToggleAttribute(this BehaviourToggleAttribute toggleAttr, BehaviourActionInfo actionInfo, UnityEngine.Object target, ref string expandedPropertyName) {
      // If a condition member exists for this attribute, check it.
      if (actionInfo.Condition != null) {
        object valObj = actionInfo.Condition(target);
        if (valObj == null || valObj.GetObjectValueAsDouble() == 0) {
          return;
        }
      }

      DrawEditorToggleAttributeFinal(toggleAttr, target, actionInfo, ref expandedPropertyName);
    }

    internal static void DrawEditorButtonAttribute(this BehaviourButtonActionAttribute buttonAttr, BehaviourActionInfo actionInfo, UnityEngine.Object target, ref string expandedPropertyName) {

      // If a condition member exists for this attribute, check it.
      if (actionInfo.Condition != null) {
        object valObj = actionInfo.Condition(target);
        if (valObj == null || valObj.GetObjectValueAsDouble() == 0) {
          return;
        }
      }
      
      DrawEditorButtonAttributeFinal(buttonAttr, target, actionInfo, ref expandedPropertyName);
    }

    internal static void DrawEditorWarnAttribute(this BehaviourWarnAttribute buttonAttr, UnityEngine.Object target) {
      if (buttonAttr.ConditionMember != null) {

        var getValDelegate = target.GetType().GetDelegateFromMember(buttonAttr.ConditionMember);
        if (getValDelegate == null)
          return;

        object valObj = getValDelegate(target);

        if (valObj == null || valObj.GetObjectValueAsDouble() == 0) {
          return;
        }
      }

      DrawEditorWarnAttributeFinal(buttonAttr);
    }

    internal static void DrawEditorWarnAttribute(this BehaviourWarnAttribute buttonAttr, UnityEngine.Object target, Func<object, object> condition) {

      // If a condition member exists for this attribute, check it.
      if (condition != null) {
        object valObj = condition(target);
        if (valObj == null || valObj.GetObjectValueAsDouble() == 0) {
          return;
        }
      }
      DrawEditorWarnAttributeFinal(buttonAttr);
    }


    static void DrawEditorButtonAttributeFinal(this BehaviourButtonActionAttribute buttonAttr, UnityEngine.Object target, BehaviourActionInfo actionInfo, ref string expandedPropertyName) {
      var flags = buttonAttr.ConditionFlags;

      if (ShouldShow(flags)) {
        GUILayout.Space(4);
        Rect rect = EditorGUILayout.GetControlRect();

        if (!string.IsNullOrEmpty(actionInfo.Summary)) {
          var wasExpanded = FusionEditorGUI.IsHelpExpanded(target, actionInfo.MemberName);
          var buttonRect = FusionEditorGUI.GetInlineHelpButtonRect(InlineHelpButtonPlacement.BeforeLabel, rect, GUIContent.none, expectFoldout: false);

          if (wasExpanded) {
            var content = new GUIContent(actionInfo.Summary);

            var contentRect = FusionEditorGUI.GetInlineHelpRect(content);
            var totalRect = new Rect(rect.x, rect.y, rect.width, rect.height + contentRect.height);

            EditorGUILayout.GetControlRect(false, contentRect.height, EditorStyles.helpBox);
            FusionEditorGUI.DrawInlineHelp(content, totalRect, contentRect);
          }

          if (FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded)) {
            FusionEditorGUI.SetHelpExpanded(target, actionInfo.MemberName, !wasExpanded);
            if (!wasExpanded) {
              expandedPropertyName = actionInfo.MemberName;
            }
          }
        }

        if (GUI.Button(rect, buttonAttr.ButtonName)) {
          actionInfo.Action.Invoke();
          if ((flags & BehaviourActionAttribute.ActionFlags.DirtyAfterButton) == BehaviourActionAttribute.ActionFlags.DirtyAfterButton) {
            EditorUtility.SetDirty(target);
          }
        }
      }
    }

    static void DrawEditorToggleAttributeFinal(this BehaviourToggleAttribute toggleAttr, UnityEngine.Object target, BehaviourActionInfo actionInfo, ref string expandedPropertyName) {
      var flags = toggleAttr.ConditionFlags;

      if (ShouldShow(flags)) {
        GUILayout.Space(4);
        Rect rect = EditorGUILayout.GetControlRect();

        if (!string.IsNullOrEmpty(actionInfo.Summary)) {
          var wasExpanded = FusionEditorGUI.IsHelpExpanded(target, actionInfo.MemberName);
          var buttonRect = FusionEditorGUI.GetInlineHelpButtonRect(InlineHelpButtonPlacement.BeforeLabel, rect, GUIContent.none, expectFoldout: false);

          if (wasExpanded) {
            var content = new GUIContent(actionInfo.Summary);

            var contentRect = FusionEditorGUI.GetInlineHelpRect(content);
            var totalRect = new Rect(rect.x, rect.y, rect.width, rect.height + contentRect.height);

            EditorGUILayout.GetControlRect(false, contentRect.height, EditorStyles.helpBox);
            FusionEditorGUI.DrawInlineHelp(content, totalRect, contentRect);
          }

          if (FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded)) {
            FusionEditorGUI.SetHelpExpanded(target, actionInfo.MemberName, !wasExpanded);
            if (!wasExpanded) {
              expandedPropertyName = actionInfo.MemberName;
            }
          }
        }

        var pinfo = actionInfo.MemberInfo as PropertyInfo;

        var oldval = (bool)pinfo.GetValue(target);
        var newval = EditorGUI.Toggle(rect, ObjectNames.NicifyVariableName(pinfo.Name), oldval);
        if (newval != oldval) {
          pinfo.SetValue(target, newval);
          if ((flags & BehaviourActionAttribute.ActionFlags.DirtyAfterButton) == BehaviourActionAttribute.ActionFlags.DirtyAfterButton) {
            EditorUtility.SetDirty(target);
          }
        }
      }
    }

    static void DrawEditorWarnAttributeFinal(this BehaviourWarnAttribute warnAttr) {

      if (ShouldShow(warnAttr.ConditionFlags)) {
        GUILayout.Space(4);
        DrawWarnBox(warnAttr.WarnText, MessageType.Warning);
      }
    }

    public static bool DrawWarnButton(GUIContent buttonText, MessageType icon = MessageType.None) {

      var rect = EditorGUILayout.GetControlRect(false, 24);
      var clicked = GUI.Button(rect, buttonText);

      if (icon != MessageType.None) {
        Texture2D icontexture;
        switch (icon) {
          case MessageType.Info:
            icontexture = FusionGUIStyles.InfoIcon;
            break;
          case MessageType.Warning:
            icontexture = FusionGUIStyles.WarnIcon;
            break;
          case MessageType.Error:
            icontexture = FusionGUIStyles.ErrorIcon;
            break;
          default:
            icontexture = FusionGUIStyles.InfoIcon;
            break;
        }
        GUI.Label(new Rect(rect) { xMin = rect.xMin + 4, yMin = rect.yMin + 2, yMax = rect.yMax - 2  }, icontexture);
      } 
      return clicked;
    }

    public static void DrawWarnBox(string message, MessageType msgtype = MessageType.Warning, FusionGUIStyles.GroupBoxType? groupBoxType = null) {

      var style = groupBoxType.HasValue ? groupBoxType.Value.GetStyle() : ((FusionGUIStyles.GroupBoxType)msgtype).GetStyle();
      EditorGUILayout.BeginHorizontal(style/*, GUILayout.ExpandHeight(true)*/);
      {

        // TODO: Cache these icons in a utility
        if (msgtype != MessageType.None) {
          Texture icon =
            msgtype == MessageType.Warning ? FusionGUIStyles.WarnIcon :
            msgtype == MessageType.Error ? FusionGUIStyles.ErrorIcon :
             FusionGUIStyles.InfoIcon;

          GUI.DrawTexture(EditorGUILayout.GetControlRect(GUILayout.Width(32), GUILayout.Height(32)), icon, ScaleMode.ScaleAndCrop);
        }

        EditorGUILayout.LabelField(message, FusionGUIStyles.WarnLabelStyle);
      }
      EditorGUILayout.EndHorizontal();
    }

    static bool ShouldShow(BehaviourActionAttribute.ActionFlags flags) {
      bool isPlaying = Application.isPlaying;
      return (
        (isPlaying && (flags & BehaviourActionAttribute.ActionFlags.ShowAtRuntime) == BehaviourActionAttribute.ActionFlags.ShowAtRuntime) ||
        (!isPlaying && (flags & BehaviourActionAttribute.ActionFlags.ShowAtNotRuntime) == BehaviourActionAttribute.ActionFlags.ShowAtNotRuntime));
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/ChildLookupEditor.cs

// removed July 12 2021


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/AccuracyDefaultsDrawer.cs

namespace Fusion.Editor {

  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AccuracyDefaults))]
  public class AccuracyDefaultsDrawer : PropertyDrawer {

    private static string[] _tagNames;
    public static string[] TagNames {
      get {
        if (_tagNames == null) {
          RefreshCache();
        }
        return _tagNames;
      }
    }

    private static Dictionary<int, (int, string)> _tagLookup = new Dictionary<int, (int, string)>();
    public static Dictionary<int, (int, string)> TagLookup {
      get {
        if (_tagLookup.Count == 0) {
          RefreshCache();
        }
        return _tagLookup;
      }
    }

    // Invalidates the static caches of AccuracyDefault names, used in Accuracy droplist.
    private static void ClearTagCache() {
      _tagLookup.Clear();
      _tagNames = null;
    }

    public static void RefreshCache() {

      var settings = NetworkProjectConfig.Global.AccuracyDefaults;
      settings.RebuildLookup();

      List<string> names = new List<string>(settings.coreKeys);
      names.AddRange(settings.tags);
      _tagNames = names.ToArray();

      _tagLookup.Clear();
      for (int i = 0, cnt = _tagNames.Length; i < cnt; ++i) {
        string t = _tagNames[i];
        _tagLookup.Add(t.GetHashDeterministic(), (i, t));
      }
    }

    private static void SetValue(SerializedProperty array, int index, Accuracy value) {
      array.GetArrayElementAtIndex(index).FindPropertyRelativeOrThrow("_value").floatValue = value._value;
      array.GetArrayElementAtIndex(index).FindPropertyRelativeOrThrow("_inverse").floatValue = value._inverse;
    }

    private static string GetKey(SerializedProperty array, int index) {
      return array.GetArrayElementAtIndex(index).stringValue;
    }

    private static string SetKeyEnsureUnique(SerializedProperty array, int index, string value) {
      HashSet<string> keys = new HashSet<string>();
      for (int i = 0; i < array.arraySize; ++i) {
        if (i == index)
          continue;
        keys.Add(GetKey(array, i));
      }
      while (keys.Contains(value)) {
        value += "X";
      }
      array.GetArrayElementAtIndex(index).stringValue = value;
      return value;
    }

    private static Accuracy GetValue(SerializedProperty array, int index) {
      var elem = array.GetArrayElementAtIndex(index);
      return new Accuracy() {
        _value = elem.FindPropertyRelativeOrThrow("_value").floatValue,
        _inverse = elem.FindPropertyRelativeOrThrow("_inverse").floatValue,
      };
    }

    const int ELEMENT_HEIGHT = 20;
    const int ELEMENT_INNER = 18;
    const int BOX_PADDING = 14;
    const int BUTTON_TOPPAD = 2;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var coreKeys = property.FindPropertyRelativeOrThrow(nameof(AccuracyDefaults.coreKeys));
      var tags = property.FindPropertyRelativeOrThrow(nameof(AccuracyDefaults.tags));
      if (property.isExpanded) {
        return ELEMENT_HEIGHT + ELEMENT_HEIGHT + ((tags.arraySize + coreKeys.arraySize) * ELEMENT_HEIGHT) + (BOX_PADDING * 2) + BUTTON_TOPPAD + ELEMENT_HEIGHT;
      } else {
        return base.GetPropertyHeight(property, label);
      }
    }

    public override void OnGUI(Rect r, SerializedProperty property, GUIContent label) {

      EditorGUI.BeginProperty(r, label, property);
      // Indented to make room for the inline help icon
      property.isExpanded = EditorGUI.Foldout(new Rect(r) { xMin = r.xMin, height = ELEMENT_INNER }, property.isExpanded, label);

      if (property.isExpanded) {

        float y = r.yMin + ELEMENT_HEIGHT;

        GUI.Box(new Rect(r) { yMin = y }, "", EditorStyles.helpBox); // FusionGUIStyles.GroupBoxType.Sand.GetStyle());

        r = new Rect(r) { xMin = r.xMin + BOX_PADDING, xMax = r.xMax - BOX_PADDING, yMin = y + BOX_PADDING,  height = ELEMENT_INNER };

        const float LABEL_WIDTH = 120;
        float labelleft = r.xMin;
        float resetbtnwidth = 24;
        float valueleft = r.xMin + LABEL_WIDTH;
        float valuewidth = r.width - LABEL_WIDTH - resetbtnwidth - 2;

        EditorGUI.BeginChangeCheck();
        {

          GUI.Label(r, "Tag:");
          GUI.Label(new Rect(r) { x = valueleft }, "Accuracy:");

          r.y += ELEMENT_HEIGHT;

          var settings = NetworkProjectConfig.Global.AccuracyDefaults;

          var serializedObject = property.serializedObject;

          const float MIN = .0000008f;
          const float MAX = 10f;
          const float ZERO = .000001f;

          var coreDefs = settings.coreDefs;

          var coreKeys = property.FindPropertyRelativeOrThrow(nameof(AccuracyDefaults.coreKeys));
          var coreVals = property.FindPropertyRelativeOrThrow(nameof(AccuracyDefaults.coreVals));

          var tags   = property.FindPropertyRelativeOrThrow(nameof(AccuracyDefaults.tags));
          var values = property.FindPropertyRelativeOrThrow(nameof(AccuracyDefaults.values));

          // Fixed named items (Core)
          for (int i = 0; i < AccuracyDefaults.CORE_COUNT; ++i) {

            string key = GetKey(coreKeys, i);
            Accuracy val = GetValue(coreVals, i);

            string tooltip = "hash: " + key.GetHashDeterministic();
            GUI.Label(new Rect(r) { width = LABEL_WIDTH - 2 }, new GUIContent(key, tooltip), new GUIStyle("Label") { fontStyle = FontStyle.Italic });

            EditorGUI.BeginDisabledGroup(i == 0);
            {
              float newVal = CustomSliders.Log10Slider(new Rect(r) { x = valueleft, width = valuewidth }, val.Value, null, MIN, MAX, ZERO, 1);

              // Button - Reset to default
              if (GUI.Button(new Rect(r) { xMin = r.xMax - resetbtnwidth}, EditorGUIUtility.FindTexture("d_RotateTool"))) {
                SetValue(coreVals, i, coreDefs[i]._value);
              }
              if (val._value != newVal) {
                SetValue(coreVals, i, newVal);
              }
            }
            EditorGUI.EndDisabledGroup();

            r.y += ELEMENT_HEIGHT;
          }

          //User Editable list items
          for (int i = 0, cnt = tags.arraySize; i < cnt; ++i) {

            string key = GetKey(tags, i);
            float val = GetValue(values, i)._value;

            string tooltip = "hash: " + key.GetHashDeterministic();
            GUI.Label(r, new GUIContent(key, tooltip));
            string newKey = GUI.TextField(new Rect(r) { width = LABEL_WIDTH - 4 }, key);

            float newVal = CustomSliders.Log10Slider(new Rect(r) { x = valueleft, width = valuewidth }, val, null, MIN, MAX, ZERO, 1);

            if (GUI.Button(new Rect(r) { xMin = r.xMax - resetbtnwidth }, "X")) {
              values.DeleteArrayElementAtIndex(i);
              tags.DeleteArrayElementAtIndex(i);
              ClearTagCache();
              break;
            }

            if (key != newKey) {
              SetKeyEnsureUnique(tags, i, newKey);
              ClearTagCache();
            }

            if (val != newVal) {
              SetValue(values, i, newVal);
            }
            r.y += ELEMENT_HEIGHT;
          }

          r.y += BUTTON_TOPPAD;

          if (GUI.Button(new Rect(r) { xMin = r.xMin}, "Add New")) {
            tags.InsertArrayElementAtIndex(tags.arraySize);
            values.InsertArrayElementAtIndex(values.arraySize);

            SetKeyEnsureUnique(tags, tags.arraySize - 1, "UserDefined");
            SetValue(values, values.arraySize - 1, AccuracyDefaults.DEFAULT_ACCURACY);
            ClearTagCache();
          }
        }

        if (EditorGUI.EndChangeCheck()) {
          property.serializedObject.ApplyModifiedProperties();
          property.serializedObject.Update();
        }
      }

      EditorGUI.EndProperty();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/AccuracyDefaultsDrawGUI.cs

// deleted

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/AccuracyDrawer.cs

namespace Fusion.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Accuracy), true)]
  public class AccuracyDrawer : PropertyDrawer, IFoldablePropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      OnGUIExtended(position, property, label, new AccuracyRangeAttribute(AccuracyRangePreset.Defaults));
    }

    /// <summary>
    /// OnGui handling for custom fusion types that use a custom AccuracyRangeAttribute slider.
    /// </summary>
    internal static void OnGUIExtended(Rect r, SerializedProperty property, GUIContent label, AccuracyRangeAttribute range) {

      EditorGUI.BeginProperty(r, label, property);

      var value = property.FindPropertyRelative(nameof(Accuracy._value));
      var inverse = property.FindPropertyRelative(nameof(Accuracy._inverse));
      var hash = property.FindPropertyRelative(nameof(Accuracy._hash));

      if (value == null || inverse == null) {
        EditorGUI.PropertyField(r, property, label);

        Debug.LogWarning($"AccuracyAttribute and AccuracyRangeAttribute can only be used on Accuracy types. {property.serializedObject.targetObject.name}:{property.name} is a {property.type}");
        return;
      }

      float min = range.min;
      float max = range.max;
      float labelWidth = EditorGUIUtility.labelWidth;
      const int CHECK_WIDTH = 18;

      EditorGUI.LabelField(r, label);

      Rect toggleRect = new Rect(r) { xMin = r.xMin + labelWidth + 2, width = 16 };
      const string globalsTooltip = "Toggles between custom accuracy value, and using a defined Global Accuracy (found in " + nameof(NetworkProjectConfig) + ").";
      EditorGUI.LabelField(r /*toggleRect*/, new GUIContent("", globalsTooltip));
      bool useGlobals = GUI.Toggle(toggleRect, inverse.floatValue == 0, GUIContent.none);

      // To spare some memory, a toggle bool isn't used. Instead the Inverse value being zero indicates usage of the hash/global setting.
      if (useGlobals != (inverse.floatValue == 0)) {
        if (useGlobals) {
          inverse.floatValue = 0;

          if (hash.intValue == 0) {
            hash.intValue = AccuracyDefaults.ZeroHashRemap;
            property.serializedObject.ApplyModifiedProperties();
          }
        } else {
          inverse.floatValue = 1f / value.floatValue;
        }
      }

      // Slider and Field

      if (useGlobals) {
        var newval = DrawDroplist(new Rect(r) { xMin = r.xMin + labelWidth + CHECK_WIDTH }, hash.intValue, property.serializedObject.targetObject);

        if (hash.intValue != newval) {
          hash.intValue = newval;
          property.serializedObject.ApplyModifiedProperties();
        }
      }
      else {

        // if linear, convert base10, and hand draw the slider (since its internal values should never be seen)
        if (range.logarithmic) {
          EditorGUI.BeginChangeCheck();
          value.floatValue = CustomSliders.Log10Slider(r, value.floatValue, GUIContent.none, (min * .9f), max, min, range.places, CHECK_WIDTH);
          if (EditorGUI.EndChangeCheck()) {
            inverse.floatValue = 1 / value.floatValue;
            property.serializedObject.ApplyModifiedProperties();
          }
        }
        // Non-linear, just keep this simple.
        else {

          EditorGUI.BeginChangeCheck();

          EditorGUI.Slider(new Rect(r) { xMin = r.xMin + labelWidth + 4 + CHECK_WIDTH }, value, max, 0, GUIContent.none);
         
          if (EditorGUI.EndChangeCheck()) {

            if (value.floatValue < min) {
              value.floatValue = 0;
              inverse.floatValue = float.PositiveInfinity;
            } else {
              value.floatValue = CustomSliders.RoundAndClamp(value.floatValue, min, max, range.places);
              inverse.floatValue = 1 / value.floatValue;
            }

            property.serializedObject.ApplyModifiedProperties();
          }
        }
      }
      EditorGUI.EndProperty();
    }

    public static int DrawDroplist(Rect r, int hash, UnityEngine.Object target) {

      const float VAL_WIDTH = 50;
      const float COLLAPSE_WIDTH = 120;

      bool isNarrow = r.width < COLLAPSE_WIDTH;

      if (hash == 0) {
        hash = AccuracyDefaults.ZeroHashRemap;
      }

      bool success = AccuracyDefaultsDrawer.TagLookup.TryGetValue(hash, out (int popupindex, string) tag);

      var hold = EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;
      var selected = EditorGUI.Popup(new Rect(r) { xMax = isNarrow ? r.xMax : r.xMax - VAL_WIDTH }, success ? tag.popupindex : -1, AccuracyDefaultsDrawer.TagNames);
      EditorGUI.indentLevel = hold;


      var accuracy = GetAccuracyFromHash(hash, target);

      float val = accuracy.Value;
      // Round the value to fit the label
      val = (val > 1) ? (float)System.Math.Round(val, 3) : (float)System.Math.Round(val, 4);

      if (isNarrow == false) {
        GUIStyle valStyle = new GUIStyle("MiniLabel") { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Italic };

        if (GUI.Button(new Rect(r) { xMin = r.xMax - VAL_WIDTH }, val.ToString(), valStyle)) {
          NetworkProjectConfigUtilities.PingGlobalConfigAsset();
        }
      }


      if (selected == -1) {
        GUI.Label(new Rect(r) { width = 16 }, FusionGUIStyles.ErrorIcon);
        EditorGUI.BeginDisabledGroup(true);
        GUI.Label(new Rect(r) { xMin = r.xMin + 20, xMax = r.xMax - VAL_WIDTH - 24 }, new GUIContent("Missing Tag: " + hash.ToString(), "The previously selected tag no longer exists in Accuracy Defaults"), EditorStyles.miniLabel);
        EditorGUI.EndDisabledGroup();
      }
      return selected == -1 ? hash : AccuracyDefaultsDrawer.TagNames[selected].GetHashDeterministic();
    }

    static System.Collections.Generic.HashSet<int> _beenWarnedOnce;

    static Accuracy GetAccuracyFromHash(int hash, UnityEngine.Object target) {
      bool found = NetworkProjectConfig.Global.AccuracyDefaults.TryGetAccuracy(hash, out Accuracy accuracy);
      if (found == false) {
        // Warn of an invalid hash, but only once to avoid log spam.
        if (_beenWarnedOnce == null) {
          _beenWarnedOnce = new System.Collections.Generic.HashSet<int>();
        }
        if (_beenWarnedOnce.Contains(hash) == false) {
          _beenWarnedOnce.Add(hash);
          Debug.LogWarning($"GameObject: '{target.name}' - Accuracy for hash '{hash}' was not found in {nameof(AccuracyDefaults)}.{nameof(AccuracyDefaults.Lookup)}. " +
    $"Make sure a matching entry exists in {nameof(AccuracyDefaults)} in the {nameof(NetworkProjectConfig)}. " +
    $"A user defined entry in {nameof(NetworkProjectConfig)} > Accuracy Defaults may have been deleted, or was lost due to a {nameof(NetworkProjectConfig)} reset. " +
    $"Default value will be used until this is corrected.");
        }
      }

      return accuracy;
    }

    bool IFoldablePropertyDrawer.HasFoldout(SerializedProperty property) {
      return false;
    }
  }
}





#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/AccuracyRangeDrawer.cs

namespace Fusion.Editor {

  using System;
  using UnityEngine;
  using UnityEditor;

  [CustomPropertyDrawer(typeof(AccuracyRangeAttribute))]
  public class AccuracyRangeDrawer : PropertyDrawer {

    public override void OnGUI(Rect r, SerializedProperty property, GUIContent label) {

      EditorGUI.BeginProperty(r, label, property);

      AccuracyRangeAttribute range = (AccuracyRangeAttribute)attribute;

      AccuracyDrawer.OnGUIExtended(r, property, label, range);

    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/AssemblyNameAttributeDrawer.cs

// ---------------------------------------------------------------------------------------------
// <copyright>PhotonNetwork Framework for Unity - Copyright (C) 2020 Exit Games GmbH</copyright>
// <author>developer@exitgames.com</author>
// ---------------------------------------------------------------------------------------------

namespace Fusion.Editor {
  using UnityEngine;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using System.Linq;
  using System.IO;
  using System;
  using System.Collections.Generic;

  [CanEditMultipleObjects]
  [CustomPropertyDrawer(typeof(AssemblyNameAttribute))]
  public class AssemblyNameAttributeDrawer : PropertyDrawerWithErrorHandling {

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

      var assemblyName = property.stringValue;
      if (!string.IsNullOrEmpty(assemblyName)) {
        if (_allAssemblies == null) {
          _allAssemblies = new HashSet<string>(GetAssemblies(AsmDefType.All), StringComparer.OrdinalIgnoreCase);
        }
        if (!_allAssemblies.Contains(assemblyName, StringComparer.OrdinalIgnoreCase)) {
          SetError($"Assembly not found: {assemblyName}");
        }
      }

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.BeginChangeCheck();

        assemblyName = EditorGUI.TextField(new Rect(position) { xMax = position.xMax - DropdownWidth }, label, assemblyName);

        var dropdownRect = EditorGUI.IndentedRect(new Rect(position) { xMin = position.xMax - DropdownWidth });

        if (EditorGUI.DropdownButton(dropdownRect, DropdownContent, FocusType.Passive)) {

          GenericMenu.MenuFunction2 onClicked = (userData) => {
            property.stringValue = (string)userData;
            property.serializedObject.ApplyModifiedProperties();
            UnityInternal.EditorGUI.EndEditingActiveTextField();
            ClearError(property);
          };

          var menu = new GenericMenu();

          foreach (var (flag, prefix) in new[] { (AsmDefType.Editor, "Editor/"), (AsmDefType.Runtime, "") }) {

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
          query = query.Concat(new[] { "Assembly-CSharp-firstpass", "Assembly-CSharp" });
        }
        if (types.HasFlag(AsmDefType.Editor)) {
          query = query.Concat(new[] { "Assembly-CSharp-Editor-firstpass", "Assembly-CSharp-Editor" });
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
      public string name = string.Empty;
    }
  }

}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/AutoGUIAttributeDrawer.cs

// Removed May 22 2021 (Alpha 3)


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/CastEnumAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(CastEnumAttribute))]
  public class CastEnumAttributeDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      EditorGUI.BeginProperty(position, label, property);

      var attr = attribute as CastEnumAttribute;
      var castToType = attr.CastToType;

      // get cast type using property if a type was not given.
      if (castToType == null) {
        var methodname = attr.GetTypeMethodName;
        var instance = property.serializedObject.targetObject;
        var compType = instance.GetType();
        var propInfo = compType.GetProperty(methodname, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (propInfo == null) {
          Debug.LogWarning($"Invalid property supplied to {nameof(CastEnumAttribute)} in class '{compType.Name}' on field {property.name}");
        } else {
          if (propInfo.PropertyType == typeof(Type)) {
            castToType = (Type)propInfo.GetValue(instance);
          } else {
            Debug.LogWarning($"Property supplied to {nameof(CastEnumAttribute)} in class '{compType.Name}' on field '{property.name}' does not evaluate to an Enum type.");
          }
        }
      } 

      if (castToType != null){
        
        // Only cast if the Type supplied is an enum type.
        if (castToType.IsEnum) {
          var converted = (Enum)Enum.ToObject(castToType, property.intValue);
          EditorGUI.BeginChangeCheck();
          {
            property.intValue = Convert.ToInt32(EditorGUI.EnumPopup(position, label, converted));
          }
          if (EditorGUI.EndChangeCheck()) {
            property.serializedObject.ApplyModifiedProperties();
          }

        } else {
          Debug.LogWarning($"Type supplied to {nameof(CastEnumAttribute)} in class '{property.serializedObject.targetObject.GetType().Name}' on field '{property.name}' does not evaluate to an Enum type.");
          EditorGUI.PropertyField(position, property, label);
        }


      } else {
        EditorGUI.PropertyField(position, property, label);
      }

      EditorGUI.EndProperty();
    }
  }

}



#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/DoIfAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;

  public abstract class DoIfAttributeDrawer : ForwardingPropertyDrawer {

    protected object realTargetObject;

    protected double GetCompareValue(SerializedProperty property, string conditionMember, FieldInfo finfo) {

      if (conditionMember == null || conditionMember == "") {
        Debug.LogWarning("Invalid Condition."); 
      }

      var condDelegate = finfo.DeclaringType.GetDelegateFromMember(conditionMember);
      if (condDelegate == null)
        return 0;

      if (realTargetObject == null)
        realTargetObject = property.GetParent();

      var valObj = condDelegate(realTargetObject);
      return valObj == null ? 0 : valObj.GetObjectValueAsDouble();
    }

    public static bool CheckDraw(DoIfAttribute warnIf, double referenceValue) {

      switch (warnIf.Compare) {
        case DoIfCompareOperator.Equal: return referenceValue.Equals(warnIf.CompareToValue);
        case DoIfCompareOperator.NotEqual: return !referenceValue.Equals(warnIf.CompareToValue);
        case DoIfCompareOperator.Less: return referenceValue < warnIf.CompareToValue;
        case DoIfCompareOperator.LessOrEqual: return referenceValue <= warnIf.CompareToValue;
        case DoIfCompareOperator.GreaterOrEqual: return referenceValue >= warnIf.CompareToValue;
        case DoIfCompareOperator.Greater: return referenceValue > warnIf.CompareToValue;
        case DoIfCompareOperator.NotZero: return referenceValue != 0;
        case DoIfCompareOperator.IsZero:  return referenceValue == 0;
      }
      return false;
    }

    public static bool CheckDraw(WarnIfAttribute warnIf, double referenceValue, double compareToValue) {

      switch (warnIf.Compare) {
        case DoIfCompareOperator.Equal: return referenceValue.Equals(compareToValue);
        case DoIfCompareOperator.NotEqual: return !referenceValue.Equals(compareToValue);
        case DoIfCompareOperator.Less: return referenceValue < compareToValue;
        case DoIfCompareOperator.LessOrEqual: return referenceValue <= compareToValue;
        case DoIfCompareOperator.GreaterOrEqual: return referenceValue >= compareToValue;
        case DoIfCompareOperator.Greater: return referenceValue > compareToValue;
        case DoIfCompareOperator.NotZero: return referenceValue != 0;
        case DoIfCompareOperator.IsZero:  return referenceValue == 0;
      }
      return false;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/DrawIfAttributeDrawer.cs

namespace Fusion.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(DrawIfAttribute))]
  public class DrawIfAttributeDrawer : DoIfAttributeDrawer {
    public DrawIfAttribute Attribute => (DrawIfAttribute)attribute;

    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      
      double otherValue = GetCompareValue(property, Attribute.ConditionMember, fieldInfo);

      if (Attribute.Hide == false || CheckDraw(Attribute, otherValue)) {
        return base.GetPropertyHeightInternal(property, label);
      }

      // -2 is required rather than zero, otherwise a space is added for hidden fields.
      return -2;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      double otherValue = GetCompareValue(property, Attribute.ConditionMember, fieldInfo);
      
      var readOnly = Attribute.Hide == false;
      var draw = CheckDraw(Attribute, otherValue);

      if (readOnly || draw) {
        EditorGUI.BeginDisabledGroup(!draw);

        base.OnGUIInternal(position, property, label);

        EditorGUI.EndDisabledGroup();
      }
    }    
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/EditorDisabledAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(EditorDisabledAttribute))]
  public class EditorDisabledDecoratorDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      var attr = attribute as EditorDisabledAttribute;
      if (attr.HideInRelease && NetworkRunner.BuildType == NetworkRunner.BuildTypes.Release)
        return;

      try {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
      } finally {
        GUI.enabled = true;
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      // If we are hiding this property, need to use negative height to erase the margin with a -2
      var attr = attribute as EditorDisabledAttribute;
      if (attr.HideInRelease && NetworkRunner.BuildType == NetworkRunner.BuildTypes.Release)
        return -2;

      return EditorGUI.GetPropertyHeight(property);
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/EditorDisabledGroupAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(EditorDisabledGroupAttribute))]
  public class EditorDisabledGroupAttributeDrawer : DecoratorDrawer {
    public override void OnGUI(Rect position) {
      if (((EditorDisabledGroupAttribute)attribute).Begin) {
        EditorGUI.BeginDisabledGroup(true);
      } else {
        EditorGUI.EndDisabledGroup();
      }
    }

    public override float GetHeight() {
      return 0;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/EnumMaskAttributeDrawer.cs

namespace Fusion.Editor {

  using UnityEngine;
  using System;
  using UnityEditor;

  [CustomPropertyDrawer(typeof(EnumMaskAttribute))]
  public class EnumMaskAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect r, SerializedProperty property, GUIContent label) {
      string[] names;
      var maskattr = attribute as EnumMaskAttribute;
      if (maskattr.castTo != null)
        names = Enum.GetNames(maskattr.castTo);
      else
        names = property.enumDisplayNames;

      if (maskattr.definesZero) {
        string[] truncated = new string[names.Length - 1];
        Array.Copy(names, 1, truncated, 0, truncated.Length);
        names = truncated;
      }

      //_property.intValue = System.Convert.ToInt32(EditorGUI.EnumMaskPopup(_position, _label, (SendCullMask)_property.intValue));
      property.intValue = EditorGUI.MaskField(r, label, property.intValue, names);

    }
  }
}



#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/FixedBufferPropertyAttributeDrawer.cs

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
  public unsafe class FixedBufferPropertyAttributeDrawer : PropertyDrawerWithErrorHandling {
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

      private FieldInfo _surrogateField = typeof(UnitySurrogateBaseWrapper).GetField(nameof(UnitySurrogateBaseWrapper.Surrogate));
      private Dictionary<(Type, string, int), PropertyEntry> _used = new Dictionary<(Type, string, int), PropertyEntry>();
      private Dictionary<Type, Stack<UnitySurrogateBaseWrapper>> _wrappersPool = new Dictionary<Type, Stack<UnitySurrogateBaseWrapper>>();

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
        var wrappers = new UnitySurrogateBaseWrapper[key.Item3];
        if (!_wrappersPool.TryGetValue(type, out var pool)) {
          pool = new Stack<UnitySurrogateBaseWrapper>();
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
            wrappers[i] = ScriptableObject.CreateInstance<UnitySurrogateBaseWrapper>();
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
        public UnitySurrogateBaseWrapper[] Wrappers;
      }
    }

    private class UnitySurrogateBaseWrapper : ScriptableObject {
      [SerializeReference]
      public UnitySurrogateBase Surrogate;
      [NonSerialized]
      public SerializedProperty SurrogateProperty;
      [NonSerialized]
      public Type SurrogateType;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ForwardingPropertyDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  // Unity 2021.1 introduced support for multiple property drawers,
  // but Odin does not seem to respect chaining
#if !UNITY_2021_1_OR_NEWER
  // in newer Unity versions it is better to simply ignore this attribute
  [CustomPropertyDrawer(typeof(MultiPropertyDrawersFixAttribute))]
#endif
  public class ForwardingPropertyDrawer : PropertyDrawer {

    /// <summary>
    /// The drawer that's been chosen by Unity; its job is to
    /// iterate all ForwardingPropertyDrawerBase drawers
    /// that'd be created had Unity 2020.3 supported multiple
    /// property drawers - including self.
    /// </summary>
    private ForwardingPropertyDrawer _mainDrawer;
    private List<ForwardingPropertyDrawer> _subDrawers;
    private PropertyDrawer _nextDrawer;
    private bool _isLastDrawer;

    public List<ForwardingPropertyDrawer> PropertyDrawers => _subDrawers;
    public PropertyDrawer NextDrawer => _nextDrawer;
    public bool UsesManualChaining { get; private set; }


    [Obsolete("Derived classes should override and call OnGUIInternal", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public sealed override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
      EnsureInitialized();
      Debug.Assert(_mainDrawer == this);
      Debug.Assert(_subDrawers != null);
      Debug.Assert(_subDrawers.Count > 0);
      _subDrawers[0].OnGUIInternal(position, prop, label);
    }

    [Obsolete("Derived classes should override and call GetPropertyHeightInternal", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public sealed override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
      EnsureInitialized();
      Debug.Assert(_mainDrawer == this);
      Debug.Assert(_subDrawers != null);
      Debug.Assert(_subDrawers.Count > 0);
      return _subDrawers[0].GetPropertyHeightInternal(prop, label);
    }

    protected virtual float GetPropertyHeightInternal(SerializedProperty prop, GUIContent label) {
      Debug.Assert(_mainDrawer != null);
      return _mainDrawer.InvokeGetPropertyHeightOnNextDrawer(this, prop, label);
    }

    protected virtual void OnGUIInternal(Rect position, SerializedProperty prop, GUIContent label) {
      Debug.Assert(_mainDrawer != null);
      _mainDrawer.InvokeOnGUIOnNextDrawer(this, position, prop, label);
    }

    void InvokeOnGUIOnNextDrawer(ForwardingPropertyDrawer current, Rect position, SerializedProperty prop, GUIContent label) {
      Debug.Assert(_mainDrawer == this);
      var index = _subDrawers.IndexOf(current);
      if (index < _subDrawers.Count - 1) {
        _subDrawers[index + 1].OnGUIInternal(position, prop, label);
      } else {
        if (_nextDrawer != null) {
          _nextDrawer.OnGUI(position, prop, label);
        } else {
          FusionEditorGUI.ForwardPropertyField(position, prop, label, prop.IsArrayProperty() ? true : prop.isExpanded, lastDrawer: _isLastDrawer);
        }
      }
    }

    float InvokeGetPropertyHeightOnNextDrawer(ForwardingPropertyDrawer current, SerializedProperty prop, GUIContent label) {
      Debug.Assert(_mainDrawer == this);
      var index = _subDrawers.IndexOf(current);
      if (index < _subDrawers.Count - 1) {
        return _subDrawers[index + 1].GetPropertyHeightInternal(prop, label);
      } else {
        return (_nextDrawer?.GetPropertyHeight(prop, label) ?? EditorGUI.GetPropertyHeight(prop, label));
      }
    }

    void EnsureInitialized() {

      if (_mainDrawer != null || _subDrawers != null) {
        return;
      }

      Debug.Assert(attribute != null);

      _subDrawers = new List<ForwardingPropertyDrawer>();
      _mainDrawer = this;
      _nextDrawer = null;

      
      bool isOdin = IsUsingOdinInspector();
      bool needsManualChaining =
#if UNITY_2021_1_OR_NEWER
        isOdin;
#else
        true;
#endif

      UsesManualChaining = needsManualChaining;

      bool isLastDrawer = false;
      bool foundSelf = false;

      var fieldAttributes = fieldInfo != null ? UnityInternal.ScriptAttributeUtility.GetFieldAttributes(fieldInfo) : null;

      if (fieldAttributes != null) {

#if UNITY_2021_1_OR_NEWER
        Debug.Assert(fieldAttributes.OrderBy(x =>  x.order).SequenceEqual(fieldAttributes), $"Expected field attributes to be sorted");
#else
        Debug.Assert(fieldAttributes.OrderBy(x => -x.order).SequenceEqual(fieldAttributes), $"Expected field attributes to be sorted in reverse order");
        fieldAttributes.Sort((a, b) => a.order - b.order);
#endif
        Debug.Assert(fieldAttributes.Count > 0);

        for (int i = 0; i < fieldAttributes.Count; ++i) {

          var fieldAttribute = fieldAttributes[i];

          var attributeDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(fieldAttribute.GetType());
          if (attributeDrawerType == null) {
            TraceField($"No drawer for {attributeDrawerType}");
            continue;
          }

          if (attributeDrawerType.IsSubclassOf(typeof(DecoratorDrawer))) {
            // decorators are their own thing
            continue;
          }

          Debug.Assert(attributeDrawerType.IsSubclassOf(typeof(PropertyDrawer)));

          if (fieldAttribute.Equals(attribute)) {
            // self
            _subDrawers.Add(this);
            Debug.Assert(foundSelf == false);
            foundSelf = true;
            isLastDrawer = true;
            TraceField($"Found self at {i} ({this})");
            continue;
          }

          isLastDrawer = false;
          if (needsManualChaining) {
            // create the drawer
            var drawer = (PropertyDrawer)Activator.CreateInstance(attributeDrawerType);
            UnityInternal.PropertyDrawer.SetAttribute(drawer, fieldAttribute);
            UnityInternal.PropertyDrawer.SetFieldInfo(drawer, fieldInfo);

            if (drawer is ForwardingPropertyDrawer forwardingDrawer) {
              // can keep on chaining
              _subDrawers.Add(forwardingDrawer);
              forwardingDrawer._mainDrawer = this;
              TraceField($"Chaining {drawer} for attribute {fieldAttribute}");
            } else {
              if (_nextDrawer == null) {
                TraceField($"The final drawer is {drawer} for attribute {fieldAttribute}");
              } else {
                TraceField($"There was a drawer after the final one ({drawer} for {fieldAttribute}), overriding");
              }
              _nextDrawer = drawer;
            }
          }
          
        }
      }

      if (!foundSelf) {
        TraceField($"Force-adding self");
        _subDrawers.Add(this);
      }

      if (_nextDrawer == null && (needsManualChaining || isLastDrawer) && fieldInfo != null) {
        // try creating type drawer instead
        var typeDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(fieldInfo.FieldType);
        if (typeDrawerType != null) {
          var drawer = (PropertyDrawer)Activator.CreateInstance(typeDrawerType);
          UnityInternal.PropertyDrawer.SetFieldInfo(drawer, fieldInfo);
          TraceField($"Found final drawer is type drawer ({drawer})");
          _nextDrawer = drawer;
        }
      }

      if (!needsManualChaining && isLastDrawer) {
        _isLastDrawer = true;
      }
    }

    internal void AddDrawer(PropertyDrawer drawer) {
      EnsureInitialized();
      if (drawer is ForwardingPropertyDrawer forwardingDrawer) {
        forwardingDrawer._mainDrawer = this;
        forwardingDrawer._subDrawers = null;
        forwardingDrawer._nextDrawer = null;
        FusionEditorGUI.InsertPropertyDrawerByAttributeOrder(_subDrawers, forwardingDrawer);
      } else {
        // TODO: add order checks
        _nextDrawer = drawer;
      }
    }

    internal void InitInjected(PropertyDrawer next) {
      _mainDrawer = this;
      _subDrawers = new List<ForwardingPropertyDrawer> { this };
      _nextDrawer = next;
    }

    public PropertyDrawer GetNextDrawer(SerializedProperty property) {
      if (UsesManualChaining) {
        return GetNextDrawerInner(this);
      }

      if (NextDrawer != null) {
        return NextDrawer;
      }

#if UNITY_2021_1_OR_NEWER
      var handler = UnityInternal.ScriptAttributeUtility.propertyHandlerCache.GetHandler(property);
      var drawers = handler.m_PropertyDrawers;
      var index = drawers.IndexOf(this);
      if (index >= 0 && index < drawers.Count - 1) {
        return drawers[index + 1];
      }
#endif
      return null;
    }

    bool IsUsingOdinInspector() {
#if ODIN_INSPECTOR
      var stack = new System.Diagnostics.StackTrace(3);
      var odinAsm = typeof(Sirenix.OdinInspector.Editor.OdinEditor).Assembly;
      return stack.GetFrames()
        .SkipWhile(x => x.GetMethod().DeclaringType != UnityInternal.PropertyHandler.InternalType)
        .SkipWhile(x => x.GetMethod().DeclaringType.Assembly != odinAsm)
        .Any();
#else
      return false;
#endif
    }

    PropertyDrawer GetNextDrawerInner(PropertyDrawer drawer) {
      Debug.Assert(_mainDrawer != null);
      if (_mainDrawer != this) {
        return _mainDrawer.GetNextDrawerInner(drawer);
      }
      if (drawer is ForwardingPropertyDrawer forwarding) {
        var index = _subDrawers.IndexOf(forwarding);
        if (index < 0) {
          return null;
        } else if (index >= _subDrawers.Count - 1) {
          return _nextDrawer;
        } else {
          return _subDrawers[index + 1];
        }
      }
      return null;
    }

    [System.Diagnostics.Conditional("FUSION_EDITOR_TRACE")]
    private void TraceField(string message) {
      FusionEditorLog.TraceInspector($"[{fieldInfo.DeclaringType.Name}.{fieldInfo.Name}] {message}");
    }

  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/HitboxRootEditor.cs

// Deleted Aug 5 2021

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/IFoldablePropertyDrawer.cs

namespace Fusion {
  public interface IFoldablePropertyDrawer {
    bool HasFoldout(UnityEditor.SerializedProperty property);
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/InlineEditorAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEditorInternal;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(InlineEditorAttribute))]
  public class InlineEditorAttributeDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      bool enterChildren = true;
      int parentDepth = property.depth;

      using (new FusionEditorGUI.PropertyScope(position, label, property)) {
        for (var prop = property; property.NextVisible(enterChildren) && property.depth > parentDepth; enterChildren = false) {
          position.height = EditorGUI.GetPropertyHeight(prop);
          EditorGUI.PropertyField(position, prop);
          position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        }
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      property.isExpanded = true;

      float result = -EditorGUIUtility.standardVerticalSpacing;
      bool enterChildren = true;
      int parentDepth = property.depth;

      for (var prop = property; property.NextVisible(enterChildren) && property.depth > parentDepth; enterChildren = false) {
        result += EditorGUI.GetPropertyHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
      }

      return result;
    }
  }

  //[CustomPropertyDrawer(typeof(DictionaryAdapter), true)]
  //public class DictionaryAdapterDrawer : PropertyDrawerWithErrorHandling {

  //  private Dictionary<string, ReorderableList> reorderables = new Dictionary<string, ReorderableList>();

  //  protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
  //    GetOrCreateList(property).DoList(position);
  //  }

  //  public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
  //    return GetOrCreateList(property).GetHeight();
  //  }

  //  private ReorderableList GetOrCreateList(SerializedProperty property) {
  //    if (reorderables.TryGetValue(property.propertyPath, out var reorderable)) {
  //      return reorderable;
  //    }

  //    var entries = property.FindPropertyRelativeOrThrow("Entries");

  //    reorderable = new ReorderableList(property.serializedObject, entries);

  //    reorderable.headerHeight = 0.0f;

  //    reorderable.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
  //      var entry = entries.GetArrayElementAtIndex(index);
  //      EditorGUI.PropertyField(rect, entry, true);
  //    };

  //    reorderable.elementHeightCallback = (int index) => {
  //      var entry = entries.GetArrayElementAtIndex(index);
  //      return EditorGUI.GetPropertyHeight(entry);
  //    };


  //    reorderables.Add(property.propertyPath, reorderable);
  //    return reorderable;
  //  }
  //}
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/InlineHelpAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(InlineHelpAttribute))]
  internal class InlineHelpAttributeDrawer : ForwardingPropertyDrawer {

    internal ReserveArrayPropertyHeightDecorator ArrayHeightDecorator;

    private FusionEditorGUI.PropertyInlineHelpInfo _helpInfo;
    private bool _initialized;

    public InlineHelpAttributeDrawer() {
    }

    private new InlineHelpAttribute attribute => (InlineHelpAttribute)base.attribute;
    

    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {

      if (property.IsArrayProperty()) {
        return base.GetPropertyHeightInternal(property, label);
      }

      EnsureInitialized(property);
      
      float height = base.GetPropertyHeightInternal(property, label);
      if (height <= 0) {
        return height;
      }

      if (FusionEditorGUI.IsHelpExpanded(this, property.propertyPath)) {
        height += FusionEditorGUI.GetInlineHelpRect(_helpInfo.Summary).height;
      }

      return height;
    }

    private void SetRequiredHeight(float value) {
      if (ArrayHeightDecorator != null) {
        ArrayHeightDecorator.Height = value;
      }
    }


    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      EnsureInitialized(property);

      if (position.height <= 0 || _helpInfo == null) {
        // ignore
        base.OnGUIInternal(position, property, label);
        return;
      } 
      
      if (_helpInfo.Summary == null) {
        // just the tooltip
        ReplaceTooltip(ref label);
        base.OnGUIInternal(position, property, label);
        return;
      }

      var nextDrawer = GetNextDrawer(property);

      bool hasFoldout = HasFoldout(nextDrawer, property);
      if (hasFoldout && EditorGUI.indentLevel == 0 && attribute.ButtonPlacement == InlineHelpButtonPlacement.BeforeLabel) {
        // help button won't fit
        ReplaceTooltip(ref label);
        base.OnGUIInternal(position, property, label);
        return;
      }

      Rect buttonRect = FusionEditorGUI.GetInlineHelpButtonRect(attribute.ButtonPlacement, position, label, hasFoldout);
      bool wasExpanded = FusionEditorGUI.IsHelpExpanded(this, property.propertyPath);
      if (wasExpanded) {
        var helpRect = FusionEditorGUI.GetInlineHelpRect(_helpInfo.Summary);
        FusionEditorGUI.DrawInlineHelp(_helpInfo.Summary, position, helpRect);
        label = new GUIContent(label);
        position.height = position.height - helpRect.height;
        SetRequiredHeight(helpRect.height);
      } else {
        SetRequiredHeight(0.0f);
      }
        
      if (FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded, doButton: true, doIcon: false)) {
        FusionEditorGUI.SetHelpExpanded(this, property.propertyPath, !wasExpanded);

        if (wasExpanded) {
          SetRequiredHeight(0.0f);
        } else {
          SetRequiredHeight(FusionEditorGUI.GetInlineHelpRect(_helpInfo.Summary).y);
        }
      }

      ReplaceTooltip(ref label);
      base.OnGUIInternal(position, property, label);

      // paint over what the inspector has drawn
      FusionEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded, doButton: false, doIcon: true);

      // a hacky fix for icons
      if (nextDrawer is PropertyDrawerWithErrorHandling next) {
        next.IconOffset = buttonRect.width + 2;
      }
    }


    private void EnsureInitialized(SerializedProperty property) {
      if (_initialized) {
        return;
      }

      _initialized = true;

      if (property.IsArrayElement()) {
        _helpInfo = null;
        return;
      }

      var rootType = property.serializedObject.targetObject.GetType();
      var type = fieldInfo?.DeclaringType ?? rootType;

      _helpInfo = type.GetInlineHelpInfo(property.name);
    }

    private void ReplaceTooltip(ref GUIContent label) {
      if (!string.IsNullOrEmpty(_helpInfo.Label.tooltip)) {
        label = new GUIContent(label);
        if (string.IsNullOrEmpty(label.tooltip)) {
          label.tooltip = _helpInfo.Label.tooltip;
        } else {
          label.tooltip += "\n" + _helpInfo.Label.tooltip;
        }
      }
    }

    private bool HasFoldout(PropertyDrawer nextDrawer, SerializedProperty property) {
      if (nextDrawer is IFoldablePropertyDrawer foldable) {
        return foldable.HasFoldout(property);
      }
      if (property.IsArrayProperty()) {
        return true;
      }
      if (property.propertyType == SerializedPropertyType.Generic) {
        return true;
      }
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/KeyValuePairAttributeDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(KeyValuePairAttribute))]
  public class KeyValuePairAttributeDrawer : PropertyDrawer {

    const string KeyPropertyName = "Key";
    const string ValuePropertyName = "Value";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var origLabel = new GUIContent(label);
      var keyProperty = property.FindPropertyRelativeOrThrow(KeyPropertyName);
      var keyHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, EditorGUI.GetPropertyHeight(keyProperty));

      var elementRect = position;
      elementRect.height = EditorGUIUtility.singleLineHeight;

      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        var keyRect = position;
        keyRect.height = keyHeight;
        keyRect.xMin += EditorGUIUtility.labelWidth;
        EditorGUI.PropertyField(keyRect, keyProperty, GUIContent.none, true);
      }

      if (EditorGUI.PropertyField(elementRect, property, origLabel, false)) {
        var valueProperty = property.FindPropertyRelativeOrThrow(ValuePropertyName);
        using (new EditorGUI.IndentLevelScope()) {
          var valueHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, EditorGUI.GetPropertyHeight(valueProperty));
          var valueRect = position;
          valueRect.yMin += keyHeight + EditorGUIUtility.standardVerticalSpacing;
          valueRect.height = valueHeight;
          EditorGUI.PropertyField(valueRect, valueProperty, true); 
        }
      }

    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var keyProperty = property.FindPropertyRelativeOrThrow(KeyPropertyName);
      var keyHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, EditorGUI.GetPropertyHeight(keyProperty));

      var result = keyHeight;

      if (property.isExpanded) {
        var valueProperty = property.FindPropertyRelativeOrThrow(ValuePropertyName);
        result += EditorGUIUtility.standardVerticalSpacing;
        result += Mathf.Max(EditorGUIUtility.singleLineHeight, EditorGUI.GetPropertyHeight(valueProperty, true));
      }

      return result;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkBoolDrawer.cs

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


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkObjectGuidDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkObjectGuid))]
  public class NetworkObjectGuidDrawer : PropertyDrawerWithErrorHandling, IFoldablePropertyDrawer {

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

    bool IFoldablePropertyDrawer.HasFoldout(SerializedProperty property) {
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkPrefabAssetDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkPrefabAsset))]
  public class NetworkPrefabAssetDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        position.width -= 40;

        // handle dragging of NetworkObjects
        NetworkObject draggedObject = null;

        if (Event.current.type == EventType.DragPerform || Event.current.type == EventType.DragUpdated) {

          if (position.Contains(Event.current.mousePosition) && GUI.enabled) {

            draggedObject = DragAndDrop.objectReferences
              .OfType<NetworkObject>()
              .FirstOrDefault(x => EditorUtility.IsPersistent(x));

            if (draggedObject == null) {
              draggedObject = DragAndDrop.objectReferences
                .OfType<GameObject>()
                .Where(x => EditorUtility.IsPersistent(x))
                .Select(x => x.GetComponent<NetworkObject>())
                .FirstOrDefault(x => x != null);
            }

            if (draggedObject != null) {
              DragAndDrop.visualMode = DragAndDropVisualMode.Generic;

              if (Event.current.type == EventType.DragPerform) {
                if (NetworkProjectConfigUtilities.TryGetPrefabAsset(draggedObject.NetworkGuid, out NetworkPrefabAsset prefabAsset)) { 
                  property.objectReferenceValue = prefabAsset;
                  property.serializedObject.ApplyModifiedProperties();
                }
              }

              Event.current.Use();
            }
          }
        }

        EditorGUI.PropertyField(position, property, GUIContent.none, false);

        using (new EditorGUI.DisabledScope(property.objectReferenceValue == null)) {
          position.x += position.width;
          position.width = 40;
          if (GUI.Button(position, "Ping")) {
            // ping the main asset
            var info = (NetworkPrefabAsset)property.objectReferenceValue;
            if (NetworkProjectConfigUtilities.TryResolvePrefab(info.AssetGuid, out var prefab)) { 
              EditorGUIUtility.PingObject(prefab.gameObject);
            }
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkPrefabAssetEditor.cs

namespace Fusion.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(NetworkPrefabAsset), true)]
  [CanEditMultipleObjects]
  public class NetworkPrefabAssetEditor : UnityEditor.Editor {

    private UnityEditor.Editor[] _gameObjectEditors = new UnityEditor.Editor[1];
    private bool multiSelection = false;

    public override void OnInspectorGUI() {

      FusionEditorGUI.InjectPropertyDrawers(serializedObject);
      FusionEditorGUI.DrawDefaultInspector(serializedObject);

      if (targets.Length > 1) {
        EditorGUILayout.LabelField("Prefabs");
      }

      foreach (var prefab in targets
        .Cast<NetworkPrefabAsset>()
        .OrderBy(x => x.name)
        .Select(x => {
          NetworkProjectConfigUtilities.TryResolvePrefab(x.AssetGuid, out var prefab);
          return prefab;
        })) {

        if (targets.Length > 1) {
          EditorGUILayout.ObjectField(prefab, typeof(NetworkObject), false);
        } else {
          EditorGUILayout.ObjectField("Prefab", prefab, typeof(NetworkObject), false);
        }
      }

      if (targets.OfType<NetworkPrefabAssetMissing>().Any()) {
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox($"Prefab assets have their type changed to {"MISSING"} in case a prefab is removed or is set as not spawnable. " +
          $"If a prefab is restored/made spawnable again, all the references will once again point to the same prefab. Having such placeholders also makes it trivial " +
          $"to find any assets referencing missing prefabs.", MessageType.Info);

        if (GUILayout.Button("Destroy Selected Missing Prefab Placeholders")) {
          foreach (var asset in targets.OfType<NetworkPrefabAssetMissing>().ToList()) {
            DestroyImmediate(asset, true);
          }
          GUIUtility.ExitGUI();
        }
      }

      RefreshEditors();
    }

    private void RefreshEditors() {
      Array.Resize(ref _gameObjectEditors, targets.Length);

      int i = 0;
      foreach (NetworkPrefabAsset info in targets) {
        var prefab = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(info.AssetGuid.ToString("N"))) as GameObject;
        if (prefab == null) {
          _gameObjectEditors[i] = null;
        } else if (_gameObjectEditors[i]?.target != prefab) {
          _gameObjectEditors[i] = UnityEditor.Editor.CreateEditor(prefab);
        }
        ++i;
      }
    }

    private void OnDisable() {
      for (int i = 0; i < _gameObjectEditors.Length; ++i) {
        if (_gameObjectEditors[i]) {
          DestroyImmediate(_gameObjectEditors[i]);
          _gameObjectEditors[i] = null;
        }
      }
    }

    public override bool HasPreviewGUI() {
      // GameObject preview is messed
      multiSelection = targets.Length > 1;
      return true;
    }

    public override GUIContent GetPreviewTitle() {
      if (NetworkProjectConfigUtilities.TryGetPrefabSource(target.AssetGuid, out INetworkPrefabSource entry)) {
        return new GUIContent(entry.EditorSummary);
      } else {
        return new GUIContent("null");
      }

    }

    public override void OnPreviewGUI(Rect r, GUIStyle background) {
      var assetPath = AssetDatabase.GUIDToAssetPath(target.AssetGuid.ToString("N"));
      var prefab = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
      if (prefab == null) {
        EditorGUI.HelpBox(r, $"Prefab not found!\nGuid: {target.AssetGuid}\nPath: {assetPath}", MessageType.Error);
      } else {

        float pickerHeight = EditorGUIUtility.singleLineHeight;
        float marginBottom = 2.0f;


        var pathRect = new Rect(r);
        pathRect.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(pathRect, assetPath);

        r.yMin = pathRect.yMax;
        r.height = Mathf.Max(1, r.height - pickerHeight - marginBottom);

        if (!multiSelection) {
          RefreshEditors();
        }

        var editor = _gameObjectEditors?.FirstOrDefault(x => x?.target == prefab);
        if (editor != null) {
          editor.OnPreviewGUI(r, background);
        }


        var pickerRect = new Rect(r);
        pickerRect.y = r.yMax;
        pickerRect.height = pickerHeight;
        EditorGUI.ObjectField(pickerRect, prefab, typeof(GameObject), false);
      }
    }

    private new NetworkPrefabAsset target => (NetworkPrefabAsset)base.target;
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkPrefabAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkPrefabAttribute))]
  public class NetworkPrefabAttributeDrawer : PropertyDrawerWithErrorHandling, IFoldablePropertyDrawer {

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
          if (no == null) {
            SetError($"Prefab {prefab} does not have a {nameof(NetworkObject)} component");
          } else if (!no.NetworkGuid.IsValid) {
            SetError($"Prefab {prefab} needs to be reimported.");
          } else if (!NetworkProjectConfigUtilities.TryResolvePrefab(no.NetworkGuid, out var resolved)) {
            SetError($"Prefab {prefab} with guid {no.NetworkGuid} not found in the config. Try reimporting.");
          } else if (resolved != no) {
            SetError($"Prefab {prefab} with guid {no.NetworkGuid} resolved to a different prefab: {resolved}.");
          }
        }
      }
    }

    bool IFoldablePropertyDrawer.HasFoldout(SerializedProperty property) {
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkPrefabRefDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkPrefabRef))]
  public class NetworkPrefabRefDrawer : PropertyDrawerWithErrorHandling, IFoldablePropertyDrawer {

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var prefabRef = NetworkObjectGuidDrawer.GetValue(property);

      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        NetworkObject prefab = null;

        if (prefabRef.IsValid && !NetworkProjectConfigUtilities.TryResolvePrefab(prefabRef, out prefab)) {
          var prefabActualPath = AssetDatabase.GUIDToAssetPath(prefabRef.ToString("N"));
          if (!string.IsNullOrEmpty(prefabActualPath)) {
            var go = AssetDatabase.LoadMainAssetAtPath(prefabActualPath) as GameObject;
            if ( go != null ) {
              prefab = go.GetComponent<NetworkObject>();
            }
          }

          if (!prefab) {
            SetError($"Prefab with guid {prefabRef} not found.");
          }
        }

        EditorGUI.BeginChangeCheck();

        var prefabGo = (GameObject)EditorGUI.ObjectField(position, prefab != null ? prefab.gameObject : null, typeof(GameObject), false);

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
          if (prefabGo) {
            prefab = prefabGo.GetComponent<NetworkObject>();
            if (!prefab) {
              SetError($"Prefab {prefabGo} does not have a {nameof(NetworkObject)} component");
              return;
            }
          } else {
            prefab = null;
          }

          if (prefab) {
            prefabRef = prefab.NetworkGuid;
          } else {
            prefabRef = default;
          }
          NetworkObjectGuidDrawer.SetValue(property, prefabRef);
          property.serializedObject.ApplyModifiedProperties();
        }

        SetInfo($"{prefabRef}");


        if (prefab) {
          var expectedPrefabRef = prefab.NetworkGuid;
          if (!prefabRef.Equals(expectedPrefabRef)) {
            SetError($"Resolved {prefab} has a different guid ({expectedPrefabRef}) than expected ({prefabRef}). " +
              $"This can happen if prefabs are incorrectly resolved, e.g. when there are multiple resources of the same name.");
          } else if (!expectedPrefabRef.IsValid) {
            SetError($"Prefab {prefab} needs to be reimported.");
          } else if (!NetworkProjectConfigUtilities.TryResolvePrefab(expectedPrefabRef, out _)) {
            SetError($"Prefab {prefab} with guid {prefab.NetworkGuid} not found in the config. Try reimporting.");
          } else {
            // ClearError();
          }
        }
      }
    }

    bool IFoldablePropertyDrawer.HasFoldout(SerializedProperty property) {
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NetworkStringDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(NetworkString<>))]
  public class NetworkStringDrawer : PropertyDrawerWithErrorHandling, IFoldablePropertyDrawer {

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

    bool IFoldablePropertyDrawer.HasFoldout(SerializedProperty property) {
      return false;
    }

  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/NormalizedRectAttributeDrawer.cs


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


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/Pow2SliderAttributeDrawer.cs

namespace Fusion.Editor {

  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Pow2SliderAttribute))]
  public class Pow2SliderAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect r, SerializedProperty property, GUIContent label) {

      EditorGUI.BeginProperty(r, label, property);

      var attr = attribute as Pow2SliderAttribute;

      bool showUnits = attr.Unit != Units.None;
      float fieldWidth = showUnits ? UnitAttributeDecoratorDrawer.FIELD_WIDTH : 50;

      EditorGUI.LabelField(r, label);

      float labelw = EditorGUIUtility.labelWidth;

      int exp;
      if (attr.AllowZero && property.intValue == 0)
        exp = 0;
      else
        exp = (int)Math.Log(property.intValue, 2);

      int oldValue = property.intValue;

      Rect rightRect = new Rect(r) { xMin = r.xMin + EditorGUIUtility.labelWidth + 2 };
      Rect r1 = new Rect(rightRect) { xMax = rightRect.xMax - fieldWidth - 4 };
      Rect r2 = new Rect(rightRect) { xMin = rightRect.xMax - fieldWidth };
      int newExp = (int)GUI.HorizontalSlider(r1, exp, attr.MinPower, attr.MaxPower);

      if (newExp != exp) {
        int newValue = (attr.AllowZero && newExp == 0) ? 0 : (int)Math.Round(Math.Pow(2, newExp));

        property.intValue = newValue;

        if (property.intValue != newValue)
          Debug.LogWarning(property.name + " Pow2SliderAttribute range exceeds the possible value range of its field type.");

        property.serializedObject.ApplyModifiedProperties();
      }

      EditorGUI.BeginChangeCheck();
      int newVal = EditorGUI.DelayedIntField(r2, property.intValue);
      if (newVal != property.intValue) {

        // Round to the nearest even exponent
        int rounded;
        if (attr.AllowZero && newVal == 1)
          rounded = 0;
        else
          rounded = (int)Math.Pow(2, (int)Math.Log(newVal, 2));

        property.intValue = rounded;

        if (property.intValue != rounded)
          Debug.LogWarning(property.name + " Pow2SliderAttribute range exceeds the possible value range of its field type.");

        property.serializedObject.ApplyModifiedProperties();
      }

      if (showUnits) {
        var (style, name) = UnitAttributeDecoratorDrawer.GetOverlayStyle(attr.Unit);
        GUI.Label(r, name.l, style);
      }

      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return EditorGUI.GetPropertyHeight(property);
    }
  }

}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/PropertyDrawerWithErrorHandling.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  public abstract class PropertyDrawerWithErrorHandling : PropertyDrawer {
    private SerializedProperty _currentProperty;
    private string _info;
    public float IconOffset;

    struct Entry {
      public string message;
      public MessageType type;
    }

    private Dictionary<string, Entry> _errors = new Dictionary<string, Entry>();
    private bool _hadError;

    public override sealed void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      Debug.Assert(_currentProperty == null);

      var decoration = GetDecoration(property);

      if (decoration != null) {
        DrawDecoration(position, decoration.Value, label != GUIContent.none, drawButton: true, drawIcon: false);
      }


      _currentProperty = property;
      _hadError = false;
      _info = null;

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
        DrawDecoration(position, decoration.Value, label != GUIContent.none, drawButton: false, drawIcon: true);
      }
    }

    private void DrawDecoration(Rect position, (string, MessageType, bool) decoration, bool hasLabel, bool drawButton = true, bool drawIcon = true) {
      var iconPosition = position;
      iconPosition.height = EditorGUIUtility.singleLineHeight;
      iconPosition.x -= IconOffset;
      FusionEditorGUI.Decorate(iconPosition, decoration.Item1, decoration.Item2, hasLabel, drawButton: drawButton, drawBorder: decoration.Item3);
    }

    private (string, MessageType, bool)? GetDecoration(SerializedProperty property) {
      if (_errors.TryGetValue(property.propertyPath, out var error)) {
        return (error.message, error.type, true);
      } else if (_info != null) {
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
      _errors[_currentProperty.propertyPath] = new Entry() {
        message = error,
        type = MessageType.Error
      };
    }

    protected void SetWarning(string warning) {
      if (_errors.TryGetValue(_currentProperty.propertyPath, out var entry) && entry.type == MessageType.Error) {
        return;
      }

      _errors[_currentProperty.propertyPath] = new Entry() {
        message = warning,
        type = MessageType.Warning
      };
    }

    protected void SetInfo(string message) {
      _info = message;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ReserveArrayPropertyHeightDecorator.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  internal class ReserveArrayPropertyHeightDecorator : DecoratorDrawer {

    public float Height;

    private bool IsGettingHeight() {
      var method = new System.Diagnostics.StackFrame(2)?.GetMethod();
      if (method == null) {
        // impossible to tell
        return false;
      }

      if (method.DeclaringType == UnityInternal.PropertyHandler.InternalType && method.Name == "GetHeight") {
        return true;
      }

      return false;
    }


    public override float GetHeight() {
      if (Height == 0.0f) {
        // nothing to do
        return 0.0f;
      }

      if (!IsGettingHeight()) {
        // being drawn, it seems
        return 0.0f;
      }

      return Height;
    }

    public override void OnGUI(Rect position) {
      // do nothing
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ResolveNetworkPrefabSourceUnityDrawer.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ResolveNetworkPrefabSourceUnityAttribute))]
  public class ResolveNetworkPrefabSourceUnityDrawer : PropertyDrawerWithErrorHandling {

    const int ThumbnailWidth = 20;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var source = (NetworkPrefabSourceUnityBase)property.objectReferenceValue;
      if (source == null) {
        EditorGUI.PropertyField(position, property, label);
        return;
      }

      var prefab = NetworkPrefabSourceFactory.ResolveOrThrow(source);

      EditorGUI.ObjectField(position, label, prefab.gameObject, typeof(GameObject), false);

      // draw thumbnail
      { 
        var pos = position;
        pos.x += EditorGUIUtility.labelWidth;
        pos.width = ThumbnailWidth;
        pos.x -= ThumbnailWidth;

        var objectType = property.objectReferenceValue.GetType();
        FusionEditorGUI.DrawTypeThumbnail(pos, objectType, "NetworkPrefabSourceUnity", tooltip: source.EditorSummary);
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ResourcePathAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEditorInternal;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnityResourcePathAttribute))]
  public class ResourcePathAttributeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      var attrib = (UnityResourcePathAttribute)attribute;
      
      using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        position.width -= 40;
        EditorGUI.PropertyField(position, property, GUIContent.none, false);
        UnityEngine.Object asset = null;

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
          position.x += position.width;
          position.width = 40;
          if (GUI.Button(position, "Ping")) {
            // ping the main asset
            EditorGUIUtility.PingObject(Resources.Load(path));
          }
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ScenePathAttributeDrawer.cs

// ---------------------------------------------------------------------------------------------
// <copyright>PhotonNetwork Framework for Unity - Copyright (C) 2020 Exit Games GmbH</copyright>
// <author>developer@exitgames.com</author>
// ---------------------------------------------------------------------------------------------

namespace Fusion.Editor {
  using UnityEngine;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using System.Linq;
  using System.IO;
  using System;

  [CanEditMultipleObjects]
  [CustomPropertyDrawer(typeof(ScenePathAttribute))]
  public class ScenePathAttributeDrawer : PropertyDrawerWithErrorHandling {

    private SceneAsset[] _allScenes;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      var oldScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
      if (oldScene == null && !string.IsNullOrEmpty(property.stringValue)) {

        // well, maybe by name then?
        _allScenes = _allScenes ?? AssetDatabase.FindAssets("t:scene")
          .Select(x => AssetDatabase.GUIDToAssetPath(x))
          .Select(x => AssetDatabase.LoadAssetAtPath<SceneAsset>(x))
          .ToArray();

        var matchedByName = _allScenes.Where(x => x.name == property.stringValue).ToList(); ;

        if (matchedByName.Count == 0) { 
          base.SetError($"Scene not found: {property.stringValue}");
        } else {
          oldScene = matchedByName[0];
          if (matchedByName.Count > 1) {
            base.SetWarning($"There are multiple scenes with this name");
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
          base.ClearError();
        }
      }
    }
  }

}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ScriptHeaderAttributeDrawer.cs

namespace Fusion.Editor {

  using UnityEngine;
  using UnityEditor;

  internal class ScriptHeaderAttributeDrawer : PropertyDrawer {

    internal class Attribute : PropertyAttribute {
      public ScriptHelpAttribute Settings;
    }

    internal new Attribute attribute => (Attribute)base.attribute;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var settings = attribute.Settings;

      if (settings == null ) {
        EditorGUI.PropertyField(position, property, label, property.isExpanded);
        return;
      }

      bool useEnhancedHeader = settings.BackColor != EditorHeaderBackColor.None;

      // ensures exact height correctness (might slightly differ due to scaling)
      position.height = useEnhancedHeader ? 24f : 18;

      var scriptType = property.serializedObject.targetObject.GetType();

      EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

      if (useEnhancedHeader) {
        using (new FusionEditorGUI.EnabledScope(true)) {
          Event e = Event.current;
          if (e.type == EventType.MouseDown && position.Contains(e.mousePosition)) {
            if (e.clickCount == 1) {
              if (!string.IsNullOrEmpty(settings.Url)) {
                Application.OpenURL(settings.Url);
              }
              EditorGUIUtility.PingObject(MonoScript.FromMonoBehaviour(property.serializedObject.targetObject as MonoBehaviour));
            } else {
              AssetDatabase.OpenAsset(MonoScript.FromMonoBehaviour(property.serializedObject.targetObject as MonoBehaviour));
            }
          }
          var scriptName = string.IsNullOrEmpty(settings.Title) ? scriptType.Name : settings.Title;
          EditorGUI.LabelField(position, ObjectNames.NicifyVariableName(scriptName).ToUpper(), FusionGUIStyles.GetFusionHeaderBackStyle((int)settings.BackColor));
        }
      } else {
        using (new FusionEditorGUI.EnabledScope(false)) {
          SerializedProperty prop = property.serializedObject.FindProperty("m_Script");
          EditorGUI.PropertyField(position, prop);
        }
      }


      //// Draw Icon overlay
      //var icon = FusionGUIStyles.GetFusionIconTexture(attribute.Settings.Icon);
      //if (icon != null) {
      //  GUI.DrawTexture(new Rect(position) { xMin = position.xMax - 128 }, icon);
      //}
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var settings = attribute.Settings;
      return settings == null ? base.GetPropertyHeight(property, label) : settings.BackColor != EditorHeaderBackColor.None ? 24.0f : 18.0f;
    }
  }
}



#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/SerializableDictionaryDrawer.cs

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
  public class SerializableDictionaryDrawer : PropertyDrawerWithErrorHandling {
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


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/ToggleLeftAttributeDrawer.cs

namespace Fusion.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
  public class ToggleLeftAttributeDrawer : PropertyDrawer {

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


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/UnitAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnitAttribute))]
  public class UnitAttributeDecoratorDrawer : PropertyDrawer {

    public const float FIELD_WIDTH = 130;
    public const int   MAX_PLACES = 6;

    static Dictionary<Units, (GUIStyle, (string l , string s))> _unitStyles = new Dictionary<Units, (GUIStyle, (string l, string s))>();

    // Style used for the units name which overlays on top of the edit field.
    internal static (GUIStyle, (string l, string s)) GetOverlayStyle(Units unit) {
      if (_unitStyles.TryGetValue(unit, out var styleAndName) == false) {
        GUIStyle style;
        style               = new GUIStyle(EditorStyles.miniLabel);
        style.alignment     = TextAnchor.MiddleRight;
        style.contentOffset = new Vector2(-2, 0);

        style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(255f/255f, 221/255f, 0/255f, 1f) : Color.blue;

        _unitStyles.Add(unit, styleAndName = (style, unit.GetDescriptions()));
      }

      return styleAndName;
    }

    static GUIStyle _inverseLabel;

    static GUIStyle InverseLabel {
      get => _inverseLabel != null ? _inverseLabel : _inverseLabel = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic };
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var attr = attribute as UnitAttribute;
      if (attr.UseInverse) {
        return base.GetPropertyHeight(property, label) * 2 + 6;
        //return EditorGUI.GetPropertyHeight(property) * 2 + 6;
      }
      return base.GetPropertyHeight(property, label);
      //return EditorGUI.GetPropertyHeight(property);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      DrawUnitsProperty(position, property, label, attribute as UnitAttribute);
    }

    public static void DrawUnitsProperty(Rect position, SerializedProperty property, GUIContent label, UnitAttribute attr) {

      // Strange gui bug
      if (position.width == 1)
        return;

      EditorGUI.BeginProperty(position, label, property);
      var holdIndent = EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;

      var useInverse = attr.UseInverse;

      // Draw testing background for inverse
      //if (useInverse) {
      //  EditorGUI.DrawRect(new Rect(position) { xMin = position.xMin - 2 }, new Color(0, 0, 0, .2f));
      //}

      Rect secondRow;
      if (useInverse) {
        position.height = 18;
        secondRow = new Rect(position) { y = position.y + 20 };
      } else {
        position.height = 18;
        secondRow = default;
      }

      var proptype = property.type;

      double min = attr.Min;
      double max = attr.Max;

      double realmin = min < max ? min : max;
      double realmax = min < max ? max : min;

      Rect rightSide1 = new Rect(position) { xMin = position.xMin + EditorGUIUtility.labelWidth + 2 };
      Rect rightSide2 = (useInverse) ? new Rect(secondRow) { xMin = secondRow.xMin + EditorGUIUtility.labelWidth + 2 } : default;
      Rect sliderRect1 = new Rect(rightSide1) { xMax = rightSide1.xMax - FIELD_WIDTH - 4 };
      Rect sliderRect2 = new Rect(rightSide2) { xMax = rightSide2.xMax - FIELD_WIDTH - 4 };
      bool useSlider = attr.UseSlider &&/* (min != 0 || max != 0) &&*/ sliderRect1.width > 20;


      Rect valRect1 = new Rect(rightSide1);
      Rect valRect2 = new Rect(rightSide2);

      if (useSlider) {
        var valwidth = rightSide1.xMax - FIELD_WIDTH;
        valRect1.xMin = valwidth;
        valRect2.xMin = valwidth;
      }

      if (property.propertyType == SerializedPropertyType.Float) {

        // Slider is always float based, even for doubles
        if (useSlider) {
          bool isDouble = proptype == "double";

          // Slider is the same for double and float, it just casts differently at the end.
          using (new EditorGUI.IndentLevelScope(holdIndent)) {
            EditorGUI.LabelField(position, label, GUIContent.none);
          }
          EditorGUI.BeginChangeCheck();
          //EditorGUI.indentLevel = 0;
          float sliderval = GUI.HorizontalSlider(sliderRect1, property.floatValue, (float)min, (float)max);
          if (EditorGUI.EndChangeCheck()) {
            double rounded = Math.Round(sliderval, attr.DecimalPlaces);
            double clamped = Clamp(rounded, realmin, realmax, attr);
            property.doubleValue = clamped;
            property.serializedObject.ApplyModifiedProperties();
          }
          if (useInverse) {
            using (new EditorGUI.IndentLevelScope(holdIndent)) {
              EditorGUI.LabelField(secondRow, attr.InverseName, InverseLabel);
            }
            EditorGUI.BeginChangeCheck();
            //EditorGUI.indentLevel = 0;
            float sliderinv = 1f / GUI.HorizontalSlider(sliderRect2, property.floatValue, (float)max, (float)min);


            if (EditorGUI.EndChangeCheck()) {
              double rounded = Math.Round(1d / sliderinv, attr.InverseDecimalPlaces);
              double clamped = Clamp(rounded, realmin, realmax, attr);
              if (isDouble) {
                property.doubleValue = clamped;
              } else {
                property.floatValue = (float)clamped;
              }
              property.serializedObject.ApplyModifiedProperties();
            }
          }
            
          // Double editable fields
          if (isDouble) {
            EditorGUI.BeginChangeCheck();
            double newval = EditorGUI.DelayedDoubleField(valRect1, GUIContent.none, Math.Round(property.doubleValue, MAX_PLACES));
            if (EditorGUI.EndChangeCheck()) {
              //double rounded = Math.Round(d, places);
              double clamped = Clamp(newval, realmin, realmax, attr);
              property.doubleValue = clamped;
              property.serializedObject.ApplyModifiedProperties();
            }

            if (useInverse) {
              EditorGUI.BeginChangeCheck();
              // Cast to float going into the field rendering, to limit the number of shown characters, but doesn't actually affect the accuracy of the value.
              double newinv = 1d / EditorGUI.DelayedDoubleField(valRect2, Math.Round(1d / property.doubleValue, MAX_PLACES));
              if (EditorGUI.EndChangeCheck()) {
                //double rounded = Math.Round(newinv, attr.InverseDecimalPlaces);
                double clamped = Clamp(newinv, realmin, realmax, attr);
                property.doubleValue = clamped;
                property.serializedObject.ApplyModifiedProperties();
              }
            }
          }
          // Float editable fields
          else {
            EditorGUI.BeginChangeCheck();
            float newval = EditorGUI.DelayedFloatField(valRect1, property.floatValue);
            if (EditorGUI.EndChangeCheck()) {
              //double rounded = Math.Round(newval, places);
              double clamped = Clamp(newval, realmin, realmax, attr);
              property.doubleValue = clamped;
              property.serializedObject.ApplyModifiedProperties();
            }

            if (useInverse) {
              EditorGUI.BeginChangeCheck();
              float newinv = 1f / EditorGUI.DelayedFloatField(valRect2, 1f / property.floatValue);
              if (EditorGUI.EndChangeCheck()) {
                //double rounded = Math.Round(newinv, places);
                float clamped = (float)Clamp(newinv, realmin, realmax, attr);
                property.floatValue = clamped;
                property.serializedObject.ApplyModifiedProperties();
              }
            }
          } 

          // No slider handling. Just using a regular property so that dragging over the label works.
        } else {

          EditorGUI.BeginChangeCheck();

          using (new EditorGUI.IndentLevelScope(holdIndent)) {
            EditorGUI.PropertyField(position, property, label);
          }

          if (EditorGUI.EndChangeCheck()) {
            double newval = property.doubleValue;
            if (realmin != 0 || realmax != 0) {
              double clamped = Clamp(newval, realmin, realmax, attr);
              property.doubleValue = clamped;
              property.serializedObject.ApplyModifiedProperties();
            }
          }
          if (useInverse) {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.IndentLevelScope(holdIndent)) {
              EditorGUI.LabelField(secondRow, attr.InverseName, InverseLabel);
            }
            double newval = 1d / EditorGUI.DelayedFloatField(secondRow, " "/*attr.InverseName*/, (float)Math.Round(1d / property.doubleValue, MAX_PLACES));
            if (EditorGUI.EndChangeCheck()) {
              if (realmin != 0 || realmax != 0) {
                double clamped = Clamp(newval, realmin, realmax, attr);
                property.doubleValue = clamped;
              } else {
                property.doubleValue = newval;
              }
              property.serializedObject.ApplyModifiedProperties();
            }
          }
        }

      } else if (property.propertyType == SerializedPropertyType.Integer) {
        // Slider
        if (useSlider) {
          using (new EditorGUI.IndentLevelScope(holdIndent)) {
            EditorGUI.LabelField(position, label, GUIContent.none);
          }

          //float drag = DrawLabelDrag(position);
          //if (drag != 0) {
          //  int draggedval = property.intValue + (int)drag;
          //  int clamped = attr.Clamp ? (int)(draggedval < realmin ? realmin : (draggedval > realmax ? realmax : draggedval)) : draggedval;
          //  property.intValue = clamped;
          //  property.serializedObject.ApplyModifiedProperties();
          //}

          // Int slider
          EditorGUI.BeginChangeCheck();
          //EditorGUI.indentLevel = 0;
          int sliderval = (int)GUI.HorizontalSlider(sliderRect1, property.intValue, (float)min, (float)max);
          if (EditorGUI.EndChangeCheck()) {
            property.intValue = sliderval;
            property.serializedObject.ApplyModifiedProperties();
          }

          // Int input Field
          EditorGUI.BeginChangeCheck();
          int i = EditorGUI.DelayedIntField(valRect1, property.intValue);
          if (EditorGUI.EndChangeCheck()) {
            property.intValue = (int)Clamp(i, realmin, realmax, attr);
            property.serializedObject.ApplyModifiedProperties();
          }

          if (useInverse) {
            using (new EditorGUI.IndentLevelScope(holdIndent)) {
              EditorGUI.LabelField(secondRow, attr.InverseName, InverseLabel);
            }

            // Inverse slider for Ints
            EditorGUI.BeginChangeCheck();
            //EditorGUI.indentLevel = 0;
            float sliderinv = 1f / GUI.HorizontalSlider(sliderRect2, property.intValue, (float)max, (float)min);
            if (EditorGUI.EndChangeCheck()) {
              property.intValue = (int)Math.Round(1f / sliderinv);
              property.serializedObject.ApplyModifiedProperties();
            }

            // inverse Int field when slider exists
            EditorGUI.BeginChangeCheck();
            float newinv = 1f / EditorGUI.DelayedFloatField(valRect2, (float)Math.Round(1d / property.intValue, MAX_PLACES));
            if (EditorGUI.EndChangeCheck()) {
              //int val = (int)(1d / newinv);
              int clamped = (int)Clamp(newinv, realmin, realmax, attr);
              property.intValue = clamped;
              property.serializedObject.ApplyModifiedProperties();
            }
          }


          // No slider handling. Just using a regular property so that dragging over the label works.
        } else {
          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(holdIndent)) {
            EditorGUI.PropertyField(position, property, label);
          }
          if (EditorGUI.EndChangeCheck() && (realmin != 0 || realmax != 0)) {
            int intval = property.intValue;
            int clamped = (int)Clamp(intval, realmin, realmax, attr);
            property.intValue = clamped;
            property.serializedObject.ApplyModifiedProperties();
          }

          if (useInverse) {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.IndentLevelScope(holdIndent)) {
              EditorGUI.LabelField(secondRow, attr.InverseName, InverseLabel);
            }
            double newval = EditorGUI.DelayedFloatField(secondRow, " "/*attr.InverseName*/, (float)Math.Round(1d / property.intValue, MAX_PLACES));

            if (EditorGUI.EndChangeCheck()) {
              double rounded = 1d / newval;
              if (realmin != 0 || realmax != 0) {
                double clamped = Clamp(rounded, realmin, realmax, attr);
                property.doubleValue = clamped;
              } else
                property.doubleValue = rounded;
            }
          }
        }

        // Fallback for unsupported field types
      } else {
        Debug.LogWarning(nameof(UnitAttribute) + " only is applicable to double, float and integer field types.");
        using (new EditorGUI.IndentLevelScope(holdIndent)) {
          EditorGUI.PropertyField(position, property, label, true);
        }
      }

      if (attr.Unit != Units.None) {
        var (style, name) = GetOverlayStyle(attr.Unit);
        float fieldwidth = position.width - EditorGUIUtility.labelWidth;
        GUI.Label(position, fieldwidth > 200 && !useSlider ? name.l : fieldwidth >100 ? name.s : "", style);
      }
      if (useInverse && attr.InverseUnit != Units.Units) {
        var (style, name) = GetOverlayStyle(attr.InverseUnit);
        float fieldwidth = secondRow.width - EditorGUIUtility.fieldWidth;
        GUI.Label(secondRow, fieldwidth > 200 && !useSlider ? name.l : fieldwidth > 100 ? name.s : "", style);
      }

      EditorGUI.indentLevel = holdIndent;

      EditorGUI.EndProperty();
    }

    static double Clamp(double val, double min, double max, UnitAttribute attr) {
      return 
        (val < min) ? attr.ClampMin ? min : val :
        (val > max) ? attr.ClampMax ? max : val :
        val;
    }

    // Makes the label field draggable - TODO: doesn't handle dragging out of window
    private static float DrawLabelDrag(Rect rect) {

      rect.width = EditorGUIUtility.labelWidth;

      EditorGUIUtility.AddCursorRect(rect, MouseCursor.SlideArrow);
      var e = Event.current;

      if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition)) {
        return e.delta.x;
      }

      return 0;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/UnityAssetGuidAttributeDrawer.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnityAssetGuidAttribute))]
  public class UnityAssetGuidAttributeDrawer : PropertyDrawerWithErrorHandling {

    private NetworkObjectGuidDrawer _guidDrawer = null;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      string guid;
      position.width -= 40;

      if (GetUnityLeafType(fieldInfo.FieldType) == typeof(NetworkObjectGuid)) {
        if (_guidDrawer == null) {
          _guidDrawer = new NetworkObjectGuidDrawer();
        }
        _guidDrawer.OnGUI(position, property, label);
        guid = NetworkObjectGuidDrawer.GetValue(property).ToString("N");
      } else {
        using (new FusionEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
          EditorGUI.PropertyField(position, property, GUIContent.none, false);
          guid = property.stringValue;
        }
      }

      string assetPath = string.Empty;

      if (!string.IsNullOrEmpty(guid)) {
        assetPath = AssetDatabase.GUIDToAssetPath(guid);
      }

      using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(assetPath))) {
        position.x += position.width;
        position.width = 40;
        bool buttonPressed;

        bool wasEnabled = GUI.enabled;
        GUI.enabled = true;
        buttonPressed = GUI.Button(position, "Ping");
        GUI.enabled = wasEnabled;

        if (buttonPressed) {
          // ping the main assets
          EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
        }
      }

      if (!string.IsNullOrEmpty(assetPath)) {
        var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset == null) {
          SetError($"Asset with this guid does not exist. Last path:\n{assetPath}");
        } else {
          SetInfo($"Asset path:\n{assetPath}");
        }
      }
    }

    private static Type GetUnityLeafType(Type type) {
      if (type.HasElementType) {
        type = type.GetElementType();
      } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
        type = type.GetGenericArguments()[0];
      }
      return type;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/VersaMaskAttributeDrawer.cs

namespace Fusion.Editor {

  using UnityEditor;
  using UnityEngine;
  using System.Collections.Generic;
  using System;
  using System.Text;
  
  [CustomPropertyDrawer(typeof(VersaMaskAttribute))]
  public class VersaMaskAttributeDrawer : PropertyDrawer {

    // persistent cache of XML summaries and values
    private static Dictionary<Type, (string[] names, string[] summaries, int[] values)> _typeInfo;
    private static StringBuilder _sb;
    
    private const float PAD           = 4;
    private const float LINE_SPACING  = 18;
    private const float BOX_INDENT    = 0; //16 - PAD;
    private const float FOLDOUT_WIDTH = 16;
    
    protected virtual (string[] names, string[] summaries, int[] values) GetStringNames() {

      var maskAttribute = attribute as VersaMaskAttribute;

      var fieldType = (maskAttribute.CastTo == null) ? fieldInfo.FieldType : maskAttribute.CastTo;
      
      if (_typeInfo == null) {
        _typeInfo = new Dictionary<Type, (string[], string[], int[])>();
      }
      if (_typeInfo.TryGetValue(fieldType, out var entry)) {
        return entry;
      }

      int[] enumValues = (int[])fieldType.GetEnumValues();
      string[] names = new string[enumValues.Length];
      
      for (int i = 0; i < enumValues.Length; ++i) {
         names[i] = Enum.GetName(fieldType, enumValues[i]);
      }
      
      // Remove Zero value from the array if need be.
      int len = enumValues.Length;
      entry = (new string[len], new string[len], new int[len]);

      for (int i = 0; i < len; i++) {
        var name  = names[i];
        entry.names[i]  = ObjectNames.NicifyVariableName(name);
        entry.values[i] = enumValues[i];
        var enumField   = fieldType.GetField(name);
        entry.summaries[i] = XmlDocumentation.GetXmlDocSummary(enumField, true);
      }
      _typeInfo.Add(fieldType, entry);
      return entry;
    }


    
    public override void OnGUI(Rect r, SerializedProperty property, GUIContent label) {

      var attr = attribute as VersaMaskAttribute;

      bool useFoldout = !attr.AlwaysExpanded && !string.IsNullOrEmpty(label.text);

      if (useFoldout) {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = true;
        property.isExpanded = EditorGUI.Toggle(new Rect(r) { xMin = r.xMin + EditorGUIUtility.labelWidth, height = LINE_SPACING, width = FOLDOUT_WIDTH }, property.isExpanded, (GUIStyle)"Foldout");
        GUI.enabled = wasEnabled;
      }

      label = EditorGUI.BeginProperty(r, label, property);

      int scratchMask;
      
      Rect br = new Rect(r) { xMin = r.xMin + BOX_INDENT };
      Rect ir = new Rect(br) { height = LINE_SPACING };

      Rect labelRect = new Rect(r) { height = LINE_SPACING };

      var entries = GetStringNames();

      if (attr.AlwaysExpanded || (useFoldout && property.isExpanded)) {
        scratchMask = property.intValue;

        EditorGUI.LabelField(new Rect(br) { yMin = br.yMin + LINE_SPACING }, "", EditorStyles.helpBox);
        ir.xMin += PAD * 2;
        ir.y += PAD;

        var ShowMaskBits = attr.ShowBitmask;

        var names = entries.names;
        int len   = names.Length;
        for (int i = 0; i < len; ++i) {
          ir.y += LINE_SPACING;

          int offsetBit = entries.values[i];

          // Invisible button. Toggle is cosmetic, the button does the actual toggling.
          if (GUI.Button(ir, "", GUIStyle.none)) {
            if (offsetBit == 0) {
              if (scratchMask == 0) {
                scratchMask = -1;
              } else {
                scratchMask = 0;
              }
            } else {
              bool isSelected = (scratchMask & offsetBit) == offsetBit;
              if (isSelected) {
                scratchMask &= ~offsetBit;
              } else {
                scratchMask |= offsetBit;
              }              
            }
          }

          // Cosmetic Toggle
          if (offsetBit != 0) {
            EditorGUI.ToggleLeft(ir, "", (scratchMask & offsetBit) == offsetBit);
          } else {
            EditorGUI.ToggleLeft(ir, "", scratchMask == 0);
          }

          using (new EditorGUI.DisabledScope((scratchMask & offsetBit) != offsetBit || (offsetBit == 0 && scratchMask != 0))) {
            EditorGUI.LabelField(new Rect(ir) { xMin = ir.xMin + 24 }, new GUIContent(names[i], entries.summaries[i]));
          }
        }

        // Draw Label
        EditorGUI.LabelField(labelRect, label, GUIContent.none);

        // Draw Bitmask (if enabled for this attribute)
        if (ShowMaskBits) {

          // Determine how many bits of bitmask to show
          int bitCount = 8; // Show at least one byte
          var lastValue = entries.values[entries.values.Length - 1];
          for (; bitCount < 32; ++bitCount) {
            if (lastValue >> (bitCount + 1) == 0) {
              break;
            }
          }
          
          // Build the bitmask label
          if (_sb == null) {
            _sb = new StringBuilder(32);
          } else {
            _sb.Clear();
          }
          for (int b = bitCount - 1; b >= 0; --b) {
            _sb.Append((scratchMask & (1 << b)) == 0 ? "-" : "1");
            // Space every 4 bits
            if (b != 0 && b % 4 == 0) {
              _sb.Append(" ");
            }
          }
          EditorGUI.LabelField(new Rect(labelRect) { xMin = labelRect.xMin + +EditorGUIUtility.labelWidth + FOLDOUT_WIDTH }, $"[{_sb}]");
          
        } else {
          // Draw All/None buttons when foldout is expanded (and bitmask is not shown)
          var valueAreaRect = new Rect(labelRect) { xMin = labelRect.xMin + EditorGUIUtility.labelWidth + FOLDOUT_WIDTH };
          if (GUI.Button(new Rect(valueAreaRect) { width = valueAreaRect.width * .5f }, "All")) {
            scratchMask = (int)(~0);
          }
          if (GUI.Button(new Rect(valueAreaRect) { xMin = valueAreaRect.xMin + valueAreaRect.width * .5f }, "None")) {
            scratchMask = 0;
          }
        }
      } else {
        EditorGUI.LabelField(r, label, GUIContent.none, EditorStyles.label);
        scratchMask = EditorGUI.MaskField(new Rect(r) { xMin = r.xMin + EditorGUIUtility.labelWidth + FOLDOUT_WIDTH}, GUIContent.none, property.intValue, entries.names);
      }

      // Apply any changes which have been made to the mask
      if (scratchMask != property.intValue) {
        Undo.RecordObject(property.serializedObject.targetObject, "Change Mask Selection");
        property.intValue = scratchMask;
        property.serializedObject.ApplyModifiedProperties();
      }

      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {

      var attr = attribute as VersaMaskAttribute;
      bool expanded = (attr.AlwaysExpanded || (property.isExpanded && !string.IsNullOrEmpty(label.text)));

      if (expanded) {
        var entry = GetStringNames();
        return LINE_SPACING * (entry.names.Length + 1) + PAD * 2;
      } else
        return base.GetPropertyHeight(property, label);
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/VersaMaskDrawer.cs

// Deleted Sept 3, 2022

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/CustomTypes/WarnIfAttributeDrawer.cs

namespace Fusion.Editor {

  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(WarnIfAttribute))]
  public class WarnIfAttributeDrawer : DoIfAttributeDrawer {



    public WarnIfAttribute Attribute => (WarnIfAttribute)attribute;

    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {

      float height = base.GetPropertyHeightInternal(property, label);
      if (height <= 0) {
        return height;
      }

      // Not inserting message box inline. No height changes will occur.
      if (Attribute.UseMsgIconOnly) {
        return height;
      }

      bool showWarningBox;

      if (Attribute.ConditionMember != null) {
        double condValue = GetCompareValue(property, Attribute.ConditionMember, fieldInfo);
        showWarningBox = CheckDraw(Attribute, condValue);
      } else {

        if (realTargetObject == null) {
          realTargetObject = property.GetParent();
        }

        int msgType = DetermineMsgType();
        showWarningBox = msgType > 0;
      }

      if (showWarningBox) {
        string msg = DetermineMessage();
        float extra = FusionEditorGUI.GetMessageBoxRect(new GUIContent(msg, FusionGUIStyles.WarnIcon), MessageType.Warning).height;
        return height + extra;
      } else {
        return height;
      }

    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      bool showWarning;
      int msgType;

      // Determine if we are showing a warning at all. ConditionMember is the first priority. If that is null, the MsgTypeMember is checked.
      if (Attribute.ConditionMember != null) {
        double condValue = GetCompareValue(property, Attribute.ConditionMember, fieldInfo);
        showWarning = CheckDraw(Attribute, condValue);
        msgType = Attribute.MsgType;
      } else {

        if (realTargetObject == null) {
          realTargetObject = property.GetParent();
        }

        msgType = DetermineMsgType();
        showWarning = msgType > 0;
      }

      // No warning - we are done here.
      if (showWarning == false) {
        base.OnGUIInternal(position, property, label);
        return;
      }

      string message = DetermineMessage();

      var icon =
        msgType == 1 ? FusionGUIStyles.InfoIcon :
        msgType == 2 ? FusionGUIStyles.WarnIcon :
        msgType == 3 ? FusionGUIStyles.ErrorIcon :
        null;


      if (Attribute.UseMsgIconOnly) {
        base.OnGUIInternal(position, property, new GUIContent(label) { tooltip = message });
      } else {
        var boxContent = new GUIContent(message, icon);
        var boxRect = FusionEditorGUI.GetMessageBoxRect(boxContent, MessageType.Warning);

        base.OnGUIInternal(new Rect(position) { height = position.height - boxRect.height },
          property, new GUIContent(label) { tooltip = message });

        FusionEditorGUI.DrawMesssageBox(boxContent, (MessageType)msgType, position, boxRect);
      }

      // Draw MsgIcon? Shows if explicitly using only the icon.. or if we have an action (which needs a button)
      if (Attribute.UseMsgIconOnly || Attribute.ActionMethod != null) {
        var toppos = position;
        toppos.width = toppos.height > 18 ? 18 : toppos.height;
        toppos.height = toppos.width;

        // position just to the left of the typical field area (this attribute obviously will not work with unusual drawer layouts)
        toppos.x = toppos.xMin + EditorGUIUtility.labelWidth - toppos.width;

        var buttonWidth = new Rect(toppos) { xMin = toppos.xMin - 2, xMax = toppos.xMax + 2 };

        if (GUI.Button(buttonWidth, new GUIContent(icon, message))) {
          ExecuteAction(Attribute.ActionMethod);
        }
      }
    }

    void ExecuteAction(string actionMethodName) {

      if (Attribute.ActionMethod != null) {
        var a = fieldInfo.DeclaringType.GetMethod(actionMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (a != null) {
          a.Invoke(realTargetObject, null);
        }
      }
    }

    int DetermineMsgType() {

      var msgTypeProvider = Attribute.MsgTypeProvider;

      if (msgTypeProvider != null) {
        var a = fieldInfo.DeclaringType.GetMember(msgTypeProvider, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (a != null && a.Length > 0) {
          if (a[0] is MethodInfo) {
            return (int)(a[0] as MethodInfo).Invoke(realTargetObject, null);
          }
          if (a[0] is PropertyInfo) {
            return (int)(a[0] as PropertyInfo).GetValue(realTargetObject);
          }
          if (a[0] is FieldInfo) {
            return (int)(a[0] as FieldInfo).GetValue(realTargetObject);
          }
        }
      }
      return Attribute.MsgType;
    }

    string DetermineMessage() {

      var msgProvider = Attribute.MsgProvider;

      if (msgProvider == null) {
        return Attribute.Message;
      }

      var a = fieldInfo.DeclaringType.GetMember(msgProvider, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
      if (a != null && a.Length > 0) {
        if (a[0] is MethodInfo) {
          return (string)(a[0] as MethodInfo).Invoke(realTargetObject, null);
        }
        if (a[0] is PropertyInfo) {
          return (string)(a[0] as PropertyInfo).GetValue(realTargetObject);
        }
        if (a[0] is FieldInfo) {
          return (string)(a[0] as FieldInfo).GetValue(realTargetObject);
        }
      }

      // if the member isn't found, fallback to the message supplied to the attribute.
      return Attribute.Message;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/DebugDllToggle.cs

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

    [MenuItem("Fusion/Toggle Debug Dlls")]
    public static void Toggle() {

      // find the root
      string dir;
      {
        var fusionRuntimeDllPath = AssetDatabase.GUIDToAssetPath(FusionRuntimeDllGuid);
        if (string.IsNullOrEmpty(fusionRuntimeDllPath)) {
          Debug.LogError($"Cannot locate assemblies directory");
          return;
        } else {
          dir = PathUtils.MakeSane(Path.GetDirectoryName(fusionRuntimeDllPath));
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


#region Assets/Photon/Fusion/Scripts/Editor/EditorRecompileHook.cs

namespace Fusion.Editor {
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEditor.Compilation;
  
  [InitializeOnLoad]
  public static class EditorRecompileHook {
    static EditorRecompileHook() {
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

      var configPath = NetworkProjectConfigUtilities.GetGlobalConfigPath(false);
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


#region Assets/Photon/Fusion/Scripts/Editor/FusionBuildTriggers.cs

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


#region Assets/Photon/Fusion/Scripts/Editor/FusionGUIStyles.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  public static class FusionGUIStyles {

    const string GRAPHICS_FOLDER = "EditorGraphics/";
    //const string HEADER_BACKS_PATH = "ComponentHeaderGraphics/Backs/";
    //const string GROUPBOX_FOLDER = GRAPHICS_FOLDER + "GroupBox/";
    const string FONT_PATH = "ComponentHeaderGraphics/Fonts/Oswald-Header";
    const string ICON_PATH = "ComponentHeaderGraphics/Icons/";

    //// Extra safety code to deal with these styles being used in the inspector for a currently selected object.
    //// The styles array never gets nulled if they are being used for some reason, but the textures do get nulled.
    //// This OnPlayModeState change to exit detects this and nulls the styles manually.
    //static FusionGUIStyles() {
    //  EditorApplication.playModeStateChanged += OnPlayModeState;
    //}
    //private static void OnPlayModeState(PlayModeStateChange state) {
    //  if (state == PlayModeStateChange.EnteredEditMode) {
    //    _groupBoxStyles = null;
    //    _fusionHeaderStyles = null;
    //  }
    //}


    private static Color[] _headerColors = new Color[12] {
      // none
      new Color(),
      // gray
      new Color(0.35f, 0.35f, 0.35f),
      // blue
      new Color(0.15f, 0.25f, 0.6f),
      // red
      new Color(0.5f, 0.1f, 0.1f),
      // green
      new Color(0.1f, 0.4f, 0.1f),
      // orange
      new Color(0.6f, 0.35f, 0.1f),
      // black
      new Color(0.1f, 0.1f, 0.1f),
      // steel
      new Color(0.32f, 0.35f, 0.38f),
      // sand
      new Color(0.38f, 0.35f, 0.32f),
      // olive
      new Color(0.25f, 0.33f, 0.15f),
      // cyan
      new Color(0.25f, 0.5f, 0.5f),
      // violet
      new Color(0.35f, 0.2f, 0.4f),
    };

    public enum GroupBoxType {
      None,
      Info,
      Warn,
      Error,
      Help,
      Gray,
      Steel,
      Sand,
      InlineHelpOuter,
      InlineHelpInner,
      Outline,
    }

    private struct GroupBoxInfo {
      public string Name;
      public Color? BackColor;
      public Color? OutlineColor;
      public Color? FontColor;
      public int? Padding;
      public RectOffset Margin;
      public int? BorderWidth;
      public bool? RichTextBox;
      public GroupBoxInfo(string name, Color? backColor = null, Color? outlineColor = null, Color? fontColor = null, int? padding = null, int? borderWidth = null, bool? richTextBox = null, RectOffset margin = null) {
        Name         = name;
        BackColor    = backColor;
        OutlineColor = outlineColor;
        FontColor    = fontColor;
        Padding      = padding;
        Margin       = margin;
        BorderWidth  = borderWidth;
        RichTextBox  = richTextBox;
      }
    }

    static Color HelpOuterColor = EditorGUIUtility.isProSkin ? new Color(0.431f, 0.490f, 0.529f, 1.000f) : new Color(0.478f, 0.639f, 0.800f); //new Color(0.451f, 0.675f, 0.898f);
    static Color HelpInnerColor = EditorGUIUtility.isProSkin ? new Color(0.317f, 0.337f, 0.352f, 1.000f) : new Color(0.686f, 0.776f, 0.859f);

    private static GroupBoxInfo[] _groupBoxInfo = new GroupBoxInfo[] {
      // None
      new GroupBoxInfo(),
      // Info
      new GroupBoxInfo() { 
        BackColor = EditorGUIUtility.isProSkin ?
        new Color(0.30f, 0.30f, 0.30f, 1.00f) :
        new Color(0.90f, 0.90f, 0.90f, 0.50f),
        OutlineColor = Color.black},
      // Warn
      new GroupBoxInfo() {
        BackColor = EditorGUIUtility.isProSkin ? 
        new Color(0.36f, 0.33f, 0.22f, 1.00f) : 
        new Color(0.98f, 0.94f, 0.80f, 0.90f),
        OutlineColor = Color.black},

      // Error
      new GroupBoxInfo() { 
        BackColor = EditorGUIUtility.isProSkin ? 
        new Color(0.40f, 0.15f, 0.10f, 1.00f) : 
        new Color(0.9f, 0.70f, 0.70f, 1.00f),   
        OutlineColor = EditorGUIUtility.isProSkin ? Color.black: Color.red},
      
      // Help
      new GroupBoxInfo() { BackColor = new Color(0.50f, 0.50f, 0.50f, 0.20f),   OutlineColor = Color.black},
      // Gray
      new GroupBoxInfo() { BackColor = new Color(0.40f, 0.40f, 0.40f, 0.20f),   OutlineColor = Color.black},
      // Steel
      new GroupBoxInfo() { BackColor = new Color(0.45f, 0.50f, 0.55f, 0.20f),   OutlineColor = Color.black},
      // Sand
      new GroupBoxInfo() { BackColor = new Color(0.55f, 0.50f, 0.45f, 0.20f),   OutlineColor = Color.black},
      
      // Outer Help
      new GroupBoxInfo() {
        BackColor = HelpOuterColor,
        OutlineColor = HelpOuterColor,
      },
      // Inner Help
      new GroupBoxInfo() {
        BackColor =   HelpInnerColor,
        OutlineColor = HelpOuterColor,
        BorderWidth = 4,
        Padding = 12,
        RichTextBox = true
      },
      // Outline
      new GroupBoxInfo() { 
        BackColor = new Color(0.0f, 0.0f, 0.0f, 0f),   
        OutlineColor = EditorGUIUtility.isProSkin ? new Color(0,0,0, 0.5f) : new Color(0,0,0, 0.5f) },
    };


    public const int HEADER_CORNER_RADIUS = 4;
    const float SELECTED_ICON_ALPHA = 1.00f;
    const float SELECTED_QMRK_ALPHA = 0.90f;
    const float UNSELCTD_ICON_ALPHA = 0.33f;

    // Creates the "?" inline help icon with pre-rendered alpha channel, to avoid resorting to an editor resource file.
    static Texture2D _questionMarkTexture;
    public static Texture2D QuestionMarkTexture {

      get {
        if (_questionMarkTexture == null) {
          var alphas
       = new float[,]{
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.05f, 0.04f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.12f, 0.74f, 0.88f, 0.88f, 0.65f, 0.18f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.09f, 0.14f, 0.14f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.86f, 1.00f, 1.00f, 1.00f, 0.88f, 0.11f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.62f, 1.00f, 1.00f, 0.57f, 0.14f, 0.86f, 0.86f, 0.86f, 0.14f, 0.00f, 0.00f, 0.00f, 0.61f, 1.00f, 1.00f, 1.00f, 1.00f, 0.57f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.62f, 1.00f, 1.00f, 0.57f, 0.14f, 1.00f, 1.00f, 1.00f, 0.86f, 0.25f, 0.00f, 0.00f, 0.25f, 0.18f, 0.42f, 1.00f, 1.00f, 0.80f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.62f, 1.00f, 1.00f, 0.57f, 0.14f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0.43f, 0.00f, 0.00f, 0.00f, 0.14f, 1.00f, 1.00f, 0.84f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.18f, 0.29f, 0.29f, 0.14f, 0.00f, 0.14f, 0.14f, 0.71f, 1.00f, 1.00f, 1.00f, 0.86f, 0.71f, 0.71f, 0.86f, 1.00f, 1.00f, 0.72f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.71f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0.48f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.43f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0.71f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.09f, 0.45f, 0.63f, 0.69f, 0.59f, 0.40f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      {0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      };

      // Previous thinner question mark
      //  = new float[,]{
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.14f, 0.57f, 0.57f, 0.43f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.43f, 1.00f, 1.00f, 1.00f, 0.86f, 0.14f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.71f, 0.71f, 0.43f, 0.00f, 0.29f, 0.57f, 0.57f, 0.14f, 0.00f, 0.00f, 0.00f, 0.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0.71f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 1.00f, 1.00f, 0.43f, 0.00f, 0.43f, 1.00f, 1.00f, 0.86f, 0.14f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.57f, 1.00f, 1.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 1.00f, 1.00f, 0.43f, 0.00f, 0.43f, 0.86f, 1.00f, 1.00f, 0.86f, 0.29f, 0.00f, 0.00f, 0.00f, 0.00f, 0.43f, 1.00f, 1.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.14f, 1.00f, 1.00f, 1.00f, 0.71f, 0.29f, 0.29f, 0.29f, 0.86f, 1.00f, 1.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.14f, 0.86f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0.43f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.57f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0.71f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.07f, 0.33f, 0.47f, 0.40f, 0.23f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, },
      //};

          var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
          for (int x = 0; x < 32; ++x) {
            for (int y = 0; y < 32; ++y) {
              tex.SetPixel(x, y, EditorGUIUtility.isProSkin ? new Color(1, 1, 1, alphas[x, y]) : new Color(0, 0, 0, alphas[x, y]));
            }
          }
          tex.Apply();
          _questionMarkTexture = tex;
        }
        return _questionMarkTexture;
      }
    }

    static Color HelpIconColorSelected   = EditorGUIUtility.isProSkin ? new Color(0.000f, 0.500f, 1.000f, SELECTED_ICON_ALPHA)  : new Color(0.502f, 0.749f, 1.000f, SELECTED_ICON_ALPHA);
    static Color HelpIconColorUnselected = EditorGUIUtility.isProSkin ? new Color(1.000f, 1.000f, 1.000f, UNSELCTD_ICON_ALPHA)  : new Color(0.000f, 0.000f, 0.000f, UNSELCTD_ICON_ALPHA);

    static Texture2D _helpIconSelected;
    public static Texture2D HelpIconSelected {
      get {
        if (_helpIconSelected == null) {
          var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
          tex.ClearTexture(new Color(0.06f, 0.5f, 0.75f, 0));
          tex.RenderCircle(13, HelpIconColorSelected, Blend.Overwrite);
          tex.OverlayTexture(QuestionMarkTexture, SELECTED_QMRK_ALPHA);
          tex.alphaIsTransparency = true;
          tex.Apply();
          _helpIconSelected = tex;
        }
        return _helpIconSelected;
      }
    }


    static Texture2D _helpIconUnselected;
    public static Texture2D HelpIconUnselected {
      get {
        if (_helpIconUnselected == null) {
          var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
          tex.ClearTexture(HelpIconColorUnselected * new Color(1, 1, 1, 0));
          tex.RenderCircle(13, HelpIconColorUnselected, Blend.Overwrite);
          tex.ClearCircle(EditorGUIUtility.pixelsPerPoint > 1 ? 10 : 11, true);
          tex.OverlayTexture(QuestionMarkTexture, UNSELCTD_ICON_ALPHA);
          tex.alphaIsTransparency = true;
          tex.filterMode = FilterMode.Trilinear;
          tex.Apply();
          _helpIconUnselected = tex;
        }
        return _helpIconUnselected;
      }
    }

    static Texture2D[] _headerTextures;
    public static Texture2D[] HeaderTextures {
      get {
        if (_headerTextures == null || _headerTextures[0] == null) {
          _headerTextures = new Texture2D[_headerColors.Length];
          for(int i = 0; i < _headerColors.Length; ++i) {
            _headerTextures[i] = GenerateRoundedBoxTexture(HEADER_CORNER_RADIUS, new GroupBoxInfo($"Header{i}", _headerColors[i], Color.black), false);
          }
        }
        return _headerTextures;
      }
    }
    static Texture2D[] _headerTexturesHiRez;
    public static Texture2D[] HeaderTexturesHiRez {
      get {
        if (_headerTexturesHiRez == null || _headerTexturesHiRez[0] == null) {
          _headerTexturesHiRez = new Texture2D[_headerColors.Length];
          for (int i = 0; i < _headerColors.Length; ++i) {
            _headerTexturesHiRez[i] = GenerateRoundedBoxTexture(HEADER_CORNER_RADIUS, new GroupBoxInfo($"Header{i}", _headerColors[i], Color.black), true);
          }
        }
        return _headerTexturesHiRez;
      }
    }

    static Texture2D[] _groupBoxTextures;
    public static Texture2D[] GroupBoxTextures {
      get {
        if (_groupBoxTextures == null || _groupBoxTextures[0] == null) {
          _groupBoxTextures = new Texture2D[_groupBoxInfo.Length];
          for (int i = 0; i < _groupBoxInfo.Length; ++i) {
            _groupBoxTextures[i] = GenerateRoundedBoxTexture(HEADER_CORNER_RADIUS/* + _groupBoxInfo[i].BorderWidth.GetValueOrDefault(1)*/, _groupBoxInfo[i], false);
          }
        }
        return _groupBoxTextures;
      }
    }

    static Texture2D[] _groupBoxTexturesHiRez;
    public static Texture2D[] GroupBoxTexturesHiRez {
      get {
        if (_groupBoxTexturesHiRez == null || _groupBoxTexturesHiRez[0] == null) {
          _groupBoxTexturesHiRez = new Texture2D[_groupBoxInfo.Length];
          for (int i = 0; i < _groupBoxInfo.Length; ++i) {
            _groupBoxTexturesHiRez[i] = GenerateRoundedBoxTexture(HEADER_CORNER_RADIUS/* + _groupBoxInfo[i].BorderWidth.GetValueOrDefault(1)*/, _groupBoxInfo[i], true);
          }
        }
        return _groupBoxTexturesHiRez;
      }
    }

    static void OverlayTexture(this Texture2D tex, Texture2D overlay, float opacity) {
      for (int x = 0; x < tex.width; ++x) {
        for (int y = 0; y < tex.width; ++y) {
          var b = tex.GetPixel(x, y);
          var o = overlay.GetPixel(x, y) * new Color(1, 1, 1, opacity);
          var c = Color.Lerp(b, o, o.a);
          tex.SetPixel(x, y, c);
        }
      }
    }

    static Texture2D GenerateRoundedBoxTexture(int radius, GroupBoxInfo info, bool hirez) {

      if (hirez) {
        radius *= 2;
      }
      var border = hirez ? info.BorderWidth.GetValueOrDefault(1) * 2 : info.BorderWidth.GetValueOrDefault(1);

      var tex = new Texture2D((radius + border) * 2, (radius + border) * 2, TextureFormat.RGBA32, false);
      bool hasOutline = info.OutlineColor != null;
      var color = info.BackColor.GetValueOrDefault();
      tex.ClearTexture(color * new Color(1, 1, 1, 0));
      bool outlineOnly = color.a == 0;

      if (hasOutline) {
        tex.RenderCircle(radius + border, info.OutlineColor.Value, Blend.Overwrite);
      }


      if (outlineOnly) {
        tex.RenderCircle(hasOutline ? radius /*- border */: radius, info.OutlineColor.GetValueOrDefault(), Blend.Subtract);
      } else {
        tex.RenderCircle(hasOutline ? radius /*- border*/ : radius, color/* new Color(color.r, color.g, color.b, 1f)*/, hasOutline ? Blend.Overlay : Blend.Overwrite);
        tex.ApplyAlpha(color.a);
      }

      tex.alphaIsTransparency = true;
      tex.filterMode = FilterMode.Point;
      tex.mipMapBias = 0;
      tex.Apply();
      return tex;
    }

    private static void ClearTexture(this Texture2D tex, Color color) {
      for(int x = 0; x < tex.width; ++x) {
        for(int y = 0; y < tex.width; ++y) {
          tex.SetPixel(x, y, color);
        }
      }
    }

    enum Blend {
      Overwrite,
      Overlay,
      Subtract
    }

    private static void RenderCircle(this Texture2D tex, int r, Color color, Blend blend, int? midX = null, int? midY = null) {
      if (midX == null) {
        midX = tex.width / 2;
      }
      
      if (midY == null) {
        midY = tex.height / 2;
      }

      var midx = midX.Value;
      var midy = midY.Value;

      for (int x = 0; x < r; ++x) {
        for (int y = 0; y < r; ++y) {
          double h = Math.Abs(Math.Sqrt(x * x + y * y));
          float a = Mathf.Clamp01((float)(r - h));

          var e = tex.GetPixel(midx + x, midy + y);
          var c = color * new Color(1f, 1f, 1f, a);
          if (blend == Blend.Overwrite) {
            c = color * new Color(1, 1, 1, a * color.a);
          }
          else if (blend == Blend.Overlay) {
            if (a > 0) {
              c = Color.Lerp(e, c, a);
              c.a = e.a;
            } else {
              c = e;
            }
          }
          else if (blend == Blend.Subtract) {
            c = new Color(e.r, e.g, e.b, a > 0 ? Math.Min(e.a, (1-a) * color.a) : e.a);
          }
          tex.SetPixel(midx + 0 + x, midy + 0 + y, c);
          tex.SetPixel(midx - 1 - x, midy + 0 + y, c);
          tex.SetPixel(midx + 0 + x, midy - 1 - y, c);
          tex.SetPixel(midx - 1 - x, midy - 1 - y, c);

        }
      }
    }

    private static void ClearCircle(this Texture2D tex, int r, bool blend, int? midX = null, int? midY = null) {
      if (midX == null) {
        midX = tex.width / 2;
      }

      if (midY == null) {
        midY = tex.height / 2;
      }

      var midx = midX.Value;
      var midy = midY.Value;

      for (int x = 0; x < tex.width; ++x) {
        for (int y = 0; y < tex.height; ++y) {
          double h = Math.Abs((Math.Sqrt(x * x + y * y)));
          float a = Mathf.Clamp01((float)(h - r));
          var e = tex.GetPixel(midx + x, midy + y);
          var c = e * new Color(1f, 1f, 1f, a);
          tex.SetPixel(midx + 0 + x, midy + 0 + y, c);
          tex.SetPixel(midx - 1 - x, midy + 0 + y, c);
          tex.SetPixel(midx + 0 + x, midy - 1 - y, c);
          tex.SetPixel(midx - 1 - x, midy - 1 - y, c);

        }
      }
    }

    private static void ApplyAlpha(this Texture2D tex, float alpha) {
      int midx = tex.width / 2;
      int midy = tex.height / 2;

      Color multiplier = new Color(1, 1, 1, alpha);

      for (int x = 0; x < midx; ++x) {
        for (int y = 0; y < midy; ++y) {
          var e = tex.GetPixel(midx + x, midy + y);
          var c = e * multiplier;
          tex.SetPixel(midx + 0 + x, midy + 0 + y, c);
          tex.SetPixel(midx - 1 - x, midy + 0 + y, c);
          tex.SetPixel(midx + 0 + x, midy - 1 - y, c);
          tex.SetPixel(midx - 1 - x, midy - 1 - y, c);

        }
      }
    }

    private static GUIStyle _warnLabelStyle;
    public static GUIStyle WarnLabelStyle {
      get {
        if (_warnLabelStyle == null) {
          _warnLabelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, richText = true, wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }
        return _warnLabelStyle;
      }
    }

    private static Texture2D _infoIcon;
    public static Texture2D InfoIcon {
      get {
        if (_infoIcon == null) {
          _infoIcon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_console.infoicon@2x" : "console.infoicon@2x");
        }
        return _infoIcon;
      }
    }

    private static Texture2D _warnIcon;
    public static Texture2D WarnIcon {
      get {
        if (_warnIcon == null) {
          _warnIcon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_console.warnicon@2x"  : "console.warnicon@2x");
        }
        return _warnIcon;
      }
    }

    private static Texture2D _errorIcon;
    public static Texture2D ErrorIcon {
      get {
        if (_errorIcon == null) {
          _errorIcon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_console.erroricon@2x" : "console.erroricon@2x");
        }
        return _errorIcon;
      }
    }

    const int IconWidth = 48;
      
    private static GUIStyle _baseHeaderLabelStyle;

    private static (int fontsize, int padTop) GetHeaderFontAttrs() {
      switch (EditorGUIUtility.pixelsPerPoint) {
        case 2.0f:
        case 3.0f:
        case 4.0f:
          return (15, 0);
        case 1.25f: 
        case 2.5f:
        case 2.25f:
        case 2.75f:
        case 3.5f:
          return (16, -1);
        default:
          return (17, -1);
      }
    }

    public static GUIStyle BaseHeaderLabelStyle {
      get {
        if (_baseHeaderLabelStyle == null) {
          var fontattrs = GetHeaderFontAttrs();

          _baseHeaderLabelStyle = new GUIStyle(EditorStyles.label) { 
            font = Resources.Load<Font>(FONT_PATH), 
            fontSize = fontattrs.fontsize, 
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(5, 5 /*IconWidth*/, fontattrs.padTop, 0), 
            margin  = new RectOffset(0, 0, 0, 0),
            border = new RectOffset(3, 3, 3, 3),
            clipping = TextClipping.Clip,
            wordWrap = true,

          };
        }
        return _baseHeaderLabelStyle;
      }
    }

    private static GUIStyle[] _fusionHeaderStyles;

    public static GUIStyle GetFusionHeaderBackStyle(int colorIndex) {

      if (_fusionHeaderStyles == null || _fusionHeaderStyles[0] == null) {
        string[] colorNames = Enum.GetNames(typeof(EditorHeaderBackColor));
        _fusionHeaderStyles = new GUIStyle[colorNames.Length];
        for (int i = 1; i < colorNames.Length; ++i) {
          var style = new GUIStyle(BaseHeaderLabelStyle);
          style.normal.background = HeaderTextures[i];
          style.normal.scaledBackgrounds = new Texture2D[] { HeaderTexturesHiRez[i] };
          style.border = new RectOffset(HEADER_CORNER_RADIUS, HEADER_CORNER_RADIUS, HEADER_CORNER_RADIUS, HEADER_CORNER_RADIUS);
          //style.fixedHeight = 24;
          style.normal.textColor = new Color(1, 1, 1, .9f);
          _fusionHeaderStyles[i] = style;
        }
      }
      return _fusionHeaderStyles[colorIndex];
    }

    static Texture2D[] _loadedIcons;
    public static Texture2D GetFusionIconTexture(EditorHeaderIcon icon) {
      if (_loadedIcons == null || _loadedIcons[0] == null) {
        string[] iconNames = Enum.GetNames(typeof(EditorHeaderIcon));
        _loadedIcons = new Texture2D[iconNames.Length];
        for (int i = 1; i < iconNames.Length; ++i) {
          _loadedIcons[i] = Resources.Load<Texture2D>(ICON_PATH + iconNames[i] + "HeaderIcon");
        }
      }
      return _loadedIcons[(int)icon];
    }


    private static GUIStyle[] _groupBoxStyles;
    private static GUIStyle GetGroupBoxStyle(GroupBoxType groupType) {
      // texture can be reset without the styles going null in some cases (scene changes, exiting play mode, etc), so need to make sure the texture itself has not gone null always.
      if (_groupBoxStyles == null || _groupBoxStyles[0] == null || _groupBoxStyles[0].normal.background == null) {
        var len = Enum.GetNames(typeof(GroupBoxType)).Length;
        _groupBoxStyles = new GUIStyle[len];
        for (int i = 0; i < len; ++i) {
          var info = _groupBoxInfo[i];
          _groupBoxStyles[i] = CreateGroupStyle(i, info);
        }
      }
      return _groupBoxStyles[(int)groupType];
    }

    public static GUIStyle GetStyle(this GroupBoxType type) {
      return GetGroupBoxStyle(type);
    }

    private static GUIStyle CreateGroupStyle(int groupBoxType, GroupBoxInfo info) {

      var style = new GUIStyle(EditorStyles.label);
      var border = GroupBoxTextures[groupBoxType].width / 2;
      //var border = HEADER_CORNER_RADIUS + info.BorderWidth.GetValueOrDefault(0);
      var padding = info.Padding.GetValueOrDefault(10);
      var margin  = info.Margin != null ? info.Margin : new RectOffset(5, 5, 5, 5);
      style.border = new RectOffset(border, border, border, border);
      style.padding = new RectOffset(padding, padding, padding, padding);
      style.margin = margin;
      style.wordWrap = true;
      style.normal.background = GroupBoxTextures[groupBoxType];
      style.normal.scaledBackgrounds = new Texture2D[] { GroupBoxTexturesHiRez[groupBoxType] };
      style.fontSize = 11;

      if (info.RichTextBox.GetValueOrDefault()) {
        style.alignment = TextAnchor.UpperLeft;
        style.richText = true;
      }
      return style;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/FusionHub/FusionHubWindow.cs

namespace Fusion.Editor {
#if FUSION_WEAVER && UNITY_EDITOR
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;
  using EditorUtility = UnityEditor.EditorUtility;

  [InitializeOnLoad]
  public partial class FusionHubWindow : EditorWindow {

    const int NAV_WIDTH = 256 + 2;

    private static bool? ready; // true after InitContent(), reset onDestroy, onEnable, etc.

    private static Vector2 windowSize;
    private static Vector2 windowPosition = new Vector2(100, 100);

    int currentSection;
    // Indicates that the AppId is invalid and needs to be presented on the welcome screen.
    static bool _showAppIdInWelcome;

    [MenuItem("Fusion/Fusion Hub &f", false, 0)]
    public static void Open() {
      if (Application.isPlaying) {
        return;
      }

      FusionHubWindow window = GetWindow<FusionHubWindow>(true, WINDOW_TITLE, true);
      window.position = new Rect(windowPosition, windowSize);
      _showAppIdInWelcome = !IsAppIdValid();
      window.Show();
    }

    private static void ReOpen() {
      if (ready.HasValue && ready.Value == false) {
        Open();
      }

      EditorApplication.update -= ReOpen;
    }


    private void OnEnable() {
      ready = false;
      windowSize = new Vector2(800, 540);

      this.minSize = windowSize;

      // Pre-load Release History
      this.PrepareReleaseHistoryText();
      wantsMouseMove = true;
    }

    private void OnDestroy() {
      ready = false;
    }

    private void OnGUI() {

      GUI.skin = FusionHubSkin;

      try {
        InitContent();

        windowPosition = this.position.position;

        // full window wrapper
        EditorGUILayout.BeginHorizontal(GUI.skin.window);
        {
          // Left Nav menu
          EditorGUILayout.BeginVertical(GUILayout.MaxWidth(NAV_WIDTH), GUILayout.MinWidth(NAV_WIDTH));
          DrawHeader();
          DrawLeftNavMenu();
          EditorGUILayout.EndVertical();

          // Right Main Content
          EditorGUILayout.BeginVertical();
          DrawContent();
          EditorGUILayout.EndVertical();

        }
        EditorGUILayout.EndHorizontal();

        DrawFooter();

      } catch (Exception) {
        // ignored
      }

      // Force repaints while mouse is over the window, to keep Hover graphics working (Unity quirk)
      var timeSinceStartup = Time.realtimeSinceStartupAsDouble;
      if (Event.current.type == EventType.MouseMove && timeSinceStartup > _nextForceRepaint) {
        // Cap the repaint rate a bit since we are forcing repaint on mouse move
        _nextForceRepaint = timeSinceStartup + .05f;
        Repaint();
      }
    }

    private double _nextForceRepaint;
    private Vector2 _scrollRect;

    private void DrawContent() {
      {
        var section = Sections[currentSection];
        GUILayout.Label(section.Description, headerTextStyle);

        EditorGUILayout.BeginVertical(FusionHubSkin.box);
        _scrollRect = EditorGUILayout.BeginScrollView(_scrollRect);
        section.DrawMethod.Invoke();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
      }
    }

    static void DrawWelcomeSection() {

      // Top Welcome content box
      GUILayout.Label(WELCOME_TEXT);
      GUILayout.Space(16);

      if (_showAppIdInWelcome)
        DrawSetupAppIdBox();
    }

    static void DrawSetupSection() {
      DrawSetupAppIdBox();
      DrawButtonAction(Icon.FusionIcon, "Fusion Network Project Settings", "Network settings specific to Fusion.", 
        callback: () => NetworkProjectConfigUtilities.PingGlobalConfigAsset(true));
      DrawButtonAction(Icon.PhotonCloud, "Photon App Settings", "Network settings specific to the Photon transport.",
        callback: () => { EditorGUIUtility.PingObject(Photon.Realtime.PhotonAppSettings.Instance); Selection.activeObject = Photon.Realtime.PhotonAppSettings.Instance; });

    }

    static void DrawDocumentationSection() {
      DrawButtonAction(Icon.Documentation, "Fusion Introduction", "The Fusion Introduction web page.", callback: OpenURL(UrlFusionIntro));
      DrawButtonAction(Icon.Documentation, "SDK and Release Notes", "Link to the latest Fusion version SDK.", callback: OpenURL(UrlFusionSDK));
      DrawButtonAction(Icon.Documentation, "API Reference", "The API library reference documentation.", callback: OpenURL(UrlFusionDocApi));
    }

    static void DrawSamplesSection() {

      GUILayout.Label("Tutorials", headerLabelStyle);
      DrawButtonAction(Icon.Samples, "Fusion 100 Tutorial", "Fusion Fundamentals Tutorial", callback: OpenURL(UrlFusion100));
      
      //GUILayout.Label("Samples", headerLabelStyle);
      //DrawButtonAction(Icon.Samples, "Fusion Samples", "Collection of Demos and Tech Samples", callback: OpenURL(UrlSampleSection));

      // Hidden for now
      /*
      DrawButtonAction(Icon.Samples, "Fusion 100 Tutorial", "Fusion Fundamentals", callback: OpenURL(UrlFusion100));
      DrawButtonAction(Icon.Samples, "Fusion Application Loop", "Matchmaking, Room Creation, Scene Loading, and Shutdown", callback: OpenURL(UrlFusionLoop));

      GUILayout.Label("Samples", headerLabelStyle);
      //DrawButtonAction(Resources.Load<Texture2D>("FusionHubSampleIcons/tanknarok-logo"), "Fusion Tanknarok", callback: OpenURL(UrlTanks));
      DrawButtonAction(Icon.Samples, "Tanknarok", "Vehicle Control, and Predicted Projectile Spawns", callback: OpenURL(UrlTanks));
      DrawButtonAction(Icon.Samples, "Fusion Karts", "Advanced Player Rigidbody Prediction", callback: OpenURL(UrlKarts));
      DrawButtonAction(Icon.Samples, "DragonHunters VR", "VR Movement, and Object Manipulation", callback: OpenURL(UrlDragonHuntersVR));
      GUILayout.Space(15);
      */

      //DrawButtonAction(Icon.Samples, "Hello Fusion Demo", callback: OpenURL(UrlHelloFusion));
      //DrawButtonAction(Icon.Samples, "Hello Fusion VR Demo", callback: OpenURL(UrlHelloFusionVr));
    }

    static void DrawRealtimeReleaseSection() {
      GUILayout.BeginVertical();
      {
        GUILayout.Space(5);

        DrawReleaseHistoryItem("Added:", releaseHistoryTextAdded);
        DrawReleaseHistoryItem("Changed:", releaseHistoryTextChanged);
        DrawReleaseHistoryItem("Fixed:", releaseHistoryTextFixed);
        DrawReleaseHistoryItem("Removed:", releaseHistoryTextRemoved);
        DrawReleaseHistoryItem("Internal:", releaseHistoryTextInternal);
      }
      GUILayout.EndVertical();
    }

    static void DrawFusionReleaseSection() {
      GUILayout.Label(fusionReleaseHistory, releaseNotesStyle);
    }

    static void DrawReleaseHistoryItem(string label, List<string> items) {
      if (items != null && items.Count > 0) {
        GUILayout.BeginVertical();
        {
          GUILayout.Space(5);

          foreach (string text in items) {
            GUILayout.Label(string.Format("- {0}.", text), textLabelStyle);
          }
        }
        GUILayout.EndVertical();
      }
    }

    static void DrawSupportSection() {

      GUILayout.BeginVertical();
      GUILayout.Space(5);
      GUILayout.Label(SUPPORT, textLabelStyle);
      GUILayout.EndVertical();

      GUILayout.Space(15);

      DrawButtonAction(Icon.Community, DISCORD_HEADER, DISCORD_TEXT, callback: OpenURL(UrlDiscordGeneral));
      DrawButtonAction(Icon.Documentation, DOCUMENTATION_HEADER, DOCUMENTATION_TEXT, callback: OpenURL(UrlFusionDocsOnline));
    }

    static void DrawSetupAppIdBox() {
      var realtimeSettings = Photon.Realtime.PhotonAppSettings.Instance;
      var realtimeAppId = realtimeSettings.AppSettings.AppIdFusion;
      // Setting up AppId content box.
      EditorGUILayout.BeginVertical(FusionHubSkin.GetStyle("SteelBox") /*contentBoxStyle*/) ;
      {
        GUILayout.Label(REALTIME_APPID_SETUP_INSTRUCTIONS);

        DrawButtonAction(Icon.PhotonCloud, "Open the Photon Dashboard", callback: OpenURL(UrlDashboard));
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal(FusionHubSkin.GetStyle("SteelBox"));
        {
          EditorGUI.BeginChangeCheck();
          GUILayout.Label("Fusion App Id:", GUILayout.Width(120));
          var icon = IsAppIdValid() ? Resources.Load<Texture2D>("icons/correct-icon") : EditorGUIUtility.FindTexture("console.erroricon.sml");
          GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));
          var editedAppId = EditorGUILayout.DelayedTextField("", realtimeAppId, FusionHubSkin.textField, GUILayout.Height(24));
          if (EditorGUI.EndChangeCheck()) {
            realtimeSettings.AppSettings.AppIdFusion = editedAppId;
            EditorUtility.SetDirty(realtimeSettings);
            AssetDatabase.SaveAssets();
          }
        }
        EditorGUILayout.EndHorizontal();
      }
      EditorGUILayout.EndVertical();
    }

    void DrawLeftNavMenu() {
      for (int i = 0; i < Sections.Length; ++i) {
        var section = Sections[i];
        if (DrawNavButton(section, currentSection == i)) {
          // Check if appid is valid whenever we change sections. It no longer needs to be shown on welcome page once it is set.
          _showAppIdInWelcome = !IsAppIdValid();
          currentSection = i;
        }
      }
    }

    static void DrawHeader() {
      GUILayout.Label(Icons[(int)Icon.ProductLogo], _navbarHeaderGraphicStyle);
    }

    static void DrawFooter() {
      GUILayout.BeginHorizontal(FusionHubSkin.window);
      {
        GUILayout.Label("\u00A9 2022, Exit Games GmbH. All rights reserved.");
      }
      GUILayout.EndHorizontal();
    }

    static bool DrawNavButton(Section section, bool currentSection) {
      var content = new GUIContent() {
        text = "  " + section.Title,
        image = Icons[(int)section.Icon],
      };

      var renderStyle = currentSection ? buttonActiveStyle : GUI.skin.button;
      return GUILayout.Button(content, renderStyle);
    }

    static void DrawButtonAction(Icon icon, string header, string description = null, bool? active = null, Action callback = null, int? width = null) {
      DrawButtonAction(Icons[(int)icon], header, description, active, callback, width);
    }

    static void DrawButtonAction(Texture2D icon, string header, string description = null, bool? active = null, Action callback = null, int? width = null) {

      var padding = GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
      var height = icon.height + padding;

      var renderStyle = active.HasValue && active.Value == true ? buttonActiveStyle : GUI.skin.button;
      // Draw text separately (not part of button guiconent) to have control over the space between the icon and the text.
      var rect = EditorGUILayout.GetControlRect(false, height, width.HasValue ? GUILayout.Width(width.Value) : GUILayout.ExpandWidth(true));
      bool clicked = GUI.Button(rect, icon, renderStyle);
      GUI.Label(new Rect(rect) { xMin = rect.xMin + icon.width + 20 }, description == null ? "<b>" + header +"</b>" : string.Format("<b>{0}</b>\n{1}", header, "<color=#aaaaaa>" + description + "</color>"));
      if (clicked && callback != null) {
        callback.Invoke();
      }
    }
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/FusionHub/FusionHubWindowUtils.cs

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
  using UnityEditor;
  using UnityEngine;

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

    static Texture2D[] Icons;
    internal enum Icon {
      [Description("FusionHubIcons/information")]
      Setup,
      [Description("FusionHubIcons/documentation")]
      Documentation,
      [Description("FusionHubIcons/samples")]
      Samples,
      [Description("FusionHubIcons/community")]
      Community,
      [Description("FusionHubIcons/fusion-logo")]
      ProductLogo,
      [Description("FusionHubIcons/photon-cloud-32-dark")]
      PhotonCloud,
      [Description("FusionHubIcons/fusion-icon")]
      FusionIcon,
    }

    static Section[] Sections = new Section[] {
        new Section("Welcome", "Welcome to Photon Fusion", DrawWelcomeSection, Icon.Setup),
        new Section("Fusion Setup", "Setup Photon Fusion", DrawSetupSection, Icon.PhotonCloud),
        new Section("Tutorials & Samples", "Fusion Tutorials and Samples", DrawSamplesSection, Icon.Samples),
        new Section("Documentation", "Photon Fusion Documentation", DrawDocumentationSection, Icon.Documentation),
        new Section("Fusion Release Notes", "Fusion Release Notes", DrawFusionReleaseSection, Icon.Documentation),
        //new Section("Realtime Release Notes", "Realtime Release Notes", DrawRealtimeReleaseSection, Icon.Documentation),
        new Section("Support", "Support and Community Links", DrawSupportSection, Icon.Community),
    };

    internal const string UrlFusionDocsOnline = "https://doc.photonengine.com/fusion/";
    internal const string UrlFusionIntro = "https://doc.photonengine.com/fusion/current/getting-started/fusion-intro";
    internal const string UrlFusionSDK = "https://doc.photonengine.com/fusion/current/getting-started/sdk-download";
    internal const string UrlCloudDashboard = "https://id.photonengine.com/account/signin?email=";
    internal const string UrlDiscordGeneral = "https://discord.gg/qP6XVe3XWK";
    internal const string UrlDashboard = "https://dashboard.photonengine.com/";
    internal const string UrlSampleSection = "https://doc.photonengine.com/fusion/current/samples/overview";
    internal const string UrlFusion100 = "https://doc.photonengine.com/fusion/current/fusion-100/overview";
    internal const string UrlFusionLoop = "https://doc.photonengine.com/fusion/current/samples/fusion-application-loop";
    internal const string UrlHelloFusion = "https://doc.photonengine.com/fusion/current/hello-fusion/hello-fusion";
    internal const string UrlHelloFusionVr = "https://doc.photonengine.com/fusion/current/hello-fusion/hello-fusion-vr";
    internal const string UrlTanks = "https://doc.photonengine.com/fusion/current/samples/fusion-tanknarok";
    internal const string UrlKarts = "https://doc.photonengine.com/fusion/current/samples/fusion-karts";
    internal const string UrlDragonHuntersVR = "https://doc.photonengine.com/fusion/current/samples/fusion-dragonhunters-vr";

    internal const string UrlFusionDocApi = "https://doc-api.photonengine.com/en/fusion/current/annotated.html";

    internal const string WINDOW_TITLE = "Photon Fusion Hub";
    internal const string SUPPORT = "You can contact the Photon Team using one of the following links. You can also go to Photon Documentation in order to get started.";
    internal const string DISCORD_TEXT = "Join the Discord.";
    internal const string DISCORD_HEADER = "Community";
    internal const string DOCUMENTATION_TEXT = "Open the documentation.";
    internal const string DOCUMENTATION_HEADER = "Documentation";
    internal const string WELCOME_TEXT = "Thank you for installing Photon Fusion, " +
      "and welcome to the Photon Fusion.\n\n" +
      "Once you have set up your Fusion App Id, explore the sections on the left to get started. " +
      "More samples, tutorials, and documentation are being added regularly - so check back often.";

    internal const string REALTIME_APPID_SETUP_INSTRUCTIONS =
@"<b>An App Id specific to Fusion is required for networking.</b>

To acquire an App Id:
- Open the Photon Dashboard (Log-in as required)
- Select an existing Fusion App Id, or create a new one.
- Copy the App Id and paste into the field below (or into the PhotonAppSettings.asset).
";

    internal const string GETTING_STARTED_INSTRUCTIONS =
      @"Links to demos, tutorials, API references and other information can be found on the PhotonEngine.com website.";

    private static string releaseHistoryHeader;
    private static List<string> releaseHistoryTextAdded;
    private static List<string> releaseHistoryTextChanged;
    private static List<string> releaseHistoryTextFixed;
    private static List<string> releaseHistoryTextRemoved;
    private static List<string> releaseHistoryTextInternal;

    private static string fusionReleaseHistory;

    private static GUISkin _fusionHubSkinBacking;
    static GUISkin FusionHubSkin {
      get {
        if (_fusionHubSkinBacking == null) {
          _fusionHubSkinBacking = Resources.Load<GUISkin>("FusionHubSkin/FusionHubSkin");
        }
        return _fusionHubSkinBacking;
      }
    }

    private static GUIStyle _navbarHeaderGraphicStyle;
    private static GUIStyle textLabelStyle;
    private static GUIStyle headerLabelStyle;
    private static GUIStyle releaseNotesStyle;
    private static GUIStyle headerTextStyle;
    private static GUIStyle buttonActiveStyle;

    /// <summary>
    /// Converts the enumeration of icons into the array of textures.
    /// </summary>
    private static void ConvertIconEnumToArray() {
      bool isProSkin = EditorGUIUtility.isProSkin;
      // convert icon enum into array of textures
      var icons = Enum.GetValues(typeof(Icon));
      var list = new List<Texture2D>();
      for (int i = 0; i < icons.Length; ++i) {
        // : indicates two paths, one for dark, and one for light
        var path = ((Icon)i).GetDescription();
        if (path.Contains(":")) {
          if (isProSkin) {
            path = path.Substring(0, path.IndexOf(":"));
          } else {
            path = path.Substring(path.IndexOf(":") + 1);
          }
        }
        list.Add(Resources.Load<Texture2D>(path));
      }
      Icons = list.ToArray();
    }

    private static void InitContent() {
      if (ready.HasValue && ready.Value) {
        return;
      }

      ConvertIconEnumToArray();

      Color commonTextColor = Color.white;

      var _guiSkin = FusionHubSkin;

      _navbarHeaderGraphicStyle = new GUIStyle(_guiSkin.button) { alignment = TextAnchor.MiddleCenter };

      headerTextStyle = new GUIStyle(_guiSkin.label) {
        fontSize = 18,
        padding = new RectOffset(12, 8, 8, 8),
        fontStyle = FontStyle.Bold,
        normal = { textColor = commonTextColor }
      };

      buttonActiveStyle = new GUIStyle(_guiSkin.button) {
        fontStyle = FontStyle.Bold,
        normal = { background = _guiSkin.button.active.background, textColor = Color.white }
      };


      textLabelStyle = new GUIStyle(_guiSkin.label) {
        wordWrap = true,
        normal   =  { textColor = commonTextColor },
        richText = true,
        
      };
      headerLabelStyle = new GUIStyle(textLabelStyle) {
        fontSize = 15,
      };

      releaseNotesStyle = new GUIStyle(textLabelStyle) {
        richText = true,
      };

      ready = true;
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
      var photonSettings = NetworkProjectConfigUtilities.GetOrCreatePhotonAppSettingsAsset();
      var val = photonSettings.AppSettings.AppIdFusion;
      try {
        new Guid(val);
      } catch {
        return false;
      }
      return true;
    }

    static string titleVersionReformat, sectionReformat, header1Reformat, header2Reformat, header3Reformat, classReformat;

    void InitializeFormatters() {
      titleVersionReformat = "<size=22><color=white>$1</color></size>" ;
      sectionReformat = "<i><color=lightblue>$1</color></i>";
      header1Reformat = "<size=22><color=white>$1</color></size>";
      header2Reformat = "<size=18><color=white>$1</color></size>";
      header3Reformat = "<b><color=#ffffaaff>$1</color></b>";
      classReformat   = "<color=#FFDDBB>$1</color>";
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
      }

      // Realtime
      {
        try {

          var filePath = BuildPath(Application.dataPath, "Photon", "PhotonRealtime", "Code", "changes-realtime.txt");

          var text = (TextAsset)AssetDatabase.LoadAssetAtPath(filePath, typeof(TextAsset));

          var baseText = text.text;

          var regexVersion  = new Regex(@"Version (\d+\.?)*",   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexAdded    = new Regex(@"\b(Added:)(.*)\b",    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexChanged  = new Regex(@"\b(Changed:)(.*)\b",  RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexUpdated  = new Regex(@"\b(Updated:)(.*)\b",  RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexFixed    = new Regex(@"\b(Fixed:)(.*)\b",    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
          var regexRemoved  = new Regex(@"\b(Removed:)(.*)\b",  RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
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
        } catch (Exception) {
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
      GUIStyle toggle = new GUIStyle("Toggle") {
        margin = new RectOffset(),
        padding = new RectOffset()
      };

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


#region Assets/Photon/Fusion/Scripts/Editor/FusionInstaller.cs

namespace Fusion.Editor {
#if !FUSION_DEV
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEditor.PackageManager;
  using UnityEngine;

  [InitializeOnLoad]
  class FusionInstaller {
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

      if (defines.IndexOf(DEFINE, StringComparison.Ordinal) >= 0) {
        return;
      }

      if (!PlayerSettings.runInBackground) {
        FusionEditorLog.LogInstaller($"Setting {nameof(PlayerSettings)}.{nameof(PlayerSettings.runInBackground)} to true");
        PlayerSettings.runInBackground = true;
      }

      var manifest = Path.Combine(Path.GetDirectoryName(Application.dataPath), PACKAGES_DIR, MANIFEST_FILE);

      if (File.ReadAllText(manifest).IndexOf(PACKAGE_TO_SEARCH, StringComparison.Ordinal) >= 0) {
        FusionEditorLog.LogInstaller($"Setting '{DEFINE}' Define");
#if UNITY_SERVER
        PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Server, defines + ";" + DEFINE);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines + ";" + DEFINE);
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


#region Assets/Photon/Fusion/Scripts/Editor/FusionProfiler/FusionSamplerWindow.cs

// deleted on 31st May 2021

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/FusionWeaverTriggerImporter.cs

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
      var configPath = NetworkProjectConfigUtilities.GetGlobalConfigPath(false);
      if (string.IsNullOrEmpty(configPath)) {
        return;
      }

      try {
        var cfg = NetworkProjectConfigImporter.LoadConfigFromFile(configPath);

        var hash = new Hash128();

        foreach (var key in cfg.AccuracyDefaults.coreKeys) {
          hash.Append(key);
        }
        foreach (var val in cfg.AccuracyDefaults.coreVals) {
          hash.Append(val._value);
        }
        foreach (var key in cfg.AccuracyDefaults.tags) {
          hash.Append(key);
        }
        foreach (var val in cfg.AccuracyDefaults.values) {
          hash.Append(val._value);
        }
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


#region Assets/Photon/Fusion/Scripts/Editor/ILWeaverUtils.cs

namespace Fusion.Editor {
  using UnityEditor;
  using UnityEditor.Compilation;
  
  [InitializeOnLoad]
  public static class ILWeaverUtils {
    [MenuItem("Fusion/Run Weaver")]
    public static void RunWeaver() {

      // ensure config exists
      _ = NetworkProjectConfigUtilities.GetGlobalConfigPath();

      CompilationPipeline.RequestScriptCompilation(
#if UNITY_2021_1_OR_NEWER
        RequestScriptCompilationOptions.CleanBuildCache
#endif
      );
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/NetworkBehaviourEditor.cs

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
      base.PrepareInspectorGUI();

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

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
        base.DrawDefaultInspector();
#else
        // can networked properties be altered for all selected targets?
        bool? hasStateAuthorityForAllTargets = null;
        foreach (var target in ValidTargets) {
          if (target.Object.HasStateAuthority) {
            hasStateAuthorityForAllTargets = true;
          } else {
            hasStateAuthorityForAllTargets = false;
            break;
          }
        }

        SerializedProperty prop = serializedObject.GetIterator();
        for (bool enterChildren = true; prop.NextVisible(enterChildren); enterChildren = false) {
          var field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(prop, out var type);
          var attributes = field?.GetCustomAttributes<DefaultForPropertyAttribute>() ?? Array.Empty<DefaultForPropertyAttribute>();
          if (attributes.Any()) {
            using (new EditorGUI.DisabledScope(hasStateAuthorityForAllTargets == false)) {
#if FUSION_DEV
              DrawNetworkedProperty(prop);
#else
              EditorGUILayout.PropertyField(prop, true);
#endif
            }
          } else {
            using (new EditorGUI.DisabledScope(prop.propertyPath == FusionEditorGUI.ScriptPropertyName)) {
              EditorGUILayout.PropertyField(prop, true);
            }
          }
        }
#endif
        
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
      DrawBehaviourActions();
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
        EditorGUILayout.BeginVertical(FusionGUIStyles.GroupBoxType.Warn.GetStyle());

        BehaviourEditorUtils.DrawWarnBox(NETOBJ_REQUIRED_WARN_TEXT, MessageType.Warning, FusionGUIStyles.GroupBoxType.None);
        
        GUILayout.Space(4);

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

        EditorGUILayout.EndVertical();
      }
    }

#if FUSION_DEV
    private GUIContent _label = new GUIContent();
    [NonSerialized]
    private string _prefixSpacing;

    private void DrawNetworkedProperty(SerializedProperty prop) {
      var rect = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(prop, true));

      const float imageHeight = 14;


      Texture2D image = FusionEditorGUI.NetworkedPropertyStyle.NetworkPropertyIcon;

      var buttonRect    = rect;
      buttonRect.height = EditorGUIUtility.singleLineHeight;
      buttonRect.width  = image.width * EditorGUIUtility.singleLineHeight / image.height;

      var imageRect    = buttonRect;
      imageRect.y     += (buttonRect.height - imageHeight) / 2;
      imageRect.height = imageHeight;
      imageRect.width  = image.width * imageHeight / image.height;

      EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
      bool clicked = GUI.Button(buttonRect, GUIContent.none, GUIStyle.none);

      // find out how many spaces we need
      if (string.IsNullOrEmpty(_prefixSpacing)) {
        GUIContent prefixContent = new GUIContent("  ");
        while (EditorStyles.label.CalcSize(prefixContent).x <= imageRect.width) {
          prefixContent.text += " ";
        }
        _prefixSpacing = prefixContent.text;
      }

      _label.text = $"{_prefixSpacing}{prop.displayName}";
      _label.tooltip = string.Empty;
      _label.image = null;


      // draw the property
      EditorGUI.PropertyField(rect, prop, _label, true);

      // draw the icon
      if (Event.current.type == EventType.Repaint) {
        GUI.DrawTexture(imageRect, image);
      }

      if (clicked) {
        PopupWindow.Show(buttonRect, new NetworkedPropertyPopupWindowContent(prop, rect.width));
      }
    }

    unsafe class NetworkedPropertyPopupWindowContent : PopupWindowContent {

      readonly SerializedProperty _property;
      readonly FieldInfo _field;
      readonly DefaultForPropertyAttribute[] _attributes;
      readonly float _width;
      readonly List<string> _placeholderStrings;
      readonly GUIContent _placeholder;
      readonly GUIStyle _rawStyle;
      readonly byte*[] _pointers;

      readonly GUIContent _wordOffsetLabel = new GUIContent("Word Offset");
      readonly GUIContent _wordCountLabel  = new GUIContent("Word Count");

      const int Margin = 2;

      public NetworkedPropertyPopupWindowContent(SerializedProperty property, float width) {
        _width              = width;
        _property           = property;
        _field              = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out _);
        _attributes         = _field.GetCustomAttributes<DefaultForPropertyAttribute>().ToArray() ?? Array.Empty<DefaultForPropertyAttribute>();
        _pointers           = new byte*[_property.serializedObject.targetObjects.Length];
        _placeholder        = new GUIContent();

        _placeholderStrings = _attributes
          .Select(x => new string(Enumerable.Repeat('F', x.WordCount * 2 * Allocator.REPLICATE_WORD_SIZE).ToArray()))
          .ToList();

        _rawStyle           = new GUIStyle(EditorStyles.numberField) {
          wordWrap          = true,
        };
      }

      public override void OnGUI(Rect rect) {
        {
          var boxRect = rect;
          boxRect.x      += Margin / 2;
          boxRect.width  -= Margin;
          boxRect.y      += Margin / 2;
          boxRect.height -= Margin;
          GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);
        }

        var targets = _property.serializedObject.targetObjects.Cast<NetworkBehaviour>().ToArray();

        rect.x      += Margin;
        rect.width  -= 2 * Margin;
        rect.y      += Margin;
        rect.height -= 2 * Margin;

        foreach (var (attribute, placeholder) in _attributes.Zip(_placeholderStrings, (a, b) => (a, b))) {

          rect.height = EditorGUIUtility.singleLineHeight;

          EditorGUI.LabelField(rect, attribute.PropertyName, EditorStyles.boldLabel);
          rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

          EditorGUI.SelectableLabel(EditorGUI.PrefixLabel(rect, _wordOffsetLabel), $"{attribute.WordOffset}");
          rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

          EditorGUI.SelectableLabel(EditorGUI.PrefixLabel(rect, _wordCountLabel), $"{attribute.WordCount}");
          rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

          _placeholder.text = placeholder;
          rect.height = Mathf.Max(EditorGUIUtility.singleLineHeight, _rawStyle.CalcHeight(_placeholder, rect.width - EditorGUIUtility.labelWidth));

          if (!targets.All(x => x && x.Object && x.Object.IsValid)) {
            using (new EditorGUI.DisabledScope(true)) {
              EditorGUI.TextField(rect, "Raw", "Target not valid");
            }
          } else {

            string content;
            bool readOnly = true;
            bool mixedValues = false;
            int byteCount = attribute.WordCount * Allocator.REPLICATE_WORD_SIZE;

            readOnly = targets.Any(x => !x.Object.HasStateAuthority);

            for (int i = 0; i < targets.Length; ++i) {
              _pointers[i] = (byte*)(targets[i].Ptr + attribute.WordOffset);
            }

            for (int i = 1; i < _pointers.Length; ++i) {
              if (Native.MemCmp(_pointers[0], _pointers[i], byteCount) != 0) {
                mixedValues = true;
                break;
              }
            }

            if (mixedValues) {
              content = string.Empty;
            } else {
              content = BinUtils.BytesToHex(_pointers[0], byteCount, columns: int.MaxValue, columnSeparator: "");
            }

            using (new EditorGUI.DisabledScope(readOnly)) {
              using (new FusionEditorGUI.ShowMixedValueScope(mixedValues)) {
                var id = GUIUtility.GetControlID(UnityInternal.EditorGUI.DelayedTextFieldHash, FocusType.Keyboard, rect);

                EditorGUI.BeginChangeCheck();
                var newStr = UnityInternal.EditorGUI.DelayedTextFieldInternal(rect, id, new GUIContent("Raw"), content, "0123456789abcdefABCDEF ", _rawStyle);
                if (EditorGUI.EndChangeCheck()) {
                  Native.MemClear(_pointers[0], byteCount);
                  BinUtils.HexToBytes(newStr, _pointers[0], byteCount);
                  for (int j = 1; j < _pointers.Length; ++j) {
                    Native.MemCpy(_pointers[j], _pointers[0], byteCount);
                  }
                  _property.serializedObject.Update();
                }
              }
            }
          }

          rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
        }
      }

      public override Vector2 GetWindowSize() {
        var result = base.GetWindowSize();
        result.x = _width;
        result.y = 2 * Margin;

        foreach (var (attribute, placeholder) in _attributes.Zip(_placeholderStrings, (a, b) => (a, b))) {
          result.y += 3 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

          _placeholder.text = placeholder;
          result.y += _rawStyle.CalcHeight(_placeholder, _width - EditorGUIUtility.labelWidth - 2 * Margin);
          result.y += EditorGUIUtility.standardVerticalSpacing;
        }
        return result;
      }
    }
#endif
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/NetworkMecanimAnimatorEditor.cs

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


#region Assets/Photon/Fusion/Scripts/Editor/NetworkObjectEditor.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
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

    public static bool BakeHierarchy(GameObject root, NetworkObjectGuid? prefabGuid, Action<object> setDirty = null, Func<NetworkObject, NetworkObjectGuid> guidProvider = null) {
      var networkObjectsBuffer = new List<NetworkObject>();
      var simulationBehaviourBuffer = new List<SimulationBehaviour>();
      var networkBehaviourBuffer = new List<NetworkBehaviour>();

      Dictionary<Type, int> executionOrderCache = new Dictionary<Type, int>();

      int GetExecutionOrderWarnIfFailed(MonoBehaviour obj) {
        if (executionOrderCache.TryGetValue(obj.GetType(), out var result)) {
          return result;
        }
        var monoScript = MonoScript.FromMonoBehaviour(obj);
        if (monoScript) {
          result = MonoImporter.GetExecutionOrder(monoScript);
        } else {
          Debug.LogWarning($"Unable to get execution order for deactivated {obj?.GetType().FullName}. Assuming 0.", obj);
          result = 0;
        }
        executionOrderCache.Add(obj.GetType(), result);
        return result;
      }

      bool dirty = false;

      using (var pathCache = new TransformPathCache()) {
        root.GetComponentsInChildren(true, networkObjectsBuffer);
        if (networkObjectsBuffer.Count == 0) {
          return dirty;
        }

        var networkObjects = networkObjectsBuffer.Select(x => new {
          Path = pathCache.Create(x.transform),
          Object = x
        }).OrderByDescending(x => x.Path).ToList();

        root.GetComponentsInChildren(true, simulationBehaviourBuffer);

        // sort scripts in a descending way
        var networkScripts = simulationBehaviourBuffer.Select(x => new {
          Path = pathCache.Create(x.transform),
          Script = x
        }).OrderBy(x => x.Path).ToList();

        // start from the leaves
        for (int i = 0; i < networkObjects.Count; ++i) {
          var entry = networkObjects[i];
          var objActive = entry.Object.gameObject.activeInHierarchy;
          int objExecutionOrder = 0;
          if (!objActive) {
            objExecutionOrder = GetExecutionOrderWarnIfFailed(entry.Object);
          }

          // find nested behaviours
          networkBehaviourBuffer.Clear();
          simulationBehaviourBuffer.Clear();

          string entryPath = entry.Path.ToString();
          for (int scriptIndex = networkScripts.Count - 1; scriptIndex >= 0; --scriptIndex) {
            var scriptEntry = networkScripts[scriptIndex];
            var scriptPath = scriptEntry.Path.ToString();

            if (entry.Path.IsEqualOrAncestorOf(scriptEntry.Path)) {
              var script = scriptEntry.Script;
              if (script is NetworkBehaviour networkBehaviour) {
                networkBehaviour.Object = entry.Object;
                networkBehaviourBuffer.Add(networkBehaviour);
              } else {
                simulationBehaviourBuffer.Add(script);
              }
              networkScripts.RemoveAt(scriptIndex);

              if (!objActive) {
                // check if execution order is ok
                int scriptExecutionOrder = GetExecutionOrderWarnIfFailed(script);

                if (objExecutionOrder <= scriptExecutionOrder) {
                  Debug.LogWarning($"{entry.Object?.GetType().FullName} execution order is less or equal than of the script {script?.GetType().FullName}. " +
                    $"This will result in Spawned callback being invoked before the script's Awake.", script);
                }
              }


            } else if (entry.Path.CompareTo(scriptEntry.Path) < 0) {
              // can't discard it yet
            } else {
              Debug.Assert(entry.Path.CompareTo(scriptEntry.Path) > 0);
              break;
            }
          }

          networkBehaviourBuffer.Reverse();
          dirty |= Set(entry.Object, ref entry.Object.NetworkedBehaviours, networkBehaviourBuffer, setDirty);

          simulationBehaviourBuffer.Reverse();
          dirty |= Set(entry.Object, ref entry.Object.SimulationBehaviours, simulationBehaviourBuffer, setDirty);

          // handle flags

          var flags = entry.Object.Flags;

          if (!flags.IsVersionCurrent()) {
            flags = flags.SetCurrentVersion();
          }

          flags &= ~NetworkObjectFlags.RuntimeFlagsMask;


          if (prefabGuid == null) {
            if (flags.IsPrefab()) {
              dirty |= Set(entry.Object, ref entry.Object.NetworkGuid, default, setDirty);
            }
            flags = flags.SetType(NetworkObjectFlags.TypeSceneObject);
            if (guidProvider == null) {
              throw new ArgumentNullException(nameof(guidProvider));
            }
            dirty |= Set(entry.Object, ref entry.Object.NetworkGuid, guidProvider(entry.Object), setDirty);
          } else {
            flags = flags.SetType(entry.Path.Depth == 1 ? NetworkObjectFlags.TypePrefab : NetworkObjectFlags.TypePrefabChild);
            if (entry.Path.Depth > 1) {
              dirty |= Set(entry.Object, ref entry.Object.NetworkGuid, NetworkObjectGuid.Empty, setDirty);
            } else {
              if (prefabGuid?.IsValid != true) {
                throw new ArgumentException($"Invalid value: {prefabGuid}", nameof(prefabGuid));
              }

              dirty |= Set(entry.Object, ref entry.Object.NetworkGuid, prefabGuid.Value, setDirty);
            }
          }

          flags = flags.SetActivatedByUser(entry.Object.gameObject.activeInHierarchy == false);
          dirty |= Set(entry.Object, ref entry.Object.Flags, flags, setDirty);
        }

        // what's left is nested network objects resolution
        for (int i = 0; i < networkObjects.Count; ++i) {
          var entry = networkObjects[i];
          networkObjectsBuffer.Clear();

          // collect descendants; descendants should be continous without gaps here
          int j = i - 1;
          for (; j >= 0 && entry.Path.IsAncestorOf(networkObjects[j].Path); --j) {
            networkObjectsBuffer.Add(networkObjects[j].Object);
          }

          int descendantsBegin = j + 1;
          Debug.Assert(networkObjectsBuffer.Count == i - descendantsBegin);

          dirty |= Set(entry.Object, ref entry.Object.NestedObjects, networkObjectsBuffer, setDirty);
        }
      }

      return dirty;
    }

    static string GetLoadInfoString(NetworkObject prefab) {
      if (NetworkProjectConfigUtilities.TryGetPrefabSource(prefab.NetworkGuid, out INetworkPrefabSource prefabAsset)) { 
        return prefabAsset.EditorSummary;
      }
      return "Null";
    }


    public override void OnInspectorGUI() {

      FusionEditorGUI.InjectPropertyDrawers(serializedObject);
      FusionEditorGUI.ScriptPropertyField(serializedObject);

      // these properties' isExpanded are going to be used for foldouts; that's the easiest
      // way to get quasi-persistent foldouts
      var guidProperty = serializedObject.FindPropertyOrThrow(nameof(NetworkObject.NetworkGuid));
      var flagsProperty = serializedObject.FindPropertyOrThrow(nameof(NetworkObject.Flags));
      var obj = (NetworkObject)base.target;
      var netObjType = typeof(NetworkObject);

      if (targets.Length == 1) {
        if (AssetDatabase.IsMainAsset(obj.gameObject) || PrefabStageUtility.GetPrefabStage(obj.gameObject)?.prefabContentsRoot == obj.gameObject) {
          Debug.Assert(!AssetDatabaseUtils.IsSceneObject(obj.gameObject));

          if (!obj.Flags.IsVersionCurrent() || !obj.Flags.IsPrefab() || !obj.NetworkGuid.IsValid) {
            BehaviourEditorUtils.DrawWarnBox("Prefab needs to be reimported.", MessageType.Error);
            if (GUILayout.Button("Reimport")) {
              string assetPath = PrefabStageUtility.GetPrefabStage(obj.gameObject)?.assetPath ?? AssetDatabase.GetAssetPath(obj.gameObject);
              Debug.Assert(!string.IsNullOrEmpty(assetPath));
              AssetDatabase.ImportAsset(assetPath);
            }
          } else {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefab Settings", EditorStyles.boldLabel);

            // Is Spawnable
            {
              EditorGUI.BeginChangeCheck();

              bool spawnable = this.DrawToggleForProperty(nameof(NetworkObject.IsSpawnable), !obj.Flags.IsIgnored(), netObjType);
              if (EditorGUI.EndChangeCheck()) {
                var value = obj.Flags.SetIgnored(!spawnable);
                serializedObject.FindProperty(nameof(NetworkObject.Flags)).intValue = (int)value;
                serializedObject.ApplyModifiedProperties();
              }
              EditorGUILayout.LabelField("Prefab Source", spawnable ? GetLoadInfoString(obj) : "---");
            }
          }
        } else if (AssetDatabaseUtils.IsSceneObject(obj.gameObject)) {
          if (!obj.Flags.IsVersionCurrent() || !obj.Flags.IsSceneObject() || !obj.NetworkGuid.IsValid) {
            if (!EditorApplication.isPlaying) {
              BehaviourEditorUtils.DrawWarnBox("This object hasn't been baked yet. Save the scene or enter playmode.");
            }
          }
        }
      }


      if (EditorApplication.isPlaying && targets.Length == 1) {
        EditorGUILayout.Space();
        flagsProperty.isExpanded = EditorGUILayout.Foldout(flagsProperty.isExpanded, "Runtime Info");
        if (flagsProperty.isExpanded) {
          using (new EditorGUI.IndentLevelScope()) {

            EditorGUILayout.BeginVertical(FusionGUIStyles.GetStyle(FusionGUIStyles.GroupBoxType.Outline));
            {
              EditorGUILayout.Toggle("Is Valid", obj.IsValid);
              if (obj.IsValid) {
                //EditorGUILayout.LabelField("Id", obj.Id.ToString());
                this.DrawLabelForProperty(nameof(NetworkObject.Id), obj.Id.ToString(), netObjType);

                EditorGUILayout.IntField("Word Count", NetworkObject.GetWordCount(obj));
                this.DrawToggleForProperty(nameof(NetworkObject.IsSceneObject),     obj.IsSceneObject, netObjType);

                bool headerIsNull = obj.Header == null;
                this.DrawLabelForProperty(nameof(NetworkObjectHeader.NestingRoot), headerIsNull ? "---" : obj.Header->NestingRoot.ToString(), typeof(NetworkObjectHeader));
                this.DrawLabelForProperty(nameof(NetworkObjectHeader.NestingKey),  headerIsNull ? "---" : obj.Header->NestingKey.ToString(),  typeof(NetworkObjectHeader));

                this.DrawLabelForProperty(nameof(NetworkObject.InputAuthority),     obj.InputAuthority.ToString(), netObjType);
                this.DrawLabelForProperty(nameof(NetworkObject.StateAuthority),     obj.StateAuthority.ToString(), netObjType);

                this.DrawToggleForProperty(nameof(NetworkObject.HasInputAuthority), obj.HasInputAuthority,         netObjType);
                this.DrawToggleForProperty(nameof(NetworkObject.HasStateAuthority), obj.HasStateAuthority,         netObjType);
                this.DrawToggleForProperty(nameof(NetworkObject.InSimulation),      obj.InSimulation,              netObjType);

                EditorGUILayout.Toggle("Is Local PlayerObject", obj == obj.Runner.GetPlayerObject(obj.Runner.LocalPlayer));

              }
            }
            EditorGUILayout.EndVertical();

          }
        }
      }

      EditorGUI.BeginChangeCheck();

      var config = NetworkProjectConfig.Global;
      var isPlaying = EditorApplication.isPlaying;
      var ecIsEnabled = config.Simulation.ReplicationMode == SimulationConfig.StateReplicationModes.EventualConsistency;

      using (new EditorGUI.DisabledScope(isPlaying)) {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shared Mode Settings", EditorStyles.boldLabel);

        var destroyWhenStateAuthLeaves = serializedObject.FindProperty(nameof(NetworkObject.DestroyWhenStateAuthorityLeaves));
        EditorGUILayout.PropertyField(destroyWhenStateAuthLeaves);

        var allowStateAuthorityOverride = serializedObject.FindProperty(nameof(NetworkObject.AllowStateAuthorityOverride));
        EditorGUILayout.PropertyField(allowStateAuthorityOverride);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Interest Management Settings", EditorStyles.boldLabel);

        if (ecIsEnabled) {

          var objectInterest = serializedObject.FindProperty(nameof(NetworkObject.ObjectInterest));
          EditorGUILayout.PropertyField(objectInterest);

          if (objectInterest.intValue == (int)NetworkObject.ObjectInterestModes.AreaOfInterest) {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetworkObject.AoiPositionSource)));
          }

          using (new EditorGUI.IndentLevelScope()) {
            EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.DefaultInterestGroups)));
          }
        } else {
          BehaviourEditorUtils.DrawWarnBox($"<b>Interest Management</b> requires <b>Eventual Consistency</b> to be enabled in <b>{nameof(NetworkProjectConfig)}</b>.", MessageType.Info);
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }

      EditorGUILayout.Space();
      EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
      EditorGUILayout.Space();

      guidProperty.isExpanded = EditorGUILayout.Foldout(guidProperty.isExpanded, "Baked Data", EditorStyles.foldoutHeader);
      if (guidProperty.isExpanded) {

        using (new EditorGUILayout.VerticalScope(new GUIStyle(FusionGUIStyles.GetStyle(FusionGUIStyles.GroupBoxType.Outline)))) {
          using (new EditorGUI.DisabledScope(true)) {
            using (new EditorGUI.IndentLevelScope()) {
              using (new FusionEditorGUI.ShowMixedValueScope(flagsProperty.hasMultipleDifferentValues)) {
                FusionEditorGUI.LayoutSelectableLabel(EditorGUIUtility.TrTextContent(nameof(obj.Flags)), obj.Flags.ToString());
              }
              EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.NetworkGuid)));
              using (new EditorGUI.IndentLevelScope()) {

                EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.NestedObjects)));
                EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.SimulationBehaviours)));
                EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow(nameof(NetworkObject.NetworkedBehaviours)));
              }
            }
          }
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
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/NetworkObjectPostprocessor.cs

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

  public class NetworkObjectPostprocessor : AssetPostprocessor,
    UnityEditor.Build.IPreprocessBuildWithReport,
    UnityEditor.Build.IProcessSceneWithReport,
    UnityEditor.Build.IPostprocessBuildWithReport {

    static NetworkObjectPostprocessor() {
      EditorSceneManager.sceneSaving += OnSceneSaving;
      EditorApplication.playModeStateChanged += OnPlaymodeChange;
    }

    int IOrderedCallback.callbackOrder => 0;

    private static HashSet<string> _pendingPrefabImports = new HashSet<string>();
    private static string _prefabBeingBaked = null;

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      FusionEditorLog.TraceImport($"Postprocessing imported assets [{importedAssets.Length}]:\n{string.Join("\n", importedAssets)}");

      bool configPossiblyDirty = false;

      foreach (var path in importedAssets) {
        if (!path.EndsWith(".prefab")) {
          continue;
        }

        Debug.Assert(path != _prefabBeingBaked);
        if (!_pendingPrefabImports.Remove(path)) {
          if (AssetDatabaseUtils.HasLabel(path, NetworkProjectConfigImporter.FusionPrefabTag)) {
            FusionEditorLog.TraceImport(path, $"Not marked as pending, but has Fusion label; going to process");
          } else { 
            FusionEditorLog.TraceImport(path, $"Skipping, not being marked as pending by OnPostprocessPrefab and not a Fusion prefab");
            continue;
          }
        }

        _prefabBeingBaked = path;
        try {
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
          if (AssetDatabaseUtils.SetLabel(go, NetworkProjectConfigImporter.FusionPrefabTag, isSpawnable)) {
              configPossiblyDirty = true;
              AssetDatabase.ImportAsset(path);
              FusionEditorLog.TraceImport(path, "Labels dirty, going to reimport the config, too");
          } else if (no) {
            FusionEditorLog.TraceImport(path, "Labels up to date");
          }
        } finally {
          _prefabBeingBaked = null;
        }
      }

      if (configPossiblyDirty) {
        // configs needs to be reimported as well
        var configPath = NetworkProjectConfigUtilities.GetGlobalConfigPath(createIfMissing: false);
        if (!string.IsNullOrEmpty(configPath)) {
          AssetDatabase.ImportAsset(configPath);
        }
      }
    }

    void OnPostprocessPrefab(GameObject prefab) {
      // prefab is already loaded, so this is a good place to discriminate Fusion and non-Fusion prefabs
      if (assetPath == _prefabBeingBaked) {
        FusionEditorLog.TraceImport(assetPath, $"Ignoring OnPostprocessPrefab, being imported");
      } else if (prefab.GetComponent<NetworkObject>() || AssetDatabaseUtils.HasLabel(assetPath, NetworkProjectConfigImporter.FusionPrefabTag)) {
        FusionEditorLog.TraceImport(assetPath, $"Fusion prefab detected, going to bake in OnPostprocessAllAssets");
        _pendingPrefabImports.Add(assetPath);
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
        bool dirty = NetworkObjectEditor.BakeHierarchy(stageGo, guid);
        FusionEditorLog.TraceImport(prefabPath, $"Baking took {sw.Elapsed}, changed: {dirty}");

        if (dirty) {
          root = PrefabUtility.SaveAsPrefabAsset(stageGo, prefabPath);
        }

        return root;
      } finally {
        PrefabUtility.UnloadPrefabContents(stageGo);
      }
    }

    private static Action<object> SetDirty = x => EditorUtility.SetDirty((UnityEngine.Object)x);
    private static Dictionary<NetworkObjectGuid, int> SceneObjectIds = new Dictionary<NetworkObjectGuid, int>();

    private static Func<NetworkObject, NetworkObjectGuid> GuidProvider = obj => {
      var instanceId = obj.GetInstanceID();
      var guid = obj.NetworkGuid;

      if (guid.IsValid) {
        if (SceneObjectIds.TryGetValue(guid, out var otherInstanceId)) {
          if (otherInstanceId == instanceId) {
            // can keep the guid
            return guid;
          } else {
            var otherInstance = EditorUtility.InstanceIDToObject(otherInstanceId);
            if (otherInstance == null || otherInstance == obj) {
              // fine, can reuse
              SceneObjectIds[guid] = instanceId;
              return guid;
            } else {
              // can't reuse
            }
          }
        } else {
          // keep and add to cache
          SceneObjectIds.Add(guid, instanceId);
          return guid;
        }
      }

      // need a new guid
      do {
        guid = Guid.NewGuid();
      } while (SceneObjectIds.ContainsKey(guid));

      SceneObjectIds.Add(guid, instanceId);
      return guid;
    };

    public static void BakeSceneObjects(Scene scene) {
      var sw = System.Diagnostics.Stopwatch.StartNew();

      try {
        foreach (var root in scene.GetRootGameObjects()) {
          NetworkObjectEditor.BakeHierarchy(root, null, SetDirty, guidProvider: GuidProvider);
        }
      } finally {
        FusionEditorLog.TraceImport(scene.path, $"Baking {scene} took: {sw.Elapsed}");
      }
    }

    private static void OnPlaymodeChange(PlayModeStateChange change) {
      if (change != PlayModeStateChange.ExitingEditMode) {
        return;
      }
      for (int i = 0; i < EditorSceneManager.sceneCount; ++i) {
        BakeSceneObjects(EditorSceneManager.GetSceneAt(i));
      }
    }

    private static void OnSceneSaving(Scene scene, string path) {
      BakeSceneObjects(scene);
    }

    [MenuItem("Fusion/Bake Scene Objects")]
    public static void BakeSceneObjects() {
      for (int i = 0; i < SceneManager.sceneCount; ++i) {
        var scene = SceneManager.GetSceneAt(i);
        try {
          BakeSceneObjects(scene);
        } catch (Exception ex) {
          Debug.LogError($"Failed to bake scene {scene}: {ex}");
        }
      }
    }

    void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report) {
      SceneObjectIds.Clear();
    }

    void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report) {
      SceneObjectIds.Clear();
    }

    void IProcessSceneWithReport.OnProcessScene(Scene scene, BuildReport report) {
      if (report == null) {
        return;
      }

      BakeSceneObjects(scene);
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/NetworkProjectConfigAssetEditor.cs

namespace Fusion.Editor {
  using System.Collections.Generic;
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(NetworkProjectConfigAsset))] 
  public class NetworkProjectConfigAssetEditor : UnityEditor.Editor {

    public override void OnInspectorGUI() {
      var config = (NetworkProjectConfigAsset) target;

      EditorGUILayout.HelpBox($"Config format has changed. Click on the button below to convert this config to the new format.", MessageType.Info);

      if (GUILayout.Button("Convert To The New Config Format")) {
        Selection.activeObject = Convert(config, true);
      }

      GUI.enabled = false;
      EditorGUILayout.PropertyField(serializedObject.FindProperty("Config"), true);
      GUI.enabled = true;
    }

    internal static NetworkProjectConfigImporter Convert(NetworkProjectConfigAsset config, bool deferDelete = false) {
#pragma warning disable CS0618 // Type or member is obsolete
      config.Config.AssembliesToWeave = config.AssembliesToWeave;
#pragma warning restore CS0618 // Type or member is obsolete

      var json = EditorJsonUtility.ToJson(config.Config, true);
      var path = AssetDatabase.GetAssetPath(config);
      var newPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + NetworkProjectConfigImporter.Extension);

      File.WriteAllText(newPath, json);
      AssetDatabase.ImportAsset(newPath);

      var importer = NetworkProjectConfigUtilities.GlobalConfigImporter;
      importer.PrefabAssetsContainerPath = config.PrefabAssetsContainerPath;
      EditorUtility.SetDirty(importer);
      AssetDatabase.SaveAssets();

      if (deferDelete) {
        EditorApplication.delayCall += () => AssetDatabase.DeleteAsset(path);
      } else {
        AssetDatabase.DeleteAsset(path);
      }
      return importer;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/NetworkRunnerEditor.cs

namespace Fusion.Editor {
  using System.Linq;
  using UnityEditor;

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

          var localplayerobj = runner.LocalPlayer.IsValid ? runner.GetPlayerObject(runner.LocalPlayer) : null;
          EditorGUILayout.ObjectField("Local PlayerObject", localplayerobj, typeof(NetworkObject), true);

          Label("Is SinglePlayer", runner.IsSinglePlayer);
          Label("Scene Ref", runner.CurrentScene);

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
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/NetworkSceneDebugStartEditor.cs

// file deleted

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Ordering/OrderWindow.cs

namespace Fusion.Editor {

#if UNITY_EDITOR

  using System;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  public class OrderWindow : EditorWindow {

    private const int CLASS_WIDTH = 100;
    private const int COL_WIDTH   = 38;
    private const int SCRL_WIDTH  = 12;

    static bool        _errorSuppression;
    static OrderNode[] _sortedNodes;
    static string[]    _stageNames;
    static string[]    _modeNames;

    Vector2 _scrollPos;

    SimulationModes  _modes  = (SimulationModes)(-1);
    SimulationStages _stages = (SimulationStages)(-1);

    OrderNode _rolloverNode;
    OrderNode _prevRolloverNode;

    [UnityEditor.Callbacks.DidReloadScripts]
    static void Init() {

      // Don't keep running sorter after a null result. There is a error that is already reported to the log.
      if (_errorSuppression)
        return;

      var sorter = new OrderSorter();
      _sortedNodes = sorter.Run();

      if (_sortedNodes == null) {
        _errorSuppression = true;
      }
    }

    static readonly Lazy<GUIContent> _beforeIconRollover = new Lazy<GUIContent>(() => {
      var result = new GUIContent("\u25c4", $"Script contains an [{nameof(OrderBeforeAttribute)}].");
      return result;
    });
    static readonly Lazy<GUIContent> _beforeIconNoRollover = new Lazy<GUIContent>(() => {
      var result = new GUIContent("\u25c4", "");
      return result; 
    });

    static readonly Lazy<GUIContent> _afterIconRollover = new Lazy<GUIContent>(() => {
      var result = new GUIContent("\u25ba", $"Script contains an [{nameof(OrderAfterAttribute)}].");
      return result;
    });
    static readonly Lazy<GUIContent> _afterIconNoRollover = new Lazy<GUIContent>(() => {
      var result = new GUIContent("\u25ba", "");
      return result;
    });


    static readonly Lazy<GUIContent> _simIconRollover = new Lazy<GUIContent>(() => {
      var result = new GUIContent("\u25cf", $"Script contains a [{nameof(SimulationBehaviourAttribute)}].");
      return result;
    });
    static readonly Lazy<GUIContent> _simIconNoRollover = new Lazy<GUIContent>(() => {
      var result = new GUIContent("\u25cb", "");
      return result;
    });

    static readonly Lazy<GUIStyle> _rowStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle("Label") { padding = new RectOffset(4, 4, 0, 0), margin = new RectOffset(3, 3, 0, 0) };
      return result;
    });

    static readonly Lazy<GUIStyle> _classLabelButtonStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle((GUIStyle)"toolbarButtonLeft") {
        //fixedHeight = 21,
      };
      return result;
    });

    static readonly Lazy<GUIStyle> _classLabelButtonSelectedStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle((GUIStyle)"LargeButtonLeft") {
        //fontSize = 11,
        //stretchWidth = false,
        //alignment = TextAnchor.UpperLeft,
        //padding = new RectOffset(5, 5, 4, 2),
        //fixedHeight = 21,
        //richText = true
      };
      return result;
    });

    static readonly Lazy<GUIStyle> _helpStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(FusionGUIStyles.GroupBoxType.Info.GetStyle());
      result.fontSize = 10;
      return result;
    });

    static readonly Lazy<GUIStyle> _litIndicatorStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_gridLabelStyle.Value);
      result.alignment = TextAnchor.MiddleCenter;
      result.padding = new RectOffset(0, 0, 0, 0);
      result.fontSize = 8;
      return result;
    });

    static readonly Lazy<GUIStyle> _grayIndicatorStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_litIndicatorStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.33f, .33f, .33f) : new Color(.66f, .66f, .66f);
      //result.alignment = TextAnchor.MiddleLeft;
      //result.fontSize = 8;
      return result;
    });


    static readonly Lazy<GUIStyle> _defaultLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.label) {
        fontSize = 11,
        alignment = TextAnchor.UpperLeft,
        padding = new RectOffset(5, 5, 4, 2),
        fixedHeight = 21,
      };
      return result;
    });

    static readonly Lazy<GUIStyle> _sbDerivedLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_defaultLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.80f, 0.9f, 0.80f) : new Color(0, 0, 0);
      return result;
    });

    static readonly Lazy<GUIStyle> _nbDerivedLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_defaultLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.85f, 1.00f) : new Color(0,0,.4f);
      return result;
    });

    static readonly Lazy<GUIStyle> _sbStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_sbDerivedLabelStyle.Value);
      result.fontStyle = FontStyle.Bold;
      return result;
    });

    static readonly Lazy<GUIStyle> _nbStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_nbDerivedLabelStyle.Value);
      result.fontStyle = FontStyle.Bold;
      return result;
    });

    static readonly Lazy<GUIStyle> _flagsGroupStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(EditorStyles.helpBox) {
        border = new RectOffset(0,0,0,0),
        padding = new RectOffset(3, 3, 3, 3),
        margin = new RectOffset(),
        alignment = TextAnchor.MiddleCenter,
        fontSize = 9
      };
      return result;
    });

    static readonly Lazy<GUIStyle> _gridLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle("MiniLabel") {
        alignment = TextAnchor.UpperCenter,
        padding = new RectOffset(0, 0, 4, 0),
        margin = new RectOffset(0, 0, 0, 0),
        fontSize = 9
      };
      return result;
    });

    static readonly Lazy<GUIStyle> _fadedGridLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_gridLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.4f, .4f, .4f) : new Color(.6f, .6f, .6f);
      return result;
    });

    static readonly Lazy<GUIStyle> _selectedLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_defaultLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f) : new Color(.0f, .0f, .0f);
      result.fontStyle = FontStyle.Bold;
      return result;
    });

    static readonly Lazy<GUIStyle> _fadedLabelStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_defaultLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.4f, .4f, .4f) : new Color(.6f, .6f, .6f);
      return result;
    });

    static readonly Lazy<GUIStyle> _beforeStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_defaultLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.8f, .4f, .4f) : new Color(.1f, .1f, .5f);
      result.fontStyle = FontStyle.Bold;
      return result;
    });

    static readonly Lazy<GUIStyle> _afterStyle = new Lazy<GUIStyle>(() => {
      var result = new GUIStyle(_defaultLabelStyle.Value);
      result.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.5f, .5f, .8f) : new Color(.5f, .2f, .0f);
      result.fontStyle = FontStyle.Bold;
      return result;
    });


    static readonly Lazy<GUIStyle> _toolbarButtonStyle = new Lazy<GUIStyle>(()=> {
      var result = new GUIStyle("ToolbarButtonFlat");
      return result;
    });


    [MenuItem("Window/Fusion/Fusion Script Execution Inspector")]
    [MenuItem("Fusion/Windows/Fusion Script Execution Inspector")]
    public static void ShowWindow() {
      var window = GetWindow(typeof(OrderWindow), false, "Fusion Script Execution");
      window.minSize = new Vector2(400, 300);
    }

    void CacheEnumNames() {

      var modevals = Enum.GetValues(typeof(SimulationModes));
      _modeNames = new string[modevals.Length];
      for (int i = 0; i < _modeNames.Length; ++i) {
        _modeNames[i] = ((SimulationModes)(1 << i)).GetDescription();
      }

      var stagevals = Enum.GetValues(typeof(SimulationStages));
      _stageNames = new string[stagevals.Length];
      for (int i = 0; i < _stageNames.Length; ++i) {
        // SimulationStages does not have a 1 flag - workaround with +1
        _stageNames[i] = ((SimulationStages)(1 << (i + 1))).GetDescription();
      }
    }

    void OnGUI() {

      if (_sortedNodes == null)
        Init();

      SerializedObject so = new SerializedObject(this);

      EditorGUILayout.LabelField("Use [OrderBefore], [OrderAfter] and [SimulationBehaviour] attributes on SimulationBehaviours to control order and execution.", _helpStyle.Value);

      EditorGUILayout.BeginVertical(_rowStyle.Value);
      DrawHeader();
      DrawSubHeader();
      EditorGUILayout.EndVertical();

      float headWidth = GUILayoutUtility.GetLastRect().width;

      _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
      
      if (_sortedNodes == null) {
        BehaviourEditorUtils.DrawWarnBox("Conflicts in script OrderBefore and OrderAfter attributes. Check the Unity Debug Log for details on script conflicts.", msgtype: MessageType.Error);
      } else {
        if (_stageNames == null) {
          CacheEnumNames();
        }
        foreach (var node in _sortedNodes) {

          var val = node.SimFlags;

          if (node.Type == typeof(SimulationBehaviour) || node.Type == typeof(NetworkBehaviour) || (val.modes & _modes) != 0 || (val.stages & _stages) != 0) {
            DrawRow(node);
          }
        }
      }
      EditorGUILayout.EndScrollView();
    }

    private void DrawHeader() {
      var style = _gridLabelStyle.Value;
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField("  ", GUILayout.ExpandWidth(true), GUILayout.MinWidth(1), GUILayout.MaxHeight(8));
      EditorGUILayout.LabelField("Modes",  style, GUILayout.Width(COL_WIDTH * Enum.GetValues(typeof(SimulationModes)).Length));
      EditorGUILayout.LabelField(" ",      style, GUILayout.Width(8), GUILayout.MaxHeight(8));
      EditorGUILayout.LabelField("Stages", style, GUILayout.Width(COL_WIDTH * Enum.GetValues(typeof(SimulationStages)).Length));
      EditorGUILayout.LabelField(" ",      style, GUILayout.Width(16), GUILayout.MaxHeight(8));
      EditorGUILayout.EndHorizontal();
    }
    private void DrawSubHeader() {

      EditorGUILayout.BeginHorizontal();

      // Master All/None toggle
      Rect r = EditorGUILayout.GetControlRect(false, GUILayout.MaxWidth(40));
      if (GUI.Button(r, "All", _toolbarButtonStyle.Value)) {
        _modes = (SimulationModes)(-1);
        _stages = (SimulationStages)(-1);
      }
      r = EditorGUILayout.GetControlRect(false, GUILayout.MaxWidth(40));
      if (GUI.Button(r, "None", _toolbarButtonStyle.Value)){
        _modes = 0;
        _stages = 0;
      }

      // Spacing to align the right aligned check boxes
      EditorGUILayout.LabelField(" ", GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)); // GUILayout.Width(rowLabelWidth - 40 - 40));

      foreach (var flag in Enum.GetValues(typeof(SimulationModes))) {
        var m = (SimulationModes)flag;
        var curr = (_modes & m) != 0;
        bool on = EditorGUILayout.Toggle(curr, GUILayout.Width(COL_WIDTH));
        if (on != curr) {
          if (on)
            _modes |= m;
          else
            _modes &= ~m;
        }
      }

      EditorGUILayout.LabelField(" ", _gridLabelStyle.Value, GUILayout.Width(8));

      foreach (var flag in Enum.GetValues(typeof(SimulationStages))) {
        var s = (SimulationStages)flag;
        var curr = (_stages & s) != 0;
        bool on = EditorGUILayout.Toggle(curr, GUILayout.Width(COL_WIDTH));
        if (on != curr) {
          if (on)
            _stages |= s;
          else
            _stages &= ~s;
        }
      }

      EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(OrderNode node) {

      EditorGUILayout.BeginHorizontal(_rowStyle.Value, GUILayout.Width(position.width - 20));

      string name = node.Type.Name;

      var isDefaultModesAndStages = node.SimFlags.modes == OrderNode.ALL_MODES && node.SimFlags.stages == OrderNode.ALL_STAGES;
      bool thisItemIsSelected = _rolloverNode == node;
      bool selectedIsNull = _rolloverNode == null;
      bool isSimulationBehaviour = node.Type == typeof(SimulationBehaviour);
      bool isNetworkBehaviour = node.Type == typeof(NetworkBehaviour);

      var labelstyle =
        (!selectedIsNull && thisItemIsSelected) ? _selectedLabelStyle.Value :
        (!selectedIsNull && _rolloverNode.OrigAfter.Contains(node)) ? _afterStyle.Value :
        (!selectedIsNull && _rolloverNode.OrigBefore.Contains(node)) ? _beforeStyle.Value :
        (!selectedIsNull) ? _fadedLabelStyle.Value :
        isSimulationBehaviour ? _sbStyle.Value :
        isNetworkBehaviour ? _nbStyle.Value :
        typeof(NetworkBehaviour).IsAssignableFrom(node.Type) ? _nbDerivedLabelStyle.Value : _sbDerivedLabelStyle.Value;

      if (isSimulationBehaviour || isNetworkBehaviour) {
        name = $"[ {name} ]";
      }

     GUIStyle back = thisItemIsSelected ? _classLabelButtonSelectedStyle.Value : _classLabelButtonStyle.Value;

      Rect r = EditorGUILayout.GetControlRect(GUILayout.MinWidth(CLASS_WIDTH));

      // Draw backing button for label
      if (!isSimulationBehaviour && !isNetworkBehaviour && GUI.Button(new Rect(r) { xMax = r.xMax + 4 }, GUIContent.none, back)) {
        // Find cs file location:
        var found = AssetDatabase.FindAssets(node.Type.Name);
        if (found.Length > 0) {
          foreach (var f in found) {
            var path = AssetDatabase.GUIDToAssetPath(f);

            // Make sure this file is an exact match, since the search can find subsets
            if (!path.Contains("/" + node.Type.Name + ".cs"))
              continue;

            var obj = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object)) as UnityEngine.Object;

            if (obj != null)
              EditorGUIUtility.PingObject(obj);
          }
        }
        _rolloverNode = !thisItemIsSelected ? node : null;
      }

      // Draw text label
      GUI.Label(r, name, labelstyle);

      // Draw flags
      if (!isSimulationBehaviour && !isNetworkBehaviour) {
        var gridstyle = selectedIsNull || thisItemIsSelected ? _gridLabelStyle.Value : _fadedGridLabelStyle.Value;
        var modes = node.SimFlags.modes;

        using (var h = new EditorGUILayout.HorizontalScope(_flagsGroupStyle.Value, GUILayout.ExpandWidth(false), GUILayout.Width(12 * 3))) {

          EditorGUILayout.LabelField(node.FoundBefores ? _beforeIconRollover.Value : _beforeIconNoRollover.Value, node.FoundBefores ? _litIndicatorStyle.Value : _grayIndicatorStyle.Value, GUILayout.MinWidth(16));
          EditorGUILayout.LabelField(isDefaultModesAndStages ? _simIconNoRollover.Value : _simIconRollover.Value, isDefaultModesAndStages ? _grayIndicatorStyle.Value : _litIndicatorStyle.Value, GUILayout.MinWidth(16));
          EditorGUILayout.LabelField(node.FoundAfters ? _afterIconRollover.Value : _afterIconNoRollover.Value, node.FoundAfters ? _litIndicatorStyle.Value : _grayIndicatorStyle.Value, GUILayout.MinWidth(16));
        }

        using (var h = new EditorGUILayout.HorizontalScope(_flagsGroupStyle.Value, GUILayout.Width(COL_WIDTH * 3))) {

          for (int i = 0; i < _modeNames.Length; ++i) {
            var flag = (SimulationModes)(1 << i);
            bool used = (modes & flag) != 0;
            string lbl = used ? _modeNames[i] : "--";
            EditorGUILayout.LabelField(lbl, gridstyle, GUILayout.Width(COL_WIDTH));
          }
        }

        var stages = node.SimFlags.stages;

        using (var h = new EditorGUILayout.HorizontalScope(_flagsGroupStyle.Value, GUILayout.Width(COL_WIDTH * 2))) {

          // Stages is missing a 1 value (due to removal), so there are only the flags 2 and 4.
          for (int i = 0; i < _stageNames.Length; ++i) {
            var flag = (SimulationStages)(1 << (i + 1));

            bool used = (stages & flag) != 0;
            string lbl = used ? _stageNames[i] : "--";
            EditorGUILayout.LabelField(lbl, gridstyle, GUILayout.Width(COL_WIDTH));
          }
        }
      }

      EditorGUILayout.EndHorizontal();
    }
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/PhotonAppSettingsEditor.cs

namespace Fusion.Editor {
  using System.Collections;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEditor;
  using Photon.Realtime;

  [CustomEditor(typeof(PhotonAppSettings))]
  public class PhotonAppSettingsEditor : Editor {

    public override void OnInspectorGUI() {
      FusionEditorGUI.InjectPropertyDrawers(serializedObject, addForAllFields: true);
      base.DrawDefaultInspector();
    }

    [MenuItem("Fusion/Realtime Settings", priority = 200)]
    public static void PingNetworkProjectConfigAsset() {
      EditorGUIUtility.PingObject(PhotonAppSettings.Instance);
      Selection.activeObject = PhotonAppSettings.Instance;
    }
  }

}



#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/Animator/AnimatorControllerTools.cs

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

    public static AnimatorController GetController(this Animator a) {
      
      RuntimeAnimatorController rac = a.runtimeAnimatorController;
      AnimatorOverrideController overrideController = rac as AnimatorOverrideController;

      /// recurse until no override controller is found
      while (overrideController != null) {
        rac = overrideController.runtimeAnimatorController;
        overrideController = rac as AnimatorOverrideController;
      }

      return rac as AnimatorController;
    }

    public static void GetTriggerNames(this AnimatorController ctr, List<string> namelist) {
      namelist.Clear();

      foreach (var p in ctr.parameters)
        if (p.type == AnimatorControllerParameterType.Trigger) {
          if (namelist.Contains(p.name)) {
            Debug.LogWarning("Identical Trigger Name Found.  Check animator on '" + ctr.name + "' for repeated trigger names.");
          } else
            namelist.Add(p.name);
        }
    }

    public static void GetTriggerNames(this AnimatorController ctr, List<int> hashlist) {
      hashlist.Clear();

      foreach (var p in ctr.parameters)
        if (p.type == AnimatorControllerParameterType.Trigger) {
          hashlist.Add(Animator.StringToHash(p.name));
        }
    }

    /// ------------------------------ STATES --------------------------------------

    public static void GetStatesNames(this AnimatorController ctr, List<string> namelist) {
      namelist.Clear();

      foreach (var l in ctr.layers) {
        var states = l.stateMachine.states;
        ExtractNames(ctr, l.name, states, namelist);

        var substates = l.stateMachine.stateMachines;
        ExtractSubNames(ctr, l.name, substates, namelist);
      }
    }

    public static void ExtractSubNames(AnimatorController ctr, string path, ChildAnimatorStateMachine[] substates, List<string> namelist) {
      foreach (var s in substates) {
        var sm = s.stateMachine;
        var subpath = path + "." + sm.name;

        ExtractNames(ctr, subpath, s.stateMachine.states, namelist);
        ExtractSubNames(ctr, subpath, s.stateMachine.stateMachines, namelist);
      }
    }

    public static void ExtractNames(AnimatorController ctr, string path, ChildAnimatorState[] states, List<string> namelist) {
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

    public static void GetStatesNames(this AnimatorController ctr, List<int> hashlist) {
      hashlist.Clear();

      foreach (var l in ctr.layers) {
        var states = l.stateMachine.states;
        ExtractHashes(ctr, l.name, states, hashlist);

        var substates = l.stateMachine.stateMachines;
        ExtractSubtHashes(ctr, l.name, substates, hashlist);
      }

    }

    public static void ExtractSubtHashes(AnimatorController ctr, string path, ChildAnimatorStateMachine[] substates, List<int> hashlist) {
      foreach (var s in substates) {
        var sm = s.stateMachine;
        var subpath = path + "." + sm.name;

        ExtractHashes(ctr, subpath, sm.states, hashlist);
        ExtractSubtHashes(ctr, subpath, sm.stateMachines, hashlist);
      }
    }

    public static void ExtractHashes(AnimatorController ctr, string path, ChildAnimatorState[] states, List<int> hashlist) {
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
      /// always get new Animator in case it has changed.
      Animator animator = netAnim.Animator;
      if (animator == null) {
        animator = netAnim.GetComponent<Animator>();
        if (animator == null) {
          return default;
        }
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

      /// always get new Animator in case it has changed.
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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/AssetDatabaseUtils.cs

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

  public static class AssetDatabaseUtils {
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

    public static bool HasLabel(string assetPath, string label) {
      var guidStr = AssetDatabase.AssetPathToGUID(assetPath);
      if (!GUID.TryParse(guidStr, out var guid)) {
        return false;
      }

      var labels = AssetDatabase.GetLabels(guid);
      var index = Array.IndexOf(labels, label);
      return index >= 0;
    }

    public static bool HasLabel(UnityEngine.Object obj, string label) {
      var labels = AssetDatabase.GetLabels(obj);
      var index = Array.IndexOf(labels, label);
      return index >= 0;
    }

    public static bool SetLabel(UnityEngine.Object obj, string label, bool present) {
      var labels = AssetDatabase.GetLabels(obj);
      var index = Array.IndexOf(labels, label);
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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/AutoGUIUtilities.cs

// Removed May 22 2021 (Alpha 3)


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/EnumEditorUtilities.cs

namespace Fusion.Editor {
  using System;
  using System.Linq;
  using System.Reflection;

  public static class EnumEditorUtilities {

    public const string SPLIT = " - ";

    /// <summary>
    /// Returns enum description attribute value.
    /// Used for Fusion Editor code internally.
    /// </summary>
    public static string GetDescription(this Enum GenericEnum) {
      Type genericEnumType = GenericEnum.GetType();
      MemberInfo[] memberInfo = genericEnumType.GetMember(GenericEnum.ToString());
      if ((memberInfo != null && memberInfo.Length > 0)) {
        var _Attribs = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
        if ((_Attribs != null && _Attribs.Count() > 0)) {
          return ((System.ComponentModel.DescriptionAttribute)_Attribs.ElementAt(0)).Description;
        }
      }
      return GenericEnum.ToString();
    }

    /// <summary>
    /// Returns long and short enum description attribute values.
    /// Assumes that the long and short text for the Description is separated by with " - ".
    /// Used for Fusion Editor code internally.
    /// </summary>
    public static (string longdesc, string shortdesc) GetDescriptions(this Enum GenericEnum) {
      Type genericEnumType = GenericEnum.GetType();
      MemberInfo[] memberInfo = genericEnumType.GetMember(GenericEnum.ToString());
      if ((memberInfo != null && memberInfo.Length > 0)) {
        var _Attribs = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
        if ((_Attribs != null && _Attribs.Count() > 0)) {
          string fulldesc = ((System.ComponentModel.DescriptionAttribute)_Attribs.ElementAt(0)).Description;
          int split = fulldesc.IndexOf(SPLIT);
          string l = split > 0 ? fulldesc.Remove(split) : fulldesc;
          string s = split > 0 ? fulldesc.Substring(split + SPLIT.Length) : fulldesc;
          return (l, s);
        }
      }
      return (GenericEnum.ToString(), GenericEnum.ToString());
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorGUI.cs

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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorGUI.InlineHelp.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {

    static Dictionary<Type, Dictionary<string, PropertyInlineHelpInfo>> s_codeDocCache = new Dictionary<Type, Dictionary<string, PropertyInlineHelpInfo>>();

    static (object, string) s_expandedHelp = default;

    internal static Rect GetInlineHelpButtonRect(InlineHelpButtonPlacement buttonPlacement, Rect position, GUIContent label, bool expectFoldout = true) {
      float size = InlineHelpStyle.ButtonSize;
      switch (buttonPlacement) {
        case InlineHelpButtonPlacement.BeforeLabel: {
            var buttonRect = new Rect(position.x - size, position.y + ((16 - size) / 2) + 1, size, size);
            using (new EditorGUI.IndentLevelScope(expectFoldout ? -1 : 0)) {
              buttonRect.x = EditorGUI.IndentedRect(buttonRect).x;
              // give indented items a little extra padding - no need for them to be so crammed
              if (buttonRect.x > 8) {
                buttonRect.x -= 2;
              }
            }
            return buttonRect;
          }

        case InlineHelpButtonPlacement.AfterLabel: {
            var labelSize = EditorStyles.label.CalcSize(label);
            var buttonRect = new Rect(position.x + labelSize.x, position.y, size, size);
            buttonRect = EditorGUI.IndentedRect(buttonRect);
            buttonRect.x = Mathf.Min(buttonRect.x + 2, EditorGUIUtility.labelWidth - size);
            buttonRect.width = size;
            return buttonRect;
          }

        case InlineHelpButtonPlacement.BeforeValue:
        default:
          return new Rect(position.x + EditorGUIUtility.labelWidth - size, position.y, size, size);
      }
    }

    internal static bool DrawInlineHelpButton(Rect buttonRect, bool state, bool doButton = true, bool doIcon = true) {
      bool result = false;
      if (doButton) {
        EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
        using (GUI.enabled ? null : new FusionEditorGUI.EnabledScope(true)) {
          result = GUI.Button(buttonRect, state ? InlineHelpStyle.HideInlineContent : InlineHelpStyle.ShowInlineContent, GUIStyle.none);
        }
      }

      if (doIcon) {
        // paint over what the inspector has drawn
        if (Event.current.type == EventType.Repaint) {
          GUI.DrawTexture(buttonRect, state ? FusionGUIStyles.HelpIconSelected : FusionGUIStyles.HelpIconUnselected);
        }
      }

      return result;
    }

    const float SCROLL_WIDTH = 16f;
    const float LEFT_HELP_INDENT = 8f;

    internal static Rect GetInlineHelpRect(GUIContent content) {

      // well... we do this, because there's no way of knowing the indent and scroll bar existence
      // when property height is calculated
      var width = UnityInternal.EditorGUIUtility.contextWidth - /*InlineHelpStyle.MarginOuter -*/ SCROLL_WIDTH - LEFT_HELP_INDENT;

      if (content == null) {
        return default;
      }

      var height = FusionGUIStyles.GetStyle(FusionGUIStyles.GroupBoxType.InlineHelpInner).CalcHeight(content, width);

      return new Rect(InlineHelpStyle.MarginOuter, 0, width, height);
    }

    internal static void DrawInlineHelp(GUIContent help, Rect propertyRect, Rect helpRect) {
      using (new FusionEditorGUI.EnabledScope(true)) {
        if (Event.current.type == EventType.Repaint) {

          int extraPadding = 0;
          // main bar
          var rect = new Rect() {
            xMin = LEFT_HELP_INDENT,
            // This assumes that any style used has equal padding on left and right, and determines the padding/margin from the property xMin
            xMax = propertyRect.xMax + propertyRect.xMin - SCROLL_WIDTH, 
            yMin = propertyRect.yMin,
            yMax = propertyRect.yMax,
          };

          // extra space that needs to be accounted for, like when there's no scrollbar
          extraPadding = Mathf.FloorToInt(Mathf.Max(0, rect.width - (helpRect.width)));

          var orect = new Rect(rect) {
            width = helpRect.width + extraPadding,
            yMin = rect.yMin - 2,
          };

          FusionGUIStyles.GetStyle(FusionGUIStyles.GroupBoxType.InlineHelpOuter).Draw(orect, false, false, false, false);

          if (helpRect.height > 0) {
            Rect irect = new Rect(helpRect) {
              x = rect.x,
              y = propertyRect.yMax - helpRect.height,
            };

            var innerStyle = FusionGUIStyles.GetStyle(FusionGUIStyles.GroupBoxType.InlineHelpInner);
            if (extraPadding > 0.0f) {
              innerStyle.padding.right += extraPadding;
              irect.width += extraPadding;
            }
            try {
              innerStyle.Draw(irect, help, false, false, false, false);
            } finally {
              if (extraPadding > 0.0f) {
                innerStyle.padding.right -= extraPadding;
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// For drawing inline help using cached InlineHelpInfo.
    /// </summary>
    internal static GUIContent DrawInlineHelp(this PropertyInlineHelpInfo help, Rect rect, BehaviourEditor editor) {
      if (help.Summary != null) {
        string path = help.Label.text;
        Rect buttonRect = GetInlineHelpButtonRect(InlineHelpButtonPlacement.BeforeLabel, rect, GUIContent.none, false);
        bool wasExpanded = IsHelpExpanded(editor, path);

        if (wasExpanded) {
          var helpRect = GetInlineHelpRect(help.Summary);
          var r = EditorGUILayout.GetControlRect(false, helpRect.height);
          r.y = rect.y;
          r.height += rect.height;
          DrawInlineHelp(help.Summary, r, helpRect);
        }

        if (DrawInlineHelpButton(buttonRect, wasExpanded, doButton: true, doIcon: false)) {
          SetHelpExpanded(editor, path, !wasExpanded);
        }

        DrawInlineHelpButton(buttonRect, wasExpanded, doButton: false, doIcon: true);
      }
      return help.Label;
    }

    /// <summary>
    /// For drawing inline help manually with custom editors.
    /// </summary>
    internal static GUIContent DrawInlineHelp(this BehaviourEditor editor, Rect rect, Type monoBehaviourType, string memberName) {
      var info = GetInlineHelpInfo(monoBehaviourType, memberName);
      return DrawInlineHelp(info, rect, editor);
    }

    internal static PropertyInlineHelpInfo GetInlineHelpInfo(this Type monoBehaviourType, string memberName ) {
      if (!s_codeDocCache.TryGetValue(monoBehaviourType, out var propertyLookup)) {
        propertyLookup = new Dictionary<string, PropertyInlineHelpInfo>();
        s_codeDocCache.Add(monoBehaviourType, propertyLookup);
      }

      // Try and see if we have an entry for this property for this target object type yet.
      if (propertyLookup.TryGetValue(memberName, out var helpInfo)) {
        return helpInfo;
      }

      // Failed to find existing record, do the heavy lifting of extracting it from the XMLDocumentation
      MemberInfo minfo = monoBehaviourType.GetMemberIncludingBaseTypes(memberName, stopAtType: typeof(Fusion.Behaviour));

      string fieldSummary, tooltipSummary;

      if (minfo == null) {
        fieldSummary = monoBehaviourType.GetXmlDocSummary(false);
        tooltipSummary = monoBehaviourType.GetXmlDocSummary(true);
      } else {

        tooltipSummary = XmlDocumentation.GetXmlDocSummary(minfo, true);
        fieldSummary = string.Join("\n\n", new[] {
          minfo.GetXmlDocSummary(false),
          minfo.MemberType == MemberTypes.Field ? GetFormattedTypeSummary((minfo as FieldInfo).FieldType) :
          minfo.MemberType == MemberTypes.Property ? GetFormattedTypeSummary((minfo as PropertyInfo).PropertyType) :
          null
        }.Where(x => !string.IsNullOrEmpty(x)));
      }

      helpInfo = new PropertyInlineHelpInfo() {
        Label = new GUIContent(ObjectNames.NicifyVariableName(memberName), tooltipSummary),
        Summary = string.IsNullOrEmpty(fieldSummary) ? null : new GUIContent(fieldSummary),
      };

      propertyLookup.Add(memberName, helpInfo);
      return helpInfo;
    }

    public static bool InjectPropertyDrawers(SerializedObject serializedObject, bool addScriptDrawer = true, bool fixArrayHelp = true, bool addForAllFields = false) {

      var rootType = serializedObject.targetObject.GetType();
      var anyInjected = false;

      if (addScriptDrawer) {
        var sp = serializedObject.FindPropertyOrThrow(ScriptPropertyName);
        Debug.Assert(sp.depth == 0 && rootType.IsSubclassOf(typeof(UnityEngine.Object)));
        
        if (TryInjectDrawer(sp, null, () => new InlineHelpAttribute(), () => new InlineHelpAttributeDrawer(), out var injected)) {
          var helpAttribute = rootType.GetCustomAttributes(true).OfType<ScriptHelpAttribute>().SingleOrDefault();
          var scriptDrawer = new ScriptHeaderAttributeDrawer();
          UnityInternal.PropertyDrawer.SetAttribute(scriptDrawer, new ScriptHeaderAttributeDrawer.Attribute() {
            Settings = helpAttribute ?? new ScriptHelpAttribute() 
          });

          AddDrawer(sp, scriptDrawer);
          if (scriptDrawer.attribute.Settings != null) {
            ((InlineHelpAttribute)injected.attribute).ButtonPlacement = InlineHelpButtonPlacement.BeforeLabel;
          }
        }
      }

      if (!fixArrayHelp && !addForAllFields) {
        return anyInjected;
      }


      bool enterChildren = true;
      for (var sp = serializedObject.GetIterator(); sp.NextVisible(enterChildren); enterChildren = sp.isExpanded) {

        bool acceptProperty = false;

        // early out check
        if (sp.IsArrayProperty()) {
          acceptProperty = fixArrayHelp;
        } else if (sp.name == ScriptPropertyName && sp.depth == 0 && rootType.IsSubclassOf(typeof(UnityEngine.Object))) {
          continue;
        } else {
          acceptProperty = addForAllFields;
        }

        if (!acceptProperty) {
          continue;
        }

        var field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(sp, out var type);

        // ignore non-marked fields if attribute not added
        if (!addForAllFields && field.GetCustomAttribute<InlineHelpAttribute>() == null) {
          continue;
        }

        if (TryInjectDrawer(sp, field, () => new InlineHelpAttribute(), () => new InlineHelpAttributeDrawer(), out var drawer)) {
          anyInjected = true;

          if (sp.IsArrayProperty()) {
            var handler = UnityInternal.ScriptAttributeUtility.GetHandler(sp);
            if (handler.decoratorDrawers == null) {
              handler.decoratorDrawers = new List<DecoratorDrawer>();
            }
            var decorator = new ReserveArrayPropertyHeightDecorator();
            handler.decoratorDrawers.Add(decorator);
            drawer.ArrayHeightDecorator = decorator;
          }

        }
      }

      return anyInjected;
    }


    private static void AddDrawer(SerializedProperty property, PropertyDrawer drawer) {
      var handler = UnityInternal.ScriptAttributeUtility.GetHandler(property);

#if UNITY_2021_1_OR_NEWER
      if (handler.m_PropertyDrawers == null) {
        handler.m_PropertyDrawers = new List<PropertyDrawer>();
      }
      InsertPropertyDrawerByAttributeOrder(handler.m_PropertyDrawers, drawer);
#else
      {
        if (drawer is ForwardingPropertyDrawer forwarding) {
          forwarding.InitInjected(handler.m_PropertyDrawer);
        }
      }

      if (handler.m_PropertyDrawer is ForwardingPropertyDrawer mainDrawer) {
        mainDrawer.AddDrawer(drawer);
      } else if (handler.m_PropertyDrawer == null || drawer is ForwardingPropertyDrawer forwarding) {
        handler.m_PropertyDrawer = drawer;
      } else {
        throw new NotSupportedException();
      }
#endif
    }

    private static bool TryInjectDrawer<DrawerType>(SerializedProperty property, FieldInfo field, Func<PropertyAttribute> attributeFactory, Func<DrawerType> drawerFactory, out DrawerType drawer) 
      where DrawerType : PropertyDrawer { 

      drawer = null;

      var handler = UnityInternal.ScriptAttributeUtility.GetHandler(property);


#if UNITY_2021_1_OR_NEWER
      if (HasPropertyDrawer<DrawerType>(handler.m_PropertyDrawers)) {
        return false;
      }
#else
      if (handler.m_PropertyDrawer is DrawerType) {
        return false;
      }
      if (handler.m_PropertyDrawer is ForwardingPropertyDrawer multiDrawer && HasPropertyDrawer<DrawerType>(multiDrawer.PropertyDrawers)) {
        return false;
      }
#endif

      if (handler.Equals(UnityInternal.ScriptAttributeUtility.sharedNullHandler)) {
        // need to add one?
        handler = UnityInternal.PropertyHandler.New();
        UnityInternal.ScriptAttributeUtility.propertyHandlerCache.SetHandler(property, handler);
      }

      var attribute = attributeFactory();

      drawer = drawerFactory();
      Debug.Assert(drawer != null);
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

    private static string GetFormattedTypeSummary(Type type) {
      var summary = XmlDocumentation.GetXmlDocSummary(type, false);
      if (string.IsNullOrEmpty(summary)) {
        return summary;
      }
      return $"<b>[{type.Name}]</b> {summary}";
    }

    internal static class NetworkedPropertyStyle {
      public static LazyAuto<Texture2D> NetworkPropertyIcon = LazyAuto.Create(() => {
        return Resources.Load<Texture2D>("icons/networked-property-icon");
      });
    }

    internal static class InlineHelpStyle {
      public const float ButtonSize = 16.0f;
      public const float MarginOuter = ButtonSize;
      public static GUIContent HideInlineContent = new GUIContent("", "Hide");
      public static GUIContent ShowInlineContent = new GUIContent("", "");
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

    // Cached help info
    internal class PropertyInlineHelpInfo {
      public GUIContent Summary;
      public GUIContent Label;
    }

    // Draw label with specified XML Help member
    static GUIContent reusableGC = new GUIContent();
    public static void DrawLabelForProperty(this BehaviourEditor editor, string membername, string value, Type memberContainerType = null, int? height = null) {
      if (memberContainerType == null) {
        memberContainerType = editor.serializedObject.GetType();
      }
      var rect = height.HasValue ? EditorGUILayout.GetControlRect(false, height.Value) : EditorGUILayout.GetControlRect();
      reusableGC.text = value;
      EditorGUI.LabelField(rect, editor.DrawInlineHelp(rect, memberContainerType, membername), reusableGC);
    }

    // Draw readonly toggle with specified XML Help member
    public static bool DrawToggleForProperty(this BehaviourEditor editor, string membername, bool value, Type memberContainerType = null, int? height = null) {
      if (memberContainerType == null) {
        memberContainerType = editor.serializedObject.GetType();
      }
      var rect = height.HasValue ? EditorGUILayout.GetControlRect(false, height.Value) : EditorGUILayout.GetControlRect();
      return EditorGUI.Toggle(rect, editor.DrawInlineHelp(rect, memberContainerType, membername), value);
    }

    private static bool HasPropertyDrawer<T>(IEnumerable<PropertyDrawer> orderedDrawers) where T : PropertyDrawer {
      return orderedDrawers?.Any(x => x is T) ?? false;
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



    private class PropertyDrawerOrderComparer : IComparer<PropertyDrawer> {
      public static readonly PropertyDrawerOrderComparer Instance = new PropertyDrawerOrderComparer();

      public int Compare(PropertyDrawer x, PropertyDrawer y) {
        var ox = x.attribute?.order ?? int.MaxValue;
        var oy = y.attribute?.order ?? int.MaxValue;
        return ox - oy;
      }
    }
  }
}



#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorGUI.Odin.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;

#if ODIN_INSPECTOR && !FUSION_ODIN_DISABLED
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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorGUI.Scopes.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {

    public sealed class EnabledScope : GUI.Scope {
      private readonly bool value;

      public EnabledScope(bool enabled) {
        value = GUI.enabled;
        GUI.enabled = enabled;
      }

      protected override void CloseScope() {
        GUI.enabled = value;
      }
    }

    public sealed class BackgroundColorScope : GUI.Scope {
      private readonly Color value;

      public BackgroundColorScope(Color color) {
        value = GUI.backgroundColor;
        GUI.backgroundColor = color;
      }

      protected override void CloseScope() {
        GUI.backgroundColor = value;
      }
    }

    public sealed class ColorScope : GUI.Scope {
      private readonly Color value;

      public ColorScope(Color color) {
        value = GUI.color;
        GUI.color = color;
      }

      protected override void CloseScope() {
        GUI.color = value;
      }
    }

    public sealed class ContentColorScope : GUI.Scope {
      private readonly Color value;

      public ContentColorScope(Color color) {
        value = GUI.contentColor;
        GUI.contentColor = color;
      }

      protected override void CloseScope() {
        GUI.contentColor = value;
      }
    }

    public sealed class FieldWidthScope : GUI.Scope {
      private float value;

      public FieldWidthScope(float fieldWidth) {
        value = EditorGUIUtility.fieldWidth;
        EditorGUIUtility.fieldWidth = fieldWidth;
      }

      protected override void CloseScope() {
        EditorGUIUtility.fieldWidth = value;
      }
    }

    public sealed class HierarchyModeScope : GUI.Scope {
      private bool value;

      public HierarchyModeScope(bool value) {
        this.value = EditorGUIUtility.hierarchyMode;
        EditorGUIUtility.hierarchyMode = value;
      }

      protected override void CloseScope() {
        EditorGUIUtility.hierarchyMode = value;
      }
    }

    public sealed class IndentLevelScope : GUI.Scope {
      private readonly int value;

      public IndentLevelScope(int indentLevel) {
        value = EditorGUI.indentLevel;
        EditorGUI.indentLevel = indentLevel;
      }

      protected override void CloseScope() {
        EditorGUI.indentLevel = value;
      }
    }

    public sealed class LabelWidthScope : GUI.Scope {
      private float value;

      public LabelWidthScope(float labelWidth) {
        value = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = labelWidth;
      }

      protected override void CloseScope() {
        EditorGUIUtility.labelWidth = value;
      }
    }

    public sealed class ShowMixedValueScope : GUI.Scope {
      private bool value;

      public ShowMixedValueScope(bool show) {
        value = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = show;
      }

      protected override void CloseScope() {
        EditorGUI.showMixedValue = value;
      }
    }

    public sealed class PropertyScope : GUI.Scope {

      public PropertyScope(Rect position, GUIContent label, SerializedProperty property) {
        EditorGUI.BeginProperty(position, label, property);
      }

      protected override void CloseScope() {
        EditorGUI.EndProperty();
      }
    }

    public sealed class PropertyScopeWithPrefixLabel : GUI.Scope {
      private int indent;

      public PropertyScopeWithPrefixLabel(Rect position, GUIContent label, SerializedProperty property, out Rect indentedPosition) {
        EditorGUI.BeginProperty(position, label, property);
        indentedPosition = EditorGUI.PrefixLabel(position, label);
        indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
      }

      protected override void CloseScope() {
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
      }
    }

    public static bool BeginBox(string headline = null, int indentLevel = 1, bool? foldout = null) {
      bool result = true;
      GUILayout.BeginVertical(EditorStyles.helpBox);
      if (!string.IsNullOrEmpty(headline)) {
        if (foldout.HasValue) {
          result = EditorGUILayout.Foldout(foldout.Value, headline);
        } else {
          EditorGUILayout.LabelField(headline, EditorStyles.boldLabel);
        }
      }
      EditorGUI.indentLevel += indentLevel;
      return result;
    }

    public static void EndBox(int indentLevel = 1) {
      EditorGUI.indentLevel -= indentLevel;
      GUILayout.EndVertical();
    }

    public sealed class BoxScope : IDisposable {
      private readonly SerializedObject _serializedObject;
      private readonly Color _backgroundColor;
      private readonly int _indentLevel;

      public BoxScope(string headline = null, SerializedObject serializedObject = null, int indentLevel = 1, bool? foldout = null) {
        _indentLevel = indentLevel;
        _serializedObject = serializedObject;

#if !UNITY_2019_3_OR_NEWER
        _backgroundColor = GUI.backgroundColor;
        if (EditorGUIUtility.isProSkin) {
          GUI.backgroundColor = Color.grey;
        }
#endif

        IsFoldout = BeginBox(headline: headline, indentLevel: indentLevel, foldout: foldout);

        if (_serializedObject != null) {
          EditorGUI.BeginChangeCheck();
        }
      }

      public bool IsFoldout { get; private set; }

      public void Dispose() {
        if (_serializedObject != null && EditorGUI.EndChangeCheck()) {
          _serializedObject.ApplyModifiedProperties();
        }

        EndBox(indentLevel: _indentLevel);

#if !UNITY_2019_3_OR_NEWER
        GUI.backgroundColor = _backgroundColor;
#endif
      }
    }

  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorGUI.Thumbnail.cs

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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorGUI.Utils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;

  public static partial class FusionEditorGUI {

    public static readonly GUIContent WhitespaceContent = new GUIContent(" ");

    public const string ScriptPropertyName = "m_Script";

    private const int IconHeight = 14;

    public static Color PrefebOverridenColor => new Color(1f / 255f, 153f / 255f, 235f / 255f, 0.75f);

    public static Rect Decorate(Rect rect, string tooltip, MessageType messageType, bool hasLabel = false, bool drawBorder = true, bool drawButton = true) {

      if (hasLabel) {
        rect.xMin += EditorGUIUtility.labelWidth;
      }

      var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, tooltip, messageType);
      var iconRect = rect;
      iconRect.width = Mathf.Min(16, rect.width);
      iconRect.xMin -= iconRect.width;

      iconRect.y += (iconRect.height - IconHeight) / 2;
      iconRect.height = IconHeight;

      if (drawButton) {
        using (GUI.enabled ? null : new FusionEditorGUI.EnabledScope(true)) {
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
            borderColor = Color.white;
            break;
        }
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, borderColor, 1.0f, 1.0f);
      }

      return iconRect;
    }

    public static void ScriptPropertyField(SerializedObject obj) {
      var scriptProperty = obj.FindProperty(ScriptPropertyName);
      if (scriptProperty != null) {
        using (new EditorGUI.DisabledScope(true)) {
          EditorGUILayout.PropertyField(scriptProperty);
        }
      }
    }

    internal static Rect GetMessageBoxRect(GUIContent content, MessageType messageType) {
      if (content == null) {
        return default;
      }

      // well... we do this, because there's no way of knowing the indent and scroll bar existence
      // when property height is calculated
      var width = UnityInternal.EditorGUIUtility.contextWidth - /*InlineHelpStyle.MarginOuter -*/ SCROLL_WIDTH - LEFT_HELP_INDENT;
      var style = ((FusionGUIStyles.GroupBoxType)messageType).GetStyle();
      var height = style.CalcHeight(content, width);
      return new Rect(InlineHelpStyle.MarginOuter, 0, width, height);
    }

    internal static void DrawMesssageBox(GUIContent content, MessageType messageType, Rect propertyRect, Rect boxRect) {
      using (new FusionEditorGUI.EnabledScope(true)) {
        if (Event.current.type == EventType.Repaint) {

          var style = ((FusionGUIStyles.GroupBoxType)messageType).GetStyle();

          int extraPadding = 0;
          // main bar
          var rect = new Rect() {
            xMin = LEFT_HELP_INDENT,
            // This assumes that any style used has equal padding on left and right, and determines the padding/margin from the property xMin
            xMax = propertyRect.xMax + propertyRect.xMin - SCROLL_WIDTH,
            yMin = propertyRect.yMin,
            yMax = propertyRect.yMax,
          };

          // extra space that needs to be accounted for, like when there's no scrollbar
          extraPadding = Mathf.FloorToInt(Mathf.Max(0, rect.width - (boxRect.width)));

          if (boxRect.height > 0) {
            Rect irect = new Rect(boxRect) {
              x = rect.x,
              y = propertyRect.yMax - boxRect.height,
            };

            if (extraPadding > 0.0f) {
              style.padding.right += extraPadding;
              irect.width += extraPadding;
            }
            try {
              style.Draw(irect, content, false, false, false, false);
            } finally {
              if (extraPadding > 0.0f) {
                style.padding.right -= extraPadding;
              }
            }
          }
        }
      }
    }

  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/FusionEditorLog.cs

namespace Fusion.Editor {
  using UnityEngine;
  using ConditionalAttribute = System.Diagnostics.ConditionalAttribute;

  public static class FusionEditorLog {

    static string LogPrefix;
    static string ImportPrefix;
    static string ConfigPrefix;
    static string InspectorPrefix;
    static string InstallerPrefix;

    static FusionEditorLog() {
      // Color duplicated from FusionUnityLogger - should use a direct reference when ASMDEFs exist
      var c = UnityEditor.EditorGUIUtility.isProSkin ? new Color32(115, 172, 229, 255) : new Color32(20, 64, 120, 255);
      var cs = string.Format("#{0:X6}", (c.r << 16) | (c.g << 8) | c.b);
      LogPrefix       = $"<color={cs}>[Fusion/Editor]</color>";
      ImportPrefix    = $"<color={cs}>[Fusion/Import]</color>";
      ConfigPrefix    = $"<color={cs}>[Fusion/Config]</color>";
      InspectorPrefix = $"<color={cs}>[Fusion/Inspector]</color>";
      InstallerPrefix = $"<color={cs}>[Fusion/Installer]</color>";
    }

    [Conditional("FUSION_EDITOR_TRACE")]
    public static void Trace(string msg) {
      Log(msg);
    }

    public static void Log(string msg) {
      Debug.Log($"{LogPrefix} {msg}");
    }

    internal static void Warn(string msg) {
      Debug.LogWarning($"{LogPrefix} {msg}");
    }

    [Conditional("FUSION_EDITOR_TRACE")]
    public static void TraceConfig(string msg) {
      LogConfig(msg);
    }

    public static void WarnConfig(string msg) {
      Debug.LogWarning($"{ConfigPrefix} {msg}");
    }

    public static void LogConfig(string msg) {
      Debug.Log($"{ConfigPrefix} {msg}");
    }

    public static void ErrorConfig(string msg) {
      Debug.LogError($"{ConfigPrefix} {msg}");
    }


    public static void LogInstaller(string msg) {
      Debug.Log($"{InstallerPrefix} {msg}");
    }

    [Conditional("FUSION_EDITOR_TRACE")]
    public static void TraceImport(string assetPath, string msg) {
      Debug.Log($"{ImportPrefix} {assetPath}: {msg}");
    }

    [Conditional("FUSION_EDITOR_TRACE")]
    public static void TraceImport(string msg) {
      Debug.Log($"{ImportPrefix} {msg}");
    }

    public static void ErrorImport(string msg) {
      Debug.LogError($"{ImportPrefix} {msg}");
    }

    public static void ErrorImport(string assetPath, string msg) {
      Debug.LogError($"{ImportPrefix} {assetPath}: {msg}");
    }


    internal static void WarnImport(string msg) {
      Debug.LogWarning($"{ImportPrefix} {msg}");
    }

    [Conditional("FUSION_EDITOR_TRACE")]
    public static void TraceInspector(string msg) {
      Debug.Log($"{InspectorPrefix} {msg}");
    }

    public static void ErrorInspector(string msg) {
      Debug.LogError($"{InspectorPrefix} {msg}");
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/LogSlider.cs

namespace Fusion.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  public class CustomSliders {

    public static float Log10Slider(Rect r, float value, GUIContent label, float min, float max, float zero, int places, float extraSpace = 0) {

      const int VAL_WIDTH = 58;
      const int SPACER = 4;
      const float MIN_SLIDER_WIDTH = 64;

      float logmin = (float)Math.Log10(min);
      float logmax = (float)Math.Log10(max);
      float logzro = (float)Math.Log10(zero);
      float logval = (float)Math.Log10(value < zero ? min : value);

      float logres;
      Rect sliderect;
      float labelWidth;

      bool showSlider = false;

      if (label == null) {
        labelWidth = 0;
        sliderect = new Rect(r) { xMin = r.xMin + extraSpace,  xMax = r.xMax - VAL_WIDTH - SPACER };
        showSlider = sliderect.width > MIN_SLIDER_WIDTH;
        // convert to log10 linear just for slider. Then convert result back.
        logres = showSlider ? GUI.HorizontalSlider(sliderect, logval, logmax, logmin) : logval;
      }
      else {
        labelWidth = EditorGUIUtility.labelWidth;
        Rect labelrect = new Rect(r) { width = labelWidth };
        sliderect = new Rect(r) { xMin = r.xMin + SPACER + EditorGUIUtility.labelWidth + extraSpace, xMax = r.xMax - VAL_WIDTH };
        showSlider = sliderect.width > MIN_SLIDER_WIDTH;

        GUI.Label(labelrect, label);
        // convert to log10 linear just for slider. Then convert result back.
        logres = showSlider ? GUI.HorizontalSlider(sliderect, logval, logmax, logmin) : logval;
      }

      // If slider moved, return the new value.
      if (showSlider && logres != logval) {

        float newval = (float)Math.Pow(10, logres);

        if (newval < zero) {
          return 0;
        }

        float rounded = RoundAndClamp(newval, min, max, places);
        return rounded;
      }

      float fieldXMin = showSlider ? r.xMax /*+ SPACER */+ SPACER - VAL_WIDTH : r.xMin + labelWidth + /*SPACER +*/ extraSpace;
      Rect valuerect = new Rect(r) { xMin = fieldXMin };
      var holdindent =  EditorGUI.indentLevel;
      EditorGUI.indentLevel = 0;
      var val = EditorGUI.FloatField(valuerect, value < zero ? 0 : value);
      EditorGUI.indentLevel = holdindent;
      return val;
    }

    public static float RoundAndClamp(float val, float min, float max, int places) {
      // If places were not defined to increment by, use Log to round slider movements to nearest single value.
      float rounded;

      places = (int)Math.Log10(val) - places;
      if (places > 0)
        places = 0;
      else if (places < -15)
        places = -15;
      rounded = (float)Math.Round(val, -places);

      // Then clamp to ensure slider end values are perfect.
      if (rounded > max)
        rounded = max;
      else if (rounded < min)
        rounded = min;

      return rounded;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/NetworkProjectConfigUtilities.cs

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
  /// Unity handling for post asset processing callback. Checks existence of settings assets every time assets change.
  /// </summary>
  class FusionSettingsPostProcessor : AssetPostprocessor {
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      NetworkProjectConfigUtilities.RetryEnsurePhotonAppSettingsExists();
    }
  }

  /// <summary>
  /// Editor utilities for creating and managing the <see cref="NetworkProjectConfigAsset"/> singleton.
  /// </summary>
  [InitializeOnLoad]
  public static class NetworkProjectConfigUtilities {

    public const string CONFIG_RESOURCE_FOLDER_GUID = "65cf5f43e8c20f941b0bb130b392ec89";
    public const string FALLBACK_CONFIG_FOLDER_PATH = "Assets/Photon/Fusion/Resources";

    // Constructor runs on project load, allows for startup check for existence of NPC asset.
    static NetworkProjectConfigUtilities() {
      EnsureAssetExists();
      EditorApplication.playModeStateChanged += (change) => {
        if (change == PlayModeStateChange.EnteredEditMode) {
          NetworkProjectConfig.UnloadGlobal();
        }
      };
    }

    /// <summary>
    /// Attempts enforce existence of singleton. If Editor is not ready, this method will be deferred one editor update and try again until it succeeds.
    /// </summary>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void EnsureAssetExists() {
      RetryEnsureProjectConfigConverted();
      RetryEnsurePhotonAppSettingsExists();
    }

    internal static void RetryEnsureProjectConfigConverted() {
      // Keep deferring this check until Unity is ready to deal with asset find/create.
      if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
        EditorApplication.delayCall += RetryEnsureProjectConfigConverted;
        return;
      }
      EditorApplication.delayCall += EnsureProjectConfigConverted;
    }

    internal static void RetryEnsurePhotonAppSettingsExists() {
      PhotonAppSettings photonSettings = PhotonAppSettings.Instance;

      if (photonSettings)
        return;

      // Keep deferring this check until Unity is ready to deal with asset find/create.
      if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
        EditorApplication.delayCall += RetryEnsurePhotonAppSettingsExists;
        return;
      }
      EditorApplication.delayCall += EnsurePhotonAppSettingsAssetExists;
    }

    static void EnsurePhotonAppSettingsAssetExists() {
      GetOrCreatePhotonAppSettingsAsset();
    }

    static void EnsureProjectConfigConverted() {

      var legacyConfigs = AssetDatabase.FindAssets($"t:{nameof(NetworkProjectConfigAsset)}")
          .Select(AssetDatabase.GUIDToAssetPath)
          .Where(x => Path.GetExtension(x) == ".asset");

      foreach (var legacyConfigPath in legacyConfigs) {
        var legacyConfig = AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>(legacyConfigPath);
        try {
          var importer = NetworkProjectConfigAssetEditor.Convert(legacyConfig);
          Debug.Log($"Converted legacy Fusion config {legacyConfigPath} to {importer.assetPath}");
        } catch (Exception ex) {
          Debug.LogError($"Failed to convert legacy Fusion config {legacyConfigPath}: {ex}");
        }
      }
    }

    static string EnsureConfigFolderExists() {

      string folder = null;
      var markerResource = AssetDatabase.GUIDToAssetPath(CONFIG_RESOURCE_FOLDER_GUID);
      if (markerResource != null && markerResource != "") {
        folder = System.IO.Path.GetDirectoryName(markerResource);
        if (folder != null && folder != "") {
          return folder;
        }
      }

      // Unity requires folders to be built one folder at a time, since parent folder must first exist.
      folder = FALLBACK_CONFIG_FOLDER_PATH;
      if (AssetDatabase.IsValidFolder(folder))
        return folder;

      string parent = "";
      string[] split = folder.Split('\\', '/');
      foreach(var f in split) {
        // First folder split should be "Assets", which always exists.
        if (f == "Assets" && parent == "") {
          parent = "Assets";
          continue;
        }
        if (AssetDatabase.IsValidFolder(parent + "/" + f) == false) {
          AssetDatabase.CreateFolder(parent, f);
        }
        parent = parent + '/' + f;
      }
      return folder;
    }

    /// <summary>
    /// Gets the <see cref="PhotonAppSettings"/> singleton. If none was found, attempts to create one.
    /// </summary>
    /// <returns></returns>
    public static PhotonAppSettings GetOrCreatePhotonAppSettingsAsset() {
      PhotonAppSettings photonConfig;

      photonConfig = PhotonAppSettings.Instance;

      if (photonConfig != null)
        return photonConfig;

      // If trying to get instance returned null - create a new asset.
      Debug.Log($"{nameof(PhotonAppSettings)} not found. Creating one now.");


      photonConfig = ScriptableObject.CreateInstance<PhotonAppSettings>();
      string folder = EnsureConfigFolderExists();
      AssetDatabase.CreateAsset(photonConfig, folder + "/" + PhotonAppSettings.ExpectedAssetName);
      PhotonAppSettings.Instance = photonConfig;
      EditorUtility.SetDirty(photonConfig);
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();


#if FUSION_WEAVER
      // QOL: Open the Fusion Hub window, as there will be no App Id yet.
      if (photonConfig.AppSettings.AppIdFusion == null)
        FusionHubWindow.Open();
#endif

      return PhotonAppSettings.Instance;
    }



    [MenuItem("Fusion/Network Project Config", priority = 200)]
    static void PingNetworkProjectConfigAsset() {
      NetworkProjectConfigUtilities.PingGlobalConfigAsset(true);
    }


    [Obsolete("Use " + nameof(RebuildPrefabTable) + " instead")]
    public static void RebuildObjectTable() => RebuildPrefabTable();

    [MenuItem("Fusion/Rebuild Prefab Table", priority = 100)]
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
      SaveGlobalConfig();
    }

    public static string SaveGlobalConfig() {
      return SaveGlobalConfig(NetworkProjectConfig.Global ?? new NetworkProjectConfig());
    }

    public static string SaveGlobalConfig(NetworkProjectConfig config) {
      if (config == null) {
        throw new ArgumentNullException(nameof(config));
      }

      var json = EditorJsonUtility.ToJson(config, true);

      string path = GetGlobalConfigPath();
      string existingJson = File.ReadAllText(path);
      
      if (!string.Equals(json, existingJson)) {
        AssetDatabase.MakeEditable(path);
        File.WriteAllText(path, json);
      }

      AssetDatabase.ImportAsset(path);
      return PathUtils.MakeSane(path);
    }

    public static void PingGlobalConfigAsset(bool select = false) {
      var config = AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>(GetGlobalConfigPath());
      if (config != null) {
        EditorGUIUtility.PingObject(config);
        if (select) {
          Selection.activeObject = config;
        }
      }
    }

    public static NetworkProjectConfigImporter GlobalConfigImporter {
      get {
        return (NetworkProjectConfigImporter)AssetImporter.GetAtPath(GetGlobalConfigPath());
      }
    }

    public static bool TryGetPrefabAsset(NetworkObjectGuid guid, out NetworkPrefabAsset prefabAsset) {
      prefabAsset = AssetDatabase.LoadAllAssetsAtPath(GetGlobalConfigPath())
        .OfType<NetworkPrefabAsset>()
        .FirstOrDefault(x => x.AssetGuid == guid);
      return prefabAsset;
    }

    public static bool TryGetPrefabSource<T>(NetworkObjectGuid guid, out T source) where T : class, INetworkPrefabSource {
      if (NetworkProjectConfig.Global.PrefabTable.TryGetPrefabEntry(guid, out var iprefab) && iprefab is T asset) {
        source = asset;
        return true;
      }
      source = null;
      return false;
    }

    public static bool TryResolvePrefab(NetworkObjectGuid guid, out NetworkObject prefab) {
      if (TryGetPrefabSource(guid, out NetworkPrefabSourceUnityBase source)) {
        try {
          prefab = NetworkPrefabSourceFactory.ResolveOrThrow(source);
          return true;
        } catch (Exception ex) {
          FusionEditorLog.Trace(ex.ToString());
        }
      }

      prefab = null;
      return false;
    }

    internal static string GetGlobalConfigPath(bool createIfMissing = true) {
      var candidates = AssetDatabase.FindAssets($"glob:\"*{NetworkProjectConfigImporter.Extension}\"")
        .Select(AssetDatabase.GUIDToAssetPath)
        .ToArray();

      if (candidates.Length == 0) {
        // try with a regular file api, as maybe the file has not been imported yet
        candidates = Directory.GetFiles("Assets/", $"*{NetworkProjectConfigImporter.Extension}", SearchOption.AllDirectories);

        if (candidates.Length > 0) {
          FusionEditorLog.WarnConfig($"AssetDatabase did not find any config, but a raw glob found these:\n{string.Join("\n", candidates)}");
          for (int i = 0; i < candidates.Length; ++i) {
            candidates[i] = PathUtils.MakeSane(candidates[i]);
          }
        }
      }

      if (candidates.Length == 0) {
        if (createIfMissing) {

          var defaultPath = Path.Combine(EnsureConfigFolderExists() , NetworkProjectConfig.DefaultResourceName + NetworkProjectConfigImporter.Extension);

          if (AssetDatabase.IsAssetImportWorkerProcess()) {
            FusionEditorLog.WarnConfig($"Creating a new config at {defaultPath}, but an import is already taking place. " +
              $"AssetDatabase will \"see\" the config after the current import is over.");
          } else {
            FusionEditorLog.LogConfig($"Creating new config at {defaultPath}");
          }

          var json = EditorJsonUtility.ToJson(CreateDefaultConfig());
          File.WriteAllText(defaultPath, json);          
          AssetDatabase.ImportAsset(defaultPath);

          return defaultPath;
        } else {
          return string.Empty;
        }
      }
      if (candidates.Length > 1) {
        FusionEditorLog.WarnConfig($"There are multiple configs, choosing the first one: {(string.Join("\n", candidates))}");
      }
      return candidates[0];
    }

    // invoked by reflection, don't remove
    private static NetworkProjectConfigAsset EditTimeLoadGlobalConfigWrapper() {
      var path = GetGlobalConfigPath();

      FusionEditorLog.TraceConfig($"Loading Global config from {path}");

      var config = NetworkProjectConfigImporter.LoadConfigFromFile(path);
      var wrapper = AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>(path);

      if (!wrapper) {
        // well, try reimporting?
        FusionEditorLog.TraceConfig($"Failed to load config at first attempt, reimporting and trying again.");
        AssetDatabase.ImportAsset(path);
        wrapper = AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>(path);
      }

      if (!wrapper) {
        if (AssetDatabase.IsAssetImportWorkerProcess()) {
          FusionEditorLog.WarnConfig($"Config created/dirty during import, this is not supported");
        } else {
          FusionEditorLog.WarnConfig($"Failed to load config with regular AssetDatabase, " +
            $"prefab assets disabled until next reimport.");
        }

        wrapper = ScriptableObject.CreateInstance<NetworkProjectConfigAsset>();
      }

      // overwrite imported config with raw one, in case there's an import lagging behind
      wrapper.Config = config;
      return wrapper;
    }

    private static NetworkProjectConfig CreateDefaultConfig() {
      return new NetworkProjectConfig() {
      };
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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/NetworkRunnerUtilities.cs

namespace Fusion.Editor {

  using System.Collections.Generic;
  using UnityEngine;
  using UnityEditor;
  using Fusion;

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


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/PathUtils.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;

  public static class PathUtils {
    public static bool MakeRelativeToFolder(String path, String folder, out String result) {
      result = String.Empty;
      var formattedPath = MakeSane(path);
      if (formattedPath.Equals(folder, StringComparison.Ordinal) ||
          formattedPath.EndsWith("/" + folder)) {
        return true;
      }
      var index = formattedPath.IndexOf(folder + "/", StringComparison.Ordinal);
      var size = folder.Length + 1;
      if (index >= 0 && formattedPath.Length >= size) {
        result = formattedPath.Substring(index + size, formattedPath.Length - index - size);
        return true;
      }
      return false;
    }

    public static string MakeSane(String path) {
      return path.Replace("\\", "/").Replace("//", "/").TrimEnd('\\', '/').TrimStart('\\', '/');
    }

    public static string GetPathWithoutExtension(string path) {
      if (path == null)
        return (string)null;
      int length;
      if ((length = path.LastIndexOf('.')) == -1)
        return path;
      return path.Substring(0, length);
    }

  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/ReflectionUtils.cs

namespace Fusion.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using System.Reflection;

  public static class ReflectionUtils {
    public const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static Type GetUnityLeafType(this Type type) {
      if (type.HasElementType) {
        type = type.GetElementType();
      } else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
        type = type.GetGenericArguments()[0];
      }
      return type;
    }

#if UNITY_EDITOR

    public static T CreateEditorMethodDelegate<T>(string editorAssemblyTypeName, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      return CreateMethodDelegate<T>(typeof(UnityEditor.Editor).Assembly, editorAssemblyTypeName, methodName, flags);
    }

    public static Delegate CreateEditorMethodDelegate(string editorAssemblyTypeName, string methodName, BindingFlags flags, Type delegateType) {
      return CreateMethodDelegate(typeof(UnityEditor.Editor).Assembly, editorAssemblyTypeName, methodName, flags, delegateType);
    }

#endif

    public static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      try {
        return CreateMethodDelegateInternal<T>(type, methodName, flags);
      } catch (System.Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(type.Assembly, type.FullName, methodName, flags), ex);
      }
    }

    public static Delegate CreateMethodDelegate(this Type type, string methodName, BindingFlags flags, Type delegateType) {
      try {
        return CreateMethodDelegateInternal(type, methodName, flags, delegateType);
      } catch (System.Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage(type.Assembly, type.FullName, methodName, flags, delegateType), ex);
      }
    }

    public static T CreateMethodDelegate<T>(Assembly assembly, string typeName, string methodName, BindingFlags flags = DefaultBindingFlags) where T : Delegate {
      try {
        var type = assembly.GetType(typeName, true);
        return CreateMethodDelegateInternal<T>(type, methodName, flags);
      } catch (System.Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage<T>(assembly, typeName, methodName, flags), ex);
      }
    }

    public static Delegate CreateMethodDelegate(Assembly assembly, string typeName, string methodName, BindingFlags flags, Type delegateType) {
      try {
        var type = assembly.GetType(typeName, true);
        return CreateMethodDelegateInternal(type, methodName, flags, delegateType);
      } catch (System.Exception ex) {
        throw new InvalidOperationException(CreateMethodExceptionMessage(assembly, typeName, methodName, flags, delegateType), ex);
      }
    }

    public static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags, Type delegateType, params DelegateSwizzle[] fallbackSwizzles) where T : Delegate {
      try {
        MethodInfo method = GetMethodOrThrow(type, methodName, flags, delegateType, fallbackSwizzles, out var swizzle);

        var delegateParameters = typeof(T).GetMethod("Invoke").GetParameters();
        var parameters = new List<ParameterExpression>();

        for (int i = 0; i < delegateParameters.Length; ++i) {
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

        var l = Expression.Lambda(typeof(T), callExpression, parameters);
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
        var parameters = new List<ParameterExpression>();

        for (int i = 0; i < delegateParameters.Length; ++i) {
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

        NewExpression newExpression = Expression.New(constructor, convertedParameters);
        var l = Expression.Lambda(typeof(T), newExpression, parameters);
        var del = l.Compile();
        return (T)del;
      } catch (Exception ex) {
        throw new InvalidOperationException(CreateConstructorExceptionMessage(type.Assembly, type.FullName, flags), ex);
      }
    }

    /// <summary>
    /// Returns the first found member of the given name. Includes private members.
    /// </summary>
    public static MemberInfo GetMemberIncludingBaseTypes(this Type type, string memberName, BindingFlags flags = DefaultBindingFlags, Type stopAtType = null) {
      var members = type.GetMember(memberName, flags);
      if (members.Length > 0)
        return members[0];

      type = type.BaseType;

      // loop as long as we have a parent class to search.
      while (type != null) {

        // No point recursing into the abstracts.
        if (type == stopAtType)
          break;

        members = type.GetMember(memberName, flags);
        if (members.Length > 0)
          return members[0];

        type = type.BaseType;
      }
      return null;
    }

    /// <summary>
    /// Normal reflection GetField() won't find private fields in parents (only will find protected). So this recurses the hard to find privates. 
    /// This is needed since Unity serialization does find inherited privates.
    /// </summary>
    public static FieldInfo GetFieldIncludingBaseTypes(this Type type, string fieldName, BindingFlags flags = DefaultBindingFlags, Type stopAtType = null) {
      var field = type.GetField(fieldName, flags);
      if (field != null)
        return field;

      type = type.BaseType;

      // loop as long as we have a parent class to search.
      while (type != null) {

        // No point recursing into the abstracts.
        if (type == stopAtType)
          break;

        field = type.GetField(fieldName, flags);
        if (field != null)
          return field;

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
      if (field.FieldType != fieldType) {
        throw new InvalidProgramException($"Field {type.FullName}.{fieldName} is of type {field.FieldType}, not expected {fieldType}");
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
      return $"{assembly.FullName}.{typeName}({(string.Join(", ", types.Select(x => x.FullName)))}) with flags: {flags}";
    }

    private static T CreateMethodDelegateInternal<T>(this Type type, string name, BindingFlags flags) where T : Delegate {
      return (T)CreateMethodDelegateInternal(type, name, flags, typeof(T));
    }

    private static Delegate CreateMethodDelegateInternal(this Type type, string name, BindingFlags flags, Type delegateType) {
      MethodInfo method = GetMethodOrThrow(type, name, flags, delegateType);
      return System.Delegate.CreateDelegate(delegateType, null, method);
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
          Type[] swizzled = swizzle.Swizzle(allDelegateParameters);
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
        $", candidates are\n: {(string.Join("\n", constructors.Select(x => x.ToString())))}");
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
          Type[] swizzled = swizzle.Swizzle(allDelegateParameters);
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
        $", candidates are\n: {(string.Join("\n", methods.Select(x => x.ToString())))}");
    }

    public static bool IsArrayOrList(this Type listType) {
      if (listType.IsArray) {
        return true;
      } else if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) {
        return true;
      }
      return false;
    }

    public static Type GetArrayOrListElementType(this Type listType) {
      if (listType.IsArray) {
        return listType.GetElementType();
      } else if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>)) {
        return listType.GetGenericArguments()[0];
      }
      return null;
    }

    public static Type MakeFuncType(params Type[] types) {
      return GetFuncType(types.Length).MakeGenericType(types);
    }

    private static Type GetFuncType(int argumentCount) {
      switch (argumentCount) {
        case 1: return typeof(Func<>);
        case 2: return typeof(Func<,>);
        case 3: return typeof(Func<,,>);
        case 4: return typeof(Func<,,,>);
        case 5: return typeof(Func<,,,,>);
        case 6: return typeof(Func<,,,,,>);
        default: throw new ArgumentOutOfRangeException(nameof(argumentCount));
      }
    }

    public static Type MakeActionType(params Type[] types) {
      if (types.Length == 0) return typeof(Action);
      return GetActionType(types.Length).MakeGenericType(types);
    }

    private static Type GetActionType(int argumentCount) {
      switch (argumentCount) {
        case 1: return typeof(Action<>);
        case 2: return typeof(Action<,>);
        case 3: return typeof(Action<,,>);
        case 4: return typeof(Action<,,,>);
        case 5: return typeof(Action<,,,,>);
        case 6: return typeof(Action<,,,,,>);
        default: throw new ArgumentOutOfRangeException(nameof(argumentCount));
      }
    }

    private static StaticAccessor<T> CreateStaticAccessorInternal<T>(MemberInfo fieldOrProperty) {
      try {
        var valueParameter = Expression.Parameter(typeof(T), "value");
        bool canWrite = true;

        UnaryExpression valueExpression;
        MemberExpression memberExpression;
        if (fieldOrProperty is PropertyInfo property) {
          valueExpression = Expression.Convert(valueParameter, property.PropertyType);
          memberExpression = Expression.Property(null, property);
          canWrite = property.CanWrite;
        } else {
          var field = (FieldInfo)fieldOrProperty;
          valueExpression = Expression.Convert(valueParameter, field.FieldType);
          memberExpression = Expression.Field(null, field);
          canWrite = field.IsInitOnly == false;
        }

        Func<T> getter;
        var getExpression = Expression.Convert(memberExpression, typeof(T));
        var getLambda = Expression.Lambda<Func<T>>(getExpression);
        getter = getLambda.Compile();

        Action<T> setter = null;
        if (canWrite) {
          var setExpression = Expression.Assign(memberExpression, valueExpression);
          var setLambda = Expression.Lambda<Action<T>>(setExpression, valueParameter);
          setter = setLambda.Compile();
        }

        return new StaticAccessor<T>() {
          GetValue = getter,
          SetValue = setter
        };
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to create accessor for {fieldOrProperty.DeclaringType}.{fieldOrProperty.Name}", ex);
      }
    }

    private static InstanceAccessor<T> CreateAccessorInternal<T>(MemberInfo fieldOrProperty) {
      try {
        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var instanceExpression = Expression.Convert(instanceParameter, fieldOrProperty.DeclaringType);

        var valueParameter = Expression.Parameter(typeof(T), "value");
        bool canWrite = true;

        UnaryExpression valueExpression;
        MemberExpression memberExpression;
        if (fieldOrProperty is PropertyInfo property) {
          valueExpression = Expression.Convert(valueParameter, property.PropertyType);
          memberExpression = Expression.Property(instanceExpression, property);
          canWrite = property.CanWrite;
        } else {
          var field = (FieldInfo)fieldOrProperty;
          valueExpression = Expression.Convert(valueParameter, field.FieldType);
          memberExpression = Expression.Field(instanceExpression, field);
          canWrite = field.IsInitOnly == false;
        }

        Func<object, T> getter;

        var getExpression = Expression.Convert(memberExpression, typeof(T));
        var getLambda = Expression.Lambda<Func<object, T>>(getExpression, instanceParameter);
        getter = getLambda.Compile();

        Action<object, T> setter = null;
        if (canWrite) {
          var setExpression = Expression.Assign(memberExpression, valueExpression);
          var setLambda = Expression.Lambda<Action<object, T>>(setExpression, instanceParameter, valueParameter);
          setter = setLambda.Compile();
        }

        return new InstanceAccessor<T>() {
          GetValue = getter,
          SetValue = setter
        };
      } catch (Exception ex) {
        throw new InvalidOperationException($"Failed to create accessor for {fieldOrProperty.DeclaringType}.{fieldOrProperty.Name}", ex);
      }
    }

    public struct InstanceAccessor<TValue> {
      public Func<object, TValue> GetValue;
      public Action<object, TValue> SetValue;
    }

    public struct StaticAccessor<TValue> {
      public Func<TValue> GetValue;
      public Action<TValue> SetValue;
    }

    public class DelegateSwizzle {
      private int[] _args;

      public int Count => _args.Length;

      public DelegateSwizzle(params int[] args) {
        _args = args;
      }

      public T[] Swizzle<T>(T[] inputTypes) {
        T[] result = new T[_args.Length];

        for (int i = 0; i < _args.Length; ++i) {
          result[i] = inputTypes[_args[i]];
        }

        return result;
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/SerializedPropertyUtilities.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Text.RegularExpressions;
  using UnityEditor;
  using UnityEngine;

  public static class SerializedPropertyUtilities {

    public static SerializedProperty FindPropertyOrThrow(this SerializedObject so, string propertyPath) {
      var result = so.FindProperty(propertyPath);
      if (result == null)
        throw new ArgumentOutOfRangeException($"Property not found: {propertyPath}");
      return result;
    }

    public static SerializedProperty FindPropertyRelativeOrThrow(this SerializedProperty sp, string relativePropertyPath) {
      var result = sp.FindPropertyRelative(relativePropertyPath);
      if (result == null)
        throw new ArgumentOutOfRangeException($"Property not found: {relativePropertyPath}");
      return result;
    }


    public static SerializedProperty FindPropertyRelativeToParentOrThrow(this SerializedProperty property, string relativePath) {
      var result = FindPropertyRelativeToParent(property, relativePath);
      if (result == null) {
        throw new ArgumentOutOfRangeException($"Property relative to the parent of \"{property.propertyPath}\" not found: {relativePath}");
      }

      return result;
    }

    static readonly Regex _arrayElementRegex = new Regex(@"\.Array\.data\[\d+\]$", RegexOptions.Compiled);

    static SerializedProperty FindPropertyRelativeToParent(SerializedProperty property, string relativePath) {
      SerializedProperty otherProperty;

      var path = property.propertyPath;

      // array element?
      if (path.EndsWith("]")) {
        var match = _arrayElementRegex.Match(path);
        if (match.Success) {
          path = path.Substring(0, match.Index);
        }
      }

      var lastDotIndex = path.LastIndexOf('.');
      if (lastDotIndex < 0) {
        otherProperty = property.serializedObject.FindProperty(relativePath);
      } else {
        otherProperty = property.serializedObject.FindProperty(path.Substring(0, lastDotIndex) + "." + relativePath);
      }

      return otherProperty;
    }

    /// <summary>
    /// Returns a long representation of a SerializedProperty's value. Bools convert to 0 and 1. UnityEngine.Objects convert to GetInstanceId().
    /// </summary>
    public static double GetValueAsDouble(this SerializedProperty property) {

      return
        property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1 : 0) :
        property.propertyType == SerializedPropertyType.ObjectReference ? (property.objectReferenceValue == null ? 0 : property.objectReferenceValue.GetInstanceID()) :
        property.propertyType == SerializedPropertyType.Float ? (double)property.floatValue :
        property.longValue;
    }

    /// <summary>
    /// Returns the value of a field, property, or method in the form of an object. For non-ref types unboxing will be required.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="name">Name of a Field, Property or parameterless Method member of the target object</param>
    /// <returns></returns>
    [System.Obsolete("Cache this delegate")]
    public static object GetValueFromMember(this UnityEngine.Object obj, string name) {

      if (name == null || name == "")
        return null;

      var type = obj.GetType();

      var members = type.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

      if (members.Length == 0)
        return null;

      foreach (var m in members) {
        switch (m) {
          case FieldInfo finfo: {
              return finfo.GetValue(obj);
            }
          case PropertyInfo pinfo: {
              return pinfo.GetValue(obj);
            }

          case MethodInfo minfo: {
              try {
                return minfo.Invoke(obj, null);
              } catch {
                continue;
              }
            }

          default: {
              break;
            }
        }
      }
      return null;
    }

    /// <summary>
    /// Returns a double representation of a boxed value if possible. Bools convert to 0 and 1. UnityEngine.Objects convert to GetInstanceId(). GetHashCode() value is final fallback.
    /// </summary>
    public static double GetObjectValueAsDouble(this object valueObj) {
      if (valueObj == null)
        return 0;

      var type = valueObj.GetType();

      if (type.IsByRef) {
        var objObj = valueObj as UnityEngine.Object;
        if (objObj != null)
          return objObj.GetInstanceID();

      } else {

        if (type.IsEnum)
          type = type.GetEnumUnderlyingType();

        if (type == typeof(bool))
          return ((bool)valueObj) ? 1 : 0;

        if (type == typeof(int))
          return (int)valueObj;

        if (type == typeof(uint))
          return (uint)valueObj;

        if (type == typeof(float))
          return (float)valueObj;

        if (type == typeof(long))
          return (long)valueObj;

        if (type == typeof(ulong))
          return (long)(ulong)valueObj;

        if (type == typeof(byte))
          return (byte)valueObj;

        if (type == typeof(sbyte))
          return (sbyte)valueObj;

        if (type == typeof(short))
          return (short)valueObj;

        if (type == typeof(ushort))
          return (ushort)valueObj;
      }

      // This is a last resort fallback... probably useless.
      return valueObj.GetHashCode();
    }

    public class SerializedPropertyEqualityComparer : IEqualityComparer<SerializedProperty> {

      public static SerializedPropertyEqualityComparer Instance = new SerializedPropertyEqualityComparer();

      public bool Equals(SerializedProperty x, SerializedProperty y) {
        return SerializedProperty.DataEquals(x, y);
      }

      public int GetHashCode(SerializedProperty p) {

        bool enterChildren;
        bool isFirst = true;
        int hashCode = 0;
        int minDepth = p.depth + 1;

        do {

          enterChildren = false;

          switch (p.propertyType) {
            case SerializedPropertyType.Integer: hashCode          = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue); break;
            case SerializedPropertyType.Boolean: hashCode          = HashCodeUtilities.CombineHashCodes(hashCode, p.boolValue.GetHashCode()); break;
            case SerializedPropertyType.Float: hashCode            = HashCodeUtilities.CombineHashCodes(hashCode, p.floatValue.GetHashCode()); break;
            case SerializedPropertyType.String: hashCode           = HashCodeUtilities.CombineHashCodes(hashCode, p.stringValue.GetHashCode()); break;
            case SerializedPropertyType.Color: hashCode            = HashCodeUtilities.CombineHashCodes(hashCode, p.colorValue.GetHashCode()); break;
            case SerializedPropertyType.ObjectReference: hashCode  = HashCodeUtilities.CombineHashCodes(hashCode, p.objectReferenceInstanceIDValue); break;
            case SerializedPropertyType.LayerMask: hashCode        = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue); break;
            case SerializedPropertyType.Enum: hashCode             = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue); break;
            case SerializedPropertyType.Vector2: hashCode          = HashCodeUtilities.CombineHashCodes(hashCode, p.vector2Value.GetHashCode()); break;
            case SerializedPropertyType.Vector3: hashCode          = HashCodeUtilities.CombineHashCodes(hashCode, p.vector3Value.GetHashCode()); break;
            case SerializedPropertyType.Vector4: hashCode          = HashCodeUtilities.CombineHashCodes(hashCode, p.vector4Value.GetHashCode()); break;
            case SerializedPropertyType.Vector2Int: hashCode       = HashCodeUtilities.CombineHashCodes(hashCode, p.vector2IntValue.GetHashCode()); break;
            case SerializedPropertyType.Vector3Int: hashCode       = HashCodeUtilities.CombineHashCodes(hashCode, p.vector3IntValue.GetHashCode()); break;
            case SerializedPropertyType.Rect: hashCode             = HashCodeUtilities.CombineHashCodes(hashCode, p.rectValue.GetHashCode()); break;
            case SerializedPropertyType.RectInt: hashCode          = HashCodeUtilities.CombineHashCodes(hashCode, p.rectIntValue.GetHashCode()); break;
            case SerializedPropertyType.ArraySize: hashCode        = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue); break;
            case SerializedPropertyType.Character: hashCode        = HashCodeUtilities.CombineHashCodes(hashCode, p.intValue.GetHashCode()); break;
            case SerializedPropertyType.AnimationCurve: hashCode   = HashCodeUtilities.CombineHashCodes(hashCode, p.animationCurveValue.GetHashCode()); break;
            case SerializedPropertyType.Bounds: hashCode           = HashCodeUtilities.CombineHashCodes(hashCode, p.boundsValue.GetHashCode()); break;
            case SerializedPropertyType.BoundsInt: hashCode        = HashCodeUtilities.CombineHashCodes(hashCode, p.boundsIntValue.GetHashCode()); break;
            case SerializedPropertyType.ExposedReference: hashCode = HashCodeUtilities.CombineHashCodes(hashCode, p.exposedReferenceValue.GetHashCode()); break;
            default: {
                enterChildren = true;
                break;
              }
          }

          if (isFirst) {
            if (!enterChildren) {
              // no traverse needed
              return hashCode;
            }

            // since property is going to be traversed, a copy needs to be made
            p = p.Copy();
            isFirst = false;
          }
        } while (p.Next(enterChildren) && p.depth >= minDepth);

        return hashCode;
      }
    }

    /// <summary>
    /// Get the actual object instance that a serialized property belongs to. This can be expensive reflection and string manipulation, so be sure to cache this result.
    /// </summary>
    public static object GetParent(this SerializedProperty sp) {
      var path = sp.propertyPath;
      object obj = sp.serializedObject.targetObject;

      // Shortcut if this looks like its not a child of any kind, and target object is our real object.
      if (path.Contains(".") == false)
        return obj;

      path = path.Replace(".Array.data[", "[");
      var elements = path.Split('.');
      foreach (var element in elements.Take(elements.Length - 1)) {
        if (element.Contains("[")) {
          var elementName = element.Substring(0, element.IndexOf("["));
          var index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
          obj = GetValue(obj, elementName, index);
        } else {
          obj = GetValue(obj, element);
        }
      }
      return obj;
    }

    /// <summary>
    /// Will attempt to find the index of a property drawer item. Returns -1 if it appears to not be an array type.
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public static int GetIndexOfDrawerObject(this SerializedProperty property, bool reportError = true) {
      string path = property.propertyPath;

      int start = path.IndexOf("[") + 1;
      int len = path.IndexOf("]") - start;

      if (len < 1)
        return -1;

      int index = -1;
      if ((len > 0 && Int32.TryParse(path.Substring(path.IndexOf("[") + 1, len), out index)) == false && reportError) {
        UnityEngine.Debug.Log("Attempted to find the index of a non-array serialized property.");
      }
      return index;
    }

    private static object GetValue(object source, string name) {
      if (source == null)
        return null;
      var type = source.GetType();
      var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
      if (f == null) {
        var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p == null)
          return null;
        return p.GetValue(source, null);
      }
      return f.GetValue(source);
    }

    private static object GetValue(object source, string name, int index) {
      var enumerable = GetValue(source, name) as System.Collections.IEnumerable;
      var enm = enumerable.GetEnumerator();
      while (index-- >= 0)
        enm.MoveNext();
      return enm.Current;
    }

    public static void DrawPropertyUsingFusionAttributes(this SerializedProperty property, Rect position, GUIContent label, FieldInfo fieldInfo) {

      var unitAttribute = fieldInfo.GetCustomAttribute<UnitAttribute>();
      if (unitAttribute != null) {
        UnitAttributeDecoratorDrawer.DrawUnitsProperty(position, property, label, unitAttribute);
      } else {
        EditorGUI.PropertyField(position, property, label, property.isExpanded);
      }

    }

    public static bool IsArrayElement(this SerializedProperty sp) {
      return sp.propertyPath.EndsWith("]");
    }

    public static bool IsArrayProperty(this SerializedProperty sp) {
      return sp.isArray && sp.propertyType != SerializedPropertyType.String;
    }

    private static int[] _temp = Array.Empty<int>();

    internal static bool UpdateFixedBuffer(this SerializedProperty sp, Action<int[], int> fill, Action<int[], int> update, bool write,  bool force = false) {
      int count = sp.fixedBufferSize;
      Array.Resize(ref _temp, Math.Max(_temp.Length, count));

      // need to get to the first property... `GetFixedBufferElementAtIndex` is slow and allocs

      var element = sp.Copy();
      element.Next(true); // .Array
      element.Next(true); // .Array.size
      element.Next(true); // .Array.data[0]

      unsafe {
        fixed (int* p = _temp) {
          Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemClear(p, count * sizeof(int));
        }

        fill(_temp, count);

        int i = 0;
        if (!force) {
          // find the first difference
          for (; i < count; ++i, element.Next(true)) {
            Debug.Assert(element.propertyType == SerializedPropertyType.Integer);
            if (element.intValue != _temp[i]) {
              break;
            }
          }
        }

        if (i < count) {
          // update data
          if (write) {
            for (; i < count; ++i, element.Next(true)) {
              element.intValue = _temp[i];
            }
          } else {
            for (; i < count; ++i, element.Next(true)) {
              _temp[i] = element.intValue;
            }
          }

          // update surrogate
          update(_temp, count);
          return true;
        } else {
          return false;
        }
      }
      
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/TransformPath.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Text;
  using UnityEngine;

  public unsafe struct TransformPath : IComparable<TransformPath>, IEquatable<TransformPath> {
    public const int MaxDepth = 10;
    public ushort Depth;
    public fixed ushort Indices[MaxDepth];
    public TransformPath* Next;

    public int CompareTo(TransformPath other) {
      var diff = CompareToDepthUnchecked(&other, Mathf.Min(Depth, other.Depth));
      if (diff != 0) {
        return diff;
      }

      return Depth - other.Depth;
    }

    public bool Equals(TransformPath other) {
      if (Depth != other.Depth) {
        return false;
      }

      return CompareToDepthUnchecked(&other, Depth) == 0;
    }

    public override bool Equals(object obj) {
      return obj is TransformPath other ? Equals(other) : false;
    }

    public override int GetHashCode() {
      int hash = Depth;
      return GetHashCode(hash);
    }

    public bool IsAncestorOf(TransformPath other) {
      if (Depth >= other.Depth) {
        return false;
      }

      return CompareToDepthUnchecked(&other, Depth) == 0;
    }

    public bool IsEqualOrAncestorOf(TransformPath other) {
      if (Depth > other.Depth) {
        return false;
      }

      return CompareToDepthUnchecked(&other, Depth) == 0;
    }

    public override string ToString() {
      var builder = new StringBuilder();
      fixed (ushort* levels = Indices) {
        for (int i = 0; i < Depth && i < MaxDepth; ++i) {
          if (i > 0) {
            builder.Append("/");
          }
          builder.Append(levels[i]);
        }
      }

      if (Depth > MaxDepth) {
        Debug.Assert(Next != null);
        builder.Append("/");
        builder.Append(Next->ToString());
      }

      return builder.ToString();
    }

    private int CompareToDepthUnchecked(TransformPath* other, int depth) {
      fixed (ushort* indices = Indices) {
        for (int i = 0; i < depth && i < MaxDepth; ++i) {
          int diff = (int)indices[i] - (int)other->Indices[i];
          if (diff != 0) {
            return diff;
          }
        }
      }

      if (depth > MaxDepth) {
        Debug.Assert(Next != null);
        Debug.Assert(other->Next != null);
        Next->CompareToDepthUnchecked(other->Next, depth - MaxDepth);
      }

      return 0;
    }
    private int GetHashCode(int hash) {
      fixed (ushort* indices = Indices) {
        for (int i = 0; i < Depth && i < MaxDepth; ++i) {
          hash = hash * 31 + indices[i];
        }
      }

      if (Depth > MaxDepth) {
        Debug.Assert(Next != null);
        hash = Next->GetHashCode(hash);
      }

      return hash;
    }
  }

  public sealed unsafe class TransformPathCache : IDisposable {
    public List<IntPtr> _allocs = new List<IntPtr>();
    public Dictionary<Transform, TransformPath> _cache = new Dictionary<Transform, TransformPath>();
    public List<ushort> _siblingIndexStack = new List<ushort>();

    public TransformPath Create(Transform transform) {
      if (_cache.TryGetValue(transform, out var existing)) {
        return existing;
      }

      _siblingIndexStack.Clear();
      for (var tr = transform; tr != null; tr = tr.parent) {
        _siblingIndexStack.Add(checked((ushort)tr.GetSiblingIndex()));
      }
      _siblingIndexStack.Reverse();

      TransformPath result = new TransformPath() {
        Depth = checked((ushort)_siblingIndexStack.Count)
      };

      TransformPath* current = &result;
      for (int i = 0, j = 0; i < _siblingIndexStack.Count; ++i, ++j) {
        Debug.Assert(j <= TransformPath.MaxDepth, $"{j}");

        if (j >= TransformPath.MaxDepth) {
          Debug.Assert(current->Next == null);
          current->Next = Native.MallocAndClear<TransformPath>();
          current = current->Next;
          current->Depth = checked((ushort)(_siblingIndexStack.Count - i));
          _allocs.Add(new IntPtr(current));
          j = 0;
        }
        current->Indices[j] = _siblingIndexStack[i];
      }

      _cache.Add(transform, result);
      return result;
    }

    public void Dispose() {
      foreach (var ptr in _allocs) {
        Native.Free((void*)ptr);
      }
      _allocs.Clear();
      _cache.Clear();
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Utilities/UnityInternal.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEngine;

  using static Fusion.Editor.ReflectionUtils;
  using InitializeOnLoad = UnityEditor.InitializeOnLoadAttribute;


  public static class UnityInternal {

    [InitializeOnLoad]
    public static class Editor {
      public delegate bool DoDrawDefaultInspectorDelegate(UnityEditor.SerializedObject obj);
      public static readonly DoDrawDefaultInspectorDelegate DoDrawDefaultInspector = typeof(UnityEditor.Editor).CreateMethodDelegate<DoDrawDefaultInspectorDelegate>(nameof(DoDrawDefaultInspector));
    }


    [InitializeOnLoad]
    public static class EditorGUI {
      public delegate Rect MultiFieldPrefixLabelDelegate(Rect totalPosition, int id, GUIContent label, int columns);
      public static readonly MultiFieldPrefixLabelDelegate MultiFieldPrefixLabel = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<MultiFieldPrefixLabelDelegate>("MultiFieldPrefixLabel");

      public delegate string TextFieldInternalDelegate(int id, Rect position, string text, GUIStyle style);
      public static readonly TextFieldInternalDelegate TextFieldInternal = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<TextFieldInternalDelegate>("TextFieldInternal");

      public delegate string DelayedTextFieldInternalDelegate(Rect position, int id, GUIContent label, string value, string allowedLetters, GUIStyle style);
      public static readonly DelayedTextFieldInternalDelegate DelayedTextFieldInternal = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<DelayedTextFieldInternalDelegate>(nameof(DelayedTextFieldInternal));

      private static readonly FieldInfo s_TextFieldHash = typeof(UnityEditor.EditorGUI).GetFieldOrThrow(nameof(s_TextFieldHash));
      public static int TextFieldHash => (int)s_TextFieldHash.GetValue(null);

      private static readonly FieldInfo s_DelayedTextFieldHash = typeof(UnityEditor.EditorGUI).GetFieldOrThrow(nameof(s_DelayedTextFieldHash));
      public static int DelayedTextFieldHash => (int)s_DelayedTextFieldHash.GetValue(null);

      private static readonly StaticAccessor<float> s_indent = typeof(UnityEditor.EditorGUI).CreateStaticPropertyAccessor<float>(nameof(indent));
      internal static float indent => s_indent.GetValue();

      public static readonly Action EndEditingActiveTextField = typeof(UnityEditor.EditorGUI).CreateMethodDelegate<Action>(nameof(EndEditingActiveTextField));
    }

    [InitializeOnLoad]
    public static class DecoratorDrawer {

      static InstanceAccessor<UnityEngine.PropertyAttribute> m_Attribute = typeof(UnityEditor.DecoratorDrawer).CreateFieldAccessor<UnityEngine.PropertyAttribute>(nameof(m_Attribute));

      public static void SetAttribute(UnityEditor.DecoratorDrawer drawer, UnityEngine.PropertyAttribute attribute) {
        m_Attribute.SetValue(drawer, attribute);
      }
    }

    [InitializeOnLoad]
    public static class PropertyDrawer {

      static InstanceAccessor<UnityEngine.PropertyAttribute> m_Attribute = typeof(UnityEditor.PropertyDrawer).CreateFieldAccessor<UnityEngine.PropertyAttribute>(nameof(m_Attribute));
      static InstanceAccessor<FieldInfo> m_FieldInfo = typeof(UnityEditor.PropertyDrawer).CreateFieldAccessor<FieldInfo>(nameof(m_FieldInfo));

      public static void SetAttribute(UnityEditor.PropertyDrawer drawer, UnityEngine.PropertyAttribute attribute) {
        m_Attribute.SetValue(drawer, attribute);
      }
      public static void SetFieldInfo(UnityEditor.PropertyDrawer drawer, FieldInfo fieldInfo) {
        m_FieldInfo.SetValue(drawer, fieldInfo);
      }
    }

    [InitializeOnLoad]
    public static class EditorGUIUtility {
      private static readonly StaticAccessor<int> s_LastControlID = typeof(UnityEditor.EditorGUIUtility).CreateStaticFieldAccessor<int>(nameof(s_LastControlID));
      public static int LastControlID => s_LastControlID.GetValue();

      private static readonly StaticAccessor<float> _contentWidth = typeof(UnityEditor.EditorGUIUtility).CreateStaticPropertyAccessor<float>(nameof(contextWidth));
      public static float contextWidth => _contentWidth.GetValue();
    }

    [InitializeOnLoad]
    public static class ScriptAttributeUtility {
      public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ScriptAttributeUtility", true);

      public delegate FieldInfo GetFieldInfoFromPropertyDelegate(UnityEditor.SerializedProperty property, out Type type);
      public static readonly GetFieldInfoFromPropertyDelegate GetFieldInfoFromProperty =
        CreateEditorMethodDelegate<GetFieldInfoFromPropertyDelegate>(
        "UnityEditor.ScriptAttributeUtility",
        "GetFieldInfoFromProperty",
        BindingFlags.Static | BindingFlags.NonPublic);

      public delegate Type GetDrawerTypeForTypeDelegate(Type type);
      public static readonly GetDrawerTypeForTypeDelegate GetDrawerTypeForType =
        CreateEditorMethodDelegate<GetDrawerTypeForTypeDelegate>(
        "UnityEditor.ScriptAttributeUtility",
        "GetDrawerTypeForType",
        BindingFlags.Static | BindingFlags.NonPublic);

      private delegate object GetHandlerDelegate(UnityEditor.SerializedProperty property);
      private static readonly GetHandlerDelegate _GetHandler = InternalType.CreateMethodDelegate<GetHandlerDelegate>("GetHandler", BindingFlags.NonPublic | BindingFlags.Static,
        MakeFuncType(typeof(UnityEditor.SerializedProperty), PropertyHandler.InternalType)
      );

      public delegate List<UnityEngine.PropertyAttribute> GetFieldAttributesDelegate(FieldInfo field);
      public static readonly GetFieldAttributesDelegate GetFieldAttributes = InternalType.CreateMethodDelegate<GetFieldAttributesDelegate>(nameof(GetFieldAttributes));

      public static PropertyHandler GetHandler(UnityEditor.SerializedProperty property) => PropertyHandler.Wrap(_GetHandler(property));

      private static readonly StaticAccessor<object> _propertyHandlerCache = InternalType.CreateStaticPropertyAccessor(nameof(propertyHandlerCache), PropertyHandlerCache.InternalType);
      public static PropertyHandlerCache propertyHandlerCache => new PropertyHandlerCache { _instance = _propertyHandlerCache.GetValue() };

      private static readonly StaticAccessor<object> s_SharedNullHandler = InternalType.CreateStaticFieldAccessor("s_SharedNullHandler", PropertyHandler.InternalType);
      public static PropertyHandler sharedNullHandler => PropertyHandler.Wrap(s_SharedNullHandler.GetValue());



    }

    public struct PropertyHandlerCache {

      [InitializeOnLoad]
      static class Statics {
        public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyHandlerCache", true);
        public static readonly GetPropertyHashDelegate GetPropertyHash = InternalType.CreateMethodDelegate<GetPropertyHashDelegate>(nameof(GetPropertyHash));

        public static readonly GetHandlerDelegate GetHandler = InternalType.CreateMethodDelegate<GetHandlerDelegate>(nameof(GetHandler), BindingFlags.NonPublic | BindingFlags.Instance,
          MakeFuncType(InternalType, typeof(UnityEditor.SerializedProperty), PropertyHandler.InternalType));

        public static readonly SetHandlerDelegate SetHandler = InternalType.CreateMethodDelegate<SetHandlerDelegate>(nameof(SetHandler), BindingFlags.NonPublic | BindingFlags.Instance,
          MakeActionType(InternalType, typeof(UnityEditor.SerializedProperty), PropertyHandler.InternalType));
      }

      public static Type InternalType => Statics.InternalType;

      public delegate int GetPropertyHashDelegate(UnityEditor.SerializedProperty property);
      public delegate object GetHandlerDelegate(object instance, UnityEditor.SerializedProperty property);
      public delegate void SetHandlerDelegate(object instance, UnityEditor.SerializedProperty property, object handlerInstance);

      public object _instance;

      public PropertyHandler GetHandler(UnityEditor.SerializedProperty property) {
        return new PropertyHandler() { _instance = Statics.GetHandler(_instance, property) };
      }

      public void SetHandler(UnityEditor.SerializedProperty property, PropertyHandler newHandler) {
        Statics.SetHandler(_instance, property, newHandler._instance);
      }
    }

    public struct PropertyHandler : IEquatable<PropertyHandler> {

      [InitializeOnLoad]
      static class Statics {
        public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyHandler", true);
        public static readonly InstanceAccessor<List<UnityEditor.DecoratorDrawer>> m_DecoratorDrawers = InternalType.CreateFieldAccessor<List<UnityEditor.DecoratorDrawer>>(nameof(m_DecoratorDrawers));

#if UNITY_2021_1_OR_NEWER
        public static readonly InstanceAccessor<List<UnityEditor.PropertyDrawer>> m_PropertyDrawers = InternalType.CreateFieldAccessor<List<UnityEditor.PropertyDrawer>>(nameof(m_PropertyDrawers));
#else
        public static readonly InstanceAccessor<UnityEditor.PropertyDrawer> m_PropertyDrawer = InternalType.CreateFieldAccessor<UnityEditor.PropertyDrawer>(nameof(m_PropertyDrawer));
#endif
      }



      public static Type InternalType => Statics.InternalType;

      public object _instance;

      internal static PropertyHandler Wrap(object instance) => new PropertyHandler() { _instance = instance };

      public static PropertyHandler New() {
        return PropertyHandler.Wrap(Activator.CreateInstance(InternalType));
      }

#if UNITY_2021_1_OR_NEWER
      public List<UnityEditor.PropertyDrawer> m_PropertyDrawers {
        get => Statics.m_PropertyDrawers.GetValue(_instance);
        set => Statics.m_PropertyDrawers.SetValue(_instance, value);
      }
#else
      public UnityEditor.PropertyDrawer m_PropertyDrawer {
        get => Statics.m_PropertyDrawer.GetValue(_instance);
        set => Statics.m_PropertyDrawer.SetValue(_instance, value);
      }
#endif

      public bool Equals(PropertyHandler other) {
        return _instance == other._instance;
      }

      public override int GetHashCode() {
        return _instance?.GetHashCode() ?? 0;
      }

      public override bool Equals(object obj) {
        return (obj is PropertyHandler h) ? Equals(h) : false;
      }

      public List<UnityEditor.DecoratorDrawer> decoratorDrawers {
        get => Statics.m_DecoratorDrawers.GetValue(_instance);
        set => Statics.m_DecoratorDrawers.SetValue(_instance, value);
      }
    }

    [InitializeOnLoad]
    public static class EditorApplication {
      public static readonly Action Internal_CallAssetLabelsHaveChanged = typeof(UnityEditor.EditorApplication).CreateMethodDelegate<Action>(nameof(Internal_CallAssetLabelsHaveChanged));
    }

    public struct ObjectSelector {
      [InitializeOnLoad]
      static class Statics {
        public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ObjectSelector", true);
        public static readonly StaticAccessor<bool> _tooltip = InternalType.CreateStaticPropertyAccessor<bool>(nameof(isVisible));
        public static readonly StaticAccessor<UnityEditor.EditorWindow> _get = InternalType.CreateStaticPropertyAccessor<UnityEditor.EditorWindow>(nameof(get), InternalType);
        public static readonly InstanceAccessor<string> _searchFilter = InternalType.CreatePropertyAccessor<string>(nameof(searchFilter));
      }

      private UnityEditor.EditorWindow _instance;

      public static bool isVisible => Statics._tooltip.GetValue();
      
      public static ObjectSelector get => new ObjectSelector() { _instance = Statics._get.GetValue() };

      public string searchFilter {
        get => Statics._searchFilter.GetValue(_instance);
        set => Statics._searchFilter.SetValue(_instance, value);
      }

      private static readonly InstanceAccessor<int> _objectSelectorID = Statics.InternalType.CreateFieldAccessor<int>(nameof(objectSelectorID));
      public int objectSelectorID => _objectSelectorID.GetValue(_instance);
    }

    [InitializeOnLoad]
    public class InspectorWindow {
      public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow", true);
      public static readonly InstanceAccessor<bool> _isLockedAccessor = InternalType.CreatePropertyAccessor<bool>(nameof(isLocked));

      UnityEditor.EditorWindow _instance;

      public InspectorWindow(UnityEditor.EditorWindow instance) {
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
  }
}


#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Wizard/scripts/WizardWindow.cs

// Renamed and moved Jun 14 2021

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/Wizard/scripts/WizardWindowUtils.cs

// Renamed and moved Jun 14 2021

#endregion


#region Assets/Photon/Fusion/Scripts/Editor/XmlDocumentation.cs

namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Text.RegularExpressions;
  using System.Threading.Tasks;
  using System.Xml;
  using UnityEngine;

  public static class XmlDocumentation {
    static HashSet<Assembly> loadedAssemblies = new HashSet<Assembly>();
    static Dictionary<string, Entry> loadedXmlSummaries = new Dictionary<string, Entry>();

    public static string GetXmlDocSummary(this MemberInfo member, bool forTooltip = false) {
      if (member == null) {
        return null;
        //throw new ArgumentNullException(nameof(member));
      }

      var type = member as Type ?? member.DeclaringType;
      Debug.Assert(type != null);

      LoadCodeDoc(type.Assembly);
      var key = GetCodeDocMemberKey(member);

      if (loadedXmlSummaries.TryGetValue(key, out var entry)) {
        return forTooltip ? entry.Tooltip : entry.Summary;
      } else {
        return null;
      }
    }

    static string GetCodeDocMemberKey(MemberInfo member) {
      switch (member) {
        case FieldInfo f:     return $"F:{SanitizeTypeName(f.DeclaringType)}.{f.Name}";
        case PropertyInfo p:  return $"P:{SanitizeTypeName(p.DeclaringType)}.{p.Name}";
        case MethodInfo m:    return $"M:{SanitizeTypeName(m.DeclaringType)}.{m.Name}";
        case Type t:          return $"T:{SanitizeTypeName(t)}";
        default:
          throw new NotSupportedException($"{member.GetType()}");
      }
    }


    static string GetDirectoryPath(this Assembly assembly) {
      string codeBase = assembly.CodeBase;
      UriBuilder uri = new UriBuilder(codeBase);
      string path = Uri.UnescapeDataString(uri.Path);
      return Path.GetDirectoryName(path);
    }

    static void LoadCodeDoc(Assembly assembly) {
      if (!loadedAssemblies.Add(assembly)) {
        // already load or attempted to load
        return;
      }

      if (assembly.GetName().Name.StartsWith("UnityEngine")) {
        return;
      }

      string directoryPath = assembly.GetDirectoryPath();
      string xmlFilePath = Path.Combine(directoryPath, assembly.GetName().Name + ".xml");
      string xmlContents;

      if (File.Exists(xmlFilePath)) {
        xmlContents = File.ReadAllText(xmlFilePath);
      } else {
        // located in resources
        var asset = Resources.Load<TextAsset>(assembly.GetName().Name);
        if (asset != null) {
          xmlContents = asset.text;
        } else {
          return;
        }
      }

      var sw = System.Diagnostics.Stopwatch.StartNew();
      ParseCodeDoc(xmlContents, UnityEditor.EditorGUIUtility.isProSkin);
      FusionEditorLog.TraceInspector($"Parsing codedoc for {assembly.GetName().Name}: {sw.Elapsed}");
    }



    // https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/october/csharp-accessing-xml-documentation-via-reflection
    static void ParseCodeDoc(string xmlDocumentation, bool proSkin) {

      var result = new List<(string, Entry)>();

      var xmlDoc = new XmlDocument();
      xmlDoc.LoadXml(xmlDocumentation);

      var members = xmlDoc.DocumentElement.SelectSingleNode("members");
      var nodes = members.ChildNodes.Cast<XmlNode>()
        .Where(node => node.NodeType == XmlNodeType.Element && node.Name == "member");

      Parallel.ForEach(nodes, new ParallelOptions() {
        MaxDegreeOfParallelism = 1
      },
        () => new List<(string, Entry)>(),
        (node, _, list) => {

          var name = node.Attributes["name"].Value;
          var summary = node.SelectSingleNode("summary")?.InnerXml.Trim();

          if (summary != null) {

            // remove generic indicator
            summary = summary.Replace("`1", "");
            // remove Fusion namespace
            summary = summary.Replace(":Fusion.", ":");

            // fork tooltip and help summaries
            var help = Reformat(summary, proSkin, false);
            var ttip = Reformat(summary, proSkin, true);

            list.Add((name, new Entry() { Summary = help, Tooltip = ttip }));
          }
          return list;
        },
        (list) => {
          lock (loadedXmlSummaries) {
            //result.AddRange(list);
            foreach (var e in list) {
              var (name, entry) = e;
              if (loadedXmlSummaries.TryGetValue(name, out var existing)) {
                FusionEditorLog.Trace($"XmlDoc conflict {name}: {entry} vs {existing}");
              } else { 
                loadedXmlSummaries.Add(name, entry);
              }
            }
          }
        }
      );
    }



    static class Regexes {
      public static readonly Regex SeeWithCref = new Regex(@"<see\w* cref=""(?:\w: ?)?([\w\.\d]*)(?:\(.*\))?"" ?\/>", RegexOptions.None);
      public static readonly Regex See = new Regex(@"<see\w* .*>([\w\.\d]*)<\/see\w*>", RegexOptions.None);
      public static readonly Regex WhitespaceString = new Regex(@"\s+");
      public static readonly Regex SquareBracketsWithContents = new Regex(@"\[.*\]");
      public static readonly Regex XmlCodeBracket = new Regex(@"<code>([\s\S]*?)</code>");
      public static readonly Regex XmlEmphasizeBrackets = new Regex(@"<\w>([\s\S]*?)</\w>");
    }

    // (Inline help summary, Tooltip summary)
    private static string Reformat(string summary, bool proSkin, bool forTooltip) {

      // Tooltips don't support formatting tags. Inline help does.
      if (forTooltip) {
        summary = Regexes.SeeWithCref.Replace(summary, "$1");
        summary = Regexes.See.Replace(summary, "$1");
        summary = Regexes.XmlEmphasizeBrackets.Replace(summary, "$1");

      } else {
        string colorstring = proSkin ? "<color=#FFEECC>$1</color>" : "<color=#664400>$1</color>";
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

      summary = summary.Trim();

      return summary;
    }

    static string SanitizeTypeName(Type type) {
      return Regexes.SquareBracketsWithContents.Replace(type.FullName, string.Empty).Replace('+', '.');
    }

    struct Entry {
      public string Summary;
      public string Tooltip;

      public override string ToString() {
        return $"{{ {Summary}; {Tooltip} }}";
      }
    }
  }
}

#endregion

#endif
