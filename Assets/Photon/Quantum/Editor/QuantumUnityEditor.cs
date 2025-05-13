#if !QUANTUM_DEV

#region Assets/Photon/Quantum/Editor/CustomEditors/ComponentPrototypeDrawer.cs

namespace Quantum.Editor {
  // [CustomPropertyDrawer(typeof(ComponentPrototype), true)]
  // public class ComponentPrototypeDrawer : PropertyDrawer {
  //   private string _lastManagedReferenceFullTypename;
  //   private Type   _componentType;
  //
  //   public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
  //     if (property.propertyType != SerializedPropertyType.ManagedReference) {
  //       EditorGUI.PropertyField(position, property, label, property.isExpanded);
  //       return;
  //     }
  //
  //     if (property.managedReferenceFullTypename != _lastManagedReferenceFullTypename || _componentType == null) {
  //       _lastManagedReferenceFullTypename = property.managedReferenceFullTypename;
  //       _componentType                = null;
  //
  //       try {
  //         var parts         = _lastManagedReferenceFullTypename.Split(' ');
  //         var assemblyName  = parts[0];
  //         var typeName      = parts[1];
  //         var aqn           = $"{typeName}, {assemblyName}";
  //         var prototypeType = Type.GetType(aqn, true);
  //
  //         _componentType = ComponentPrototype.PrototypeTypeToComponentType(prototypeType);
  //       } catch {
  //         // swallow exceptions
  //       }
  //     }
  //
  //     if (property.isExpanded) {
  //       EditorGUI.PropertyField(position, property, label, property.isExpanded);
  //     } else {
  //       property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label);
  //     }
  //
  //     var thumbnailPosition = position;
  //     thumbnailPosition.x      = position.xMax - QuantumEditorGUI.ThumbnailWidth;
  //     thumbnailPosition.width  = QuantumEditorGUI.ThumbnailWidth;
  //     thumbnailPosition.height = EditorGUIUtility.singleLineHeight;
  //     if (_componentType == null) {
  //       QuantumEditorGUI.MissingComponentThumbnailPrefix(thumbnailPosition, _lastManagedReferenceFullTypename);
  //     } else {
  //       QuantumEditorGUI.ComponentThumbnailPrefix(thumbnailPosition, _componentType);
  //     }
  //   }
  //
  //   public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
  //     return EditorGUI.GetPropertyHeight(property, property.isExpanded);
  //   }
  // }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/PhotonServerSettingsEditor.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;
  using Photon.Realtime;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(PhotonServerSettings), false)]
  public class PhotonServerSettingsEditor : QuantumEditor {
    
    private static readonly string[] _localAppSettings = new[] { "Cloud", "Local Name Server", "Local Master Server" };

    private SerializedProperty _appSettingsProperty;

    protected override void OnEnable() {
      base.OnEnable();

      _appSettingsProperty = serializedObject.FindProperty(nameof(PhotonServerSettings.AppSettings));
    }

    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();
      
      QuantumEditorGUI.ScriptPropertyField(serializedObject);
      
      EditorGUI.BeginChangeCheck();

      var settings = (PhotonServerSettings)target;
      if (string.IsNullOrEmpty(settings.AppSettings.AppIdQuantum)) {
        using (new EditorGUILayout.HorizontalScope()) {
          EditorGUILayout.HelpBox("Quantum AppId is missing. Create a Quantum app in the Photon Dashboard and add its AppId to the AppSettings.", MessageType.Error);
          if (GUILayout.Button("Create AppId", GUILayout.ExpandHeight(true))) {
            Application.OpenURL("https://dashboard.photonengine.com/en-US/PublicCloud");
          }
        }
      }

      // app settings do not have the InlineHelpAttribute, so it needs to be handled manually
      if (EditorGUILayout.PropertyField(_appSettingsProperty, includeChildren: false)) {
        using (new EditorGUI.IndentLevelScope()) {
          foreach (var property in _appSettingsProperty.GetChildren()) {
            switch (property.name) {
              case nameof(AppSettings.AppIdFusion):
                continue;
              case nameof(AppSettings.AppIdRealtime):
                if (string.IsNullOrEmpty(property.stringValue)) {
                  continue;
                }
                EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, property), property);
                break;
              default:
                EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, property), property);
                break;
            }
          }
        }
      }

      {
        foreach (var property in serializedObject.GetIterator().GetChildren()) {
          if (property.name == _appSettingsProperty.name || property.name == QuantumEditorGUI.ScriptPropertyName) {
            continue;
          }
          EditorGUILayout.PropertyField(property);    
        }
      }

      // Convert RealtimeAppId to QuantumAppId
      if (string.IsNullOrEmpty(settings.AppSettings.AppIdRealtime) == false &&
          string.IsNullOrEmpty(settings.AppSettings.AppIdQuantum)) {
        settings.AppSettings.AppIdQuantum = settings.AppSettings.AppIdRealtime;
        settings.AppSettings.AppIdRealtime = null;
        serializedObject.Update();
      }

      using (new QuantumEditorGUI.SectionScope("Development Tools")) {
        using (new EditorGUILayout.VerticalScope()) {
          // Dashboard Links
          var appId = settings.AppSettings.GetAppId(ClientAppType.Quantum);
          if (string.IsNullOrEmpty(appId)) {
            if (GUILayout.Button("Create AppId", EditorStyles.miniButton)) {
              Application.OpenURL("https://dashboard.photonengine.com/en-US/PublicCloud");
            }
          } else {
            if (GUILayout.Button("Manage AppId", EditorStyles.miniButton)) {
              Application.OpenURL($"https://dashboard.photonengine.com/en-US/App/Manage/{appId}");
            }
          }

          // Best Region Cache
          EditorGUILayout.PrefixLabel(new GUIContent("Best Region Cache:", "Resets the Best Region Cache saved in PlayerPerfs.\nBest region is used when AppSettings.FixedRegion is not set."));
          var prefLabel = "n/a";
          if (!string.IsNullOrEmpty(settings.BestRegionSummary)) {
            var regionsPrefsList = settings.BestRegionSummary.Split(';');
            if (regionsPrefsList.Length > 1 && !string.IsNullOrEmpty(regionsPrefsList[0])) {
              prefLabel = $"'{regionsPrefsList[0]}' ping:{regionsPrefsList[1]}ms ";
            }
          }
            GUILayout.TextField(prefLabel);
          if (GUILayout.Button("Reset Best Region Cache", EditorStyles.miniButton)) {
            settings.BestRegionSummary = String.Empty;
          }

          if (GUILayout.Button("Open Region Dashboard", EditorStyles.miniButton)) {
            Application.OpenURL("https://dashboard.photonengine.com/en-US/App/RegionsWhitelistEdit/" + settings.AppSettings.AppIdRealtime);
          }

          // Local server configurations
          EditorGUILayout.PrefixLabel("Load App Settings:");
          
          int selectedIndex;
          if (string.IsNullOrEmpty(settings.AppSettings.Server)) {
            selectedIndex = 0;
          } else if (settings.AppSettings.UseNameServer) {
            selectedIndex = 1;
          } else {
            selectedIndex = 2;
          }
          
          EditorGUI.BeginChangeCheck();
          
          var gridSelection = GUILayout.SelectionGrid(selectedIndex, _localAppSettings, 1, EditorStyles.miniButton);
          
          if (EditorGUI.EndChangeCheck()) {
            if (gridSelection == 0) {
              SetSettingsToCloud(settings.AppSettings);
            } else if (gridSelection == 1) {
              SetSettingsToLocalNameServer(settings.AppSettings);
            } else if (gridSelection == 2) {
              SetSettingsToLocalMasterServer(settings.AppSettings);
            }
            
            EditorUtility.SetDirty(target);
            serializedObject.Update();
          }
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }
    }

    public static void SetSettingsToCloud(AppSettings appSettings) {
      appSettings.Server = string.Empty;
      appSettings.UseNameServer = true;
      appSettings.Port = 0;
      appSettings.AuthMode = AuthModeOption.AuthOnceWss;
    }

    public static void SetSettingsToLocalMasterServer(AppSettings appSettings) {
        appSettings.Server = GuessLocalIpAddress();
        appSettings.UseNameServer = false;
        appSettings.Port = 5055;
      appSettings.AuthMode = AuthModeOption.AuthOnce;
    }

    public static void SetSettingsToLocalNameServer(AppSettings appSettings) {
      appSettings.Server = GuessLocalIpAddress();
      appSettings.UseNameServer = true;
      appSettings.Port = 5058;
      appSettings.AuthMode = AuthModeOption.AuthOnce;
    }

    public static string GuessLocalIpAddress() {
      try {
        return System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
               .AddressList
               .First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
               .ToString();
      } catch (Exception e) {
        QuantumEditorLog.Exception("Cannot find local server address, sorry.", e);
      }
      return string.Empty;
    }
  }
}



#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumAddRuntimePlayersEditor.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumAddRuntimePlayers))]
  public class QuantumAddRuntimePlayersEditor : QuantumEditor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
      var data = (QuantumAddRuntimePlayers)target;
      DrawAddRemoveTools(QuantumRunner.Default?.Game, data.Players);
    }

    public static void DrawAddRemoveTools(QuantumGame game, RuntimePlayer[] players) {
      if (game == null || players == null) {
        return;
      }

      QuantumEditorGUI.Header("Runtime Tools");
      for (int i = 0; i < players.Length; i++) {
        using (new EditorGUILayout.HorizontalScope()) {
          EditorGUILayout.LabelField($"Player {i}");
          using (new EditorGUI.DisabledScope(QuantumRunner.Default.Game.Session.IsPlayerSlotLocal(i))) {
            if (GUILayout.Button("Add")) {
              QuantumRunner.Default.Game.AddPlayer(i, players[i]);
            }
          }
          using (new EditorGUI.DisabledScope(QuantumRunner.Default.Game.Session.IsPlayerSlotLocal(i) == false)) {
            if (GUILayout.Button("Remove")) {
              QuantumRunner.Default.Game.RemovePlayer(i);
            }
          }
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumAssetObjectEditor.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

#if !DISABLE_QUANTUM_ASSET_INSPECTOR && !QUANTUM_DISABLE_ASSET_EDITORS
  [CustomEditor(typeof(AssetObject), true)]
#endif
  [CanEditMultipleObjects]
  public class QuantumAssetObjectEditor : QuantumEditor {
    
    [NonSerialized]
    private string _guidParseError;
    
    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();
    
      var target = (AssetObject)base.target;
      EditorGUI.BeginChangeCheck();

      base.DrawDefaultInspector();
      
      base.DrawEditorButtons();
      
      EditorGUILayout.Space();

      DrawAssetMeta(serializedObject, ref _guidParseError);

      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }
    }

    public static void DrawAssetMeta(SerializedObject serializedObject, ref string guidParseError) {
      var scriptProperty = serializedObject.FindProperty(QuantumEditorGUI.ScriptPropertyName);
      if (scriptProperty == null) {
        return;
      }

      using (new QuantumEditorGUI.EnabledScope(true)) {
        scriptProperty.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(scriptProperty.isExpanded, "Quantum Unity DB");

        if (scriptProperty.isExpanded) {
          if (QuantumUnityDB.TryGetGlobal(out _)) {
            DrawAssetResourceGUI(serializedObject);
            DrawAssetGuidOverrideGUI(serializedObject, ref guidParseError);
          } else {
            EditorGUILayout.HelpBox("Quantum Unity DB is not available", MessageType.Warning);
          }
        }
      }

      EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawAssetResourceGUI(SerializedObject serializedObject) {
      
      var targets = serializedObject.targetObjects;
      foreach (AssetObject asset in targets) {
        using (new EditorGUILayout.HorizontalScope()) {
          var assetGuid = asset.Guid;
          var source  = QuantumUnityDB.GetGlobalAssetSource(assetGuid);
          if (source == null) {
            EditorGUILayout.LabelField("<no provider>");
          } else {
            EditorGUILayout.LabelField(source.Description);
          }

          if (targets.Length <= 1) {
            continue;
          }
          
          if (GUILayout.Button("Ping", GUILayout.ExpandWidth(false))) {
            EditorGUIUtility.PingObject(asset);
          }
        }
      }
    }

    private static void DrawAssetGuidOverrideGUI(SerializedObject serializedObject, ref string guidParseError) {
      
      bool allHaveOverrides = true;
      bool anyHaveOverrides = false;
      bool reimport         = false;
      bool invalidGuid      = false;

      var targets = serializedObject.targetObjects;
      var firstTarget = targets[0];
      
      foreach (AssetObject obj in targets) {
        if (!EditorUtility.IsPersistent(obj)) {
          continue;
        }
        var assetGuid = obj.Guid;
        var expectedGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(obj, out var isOverride);
        if (isOverride) {
          anyHaveOverrides = true;
        } else {
          allHaveOverrides = false;
        }
        
        if (expectedGuid != assetGuid) {
          invalidGuid = true;
        }
      }

      if (invalidGuid) {
        EditorGUILayout.HelpBox("Invalid guid", MessageType.Error);  
      }
      
      using (new QuantumEditorGUI.EnabledScope(true)) {
        EditorGUI.BeginChangeCheck();

        EditorGUI.showMixedValue = allHaveOverrides != anyHaveOverrides;
        bool overrideGuids = EditorGUILayout.Toggle("Guid Override", anyHaveOverrides);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck()) {
          reimport = true;
          guidParseError = null;
          QuantumUnityDBUtilities.SetAssetGuidDeterministic(targets.Cast<AssetObject>(), !overrideGuids, warnIfChange: true);
        }

        if (overrideGuids && targets.Length == 1) {
          EditorGUI.BeginChangeCheck();
          Rect rect = EditorGUILayout.GetControlRect();
          var  str  = EditorGUI.DelayedTextField(rect, "Guid", ((AssetObject)firstTarget).Guid.ToString(false));
          if (EditorGUI.EndChangeCheck()) {
            reimport        = true;
            guidParseError = null;
            if (AssetGuid.TryParse(str, out var parsedGuid, includeBrackets: false)) {
              foreach (AssetObject asset in targets) {
                QuantumUnityDBUtilities.SetAssetGuidOverride(asset, parsedGuid);
                asset.Guid = parsedGuid;
                AssetDatabaseUtils.SetAssetAndTheMainAssetDirty(asset);
              }
            } else {
              guidParseError = $"Failed to parse {str}";
            }
          }

          if (guidParseError != null) {
            QuantumEditorGUI.Decorate(rect, guidParseError, MessageType.Error, hasLabel: true);
          }

          EditorGUILayout.HelpBox(
            $"Starting with Quantum 3.0, AssetGuids are deterministic and based on the asset's Unity GUID and fileId. " +
            $"To support legacy assets and AssetGuid reassignments, AssetGuids can be overridden - the data mapping " +
            $"Unity GUID and fileId to an AssetGuid is kept in the editor-only asset {nameof(QuantumUnityDBUtilities)}", MessageType.Info);
        }

        EditorGUI.showMixedValue = false;
      }

      if (reimport) {
        AssetDatabase.SaveAssets();
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumColliderHandles.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using static UnityEditor.IMGUI.Controls.CapsuleBoundsHandle;

  public static class QuantumColliderHandles {
    public struct EdgeChangeResult {
      public bool V0Changed, V1Changed;
    }

    public const int EditCollider = 0;
    public const int EditPosition = 1;
    public const int EditRotation = 2;

    private static GUIContent[] _guiContents;
    private static GUIContent[] _guiContentsWithRotation;

    private static readonly BoxBoundsHandle _box = new BoxBoundsHandle();
    private static readonly SphereBoundsHandle _sphere = new SphereBoundsHandle();
    private static readonly CapsuleBoundsHandle _capsule = new CapsuleBoundsHandle();

    private static Color DefaultColor {
      get {
        return QuantumGameGizmosSettingsScriptableObject.Global.Settings.StaticColliders.Color;
      }
    }

    private static void UpdateAxes(PrimitiveBoundsHandle handle, bool is3D) {
      if (is3D) {
        handle.axes = PrimitiveBoundsHandle.Axes.All;
      } else {
        handle.axes = PrimitiveBoundsHandle.Axes.X;

#if QUANTUM_XY
        handle.axes |= PrimitiveBoundsHandle.Axes.Y;
#else
        handle.axes |= PrimitiveBoundsHandle.Axes.Z;
#endif
      }
    }

    private static GUIContent[] GetGUIContent(bool rotation) {
      if (rotation) {
        if (_guiContentsWithRotation == null) {
          _guiContentsWithRotation = new[] {
            EditorGUIUtility.IconContent("d_EditCollider", "Modify the collider size."), EditorGUIUtility.IconContent("d_AvatarPivot", "Modify collider position offset."), EditorGUIUtility.IconContent("RotateTool On", "Modify collider rotation offset.")
          };
        }

        return _guiContentsWithRotation;
      }

      if (_guiContents == null) {
        _guiContents = new[] { EditorGUIUtility.IconContent("d_EditCollider", "Modify the collider size."), EditorGUIUtility.IconContent("d_AvatarPivot", "Modify collider position offset."), };
      }

      return _guiContents;
    }

    public static void RepaintSceneView() {
      EditorWindow view = EditorWindow.GetWindow<SceneView>();

      if (view != null) {
        view.Repaint();
      }
    }

    public static void DrawToolbar(ref Rect position, ref int currentIndex, bool supportsRotation) {
      const float labelWidth = 100f;
      const float spacing = 5f;
      var gui = GetGUIContent(supportsRotation);
      float toolbarWidth = GUI.skin.button.CalcSize(gui[0]).x * gui.Length;

      // Label left-aligned
      Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
      EditorGUI.PrefixLabel(labelRect, new GUIContent("Edit Collider"));

      // Center toolbar after the label
      float remainingWidth = position.width - labelWidth - spacing;
      float toolbarX = labelWidth + spacing + (remainingWidth - toolbarWidth) / 2;
      Rect toolbarRect = new Rect(toolbarX, position.y, toolbarWidth, EditorStyles.toolbar.fixedHeight);

      position.y += toolbarRect.height + EditorGUIUtility.standardVerticalSpacing * 2;

      EditorGUI.BeginChangeCheck();
      int prevIndex = currentIndex;
      currentIndex = GUI.Toolbar(toolbarRect, currentIndex, gui, "AppCommand");

      if (EditorGUI.EndChangeCheck() && currentIndex == prevIndex) {
        currentIndex = -1;
      }

      if (currentIndex != prevIndex) {
        RepaintSceneView();
      }
    }

    public static EdgeChangeResult Edge(Behaviour qmb, ref FPVector2 v0, ref FPVector2 v1, FPVector2 posOffset, FP rotOffset) {
      var t = qmb.transform;
      // we do it this way to strip the unneeded axe's easily
      var rot =
        t.rotation.ToFPRotation2DDegrees().ToUnityQuaternionDegrees() *
        rotOffset.FlipRotation().ToUnityQuaternionDegrees();

      var m = Matrix4x4.TRS(
        t.TransformPoint(posOffset.ToUnityVector3()),
        rot,
        t.localScale
      );

      var v00 = v0;
      var v01 = v1;

      DrawEdgeVertex(qmb, m, ref v0);
      DrawEdgeVertex(qmb, m, ref v1);

      var result = new EdgeChangeResult();

      result.V0Changed = Approximately(v0, v00, FP.Epsilon) == false;
      result.V1Changed = Approximately(v1, v01, FP.Epsilon) == false;

      return result;
    }

    private static bool Approximately(FPVector2 a, FPVector2 b, FP epsilon) {
      return FPMath.Abs(a.X - b.X) < epsilon &&
             FPMath.Abs(a.Y - b.Y) < epsilon;
    }

    private static void DrawEdgeVertex(Behaviour behaviour, Matrix4x4 m, ref FPVector2 vertex) {
      EditorGUI.BeginChangeCheck();

      // we do not directly apply matrix here because we want the handles axe's to stay aligned

      var pos = Handles.PositionHandle(m.MultiplyPoint(vertex.ToUnityVector3()), Quaternion.identity);

      pos = m.inverse.MultiplyPoint(pos);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RegisterCompleteObjectUndo(behaviour, "Moving edge vertex");
        vertex = pos.ToFPVector2();
      }
    }

    public static void Polygon(Behaviour qmb, ref FPVector2[] vertices, FPVector2 posOffset, FP rotOffset, bool isScaled = true) {
      if (Event.current.shift || Event.current.control) {
        DrawAddAndRemoveButtons(qmb, Event.current.shift, Event.current.control, posOffset, rotOffset, ref vertices);
      } else {
        DrawMovementHandles(qmb, posOffset, rotOffset, ref vertices, isScaled);
        DrawMakeCCWButton(qmb, posOffset, ref vertices);
      }
    }

    private static void AddVertex(Behaviour qmb, int index, FPVector2 position, ref FPVector2[] vertices) {
      var newVertices = new List<FPVector2>(vertices);
      newVertices.Insert(index, position);
      Undo.RegisterCompleteObjectUndo(qmb, "Adding polygon vertex");
      vertices = newVertices.ToArray();
    }

    private static void RemoveVertex(Behaviour qmb, ref FPVector2[] vertices, int index) {
      var newVertices = new List<FPVector2>(vertices);
      newVertices.RemoveAt(index);
      Undo.RegisterCompleteObjectUndo(qmb, "Removing polygon vertex");
      vertices = newVertices.ToArray();
    }

    private static void DrawMovementHandles(Behaviour qmb, FPVector2 posOffset, FP rotOffset, ref FPVector2[] vertices, bool isScaled) {
      var isClockWise = FPVector2.IsClockWise(vertices);
      var t = qmb.transform;

      var r = t.rotation * rotOffset.FlipRotation().ToUnityQuaternionDegrees();
      r = r.ToFPRotation2DDegrees().ToUnityQuaternionDegrees();

      var absScale = Vector3.one;
      if (isScaled) {
        absScale.x = Mathf.Abs(t.transform.lossyScale.x);
        absScale.y = Mathf.Abs(t.transform.lossyScale.y);
        absScale.z = Mathf.Abs(t.transform.lossyScale.z);
      } else {
        absScale.x *= Mathf.Sign(t.transform.lossyScale.x);
        absScale.y *= Mathf.Sign(t.transform.lossyScale.y);
      }

      var m = Matrix4x4.TRS(t.TransformPoint(posOffset.ToUnityVector3()), r, Vector3.one);

      using (new Handles.DrawingScope(isClockWise ? Color.red : Color.white, m)) {
        for (int i = 0; i < vertices.Length; i++) {
          EditorGUI.BeginChangeCheck();

          var scaledPos = Vector3.Scale(vertices[i].ToUnityVector3(), absScale);
          var newWorldPosition = Handles.PositionHandle(scaledPos, r);

          if (EditorGUI.EndChangeCheck()) {
            Undo.RegisterCompleteObjectUndo(qmb, "Moving polygon vertex");

            var result = newWorldPosition;

            result.x /= absScale.x;
            result.y /= absScale.y;
            result.z /= absScale.z;
            
            vertices[i] = result.ToFPVector2();
          }
        }
      }
    }

    private static void DrawMakeCCWButton(Behaviour qmb, FPVector2 posOffset, ref FPVector2[] vertices) {
      if (FPVector2.IsPolygonConvex(vertices) && FPVector2.IsClockWise(vertices)) {
        var center = FPVector2.CalculatePolygonCentroid(vertices);
        var view = SceneView.currentDrawingSceneView;
        var screenPos = view.camera.WorldToScreenPoint(qmb.transform.position + center.ToUnityVector3() + posOffset.ToUnityVector3());
        var size = GUI.skin.label.CalcSize(new GUIContent(" Make CCW "));
        Handles.BeginGUI();
        if (GUI.Button(new Rect(screenPos.x - size.x * 0.5f, view.position.height - screenPos.y - size.y, size.x, size.y), "Make CCW")) {
          Undo.RegisterCompleteObjectUndo(qmb, "Making polygon CCW");
          FPVector2.MakeCounterClockWise(vertices);
        }

        Handles.EndGUI();
      }
    }

    private static void DrawAddAndRemoveButtons(Behaviour qmb, bool drawAddButton, bool drawRemoveButton, FPVector2 posOffset, FP rotOffset, ref FPVector2[] vertices) {
      var handlesColor = Handles.color;
      var t = qmb.transform;
      Handles.matrix = Matrix4x4.TRS(t.TransformPoint(posOffset.ToUnityVector3()),
        t.rotation * rotOffset.FlipRotation().ToUnityQuaternionDegrees(),
        t.lossyScale
      );

      for (int i = 0; i < vertices.Length; i++) {
        var facePosition_FP = (vertices[i] + vertices[(i + 1) % vertices.Length]) * FP._0_50;

        float hs = 0;
        float dtrhs = 0;
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
        // TODO review
        hs = QuantumStaticPolygonCollider2DEditor.HandlesSize;
        dtrhs = QuantumStaticPolygonCollider2DEditor.DistanceToReduceHandleSize;
#endif

        var handleSize = hs * HandleUtility.GetHandleSize(vertices[i].ToUnityVector3());
        var cameraDistance = Vector3.Distance(SceneView.currentDrawingSceneView.camera.transform.position, vertices[i].ToUnityVector3());
        if (cameraDistance > dtrhs) {
          handleSize *= (dtrhs / (cameraDistance));
        }

        if (drawRemoveButton) {
          if (vertices.Length > 3) {
            Handles.color = Color.red;
            if (Handles.Button(vertices[i].ToUnityVector3(), Quaternion.identity, handleSize, handleSize, Handles.DotHandleCap)) {
              RemoveVertex(qmb, ref vertices, i);
              return;
            }
          }
        }

        if (drawAddButton) {
          Handles.color = Color.green;
          if (Handles.Button(facePosition_FP.ToUnityVector3(), Quaternion.identity, handleSize, handleSize, Handles.DotHandleCap)) {
            AddVertex(qmb, i + 1, facePosition_FP, ref vertices);
            return;
          }
        }
      }

      Handles.color = handlesColor;
      Handles.matrix = Matrix4x4.identity;
    }

    public static void Circle(Behaviour qmb, FPVector2 posOffset, ref FP radius, Color? color = null) {
      UpdateAxes(_sphere, false);
      var transform = qmb.transform;
      var rotation = transform.rotation.ToFPRotation2DDegrees().ToUnityQuaternionDegrees();
      var scale = transform.lossyScale;

      Vector3 scaleAbs;
      scaleAbs.x = Mathf.Abs(scale.x);
      scaleAbs.y = Mathf.Abs(scale.y);
      scaleAbs.z = Mathf.Abs(scale.z);

      var circleScale = scaleAbs.ToFPVector2();
      var radiusScale = FPMath.Max(circleScale.X, circleScale.Y);

      var handleScale = (radiusScale * FPVector2.One).ToUnityVector3();

#if QUANTUM_XY
      handleScale.z = -scaleAbs.z;
#else
      handleScale.y = scaleAbs.y;
#endif

      var max = Mathf.Max(Mathf.Max(scaleAbs.x, scaleAbs.y), scaleAbs.z);
      var m = Matrix4x4.TRS(transform.TransformPoint(posOffset.ToUnityVector3()), rotation, handleScale);

      using (new Handles.DrawingScope(color.GetValueOrDefault(DefaultColor), m)) {
        _sphere.center = Vector3.zero;
        _sphere.radius = radius.AsFloat;

        EditorGUI.BeginChangeCheck();
        _sphere.DrawHandle();
        if (EditorGUI.EndChangeCheck()) {
          Undo.RecordObject(qmb, "Modified Quantum Sphere Collider.");

          radius = _sphere.radius.ToFP();

          EditorUtility.SetDirty(qmb);
        }
      }
    }

    private static void Capsule(bool is3D, Behaviour qmb, FPVector3 posOffset, FPVector3 rotOffset, ref FP radius, ref FP height, Color? color = null) {
      UpdateAxes(_capsule, is3D);

      var transform = qmb.transform;
      var rotation = transform.rotation * Quaternion.Euler(rotOffset.ToUnityVector3());
      var scale = transform.lossyScale;

      Vector3 scaleAbs = transform.lossyScale;
      scaleAbs.x = Mathf.Abs(scale.x);
      scaleAbs.y = Mathf.Abs(scale.y);
      scaleAbs.z = Mathf.Abs(scale.z);

      if (is3D == false) {
        // strip un-needed rotations
        rotation = rotation.ToFPRotation2DDegrees().ToUnityQuaternionDegrees();
      }

      // Calculate radius scale based on max of x and z scales
      float radiusScale;

      if (is3D == false) {
        radiusScale = scaleAbs.x;
      } else {
        radiusScale = Mathf.Max(scaleAbs.x, scaleAbs.z);
      }

      radiusScale = Mathf.Max(0, radiusScale);

      // Scale the radius
      var capsuleRadius = Mathf.Max(radius.AsFloat, 0) * radiusScale;

      var capsuleHeightScale = scaleAbs.y;

      if (is3D == false) {
#if !QUANTUM_XY
        capsuleHeightScale = scaleAbs.z;
#endif
      }

      var extent = Mathf.Max((height.AsFloat * capsuleHeightScale / 2.0f) - capsuleRadius, 0);

      var m = Matrix4x4.TRS(
        transform.TransformPoint(posOffset.ToUnityVector3()),
        rotation,
        Vector3.one
      );

      if (is3D == false) {
#if QUANTUM_XY
        _capsule.heightAxis = HeightAxis.Y;
#else
        _capsule.heightAxis = HeightAxis.Z;
#endif
      } else {
        _capsule.heightAxis = HeightAxis.Y;
      }

      using (new Handles.DrawingScope(color.GetValueOrDefault(DefaultColor), m)) {
        _capsule.center = Vector3.zero;
        _capsule.height = (extent + capsuleRadius) * 2;
        _capsule.radius = capsuleRadius;

        EditorGUI.BeginChangeCheck();
        _capsule.DrawHandle();

        if (EditorGUI.EndChangeCheck()) {
          Undo.RecordObject(qmb, "Modified Collider");

          // Convert radius back
          radius = (_capsule.radius / radiusScale).ToFP();

          var newHeightScaled = (_capsule.height / capsuleHeightScale);

          newHeightScaled = Mathf.Max(newHeightScaled, radius.AsFloat * 2);

          height = newHeightScaled.ToFP();

          EditorUtility.SetDirty(qmb);
        }
      }
    }

    public static void Capsule3D(Behaviour qmb, FPVector3 posOffset, FPVector3 rotOffset, ref FP radius, ref FP height, Color? color = null) {
      Capsule(true, qmb, posOffset, rotOffset, ref radius, ref height, color);
    }

    public static void Capsule2D(Behaviour qmb, FPVector2 posOffset, FP rotOffset, ref FPVector2 size, Color? color = null) {
      var radius = size.X / 2;
      var height = size.Y;

      Capsule(
        false,
        qmb,
        posOffset.ToUnityVector3().ToFPVector3(),
        rotOffset.FlipRotation().ToUnityQuaternionDegrees().ToFPQuaternion().AsEuler,
        ref radius,
        ref height,
        color
      );

      size.X = radius * 2;
      size.Y = height;
    }

    public static void Rectangle(Behaviour qmb, FPVector2 posOffset, FP rotOffset, ref FPVector2 sizeOrExtents, bool isExtents = false, Color? color = null) {
      UpdateAxes(_box, false);

      var transform = qmb.transform;
      var rotation = transform.rotation * rotOffset.FlipRotation().ToUnityQuaternionDegrees();

      rotation = rotation.ToFPRotation2DDegrees().ToUnityQuaternionDegrees();

      using (new Handles.DrawingScope(color.GetValueOrDefault(DefaultColor), Matrix4x4.TRS(transform.TransformPoint(posOffset.ToUnityVector3()), rotation, transform.localScale))) {
        _box.center = Vector3.zero;

        var size = sizeOrExtents;

        if (isExtents) {
          size *= 2;
        }

        _box.size = size.ToUnityVector3();

        EditorGUI.BeginChangeCheck();
        _box.DrawHandle();

        if (EditorGUI.EndChangeCheck()) {
          Undo.RecordObject(qmb, "Modified Collider");

          var handleSize = _box.size.ToFPVector3();
          var sizeResult = handleSize.XZ;

#if QUANTUM_XY
          sizeResult = handleSize.XY;
#endif

          sizeOrExtents = sizeResult;

          if (isExtents) {
            sizeOrExtents /= 2;
          }

          EditorUtility.SetDirty(qmb);
        }
      }
    }

    public static void Box(Behaviour qmb, FPVector3 posOffset, FPVector3 rotOffset, ref FPVector3 sizeOrExtents, bool isExtents = false, Color? color = null) {
      UpdateAxes(_box, true);
      var transform = qmb.transform;
      var position = transform.TransformPoint(posOffset.ToUnityVector3());
      var rotation = transform.rotation * Quaternion.Euler(rotOffset.ToUnityVector3());

      var matrix = Matrix4x4.TRS(position, rotation, transform.localScale);

      using (new Handles.DrawingScope(color.GetValueOrDefault(DefaultColor), matrix)) {
        _box.center = Vector3.zero;

        var size = sizeOrExtents;

        if (isExtents) {
          size *= 2;
        }

        _box.size = size.ToUnityVector3();

        EditorGUI.BeginChangeCheck();
        _box.DrawHandle();
        if (EditorGUI.EndChangeCheck()) {
          Undo.RecordObject(qmb, "Modified Collider");

          size = _box.size.ToFPVector3();

          if (isExtents) {
            size /= 2;
          }

          sizeOrExtents = size;

          EditorUtility.SetDirty(qmb);
        }
      }
    }

    public static void Sphere(Behaviour qmb, FPVector3 posOffset, ref FP radius, Color? color = null) {
      UpdateAxes(_sphere, true);

      var transform = qmb.transform;
      var position = transform.position;
      var rotation = transform.rotation;
      var scale = transform.lossyScale;

      Vector3 scaleAbs;
      scaleAbs.x = Mathf.Abs(scale.x);
      scaleAbs.y = Mathf.Abs(scale.y);
      scaleAbs.z = Mathf.Abs(scale.z);

      var max = Mathf.Max(Mathf.Max(scaleAbs.x, scaleAbs.y), scaleAbs.z);

      using (new Handles.DrawingScope(color.GetValueOrDefault(DefaultColor), Matrix4x4.TRS(transform.TransformPoint(posOffset.ToUnityVector3()), rotation, max * Vector3.one))) {
        var handle = _sphere;

        handle.center = Vector3.zero;
        handle.radius = radius.AsFloat;

        EditorGUI.BeginChangeCheck();
        handle.DrawHandle();
        if (EditorGUI.EndChangeCheck()) {
          Undo.RecordObject(qmb, "Modified Quantum Sphere Collider.");

          posOffset = handle.center.ToFPVector3();
          radius = handle.radius.ToFP();

          EditorUtility.SetDirty(qmb);
        }
      }
    }

    public static void Rotation2D(Behaviour qep, FPVector2 positionOffset, ref FP rotationValue) {
      EditorGUI.BeginChangeCheck();

      // dont need to flip here because this is already a quantum value
      var rotationOffset = rotationValue.ToUnityQuaternionDegrees();

      var worldPos = qep.transform.TransformPoint(positionOffset.ToUnityVector3());

      rotationOffset = Handles.RotationHandle(rotationOffset, worldPos);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(qep, "Changed collider rotation offset.");

        var changedRot = rotationOffset.ToFPRotation2DDegrees();

        rotationValue = changedRot;

        EditorUtility.SetDirty(qep);
      }
    }

    public static void Rotation3D(Behaviour qep, FPVector3 positionOffset, ref FPVector3 rotOffset) {
      EditorGUI.BeginChangeCheck();

      var worldPos = qep.transform.TransformPoint(positionOffset.ToUnityVector3());

      var rotation = Quaternion.Euler(rotOffset.ToUnityVector3());

      rotation = Handles.RotationHandle(rotation, worldPos);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(qep, "Changed collider rotation offset.");

        var changedRot = rotation.eulerAngles.ToFPVector3();

        rotOffset = changedRot;

        EditorUtility.SetDirty(qep);
      }
    }

    public static void Position2D(Behaviour behaviour, ref FPVector2 position) {
      EditorGUI.BeginChangeCheck();

      var unityPos = position.ToUnityVector3();

      var worldPos = behaviour.transform.TransformPoint(unityPos);

      worldPos = Handles.PositionHandle(worldPos, Quaternion.identity);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(behaviour, "Changed collider position offset.");

        var changedPos = behaviour.transform.InverseTransformPoint(worldPos).ToFPVector3();

#if QUANTUM_XY
        position = changedPos.XY;
#else
        position = changedPos.XZ;
#endif

        EditorUtility.SetDirty(behaviour);
      }
    }

    public static void Position3D(Behaviour behaviour, ref FPVector3 position) {
      EditorGUI.BeginChangeCheck();

      var unityPos = position.ToUnityVector3();

      var worldPos = behaviour.transform.TransformPoint(unityPos);

      worldPos = Handles.PositionHandle(worldPos, Quaternion.identity);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(behaviour, "Changed collider position offset.");

        var changedPos = behaviour.transform.InverseTransformPoint(worldPos).ToFPVector3();

        position = changedPos;

        EditorUtility.SetDirty(behaviour);
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumDeterministicSessionConfigAssetEditor.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.Linq;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumDeterministicSessionConfigAsset))]
  public class QuantumDeterministicSessionConfigAssetEditor : QuantumEditor {
    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();
      var asset = target as QuantumDeterministicSessionConfigAsset;
      if (asset) {
        OnInspectorGUI(asset);
      }
    }

    private SerializedProperty _configProperty;
    private Dictionary<string, GUIContent> _propertyCache;

    protected override void OnEnable() {
      base.OnEnable();
      _configProperty = serializedObject.FindPropertyOrThrow(nameof(QuantumDeterministicSessionConfigAsset.Config));
      _propertyCache = typeof(DeterministicSessionConfig)
       .GetFields()
       .ToDictionary(x => x.Name, x => QuantumCodeDoc.FindEntry(x));
    }

    void OnInspectorGUI(QuantumDeterministicSessionConfigAsset asset) {
      base.PrepareOnInspectorGUI();
      base.DrawScriptPropertyField();

      using (new QuantumEditorGUI.SectionScope("Simulation")) {

        DoProperty(nameof(DeterministicSessionConfig.UpdateFPS), min: 1, label: "Simulation Rate", unit: Units.PerSecond);
        DoProperty(nameof(DeterministicSessionConfig.LockstepSimulation), label: "Force Strict Lockstep");

        EditorGUI.BeginDisabledGroup(asset.Config.LockstepSimulation);
        DoProperty(nameof(DeterministicSessionConfig.RollbackWindow), min: 1, unit: Units.Frames);
        EditorGUI.EndDisabledGroup();

        DoProperty(nameof(DeterministicSessionConfig.ChecksumInterval), min: 0, unit: Units.Frames);

        EditorGUI.BeginDisabledGroup(asset.Config.ChecksumInterval == 0);
        DoProperty(nameof(DeterministicSessionConfig.ChecksumCrossPlatformDeterminism));
        EditorGUI.EndDisabledGroup();
      }

      using (new QuantumEditorGUI.SectionScope("Input")) {
        DoProperty(nameof(DeterministicSessionConfig.InputDeltaCompression), label: "Input Delta Compression");
        DoProperty(nameof(DeterministicSessionConfig.InputDelayMin), min: 0, label: "Offset Min");
        DoProperty(nameof(DeterministicSessionConfig.InputDelayMax), min: asset.Config.InputDelayMin + 1, label: "Offset Max");
        DoProperty(nameof(DeterministicSessionConfig.InputDelayPingStart), min: 0, unit: Units.MilliSecs, label: "Offset Ping Start");
        DoProperty(nameof(DeterministicSessionConfig.InputRedundancy), min: 1, label: "Send Redundancy", unit: Units.Frames);
        DoProperty(nameof(DeterministicSessionConfig.InputRepeatMaxDistance), min: 0, label: "Repeat Max Distance", unit: Units.Frames);
        DoProperty(nameof(DeterministicSessionConfig.InputHardTolerance), min: -10, label: "Hard Tolerance", unit: Units.Frames);
        DoProperty(nameof(DeterministicSessionConfig.MinOffsetCorrectionDiff), min: 1, unit: Units.Frames, label: "Offset Correction Limit");
      }

      using (new QuantumEditorGUI.SectionScope("Time")) {
        DoProperty(nameof(DeterministicSessionConfig.TimeCorrectionRate), min: 0, label: "Correction Send Rate", unit: Units.PerSecond);
        DoProperty(nameof(DeterministicSessionConfig.MinTimeCorrectionFrames), min: 0, unit: Units.Frames, label: "Correction Frames Limit");
        DoProperty(nameof(DeterministicSessionConfig.SessionStartTimeout), min: 0, max: 30, unit: Units.Seconds, label: "Session Start Wait Time");
        DoProperty(nameof(DeterministicSessionConfig.TimeScaleMin), min: 10, max: 100, unit: Units.Percentage, label: "Time Scale Minimum");
        DoProperty(nameof(DeterministicSessionConfig.TimeScalePingMin), min: 0, max: 1000, unit: Units.MilliSecs, label: "Time Scale Ping Start");
        DoProperty(nameof(DeterministicSessionConfig.TimeScalePingMax), min: asset.Config.TimeScalePingMin + 1, max: 1000, unit: Units.MilliSecs, label: "Time Scale Ping End");
      }

      serializedObject.ApplyModifiedProperties();
    }

    void DoProperty(string propName, int min = int.MinValue, int max = int.MaxValue, Units unit = Units.None, string label = null) {
      var property = _configProperty.FindPropertyRelativeOrThrow(propName);

      if (_propertyCache.TryGetValue(propName, out var helpContent)) {
      }

      var position = QuantumEditorGUI.LayoutHelpPrefix(this, propName, helpContent);

      if (label != null) {
        EditorGUI.PropertyField(position, property, new GUIContent(label));
      } else {
        EditorGUI.PropertyField(position, property);
      }

      if (property.propertyType == SerializedPropertyType.Integer) {
        property.intValue = Mathf.Clamp(property.intValue, min, max);
      }


      if (unit != Units.None) {
        var unitLabel = UnitAttributeDrawer.UnitToLabel(unit);
        QuantumEditorGUI.Overlay(position, unitLabel);
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumEditorSettingsEditor.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEngine;

  [CustomEditor(typeof(QuantumEditorSettings), true)]
  public class QuantumEditorSettingsEditor : QuantumEditor {

    private LogSettingsDrawer _logSettingsDrawer;
    
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Build Features", EditorStyles.boldLabel);

      DrawScriptingDefineToggle(new GUIContent("Enable DebugDraw in Dev Builds", "Toggles QUANTUM_DRAW_SHAPES scripting define for the current platform to enable/disable debug draw in development builds."), "QUANTUM_DRAW_SHAPES", false);
      
      EditorGUI.BeginChangeCheck();
      DrawScriptingDefineToggle(new GUIContent("Enable Remote Task Profiler", "Toggles QUANTUM_ENABLE_REMOTE_PROFILER scripting define for the current platform"), "QUANTUM_ENABLE_REMOTE_PROFILER");
      if (EditorGUI.EndChangeCheck()) {
        // remove legacy define
        AssetDatabaseExt.UpdateScriptingDefineSymbol("QUANTUM_REMOTE_PROFILER", false);
      }
      
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Quantum 2D", EditorStyles.boldLabel);

      DrawScriptingDefineToggle(
        new GUIContent(
          "Enable Quantum XY", 
          "Toggles QUANTUM_XY scripting define to enable/disable Quantum XY."),
      "QUANTUM_XY",
        true
        );

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
      _logSettingsDrawer.DrawLayout(this, true);
    }

    private static bool DrawScriptingDefineToggle(GUIContent label, string define, bool allPlatforms = false) {
      bool? hasDefine;
      NamedBuildTarget buildTarget = default;
      if (allPlatforms) {
        hasDefine = AssetDatabaseExt.HasScriptingDefineSymbol(define);
      } else {
        buildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
        hasDefine = AssetDatabaseExt.HasScriptingDefineSymbol(buildTarget, define);
      }

      EditorGUI.BeginChangeCheck();
      EditorGUI.showMixedValue = hasDefine == null;
      bool value = EditorGUILayout.Toggle(label, hasDefine == true);
      EditorGUI.showMixedValue = false;
      if (EditorGUI.EndChangeCheck()) {
        if (allPlatforms) {
          AssetDatabaseExt.UpdateScriptingDefineSymbol(define, value);
        } else {
          AssetDatabaseExt.UpdateScriptingDefineSymbol(buildTarget, define, value);
        }
      }

      return value;
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumEntityPrototypeAssetObjectImporterEditor.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [CustomEditor(typeof(QuantumEntityPrototypeAssetObjectImporter))]
  [CanEditMultipleObjects]
  public class QuantumEntityPrototypeAssetObjectImporterEditor : ScriptedImporterEditor {
    
    [NonSerialized]
    private string _guidParseError;

    [NonSerialized]
    private UnityEngine.Object[] _prefabs;
    
    public override void OnInspectorGUI() {
      //QuantumEditorGUI.InjectScriptHeaderDrawer(this);
      QuantumEditorGUI.ScriptPropertyField(serializedObject);
      EditorGUILayout.HelpBox($"This is a read-only asset, its contents, location, name and Addressable settings are synced with the source prefab.", MessageType.Info);

      if (_prefabs == null) {
        _prefabs = targets.Cast<QuantumEntityPrototypeAssetObjectImporter>()
         .Select(x => x.assetPath)
         .Select(x => System.IO.File.ReadAllText(x))
         .Select(x => AssetDatabase.GUIDToAssetPath(x))
         .Where(x => !string.IsNullOrEmpty(x))
         .Select(x => AssetDatabase.LoadMainAssetAtPath(x))
         .Distinct()
         .ToArray();
      }

      using (new EditorGUI.DisabledScope(true))
      using (new QuantumEditorGUI.ShowMixedValueScope(_prefabs.Length > 1)) {
        EditorGUILayout.ObjectField("Source Prefab", _prefabs.FirstOrDefault(), typeof(GameObject), false);
      }

      EditorGUILayout.Space();

      DrawPropertiesExcluding(assetSerializedObject, QuantumEditorGUI.ScriptPropertyName);
      QuantumAssetObjectEditor.DrawAssetMeta(assetSerializedObject, ref _guidParseError);
    }

    protected override bool needsApplyRevert => false;
    override public bool showImportedObject => false;
    
    public static Quantum.EntityPrototype LoadPrototypeAssetForPrefab(string prefabPath) {
      var assetPath = QuantumEntityPrototypeAssetObjectImporter.GetPathForPrefab(prefabPath);
      return AssetDatabase.LoadAssetAtPath<Quantum.EntityPrototype>(assetPath);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumEntityPrototypeEditor.cs

#if !QUANTUM_DISABLE_ASSET_EDITORS
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumEntityPrototype), false)]
  [CanEditMultipleObjects]
  public class QuantumEntityPrototypeEditor : QuantumEditor {
    private static readonly HashSet<Type> excludedComponents = new HashSet<Type>(new[] {
      typeof(QPrototypeTransform2D), typeof(QPrototypeTransform2DVertical), typeof(QPrototypeTransform3D), typeof(QPrototypePhysicsCollider2D), typeof(QPrototypePhysicsBody2D), typeof(QPrototypePhysicsCollider3D), typeof(QPrototypePhysicsBody3D),
      typeof(QPrototypeNavMeshPathfinder), typeof(QPrototypeNavMeshSteeringAgent), typeof(QPrototypeNavMeshAvoidanceAgent), typeof(QPrototypeView),
    });

    private static readonly GUIContent[] transformPopupOptions = new[] { new GUIContent("2D"), new GUIContent("3D"), new GUIContent("None"), };

    private static Lazy<Skin> _skin = new Lazy<Skin>(() => new Skin());

    [NonSerialized] private bool _upToDateComponentEditors;

    private List<QuantumUnityComponentPrototypeEditor> _componentEditors = null;

    private static Skin skin => _skin.Value;

    private static readonly int[] transformPopupValues = new[] { (int)QuantumEntityPrototypeTransformMode.Transform2D, (int)QuantumEntityPrototypeTransformMode.Transform3D, (int)QuantumEntityPrototypeTransformMode.None };

    private class Skin {
      public readonly GUIStyle inspectorTitlebar = new GUIStyle("IN Title") { alignment = TextAnchor.MiddleLeft };
      public readonly float buttonWidth = 19.0f;

      public Color inspectorTitlebarBackground =>
        EditorGUIUtility.isProSkin ? new Color32(64, 64, 64, 255) : new Color32(222, 222, 222, 255);
    }

    private static readonly GUIContent physicsCollider2D = new GUIContent(nameof(Quantum.PhysicsCollider2D));
    private static readonly GUIContent physicsCollider3D = new GUIContent(nameof(Quantum.PhysicsCollider3D));
    private static readonly GUIContent physicsBody2D = new GUIContent(nameof(Quantum.PhysicsBody2D));
    private static readonly GUIContent physicsBody3D = new GUIContent(nameof(Quantum.PhysicsBody3D));
    private static readonly GUIContent navMeshPathfinder = new GUIContent(nameof(Quantum.NavMeshPathfinder));
    private static readonly GUIContent navMeshSteeringAgent = new GUIContent(nameof(Quantum.NavMeshSteeringAgent));
    private static readonly GUIContent navMeshAvoidanceAgent = new GUIContent(nameof(Quantum.NavMeshAvoidanceAgent));
    private static readonly GUIContent navMeshAvoidanceObstacle = new GUIContent(nameof(Quantum.NavMeshAvoidanceObstacle));

    private bool toolsPreviousState;

    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();

      var target = (QuantumEntityPrototype)this.target;
      QuantumEditorGUI.ScriptPropertyField(serializedObject);

      if (AssetDatabase.IsMainAsset(target.gameObject)) {
        var prefabPath = AssetDatabaseUtils.GetAssetPathOrThrow(target.gameObject);
        var asset = AssetDatabase.LoadAssetAtPath<Quantum.EntityPrototype>(prefabPath);

        // maybe a standalone one, then?
        if (!asset) {
          var standalonePath = QuantumEntityPrototypeAssetObjectImporter.GetPathForPrefab(prefabPath);
          asset = AssetDatabase.LoadAssetAtPath<Quantum.EntityPrototype>(standalonePath);
        }

        if (asset) {
          using (new QuantumEditorGUI.BoxScope(asset.GetType().Name)) {
            using (var assetSO = new SerializedObject(asset)) {
              EditorGUILayout.PropertyField(assetSO.FindPropertyOrThrow(nameof(EntityPrototype.Identifier)));
            }
          }
        }
      }

      if (Application.isPlaying) {
        EditorGUILayout.HelpBox("Prototypes are only used for entity instantiation. To inspect an actual entity check its EntityView.", MessageType.Info);
      }

      using (new EditorGUI.DisabledScope(Application.isPlaying)) {
        // draw enum popup manually, because this way we can reorder and not follow naming rules
        QuantumEntityPrototypeTransformMode? transformMode;
        {
          var prop = serializedObject.FindPropertyOrThrow(nameof(target.TransformMode));
          var rect = EditorGUILayout.GetControlRect();
          var label = new GUIContent("Transform");

          using (new QuantumEditorGUI.PropertyScope(rect, label, prop)) {
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.IntPopup(rect, label, prop.intValue, transformPopupOptions, transformPopupValues);

            if (EditorGUI.EndChangeCheck()) {
              prop.intValue = value;
              prop.serializedObject.ApplyModifiedProperties();
            }

            transformMode = prop.hasMultipleDifferentValues ? null : (QuantumEntityPrototypeTransformMode?)value;
          }
        }

        bool is2D = transformMode == QuantumEntityPrototypeTransformMode.Transform2D;
        bool is3D = transformMode == QuantumEntityPrototypeTransformMode.Transform3D;

        EditorGUI.BeginChangeCheck();

        try {
          if (is2D && IsEnabled(nameof(target.Transform2DVertical), new GUIContent("Transform2DVertical"), out var prop) && prop.isExpanded) {
            using var indent = new EditorGUI.IndentLevelScope();

            foreach (var p in prop.Children()) {
              EditorGUILayout.PropertyField(p);
            }
          }

          bool canHavePhysicsBody = false;

          if ((is2D || is3D) && IsEnabled(nameof(target.PhysicsCollider), is3D ? physicsCollider3D : physicsCollider2D, out prop)) {
            canHavePhysicsBody = true;

            if (prop.isExpanded) {
              using var indent = new EditorGUI.IndentLevelScope();

              foreach (var p in prop.Children()) {
                if (p.name == nameof(target.PhysicsCollider.SourceCollider)) {
                  if (is3D) {
                    QuantumEditorGUI.MultiTypeObjectField(p, new GUIContent(p.displayName), typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider));
                  } else {
                    QuantumEditorGUI.MultiTypeObjectField(p, new GUIContent(p.displayName), typeof(BoxCollider), typeof(SphereCollider), typeof(BoxCollider2D), typeof(CircleCollider2D), typeof(CapsuleCollider2D));
                  }

                  continue;
                }

                if (p.name == nameof(target.PhysicsCollider.Shape2D) && !is2D ||
                    p.name == nameof(target.PhysicsCollider.Shape3D) && !is3D) {
                  continue;
                }

                if (p.name == nameof(target.PhysicsCollider.IsTrigger)) {
                  canHavePhysicsBody = !p.boolValue;
                }

                EditorGUILayout.PropertyField(p);
              }
            }
          }

          if (canHavePhysicsBody && IsEnabled(nameof(target.PhysicsBody), is3D ? physicsBody3D : physicsBody2D, out prop) && prop.isExpanded) {
            using var indent = new EditorGUI.IndentLevelScope();

            foreach (var p in prop.Children()) {
              if (is2D) {
                if (p.name.EndsWith("3D", StringComparison.Ordinal) || p.name == nameof(target.PhysicsBody.RotationFreeze)) {
                  continue;
                }
              } else {
                if (p.name.EndsWith("2D", StringComparison.Ordinal)) {
                  continue;
                }
              }

              EditorGUILayout.PropertyField(p);
            }
          }

          if ((is2D || is3D) && IsEnabled(nameof(target.NavMeshPathfinder), navMeshPathfinder, out prop)) {
            // NavMeshes can be pointed to in 3 ways: scene reference, asset ref and scene name
            if (prop.isExpanded) {
              using var indent = new EditorGUI.IndentLevelScope();

              foreach (var p in prop.Children()) {
                EditorGUILayout.PropertyField(p);
              }
            }

            if (IsEnabled(nameof(target.NavMeshSteeringAgent), navMeshSteeringAgent, out prop)) {
              if (prop.isExpanded) {
                using var indent = new EditorGUI.IndentLevelScope();

                foreach (var p in prop.Children()) {
                  EditorGUILayout.PropertyField(p);
                }
              }

              if (IsEnabled(nameof(target.NavMeshAvoidanceAgent), navMeshAvoidanceAgent, out prop)) {
                if (prop.isExpanded) {
                  using var indent = new EditorGUI.IndentLevelScope();

                  foreach (var p in prop.Children()) {
                    EditorGUILayout.PropertyField(p);
                  }
                }
              }
            }
          }

          // View can be either taken from same GameObject or fallback to asset ref
          {
            var viewProperty = serializedObject.FindPropertyOrThrow(nameof(target.View));
            var hasView = target.TryGetComponent(out QuantumEntityView _);
            var rect = EditorGUILayout.GetControlRect(true);
            var label = new GUIContent(viewProperty.displayName);

            using (new QuantumEditorGUI.PropertyScope(rect, label, viewProperty)) {
              rect = EditorGUI.PrefixLabel(rect, label);

              using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                if (hasView) {
                  EditorGUI.LabelField(rect, "Self");
                } else {
                  EditorGUI.PropertyField(rect, viewProperty, GUIContent.none);
                }
              }
            }
          }

          // add new component dropdown
          if (QuantumEditorSettings.Get(x => x.EntityComponentInspectorMode) == QuantumEntityComponentInspectorMode.ShowMonoBehaviours) {
            using (new EditorGUILayout.HorizontalScope()) {
              GUIStyle style = EditorStyles.miniPullDown;
              var content = new GUIContent("Add Entity Component");
              var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(content, style));

              if (EditorGUI.DropdownButton(rect, content, FocusType.Keyboard, style)) {
                PopupWindow.Show(rect, CreatePopupContent());
              }
            }
          }
        } finally {
          if (EditorGUI.EndChangeCheck()) {
            serializedObject.ApplyModifiedProperties();
          }
        }
      }

      try {
        target.PreSerialize();
      } catch (System.Exception ex) {
        EditorGUILayout.HelpBox(ex.Message, MessageType.Error);
      }

      target.CheckComponentDuplicates(msg => {
        EditorGUILayout.HelpBox(msg, MessageType.Warning);
      });

      if (QuantumEditorSettings.Get(x => x.EntityComponentInspectorMode) != QuantumEntityComponentInspectorMode.ShowMonoBehaviours) {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
          if (!_upToDateComponentEditors) {
            var groups = targets.Cast<QuantumEntityPrototype>()
              .SelectMany(x => x.GetComponents<QuantumUnityComponentPrototype>())
              .GroupBy(x => x.GetType());

            _upToDateComponentEditors = true;
            _componentEditors = new List<QuantumUnityComponentPrototypeEditor>();

            foreach (var group in groups) {
              if (group.Count() == targets.Length) {
                _componentEditors.Add((QuantumUnityComponentPrototypeEditor)Editor.CreateEditor(group.ToArray()));
              }
            }
          }

          {
            var labelRect = EditorGUILayout.GetControlRect(true);
            EditorGUI.LabelField(labelRect, "Entity Components", EditorStyles.boldLabel);

            var buttonRect = labelRect.AddX(labelRect.width).AddX(-skin.buttonWidth).SetWidth(skin.buttonWidth);

            if (GUI.Button(buttonRect, "+", EditorStyles.miniButton)) {
              PopupWindow.Show(buttonRect, CreatePopupContent());
            }
          }

          using (new EditorGUI.IndentLevelScope()) {
            foreach (var editor in _componentEditors) {
              var so = new SerializedObject(editor.targets);
              var sp = so.GetIterator();

              var rect = GUILayoutUtility.GetRect(GUIContent.none, skin.inspectorTitlebar);
              sp.isExpanded = EditorGUI.InspectorTitlebar(rect, sp.isExpanded, editor.targets, true);

              // draw over the default label, as it contains useless noise
              Rect textRect = new Rect(rect.x + 35, rect.y, rect.width - 100, rect.height);

              if (Event.current.type == EventType.Repaint) {
                using (new QuantumEditorGUI.ColorScope(skin.inspectorTitlebarBackground)) {
                  var texRect = textRect;
                  texRect.y += 2;
                  texRect.height -= 2;
                  GUI.DrawTextureWithTexCoords(texRect, Texture2D.whiteTexture, new Rect(0.5f, 0.5f, 0.0f, 0.0f), false);
                }

                skin.inspectorTitlebar.Draw(textRect, editor.target.GetType().Name, false, false, false, false);
              }

              if (sp.isExpanded) {
                EditorGUI.BeginChangeCheck();

                QuantumEditorGUI.SetScriptFieldHidden(editor, true);
                editor.DrawInternalGUI();
                QuantumEditorGUI.SetScriptFieldHidden(editor, false);

                foreach (QuantumUnityComponentPrototype t in editor.targets) {
                  t.Refresh();
                }

                editor.serializedObject.Update();

                if (EditorGUI.EndChangeCheck()) {
                  editor.serializedObject.ApplyModifiedProperties();
                }
              }
            }
          }
        }
      }
    }

    private static Type GetComponentType(Type componentWrapperType) {
      return componentWrapperType
        .GetInterfaces()
        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuantumUnityPrototypeWrapperForComponent<>))
        .Select(i => i.GetGenericArguments()[0])
        .SingleOrDefault();
    }

    private QuantumTypeSelectorPopupContent CreatePopupContent() {
      var popupContent = new QuantumTypeSelectorPopupContent(type => {
        foreach (Component t in targets) {
          Undo.AddComponent(t.gameObject, type);
        }

        Repaint();
      });

      var presentComponents = new HashSet<Type>(
        targets.Cast<QuantumEntityPrototype>()
          .SelectMany(x => x.GetComponents<QuantumUnityComponentPrototype>())
          .Select(x => GetComponentType(x.GetType()))
          .Where(x => x != null)
      );

      var types = TypeCache.GetTypesDerivedFrom(typeof(MonoBehaviour))
        .Where(x => !x.IsDefined(typeof(ObsoleteAttribute)))
        .Where(x => !x.IsAbstract && !x.IsGenericTypeDefinition)
        .Where(x => UnityInternal.EditorGUIUtility.GetScript(x.FullName))
        .Where(x => !excludedComponents.Contains(x))
        .Where(x => {
          var componentType = GetComponentType(x);

          if (componentType == null) {
            return false;
          }

          if (presentComponents.Contains(componentType)) {
            return false;
          }

          return true;
        })
        .Select(x => new { ComponentType = GetComponentType(x), PrototypeScriptType = x })
        .GroupBy(x => x.ComponentType, x => x.PrototypeScriptType)
        .OrderBy(x => x.Key.Name);

      foreach (var group in types) {
        var label = group.Key.GetCSharpTypeName(includeNamespace: false);

        if (group.Count() > 1) {
          popupContent.BeginGroup(label);

          foreach (var source in group) {
            popupContent.AddType($"{label} ({source.GetCSharpTypeName(includeNamespace: false)})", source);
          }

          popupContent.EndGroup();
        } else {
          popupContent.AddType(label, group.Single());
        }
      }

      return popupContent;
    }

    private bool IsEnabled(string propName, GUIContent label, out SerializedProperty property) {
      property = serializedObject.FindPropertyOrThrow(propName);

      var isEnabledProperty = property.Copy();
      isEnabledProperty.Next(true);
      QuantumEditorLog.Assert(isEnabledProperty.name == "IsEnabled");
      var position = EditorGUILayout.GetControlRect();

      EditorGUI.PropertyField(position, isEnabledProperty, label);

      if (isEnabledProperty.boolValue) {
        // foldout
        UnityInternal.EditorGUI.DefaultPropertyField(position, property, QuantumEditorGUI.WhitespaceContent);
      }

      return isEnabledProperty.boolValue;
    }

    [CustomPropertyDrawer(typeof(QuantumEntityPrototype.NavMeshSpec))]
    private class NavMeshSpecDrawer : PropertyDrawer {
      private static readonly Lazy<GUIStyle> watermarkStyle = new Lazy<GUIStyle>(() => {
        var result = new GUIStyle(EditorStyles.miniLabel);
        result.alignment = TextAnchor.MiddleRight;
        result.contentOffset = new Vector2(-2, 0);
        Color c = result.normal.textColor;
        c.a = 0.6f;
        result.normal.textColor = c;
        return result;
      });

      public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        var referenceProp = property.FindPropertyRelativeOrThrow("Reference");
        var assetProp = property.FindPropertyRelativeOrThrow("Asset");
        var nameProp = property.FindPropertyRelativeOrThrow("Name");

        EditorGUI.BeginChangeCheck();
        using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
          var rect = EditorGUI.PrefixLabel(position, label);

          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            if (referenceProp.objectReferenceValue != null) {
              EditorGUI.PropertyField(rect, referenceProp, GUIContent.none);
            } else if (assetProp.FindPropertyRelativeOrThrow("Id.Value").longValue > 0) {
              EditorGUI.PropertyField(rect, assetProp, GUIContent.none);
            } else if (!string.IsNullOrEmpty(nameProp.stringValue)) {
              EditorGUI.PropertyField(rect, nameProp, GUIContent.none);
              GUI.Label(rect, "(NavMesh name)", watermarkStyle.Value);
            } else {
              rect.width /= 3;
              EditorGUI.PropertyField(rect, referenceProp, GUIContent.none);
              EditorGUI.PropertyField(rect.AddX(rect.width), assetProp, GUIContent.none);
              EditorGUI.PropertyField(rect.AddX(2 * rect.width), nameProp, GUIContent.none);
              GUI.Label(rect.AddX(2 * rect.width), "(NavMesh name)", watermarkStyle.Value);
            }
          }
        }

        if (EditorGUI.EndChangeCheck()) {
          property.serializedObject.ApplyModifiedProperties();
        }
      }

      public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return EditorGUIUtility.singleLineHeight;
      }
    }
  }
}
#endif

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumEntityViewEditor.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;

  [CustomEditor(typeof(QuantumEntityView), true)]
  [CanEditMultipleObjects]
  public class QuantumEntityViewEditor : QuantumEditor {

    private QuantumSimulationObjectInspectorState _inspectorState = new QuantumSimulationObjectInspectorState();
    private QuantumSimulationObjectInspector _inspector = new QuantumSimulationObjectInspector();
    private bool _foldout;

    public override unsafe void OnInspectorGUI() {

      base.PrepareOnInspectorGUI();
      base.DrawScriptPropertyField();

      var target = (QuantumEntityView)base.target;

      if (!serializedObject.isEditingMultipleObjects) {
        if (!EditorApplication.isPlaying) {
          bool isOnScene = target.gameObject.scene.IsValid() && PrefabStageUtility.GetPrefabStage(target.gameObject) == null;

          if (isOnScene) {
            bool hasPrototype = target.gameObject.GetComponent<QuantumEntityPrototype>();
            if (!hasPrototype) {
              using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                EditorGUILayout.HelpBox($"This {nameof(QuantumEntityView)} will never be bound to any Entity. Add {nameof(QuantumEntityPrototype)} and bake map data.", MessageType.Warning);
                if (GUILayout.Button("Fix")) {
                  Undo.AddComponent<QuantumEntityPrototype>(target.gameObject);
                }
              }
            }
          }
        }
        
        if (AssetDatabase.IsMainAsset(target.gameObject)) {
          var asset = AssetDatabase.LoadAssetAtPath<Quantum.EntityView>(AssetDatabase.GetAssetPath(target.gameObject));
          if (asset) {
            using (new QuantumEditorGUI.BoxScope(asset.GetType().Name)) {
              var prop = new SerializedObject(asset).FindPropertyOrThrow(nameof(asset.Identifier));
              EditorGUILayout.PropertyField(prop);
            }
          }
        }
      }

      QuantumEditorGUI.SetScriptFieldHidden(this, true);
      base.DrawDefaultInspector();
      QuantumEditorGUI.SetScriptFieldHidden(this, false);

      if (QuantumRunner.Default == null)
        return;

      if (!serializedObject.isEditingMultipleObjects) {
        using (new EditorGUILayout.HorizontalScope()) {
          EditorGUILayout.PrefixLabel("Quantum Entity Id");
          EditorGUILayout.SelectableLabel(target.EntityRef.ToString(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }


        _foldout = EditorGUILayout.Foldout(_foldout, "Quantum Entity Root");
        if (_foldout) {
          using (new GUILayout.VerticalScope(GUI.skin.box)) {
            _inspectorState.FromEntity(QuantumRunner.Default.Game.Frames.Predicted, target.EntityRef);
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
              _inspector.DoGUILayout(_inspectorState, false);
            }
          }
        }
      }

    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumInstantReplayDemoEditor.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A custom editor to for the instant replay demo. 
  /// Displays start and stop buttons.
  /// </summary>
  [CustomEditor(typeof(QuantumInstantReplayDemo))]
  public class QuantumInstantReplayDemoEditor : QuantumEditor {

    private new QuantumInstantReplayDemo target => (QuantumInstantReplayDemo)base.target;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
      EditorGUILayout.HelpBox($"Use QuantumRunner.StartParameters.InstantReplaySettings to define the maximum replay length and the snapshot sampling rate.", MessageType.Info);

      if (EditorApplication.isPlaying) {
        if (target.Button_StartInstantReplay && GUILayout.Button("Start")) {
          target.Editor_StartInstantReplay();
        }
        if (target.Button_StopInstantReplay && GUILayout.Button("Stop")) {
          target.Editor_StopInstantReplay();
        }
      }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override bool RequiresConstantRepaint() {
      return true;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumMapDataEditor.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumMapData), true)]
  public class MapDataEditor : QuantumEditor {

    private Editor _mapAssetEditor;

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      var data = (QuantumMapData)target;
      data.transform.position = Vector3.zero;
      
      using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
        using (new QuantumEditorGUI.BackgroundColorScope(Color.green)) {

          var buttonHeight = GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.5f);

          if (GUILayout.Button("Bake Map Only", buttonHeight)) {
            HandleBakeButton(data, QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
          }
          if (GUILayout.Button("Bake Map Prototypes", buttonHeight)) {
            HandleBakeButton(data, QuantumMapDataBakeFlags.BakeMapPrototypes);
          }
          if (GUILayout.Button("Bake All", buttonHeight)) {
            HandleBakeButton(data, data.BakeAllMode);
          }
        }

        using (var checkScope = new EditorGUI.ChangeCheckScope()) {
          data.BakeAllMode = (QuantumMapDataBakeFlags)EditorGUILayout.EnumFlagsField("Bake All Mode", data.BakeAllMode);
          if (checkScope.changed) {
            EditorUtility.SetDirty(data);
          }
        }
      }

      var asset = data.GetAsset(true);
      if (asset) {
        if (_mapAssetEditor == null || _mapAssetEditor.target != asset) {
          _mapAssetEditor = CreateEditor(asset);
        }
        
        EditorGUILayout.Space();
        
        var sp = _mapAssetEditor.serializedObject.GetIterator();
        sp.isExpanded = EditorGUILayout.Foldout(sp.isExpanded, "Map Asset", true);

        if (sp.isExpanded) {
          using (new EditorGUI.IndentLevelScope()) {
            QuantumEditorGUI.SetScriptFieldHidden(_mapAssetEditor, true);
            _mapAssetEditor.OnInspectorGUI();
            QuantumEditorGUI.SetScriptFieldHidden(_mapAssetEditor, false);
          }
        }

      } else {
        _mapAssetEditor = null;
      }
    }

    void HandleBakeButton(QuantumMapData data, QuantumMapDataBakeFlags bakeFlags) {
      Undo.RecordObject(target, "Bake Map: " + bakeFlags);

      QuantumEditorAutoBaker.BakeMap(data, bakeFlags);

      GUIUtility.ExitGUI();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumMapNavMeshUnityEditor.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  using Object = UnityEngine.Object;

  [CustomEditor(typeof(QuantumMapNavMeshUnity))]
  public partial class QuantumMapNavMeshUnityEditor : QuantumEditor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
#if QUANTUM_ENABLE_AI_NAVIGATION

      var data = ((QuantumMapNavMeshUnity)target).Settings;
      if (data.ImportRegionMode != NavmeshRegionImportMode.Disabled) {
        EditorGUILayout.LabelField(new GUIContent("Convert Unity Areas To Quantum Region:", "Select what Unity NavMesh areas are used to generated Quantum toggleable regions. At least one must be selected if import regions is enabled. Walkable cannot be disabled."));

        if (data.RegionAreaIds == null) {
          data.RegionAreaIds = new List<int>();
        }

        using (new EditorGUI.IndentLevelScope(1)) {
          var unityNavmeshAreaMap = QuantumNavMesh.CreateUnityNavmeshAreaMap();
          unityNavmeshAreaMap.Remove(0);
          unityNavmeshAreaMap.Remove(1);

          foreach (var tuple in unityNavmeshAreaMap) {
            var isIncluded = data.RegionAreaIds.Contains(tuple.Key);
            if (GUI.Toggle(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), isIncluded, tuple.Value)) {
              if (isIncluded == false) {
                data.RegionAreaIds.Add(tuple.Key);
              }
            }
            else {
              if (isIncluded == true) {
                data.RegionAreaIds.Remove(tuple.Key);
              }
            }
          }
        }
      }
#endif
    }
    
    /// <summary>
    /// Set the static <see cref="QuantumNavMesh.DefaultMinAgentRadius"/>
    /// to the smallest agent radius found in the Unity NavMesh settings.
    /// </summary>
    public static void UpdateDefaultMinAgentRadius() {
      var settingsValue = QuantumNavMesh.DefaultMinAgentRadius;
      for (int i = 0; i < UnityEngine.AI.NavMesh.GetSettingsCount(); i++) {
        settingsValue = Math.Min(settingsValue, UnityEngine.AI.NavMesh.GetSettingsByIndex(i).agentRadius);
      }
      QuantumNavMesh.DefaultMinAgentRadius = settingsValue;
    }

    /// <summary>
    /// Runs Unity navmesh baking an all surfaces found in the given GameObject.
    /// </summary>
    /// <param name="go">Game object root</param>
    /// <returns><see langword="true"/> to indicate that all navmeshes have been baked.</returns>
    public static bool BakeUnityNavmesh(GameObject go) {
#if QUANTUM_ENABLE_AI_NAVIGATION
      // Collect surfaces
      List<GameObject> surfaces = new List<GameObject>();

      // Go through MapNavMeshUnity scripts
      var unityNavmeshes = new List<QuantumMapNavMeshUnity>();
      go.GetComponents(unityNavmeshes);
      go.GetComponentsInChildren(unityNavmeshes);
      foreach (var unityNavmesh in unityNavmeshes) {
        surfaces.AddRange(unityNavmesh.NavMeshSurfaces);
      }

      try {
        // Clear any navmesh surface data
        var instance = Unity.AI.Navigation.Editor.NavMeshAssetManager.instance;
        var surfaceObjects = surfaces.Select(s => s.GetComponent<Unity.AI.Navigation.NavMeshSurface>()).ToArray();
        foreach (var s in surfaceObjects) {
          var assetToDelete = (Object)typeof(Unity.AI.Navigation.Editor.NavMeshAssetManager).GetMethod("GetNavMeshAssetToDelete", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(instance, new object[] { s });
          if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(assetToDelete)) == false) {
            // Make sure to clear correctly to remove associated Unity navmesh asset
            typeof(Unity.AI.Navigation.Editor.NavMeshAssetManager).GetMethod("ClearSurface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(instance, new object[] { s });
          } else {
            // Only clear the surface data
            typeof(Unity.AI.Navigation.NavMeshSurface).GetMethod("RemoveData").Invoke(s, null);
          }
        }
      } catch (System.Exception e) {
        QuantumEditorLog.Warn($"Failed to reset Unity navmesh surfaces due to an exception: {e.Message}");
      }

      // Execute bake on each surface
      foreach (var gameObject in surfaces) {
        var navMeshSurface = gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        navMeshSurface.BuildNavMesh();
      }

      return false;
#else
#if UNITY_2023_3_OR_NEWER
      throw new Exception("Calling NavMeshBuilder is not supported anymore. Switch to Navmesh surfaces or implement custom global navmesh baking during OnBeforeBake()");
#else
      // Is NavMesh Surfaces is not installed the global navmesh baking is triggered
      UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
      return true;
#endif
#endif
    }

    /// <summary>
    /// Searches for Unity namveshes surfaces and clears their data.
    /// </summary>
    /// <param name="go">Game object to clear.</param>
    /// <returns><see langword="true"/> to stop processing any other game objects.</returns>
    public static bool ClearUnityNavmesh(GameObject go) {
#if QUANTUM_ENABLE_AI_NAVIGATION
      // Collect surfaces
      List<GameObject> surfaces = new List<GameObject>();

      // Go through MapNavMeshUnity scripts
      var unityNavmeshes = new List<QuantumMapNavMeshUnity>();
      go.GetComponents(unityNavmeshes);
      go.GetComponentsInChildren(unityNavmeshes);
      foreach (var unityNavmesh in unityNavmeshes) {
        surfaces.AddRange(unityNavmesh.NavMeshSurfaces);
      }

      foreach (var gameObject in surfaces) {
        //NavMeshAssetManagerType.instance.ClearSurfaces()
        var navMeshSurface = gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        navMeshSurface.RemoveData();
        navMeshSurface.navMeshData = null;
      }

      return false;
#else
#if UNITY_2023_3_OR_NEWER
      throw new Exception("Calling NavMeshBuilder is not supported anymore. Switch to Navmesh surfaces or implement custom global navmesh clearing during OnBakeNavMesh()");
#else
      // Is NavMesh Surfaces is not installed the global navmesh baking is triggered
      UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
      return true;
#endif
#endif
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumNavMeshRegionEditor.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumNavMeshRegion))]
  public class QuantumNavMeshRegionEditor : QuantumEditor {
    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();
      var data = (QuantumNavMeshRegion)target;

      using (var change = new EditorGUI.ChangeCheckScope()) {
        data.Id = EditorGUILayout.TextField("Id", data.Id);
        data.CastRegion = (QuantumNavMeshRegion.RegionCastType)EditorGUILayout.EnumPopup("Cast Region", data.CastRegion);

        if (data.CastRegion == QuantumNavMeshRegion.RegionCastType.CastRegion) {
          using (new GUILayout.HorizontalScope()) {
            using (new EditorGUI.DisabledScope(data.OverwriteCost == false)) {
              EditorGUI.BeginChangeCheck();
              EditorGUILayout.PropertyField(serializedObject.FindPropertyOrThrow("Cost"), new GUIContent("Cost"));
              if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
              }
            }
            data.OverwriteCost = EditorGUILayout.Toggle("Overwrite", data.OverwriteCost);
          }
        }

        if (change.changed) {
          EditorUtility.SetDirty(target);
        }
      }

      if (string.IsNullOrEmpty(data.Id)) {
        EditorGUILayout.HelpBox("Id is not set", MessageType.Error);
      }
      else if (data.Id == "Default") {
        EditorGUILayout.HelpBox("'Default' is not allowed", MessageType.Error);
      }

#if !UNITY_2023_3_OR_NEWER

      if (data.CastRegion == QuantumNavMeshRegion.RegionCastType.CastRegion) {

        QuantumEditorGUI.Header("NavMesh Editor Helper");

        if (data.gameObject.GetComponent<MeshRenderer>() == null) {
          EditorGUILayout.HelpBox("MapNavMeshRegion requires a MeshRenderer to be able to cast a region onto the navmesh", MessageType.Error);
        }

        using (var change = new EditorGUI.ChangeCheckScope()) {
#if !UNITY_2022_2_OR_NEWER
          var currentFlags = GameObjectUtility.GetStaticEditorFlags(data.gameObject);
          var currentNavigationStatic = (currentFlags & StaticEditorFlags.NavigationStatic) == StaticEditorFlags.NavigationStatic;
          var newNavigationStatic = EditorGUILayout.Toggle("Toggle Static Flag", currentNavigationStatic);
          if (currentNavigationStatic != newNavigationStatic) {
            if (newNavigationStatic)
              GameObjectUtility.SetStaticEditorFlags(data.gameObject, currentFlags | StaticEditorFlags.NavigationStatic);
            else
              GameObjectUtility.SetStaticEditorFlags(data.gameObject, currentFlags & ~StaticEditorFlags.NavigationStatic);
          }
#endif

          int unityAreaId = 0;
#if QUANTUM_ENABLE_AI_NAVIGATION
          var modifier = data.GetComponent<Unity.AI.Navigation.NavMeshModifier>() ?? data.GetComponentInParent<Unity.AI.Navigation.NavMeshModifier>();
          if (modifier != null) {
            using (new EditorGUI.DisabledGroupScope(true)) {
              EditorGUILayout.ObjectField("NavMesh Modifier GameObject", modifier.gameObject, typeof(GameObject), true);
              EditorGUILayout.Popup("Area", GetNavMeshAreaIndex(modifier.area), GameObjectUtility.GetNavMeshAreaNames());
            }
          }
          else {
            EditorGUILayout.LabelField("The NavMesh Area is defined by the NavMeshModifier script. No such script found in parents.");
          }
#else
          unityAreaId = GameObjectUtility.GetNavMeshArea(data.gameObject);
          var map = GameObjectUtility.GetNavMeshAreaNames();
          var currentIndex = GetNavMeshAreaIndex(unityAreaId);
          var newIndex = EditorGUILayout.Popup("Unity NavMesh Area", currentIndex, map);
          if (currentIndex != newIndex) {
            unityAreaId = UnityEngine.AI.NavMesh.GetAreaFromName(map[newIndex]);
            GameObjectUtility.SetNavMeshArea(data.gameObject, unityAreaId);
          }

          if (newIndex < 3 || newIndex >= map.Length) {
            EditorGUILayout.HelpBox("Unity NavMesh Area not valid", MessageType.Error);
          }

#if !UNITY_2022_2_OR_NEWER
          if (newNavigationStatic == false) {
            EditorGUILayout.HelpBox("Unity Navigation Static has to be enabled", MessageType.Error);
          }
#endif
#endif // QUANTUM_ENABLE_AI_NAVIGATION

          if (data.CastRegion == QuantumNavMeshRegion.RegionCastType.CastRegion && data.OverwriteCost == false) {
            data.Cost = UnityEngine.AI.NavMesh.GetAreaCost(unityAreaId).ToFP();
          }

          if (change.changed) {
            EditorUtility.SetDirty(target);
          }
        }
      }
#endif // !UNITY_2023_3_OR_NEWER
          }

        private static int GetNavMeshAreaIndex(int areaId) {
#if UNITY_2023_3_OR_NEWER
      var map = UnityEngine.AI.NavMesh.GetAreaNames();
#else
      var map = GameObjectUtility.GetNavMeshAreaNames();
#endif
      var index = 0;
      for (index = 0; index < map.Length;) {
        if (UnityEngine.AI.NavMesh.GetAreaFromName(map[index]) == areaId)
          break;
        index++;
      }

      return index;
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumRunnerEditor.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumRunnerBehaviour))]
  public class QuantumRunnerEditor : QuantumEditor {

    private new QuantumRunnerBehaviour target => (QuantumRunnerBehaviour)base.target;

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      if (target?.Runner != null ) {
        QuantumEditorGUI.Header("Quantum Runner");
        EditorGUILayout.LabelField("Id", target.Runner.Id);
        EditorGUILayout.LabelField("Running", target.Runner.IsRunning.ToString());
        EditorGUILayout.LabelField("Verified Predicted", target.Runner.Session?.FramePredicted?.Number.ToString());
        EditorGUILayout.LabelField("Verified Frame", target.Runner.Session?.FrameVerified?.Number.ToString());
        EditorGUILayout.LabelField("Predicted Frames", target.Runner.Session?.PredictedFrames.ToString());
        EditorGUILayout.LabelField("Ping", target.Runner.Session?.Stats.Ping.ToString());
        EditorGUILayout.LabelField("Simulate Time", (target.Runner.Session != null ? Math.Round(target.Runner.Session.Stats.UpdateTime * 1000, 2) : 0) + " ms");
        EditorGUILayout.LabelField("Input Offset", target.Runner.Session?.Stats.Offset.ToString());

        target.Runner.HideGizmos = EditorGUILayout.Toggle(nameof(target.Runner.HideGizmos), target.Runner.HideGizmos);
        target.Runner.DeltaTimeType = (SimulationUpdateTime)EditorGUILayout.EnumPopup("DeltaTimeType", target.Runner.DeltaTimeType);

        if (GUILayout.Button("Open State Inspector")) {
          QuantumStateInspector.ShowWindow(false);
        }
      }

      if (target?.Runner?.NetworkClient != null) {
        var client = target.Runner.NetworkClient;
        QuantumEditorGUI.Header("Photon Room");
        EditorGUILayout.LabelField("Region", client.CurrentRegion);
        EditorGUILayout.LabelField("AppVersion", client.AppSettings.AppVersion);

        if (client.InRoom) {
          EditorGUILayout.LabelField("Room Name", client.CurrentRoom.Name);
          EditorGUILayout.LabelField("IsOpen", client.CurrentRoom.IsOpen.ToString());
          EditorGUILayout.LabelField("IsVisible", client.CurrentRoom.IsVisible.ToString());
          EditorGUILayout.LabelField("MaxPlayers", client.CurrentRoom.MaxPlayers.ToString());
          EditorGUILayout.LabelField("EmptyRoomTtl", client.CurrentRoom.EmptyRoomTtl.ToString());
          EditorGUILayout.LabelField("PlayerTtl", client.CurrentRoom.PlayerTtl.ToString());
        }

        EditorGUILayout.LabelField("CrcEnabled", client.RealtimePeer.CrcEnabled.ToString());
      }
    }

    public override bool RequiresConstantRepaint() {
      return true;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumRunnerLocalDebugEditor.cs

namespace Quantum.Editor {
  using UnityEditor;

  [CustomEditor(typeof(QuantumRunnerLocalDebug))]
  public class QuantumRunnerLocalDebugEditor : QuantumEditor {

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
      var data = (QuantumRunnerLocalDebug)target;
      QuantumAddRuntimePlayersEditor.DrawAddRemoveTools(QuantumRunner.Default?.Game, data.LocalPlayers);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumRunnerLocalReplayEditor.cs

namespace Quantum.Editor {
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumRunnerLocalReplay))]
  public class QuantumRunnerLocalReplayEditor : QuantumEditor {

    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();

      var data = (QuantumRunnerLocalReplay)target;

      var oldReplayFile = data.ReplayFile;

      if (DrawDefaultInspector() && oldReplayFile != data.ReplayFile) {
        data.DatabaseFile = null;

        if (data.ReplayFile != null && data.DatabaseFile == null) {
          var assetPath = AssetDatabase.GetAssetPath(data.ReplayFile);
          var databaseFilepath = $"{Path.GetDirectoryName(assetPath)}/{Path.GetFileNameWithoutExtension(assetPath)}-DB{Path.GetExtension(assetPath)}";
          data.DatabaseFile = AssetDatabase.LoadAssetAtPath<TextAsset>(databaseFilepath);
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumRunnerLocalSavegameEditor.cs

namespace Quantum.Editor {
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumRunnerLocalSavegame))]
  public class QuantumRunnerLocalSavegameEditor : QuantumEditor {

    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();
      
      var data = (QuantumRunnerLocalSavegame)target;

      var oldSavegameFile = data.SavegameFile;

      if (DrawDefaultInspector() && oldSavegameFile != data.SavegameFile) {
        data.DatabaseFile = null;

        if (data.SavegameFile != null && data.DatabaseFile == null) {
          var assetPath = AssetDatabase.GetAssetPath(data.SavegameFile);
          var databaseFilepath = $"{Path.GetDirectoryName(assetPath)}/{Path.GetFileNameWithoutExtension(assetPath)}-DB{Path.GetExtension(assetPath)}";
          data.DatabaseFile = AssetDatabase.LoadAssetAtPath<TextAsset>(databaseFilepath);
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/QuantumUnityComponentPrototypeEditor.cs

#if !QUANTUM_DISABLE_ASSET_EDITORS
namespace Quantum.Editor {
  using System.Diagnostics;
  using UnityEditor;

  [CustomEditor(typeof(QuantumUnityComponentPrototype), true)]
  [CanEditMultipleObjects]
  public class QuantumUnityComponentPrototypeEditor : QuantumEditor {

    protected override void OnEnable() {
      base.OnEnable();
      
      if (QuantumEditorSettings.Get(x => x.EntityComponentInspectorMode) == QuantumEntityComponentInspectorMode.InlineInEntityPrototypeAndHideMonoBehaviours) {
        UnityInternal.Editor.InternalSetHidden(this, true);
      }
    }


    public sealed override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();

      if (QuantumEditorSettings.Get(x => x.EntityComponentInspectorMode) != QuantumEntityComponentInspectorMode.ShowMonoBehaviours) {
        bool comparisonPopup = false;
        var  trace           = new StackFrame(1);
        var  declaringType   = trace?.GetMethod()?.DeclaringType;
        if (declaringType != null && declaringType?.Name.EndsWith("ComparisonViewPopup") == true) {
          comparisonPopup = true;
        }

        if (!comparisonPopup) {
          QuantumEditorGUI.ScriptPropertyField(serializedObject);
          return;
        }
      }

      EditorGUI.BeginChangeCheck();

      DrawInternalGUI();
      
      base.DrawEditorButtons();

      foreach (QuantumUnityComponentPrototype t in targets) {
        t.Refresh();
      }
      serializedObject.Update();
      
      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }
    }

    public virtual void DrawInternalGUI() {
      base.DrawDefaultInspector();
    }
  }
}
#endif

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/RangeExAttributeDrawer.FP.cs

namespace Quantum.Editor {
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  partial class RangeExAttributeDrawer {
    partial void GetFloatValue(SerializedProperty property, ref float? floatValue) {
      if (property.propertyType != SerializedPropertyType.Generic && property.type != typeof(FP).FullName) {
        return;
      }
      
      var rawValueProperty = property.FindPropertyRelativeOrThrow(nameof(FP.RawValue));
      floatValue = (float)FP.FromRaw(rawValueProperty.longValue).AsRoundedDouble;
    }
    
    partial void ApplyFloatValue(SerializedProperty property, float floatValue) {
      var rawValueProperty = property.FindPropertyRelativeOrThrow(nameof(FP.RawValue));
      rawValueProperty.longValue = FP.FromRoundedFloat_UNSAFE(floatValue).RawValue;
    }

    partial void DrawFloatValue(SerializedProperty property, Rect position, GUIContent label, ref float floatValue) {
      var rawValueProperty = property.FindPropertyRelativeOrThrow(nameof(FP.RawValue));
      rawValueProperty.longValue = FP.FromRoundedFloat_UNSAFE(floatValue).RawValue;
      
      EditorGUI.BeginChangeCheck();
      FPPropertyDrawer.DrawIntPropertyAsFP(position, rawValueProperty, label, fieldInfo);
      if (EditorGUI.EndChangeCheck()) {
        floatValue = (float)FP.FromRaw(rawValueProperty.longValue).AsRoundedDouble;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticBoxCollider2DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
  using UnityEditor;

  [CustomEditor(typeof(QuantumStaticBoxCollider2D))]
  public class QuantumStaticBoxCollider2DEditor : QuantumStaticCollider2DEditorBase {
    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      DrawColliderSizeGizmos(behaviour as QuantumStaticBoxCollider2D);
    }

    private void DrawColliderSizeGizmos(QuantumStaticBoxCollider2D collider) {
      QuantumColliderHandles.Rectangle(collider, collider.PositionOffset, collider.RotationOffset, ref collider.Size);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticBoxCollider3DEditor.cs

namespace Quantum.Editor {
  using UnityEditor;

#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D 
  [CustomEditor(typeof(QuantumStaticBoxCollider3D))]
  public class QuantumStaticBoxCollider3DEditor : QuantumStaticCollider3DEditorBase {
    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      var collider = (QuantumStaticBoxCollider3D)behaviour;
      QuantumColliderHandles.Box(collider, collider.PositionOffset, collider.RotationOffset, ref collider.Size);
    }
  }
#endif
}


#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticCapsuleCollider2DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
  using UnityEditor;

  [CustomEditor(typeof(QuantumStaticCapsuleCollider2D), true)]
  public class QuantumStaticCapsuleCollider2DEditor : QuantumStaticCollider2DEditorBase {
    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      DrawColliderSizeGizmos(behaviour as QuantumStaticCapsuleCollider2D);
    }

    private void DrawColliderSizeGizmos(QuantumStaticCapsuleCollider2D collider) {
      QuantumColliderHandles.Capsule2D(collider, collider.PositionOffset, collider.RotationOffset, ref collider.Size);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticCapsuleCollider3DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D 
  using UnityEditor;
  [CustomEditor(typeof(QuantumStaticCapsuleCollider3D), true)]
  public class QuantumStaticCapsuleCollider3DEditor : QuantumStaticCollider3DEditorBase {
    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      DrawColliderSizeGizmos(behaviour as QuantumStaticCapsuleCollider3D);
    }

    private void DrawColliderSizeGizmos(QuantumStaticCapsuleCollider3D collider) {
      QuantumColliderHandles.Capsule3D(collider, collider.PositionOffset, collider.RotationOffset, ref collider.Radius, ref collider.Height);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticCircleCollider2DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
  using UnityEditor;

  [CustomEditor(typeof(QuantumStaticCircleCollider2D), true)]
  public class QuantumStaticCircleCollider2DEditor : QuantumStaticCollider2DEditorBase {
    protected override bool SupportsRotation => false;

    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      DrawCircleSizeGizmos(behaviour as QuantumStaticCircleCollider2D);
    }

    private void DrawCircleSizeGizmos(QuantumStaticCircleCollider2D collider) {
      QuantumColliderHandles.Circle(collider, collider.PositionOffset, ref collider.Radius);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticCollider2DEditorBase.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D 
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;
  public class QuantumStaticCollider2DEditorBase : QuantumStaticColliderEditorBase {
    
    
    protected override string PosName => nameof(QuantumStaticBoxCollider2D.PositionOffset);
    protected override string RotName => nameof(QuantumStaticBoxCollider2D.RotationOffset);
    protected override string SourceColliderName => nameof(QuantumStaticBoxCollider2D.SourceCollider);
    
    private const string RotationUndoMessage = "Changed collider rotation offset.";
    
    protected override void DrawRotationGizmos(QuantumMonoBehaviour behaviour, SerializedObject so) {
      EditorGUI.BeginChangeCheck();

      var posProperty = so.FindProperty(PosName);
      var rotProperty = so.FindProperty(RotName);

      var positionOffset = FPVectorToUnity(posProperty, true);
      var rotationOffset = GetRotationQuaternion(rotProperty);

      var worldPos = behaviour.transform.TransformPoint(positionOffset);
      rotationOffset = Handles.RotationHandle(rotationOffset, worldPos);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(behaviour, RotationUndoMessage);

        var changedRot = rotationOffset.ToFPRotation2DDegrees().FlipRotation();

        SetFPRawValue(rotProperty, changedRot.RawValue);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(behaviour);
      }
    }

    private static void SetFPRawValue(SerializedProperty property, long rawValue) {
      property.FindPropertyRelativeOrThrow(RawValueName).longValue = rawValue;
    }

    private static Quaternion GetRotationQuaternion(SerializedProperty rotProperty) {
      var rawValue = rotProperty.FindPropertyRelativeOrThrow(RawValueName).longValue;
      var rotation = FP.FromRaw(rawValue);
      
      return rotation.FlipRotation().ToUnityQuaternionDegrees();
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticCollider3DEditorBase.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D 
  using UnityEngine;
  using UnityEditor;
  public abstract class QuantumStaticCollider3DEditorBase : QuantumStaticColliderEditorBase {
    
    
    protected override string PosName => nameof(QuantumStaticBoxCollider3D.PositionOffset);
    protected override string RotName => nameof(QuantumStaticBoxCollider3D.RotationOffset);
    protected override string SourceColliderName => nameof(QuantumStaticBoxCollider3D.SourceCollider);
    
    private const string RotationUndoMessage = "Changed collider rotation offset.";

    protected override void DrawRotationGizmos(QuantumMonoBehaviour behaviour, SerializedObject so) {
      EditorGUI.BeginChangeCheck();

      var posProperty = so.FindProperty(PosName);
      var rotProperty = so.FindProperty(RotName);

      var positionOffset = FPVectorToUnity(posProperty, false);
      var rotationOffset = FPVectorToUnity(rotProperty, false);

      var worldPos = behaviour.transform.TransformPoint(positionOffset);
      var rotation = Quaternion.Euler(rotationOffset);
      rotation = Handles.RotationHandle(rotation, worldPos);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(behaviour, RotationUndoMessage);

        var eulerAngles = rotation.eulerAngles;
        UnityVectorToFP(rotProperty, eulerAngles, false);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(behaviour);
      }
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticColliderEditorBase.cs

namespace Quantum.Editor {
#if ((QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D) || (QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D)) 
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  public abstract class QuantumStaticColliderEditorBase : QuantumEditor {
    protected abstract string PosName { get; }
    protected abstract string RotName { get; }
    protected abstract string SourceColliderName { get; }
    
    private const string XName = nameof(FPVector3.X);
    private const string YName = nameof(FPVector3.Y);
    private const string ZName = nameof(FPVector3.Z);
    protected const string RawValueName = nameof(FP.RawValue);
    private const string UndoMessage = "Changed collider position offset.";

    private int _index = -1;

    protected int ActiveToolbarIndex => _index;

    private bool _toolsPreviousState;

    protected virtual bool SupportsRotation => true;

    protected override void OnEnable() {
      _toolsPreviousState = Tools.hidden;
    }

    protected override void OnDisable() {
      Tools.hidden = _toolsPreviousState;
    }

    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();
      base.DrawScriptPropertyField();

      var sourceColliderProp = serializedObject.FindProperty(SourceColliderName);
      var hasSourceCollider = sourceColliderProp != null && sourceColliderProp.objectReferenceValue != null;

      if (!hasSourceCollider) {
        var rect = EditorGUILayout.GetControlRect();
        QuantumColliderHandles.DrawToolbar(ref rect, ref _index, SupportsRotation);

        EditorGUILayout.Space();

        bool toolsEnabled = _index >= 0;

        Tools.hidden = toolsEnabled;
      } else {
        Tools.hidden = _toolsPreviousState;
      }

      QuantumEditorGUI.SetScriptFieldHidden(this, true);
      base.DrawDefaultInspector();
      QuantumEditorGUI.SetScriptFieldHidden(this, false);

      DrawExtraInspectorGUI();
    }

    protected virtual void DrawExtraInspectorGUI() {
    }

    protected virtual void OnSceneGUI() {
      bool toolsEnabled = _index >= 0;

      if (toolsEnabled) {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
          _index = -1;

          Repaint();
        }
      }

      TryDrawTools((QuantumMonoBehaviour)target, _index);
    }

    private void TryDrawTools(QuantumMonoBehaviour collider, int index) {
      switch (index) {
        case QuantumColliderHandles.EditCollider:
          DrawSizeGizmos(collider);
          break;
        case QuantumColliderHandles.EditPosition:
          DrawPositionGizmos(collider, serializedObject);
          break;
        case QuantumColliderHandles.EditRotation:
          DrawRotationGizmos(collider, serializedObject);
          break;
      }
    }

    protected virtual void DrawRotationGizmos(QuantumMonoBehaviour behaviour, SerializedObject so) {
    }

    protected static long GetFPVectorRawValue(SerializedProperty vectorProp, string componentName) {
      return vectorProp
        .FindPropertyRelativeOrThrow(componentName)
        .FindPropertyRelativeOrThrow(RawValueName)
        .longValue;
    }

    protected static void SetFPVectorRawValue(SerializedProperty vectorProp, string componentName, long value) {
      vectorProp
        .FindPropertyRelativeOrThrow(componentName)
        .FindPropertyRelativeOrThrow(RawValueName)
        .longValue = value;
    }

    protected static Vector3 FPVectorToUnity(SerializedProperty vectorProp, bool isVector2) {
      var x = FP.FromRaw(GetFPVectorRawValue(vectorProp, XName)).AsFloat;
      var y = FP.FromRaw(GetFPVectorRawValue(vectorProp, YName)).AsFloat;

      if (isVector2) {
#if QUANTUM_XY
        return new Vector3(x, y, 0);
#else
        return new Vector3(x, 0, y);
#endif
      }

      var z = FP.FromRaw(GetFPVectorRawValue(vectorProp, ZName)).AsFloat;
      
      return new Vector3(x, y, z);
    }

    protected static void UnityVectorToFP(SerializedProperty vectorProp, Vector3 unityVector, bool isVector2) {
      if (!isVector2) {
        SetFPVectorRawValue(vectorProp, XName, FP.FromFloat_UNSAFE(unityVector.x).RawValue);
        SetFPVectorRawValue(vectorProp, YName, FP.FromFloat_UNSAFE(unityVector.y).RawValue);
        SetFPVectorRawValue(vectorProp, ZName, FP.FromFloat_UNSAFE(unityVector.z).RawValue);
      } else {
        SetFPVectorRawValue(vectorProp, XName, FP.FromFloat_UNSAFE(unityVector.x).RawValue);
#if QUANTUM_XY
        SetFPVectorRawValue(vectorProp, YName, FP.FromFloat_UNSAFE(unityVector.y).RawValue);
#else
        SetFPVectorRawValue(vectorProp, YName, FP.FromFloat_UNSAFE(unityVector.z).RawValue);
#endif
      }
    }

    protected virtual void DrawPositionGizmos(QuantumMonoBehaviour behaviour, SerializedObject so) {
      EditorGUI.BeginChangeCheck();

      var posProperty = so.FindProperty(PosName);
      var isVector2 = posProperty.type.Contains(nameof(FPVector2));

      var positionOffset = FPVectorToUnity(posProperty, isVector2);
      var worldPos = behaviour.transform.TransformPoint(positionOffset);
      worldPos = Handles.PositionHandle(worldPos, Quaternion.identity);

      if (EditorGUI.EndChangeCheck()) {
        Undo.RecordObject(behaviour, UndoMessage);
        
        var localPos = behaviour.transform.InverseTransformPoint(worldPos);
        UnityVectorToFP(posProperty, localPos, isVector2);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(behaviour);
      }
    }

    protected virtual void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
    }
  }
#endif
}


#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticEdgeCollider2DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticEdgeCollider2D))]
  public class QuantumStaticEdgeCollider2DEditor : QuantumStaticCollider2DEditorBase {
    protected override void DrawExtraInspectorGUI() {
      var collider = (QuantumStaticEdgeCollider2D)target;

      EditorGUILayout.Space();

      if (collider.SourceCollider == null) {
        if (GUILayout.Button("Recenter", EditorStyles.miniButton)) {
          var center = collider.VertexA + (collider.VertexB - collider.VertexA) / 2;
          collider.VertexA -= center;
          collider.VertexB -= center;
        }
      } else if (Application.isPlaying == false) {
        collider.UpdateFromSourceCollider();
      }
    }

    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      var collider = (QuantumStaticEdgeCollider2D)target;

      QuantumColliderHandles.Edge(behaviour, ref collider.VertexA, ref collider.VertexB, collider.PositionOffset, collider.RotationOffset);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticPolygonCollider2DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticPolygonCollider2D))]
  public class QuantumStaticPolygonCollider2DEditor : QuantumStaticCollider2DEditorBase {
    private static float DefaultHandlesSize = 0.075f;
    private static float DefaultDistanceToReduceHandleSize = 30.0f;

    private static readonly string _handlesKey = $"{nameof(QuantumStaticPolygonCollider2DEditor)}.{nameof(HandlesSize)}";
    private static readonly string _handleSizeDistanceKey = $"{nameof(QuantumStaticPolygonCollider2DEditor)}.{nameof(DistanceToReduceHandleSize)}";

    public static float HandlesSize {
      get => EditorPrefs.GetFloat(_handlesKey, DefaultHandlesSize);
      set => EditorPrefs.SetFloat(_handlesKey, value);
    }

    public static float DistanceToReduceHandleSize {
      get => EditorPrefs.GetFloat(_handleSizeDistanceKey, DefaultDistanceToReduceHandleSize);
      set => EditorPrefs.SetFloat(_handleSizeDistanceKey, value);
    }

    private bool _staticFoldout;

    protected override void DrawExtraInspectorGUI() {
      var collider = (QuantumStaticPolygonCollider2D)target;

      _staticFoldout = EditorGUILayout.Foldout(_staticFoldout, "Global Tool Gizmo Config");
      if (_staticFoldout) {
        using (new GUILayout.VerticalScope(GUI.skin.box)) {
          var hs = HandlesSize;
          var dist = DistanceToReduceHandleSize;

          hs = EditorGUILayout.FloatField("Handles Size", hs);
          dist = EditorGUILayout.FloatField("Distance To Reduce Handle Size", dist);

          bool changed = false;

          if (Mathf.Approximately(dist, DistanceToReduceHandleSize) == false) {
            DistanceToReduceHandleSize = dist;

            changed = true;
          }

          if (Mathf.Approximately(hs, HandlesSize) == false) {
            HandlesSize = hs;

            changed = true;
          }

          if (changed) {
            QuantumColliderHandles.RepaintSceneView();
          }
        }
      }

      if (ActiveToolbarIndex == 0) {
        EditorGUILayout.HelpBox("Press shift to activate add buttons.\nPress control to activate remove buttons.\nSet static variables like `ButtonOffset` to fine-tune the sizing to your need.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Recenter", EditorStyles.miniButton))
          collider.Vertices = FPVector2.RecenterPolygon(collider.Vertices);
      }

      if (Application.isPlaying == false && collider.SourceCollider != null) {
        collider.UpdateFromSourceCollider(updateVertices: GUILayout.Button("Update Vertices from Source", EditorStyles.miniButton));
      }
    }

    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      var collider = (QuantumStaticPolygonCollider2D)behaviour;
      QuantumColliderHandles.Polygon(collider, ref collider.Vertices, collider.PositionOffset, collider.RotationOffset, true);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticSphereCollider3DEditor.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D 
  using UnityEditor;

  [CustomEditor(typeof(QuantumStaticSphereCollider3D), true)]
  public class QuantumStaticSphereCollider3DEditor : QuantumStaticCollider3DEditorBase {
    protected override bool SupportsRotation => false;

    protected override void DrawSizeGizmos(QuantumMonoBehaviour behaviour) {
      DrawSphereSizeGizmos(behaviour as QuantumStaticSphereCollider3D);
    }

    private static void DrawSphereSizeGizmos(QuantumStaticSphereCollider3D collider) {
      QuantumColliderHandles.Sphere(collider, collider.PositionOffset, ref collider.Radius);
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/StaticColliders/QuantumStaticTerrainCollider3DEditor.cs

namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomEditor(typeof(QuantumStaticTerrainCollider3D), true)]
  public class QuantumStaticTerrainCollider3DEditor : QuantumEditor {
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      var data = target as QuantumStaticTerrainCollider3D;
      if (data) {

        if (data.Asset) {
          EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);

          if (GUILayout.Button("Bake Terrain Data", EditorStyles.miniButton)) {
            data.Bake();
            EditorUtility.SetDirty(data.Asset);
            // TODO: needed or not? data.Asset.Loaded();
            AssetDatabase.Refresh();
          }

          

          EditorGUI.EndDisabledGroup();
        }

        OnInspectorGUI(data);

        QuantumEditorGUI.Header("Experimental");
        data.SmoothSphereMeshCollisions = EditorGUI.Toggle(EditorGUILayout.GetControlRect(), "Smooth Sphere Mesh Collisions", data.SmoothSphereMeshCollisions);
      }
    }

    void OnInspectorGUI(QuantumStaticTerrainCollider3D data) {
      //data.transform.position = Vector3.zero;

      if (data.Asset) {
        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("Asset Settings", EditorStyles.boldLabel);

        var asset = new SerializedObject(data.Asset);
        var property = asset.GetIterator();

        // enter first child
        property.Next(true);

        while (property.Next(false)) {
          if (property.name.StartsWith("m_")) {
            continue;
          }

          EditorGUILayout.PropertyField(property, true);
        }

        asset.ApplyModifiedProperties();
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/CustomEditors/SystemEntryDrawer.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Scripting;
  using SystemEntryBase = SystemsConfig.SystemEntryBase;

#if !ODIN_INSPECTOR || QUANTUM_ODIN_DISABLED
  [UnityEditor.CustomPropertyDrawer(typeof(SystemEntryBase), true)]
#endif
  class SystemBasePropertyDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      if (property.IsArrayElement()) {
        if (label?.text?.StartsWith("Element ", StringComparison.Ordinal) == true) {
          
          var systemTypeProperty = property.FindPropertyRelativeOrThrow(nameof(SystemEntryBase.SystemType));
      
          var (fullTypeName, decorationType, decorationMsg) = SerializableTypeDrawer.GetTypeContent(systemTypeProperty, true, out _);
          label.text = fullTypeName;
          
          EditorGUI.PropertyField(position, property, label, property.isExpanded);
          
          // now the decoration, right aligned
          if (decorationType != MessageType.None) {
            QuantumEditorGUI.Decorate(position.SetLineHeight(), decorationMsg, decorationType, rightAligned: true, drawButton: true, drawBorder: false);
          }
          return;
        }
      }

      EditorGUI.PropertyField(position, property, label, property.isExpanded);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return EditorGUI.GetPropertyHeight(property, label, property.isExpanded);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/Dotnet/QuantumDotnetBuildSettingsInspector.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Inspector code to manage the QuantumDotnetBuildSettings asset in the editor.
  /// </summary>
  [CustomEditor(typeof(QuantumDotnetBuildSettings), false)]
  public class QuantumDotnetBuildSettingsInspector : QuantumEditor {
    private SerializedProperty _pluginSdkPath;
    private SerializedProperty _showDllAfterBuild;
    private SerializedProperty _showFolderAfterGeneration;
    private SerializedProperty _projectSettings;
    private SerializedProperty _simulationProjectTemplate;
    private SerializedProperty _runnerProjectTemplate;
    private SerializedProperty _projectOutputPath;
    private SerializedProperty _targetPlatform;
    private SerializedProperty _targetConfiguration;
    private SerializedProperty _binOutputPath;
    private SerializedProperty _commandPath;
    private QuantumDotnetBuildSettings _settings;

    /// <summary>
    /// Cache properties.
    /// </summary>
    protected override void OnEnable() {
      _settings = (QuantumDotnetBuildSettings)target;
      _pluginSdkPath = serializedObject.FindProperty(nameof(_settings.PluginSdkPath));
      _showDllAfterBuild = serializedObject.FindProperty(nameof(_settings.ShowCompiledDllAfterBuild));
      _showFolderAfterGeneration = serializedObject.FindProperty(nameof(_settings.ShowFolderAfterGeneration));
      _projectSettings = serializedObject.FindProperty(nameof(_settings.ProjectSettings));
      _simulationProjectTemplate = serializedObject.FindProperty(nameof(_settings.SimulationProjectTemplate));
      _runnerProjectTemplate = serializedObject.FindProperty(nameof(_settings.RunnerProjectTemplate));
      _targetConfiguration = serializedObject.FindProperty(nameof(_settings.TargetConfiguration));
      _targetPlatform = serializedObject.FindProperty(nameof(_settings.TargetPlatform));
      _projectOutputPath = serializedObject.FindProperty(nameof(_settings.ProjectBasePath));
      _binOutputPath = serializedObject.FindProperty(nameof(_settings.BinOutputPath));
      _commandPath = serializedObject.FindProperty(nameof(_settings.DotnetCommandPath));
    }

    /// <summary>
    /// Draw the inspector GUI.
    /// </summary>
    public override void OnInspectorGUI() {
      base.PrepareOnInspectorGUI();

      EditorGUI.BeginChangeCheck();

      serializedObject.Update();

      QuantumEditorGUI.ScriptPropertyField(serializedObject);

      Draw();

      base.DrawEditorButtons();

      if (EditorGUI.EndChangeCheck()) {
        serializedObject.ApplyModifiedProperties();
      }
    }

    private void DrawNoPluginSdkFound() {
      EditorGUILayout.HelpBox(
        "Photon Server SDK folder not detected. Please set the correct path. \nThis path MUST be relative.",
        MessageType.Warning);

      EditorGUILayout.PropertyField(_pluginSdkPath, new GUIContent("Plugin SDK Path"));

      if (GUILayout.Button("Detect Plugin SDK")) {
        _settings.DetectPluginSdk();
      }
    }

    private void Draw() {
      DrawProjectGeneration();

      DrawProjectCompilation();

      DrawPluginSDK();

      DrawPhotonServerUtils();
    }

    private void DrawPhotonServerUtils() {
      DrawHeaderText("Photon Server Utils");

      if (GUILayout.Button("Launch PhotonServer.exe")) {
        _settings.LaunchPhotonServer();
      }
    }

    private static void DrawHeaderText(string headerText) {
      GUILayout.Space(20);

      GUILayout.Label(headerText, EditorStyles.boldLabel);

      GUILayout.Space(5);
    }

    private void DrawPluginSDK() {
      DrawHeaderText("Plugin SDK");

      if (_settings.HasCustomPluginSdk == false) {
        DrawNoPluginSdkFound();
        return;
      }

      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _pluginSdkPath), _pluginSdkPath);

      if (GUILayout.Button("Sync Plugin SDK Server Simulation")) {
        QuantumDotnetBuildSettings.SynchronizePluginSdk(_settings);
      }

      if (GUILayout.Button("Sync Plugin SDK Assets Only")) {
        QuantumDotnetBuildSettings.ExportPluginSdkData(_settings);
      }
    }

    private void DrawProjectCompilation() {
      DrawHeaderText("Project Compilation");

      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _targetPlatform), _targetPlatform);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _targetConfiguration), _targetConfiguration);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _showDllAfterBuild), _showDllAfterBuild);

      if (GUILayout.Button("Build Dotnet Quantum Game Dll")) {
        QuantumDotnetBuildSettings.GenerateProject(_settings, disablePopup: true);
        QuantumDotnetBuildSettings.BuildProject(_settings);
        QuantumEditorLog.Log($"Generated and Built Dotnet Project at {System.IO.Path.GetFullPath(_settings.ProjectBasePath)}");
      }
    }

    private void DrawProjectGeneration() {
      DrawHeaderText("Project Generation");

      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _projectSettings), _projectSettings);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _simulationProjectTemplate), _simulationProjectTemplate);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _runnerProjectTemplate), _runnerProjectTemplate);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _projectOutputPath), _projectOutputPath);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _binOutputPath), _binOutputPath);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _commandPath), _commandPath);
      EditorGUI.PropertyField(QuantumEditorGUI.LayoutHelpPrefix(this, _showFolderAfterGeneration), _showFolderAfterGeneration);

      if (GUILayout.Button("Generate Dotnet Project")) {
        QuantumDotnetBuildSettings.GenerateProject(_settings);
        QuantumEditorLog.Log($"Generated Dotnet Project at {System.IO.Path.GetFullPath(_settings.ProjectBasePath)}");
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Components.cs

namespace Quantum.Editor {

  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  partial class QuantumEditorGUI {
    public const float ThumbnailSpacing = 1.0f;
    public const float ThumbnailWidth = 32.0f;
    private const float ThumbnailMinHeight = 14.0f;

    private static readonly int ThumbnailFieldHash = "Thumbnail".GetHashCode();
    private static readonly GUIContent MissingComponentContent = new GUIContent("???");
    private static readonly Texture2D _isCulledCachedIcon = (Texture2D)EditorGUIUtility.IconContent("animationvisibilitytoggleoff").image;
    private static readonly Texture2D _isNotCulledCachedIcon = (Texture2D)EditorGUIUtility.IconContent("animationvisibilitytoggleon").image;

    public static Rect AssetThumbnailPrefix(Rect position, string assemblyQualifiedName, bool addSpacing = true) {
      if (QuantumEditorUtility.TryGetAssetType(assemblyQualifiedName, out var type)) {
        return AssetThumbnailPrefix(position, type, addSpacing);
      } else {
        // TODO: draw a placeholder
        return position.AddX(ThumbnailWidth + (addSpacing ? ThumbnailSpacing : 0));
      }
    }

    public static Rect AssetThumbnailPrefix(Rect position, Type componentType, bool addSpacing = true) {
      QuantumEditorUtility.GetAssetThumbnailData(componentType, out var label, out var color);
      return DrawThumbnail(position, addSpacing, label, color);
    }

    public static float CalcThumbnailsWidth(int count) {
      return count * ThumbnailWidth + Math.Max(0, count - 1) * ThumbnailSpacing;
    }

    public static Rect ComponentThumbnailPrefix(Rect position, string componentTypeName, bool addSpacing = true, bool assemblyQualified = false) {
      if (QuantumEditorUtility.TryGetComponentType(componentTypeName, out var type, assemblyQualified: assemblyQualified)) {
        return ComponentThumbnailPrefix(position, type, addSpacing);
      } else {
        return DrawThumbnail(position, addSpacing, MissingComponentContent, Color.red);
      }
    }

    public static Rect ComponentThumbnailPrefix(Rect position, Type componentType, bool addSpacing = true) {
      QuantumEditorUtility.GetComponentThumbnailData(componentType, out var label, out var color);
      return DrawThumbnail(position, addSpacing, label, color);
    }
    
    public static Rect MissingComponentThumbnailPrefix(Rect position, string componentTypeName, bool addSpacing = true) {
      return DrawThumbnail(position, addSpacing, new GUIContent(MissingComponentContent.text, componentTypeName), Color.red);
    }
    
    public static void ShowComponentTypePicker(Rect activatorRect, Type selectedType, Action<Type> selected, Predicate<Type> filter = null, Action clear = null) {
      var types = QuantumEditorUtility.ComponentTypes;
      if (filter != null) {
        types = types.Where(x => filter(x));
      }

      var content = new ComponentPopupContent(types.ToArray(),
        x => x == selectedType,
        (x, b) => {
          if (b) {
            selected(x);
          } else if (clear != null) {
            clear();
          }
        }
      ) {
        SingleMode = true,
        ShowClear = clear != null,
      };

      PopupWindow.Show(activatorRect, content);
    }

    public static void ShowComponentTypesPicker(Rect activatorRect, Func<Type, bool> isSelected, Action<Type, bool> setSelected, Predicate<Type> filter = null) {
      var types = QuantumEditorUtility.ComponentTypes;
      if (filter != null) {
        types = types.Where(x => filter(x));
      }

      var content = new ComponentPopupContent(types.ToArray(), isSelected, setSelected) {
        ShowClear = true,
      };

      PopupWindow.Show(activatorRect, content);
    }

    public static Rect ShowCulledState(Rect position, bool? isCulled) {
      var label = default(GUIContent);

      if (isCulled.HasValue) {
        label = new GUIContent {
          tooltip = isCulled.Value ? "Entity is prediction culled" : "Entity is not prediction culled",
          image = isCulled.Value ? _isCulledCachedIcon : _isNotCulledCachedIcon
        };
      } else {
        return position.AddX(ThumbnailWidth);
      }

      return DrawThumbnail(position, false, label, Color.white);
    }

    internal static Rect DrawThumbnail(Rect position, bool addSpacing, GUIContent label, Color color) {
      var rect = position.SetWidth(ThumbnailWidth);
      var style = label.image ? QuantumEditorSkin.ThumbnailImageStyle : QuantumEditorSkin.ThumbnailBoxStyle;

      var height = style.CalcHeight(label, ThumbnailWidth);

      if (position.height > height) {
        rect.height = height;
        rect.y += (position.height - height) / 2;
      }

      int controlID = GUIUtility.GetControlID(ThumbnailFieldHash, FocusType.Passive, rect);

      if (Event.current.type == EventType.Repaint) {
        var originalColor = GUI.backgroundColor;
        try {
          GUI.backgroundColor = color;
          style.Draw(rect, label, controlID);
        } finally {
          GUI.backgroundColor = originalColor;
        }
      }

      return position.AddX(ThumbnailWidth + (addSpacing ? ThumbnailSpacing : 0));
    }

    private class ComponentPopupContent : PopupWindowContent {
      private const float ScrollbarWidth = 25.0f;
      private const int ThumbnailSpacing = 2;
      private const float ToggleWidth = 16.0f;

      private readonly RectOffset marginOverride = new RectOffset(4, 2, 0, 0);

      private GUIStyle _compactLabel;
      private GUIStyle _compactRadioButton;

      private Func<Type, bool> _isSelected;
      private GUIContent[] _prettyNames;
      private Vector2 _scrollPos;
      private Action<Type, bool> _setSelected;
      private Type[] _types;
      public ComponentPopupContent(Type[] types, Func<Type, bool> isSelected, Action<Type, bool> setSelected) {
        _types = types;
        _isSelected = isSelected;
        _setSelected = setSelected;
        _prettyNames = types.Select(x => QuantumEditorUtility.GetComponentDisplayName(x)).ToArray();

        _compactRadioButton = new GUIStyle(EditorStyles.radioButton) {
          fixedHeight = EditorGUIUtility.singleLineHeight,
          margin = marginOverride,
          contentOffset = new Vector2(ThumbnailWidth + 2 * ThumbnailSpacing, 0),
        };

        _compactLabel = new GUIStyle(EditorStyles.label) {
          fixedHeight = EditorGUIUtility.singleLineHeight,
          margin = marginOverride,
          contentOffset = new Vector2(ThumbnailWidth + 2 * ThumbnailSpacing, 0)
        };
      }

      public Action OnChange { get; set; }
      public bool ShowClear { get; set; }
      public bool SingleMode { get; set; }

      public override Vector2 GetWindowSize() {
        float perfectWidth = 0;

        if (_prettyNames.Length != 0) {
          perfectWidth = _prettyNames.Max(x => EditorStyles.label.CalcSize(x).x) + ThumbnailWidth + (2 * ThumbnailSpacing) + ScrollbarWidth + marginOverride.horizontal;
        }

        // ignoring vertical spacing here because we're overriding margins
        var perfectHeight = _prettyNames.Length * (EditorGUIUtility.singleLineHeight + marginOverride.vertical);
        if (ShowClear) {
          perfectHeight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        perfectHeight += 10;
        perfectWidth += 10;

        return new Vector2(Mathf.Clamp(perfectWidth, 200, Screen.width), Mathf.Clamp(perfectHeight, 200, Screen.height));
      }

      public override void OnGUI(Rect rect) {
        EditorGUI.BeginChangeCheck();
        try {
          using (new GUI.GroupScope(rect)) {
            using (var scroll = new GUILayout.ScrollViewScope(_scrollPos)) {
              _scrollPos = scroll.scrollPosition;

              int firstSelected = Array.FindIndex(_types, x => _isSelected(x));

              for (int i = 0; i < _prettyNames.Length; ++i) {
                var label = _prettyNames[i];
                var type = _types[i];
                bool wasSelected = SingleMode ? (i == firstSelected) : _isSelected(type);

                Rect toggleRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, _compactLabel);

                EditorGUI.BeginChangeCheck();

                bool toggle;
                if (SingleMode) {
                  using (_compactRadioButton.FontStyleScope(bold: wasSelected)) {
                    toggle = GUI.Toggle(toggleRect, wasSelected, label, _compactRadioButton);
                  }
                } else {
                  using (_compactLabel.FontStyleScope(bold: wasSelected)) {
                    toggle = EditorGUI.ToggleLeft(toggleRect, label, wasSelected, _compactLabel);
                  }
                }

                ComponentThumbnailPrefix(toggleRect.AddX(ToggleWidth + ThumbnailSpacing), type);

                if (EditorGUI.EndChangeCheck()) {
                  _setSelected(type, SingleMode ? true : toggle);
                }
              }
            }

            if (ShowClear) {
              using (new GUILayout.HorizontalScope()) {
                if (GUILayout.Button("Clear")) {
                  foreach (var t in _types) {
                    if (_isSelected(t)) {
                      _setSelected(t, false);
                    }
                  }
                }
              }
            }
          }
        } finally {
          if (EditorGUI.EndChangeCheck()) {
            OnChange?.Invoke();
            if (SingleMode) {
              editorWindow.Close();
            }
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Inspector.cs

namespace Quantum.Editor {
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {

    public delegate bool PropertyCallback(SerializedProperty property, System.Reflection.FieldInfo field, System.Type fieldType);

    public static bool Inspector(SerializedObject obj, string[] filters = null, PropertyCallback callback = null, bool drawScript = true) {
      return InspectorInternal(obj.GetIterator(), filters: filters, skipRoot: true, callback: callback, drawScript: drawScript);
    }

    public static bool Inspector(SerializedObject obj, string propertyPath, string[] filters = null, bool skipRoot = true, PropertyCallback callback = null, bool drawScript = false) {
      return InspectorInternal(obj.FindPropertyOrThrow(propertyPath), filters: filters, skipRoot: skipRoot, callback: callback, drawScript: drawScript);
    }

    public static bool Inspector(SerializedProperty prop, GUIContent label = null, string[] filters = null, bool skipRoot = true, bool drawScript = false, PropertyCallback callback = null) {
      return InspectorInternal(prop, label, filters, skipRoot, drawScript, callback);
    }

    internal static bool InspectorInternal(SerializedProperty prop, GUIContent label = null, string[] filters = null, bool skipRoot = true, bool drawScript = false, PropertyCallback callback = null) {
      int minDepth = prop.depth;
      prop = prop.Copy();

      bool enterChildren = false;
      
      EditorGUI.BeginChangeCheck();
      
      int indentLevel = EditorGUI.indentLevel;
      int referenceIndentLevel = indentLevel;

      try {
        if (skipRoot) {
          referenceIndentLevel -= 1;
          enterChildren = true;
        } else {
          var position = EditorGUILayout.GetControlRect(true);
          enterChildren = EditorGUILayout.PropertyField(prop, label, false);
        }
        
        while (prop.NextVisible(enterChildren) && prop.depth > minDepth) {
          enterChildren = false;

          if (!drawScript && prop.propertyPath == QuantumEditorGUI.ScriptPropertyName) {
            continue;
          }
          
          if (filters?.Any(f => prop.propertyPath.StartsWith(f)) == true) {
            continue;
          }
          
          EditorGUI.indentLevel = referenceIndentLevel + prop.depth - minDepth;
          if (callback != null) {
            var field          = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(prop, out var fieldType);
            var propCopy       = prop.Copy();
            var callbackResult = callback(propCopy, field, fieldType);
            if (callbackResult == false) {
              // will go ahead
            } else {
              continue;
            }
          }
          
          EditorGUILayout.PropertyField(prop, null, true);
        } 
      } finally {
        EditorGUI.indentLevel = indentLevel;
      }

      if (EditorGUI.EndChangeCheck()) {
        prop.serializedObject.ApplyModifiedProperties();
        return true;
      }
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Menu.cs

namespace Quantum.Editor {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {
    public static MenuBuilder<T1> BuildMenu<T1>() => new MenuBuilder<T1>();
    public static MenuBuilder<T1, T2> BuildMenu<T1, T2>() => new MenuBuilder<T1, T2>();
    public static MenuBuilder<T1, T2, T3> BuildMenu<T1, T2, T3>() => new MenuBuilder<T1, T2, T3>();
    public static MenuBuilder<T1, T2, T3, T4> BuildMenu<T1, T2, T3, T4>() => new MenuBuilder<T1, T2, T3, T4>();

    public abstract class MenuBuilderBase<T, TAction, TPredicate>
      where TAction : System.Delegate
      where TPredicate : System.Delegate {

      public delegate void GenerateCallback(Action<string, TAction, TPredicate> addItem);

      private List<GUIContent> _labels = new List<GUIContent>();
      private List<TAction> _handlers = new List<TAction>();
      private List<TPredicate> _filters = new List<TPredicate>();
      private List<GenerateCallback> _generators = new List<GenerateCallback>();

      public T AddItem(string content, TAction onClick, TPredicate predicate = null) =>
        AddItem(new GUIContent(content), onClick, predicate);

      public T AddItem(GUIContent content, TAction onClick, TPredicate predicate = null) {
        _labels.Add(content);
        _handlers.Add(onClick);
        _filters.Add(predicate);
        return (T)(object)this;
      }

      public T AddGenerator(GenerateCallback p) {
        _generators.Add(p);
        return (T)(object)this;
      }

      public void Build(out GUIContent[] labels, out TAction[] actions, out TPredicate[] predicates) {
        var allLabels = _labels.ToList();
        var allHandlers = _handlers.ToList();
        var allPredicates = _filters.ToList();

        foreach (var generator in _generators) {
          generator((name, handler, predicate) => {
            allLabels.Add(new GUIContent(name));
            allHandlers.Add(handler);
            allPredicates.Add(predicate);
          });
        }

        labels = allLabels.ToArray();
        actions = allHandlers.ToArray();
        predicates = allPredicates.ToArray();
      }
    }

    public class MenuBuilder : MenuBuilderBase<MenuBuilder, Action, Func<bool>> {

      static public implicit operator Action<Rect>(MenuBuilder builder) {
        return (Rect rect) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke() ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected]();
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1> : MenuBuilderBase<MenuBuilder<T1>, Action<T1>, Func<T1, bool>> {

      static public implicit operator Action<Rect, T1>(MenuBuilder<T1> builder) {
        return (Rect rect, T1 t1) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1);
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1, T2> : MenuBuilderBase<MenuBuilder<T1, T2>, Action<T1, T2>, Func<T1, T2, bool>> {

      static public implicit operator Action<Rect, T1, T2>(MenuBuilder<T1, T2> builder) {
        return (Rect rect, T1 t1, T2 t2) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1, t2) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1, t2);
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1, T2, T3> : MenuBuilderBase<MenuBuilder<T1, T2, T3>, Action<T1, T2, T3>, Func<T1, T2, T3, bool>> {

      static public implicit operator Action<Rect, T1, T2, T3>(MenuBuilder<T1, T2, T3> builder) {
        return (Rect rect, T1 t1, T2 t2, T3 t3) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1, t2, t3) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1, t2, t3);
             },
             null
           );
        };
      }
    }

    public class MenuBuilder<T1, T2, T3, T4> : MenuBuilderBase<MenuBuilder<T1, T2, T3, T4>, Action<T1, T2, T3, T4>, Func<T1, T2, T3, T4, bool>> {

      static public implicit operator Action<Rect, T1, T2, T3, T4>(MenuBuilder<T1, T2, T3, T4> builder) {
        return (Rect rect, T1 t1, T2 t2, T3 t3, T4 t4) => {
          builder.Build(out var labels, out var handlers, out var filters);
          EditorUtility.DisplayCustomMenu(rect,
             labels,
             i => filters[i]?.Invoke(t1, t2, t3, t4) ?? true,
             -1,
             (ud, opts, selected) => {
               handlers[selected](t1, t2, t3, t4);
             },
             null
           );
        };
      }
    }

    public static bool HandleContextMenu(Rect rect, out Rect menuRect, bool showButton = true) {
      // Options button
      const float optionsButtonWidth = 16f;
      const float optionsButtonHeight = 16f;
      const float margin = 4f;

      if (showButton) {

        Rect buttonRect = new Rect(rect.xMax - optionsButtonWidth - margin, rect.y + (rect.height - optionsButtonHeight) * 0.5f, optionsButtonWidth, rect.height);

        if (Event.current.type == EventType.Repaint) {
          UnityInternal.Styles.OptionsButtonStyle.Draw(buttonRect, false, false, false, false);
        }

        if (EditorGUI.DropdownButton(buttonRect, GUIContent.none, FocusType.Passive, GUIStyle.none)) {
          menuRect = buttonRect;
          return true;
        }
      }

      if (Event.current.type == EventType.ContextClick) {
        if (rect.Contains(Event.current.mousePosition)) {
          menuRect = new Rect(Event.current.mousePosition, Vector2.one);
          return true;
        }
      }

      menuRect = default;
      return false;
    }

    public static bool HandleContextMenu(Rect rect, System.Action<Rect> menu, bool showButton = true) {
      if (HandleContextMenu(rect, out var menuRect, showButton: showButton)) {
        menu(menuRect);
        return true;
      }
      return false;
    }

    public static bool HandleContextMenu<T>(Rect rect, T item, System.Action<Rect, T> menu, bool showButton = true) {
      if (HandleContextMenu(rect, out var menuRect, showButton: showButton)) {
        menu(menuRect, item);
        return true;
      }
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorGUI.Utils.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumEditorGUI {
    public const   int        CheckboxWidth  = 16;
    public const   float      MinPrefixWidth = 20.0f;
    private static GUIStyle   _overlayStyle;
    private static GUIContent _tempContent = new GUIContent();

    private static GUIContent TempContent(string str) {
      _tempContent.text = str;
      _tempContent.image = null;
      _tempContent.tooltip = null;
      return _tempContent;
    }

    private static GUIStyle MiddleRightMiniLabelStyle {
      get {
        if (_overlayStyle == null) {
          _overlayStyle = new GUIStyle(EditorStyles.miniLabel) {
            alignment = TextAnchor.MiddleRight,
            contentOffset = new Vector2(-2, 0),
          };
        }
        return _overlayStyle;
      }
    }



    public static void MultiTypeObjectField(Rect rect, SerializedProperty prop, GUIContent label, params System.Type[] types) {
      UnityEngine.Object obj = prop.objectReferenceValue;
      using (new PropertyScope(rect, label, prop)) {
        rect = EditorGUI.PrefixLabel(rect, label);
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          EditorGUI.BeginChangeCheck();
          if (obj != null) {
            var matchingType = types.SingleOrDefault(x => x.IsInstanceOfType(obj));
            if (matchingType != null) {
              obj = EditorGUI.ObjectField(rect, obj, matchingType, true);
            } else {
              obj = EditorGUI.ObjectField(rect, obj, typeof(UnityEngine.Object), true);
              Decorate(rect, $"Type not supported: {obj?.GetType()}", MessageType.Error);
            }
          } else {
            var r = rect.SetWidth(rect.width / types.Length);
            foreach (var t in types) {
              var value = EditorGUI.ObjectField(r, null, t, true);
              if (obj == null) {
                obj = value;
              }
              r.x += r.width;
            }
          }
          if (EditorGUI.EndChangeCheck()) {
            prop.objectReferenceValue = obj;
            prop.serializedObject.ApplyModifiedProperties();
          }
        }
      }
    }

    public static void MultiTypeObjectField(SerializedProperty prop, GUIContent label, params System.Type[] types) {
      MultiTypeObjectField(EditorGUILayout.GetControlRect(), prop, label, types);
    }

    public static void MultiTypeObjectField(SerializedProperty prop, GUIContent label, System.Type[] types, params GUILayoutOption[] options) {
      MultiTypeObjectField(EditorGUILayout.GetControlRect(options), prop, label, types);
    }

    public static Rect PrefixIcon(Rect rect, string tooltip, MessageType messageType, bool alignLeft = false) {
      var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, tooltip, messageType);
      var iconRect = rect;
      iconRect.width = Mathf.Min(MinPrefixWidth, rect.width);
      if ( alignLeft ) {
        iconRect.x -= iconRect.width;
      }

      GUI.Label(iconRect, content, new GUIStyle());

      rect.width = Mathf.Max(0, rect.width - iconRect.width);
      rect.x += iconRect.width;

      return rect;
    }

    public static void Overlay(Rect rect, string message, Color? color = null) {
      if (color == null) {
        var c = EditorGUIUtility.isProSkin ? Color.yellow : Color.blue;
        c.a = 0.75f;
        color = c;
      }

      using (new ColorScope(color.Value)) {
        GUI.Label(rect, message, MiddleRightMiniLabelStyle);
      }
    }

    public static void Header(string header) {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
    }
    
    public static bool IsNullOrEmpty(GUIContent content) {
      if (content == null || (string.IsNullOrEmpty(content.text) && content.image == null)) {
        return true;
      }
      return false;
    }

    public static void LargeTooltip(Rect areaRect, Rect itemRect, GUIContent content) {

      const float ArrowWidth = 64.0f;
      const float ArrowHeight = 6.0f;

      var contentSize = UnityInternal.Styles.AnimationEventTooltip.CalcSize(content);
      var anchor = new Vector2(itemRect.center.x, itemRect.yMax);

      var arrowRect = new Rect(anchor.x - ArrowWidth / 2.0f, anchor.y, ArrowWidth, ArrowHeight);
      var labelRect = new Rect(anchor.x, anchor.y + ArrowHeight, contentSize.x, contentSize.y);

      // these are some magic values that Unity seems to be using with this style
      if (labelRect.xMax > areaRect.xMax + 16)
        labelRect.x = areaRect.xMax - labelRect.width + 16;
      if (arrowRect.xMax > areaRect.xMax + 20)
        arrowRect.x = areaRect.xMax - arrowRect.width + 20;
      if (labelRect.xMin < areaRect.xMin + 30)
        labelRect.x = areaRect.xMin + 30;
      if (arrowRect.xMin < areaRect.xMin - 20)
        arrowRect.x = areaRect.xMin - 20;

      // flip tooltip if too close to bottom (but do not flip if flipping would mean the tooltip is too high up)
      var flipRectAdjust = (itemRect.height + labelRect.height + 2 * arrowRect.height);
      var flipped = (anchor.y + contentSize.y + 6 > areaRect.yMax) && (labelRect.y - flipRectAdjust > 0);
      if (flipped) {
        labelRect.y -= flipRectAdjust;
        arrowRect.y -= (itemRect.height + 2 * arrowRect.height);
      }

      using (new GUI.ClipScope(arrowRect)) {
        var oldMatrix = GUI.matrix;
        try {
          if (flipped)
            GUIUtility.ScaleAroundPivot(new Vector2(1.0f, -1.0f), new Vector2(arrowRect.width * 0.5f, arrowRect.height));
          GUI.Label(new Rect(0, 0, arrowRect.width, arrowRect.height), GUIContent.none, UnityInternal.Styles.AnimationEventTooltipArrow);
        } finally {
          GUI.matrix = oldMatrix;
        }
      }

      GUI.Label(labelRect, content, UnityInternal.Styles.AnimationEventTooltip);
    }

    public struct SectionScope: IDisposable {
      public SectionScope(string headline = null) {
        EditorGUILayout.Space();
        if (!string.IsNullOrEmpty(headline)) {
          EditorGUILayout.LabelField(headline, EditorStyles.boldLabel);
        }
      }

      public void Dispose() {
      }
    }
    
    public struct ContentOffsetScope : IDisposable {

      private readonly GUIStyle _style;
      private readonly Vector2  _contentOffset;
      
      public ContentOffsetScope(GUIStyle style, float x = 0, float y = 0) {
        _style = style;
        _contentOffset = style.contentOffset;
        _style.contentOffset = _contentOffset + new Vector2(x, y);
      }
      
      public void Dispose() {
        _style.contentOffset = _contentOffset;
      }
    }
    
    public struct PaddingScope : IDisposable {

      private readonly GUIStyle   _style;
      private readonly RectOffset _padding;
      
      public PaddingScope(GUIStyle style, int left = 0, int top = 0, int right = 0, int bottom = 0) {
        _style = style;
        _padding = new RectOffset(style.padding.left, style.padding.right, style.padding.top, style.padding.bottom);
        _style.padding = new RectOffset(_padding.left + left, _padding.right + right, _padding.top + top, _padding.bottom + bottom);
      }
      
      public void Dispose() {
        _style.padding = _padding;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumEditorUtility.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Text.RegularExpressions;
  using Core;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  public unsafe partial class QuantumEditorUtility {

    public static IEnumerable<Type> ComponentTypes => Statics.Components.Keys;

    public static string DumpPointer(FrameBase frame, void* ptr, Type type) {
      var printer = Statics.Printer;
      try {
        printer.Reset(frame);
        printer.AddPointer("#ROOT", ptr, type);
        return printer.ToString();
      } finally {
        printer.Reset(null);
      }
    }

    public static string GenerateAcronym(string str) {
      StringBuilder acronymBuilder = new StringBuilder();

      for (int i = 0; i < str.Length; ++i) {
        var c = str[i];
        if (i != 0 && char.IsLower(c)) {
          continue;
        }
        acronymBuilder.Append(c);
      }

      return acronymBuilder.ToString();
    }

    public static GUIContent GetComponentAcronym(Type componentType) {
      return Statics.Components[componentType].ThumbnailContent;
    }

    public static Color GetComponentColor(Type componentType) {
      return Statics.Components[componentType].ThumbnailColor;
    }

    public static GUIContent GetComponentDisplayName(Type componentType) {
      return Statics.Components[componentType].DisplayName;
    }

    public static void* GetComponentPointer(FrameBase frame, EntityRef entityRef, Type componentType) {
      return frame.Unsafe.GetPointer(entityRef, ComponentTypeId.GetComponentIndex(componentType));
    }

    public static void GetComponentThumbnailData(Type componentType, out GUIContent label, out Color color) {
      var entry = Statics.Components[componentType];
      label = entry.ThumbnailContent;
      color = entry.ThumbnailColor;
    }

    public static bool TryGetComponentType(string name, out Type type, bool assemblyQualified = false) {
      if (assemblyQualified) {
        if (Statics.ComponentsByAQName.TryGetValue(name, out var entry)) {
          type = entry.ComponentType;
          return true;
        }
      } else {
        if (Statics.ComponentsByName.TryGetValue(name, out var entry)) {
          type = entry.ComponentType;
          return true;
        }
      }
      type = null;
      return false;
    }

    public static bool TryGetComponentType(ComponentTypeRef typeRef, out Type type) {
      foreach (var componentType in ComponentTypes) {
        if (typeRef.Is(componentType)) {
          type = componentType;
          return true;
        }
      }
      
      type = null;
      return false;
    }

    public static Type GetComponentType(string componentName, bool assemblyQualified = false) {
      if (TryGetComponentType(componentName, out var result, assemblyQualified)) {
        return result;
      }
      throw new ArgumentOutOfRangeException($"Component not found: {componentName} (assemblyQualified: {assemblyQualified})");
    }

    public static bool TryGetAssetType(string assemblyQualifiedName, out Type type) {
      if (Statics.AssetsByAQName.TryGetValue(assemblyQualifiedName, out var entry)) {
        type = entry.AssetType;
        return true;
      }
      type = null;
      return false;
    }

    public static void GetAssetThumbnailData(Type assetType, out GUIContent label, out Color color) {
      var entry = Statics.Assets[assetType];
      label = entry.ThumbnailContent;
      color = entry.ThumbnailColor;
    }

    public static SerializedProperty GetKnownObjectRoot(object obj) {
      var container = QuantumEditorUtilityContainer.instance;
      container.Object = obj;

      var so = new SerializedObject(container);
      return so.FindPropertyOrThrow($"{nameof(container.Object)}");
    }

    public static SerializedProperty GetPendingEntityPrototypeRoot(bool clear = false) {
      var container = QuantumEditorUtilityContainer.instance;

      if (clear) {
        container.PendingComponentPrototypes = Array.Empty<ComponentPrototype>();

        // temp testing
        // container.PendingComponentPrototypes = new ComponentPrototype[] {
        //   new Transform2D_Prototype() {Position = FPVector2.Zero, Rotation = FP._0},
        // };
      }
      
      var so = new SerializedObject(container);
      var rootProperty = so.FindPropertyOrThrow(nameof(container.PendingComponentPrototypes));
      return rootProperty;
    }

    public static EntityPrototype FinishPendingEntityPrototype() {
      var container = QuantumEditorUtilityContainer.instance;

      var entityPrototype = AssetObject.Create<EntityPrototype>();
      entityPrototype.Container = ComponentPrototypeSet.FromArray(container.PendingComponentPrototypes);

      container.PendingComponentPrototypes = Array.Empty<ComponentPrototype>();
      
      return entityPrototype;
    }

    public static Color GetPersistentColor(string str, int minValue = 128) {
      return GeneratePastelColor(GetPersistentHashCode(str), minValue);
    }

    public const int PersistentHashCodeStart = 5381;

    public static int GetPersistentHashCode(string str, int prevHash = PersistentHashCodeStart) {
      int hash = prevHash;
      int len = str.Length;
      fixed (char* c = str) {
        for (int i = 0; i < len; ++i) {
          hash = hash * 33 + c[i].GetHashCode();
        }
      }
      return hash;
    }

    public static object MakeKnownObjectSerializable(object obj) {
      return obj;
    }

    public static void TraverseDump(string dump, Func<string, bool> onEnter, Action onExit, Action<string, string> onValue) {
      using (var reader = new StringReader(dump)) {
        Debug.Assert(reader.ReadLine() == "#ROOT:");

        int groupDepth = 1;
        int ignoreDepth = int.MaxValue;

        for (string line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
          var valueSplitter = line.IndexOf(':');
          if (valueSplitter < 0) {
            QuantumEditorLog.Warn($"Invalid line format: {line}");
            continue;
          }

          int indent = 0;
          while (indent < line.Length && line[indent] == ' ')
            ++indent;

          Debug.Assert(indent >= 2);
          Debug.Assert(indent % 2 == 0);
          var depth = indent / 2;

          if (depth >= ignoreDepth)
            continue;

          ignoreDepth = int.MaxValue;

          if (depth > groupDepth) {
            for (int i = groupDepth + 1; i < depth; ++i) {
              QuantumEditorLog.Error("Missing node at " + i);
              if (onEnter("???")) {
                groupDepth = i;
              } else {
                ignoreDepth = i;
                break;
              }
            }

            if (ignoreDepth < int.MaxValue) {
              continue;
            }
          } else if (depth < groupDepth) {
            Debug.Assert(depth > 0);
            while (groupDepth > depth) {
              --groupDepth;
              onExit();
            }
          }

          var name = line.Substring(indent, valueSplitter - indent);
          bool hasValue = false;
          int valueIndex = valueSplitter;
          while (!hasValue && ++valueIndex < line.Length) {
            hasValue = !char.IsWhiteSpace(line[valueIndex]);
          }

          Debug.Assert(groupDepth == depth);
          if (hasValue) {
            onValue(name, line.Substring(valueIndex));
          } else {
            if (onEnter(name)) {
              groupDepth = depth + 1;
            } else {
              ignoreDepth = depth + 1;
            }
          }
        }

        while (--groupDepth > 0) {
          onExit();
        }
      }
    }

    public static bool TryParseAssetRef(string str, out AssetRef assetRef) {
      var match = Statics.AssetRefRegex.Match(str);
      if (match.Success && AssetGuid.TryParse(match.Groups[1].Value, out var guid, includeBrackets: false)) {
        assetRef = new AssetRef() {
          Id = guid
        };
        return true;
      } else {
        assetRef = default;
        return false;
      }
    }

    [Serializable]
    public sealed class HierarchicalFoldoutCache {
      public List<int> ExpandedPathIDs = new List<int>();

      private const string PathSplitter = "/";
      private Stack<int> _pathHash = new Stack<int>();
      public void BeginPathTraversal() {
        _pathHash.Clear();
        _pathHash.Push(PersistentHashCodeStart);
      }

      public bool IsPathExpanded(int id) {
        return ExpandedPathIDs.BinarySearch(id) >= 0;
      }

      public void PopNestedPath() {
        _pathHash.Pop();
      }

      public int PushNestedPath(string name) {
        var parentHash = _pathHash.Peek();
        var hash = GetPersistentHashCode(PathSplitter, parentHash);
        hash = GetPersistentHashCode(name, hash);
        _pathHash.Push(hash);
        return hash;
      }
      public void SetPathExpanded(int id, bool expanded) {
        var index = ExpandedPathIDs.BinarySearch(id);
        if (expanded) {
          if (index < 0) {
            ExpandedPathIDs.Insert(~index, id);
          }
        } else {
          if (index >= 0) {
            ExpandedPathIDs.RemoveAt(index);
          }
        }
      }
    }

    private static Color GeneratePastelColor(int seed, int minColor = 128) {
      var rng = new System.Random(seed);
      int r = rng.Next(256) + minColor;
      int g = rng.Next(256) + minColor;
      int b = rng.Next(256) + minColor;

      r = Mathf.Min(r / 2, 255);
      g = Mathf.Min(g / 2, 255);
      b = Mathf.Min(b / 2, 255);

      var result = new Color32((byte)r, (byte)g, (byte)b, 255);
      return result;
    }

    private static class Statics {
      public static readonly Regex AssetRefRegex = new Regex(@"^\s*Id: \[([A-Fa-f\d]+)\]\s*$", RegexOptions.Compiled);

      public static readonly Dictionary<Type, ComponentEntry> Components;
      public static readonly Dictionary<string, ComponentEntry> ComponentsByName;
      public static readonly Dictionary<string, ComponentEntry> ComponentsByAQName;
      public static readonly FramePrinter Printer = new FramePrinter();

      public static readonly Dictionary<Type, AssetEntry> Assets;
      public static readonly Dictionary<string, AssetEntry> AssetsByAQName;

      static Statics() {
        
        Components = TypeCache.GetTypesDerivedFrom<IComponent>()
         .Where(x => !x.IsAbstract && !x.IsGenericTypeDefinition)
         .OrderBy(x => x.FullName)
         .ToDictionary(x => x, x => CreateComponentEntry(x));
        ComponentsByName = Components.ToDictionary(x => x.Key.Name, x => x.Value);
        ComponentsByAQName = Components.ToDictionary(x => x.Key.AssemblyQualifiedName, x => x.Value);

        Assets = AppDomain.CurrentDomain.GetAssemblies()
          .SelectMany(x => x.GetLoadableTypes())
          .Where(x => x?.IsSubclassOf(typeof(AssetObject)) == true)
          .ToDictionary(x => x, x => CreateAssetEntry(x));

        AssetsByAQName = Assets.ToDictionary(x => x.Key.AssemblyQualifiedName, x => x.Value);
      }

      private static AssetEntry CreateAssetEntry(Type type) {
        return new AssetEntry() {
          AssetType = type,
          DisplayName = new GUIContent(type.Name, type.FullName),
          ThumbnailColor = GetPersistentColor(type.FullName),
          ThumbnailContent = new GUIContent(GenerateAcronym(type.Name), type.FullName)
        };
      }
      

      private static ComponentEntry CreateComponentEntry(Type type) {
        Debug.Assert(type.GetInterface(typeof(IComponent).FullName) != null);

        var result = new ComponentEntry() {
          ThumbnailColor = GetPersistentColor(type.FullName),
          ComponentType = type,
          DisplayName = new GUIContent(type.Name, type.FullName),
        };
        

        var wrapperFoo = typeof(IQuantumUnityPrototypeWrapperForComponent<>).MakeGenericType(type);

        var matchingTypes = TypeCache.GetTypesDerivedFrom(wrapperFoo);
        if (matchingTypes.Count != 0) {
          // choose the first one
          var monoScript = (MonoScript)UnityInternal.EditorGUIUtility.GetScript(matchingTypes[0].Name);
          if (monoScript != null) {
            result.UnityPrototypeComponentType = monoScript.GetClass();
            result.CustomIcon = UnityInternal.EditorGUIUtility.GetIconForObject(monoScript);
            if (result.CustomIcon != null) {
              result.ThumbnailContent = new GUIContent(result.CustomIcon, type.Name);
              result.ThumbnailColor = Color.white;
            }
          }
        }
        
        if (result.ThumbnailContent == null) {
          result.ThumbnailContent = new GUIContent(GenerateAcronym(type.Name), type.Name);
        }

        return result;
      }
    }

    private class ComponentEntry {
      public Type ComponentType;
      public Texture2D CustomIcon;
      public GUIContent DisplayName;
      //public Type PrototypeType;
      public Color ThumbnailColor;
      public GUIContent ThumbnailContent;
      public Type UnityPrototypeComponentType;
    }

    private class AssetEntry {
      public Type AssetType;
      public GUIContent DisplayName;
      public Color ThumbnailColor;
      public GUIContent ThumbnailContent;
    }


    // [Serializable]
    // public sealed class EntityPrototypeSurrogate {
    //   public FlatEntityPrototypeContainer Container;
    //   public AssetObjectIdentifier Identifier;
    // }
    //
    // [Serializable]
    // public sealed class MapSurrogate {
    //   public Quantum.Map Map;
    //   public List<FlatEntityPrototypeContainer> MapEntities;
    // }

    public abstract class SerializableObjectsContainerBase {
      public DeterministicSessionConfig[] DeterministicSessionConfig = { };
      //public EntityPrototypeSurrogate[] EntityPrototypeSurrogate = { };
      //public MapSurrogate[] MapSurrogate = { };
      public RuntimeConfig[] RuntimeConfig = { };
      public RuntimePlayer[] RuntimePlayer = { };

      internal void Store(object obj) {
        var typeName = obj.GetType().Name;
        var field = GetType().GetFieldOrThrow(typeName);
        var value = Array.CreateInstance(obj.GetType(), 1);
        value.SetValue(obj, 0);
        field.SetValue(this, value);
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumSimulationObjectInspector.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  using StateNodeType = QuantumSimulationObjectInspectorState.NodeType;

  [Serializable]
  public class QuantumSimulationObjectInspector {
    private static Lazy<Skin> _skin = new Lazy<Skin>(() => new Skin());

    [SerializeField]
    private QuantumEditorUtility.HierarchicalFoldoutCache _foldoutCache = new QuantumEditorUtility.HierarchicalFoldoutCache();
    [SerializeField]
    private Vector2 _scroll = default;

    private static Skin skin => _skin.Value;

    public Action<Rect, EntityRef, string> ComponentMenu;
    public Action<Rect, EntityRef> EntityMenu;


    public void DoGUILayout(QuantumSimulationObjectInspectorState inspectorState, bool useScrollView = false) {

      _foldoutCache.BeginPathTraversal();

      if (inspectorState == null) {
        return;
      }

      using (var scrollScope = useScrollView ? new EditorGUILayout.ScrollViewScope(_scroll) : null) {
        _scroll = scrollScope?.scrollPosition ?? default;

        int originalIndent = EditorGUI.indentLevel;
        bool originalHierarchyMode = EditorGUIUtility.hierarchyMode;
        int depth = 0;

        EntityRef currentEntity = new EntityRef() {
          Index = inspectorState.EntityRefIndex,
          Version = inspectorState.EntityRefVersion
        };

        if (!string.IsNullOrEmpty(inspectorState.ExceptionString)) {
          EditorGUILayout.HelpBox(inspectorState.ExceptionString, MessageType.Error, true);
        }

        using (new QuantumEditorGUI.HierarchyModeScope(true)) {
          int expectedEndGroupCount = 0;

          using (new GUILayout.VerticalScope()) {
            for (int nodeIndex = 0; nodeIndex < inspectorState.Nodes.Count; ++nodeIndex) {
              var node = inspectorState.Nodes[nodeIndex];

              // this skips folded groups
              {
                if (expectedEndGroupCount > 0) {
                  if ((node.Type & StateNodeType.ScopeBeginFlag) == StateNodeType.ScopeBeginFlag) {
                    ++expectedEndGroupCount;
                    continue;
                  } else if (node.Type == StateNodeType.ScopeEnd) {
                    --expectedEndGroupCount;
                  }
                }
                if (expectedEndGroupCount > 0)
                  continue;
              }

              if ((node.Type & StateNodeType.ScopeBeginFlag) == StateNodeType.ScopeBeginFlag) {
                float labelOffset = 0;
                Action<Rect> drawThumbnail = null;
                Action<Rect> doMenu = null;

                if ((node.Type & StateNodeType.ComponentScopeBegin) == StateNodeType.ComponentScopeBegin) {
                  labelOffset = 30;
                  drawThumbnail = r => QuantumEditorGUI.ComponentThumbnailPrefix(r, node.Name);
                  if (ComponentMenu != null) {
                    doMenu = r => ComponentMenu(r, currentEntity, node.Name);
                  }
                } else if ((node.Type & StateNodeType.EntityRefScopeBegin) == StateNodeType.EntityRefScopeBegin) {
                  if (EntityMenu != null) {
                    doMenu = r => EntityMenu(r, currentEntity);
                  }
                }

                if (!BeginFoldout(node.Name, depth++, labelOffset, drawThumbnail, doMenu, placeholder: (node.Type & StateNodeType.PlaceholderFlag) == StateNodeType.PlaceholderFlag)) {
                  expectedEndGroupCount = 1;
                }
              } else if (node.Type == StateNodeType.ScopeEnd) {
                EndFoldout(--depth);
              } else if (node.Type == StateNodeType.FramePrinterDump) {
                QuantumEditorUtility.TraverseDump(node.Value,
                  name => {
                    var pathId = _foldoutCache.PushNestedPath(name);
                    var result = EditorGUILayout.Foldout(_foldoutCache.IsPathExpanded(pathId), name);
                    _foldoutCache.SetPathExpanded(pathId, result);
                    if (result) {
                      ++EditorGUI.indentLevel;
                    } else {
                      _foldoutCache.PopNestedPath();
                    }
                    return result;
                  },
                  () => {
                    _foldoutCache.PopNestedPath();
                    --EditorGUI.indentLevel;
                  },
                  (name, value) => {
                    DrawValue(name, value);
                  });
              } else if (node.Type == StateNodeType.SerializableTypeDump) {
                var type = Type.GetType(node.Name);
                if (type == null) {
                  EditorGUILayout.HelpBox($"Unknown type: {node.Name}", MessageType.Error);
                } else {
                  try {
                    if (type.IsSubclassOf(typeof(ScriptableObject))) {
                      var obj = ScriptableObject.CreateInstance(type);
                      JsonUtility.FromJsonOverwrite(node.Value, obj);
                      var sp = new SerializedObject(obj);
                      QuantumEditorGUI.Inspector(sp);
                    } else {
                      var obj = JsonUtility.FromJson(node.Value, type);
                      var sp = QuantumEditorUtility.GetKnownObjectRoot(obj);
                      QuantumEditorGUI.Inspector(sp);
                    }
                  } catch (Exception ex) {
                    EditorGUILayout.TextArea(node.Value, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
                    EditorGUILayout.HelpBox(ex.ToString(), MessageType.Error);
                    GUILayout.FlexibleSpace();
                  }
                }
              } else {
                Debug.Assert(node.Type == StateNodeType.Value);
                DrawValue(node.Name, node.Value);
              }
            }
          }
        }

        Debug.Assert(originalIndent == EditorGUI.indentLevel, $"{originalIndent} {EditorGUI.indentLevel}");
        Debug.Assert(depth == 0);
      }
    }

    private static void DrawValue(string name, string value) {
      var rect = EditorGUILayout.GetControlRect(true);
      rect = EditorGUI.PrefixLabel(rect, new GUIContent(name));
      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        if (QuantumEditorUtility.TryParseAssetRef(value, out var assetRef)) {
          var halfRect = rect.SetWidth(rect.width / 2);
          EditorGUI.SelectableLabel(halfRect, value);
          AssetRefDrawer.DrawAsset(halfRect.AddX(halfRect.width), assetRef.Id);
        } else {
          EditorGUI.TextField(rect, value);
        }
      }
    }

    private bool BeginFoldout(string label, int depth, float thumbnailWidth = 0, Action<Rect> drawThumbnail = null, Action<Rect> doMenu = null, bool placeholder = false) {
      var pathId = _foldoutCache.PushNestedPath(label);
      bool isExpanded = _foldoutCache.IsPathExpanded(pathId);

      bool foldout;

      if (depth == 0) {
        using (new QuantumEditorGUI.HierarchyModeScope(false)) {

          var style = skin.foldoutHeader;

          var rect = GUILayoutUtility.GetRect(GUIContent.none, style);
          using (style.ContentOffsetScope(style.contentOffset + new Vector2(thumbnailWidth, 0))) {
            using (style.FontStyleScope(italic: placeholder)) {
              foldout = BeginFoldoutHeaderGroup(rect, isExpanded, label, style: style, menuAction: doMenu);
            }

            if (drawThumbnail != null) {
              var thumbnailRect = EditorGUI.IndentedRect(rect).AddX(skin.foldoutWidth).SetWidth(thumbnailWidth);
              drawThumbnail(thumbnailRect);
            }
          }
        }
      } 
      else 
      {
        foldout = EditorGUILayout.Foldout(isExpanded, label, true);
      }

      ++EditorGUI.indentLevel;

      _foldoutCache.SetPathExpanded(pathId, foldout);
      return foldout;
    }

    private bool BeginFoldoutHeaderGroup(Rect rect, bool isExpanded, string label, GUIStyle style, Action<Rect> menuAction) {
      var indentedRect = EditorGUI.IndentedRect(rect);
      bool foldout = EditorGUI.BeginFoldoutHeaderGroup(rect, isExpanded, label, style: style, menuAction: menuAction);

      if (Event.current.type == EventType.Repaint) {
        // the titlebar style seems special and doesn't have the foldout; it needs to be drawn manually
        skin.foldoutHeaderToggle.Draw(rect, false, false, foldout, false);
      }

      EditorGUILayout.EndFoldoutHeaderGroup();
      return foldout;
    }

    private void EndFoldout(int depth) {
      --EditorGUI.indentLevel;
      _foldoutCache.PopNestedPath();
    }

    private sealed class Skin {

      public readonly GUIContent[] componentMenuItems = new[] {
        new GUIContent("Remove Component"),
      };

      public readonly GUIContent[] entityMenuItems = new[] {
        new GUIContent("Remove Entity"),
        new GUIContent("Add Components...")
      };

      public readonly GUIStyle foldoutHeader = new GUIStyle(UnityInternal.Styles.InspectorTitlebar) {
        alignment = TextAnchor.MiddleLeft,
      };

      public readonly GUIStyle foldoutHeaderToggle = UnityInternal.Styles.FoldoutTitlebar;

      public readonly GUIStyle foldoutHeaderWithOffset = new GUIStyle(UnityInternal.Styles.InspectorTitlebar) {
        alignment = TextAnchor.MiddleLeft,
        contentOffset = new Vector2(30, 0),
      };
      public float foldoutWidth => 13;
      public float minThumbnailWidth => 30;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/EditorGUI/QuantumSimulationObjectInspectorState.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Text;
  using Photon.Deterministic;
  using Quantum.Core;
  using UnityEngine;

  [Serializable]
  public sealed class QuantumSimulationObjectInspectorState {

    [Flags]
    public enum NodeType {
      Value = 1 << 0,
      ScopeEnd = 1 << 1,
      ScopeBeginFlag = 1 << 2,
      PlaceholderFlag = 1 << 10,

      StructScopeBegin = (1 << 3) | ScopeBeginFlag,
      EntityRefScopeBegin = (1 << 4) | ScopeBeginFlag,
      ComponentScopeBegin = (1 << 5) | ScopeBeginFlag,

      FramePrinterDump = 1 << 7,
      SerializableTypeDump = 1 << 8,
    }

    [Serializable]
    public struct Node {
      public string Name;
      public string Value;
      public NodeType Type;
    }

    public List<Node> Nodes = new List<Node>();
    public int EntityRefVersion;
    public int EntityRefIndex;
    public AssetGuid AssetGuid;
    public string SerializableClassesContainerJson;
    public string ExceptionString;

    public bool FromEntity(FrameBase frame, EntityRef entityRef) {
      try {
        Clear();

        EntityRefIndex = entityRef.Index;
        EntityRefVersion = entityRef.Version;

        BeginEntityRefScope(entityRef);
        try {
          AddValue("Value", entityRef);
          AddValue("IsCullable", frame.IsCullable(entityRef));
          AddValue("IsCulled", frame.IsCulled(entityRef));
        } finally {
          EndScope();
        }

        unsafe {
          for (int componentTypeIndex = 1; componentTypeIndex < ComponentTypeId.Type.Length; componentTypeIndex++) {
            if (frame.Has(entityRef, componentTypeIndex)) {
              var componentType = ComponentTypeId.Type[componentTypeIndex];
              var componentPtr = frame.Unsafe.GetPointer(entityRef, componentTypeIndex);
              BeginComponentScope(componentType);
              try {
                AddInlineDump(frame, componentPtr, componentType);
              } finally {
                EndScope();
              }
            }
          }
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public unsafe bool FromStruct(FrameBase frame, string name, void* ptr, Type type) {
      Clear();
      try {
        BeginStructScope(name);
        try {
          AddInlineDump(frame, ptr, type);
        } finally {
          EndScope();
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public bool FromAsset(AssetObject asset) {
      Clear();
      try {
        var assetName = asset.GetType().Name;

        BeginStructScope(assetName);
        try {
          AddKnowObjectTypeJsonDump(asset);
        } finally {
          EndScope();
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public unsafe bool FromSystem(SystemBase system, Frame frame) {
      Clear();
      try {
        AddValue("Type", system.GetType().Name);
        AddValue("BaseType", system.GetType().BaseType.Name);
        AddValue("IsEnabledSelf", frame.SystemIsEnabledSelf(system));
        AddValue("IsEnabledInHierarchy", frame.SystemIsEnabledInHierarchy(system));
        AddValue("StartEnabled", system.StartEnabled);
        // TODO: maybe add culling and filter information from SystemMainThreadFilter
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public unsafe bool FromPlayer(PlayerRef player, DeterministicSession session, Frame frame) {
      Clear();
      try {
        AddValue("Player", player.ToString());
        AddValue("Input Flags", frame.GetPlayerInputFlags(player));
        AddValue("Actor Id ", frame.PlayerToActorId(player));
        AddValue("Is Player Local ", session.IsPlayerLocal(player));

        // Enabled this when GetPlayerInput() is not code-generated anymore (breaking change)
        //var input = frame.GetPlayerInput(player);
        //BeginStructScope($"Input");
        //try {
        //  if (input != null) {
        //    AddInlineDump(frame, input, typeof(Quantum.Input));
        //  }
        //} finally {
        //  EndScope();
        //}

        var data = frame.GetPlayerData(player);
        BeginStructScope($"RuntimePlayer");
        try {
          if (data != null) {
            AddKnowObjectTypeJsonDump(data);
          }
        } finally {
          EndScope();
        }
        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    public unsafe bool FromRunner(QuantumRunner runner, DeterministicSession session, Frame frame) {
      Clear();
      try {
        AddValue("Id", runner.Id);
        AddValue("State", runner.State);
        AddValue("DeltaTimeType", runner.DeltaTimeType);
        AddValue("RecordingFlags", runner.RecordingFlags);

        BeginStructScope("Communicator");
        try {
          if (runner.Communicator != null) {
            AddValue("IsConnected", runner.Communicator.IsConnected);
            AddValue("ActorNumber", runner.Communicator.ActorNumber);
            AddValue("RoundTripTime", runner.Communicator.RoundTripTime);
          }
        } finally {
          EndScope();
        }

        BeginStructScope("DeterministicSession");
        try {
          AddValue("GameMode", session.GameMode);
          AddValue("Predicted Frame", session.FramePredicted.Number);
          AddValue("Verified Frame", session.FrameVerified.Number);
          AddValue("IsStalling", session.IsStalling);
          AddValue("IsPaused", session.IsPaused);
          AddValue("MaxVerifiedTicksPerUpdate", session.MaxVerifiedTicksPerUpdate);
          AddValue("AccumulatedTime", session.AccumulatedTime);
          AddValue("SimulationTimeElapsed", session.SimulationTimeElapsed);
          AddValue("InitialTick", session.InitialTick);
          AddValue("TimeScale", session.TimeScale);
          AddValue("LocalInputOffset", session.LocalInputOffset);
          AddValue("PredictedFrames", session.PredictedFrames);
          AddValue("IsLockstep", session.IsLockstep);
          AddValue("IsReplayFinished", session.IsReplayFinished);

          BeginStructScope("LocalPlayers");
          try {
            int index = 0;
            foreach (var value in session.LocalPlayers) {
              AddValue($"[{index++}]", value);
            }
          } finally {
            EndScope();
          }

          BeginStructScope("LocalPlayerSlots");
          try {
            int index = 0;
            foreach (var value in session.LocalPlayerSlots) {
              AddValue($"[{index++}]", value);
            }
          } finally {
            EndScope();
          }

          BeginStructScope("PlatformInfo");
          try {
            AddValue("Architecture", session.PlatformInfo.Architecture);
            AddValue("Platform", session.PlatformInfo.Platform);
            AddValue("RuntimeHost", session.PlatformInfo.RuntimeHost);
            AddValue("Runtime", session.PlatformInfo.Runtime);
            AddValue("CoreCount", session.PlatformInfo.CoreCount);
            AddValue("Allocator", session.PlatformInfo.Allocator?.GetType().FullName);
            AddValue("TaskRunner", session.PlatformInfo.TaskRunner?.GetType().FullName);
          } finally {
            EndScope();
          }

          BeginStructScope("Stats");
          try {
            AddValue("Ping", session.Stats.Ping);
            AddValue("Frame", session.Stats.Frame);
            AddValue("Offset", session.Stats.Offset);
            AddValue("Predicted", session.Stats.Predicted);
            AddValue("UpdateTime", session.Stats.UpdateTime);
          } finally {
            EndScope();
          }

        } finally {
          EndScope();
        }

        session.GetLocalConfigs(out var sessionConfig, out var runtimeConfig);
        if (session.SessionConfig != null) {
          sessionConfig = session.SessionConfig;
        }
        if (session.RuntimeConfig != null) {
          runtimeConfig = session.RuntimeConfig;
        }

        BeginStructScope("DeterministicSessionConfig");
        try {
          AddKnowObjectTypeJsonDump(sessionConfig);
        } finally {
          EndScope();
        }

        BeginStructScope("RuntimeConfig");
        try {
          AddKnowObjectTypeJsonDump(frame.Context.AssetSerializer.ConfigFromByteArray<RuntimeConfig>(runtimeConfig, compressed: true));
        } finally {
          EndScope();
        }

        return true;
      } catch (Exception ex) {
        return NotifyException(ex);
      }
    }

    internal void Remove(string path) {
      var hash = QuantumEditorUtility.GetPersistentHashCode(path);

      StringBuilder pathBuilder = new StringBuilder();

      for (int nodeIndex = 0; nodeIndex < Nodes.Count; ++nodeIndex) {
        var node = Nodes[nodeIndex];

        if ((node.Type & NodeType.ScopeBeginFlag) == NodeType.ScopeBeginFlag) {
          pathBuilder.Append("/").Append(node.Name);
          if (pathBuilder.ToString() == path) {
            int expectedEndGroupCount = 1;
            int removeCount = 1;

            for (int j = nodeIndex + 1; j < Nodes.Count && expectedEndGroupCount > 0; ++j, ++removeCount) {
              var n = Nodes[j];
              if ((n.Type & NodeType.ScopeBeginFlag) == NodeType.ScopeBeginFlag) {
                ++expectedEndGroupCount;
              } else if (n.Type == NodeType.ScopeEnd) {
                --expectedEndGroupCount;
              }
            }

            Nodes.RemoveRange(nodeIndex, removeCount);
            return;
          }
        } else if (node.Type == NodeType.ScopeEnd) {
          var idx = pathBuilder.ToString().LastIndexOf('/');
          pathBuilder.Remove(idx, pathBuilder.Length - idx);
        } else {
          pathBuilder.Append("/").Append(node.Name);
          if ( pathBuilder.ToString() == path ) {
            Nodes.RemoveAt(nodeIndex);
            return;
          }
          var idx = pathBuilder.ToString().LastIndexOf('/');
          pathBuilder.Remove(idx, pathBuilder.Length - idx);
        }
      }
    }

    public void FromException(Exception ex) {
      Clear();
      ExceptionString = ex.ToString();
    }

    private bool NotifyException(Exception ex) {
      ExceptionString = ex.ToString();
      return false;
    }


    private void Clear() {
      Nodes.Clear();
      EntityRefVersion = 0;
      EntityRefIndex = 0;
      SerializableClassesContainerJson = string.Empty;
      AssetGuid = default;
      ExceptionString = string.Empty;
    }

    private void BeginStructScope(string name) {
      Nodes.Add(new Node() {
        Name = name,
        Type = NodeType.StructScopeBegin,
      });
    }

    private void BeginComponentScope(Type componentType) {
      Nodes.Add(new Node() {
        Name = componentType.Name,
        Type = NodeType.ComponentScopeBegin,
      });
    }

    private void BeginEntityRefScope(EntityRef entityRef) {
      Nodes.Add(new Node() {
        Name = "Entity",
        Type = NodeType.EntityRefScopeBegin,
      });
    }

    private void EndScope() {
      Nodes.Add(new Node() {
        Type = NodeType.ScopeEnd
      });
    }

    private unsafe void AddKnowObjectTypeJsonDump(object obj) {
      var serializableObject = QuantumEditorUtility.MakeKnownObjectSerializable(obj);
      var json = JsonUtility.ToJson(serializableObject);
      Nodes.Add(new Node() {
        Name = serializableObject.GetType().AssemblyQualifiedName,
        Type = NodeType.SerializableTypeDump,
        Value = json
      });
    }

    private unsafe void AddInlineDump(FrameBase frame, void* ptr, Type type) {
      var dump = QuantumEditorUtility.DumpPointer(frame, ptr, type);
      Nodes.Add(new Node() {
        Name = type.FullName,
        Type = NodeType.FramePrinterDump,
        Value = dump
      });
    }

    private void AddValue(string name, string value) {
      Nodes.Add(new Node() {
        Name = name,
        Type = NodeType.Value,
        Value = value
      });
    }

    private void AddValue<T>(string name, T value) {
      AddValue(name, value.ToString());
    }

    internal bool AddComponentPlaceholder(string componentTypeName) {
      // is there such a placeholder already?
      foreach (var node in Nodes) {
        if (node.Name == componentTypeName && (node.Type & NodeType.PlaceholderFlag) != 0) {
          return false;
        }
      }
      
      Nodes.Add(new Node() {
        Name = componentTypeName,
        Type = NodeType.ComponentScopeBegin | NodeType.PlaceholderFlag,
      });
      EndScope();
      return true;
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/AssetGuidDrawer.cs

namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssetGuid))]
  [QuantumPropertyDrawerMeta(HasFoldout = false)]
  internal unsafe class AssetGuidDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        var str = GetValue(property, out var valueProp).ToString(false);
        
        EditorGUI.BeginChangeCheck();
        
        str = EditorGUI.TextField(position, label, str);
        ClearErrorIfLostFocus();
        
        if (EditorGUI.EndChangeCheck()) {
          if (AssetGuid.TryParse(str, out var guid, includeBrackets: false)) {
            valueProp.longValue = guid.Value;
            property.serializedObject.ApplyModifiedProperties();
          } else {
            SetError($"Failed to parse {str}");
          }
        }
      }
    }

    public static AssetGuid GetValue(SerializedProperty property, out SerializedProperty valueProperty) {
      valueProperty = property.FindPropertyRelativeOrThrow(nameof(AssetGuid.Value));
      return new AssetGuid(valueProperty.longValue);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/AssetObjectIdentifierDrawer.cs

namespace Quantum.Editor {

  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssetObjectIdentifier))]
  internal class AssetObjectIdentifierDrawer : PropertyDrawer {
    private const string PathName = nameof(AssetObjectIdentifier.Path);
    private const string GuidName = nameof(AssetObjectIdentifier.Guid);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      
      var pathProperty = property.FindPropertyRelative(PathName);
      var guidProperty = property.FindPropertyRelative(GuidName);

      return EditorGUIUtility.standardVerticalSpacing +
        QuantumEditorGUI.GetPropertyHeight(pathProperty) +
        QuantumEditorGUI.GetPropertyHeight(guidProperty);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var pathProperty = property.FindPropertyRelative(PathName);
      var guidProperty = property.FindPropertyRelative(GuidName);

      var pathRect = new Rect(position) {
        height = QuantumEditorGUI.GetPropertyHeight(pathProperty)
      };
      
      var guidRect = new Rect(position) {
        y      = position.y + pathRect.height + EditorGUIUtility.standardVerticalSpacing,
        height = QuantumEditorGUI.GetPropertyHeight(guidProperty)
      };

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(pathRect, new GUIContent("Path"), pathProperty, out var pathValueRect)) {
        using (new QuantumEditorGUI.EnabledScope(true)) {
          EditorGUI.SelectableLabel(pathValueRect, pathProperty.stringValue);
        }
      }
      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(guidRect, new GUIContent("Guid"), guidProperty, out var guidValueRect)) {
        using (new QuantumEditorGUI.EnabledScope(true)) {
          EditorGUI.SelectableLabel(guidValueRect, AssetGuidDrawer.GetValue(guidProperty, out _).ToString(false));
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/AssetRefDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(AssetRef))]
  [CustomPropertyDrawer(typeof(AssetRef<>))]
  [QuantumPropertyDrawerMeta(HasFoldout = false)]
  internal class AssetRefDrawer : PropertyDrawerWithErrorHandling {

    public static string RawValuePath = SerializedObjectExtensions.GetPropertyPath((AssetRef x) => x.Id.Value);

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {

      Type assetType;
      
      var fieldType = fieldInfo.FieldType.GetUnityLeafType();
      if (!fieldType.IsGenericType) {
        assetType = typeof(AssetObject);
      } else {
        assetType = fieldType.GetGenericArguments()[0];
      }

      DrawAssetRefSelector(position, property, label, assetType);
    }

    unsafe void DrawAssetRefSelector(Rect position, SerializedProperty property, GUIContent label, Type type) {
      type = type ?? typeof(Quantum.AssetObject);

      var valueProperty = property.FindPropertyRelativeOrThrow(RawValuePath);
      var guid = (AssetGuid)valueProperty.longValue;

      EditorGUI.BeginChangeCheck();
      EditorGUI.BeginProperty(position, label, valueProperty);

      Quantum.AssetObject selected = null;

      if (valueProperty.hasMultipleDifferentValues) {
        selected = QuantumEditorGUI.ForwardObjectField(position, label, null, type, false) as Quantum.AssetObject;
      } else if (!guid.IsValid) {
        position.width -= 25;
        selected = QuantumEditorGUI.ForwardObjectField(position, label, null, type, false) as Quantum.AssetObject;
        var buttonPosition = position.AddX(position.width).SetWidth(20);

        if (GUI.Button(buttonPosition, "+", EditorStyles.miniButton)) {
          // need to choose a concrete type
          var candidates = new List<Type>();
          if (!type.IsAbstract) {
            candidates.Add(type);
          }
          candidates.AddRange(TypeCache.GetTypesDerivedFrom(type).Where(x => !x.IsAbstract && !x.IsGenericTypeDefinition));

          if (candidates.Count == 1) {
            selected = CreateAsset(valueProperty, candidates[0]);
          } else if (candidates.Count > 1) {
            EditorUtility.DisplayCustomMenu(buttonPosition, candidates.Select(x => new GUIContent(x.FullName)).ToArray(), -1, (_, _, selected) => {
              CreateAsset(valueProperty, candidates[selected]);
            }, null);
          }
        }
      } else {
        var rect = EditorGUI.PrefixLabel(position, label);
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          try {
            selected = DrawAsset(rect, guid, type);
          } catch (Exception ex) {
            SetError($"Failed to load {guid}: {ex}");
            selected = (AssetObject)QuantumEditorGUI.ForwardObjectField(rect, null, type, false);
          }
        }
      }

      EditorGUI.EndProperty();

      if (EditorGUI.EndChangeCheck()) {
        valueProperty.longValue = selected != null ? selected.Guid.Value : 0L;
        ClearError(property);
      }
    }

    private static Func<Quantum.AssetObject, Boolean> ObjectFilter(AssetGuid guid, Type type) {
      return
        obj => obj &&
        type.IsInstanceOfType(obj) &&
        obj.Guid == guid;
    }

    internal static Quantum.AssetObject DrawAsset(Rect position, AssetGuid assetGuid, Type assetType = null) {

      assetType = assetType ?? typeof(Quantum.AssetObject);
      Debug.Assert(assetType.IsSubclassOf(typeof(Quantum.AssetObject)) || assetType == typeof(Quantum.AssetObject));
      

      if (!assetGuid.IsValid) {
        return (Quantum.AssetObject)QuantumEditorGUI.ForwardObjectField(position, null, assetType, false);
      }

      if (assetGuid.IsDynamic) {
        // try to get an asset from the main runner
        var frame = QuantumRunner.Default ? QuantumRunner.Default.Game.Frames.Verified : null;
        if (frame != null) {
          var asset = frame.FindAsset<AssetObject>(assetGuid);
          if (asset != null) {
            if (EditorGUI.DropdownButton(position, new GUIContent(asset.ToString()), FocusType.Keyboard)) {
              // serialize asset
              var content = frame.Context.AssetSerializer.PrintObject(asset);
              PopupWindow.Show(position, new TextPopupContent() { Text = content });
            }
          } else {
            QuantumEditorGUI.ForwardObjectField(position, null, assetType, false);
            QuantumEditorGUI.Decorate(position, $"Dynamic Asset {assetGuid} not found", MessageType.Error);
          }
        } else {
          QuantumEditorGUI.ForwardObjectField(position, null, assetType, false);
          QuantumEditorGUI.Decorate(position, $"Dynamic Asset {assetGuid} not found", MessageType.Error);
        }
        return null;
      } else {
        var asset = QuantumUnityDB.GetGlobalAssetEditorInstance(assetGuid);

        Type effectiveAssetType = assetType;
        if (asset != null && asset.GetType() != assetType && !asset.GetType().IsSubclassOf(assetType)) {
          effectiveAssetType = asset.GetType();
        }

        var result = QuantumEditorGUI.ForwardObjectField(position, asset, effectiveAssetType, false);
        if (asset == null) {
          QuantumEditorGUI.Decorate(position, $"Asset {assetGuid} missing", MessageType.Error);
        } else if (effectiveAssetType != assetType) {
          QuantumEditorGUI.Decorate(position, $"Asset type mismatch: expected {assetType}, got {effectiveAssetType}", MessageType.Error);
        }

        return (Quantum.AssetObject)result;
      }
    }

    private static Quantum.AssetObject CreateAsset(SerializedProperty valueProperty, Type assetType) {
      var asset = ScriptableObject.CreateInstance(assetType) as Quantum.AssetObject;
      string assetPath = QuantumEditorSettings.Get(x => x.DefaultNewAssetsLocation, "Assets") + $"/{assetType.Name}.asset";
      assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
      AssetDatabase.CreateAsset(asset, assetPath);
      AssetDatabase.SaveAssets();
      
      valueProperty.longValue = asset.Guid.Value;
      valueProperty.serializedObject.ApplyModifiedProperties();
      return asset;
    }
    
    private sealed class TextPopupContent : PopupWindowContent {

      public string Text;
      private Vector2? _size;
      private Vector2 _scroll;

      public override Vector2 GetWindowSize() {
        if (_size == null) {
          var size = EditorStyles.textArea.CalcSize(new GUIContent(Text));
          size.x += 25; // account for the scroll bar & margins
          size.y += 10; // account for margins
          size.x = Mathf.Min(500, size.x);
          size.y = Mathf.Min(400, size.y);
          _size = size;
        }
        return _size.Value;
      }

      public override void OnGUI(Rect rect) {

        using (new GUILayout.AreaScope(rect)) {
          using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll)) {
            _scroll = scroll.scrollPosition;
            EditorGUILayout.TextArea(Text);
          }
        }
      }
    }

    public bool HasFoldout(SerializedProperty property) {
      return false;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/ColorRGBAPropertyDrawer.cs

namespace Quantum.Editor {
  using Quantum;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ColorRGBA))]
  internal class ColorRGBAPropertyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.BeginChangeCheck();

        var rProp = property.FindPropertyRelativeOrThrow(nameof(ColorRGBA.R));
        var gProp = property.FindPropertyRelativeOrThrow(nameof(ColorRGBA.G));
        var bProp = property.FindPropertyRelativeOrThrow(nameof(ColorRGBA.B));
        var aProp = property.FindPropertyRelativeOrThrow(nameof(ColorRGBA.A));

        Color32 unityColor = new Color32(
          (byte)rProp.intValue,
          (byte)gProp.intValue,
          (byte)bProp.intValue,
          (byte)aProp.intValue
        );

        unityColor = EditorGUI.ColorField(position, label, unityColor);

        if (EditorGUI.EndChangeCheck()) {
          rProp.intValue = unityColor.r;
          gProp.intValue = unityColor.g;
          bProp.intValue = unityColor.b;
          aProp.intValue = unityColor.a;
          property.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/ComponentTypeRefDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(ComponentTypeRef))]
  internal class ComponentTypeRefDrawer : PropertyDrawerWithErrorHandling {
    public struct DrawResult {
      public Type ComponentType;
      public uint ComponentTypeHash;
      public bool Success;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        var result = DrawTypeCombo(position, property, QuantumEditorUtility.ComponentTypes, type => {
          SetValue(property, ComponentTypeRef.FromType(type));
          property.serializedObject.ApplyModifiedProperties();
          ClearError(property);
        });

        if (!result.Success) {
          if (result.ComponentType == null) {
            SetError($"No IComponent type with hash {result.ComponentTypeHash} found");
          } else {
            SetError($"Type {result.ComponentType.Name} exists, but is not in the list of accepted components");
          }
        }
      }
    }



    public static DrawResult DrawTypeCombo(Rect position, SerializedProperty property, IEnumerable<Type> componentTypes, Action<Type> onChanged) {

      var hashProperty = property.FindPropertyRelativeOrThrow(nameof(ComponentTypeRef.RawValue));
      var hashValue = (uint)hashProperty.longValue;
      var componentTypeRef = ComponentTypeRef.FromRaw(hashValue);

      // find matching type
      var componentType = componentTypes.FirstOrDefault(t => ComponentTypeRef.FromType(t) == componentTypeRef);

      return DrawTypeCombo(position, componentType, componentTypes, onChanged);
    }

    public static DrawResult DrawTypeCombo(Rect position, Type componentType, IEnumerable<Type> availableComponentTypes, Action<Type> onChanged) {
      // find matching type
      var found = componentType != null && availableComponentTypes.Contains(componentType);

      bool pressed;

      var thumbnailWidth = QuantumEditorGUI.CalcThumbnailsWidth(1);

      if (componentType == null) {
        pressed = GUI.Button(position, $"None", EditorStyles.popup);
      } else {
        using (new QuantumEditorGUI.PaddingScope(EditorStyles.popup, left: (int)thumbnailWidth)) {
          pressed = GUI.Button(position, componentType.Name, EditorStyles.popup);
        }

      }

      if (componentType != null) {
        QuantumEditorGUI.ComponentThumbnailPrefix(position, componentType);
      }

      if (pressed) {
        QuantumEditorGUI.ShowComponentTypePicker(position, componentType, onChanged, t => availableComponentTypes.Contains(t));
      }

      var result = new DrawResult() {
        ComponentTypeHash = ComponentTypeRef.FromType(componentType).RawValue, ComponentType = componentType
      };

      if (!found) {
        // possible mismatched component type?
        if (componentType != null && QuantumEditorUtility.ComponentTypes.Contains(componentType)) {
          result.Success = false;
        } else {
          result.ComponentType = null;
          result.Success = false;
        }
      } else {
        result.Success = true;
      }

      return result;
    }

    public static ComponentTypeRef GetValue(SerializedProperty property) {
      var hashProperty = property.FindPropertyRelativeOrThrow(nameof(ComponentTypeRef.RawValue));
      var hashValue = (uint)hashProperty.longValue;
      return ComponentTypeRef.FromRaw(hashValue);
    }

    public static void SetValue(SerializedProperty property, ComponentTypeRef value) {
      var hashProperty = property.FindPropertyRelativeOrThrow(nameof(ComponentTypeRef.RawValue));
      hashProperty.longValue = value.RawValue;
    }

#pragma warning disable CS0618
    private static void DrawComponentDropdown(ComponentTypeSetSelector selector, string prefix) {
      GUIContent guiContent;
      float additionalWidth = 0;

      if (selector.ComponentTypeNames.Length == 0) {
        guiContent = new GUIContent($"{prefix}: None");
      } else {
        guiContent = new GUIContent($"{prefix}: ");
        additionalWidth = QuantumEditorGUI.CalcThumbnailsWidth(selector.ComponentTypeNames.Length);
      }

      var buttonSize = EditorStyles.toolbarDropDown.CalcSize(guiContent);
      var originalButtonSize = buttonSize;
      buttonSize.x += additionalWidth;
      buttonSize.x = Math.Max(50, buttonSize.x);

      // var buttonRect = GUILayoutUtility.GetRect(buttonSize.x, buttonSize.y);
      // bool pressed = GUI.Button(buttonRect, guiContent, EditorStyles.toolbarDropDown);
      //
      // var thumbnailRect = buttonRect.AddX(originalButtonSize.x - skin.toolbarDropdownButtonWidth);
      // foreach (var name in selector.ComponentTypeNames) {
      //   thumbnailRect = QuantumEditorGUI.ComponentThumbnailPrefix(thumbnailRect, name, addSpacing: true);
      // }
      //
      // if (pressed) {
      //   QuantumEditorGUI.ShowComponentTypesPicker(buttonRect, selector, onChange: () => {
      //     _needsReload = true;
      //     Repaint();
      //   });
      // }
    }
  }
#pragma warning restore CS0618
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/ComponentTypeSetSelectorDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

#pragma warning disable CS0618
  [CustomPropertyDrawer(typeof(ComponentTypeSetSelector)), Obsolete]
  unsafe class ComponentTypeSetSelectorDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      var selectedTypes = new List<Type>();
      var namesHashProperty = property.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.RawValues));
      for (int i = 0; i < namesHashProperty.arraySize; ++i) {
        var componentTypeRef = ComponentTypeRef.FromRaw((uint)namesHashProperty.GetArrayElementAtIndex(i).longValue);
        if (QuantumEditorUtility.TryGetComponentType(componentTypeRef, out var componentType)) {
          selectedTypes.Add(componentType);
        }
      }

      selectedTypes.Sort((x, y) => x.Name.CompareTo(y.Name));

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        var thumbnailWidth = QuantumEditorGUI.CalcThumbnailsWidth(selectedTypes.Count);
        string guiContent = "None";

        if (selectedTypes.Count > 0) {
          guiContent = string.Join(", ", selectedTypes.Select(x => x.Name));
        }
        
        bool pressed;
        using (new QuantumEditorGUI.PaddingScope(EditorStyles.popup, left: (int)thumbnailWidth)) {
          pressed = GUI.Button(position, guiContent, EditorStyles.popup);
        }

        var thumbPosition = position.SetWidth(QuantumEditorGUI.ThumbnailWidth);
        foreach (var selectedType in selectedTypes) {
          if (thumbPosition.xMax > position.xMax) {
            break;
          }
          thumbPosition = QuantumEditorGUI.ComponentThumbnailPrefix(thumbPosition, selectedType, true);
        }

        if (pressed) {
          QuantumEditorGUI.ShowComponentTypesPicker(position, 
            type => selectedTypes.Contains(type),
            (type, selected) => {
              if (selected) {
                selectedTypes.Add(type);
              } else {
                selectedTypes.Remove(type);
              }
              
              namesHashProperty.arraySize = selectedTypes.Count;
              for (int i = 0; i < selectedTypes.Count; ++i) {
                namesHashProperty.GetArrayElementAtIndex(i).longValue = ComponentTypeRef.FromType(selectedTypes[i]).RawValue;
              }

              property.serializedObject.ApplyModifiedProperties();
            });
        }
      }
    }

    //     public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {
//
//       var componentNames = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.NameHashes));
//
//       string error = ImportObsolete(prop, out var errorType);
//
//       using (new QuantumEditorGUI.PropertyScope(position, label, prop)) {
//         EditorGUI.PropertyField(position, componentNames, label, prop.isExpanded);
//         prop.isExpanded = componentNames.isExpanded;
//       }
//
//       if (!string.IsNullOrEmpty(error)) {
//         position.xMin += EditorGUIUtility.labelWidth;
//         QuantumEditorGUI.Decorate(position.SetLineHeight(), error, errorType);
//       }
//     }
//
//     public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) {
//       var arrayProp = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.ComponentTypeNames));
//       return EditorGUI.GetPropertyHeight(arrayProp, prop.isExpanded);
//     }
//
//     private static string ImportObsolete(SerializedProperty prop, out MessageType errorType) {
//
// #pragma warning disable CS0612 // Type or member is obsolete
//       //var qualifiedNames = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.QualifiedNames));
//       // TODO: Fix after removing obsolete QualifiedName property
//       var qualifiedNames = prop;
// #pragma warning restore CS0612 // Type or member is obsolete
//
//       string error = "";
//       errorType = MessageType.Error;
//
//       if (qualifiedNames.arraySize > 0) {
//
//         var componentNames = prop.FindPropertyRelativeOrThrow(nameof(ComponentTypeSetSelector.ComponentTypeNames));
//
//         // importing from an obsolete property
//         if (componentNames.arraySize == 0) {
//           for (int i = 0; i < qualifiedNames.arraySize; ++i) {
//             var qualifiedName = qualifiedNames.GetArrayElementAtIndex(i);
//             if (Type.GetType(qualifiedName.stringValue) == null) {
//               errorType = MessageType.Error;
//               error = $"Failed to import obsolete QualifiedName: {qualifiedName.stringValue}";
//               break;
//             }
//           }
//
//           if (string.IsNullOrEmpty(error)) {
//             for (int i = 0; i < qualifiedNames.arraySize; ++i) {
//               var qualifiedName = qualifiedNames.GetArrayElementAtIndex(i);
//               var type = Type.GetType(qualifiedName.stringValue, throwOnError: true);
//               componentNames.InsertArrayElementAtIndex(componentNames.arraySize);
//               var componentName = componentNames.GetArrayElementAtIndex(componentNames.arraySize - 1);
//               componentName.stringValue = type.Name;
//               Debug.Log($"Imported obsolete QualifiedName \"{qualifiedName.stringValue}\" to {componentName.propertyPath}", prop.serializedObject.targetObject);
//             }
//
//             qualifiedNames.arraySize = 0;
//             qualifiedNames.serializedObject.ApplyModifiedPropertiesWithoutUndo();
//           }
//         } else {
//           error = $"Obsolete {qualifiedNames.name} has values, but the new {componentNames.name} is not empty. Not importing nor clearing the old property.";
//           errorType = MessageType.Warning;
//         }
//       }
//
//       return error;
//     }
//   }
  }
#pragma warning disable CS0618
}


#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/DictionaryAttributeDrawer.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  internal class DictionaryAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    
    private const string DictionaryKeyPropertyName   = "Key";
    private const string DictionaryValuePropertyName = "Value";

    private static HashSet<SerializedProperty> _dictionaryKeyHash = new HashSet<SerializedProperty>(new SerializedPropertyEqualityComparer());
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      base.OnGUIInternal(position, property, label);
      
      string verifyMessage = VerifyDictionary(property);
      if (!string.IsNullOrEmpty(verifyMessage)) {
        QuantumEditorGUI.Decorate(new Rect(position) { height = EditorGUIUtility.singleLineHeight }, verifyMessage, MessageType.Error, hasLabel: true, drawBorder: true);
      }
    }
    
    private static string VerifyDictionary(SerializedProperty prop) {
      Debug.Assert(prop.isArray);
      try {
        for (int i = 0; i < prop.arraySize; ++i) {
          var keyProperty = prop.GetArrayElementAtIndex(i).FindPropertyRelativeOrThrow(DictionaryKeyPropertyName);
          if (!_dictionaryKeyHash.Add(keyProperty)) {

            // there are duplicates - take the slow and allocating path now
            return string.Join("\n",
              Enumerable.Range(0, prop.arraySize)
               .GroupBy(x => prop.GetArrayElementAtIndex(x).FindPropertyRelative(DictionaryKeyPropertyName), x => x, _dictionaryKeyHash.Comparer)
               .Where(x => x.Count() > 1)
               .Select(x => $"Duplicate keys for elements: {string.Join(", ", x)}")
            );
          }
        }

        return null;

      } finally {
        _dictionaryKeyHash.Clear();
      }
    }
    
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, DictionaryAttribute attribute) {
      return new System.Attribute[0];
    }
#endif
  }
  
  [CustomPropertyDrawer(typeof(DictionaryAttribute))]
  [RedirectCustomPropertyDrawer(typeof(DictionaryAttribute), typeof(DictionaryAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/DictionaryEntryDrawer.cs

namespace Quantum.Editor {

  using Prototypes;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(DictionaryEntry), true)]
  internal class DictionaryEntryDrawer : PropertyDrawer {
#if !UNITY_2023_2_OR_NEWER
    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }
#endif

    private const string DictionaryKeyPropertyName = "Key";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.PropertyField(position, property, label, property.isExpanded);

        if (!property.isExpanded) {
          var keyProp = property.FindPropertyRelativeOrThrow(DictionaryKeyPropertyName);
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
          using (new EditorGUI.DisabledScope(true)) {
            EditorGUI.PropertyField(position.AddXMin(EditorGUIUtility.labelWidth), keyProp, GUIContent.none, true);
          }
        }
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return EditorGUI.GetPropertyHeight(property, label, true);
    }
    
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    class OdinPropertyDrawer : Sirenix.OdinInspector.Editor.OdinValueDrawer<DictionaryEntry> {
      protected override void DrawPropertyLayout(GUIContent label) {
        if (label != null) {
          Property.State.Expanded = EditorGUILayout.Foldout(Property.State.Expanded, label);
          if (!Property.State.Expanded) {
            return;
          }
        }

        using (new EditorGUI.IndentLevelScope(label != null ? 1 : 0)) {
          foreach (var child in Property.Children) {
            child.Draw(child.Label);
          }
        }
      }
    }
#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/DynamicCollectionAttributeDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using Quantum.Core;
  using UnityEditor;
  using UnityEngine;
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
  using Sirenix.OdinInspector.Editor;
#endif

  internal class DynamicCollectionAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    
    private static readonly string DynamicCollectionWarning = $"Collection does not have [{(typeof(Core.FreeOnComponentRemovedAttribute).FullName)}] attribute and needs to be manually disposed in the simulation code to avoid memory leaks.";
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      base.OnGUIInternal(position, property, label);
      
      if (fieldInfo.GetCustomAttribute<FreeOnComponentRemovedAttribute>() == null) {
        var pos = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
        pos.xMin += EditorGUIUtility.labelWidth;
        pos.xMin -= 16;
        QuantumEditorGUI.Decorate(pos, DynamicCollectionWarning, MessageType.Info, hasLabel: false, drawBorder: false);
      }
    }
    
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
    public static Attribute[] ConvertToOdinAttributes(MemberInfo memberInfo, DecoratingPropertyAttribute attribute) {
      return new Attribute[] { new OdinAttributeProxy() };
    }

    class OdinAttributeProxy : Attribute {
    }

    class OdinDrawer : OdinAttributeDrawer<OdinAttributeProxy> {
      private Rect position;

      protected override bool CanDrawAttributeProperty(InspectorProperty property) {
        switch (property.GetUnityPropertyType()) {
          case SerializedPropertyType.ArraySize:
            return true;
          default:
            return false;
        }
      }

      protected override void DrawPropertyLayout(GUIContent label) {
        
        bool hasFreeOnComponentRemoved = Property.Attributes.GetAttribute<FreeOnComponentRemovedAttribute>() != null;

        using (new EditorGUILayout.VerticalScope()) {
          CallNextDrawer(label);
        }

        if (Event.current.type == EventType.Repaint) {
          position = GUILayoutUtility.GetLastRect();
        }
        
        if (position.width > 10 && !hasFreeOnComponentRemoved) {
          var pos = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
          QuantumEditorGUI.Decorate(pos, DynamicCollectionWarning, MessageType.Info, hasLabel: true, drawBorder: false);
        }
      }
    }
#endif
  }
  
  [CustomPropertyDrawer(typeof(DynamicCollectionAttribute))]
  [RedirectCustomPropertyDrawer(typeof(DynamicCollectionAttribute), typeof(DynamicCollectionAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/EntityPrototypeRefWapperDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(QUnityEntityPrototypeRef))]
  internal class EntityPrototypeRefWrapperEditor : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var sceneReferenceProperty = property.FindPropertyRelativeOrThrow(nameof(QUnityEntityPrototypeRef.ScenePrototype));
      var assetRefProperty = property.FindPropertyRelativeOrThrow(nameof(QUnityEntityPrototypeRef.AssetPrototype));
      var assetRefValueProperty = assetRefProperty.FindPropertyRelative("Id.Value");

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        var rect = EditorGUI.PrefixLabel(position, label);

        bool showAssetRef = assetRefValueProperty.longValue != 0 || sceneReferenceProperty.objectReferenceValue == null;
        bool showReference = sceneReferenceProperty.objectReferenceValue != null || assetRefValueProperty.longValue == 0;

        Debug.Assert(showAssetRef || showReference);

        if (showAssetRef && showReference) {
          rect.width /= 2;
        }

        if (showReference) {
          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(rect, sceneReferenceProperty, GUIContent.none);
          }
          rect.x += rect.width;
          if (EditorGUI.EndChangeCheck()) {
            assetRefValueProperty.longValue = 0;
            property.serializedObject.ApplyModifiedProperties();
          }
        }

        if (showAssetRef) {
          EditorGUI.BeginChangeCheck();
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(rect, assetRefProperty, GUIContent.none);
          }
          if (EditorGUI.EndChangeCheck()) {
            sceneReferenceProperty.objectReferenceValue = null;
            property.serializedObject.ApplyModifiedProperties();
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/FixedAnimationCurveDrawer.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FPAnimationCurve))]
  internal class FixedCurveDrawer : PropertyDrawer {

    private Dictionary<string, AnimationCurve> _animationCurveCache = new Dictionary<string, AnimationCurve>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeight(3);
    }

    public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {

      // Get properties accessors.
      var resolutionProperty = prop.FindPropertyRelative("Resolution");
      var samplesProperty = prop.FindPropertyRelative("Samples");
      var startTimeProperty = GetPropertyNext(prop, "StartTime");
      var endTimeProperty = GetPropertyNext(prop, "EndTime");
      var preWrapModeProperty = prop.FindPropertyRelative("PreWrapMode");
      var postWrapModeProperty = prop.FindPropertyRelative("PostWrapMode");
      var preWrapModeOriginalProperty = prop.FindPropertyRelative("OriginalPreWrapMode");
      var postWrapModeOriginalProperty = prop.FindPropertyRelative("OriginalPostWrapMode");
      var keysProperty = prop.FindPropertyRelative("Keys");

      // Default values (because we changed FPAnimationCurve to be a struct)
      if (resolutionProperty.intValue <= 1) {
        resolutionProperty.intValue = 32;
        startTimeProperty.longValue = 0;
        endTimeProperty.longValue = FP.RAW_ONE;
      }

      AnimationCurve animationCurve;

      var propertyKey = prop.propertyPath + "_" + prop.serializedObject.GetHashCode();
      if (!_animationCurveCache.TryGetValue(propertyKey, out animationCurve)) {
        // Load the Quantum data into a Unity animation curve.
        animationCurve = new AnimationCurve();
        _animationCurveCache[propertyKey] = animationCurve;
        animationCurve.preWrapMode = (WrapMode)preWrapModeOriginalProperty.intValue;
        animationCurve.postWrapMode = (WrapMode)postWrapModeOriginalProperty.intValue;
        for (int i = 0; i < keysProperty.arraySize; i++) {
          var keyProperty = keysProperty.GetArrayElementAtIndex(i);
          var key = new Keyframe();
          key.time = FP.FromRaw(GetPropertyNext(keyProperty, "Time").longValue).AsFloat;
          key.value = FP.FromRaw(GetPropertyNext(keyProperty, "Value").longValue).AsFloat;
          key.inTangent = FP.FromRaw(GetPropertyNext(keyProperty, "InTangent").longValue).AsFloat;
          key.outTangent= FP.FromRaw(GetPropertyNext(keyProperty, "OutTangent").longValue).AsFloat;

          // Saving WeightedMode since Quantum SDK 2.1.8
          var weightedModeProperty = keyProperty.FindPropertyRelative("WeightedMode");
          if (weightedModeProperty != null) {
            key.weightedMode = (WeightedMode)weightedModeProperty.intValue;
          }
          else {
            key.weightedMode = WeightedMode.None;
          }

          animationCurve.AddKey(key);

          var leftTangentMode = (AnimationUtility.TangentMode)keyProperty.FindPropertyRelative("TangentModeLeft").intValue;
          var rightTangentMode = (AnimationUtility.TangentMode)keyProperty.FindPropertyRelative("TangentModeRight").intValue;

          // Since 2018.1 key.TangentMode is deprecated. AnimationUtility was already working on 2017, so just do the conversion here. 
          var deprecatedTangentMode = keyProperty.FindPropertyRelative("TangentMode").intValue;
          if (deprecatedTangentMode > 0) { 
            leftTangentMode = ConvertTangetMode(deprecatedTangentMode, true);
            rightTangentMode = ConvertTangetMode(deprecatedTangentMode, false);
#pragma warning disable 0618
            keyProperty.FindPropertyRelative("TangentMode").intValue = key.tangentMode;
#pragma warning restore 0618
            QuantumEditorLog.Log($"FPAnimationCurve: Converted tangent for key {i} from deprecated={deprecatedTangentMode} to left={leftTangentMode}, right={rightTangentMode}");
          }
            
          AnimationUtility.SetKeyLeftTangentMode(animationCurve, animationCurve.length - 1, leftTangentMode);
          AnimationUtility.SetKeyRightTangentMode(animationCurve, animationCurve.length - 1, rightTangentMode);
        }
      }

      EditorGUI.BeginChangeCheck();

      var p = position.SetLineHeight();

      EditorGUI.LabelField(p, label);
      p = p.AddLine();

      EditorGUI.indentLevel++;

      resolutionProperty.intValue = EditorGUI.IntField(p, "Resolution", resolutionProperty.intValue);
      resolutionProperty.intValue = Mathf.Clamp(resolutionProperty.intValue, 2, 1024);

      p = p.AddLine();
      animationCurve = EditorGUI.CurveField(p, "Samples", animationCurve);
      _animationCurveCache[propertyKey] = animationCurve;

      EditorGUI.indentLevel--;

      if (EditorGUI.EndChangeCheck()) {

        // Save information to restore the Unity AnimationCurve.
        keysProperty.ClearArray();
        keysProperty.arraySize = animationCurve.keys.Length;
        for (int i = 0; i < animationCurve.keys.Length; i++) {
          var key = animationCurve.keys[i];
          var keyProperty = keysProperty.GetArrayElementAtIndex(i);
          GetPropertyNext(keyProperty, "Time").longValue = FP.FromFloat_UNSAFE(key.time).RawValue;
          GetPropertyNext(keyProperty, "Value").longValue = FP.FromFloat_UNSAFE(key.value).RawValue;
          try {
            GetPropertyNext(keyProperty, "InTangent").longValue = FP.FromFloat_UNSAFE(key.inTangent).RawValue;
          }
          catch (System.OverflowException) {
            GetPropertyNext(keyProperty, "InTangent").longValue = Mathf.Sign(key.inTangent) < 0.0f ? FP.MinValue.RawValue : FP.MaxValue.RawValue;
          }
          try {
            GetPropertyNext(keyProperty, "OutTangent").longValue = FP.FromFloat_UNSAFE(key.outTangent).RawValue;
          }
          catch (System.OverflowException) {
            GetPropertyNext(keyProperty, "OutTangent").longValue = Mathf.Sign(key.outTangent) < 0.0f ? FP.MinValue.RawValue : FP.MaxValue.RawValue;
          }

          keyProperty.FindPropertyRelative("TangentModeLeft").intValue = (int)AnimationUtility.GetKeyLeftTangentMode(animationCurve, i);
          keyProperty.FindPropertyRelative("TangentModeRight").intValue = (int)AnimationUtility.GetKeyRightTangentMode(animationCurve, i);
          keyProperty.FindPropertyRelative("TangentMode").intValue = 0;
          keyProperty.FindPropertyRelative("WeightedMode").intValue = (byte)key.weightedMode;
        }

        // Save the curve onto the Quantum FPAnimationCurve object via SerializedObject.
        preWrapModeProperty.intValue = (int)GetWrapMode(animationCurve.preWrapMode);
        postWrapModeProperty.intValue = (int)GetWrapMode(animationCurve.postWrapMode);
        preWrapModeOriginalProperty.intValue = (int)animationCurve.preWrapMode;
        postWrapModeOriginalProperty.intValue = (int)animationCurve.postWrapMode;

        // Get the used segment.
        float startTime = animationCurve.keys.Length == 0 ? 0.0f : float.MaxValue;
        float endTime = animationCurve.keys.Length == 0 ? 1.0f : float.MinValue; ;
        for (int i = 0; i < animationCurve.keys.Length; i++) {
          startTime = Mathf.Min(startTime, animationCurve[i].time);
          endTime = Mathf.Max(endTime, animationCurve[i].time);
        }

        startTimeProperty.longValue = FP.FromFloat_UNSAFE(startTime).RawValue;
        endTimeProperty.longValue = FP.FromFloat_UNSAFE(endTime).RawValue;

        // Save the curve inside an array with specific resolution.
        var resolution = resolutionProperty.intValue;
        if (resolution <= 0)
          return;
        samplesProperty.ClearArray();
        samplesProperty.arraySize = resolution + 1;
        var deltaTime = (endTime - startTime) / (float)resolution;
        for (int i = 0; i < resolution + 1; i++) {
          var time = startTime + deltaTime * i;
          var fp = FP.FromFloat_UNSAFE(animationCurve.Evaluate(time));
          GetArrayElementNext(samplesProperty, i).longValue = fp.RawValue;
        }

        prop.serializedObject.ApplyModifiedProperties();
      }
    }

    private static SerializedProperty GetPropertyNext(SerializedProperty prop, string name) {
      var result = prop.FindPropertyRelative(name);
      if (result != null)
        result.Next(true);
      
      return result;
    }

    private static SerializedProperty GetArrayElementNext(SerializedProperty prop, int index) {
      var result = prop.GetArrayElementAtIndex(index);
      result.Next(true);
      return result;
    }

    private static FPAnimationCurve.WrapMode GetWrapMode(WrapMode wrapMode) {
      switch (wrapMode) {
        case WrapMode.Loop:
          return FPAnimationCurve.WrapMode.Loop;
        case WrapMode.PingPong:
          return FPAnimationCurve.WrapMode.PingPong;
        default:
          return FPAnimationCurve.WrapMode.Clamp;
      }
    }

    private static AnimationUtility.TangentMode ConvertTangetMode(int depricatedTangentMode, bool isLeftOrRight) {
      // old to new conversion
      // Left
      // Free     0000001 -> 00000000 (TangentMode.Free)
      // Constant 0000111 -> 00000011 (TangentMode.Constant)
      // Linear   0000101 -> 00000010 (TangentMode.Linear)
      // Right
      // Free     0000001 -> 00000000 (TangentMode.Free)
      // Linear   1000001 -> 00000010 (TangentMode.Constant)
      // Constant 1100001 -> 00000011 (TangentMode.Linear)

      var shift = isLeftOrRight ? 1 : 5;

      if (((depricatedTangentMode >> shift) & 0x3) == (int)AnimationUtility.TangentMode.Linear) {
        return AnimationUtility.TangentMode.Linear;
      }
      else if (((depricatedTangentMode >> shift) & 0x3) == (int)AnimationUtility.TangentMode.Constant) {
        return AnimationUtility.TangentMode.Constant;
      }

      return AnimationUtility.TangentMode.Free;
    }
  }
}



#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/FloatMinMaxDrawer.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Quantum.MinMaxSliderAttribute))]
  class MinMaxSliderDrawer : PropertyDrawer {
    const Single MIN_MAX_WIDTH = 50f;
    const Single SPACING = 1f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var spacing = SPACING * EditorGUIUtility.pixelsPerPoint;
      var min = property.FindPropertyRelative("Min");
      var minValue = min.floatValue;

      var max = property.FindPropertyRelative("Max");
      var maxValue = max.floatValue;

      var attr = (Quantum.MinMaxSliderAttribute)attribute;

      EditorGUI.PrefixLabel(position, label);

      //var p = position;
      //p.x += EditorGUIUtility.labelWidth + MIN_MAX_WIDTH + spacing;
      //p.width -= EditorGUIUtility.labelWidth + (MIN_MAX_WIDTH + spacing) * 2;

      //EditorGUI.BeginChangeCheck();
      //EditorGUI.MinMaxSlider(p, ref minValue, ref maxValue, attr.Min, attr.Max);
      //if (EditorGUI.EndChangeCheck()) {
      //  min.floatValue = minValue;
      //  max.floatValue = maxValue;
      //}

      var w = ((position.width - EditorGUIUtility.labelWidth) * 0.5f) - spacing;

      var p = position;
      p.x += EditorGUIUtility.labelWidth;
      p.width = w;
      min.floatValue = EditorGUI.FloatField(p, min.floatValue);

      QuantumEditorGUI.Overlay(p, "(Start)");

      p = position;
      p.x += p.width - w;
      p.width = w;
      max.floatValue = EditorGUI.FloatField(p, max.floatValue);

      QuantumEditorGUI.Overlay(p, "(End)");
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/FPPropertyDrawer.cs

namespace Quantum.Editor {
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FP))]
  [CustomPropertyDrawer(typeof(FPVector2))]
  [CustomPropertyDrawer(typeof(FPVector3))]
  [CustomPropertyDrawer(typeof(NullableFP))]
  [CustomPropertyDrawer(typeof(NullableFPVector2))]
  [CustomPropertyDrawer(typeof(NullableFPVector3))]
  [QuantumPropertyDrawerMeta(HasFoldout = false, HandlesUnits = true)]
  class FPPropertyDrawer : PropertyDrawer {
    
    private static readonly GUIContent[] _labels = new[] {
      new GUIContent("X"),
      new GUIContent("Y"),
      new GUIContent("Z"),
      new GUIContent("W")
    };

    private static readonly string[] _paths = new[] {
      "X.RawValue",
      "Y.RawValue",
      "Z.RawValue",
      "W.RawValue"
    };
    
    private static readonly GUIContent _overlayContent     = new GUIContent("(FP)");
    private static readonly int        _multiFieldPrefixId = "MultiFieldPrefixId".GetHashCode();
    
    private const float  SpacingSubLabel      = 2;
    private const string NullableHasValueName = nameof(NullableFP._hasValue);
    private const string NullableValueName    = nameof(NullableFP._value);
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var leafType = fieldInfo.FieldType.GetUnityLeafType();

      var unit = GetUnit(fieldInfo);
      var fpProperty = property;
      var labelWidth = EditorGUIUtility.labelWidth;

      try {
        using (new QuantumEditorGUI.PropertyScope(position, label, property)) {

          if (leafType == typeof(NullableFP) || leafType == typeof(NullableFPVector2) || leafType == typeof(NullableFPVector3)) {
            var hasValueProperty = property.FindPropertyRelativeOrThrow(NullableHasValueName);
            fpProperty = property.FindPropertyRelativeOrThrow(NullableValueName);
            
            EditorGUI.BeginChangeCheck();

            var togglePosition = position;
            togglePosition.height = EditorGUIUtility.singleLineHeight;
            var oldToggleValue = hasValueProperty.longValue != 0;
            var toggleValue    = EditorGUI.Toggle(togglePosition, label, oldToggleValue);

            label                       =  QuantumEditorGUI.WhitespaceContent;
            EditorGUIUtility.labelWidth += QuantumEditorGUI.CheckboxWidth;

            if (EditorGUI.EndChangeCheck()) {
              hasValueProperty.longValue = toggleValue ? 1 : 0;
              hasValueProperty.serializedObject.ApplyModifiedProperties();
            }

            if (!oldToggleValue) {
              return;
            }
          }
        

          if (leafType == typeof(FP) || leafType == typeof(NullableFP)) {
            DrawIntPropertyAsFP(position, fpProperty.FindPropertyRelativeOrThrow(nameof(FP.RawValue)), label, unit, IsValueInverted(fieldInfo));
          } else if (leafType == typeof(FPVector2) || leafType == typeof(NullableFPVector2)) {
            DoMultiFpProperty(position, fpProperty, label, unit, 2);
          } else if (leafType == typeof(FPVector3) || leafType == typeof(NullableFPVector3)) {
            DoMultiFpProperty(position, fpProperty, label, unit, 3);
          } else {
            EditorGUI.LabelField(position, label, new GUIContent("Unsupported type: " + leafType));
          }
        }
      } finally {
        EditorGUIUtility.labelWidth = labelWidth;
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var leafType = fieldInfo.FieldType.GetUnityLeafType();
      
      if (leafType == typeof(NullableFP) || leafType == typeof(NullableFPVector2) || leafType == typeof(NullableFPVector3)) {
        var hasValueProperty = property.FindPropertyRelativeOrThrow(NullableHasValueName);
        if (hasValueProperty.longValue == 0) {
          return EditorGUIUtility.singleLineHeight;  
        }
      } 
      
      if (leafType == typeof(FP) || leafType == typeof(NullableFP)) {
        return EditorGUIUtility.singleLineHeight;
      } else {
        return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
      }
    }
    
    internal static Rect DoMultiFpProperty(Rect p, SerializedProperty prop, GUIContent label, Units unit, int count) {
      
      int id         = GUIUtility.GetControlID(_multiFieldPrefixId, FocusType.Keyboard, p);
      var spaceCount = Mathf.Max(3, count);
      
      p = UnityInternal.EditorGUI.MultiFieldPrefixLabel(p, id, label, _labels.Length);
      if (p.width > 1) {
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          float w  = (p.width - (spaceCount - 1) * SpacingSubLabel) / spaceCount;
          var   ph = new Rect(p) { width = w };

          for (int i = 0; i < count; ++i) {
            float labelWidth = EditorStyles.label.CalcSize(_labels[i]).x;
            using (new QuantumEditorGUI.LabelWidthScope(labelWidth)) {
              var nested = prop.FindPropertyRelativeOrThrow(_paths[i]);
              DrawIntPropertyAsFP(ph, nested, _labels[i], unit, false);
            }

            ph.x += w + SpacingSubLabel;
          }
        }
      }

      return p;
    }
    
    internal static void DrawIntPropertyAsFP(Rect position, SerializedProperty property, GUIContent label, FieldInfo fieldInfo) {
      DrawIntPropertyAsFP(position, property, label, GetUnit(fieldInfo), IsValueInverted(fieldInfo));
    }

    internal static void DrawIntPropertyAsFP(Rect position, SerializedProperty property, GUIContent label, Units unit, bool invertValue) {
      var rawValue   = property.longValue;
      if (invertValue) {
        rawValue = -rawValue;
      }
      
      double doubleValue = FP.FromRaw(rawValue).AsRoundedDouble;
      
      EditorGUI.BeginChangeCheck();
      
      doubleValue = EditorGUI.DoubleField(position, label, doubleValue);
      
      QuantumEditorGUI.Overlay(position, _overlayContent);
      
      if (unit != Units.None) {
        var unitLabel    = UnitAttributeDrawer.UnitToLabel(unit);
        var shift        = QuantumEditorSkin.OverlayLabelStyle.CalcSize(_overlayContent).x;
        var unitPosition = position.AddWidth(-shift);
        QuantumEditorGUI.Overlay(unitPosition, unitLabel);
      }
      
      if (EditorGUI.EndChangeCheck()) {
        rawValue = FP.FromRoundedDouble_UNSAFE(doubleValue).RawValue;
        if (invertValue) {
          rawValue = -rawValue;
        }
        property.longValue = rawValue;
      }
    }
    
    private static bool IsValueInverted(FieldInfo fieldInfo) {
      bool invertValue = false;
#if !QUANTUM_XY
      // 2D-rotation is special and needs to go through inversion, if using XZ plane
      invertValue = fieldInfo.IsDefined(typeof(Rotation2DAttribute))
                    && (fieldInfo.FieldType.GetUnityLeafType() == typeof(FP) || fieldInfo.FieldType.GetUnityLeafType() == typeof(NullableFP));
#endif
      return invertValue;
    }

    private static Units GetUnit(FieldInfo fieldInfo) {
      return fieldInfo.GetCustomAttribute<UnitAttribute>()?.Unit ?? Units.None;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/FPQuaternionPropertyDrawer.cs

namespace Quantum.Editor {
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(FPQuaternion))]
  [QuantumPropertyDrawerMeta(HandlesUnits = false)]
  internal class FPQuaternionPropertyDrawer : PropertyDrawer {
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
    }

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      
      using (new QuantumEditorGUI.PropertyScope(p, label, prop)) {
        EditorGUI.BeginChangeCheck();
        FPPropertyDrawer.DoMultiFpProperty(p, prop, label, Units.None, 4);
        if (EditorGUI.EndChangeCheck()) {
          var rawX = prop.FindPropertyRelativeOrThrow("X.RawValue");
          var rawY = prop.FindPropertyRelativeOrThrow("Y.RawValue");
          var rawZ = prop.FindPropertyRelativeOrThrow("Z.RawValue");
          var rawW = prop.FindPropertyRelativeOrThrow("W.RawValue");
          Normalize(rawX, rawY, rawZ, rawW);
        }
      }
    }

    private static void Normalize(SerializedProperty rawX, SerializedProperty rawY, SerializedProperty rawZ, SerializedProperty rawW) {
      var x = FP.FromRaw(rawX.longValue).AsDouble;
      var y = FP.FromRaw(rawY.longValue).AsDouble;
      var z = FP.FromRaw(rawZ.longValue).AsDouble;
      var w = FP.FromRaw(rawW.longValue).AsDouble;

      var magnitueSqr = x * x + y * y + z * z + w * w;
      if (magnitueSqr < 0.00001) {
        x = y = z = 0;
        w = 1;
      } else {
        var m = System.Math.Sqrt(magnitueSqr);
        x /= m;
        y /= m;
        z /= m;
        w /= m;
      }

      rawX.longValue = FP.FromFloat_UNSAFE((float)x).RawValue;
      rawY.longValue = FP.FromFloat_UNSAFE((float)y).RawValue;
      rawZ.longValue = FP.FromFloat_UNSAFE((float)z).RawValue;
      rawW.longValue = FP.FromFloat_UNSAFE((float)w).RawValue;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/GizmoOptionalBoolPropertyDrawer.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(OptionalGizmoBool))]
  internal class GizmoOptionalBoolPropertyDrawer : PropertyDrawer {
    // magic
    private object GetTargetObject(SerializedProperty property) {
      var targetObject = property.serializedObject.targetObject;
      var targetObjectClassType = targetObject.GetType();

      var properties = property.propertyPath.Split('.');
      var depth = properties.Length;

      object target = targetObject;
      Type classType = targetObjectClassType;
      
      for (var i = 0; i < depth; i++) {
        var fieldName = properties[i];
        var field = classType.GetField(fieldName);
        
        target = field.GetValue(target);
        classType = target.GetType();
      }

      return target;
    }
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var optional = (OptionalGizmoBool)GetTargetObject(property);

      if (optional.HasValue == false)
        return;

      EditorGUI.BeginProperty(position, label, property);
      EditorGUI.BeginChangeCheck();

      var rect = new Rect(position.x, position.y, position.width, position.height);
      var valueProperty = property.FindPropertyRelative("_value");
      EditorGUI.PropertyField(rect, valueProperty,
        new GUIContent { text = QuantumGizmoEditorUtil.AddSpacesToString(property.name) }, true);

      if (EditorGUI.EndChangeCheck()) {
        valueProperty.serializedObject.ApplyModifiedProperties();
      }

      EditorGUI.EndProperty();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/IntVectorPropertyDrawer.cs

namespace Quantum.Editor {
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(IntVector2))]
  [CustomPropertyDrawer(typeof(IntVector3))]
  [QuantumPropertyDrawerMeta(HasFoldout = false, HandlesUnits = true)]
  class IntVectorPropertyDrawer : PropertyDrawer {
    
    private static readonly GUIContent[] _labels = new[] {
      new GUIContent("X"),
      new GUIContent("Y"),
      new GUIContent("Z"),
    };
    
    private static readonly string[] _paths = new[] {
      "X",
      "Y",
      "Z",
    };

    static readonly int   _multiFieldPrefixId = "MultiFieldPrefixId".GetHashCode();
    private const   float SpacingSubLabel     = 2;
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var leafType = fieldInfo.FieldType.GetUnityLeafType();

      var unit = GetUnit(fieldInfo);
      var labelWidth = EditorGUIUtility.labelWidth;

      try {
        using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
          if (leafType == typeof(IntVector2)) {
            DoMultiIntProperty(position, property, label, unit, 2);
          } else if (leafType == typeof(IntVector3)) {
            DoMultiIntProperty(position, property, label, unit, 3);
          } else {
            EditorGUI.LabelField(position, label, new GUIContent("Unsupported type: " + leafType));
          }
        }
      } finally {
        EditorGUIUtility.labelWidth = labelWidth;
      }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeightWithNarrowModeSupport(1);
    }
    
    internal static Rect DoMultiIntProperty(Rect p, SerializedProperty prop, GUIContent label, Units unit, int count) {
      
      int id         = GUIUtility.GetControlID(_multiFieldPrefixId, FocusType.Keyboard, p);
      var spaceCount = Mathf.Max(3, count);
      
      p = UnityInternal.EditorGUI.MultiFieldPrefixLabel(p, id, label, _labels.Length);
      if (p.width > 1) {
        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          float w  = (p.width - (spaceCount - 1) * SpacingSubLabel) / spaceCount;
          var   ph = new Rect(p) { width = w };

          for (int i = 0; i < count; ++i) {
            float labelWidth = EditorStyles.label.CalcSize(_labels[i]).x;
            using (new QuantumEditorGUI.LabelWidthScope(labelWidth)) {
              var nested = prop.FindPropertyRelativeOrThrow(_paths[i]);
              DrawIntProperty(ph, nested, _labels[i], unit);
            }

            ph.x += w + SpacingSubLabel;
          }
        }
      }

      return p;
    }
   
    internal static void DrawIntProperty(Rect position, SerializedProperty property, GUIContent label, Units unit) {
      var intValue = property.intValue;
      
      EditorGUI.BeginChangeCheck();
      
      intValue = EditorGUI.IntField(position, label, intValue);
      
      if (unit != Units.None) {
        var unitLabel    = UnitAttributeDrawer.UnitToLabel(unit);
        QuantumEditorGUI.Overlay(position, unitLabel);
      }
      
      if (EditorGUI.EndChangeCheck()) {
        property.intValue = intValue;
      }
    }

    private static Units GetUnit(FieldInfo fieldInfo) {
      return fieldInfo.GetCustomAttribute<UnitAttribute>()?.Unit ?? Units.None;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/LayerMaskDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEditorInternal;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Quantum.LayerMask))]
  [QuantumPropertyDrawerMeta(HasFoldout = false)]
  internal class LayerMaskDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      // go into child property (raw)
      prop.Next(true);
      QuantumEditorLog.Assert(prop.name == nameof(Quantum.LayerMask.BitMask));

      using (new QuantumEditorGUI.ShowMixedValueScope(prop.hasMultipleDifferentValues)) {
        // draw field
        EditorGUI.BeginChangeCheck();
        var intValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUI.MaskField(p, label, InternalEditorUtility.LayerMaskToConcatenatedLayersMask(prop.intValue), InternalEditorUtility.layers));
        if (EditorGUI.EndChangeCheck()) {
          prop.intValue = intValue;
          prop.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/LayerValueDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(LayerValue))]
  class LayerValueDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      prop.Next(true);
      Draw(p, prop, label);
    }

    public static void Draw(Rect p, SerializedProperty prop, GUIContent label) {
      prop.intValue = EditorGUI.LayerField(p, label, prop.intValue);
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/LocalReferenceAttributeDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(LocalReferenceAttribute))]
  internal class LocalReferenceAttributeDrawer : PropertyDrawer {

    private string lastError;
    private string lastErrorPropertyPath;

    public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {

      EditorGUI.BeginChangeCheck();
      EditorGUI.PropertyField(position, prop, label);
      if (EditorGUI.EndChangeCheck()) {
        lastError = null;
      }

      if (lastError != null && lastErrorPropertyPath == prop.propertyPath) {
        QuantumEditorGUI.Decorate(position, lastError, MessageType.Error, hasLabel: !QuantumEditorGUI.IsNullOrEmpty(label));
      }

      var reference = prop.objectReferenceValue;
      if (reference == null)
        return;

      var target = prop.serializedObject.targetObject;

      if (target is MonoBehaviour mb) {
        if (reference is Component comp) {
          if (!AreLocal(mb, comp)) {
            prop.objectReferenceValue = null;
            prop.serializedObject.ApplyModifiedProperties();
            lastError = "Use only local references";
          }
        } else {
          lastError = "MonoBehaviour to ScriptableObject not supported yet";
        }
      } else {
        lastError = "ScriptableObject not supported yet";
      }

      if (lastError != null) {
        lastErrorPropertyPath = prop.propertyPath;
      }
    }

    public static bool AreLocal(Component a, Component b) {
      if (EditorUtility.IsPersistent(a)) {
        if (AssetDatabase.GetAssetPath(a) != AssetDatabase.GetAssetPath(b)) {
          return false;
        }
      } else {
        if (a.gameObject.scene != b.gameObject.scene) {
          return false;
        }
      }
      return true;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/MultiTypeReferenceAttributeDrawer.cs

namespace Quantum.Editor {

  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(MultiTypeReferenceAttribute))]
  internal class MultiTypeReferenceAttributeDrawer : PropertyDrawer {

    public override void OnGUI(Rect rect, SerializedProperty prop, GUIContent label) {
      QuantumEditorGUI.MultiTypeObjectField(rect, prop, label, Types);
    }

    public Type[] Types {
      get {
        var attrib = (MultiTypeReferenceAttribute)attribute;
        return attrib.Types;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/OptionalAttributeDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  internal class OptionalAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    protected override void OnGUIInternal(Rect position, SerializedProperty prop, GUIContent label) {
      const float ToggleWidth = 16.0f;
      EditorGUI.BeginChangeCheck();

      TryGetOptionalState(prop, out var optionalEnabled);

      var  height      = EditorGUI.GetPropertyHeight(prop.propertyType, label);
      var  toggleRect  = position.SetHeight(height).SetWidth(EditorGUIUtility.labelWidth + ToggleWidth);
      bool toggleValue = optionalEnabled == true;

      using (new QuantumEditorGUI.PropertyScope(toggleRect, label, prop)) {
        using (new QuantumEditorGUI.ShowMixedValueScope(optionalEnabled == null)) {
          toggleValue = EditorGUI.Toggle(toggleRect, label, toggleValue);
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        SetOptionalState(prop, toggleValue);
      }

      float labelWidth = EditorGUIUtility.labelWidth;

      if (optionalEnabled == true) {
        // in case of single-line properties, expand the label so that the default drawer
        // won't draw on top of the toggle
        if (position.height < 2 * EditorGUIUtility.singleLineHeight) {
          EditorGUIUtility.labelWidth = labelWidth += ToggleWidth;
        }

        label = QuantumEditorGUI.WhitespaceContent;
        base.OnGUIInternal(position, prop, label);
      } else {
        prop.isExpanded = false;
      }

      EditorGUIUtility.labelWidth = labelWidth;
    }

    internal bool TryGetOptionalState(SerializedProperty property, out bool? optionalEnabled) {
      var optional         = (OptionalAttribute)attribute;
      var optionalProperty = property.FindPropertyRelativeToParent(optional.EnabledPropertyPath);

      if (optionalProperty == null) {
        //Debug.LogAssertion($"Optional flag {optional.EnabledPropertyPath} not found for {property.propertyPath}");
        optionalEnabled = default;
        return false;
      } else {
        if (optionalProperty?.type == nameof(QBoolean)) {
          optionalProperty = optionalProperty.FindPropertyRelativeOrThrow(nameof(QBoolean.Value));
        }

        if (optionalProperty.hasMultipleDifferentValues) {
          optionalEnabled = null;
        } else {
          optionalEnabled = optionalProperty?.boolValue ?? true;
        }

        return true;
      }
    }

    internal void SetOptionalState(SerializedProperty property, bool value) {
      var optional         = (OptionalAttribute)attribute;
      var optionalProperty = property.FindPropertyRelativeToParent(optional.EnabledPropertyPath);
      if (optionalProperty != null) {
        if (optionalProperty.type == nameof(QBoolean)) {
          optionalProperty = optionalProperty.FindPropertyRelativeOrThrow(nameof(QBoolean.Value));
        }

        optionalProperty.boolValue = value;
        optionalProperty.serializedObject.ApplyModifiedProperties();
      }
    }
    
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
    public static Attribute[] ConvertToOdinAttributes(MemberInfo memberInfo, OptionalAttribute attribute) {
      // not supported yet
      return Array.Empty<Attribute>();
    }
#endif
  }
  
  
  [CustomPropertyDrawer(typeof(OptionalAttribute))]
  [RedirectCustomPropertyDrawer(typeof(OptionalAttribute), typeof(OptionalAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/PlayerRefDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Quantum.PlayerRef))]
  class PlayerRefDrawer : PropertyDrawer {

    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      EditorGUI.BeginProperty(p, label, prop);
      EditorGUI.BeginChangeCheck();

      var valueProperty = prop.FindPropertyRelativeOrThrow(nameof(PlayerRef._index));
      int value = valueProperty.intValue;

      var toggleRect = EditorGUI.PrefixLabel(p, GUIUtility.GetControlID(FocusType.Passive), label);
      toggleRect.width = Mathf.Min(toggleRect.width, QuantumEditorGUI.CheckboxWidth);

      var hasValue = value > 0;

      using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
        if (EditorGUI.Toggle(toggleRect, GUIContent.none, hasValue) != hasValue) {
          value = hasValue ? 0 : 1;
        }
      }

      if (hasValue) {
        EditorGUIUtility.labelWidth += QuantumEditorGUI.CheckboxWidth;
        value = EditorGUI.IntSlider(p, QuantumEditorGUI.WhitespaceContent, value, 1, Quantum.Input.MAX_COUNT);
        EditorGUIUtility.labelWidth -= QuantumEditorGUI.CheckboxWidth;
      }

      if (EditorGUI.EndChangeCheck()) {
        valueProperty.intValue = value;
      }
      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      return QuantumEditorGUI.GetLinesHeight(1);
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/QBooleanDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(QBoolean))]
  internal class QBooleanDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      prop = GetValueProperty(prop);

      EditorGUI.BeginChangeCheck();
      bool value = EditorGUI.Toggle(p, label, prop.GetIntegerValue() != 0);
      if (EditorGUI.EndChangeCheck()) {
        prop.SetIntegerValue(value ? 1 : 0);
      }
    }

    public static SerializedProperty GetValueProperty(SerializedProperty root) {
      var prop = root.Copy();
      prop.Next(true);
      Debug.Assert(prop.name == nameof(QBoolean.Value));
      return prop;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/QEnumDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
  using Sirenix.OdinInspector.Editor;
#endif

  [CustomPropertyDrawer(typeof(QEnum8<>))]
  [CustomPropertyDrawer(typeof(QEnum16<>))]
  [CustomPropertyDrawer(typeof(QEnum32<>))]
  [CustomPropertyDrawer(typeof(QEnum64<>))]
  internal class EnumWrappersDrawer : PropertyDrawer {

    private class EnumData {
      public long[]  Values;
      public string[] Names;
      public bool     IsFlags;
    }

    private Lazy<EnumData> _enumData;

    public EnumWrappersDrawer() {
      _enumData = new Lazy<EnumData>(() => {
        var fieldType          = fieldInfo.FieldType.GetUnityLeafType();
        var enumType           = fieldType.GetGenericArguments()[0];
        var enumUnderlyingType = Enum.GetUnderlyingType(enumType);

        var labels    = Enum.GetNames(enumType);
        var rawValues = Enum.GetValues(enumType);

        var values = new long[rawValues.Length];
        for (int i = 0; i < rawValues.Length; ++i) {
          if (enumUnderlyingType == typeof(int) || enumUnderlyingType == typeof(long)
              || enumUnderlyingType == typeof(short) || enumUnderlyingType == typeof(byte)) {
            values[i] = Convert.ToInt64(rawValues.GetValue(i));
          } else {
            values[i] = unchecked((long)Convert.ToUInt64(rawValues.GetValue(i)));
          }
        }

        return new EnumData() {
          Names = labels,
          Values = values,
          IsFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null
        };
      });
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {

        List<int> selectedIndices = new List<int>();

        var valueProperty = property.FindPropertyRelativeOrThrow("Value");
        var currentValue  = valueProperty.longValue;

        // find out what to show
        var enumData = _enumData.Value;
        var isFlags  = enumData.IsFlags;
        var values   = enumData.Values;
        var names    = enumData.Names;

        for (int i = 0; i < values.Length; ++i) {
          if (isFlags == false) {
            if (currentValue == values[i]) {
              selectedIndices.Add(i);
              break;
            }
          } else if ((currentValue & values[i]) == values[i]) {
            selectedIndices.Add(i);
          }
        }

        string labelValue;
        if (selectedIndices.Count == 0) {
          if (isFlags && currentValue == 0) {
            labelValue = "Nothing";
          } else {
            labelValue = "";
          }
        } else if (selectedIndices.Count == 1) {
          labelValue = names[selectedIndices[0]];
        } else {
          Debug.Assert(isFlags);
          if (selectedIndices.Count == values.Length) {
            labelValue = "Everything";
          } else {
            labelValue = string.Join(", ", selectedIndices.Select(x => names[x]));
          }
        }

        var r = EditorGUI.PrefixLabel(position, label);
        if (EditorGUI.DropdownButton(r, new GUIContent(labelValue), FocusType.Keyboard)) {
          if (isFlags) {
            var       allOptions = new[] { "Nothing", "Everything" }.Concat(names).ToArray();
            List<int> allIndices = new List<int>();
            if (selectedIndices.Count == 0)
              allIndices.Add(0); // nothing
            else if (selectedIndices.Count == values.Length)
              allIndices.Add(1); // everything
            allIndices.AddRange(selectedIndices.Select(x => x + 2));

            UnityInternal.EditorUtility.DisplayCustomMenu(r, allOptions, allIndices.ToArray(), (userData, options, selected) => {
              if (selected == 0) {
                valueProperty.longValue = 0;
              } else if (selected == 1) {
                valueProperty.longValue = 0;
                foreach (var value in values) {
                  valueProperty.longValue |= value;
                }
              } else {
                selected -= 2;
                if (selectedIndices.Contains(selected)) {
                  valueProperty.longValue &= (~values[selected]);
                } else {
                  valueProperty.longValue |= values[selected];
                }
              }

              valueProperty.serializedObject.ApplyModifiedProperties();
            }, null);
          } else {
            UnityInternal.EditorUtility.DisplayCustomMenu(r, names, selectedIndices.ToArray(), (userData, options, selected) => {
              if (!selectedIndices.Contains(selected)) {
                valueProperty.longValue = (long)values[selected];
                valueProperty.serializedObject.ApplyModifiedProperties();
              }
            }, null);
          }
        }
      }
    }
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    abstract class OdinDrawerBase<T> : OdinValueDrawer<T> {
      protected InspectorProperty _valueProperty;
      protected override void Initialize() {
        base.Initialize();
        _valueProperty = Property.Children.Get("Value");
      }

      public override bool CanDrawTypeFilter(Type type) {
        return base.CanDrawTypeFilter(type) && GeneralDrawerConfig.Instance.UseImprovedEnumDropDown;
      }
    }
    
    class OdinDrawer8<T> : OdinDrawerBase<QEnum8<T>> where T : unmanaged {
      protected override void DrawPropertyLayout(GUIContent label) {
        var entry = _valueProperty.ValueEntry;
        entry.WeakSmartValue = Convert.ToSByte(EnumSelector<T>.DrawEnumField(label, (T)Enum.ToObject(typeof(T), entry.WeakSmartValue)));
      }
    }
    class OdinDrawer16<T> : OdinDrawerBase<QEnum16<T>> where T : unmanaged {
      protected override void DrawPropertyLayout(GUIContent label) {
        var entry = _valueProperty.ValueEntry;
        entry.WeakSmartValue = Convert.ToInt16(EnumSelector<T>.DrawEnumField(label, (T)Enum.ToObject(typeof(T), entry.WeakSmartValue)));
      }
    }
    class OdinDrawer32<T> : OdinDrawerBase<QEnum32<T>> where T : unmanaged {
      protected override void DrawPropertyLayout(GUIContent label) {
        var entry = _valueProperty.ValueEntry;
        entry.WeakSmartValue = Convert.ToInt32(EnumSelector<T>.DrawEnumField(label, (T)Enum.ToObject(typeof(T), entry.WeakSmartValue)));
      }
    }
    class OdinDrawer64<T> : OdinDrawerBase<QEnum64<T>> where T : unmanaged {
      protected override void DrawPropertyLayout(GUIContent label) {
        var entry = _valueProperty.ValueEntry;
        entry.WeakSmartValue = Convert.ToInt64(EnumSelector<T>.DrawEnumField(label, (T)Enum.ToObject(typeof(T), entry.WeakSmartValue)));
      }
    }
#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/QStringDrawer.cs

namespace Quantum.Editor {
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  internal partial class QStringDrawer : PropertyDrawer {

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

      Encoding encoding;
      {
        var fieldType = fieldInfo.FieldType;
        if (fieldType.GetInterface($"Quantum.{nameof(IQStringUtf8)}") != null) {
          encoding = Encoding.UTF8;
        } else if (fieldType.GetInterface($"Quantum.{nameof(IQString)}") != null) {
          encoding = Encoding.Unicode;
        } else {
          throw new System.NotSupportedException($"Unknown string type: {fieldType.FullName}");
        }
      }

      var bytesProperty = property.FindPropertyRelativeOrThrow("Bytes");
      var byteCountProperty = property.FindPropertyRelativeOrThrow("ByteCount");

      Debug.Assert(bytesProperty.isFixedBuffer);

      int byteCount = Mathf.Min(byteCountProperty.intValue, bytesProperty.fixedBufferSize);

      byte[] buffer = new byte[byteCount];
      for (int i = 0; i < byteCount; ++i) {
        buffer[i] = (byte)bytesProperty.GetFixedBufferElementAtIndex(i).intValue;
      }

      var str = encoding.GetString(buffer, 0, byteCount);

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
          str = EditorGUI.TextField(position, str);
        }

        QuantumEditorGUI.Overlay(position, $"({byteCount} B)");

        if (EditorGUI.EndChangeCheck()) {
          buffer = encoding.GetBytes(str);
          byteCount = Mathf.Min(buffer.Length, bytesProperty.fixedBufferSize);
          for (int i = 0; i < byteCount; ++i) {
            bytesProperty.GetFixedBufferElementAtIndex(i).intValue = buffer[i];
          }
          byteCountProperty.intValue = byteCount;
          property.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/QUnityComponentPrototypeRefDrawer.cs

namespace Quantum.Editor {

  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(QUnityComponentPrototypeRef<>), true)]
  internal class QUnityComponentPrototypeRefDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var elementType = fieldInfo.FieldType.GetUnityLeafType();
      var wrapperType = elementType.IsGenericType ? elementType.GetGenericArguments()[0] : typeof(QuantumUnityComponentPrototype);

      Type componentType    = null;
      
      var  wrapperInterface = wrapperType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IQuantumUnityPrototypeWrapperForComponent<>));
      if (wrapperInterface != null) {
        componentType = wrapperInterface.GetGenericArguments()[0];
      }
      
      ClearError(property);
      DrawMultiField(position, property, label, componentType);
    }

    public void DrawMultiField(Rect position, SerializedProperty property, GUIContent label, Type specificComponentType) {

      var sceneReferenceProperty = property.FindPropertyRelativeOrThrow(nameof(QUnityComponentPrototypeRef<QuantumUnityComponentPrototype>.ScenePrototype));
      var assetRefProperty       = property.FindPropertyRelativeOrThrow(nameof(QUnityComponentPrototypeRef<QuantumUnityComponentPrototype>.AssetPrototype));
      var assetRefValueProperty  = assetRefProperty.FindPropertyRelativeOrThrow("Id.Value");
      var assetTypeProperty      = property.FindPropertyRelativeOrThrow(nameof(QUnityComponentPrototypeRef<QuantumUnityComponentPrototype>.AssetComponentType));
      
      var assetRefValue  = new AssetRef<EntityPrototype>(assetRefValueProperty.longValue);
      var assetTypeValue = ComponentTypeRefDrawer.GetValue(assetTypeProperty);

      EditorGUI.BeginChangeCheck();

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {

        bool showAssetRef  = assetRefValue.IsValid || sceneReferenceProperty.objectReferenceValue == null;
        bool showReference = sceneReferenceProperty.objectReferenceValue != null || !assetRefValue.IsValid;
        bool showTypeCombo = showAssetRef && specificComponentType == null;

        if (showAssetRef && showReference) {
          position.width /= 2;
        } else if (showTypeCombo) {
          position.width /= 2;
        }

        if (showReference) {
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(position, sceneReferenceProperty, GUIContent.none);
          }

          position.x += position.width;
        }

        if (showAssetRef) {
          using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
            EditorGUI.PropertyField(position, assetRefProperty, GUIContent.none);
          }

          position.x += position.width;
          
          string error = null;
          if (assetRefValue.IsValid) {
            if (QuantumUnityDB.TryGetGlobalAssetEditorInstance(assetRefValue, out var asset)) {
              var availableComponentTypes = asset.Container.Components?.Select(x => x.ComponentType).ToArray() ?? Array.Empty<Type>();
              if (specificComponentType == null) {
                // any component will do
                QuantumEditorUtility.TryGetComponentType(assetTypeValue, out var componentType);
                var result = ComponentTypeRefDrawer.DrawTypeCombo(position, componentType, availableComponentTypes, x => {
                  ComponentTypeRefDrawer.SetValue(assetTypeProperty, ComponentTypeRef.FromType(x));
                  assetTypeProperty.serializedObject.ApplyModifiedProperties();
                  ClearError(property);
                });
                if (!result.Success) {
                  if (result.ComponentType == null) {
                    SetError($"No IComponent type with hash {result.ComponentTypeHash} found");
                  } else {
                    SetError($"Prototype does not have a {result.ComponentType.Name} component");
                  }
                }
              } else {
                // make sure the component type is valid
                if (Array.FindIndex(availableComponentTypes, t => t == specificComponentType) >= 0) {
                  if (assetTypeValue != ComponentTypeRef.FromType(specificComponentType)) {
                    ComponentTypeRefDrawer.SetValue(assetTypeProperty, ComponentTypeRef.FromType(specificComponentType));
                    property.serializedObject.ApplyModifiedProperties();
                    ClearError(property);
                  }
          
                  ClearError();
                } else {
                  error = $"Prototype does not have {specificComponentType.Name} component";
                }
              }
            }
          }

          if (!string.IsNullOrEmpty(error)) {
            SetError(error);
          }
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        property.serializedObject.ApplyModifiedProperties();
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/ShapeConfigDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(Shape2DConfig))]
  [CustomPropertyDrawer(typeof(Shape2DConfig.CompoundShapeData2D))]
  [CustomPropertyDrawer(typeof(Shape3DConfig))]
  [CustomPropertyDrawer(typeof(Shape3DConfig.CompoundShapeData3D))]
  internal class ShapeConfigDrawer : PropertyDrawer {
    static readonly GUIContent _radiusContent = new("Radius");
    static readonly GUIContent _assetContent = new("Asset");
    static readonly GUIContent _sizeContent = new("Size");
    static readonly GUIContent _extentsContent = new("Extents");
    static readonly GUIContent _centerContent = new("Center");
    static readonly GUIContent _rotationContent = new("Rotation");
    static readonly GUIContent _shapesContent = new("Shapes");
    static readonly GUIContent _persistentContent = new("Is Persistent");
    static readonly GUIContent _tagContent = new("User Tag");
    static readonly GUIContent _heightContent = new("Height");

    static readonly FieldInfo _nativeObjectPtrField = typeof(SerializedObject)
      .GetField("m_NativeObjectPtr", BindingFlags.Instance | BindingFlags.NonPublic);
    
    private int _toolbarState = -1;
    private int _arrayIndex = -1;

    private static ShapeConfigDrawer _currentHandleDrawer;

    private SerializedProperty _currentProperty;

    static ShapeConfigDrawer() {
      SceneView.duringSceneGui += sv => {
        if (_currentHandleDrawer?._currentProperty != null) {
          _currentHandleDrawer.CallSceneGUIHandles(sv);
        }
      };
    }

    private bool SerializedObjectTargetIsNull(SerializedObject so) {
      var obj = _nativeObjectPtrField?.GetValue(so);

      if (obj is IntPtr p) {
        return p == IntPtr.Zero;
      }

      return true;
    }
    
    private void CloseTools() {
      _toolbarState = -1;
      _arrayIndex = -1;
      _currentHandleDrawer = null;
      _currentProperty = null;
    }

    private void CallSceneGUIHandles(SceneView sceneView) {
      if (SerializedObjectTargetIsNull(_currentProperty.serializedObject)) {
        CloseTools();
        return;
      }
      
      var serializedProperty = _currentProperty;

      bool shouldDrawHandles = _toolbarState >= 0;
      Tools.hidden = shouldDrawHandles;

      if (shouldDrawHandles) {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
          CloseTools();
          sceneView.Repaint();
        }

        var targetObject = serializedProperty.serializedObject.targetObject;

        var targetBehaviour = (Behaviour)targetObject;

        var value = serializedProperty.GetTargetObject(out var field, out var parent);

        var color = QuantumGameGizmosSettingsScriptableObject.Global.Settings.DynamicColliders.Color;

        switch (value) {
          case Shape3DConfig config3d:
            var csd3d = new Shape3DConfig.CompoundShapeData3D(config3d);
            DrawShapeHandles3D(targetBehaviour, ref csd3d, color);
            csd3d.CopyToConfig(config3d);
            break;
          case Shape2DConfig config2d:
            var csd2d = new Shape2DConfig.CompoundShapeData2D(config2d);
            DrawShapeHandles2D(targetBehaviour, ref csd2d, color);
            csd2d.CopyToConfig(config2d);
            break;
          case Shape3DConfig.CompoundShapeData3D compoundShapeData3D:
            DrawShapeHandles3D(targetBehaviour, ref compoundShapeData3D, color);

            if (EditorUtility.IsDirty(targetBehaviour)) {
              var array = (Shape3DConfig.CompoundShapeData3D[])field.GetValue(parent);
              array[serializedProperty.GetArrayIndex()] = compoundShapeData3D;
            }

            break;
          case Shape2DConfig.CompoundShapeData2D compoundShapeData2D:
            DrawShapeHandles2D(targetBehaviour, ref compoundShapeData2D, color);

            if (EditorUtility.IsDirty(targetBehaviour)) {
              var array = (Shape2DConfig.CompoundShapeData2D[])field.GetValue(parent);
              array[serializedProperty.GetArrayIndex()] = compoundShapeData2D;
            }

            break;
        }
      }
    }

    private void DrawShapeHandles2D(
      Behaviour qep,
      ref Shape2DConfig.CompoundShapeData2D shapeData2D,
      Color color
    ) {
      var toolsIndex = _toolbarState;
      if (toolsIndex == -1) {
        return;
      }

      ref var capsuleSize2d = ref shapeData2D.CapsuleSize;
      ref var posOffset2D = ref shapeData2D.PositionOffset;
      ref var rotOffset2D = ref shapeData2D.RotationOffset;
      ref var circleRadius = ref shapeData2D.CircleRadius;
      ref var rectangleExtents = ref shapeData2D.BoxExtents;
      ref var edgeExtents = ref shapeData2D.EdgeExtent;

      switch (toolsIndex) {
        case QuantumColliderHandles.EditCollider:
          var s2D = shapeData2D.ShapeType;

          switch (s2D) {
            case Shape2DType.None:
              break;

            case Shape2DType.Circle:
              QuantumColliderHandles.Circle(
                qep,
                posOffset2D,
                ref circleRadius,
                color
              );
              break;

            case Shape2DType.Polygon:
              var asset = QuantumUnityDB.GetGlobalAsset(shapeData2D.PolygonCollider);

              // because this is a quantum value already, we dont need to flip the value
              // but because this method expects a unity value, we need to compensate
              var flipped = rotOffset2D.FlipRotation();

              if (asset != null) {
                QuantumColliderHandles.Polygon(
                  qep,
                  ref asset.Vertices,
                  posOffset2D,
                  flipped,
                  false
                );
              }

              rotOffset2D = flipped.FlipRotation();
              break;

            case Shape2DType.Box:
              QuantumColliderHandles.Rectangle(
                qep,
                posOffset2D,
                rotOffset2D.FlipRotation(),
                ref rectangleExtents,
                true,
                color
              );

              break;

            case Shape2DType.Edge:
              var center = FPVector2.Zero;

              FPVector2 v0 = new FPVector2(
                center.X - edgeExtents,
                center.Y
              );

              FPVector2 v1 = new FPVector2(
                center.X + edgeExtents,
                center.Y
              );

              var v0Old = v0;
              var v1Old = v1;

              var result = QuantumColliderHandles.Edge(
                qep,
                ref v0,
                ref v1,
                posOffset2D,
                rotOffset2D.FlipRotation()
              );

              if (result.V0Changed || result.V1Changed) {
                var t = qep.transform;
                var m = Matrix4x4.TRS(
                  t.TransformPoint(posOffset2D.ToUnityVector3()),
                  t.rotation * rotOffset2D.ToUnityQuaternionDegrees(),
                  t.localScale
                );

                Vector3 p0, p1;

                // Convert to world space while preserving the unmodified handle
                if (result.V0Changed) {
                  p0 = m.MultiplyPoint(v0.ToUnityVector3());
                  p1 = m.MultiplyPoint(v1Old.ToUnityVector3());
                } else {
                  p0 = m.MultiplyPoint(v0Old.ToUnityVector3());
                  p1 = m.MultiplyPoint(v1.ToUnityVector3());
                }

                var worldCenter = (p0 + p1) / 2;
                var worldDir = (p1 - p0).normalized.ToFPVector2();
                var newLength = Vector3.Distance(p0, p1);

                posOffset2D = t.InverseTransformPoint(worldCenter).ToFPVector2();

                rotOffset2D = FPMath.Atan2(worldDir.Y, worldDir.X) * FP.Rad2Deg;

                edgeExtents = (newLength / 2).ToFP();
              }

              break;

            case Shape2DType.Compound:
              break;

            case Shape2DType.Capsule:
              QuantumColliderHandles.Capsule2D(
                qep,
                posOffset2D,
                rotOffset2D,
                ref capsuleSize2d
              );
              break;
          }

          break;
        case QuantumColliderHandles.EditPosition:
          QuantumColliderHandles.Position2D(qep, ref posOffset2D);
          break;
        case QuantumColliderHandles.EditRotation:
          QuantumColliderHandles.Rotation2D(
            qep,
            posOffset2D,
            ref rotOffset2D
          );

          break;
      }
    }

    private void DrawShapeHandles3D(
      Behaviour qep,
      ref Shape3DConfig.CompoundShapeData3D shapeData3D,
      Color color
    ) {
      var toolsIndex = _toolbarState;
      if (toolsIndex == -1) {
        return;
      }

      ref var posOffset3D = ref shapeData3D.PositionOffset;
      ref var rotOffset3D = ref shapeData3D.RotationOffset;
      ref var boxExtents = ref shapeData3D.BoxExtents;
      ref var capsuleHeight3d = ref shapeData3D.CapsuleHeight;
      ref var capsuleRadius3d = ref shapeData3D.CapsuleRadius;
      ref var sphereRadius = ref shapeData3D.SphereRadius;

      switch (toolsIndex) {
        case QuantumColliderHandles.EditCollider:

          var s3D = shapeData3D.ShapeType;

          switch (s3D) {
            case Shape3DType.Sphere:
              QuantumColliderHandles.Sphere(
                qep,
                posOffset3D,
                ref sphereRadius,
                color
              );
              break;
            case Shape3DType.Box:
              QuantumColliderHandles.Box(
                qep,
                posOffset3D,
                rotOffset3D,
                ref boxExtents,
                true,
                color
              );
              break;

            case Shape3DType.Capsule:
              QuantumColliderHandles.Capsule3D(
                qep,
                posOffset3D,
                rotOffset3D,
                ref capsuleRadius3d,
                ref capsuleHeight3d,
                color
              );
              break;

            case Shape3DType.Compound:
            case Shape3DType.Mesh:
            case Shape3DType.Terrain:
            case Shape3DType.None:

              break;
          }

          break;

        case QuantumColliderHandles.EditPosition:
          QuantumColliderHandles.Position3D(qep, ref posOffset3D);
          break;

        case QuantumColliderHandles.EditRotation:
          QuantumColliderHandles.Rotation3D(
            qep,
            posOffset3D,
            ref rotOffset3D
          );
          break;
      }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      bool hasSourceCollider = property.FindPropertyRelativeToParent("^SourceCollider")?.objectReferenceValue;

      var configType = fieldInfo.FieldType.GetUnityLeafType();

      var parentDepth = property.depth;

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        position = position.SetLineHeight();

        Debug.Assert(nameof(Shape2DConfig.ShapeType) == nameof(Shape3DConfig.ShapeType));
        Debug.Assert(nameof(Shape2DConfig.UserTag) == nameof(Shape3DConfig.UserTag));

        TryDrawToolbar(ref position, property);

        property.Next(true);
        QuantumEditorLog.Assert(property.name == nameof(Shape2DConfig.ShapeType));

        var shapeType = property.intValue;

        using (new QuantumEditorGUI.DisabledGroupScope(hasSourceCollider)) {
          using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
            EditorGUI.PropertyField(position, property, label, false);

            if (configType == typeof(Shape2DConfig.CompoundShapeData2D) && shapeType == (int)Shape2DType.Compound ||
                configType == typeof(Shape3DConfig.CompoundShapeData3D) && shapeType == (int)Shape3DType.Compound) {
              QuantumEditorGUI.Decorate(position, "Nested compound shapes are not supported.", MessageType.Error, true);
            } else if ((configType == typeof(Shape3DConfig) || configType == typeof(Shape3DConfig.CompoundShapeData3D)) && (shapeType == (int)Shape3DType.Terrain || shapeType == (int)Shape3DType.Mesh)) {
              QuantumEditorGUI.Decorate(position, "Shape type not supported for dynamic colliders/entities.", MessageType.Error, true);
            } else if (shapeType == 0) {
              QuantumEditorGUI.Decorate(position, "No shape type selected.", MessageType.Warning, true);
            }
          }
        }

        position = position.AddLine();

        EditorGUI.indentLevel++;

        if (configType == typeof(Shape2DConfig) || configType == typeof(Shape2DConfig.CompoundShapeData2D)) {
          while (property.NextVisible(false) && property.depth > parentDepth) {
            var fieldLabel = GetPropertyLabel(property, (Shape2DType)shapeType);

            if (fieldLabel == null) {
              continue;
            }

            position.height = QuantumEditorGUI.GetPropertyHeight(property);

            using (new QuantumEditorGUI.DisabledGroupScope(hasSourceCollider && property.name != nameof(Shape2DConfig.UserTag))) {
              EditorGUI.PropertyField(position, property, fieldLabel, true);
            }

            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            if (shapeType == (int)Shape2DType.Compound && property.name == nameof(Shape2DConfig.IsPersistent)) {
              if (property.boolValue != true) {
                property.boolValue = true;
                property.serializedObject.ApplyModifiedProperties();
              }
            }
          }
        } else {
          while (property.NextVisible(false) && property.depth > parentDepth) {
            var fieldLabel = GetPropertyLabel(property, (Shape3DType)shapeType);

            if (fieldLabel == null) {
              continue;
            }

            position.height = QuantumEditorGUI.GetPropertyHeight(property);

            using (new QuantumEditorGUI.DisabledGroupScope(hasSourceCollider && property.name != nameof(Shape2DConfig.UserTag))) {
              EditorGUI.PropertyField(position, property, fieldLabel, true);
            }

            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            if (shapeType == (int)Shape3DType.Compound && property.name == nameof(Shape3DConfig.IsPersistent)) {
              if (property.boolValue != true) {
                property.boolValue = true;
                property.serializedObject.ApplyModifiedProperties();
              }
            }
          }
        }

        EditorGUI.indentLevel--;
      }
    }

    private void TryDrawToolbar(ref Rect position, SerializedProperty property) {
      var value = _toolbarState;

      if (ShouldDrawToolbar(property)) {
        bool clicked = false;
        bool isArrayElement = property.IsArrayElement();
        bool useRotation = SupportsRotation(property);

        EditorGUI.BeginChangeCheck();

        if (isArrayElement && _arrayIndex >= 0 && _arrayIndex != property.GetArrayIndex()) {
          var unfocusedIndex = -1;
          EditorGUI.BeginChangeCheck();
          QuantumColliderHandles.DrawToolbar(ref position, ref unfocusedIndex, useRotation);
          if (EditorGUI.EndChangeCheck()) {
            value = unfocusedIndex;
          }
        } else {
          QuantumColliderHandles.DrawToolbar(ref position, ref value, useRotation);
        }

        clicked = EditorGUI.EndChangeCheck();

        if (clicked) {
          _currentProperty = property.Copy();

          if (isArrayElement) {
            _arrayIndex = property.GetArrayIndex();
          } else {
            _arrayIndex = -1;
          }

          _toolbarState = value;

          _currentHandleDrawer = this;
        }
      }
    }

    private bool ShouldDrawToolbar(SerializedProperty property) {
      // if we are in a scriptable object, there is nowhere valid to draw handles
      if (property.serializedObject.targetObject is ScriptableObject) {
        return false;
      }

      var type = fieldInfo.FieldType.GetUnityLeafType();

      if (type == typeof(Shape3DConfig) || type == typeof(Shape3DConfig.CompoundShapeData3D)) {
        var shapeType3D = GetShapeType<Shape3DType>(property);
        return shapeType3D != Shape3DType.None && shapeType3D != Shape3DType.Compound && shapeType3D != Shape3DType.Mesh && shapeType3D != Shape3DType.Terrain;
      }

      if (type == typeof(Shape2DConfig) || type == typeof(Shape2DConfig.CompoundShapeData2D)) {
        var shapeType2D = GetShapeType<Shape2DType>(property);
        return shapeType2D != Shape2DType.None && shapeType2D != Shape2DType.Compound;
      }

      return false;
    }

    private T GetShapeType<T>(SerializedProperty property) where T : Enum {
      return (T)Enum.GetValues(typeof(T)).GetValue(property.FindPropertyRelativeOrThrow("ShapeType").enumValueIndex);
    }

    private bool SupportsRotation(SerializedProperty property) {
      var type = fieldInfo.FieldType.GetUnityLeafType();

      if (type == typeof(Shape3DConfig) || type == typeof(Shape3DConfig.CompoundShapeData3D)) {
        var shapeType3D = GetShapeType<Shape3DType>(property);
        return shapeType3D != Shape3DType.Sphere && shapeType3D != Shape3DType.None && shapeType3D != Shape3DType.Compound;
      }

      if (type == typeof(Shape2DConfig) || type == typeof(Shape2DConfig.CompoundShapeData2D)) {
        var shapeType2D = GetShapeType<Shape2DType>(property);
        return shapeType2D != Shape2DType.Circle && shapeType2D != Shape2DType.None && shapeType2D != Shape2DType.Compound;
      }

      return false;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      property.isExpanded = true;

      var configType = fieldInfo.FieldType.GetUnityLeafType();
      var parentDepth = property.depth;

      var height = EditorGUIUtility.singleLineHeight;

      // toolbar height
      if (ShouldDrawToolbar(property)) {
        height += EditorStyles.toolbar.fixedHeight + EditorGUIUtility.standardVerticalSpacing * 2;
      }

      property.Next(true);
      QuantumEditorLog.Assert(property.name == nameof(Shape2DConfig.ShapeType));
      var shapeType = property.intValue;

      while (property.NextVisible(false) && property.depth > parentDepth) {
        if (configType == typeof(Shape2DConfig) || configType == typeof(Shape2DConfig.CompoundShapeData2D)) {
          if (GetPropertyLabel(property, (Shape2DType)shapeType) == null) {
            continue;
          }
        } else {
          if (GetPropertyLabel(property, (Shape3DType)shapeType) == null) {
            continue;
          }
        }

        height += QuantumEditorGUI.GetPropertyHeight(property) + EditorGUIUtility.standardVerticalSpacing;
      }

      return height;
    }

    GUIContent GetPropertyLabel(SerializedProperty property, Shape2DType shapeType) {
      if (shapeType == Shape2DType.None) {
        return null;
      }

      var name = property.name;

      if (string.Equals(name, nameof(Shape2DConfig.PositionOffset), StringComparison.Ordinal)) {
        return shapeType != Shape2DType.Compound ? _centerContent : null;
      }

      if (string.Equals(name, nameof(Shape2DConfig.UserTag), StringComparison.Ordinal)) {
        return shapeType != Shape2DType.Compound ? _tagContent : null;
      }

      if (string.Equals(name, nameof(Shape2DConfig.RotationOffset), StringComparison.Ordinal)) {
        return shapeType != Shape2DType.Compound && shapeType != Shape2DType.Circle ? _rotationContent : null;
      }

      switch (shapeType) {
        case Shape2DType.Circle:
          return string.Equals(name, nameof(Shape2DConfig.CircleRadius), StringComparison.Ordinal) ? _radiusContent : null;
        case Shape2DType.Polygon:
          return string.Equals(name, nameof(Shape2DConfig.PolygonCollider), StringComparison.Ordinal) ? _assetContent : null;
        case Shape2DType.Box:
          return string.Equals(name, nameof(Shape2DConfig.BoxExtents), StringComparison.Ordinal) ? _extentsContent : null;
        case Shape2DType.Edge:
          return string.Equals(name, nameof(Shape2DConfig.EdgeExtent), StringComparison.Ordinal) ? _extentsContent : null;
        case Shape2DType.Compound:
          return string.Equals(name, nameof(Shape2DConfig.CompoundShapes), StringComparison.Ordinal) ? _shapesContent :
            string.Equals(name, nameof(Shape2DConfig.IsPersistent), StringComparison.Ordinal) ? _persistentContent : null;
        case Shape2DType.Capsule:
          return string.Equals(name, nameof(Shape2DConfig.CapsuleSize), StringComparison.Ordinal) ? _sizeContent : null;
      }

      return null;
    }

    GUIContent GetPropertyLabel(SerializedProperty property, Shape3DType shapeType) {
      if (shapeType == Shape3DType.None) {
        return null;
      }

      var name = property.name;

      if (string.Equals(name, nameof(Shape3DConfig.PositionOffset), StringComparison.Ordinal)) {
        return shapeType is Shape3DType.Sphere or Shape3DType.Box or Shape3DType.Capsule ? _centerContent : null;
      }

      if (string.Equals(name, nameof(Shape3DConfig.UserTag), StringComparison.Ordinal)) {
        return shapeType != Shape3DType.Compound ? _tagContent : null;
      }

      if (string.Equals(name, nameof(Shape3DConfig.RotationOffset), StringComparison.Ordinal)) {
        return shapeType is Shape3DType.Box or Shape3DType.Capsule ? _rotationContent : null;
      }

      switch (shapeType) {
        case Shape3DType.Sphere:
          return string.Equals(name, nameof(Shape3DConfig.SphereRadius), StringComparison.Ordinal) ? _radiusContent : null;
        case Shape3DType.Box:
          return string.Equals(name, nameof(Shape3DConfig.BoxExtents), StringComparison.Ordinal) ? _extentsContent : null;
        case Shape3DType.Compound:
          return string.Equals(name, nameof(Shape3DConfig.CompoundShapes), StringComparison.Ordinal) ? _shapesContent :
            string.Equals(name, nameof(Shape3DConfig.IsPersistent), StringComparison.Ordinal) ? _persistentContent : null;
        case Shape3DType.Capsule:
          return string.Equals(name, nameof(Shape3DConfig.CapsuleRadius), StringComparison.Ordinal) ? _radiusContent :
            string.Equals(name, nameof(Shape3DConfig.CapsuleHeight), StringComparison.Ordinal) ? _heightContent : null;
      }

      return null;
    }

#if !UNITY_2023_2_OR_NEWER
    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }
#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/PropertyDrawers/UnionPrototypeDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnionPrototype), true)]
  [CustomPropertyDrawer(typeof(QuantumUnityUnionPrototypeAdapter<>), true)]
  class UnionPrototypeDrawer : PropertyDrawer {
    private const string UnionSelectionFieldName = "_field_used_";

    private static readonly GUIContent NoneContent = new GUIContent("(None)");

#if !UNITY_2023_2_OR_NEWER
    public override bool CanCacheInspectorGUI(SerializedProperty property) {
      return false;
    }
#endif

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var height = EditorGUIUtility.singleLineHeight;
      if (!property.isExpanded) {
        return height;
      }

      var selectedProperty = GetUnionSelectedProperty(property, out _);
      if (selectedProperty != null) {
        height += QuantumEditorGUI.GetPropertyHeight(selectedProperty) + EditorGUIUtility.standardVerticalSpacing;
      }

      return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var selectedProperty = GetUnionSelectedProperty(property, out var fieldUsedProperty);
      var unionType = fieldInfo.FieldType.GetUnityLeafType();
      var wasExpanded = property.isExpanded;

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        var popupRect = position.SetLineHeight();
        var popupLabel = QuantumEditorGUI.WhitespaceContent;
        if (selectedProperty == null) {
          popupLabel = label;
        }

        {
          EditorGUI.BeginChangeCheck();
          var selectedValue = DrawFieldPopup(popupRect, popupLabel, fieldUsedProperty.stringValue, unionType);
          if (EditorGUI.EndChangeCheck()) {
            fieldUsedProperty.stringValue = selectedValue;
          }
        }

        if (selectedProperty != null) {
          property.isExpanded = EditorGUI.Foldout(popupRect, wasExpanded, label);

          if (wasExpanded) {
            using (new EditorGUI.IndentLevelScope()) {
              var valuePosition = position.SetYMin(popupRect.yMax + EditorGUIUtility.standardVerticalSpacing);
              QuantumEditorGUI.ForwardPropertyField(valuePosition, selectedProperty, new GUIContent(selectedProperty.displayName), true);
            }
          }
        }
      }
    }

    private static string DrawFieldPopup(Rect rect, GUIContent label, string value, Type unionType) {
      var fields = GetUnionFields(unionType).OrderBy(x => x.Name).ToArray();
      var values = new[] { new GUIContent(NoneContent) }
       .Concat(fields.Select(x => new GUIContent(x.Name.ToUpperInvariant())))
       .ToArray();

      var selectedIndex = Array.FindIndex(values, x => x.text == value);

      // fallback to "(None)"
      if (selectedIndex < 0 && string.IsNullOrEmpty(value)) {
        selectedIndex = 0;
      }

      selectedIndex = EditorGUI.Popup(rect, label, selectedIndex, values);
      return selectedIndex == 0 ? string.Empty : values[selectedIndex].text;
    }

    private static IEnumerable<FieldInfo> GetUnionFields(Type unionType) {
      return unionType.GetFields().Where(x => x.Name != UnionSelectionFieldName);
    }

    private SerializedProperty GetUnionSelectedProperty(SerializedProperty unionRoot, out SerializedProperty fieldUsedProperty) {
      fieldUsedProperty = unionRoot.FindPropertyRelativeOrThrow(UnionSelectionFieldName);

      foreach (var f in GetUnionFields(fieldInfo.FieldType.GetUnityLeafType())) {
        if (f.Name.Equals(fieldUsedProperty.stringValue, StringComparison.InvariantCultureIgnoreCase)) {
          return unionRoot.FindPropertyRelativeOrThrow(f.Name);
        }
      }

      return null;
    }

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    class OdinPropertyDrawer : Sirenix.OdinInspector.Editor.OdinValueDrawer<UnionPrototype> {
      protected override void DrawPropertyLayout(GUIContent label) {
        var fieldUsedProperty = Property.FindChild(p => p.Name == UnionSelectionFieldName, false);

        var wasExpanded = Property.State.Expanded;

        var selectedPropertyName = (string)fieldUsedProperty.ValueEntry.WeakSmartValue;
        var selectedProperty = string.IsNullOrEmpty(selectedPropertyName) ? null : Property.FindChild(p => p.Name.Equals(selectedPropertyName, StringComparison.OrdinalIgnoreCase), false);

        var popupRect = EditorGUILayout.GetControlRect();
        var foldoutRect = popupRect;
        var popupLabel = QuantumEditorGUI.WhitespaceContent;

        if (label == null) {
          // Odin seems to have an issue with foldouts in arrays; offset needs to be added manually
          if (selectedProperty != null) {
            popupRect.xMin += QuantumEditorGUI.FoldoutWidth;
          }

          popupLabel = null;
        } else if (selectedProperty == null) {
          popupLabel = label;
        }

        EditorGUI.BeginChangeCheck();
        var selectedValue = DrawFieldPopup(popupRect, popupLabel, selectedPropertyName, Property.ValueEntry.TypeOfValue.GetUnityLeafType());
        if (EditorGUI.EndChangeCheck()) {
          fieldUsedProperty.ValueEntry.WeakSmartValue = selectedValue;
        }

        if (selectedProperty != null) {
          Property.State.Expanded = EditorGUI.Foldout(foldoutRect, wasExpanded, label ?? GUIContent.none);

          if (wasExpanded) {
            using (new EditorGUI.IndentLevelScope()) {
              selectedProperty.Draw();
            }
          }
        }
      }
    }
#endif
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditor.Common.cs

// merged Editor

#region IQuantumAssetSourceFactory.cs

namespace Quantum.Editor {
  using UnityEditor;

  /// <summary>
  /// A factory that creates <see cref="IQuantumAssetSource"/> instances for a given asset.
  /// </summary>
  public partial interface IQuantumAssetSourceFactory {
    /// <summary>
    /// The order in which this factory is executed. The lower the number, the earlier it is executed.
    /// </summary>
    int Order { get; }
  }
  
  /// <summary>
  /// A context object that is passed to <see cref="IQuantumAssetSourceFactory"/> instances to create an <see cref="IQuantumAssetSource"/> instance.
  /// </summary>
  public readonly partial struct QuantumAssetSourceFactoryContext {
    /// <summary>
    /// Asset instance ID.
    /// </summary>
    public readonly int    InstanceID;
    /// <summary>
    /// Asset Unity GUID;
    /// </summary>
    public readonly string AssetGuid;
    /// <summary>
    /// Asset name;
    /// </summary>
    public readonly string AssetName;
    /// <summary>
    /// Is this the main asset.
    /// </summary>
    public readonly bool   IsMainAsset;
    /// <summary>
    /// Asset Unity path.
    /// </summary>
    public string AssetPath => AssetDatabaseUtils.GetAssetPathOrThrow(InstanceID);

    /// <summary>
    /// Create a new instance of <see cref="QuantumAssetSourceFactoryContext"/>.
    /// </summary>
    public QuantumAssetSourceFactoryContext(string assetGuid, int instanceID, string assetName, bool isMainAsset) {
      AssetGuid = assetGuid;
      InstanceID = instanceID;
      AssetName = assetName;
      IsMainAsset = isMainAsset;
    }

    /// <summary>
    /// Create a new instance of <see cref="QuantumAssetSourceFactoryContext"/>.
    /// </summary>
    public QuantumAssetSourceFactoryContext(HierarchyProperty hierarchyProperty) {
      AssetGuid = hierarchyProperty.guid;
      InstanceID = hierarchyProperty.instanceID;
      AssetName = hierarchyProperty.name;
      IsMainAsset = hierarchyProperty.isMainRepresentation;
    }
    
    /// <summary>
    /// Create a new instance of <see cref="QuantumAssetSourceFactoryContext"/>.
    /// </summary>
    public QuantumAssetSourceFactoryContext(UnityEngine.Object obj) {
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


#region QuantumAssetSourceFactoryAddressable.cs

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
namespace Quantum.Editor {
  using UnityEditor.AddressableAssets;

  /// <summary>
  /// A <see cref="IQuantumAssetSourceFactory"/> implementation that creates <see cref="QuantumAssetSourceAddressable{TAsset}"/>
  /// if the asset is an Addressable.
  /// </summary>
  public partial class QuantumAssetSourceFactoryAddressable : IQuantumAssetSourceFactory {
    /// <inheritdoc cref="IQuantumAssetSourceFactory.Order"/>
    public const int Order = 800;

    int IQuantumAssetSourceFactory.Order => Order;
    
    /// <summary>
    /// Creates a new instance. Checks if AddressableAssetSettings exists and logs a warning if it does not.
    /// </summary>
    public QuantumAssetSourceFactoryAddressable() {
      if (!AddressableAssetSettingsDefaultObject.SettingsExists) {
        QuantumEditorLog.WarnImport($"AddressableAssetSettings does not exist, Quantum will not be able to use Addressables for asset sources.");
      }
    }
    
    /// <summary>
    /// Creates <see cref="QuantumAssetSourceAddressable{TAsset}"/> if the asset is an Addressable.
    /// </summary>
    protected bool TryCreateInternal<TSource, TAsset>(in QuantumAssetSourceFactoryContext context, out TSource result) 
      where TSource : QuantumAssetSourceAddressable<TAsset>, new()
      where TAsset : UnityEngine.Object {

      if (!AddressableAssetSettingsDefaultObject.SettingsExists) {
        result = default;
        return false;
      }

      var assetsSettings = AddressableAssetSettingsDefaultObject.Settings;
      if (assetsSettings == null) {
        throw new System.InvalidOperationException("Unable to load Addressables settings. This may be due to an outdated Addressables version.");
      }
      
      var addressableEntry = assetsSettings.FindAssetEntry(context.AssetGuid, true);
      if (addressableEntry == null) {
        result = default;
        return false;
      }

      result = new TSource() {
        RuntimeKey = $"{addressableEntry.guid}{(context.IsMainAsset ? string.Empty : $"[{context.AssetName}]")}",
      };
      return true;
    }
  }
}
#endif

#endregion


#region QuantumAssetSourceFactoryResource.cs

namespace Quantum.Editor {
  /// <summary>
  /// A <see cref="IQuantumAssetSourceFactory"/> implementation that creates <see cref="QuantumAssetSourceResource{TAsset}"/>
  /// instances for assets in the Resources folder.
  /// </summary>
  public partial class QuantumAssetSourceFactoryResource : IQuantumAssetSourceFactory {
    /// <inheritdoc cref="IQuantumAssetSourceFactory.Order"/>
    public const int Order = 1000;

    int IQuantumAssetSourceFactory.Order => Order;

    /// <summary>
    /// Creates <see cref="QuantumAssetSourceResource{T}"/> if the asset is in the Resources folder.
    /// </summary>
    protected bool TryCreateInternal<TSource, TAsset>(in QuantumAssetSourceFactoryContext context, out TSource result) 
      where TSource : QuantumAssetSourceResource<TAsset>, new()
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


#region QuantumAssetSourceFactoryStatic.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A <see cref="IQuantumAssetSourceFactory"/> implementation that creates <see cref="QuantumAssetSourceStaticLazy{TAsset}"/>.
  /// </summary>
  public partial  class QuantumAssetSourceFactoryStatic : IQuantumAssetSourceFactory {
    /// <inheritdoc cref="IQuantumAssetSourceFactory.Order"/>
    public const int Order = int.MaxValue;

    int IQuantumAssetSourceFactory.Order => Order;

    /// <summary>
    /// Creates <see cref="QuantumAssetSourceStaticLazy{TAsset}"/>.
    /// </summary>
    protected bool TryCreateInternal<TSource, TAsset>(in QuantumAssetSourceFactoryContext context, out TSource result)
      where TSource : QuantumAssetSourceStaticLazy<TAsset>, new()
      where TAsset : UnityEngine.Object {
      
      if (typeof(TAsset).IsSubclassOf(typeof(Component))) {
        var prefab = (GameObject)EditorUtility.InstanceIDToObject(context.InstanceID);

        result = new TSource() {
          Object = prefab.GetComponent<TAsset>()
        };
        
      } else {
        result = new TSource() {
          Object = new(context.InstanceID)
        };
      }
      return true;
    }
  }
}

#endregion


#region AssetDatabaseUtils.Addressables.cs

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AddressableAssets;
  using UnityEditor.AddressableAssets.Settings;
  using UnityEngine;

  partial class AssetDatabaseUtils {
    /// <summary>
    /// Register a handler that will be called when an addressable asset with a specific label is added or removed.
    /// </summary>
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
    
    internal static AddressableAssetEntry GetAddressableAssetEntry(UnityEngine.Object source) {
      if (source == null || !AssetDatabase.Contains(source)) {
        return null;
      }

      return GetAddressableAssetEntry(GetAssetGuidOrThrow(source));
    }
    
    internal static AddressableAssetEntry GetAddressableAssetEntry(string guid) {
      if (string.IsNullOrEmpty(guid)) {
        return null;
      }

      var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
      return addressableSettings.FindAssetEntry(guid);
    }

    internal static AddressableAssetEntry CreateOrMoveAddressableAssetEntry(UnityEngine.Object source, string groupName = null) {
      if (source == null || !AssetDatabase.Contains(source))
        return null;

      return CreateOrMoveAddressableAssetEntry(GetAssetGuidOrThrow(source), groupName);
    }
    
    internal static AddressableAssetEntry CreateOrMoveAddressableAssetEntry(string guid, string groupName = null) {
      if (string.IsNullOrEmpty(guid)) {
        return null;
      }

      var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;

      AddressableAssetGroup group;
      if (string.IsNullOrEmpty(groupName)) {
        group = addressableSettings.DefaultGroup; 
      } else {
        group = addressableSettings.FindGroup(groupName);
      }
      
      if (group == null) {
        throw new ArgumentOutOfRangeException($"Group {groupName} not found");
      }
      
      var entry = addressableSettings.CreateOrMoveEntry(guid, group);
      return entry;
    }
    
    internal static bool RemoveMoveAddressableAssetEntry(UnityEngine.Object source) {
      if (source == null || !AssetDatabase.Contains(source)) {
        return false;
      }

      return RemoveMoveAddressableAssetEntry(GetAssetGuidOrThrow(source));
    }
    
    internal static bool RemoveMoveAddressableAssetEntry(string guid) {
      if (string.IsNullOrEmpty(guid)) {
        return false;
      }

      var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
      return addressableSettings.RemoveAssetEntry(guid);
    }

    [InitializeOnLoadMethod]
    static void InitializeRuntimeCallbacks() {
      QuantumAddressablesUtils.SetLoadEditorInstanceHandler(LoadEditorInstance);
    }
    
    private static UnityEngine.Object LoadEditorInstance(string runtimeKey) {
      if (string.IsNullOrEmpty(runtimeKey)) {
        return default;
      }

      if (!QuantumAddressablesUtils.TryParseAddress(runtimeKey, out var mainKey, out var subKey)) {
        throw new ArgumentException($"Invalid address: {runtimeKey}", nameof(runtimeKey));
      }

      if (GUID.TryParse(mainKey, out _)) {
        // a guid one, we can load it
        if (string.IsNullOrEmpty(subKey)) {
          var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(mainKey));
          if (asset != null) {
            return asset;
          }
        } else {
          foreach (var subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GUIDToAssetPath(mainKey))) {
            if (subAsset.name == subKey) {
              return subAsset;
            }
          }

          // not returning null here, as there might be a chance for a guid-like address
        }
      }
      
      // need to resort to addressable asset settings
      // path... this sucks
      if (!AddressableAssetSettingsDefaultObject.SettingsExists) {
        QuantumEditorLog.Error($"Unable to load asset: {runtimeKey}; AddressableAssetSettings does not exist");
        return default;
      }
      
      var settings = AddressableAssetSettingsDefaultObject.Settings;
      Assert.Check(settings != null);

      var list = new List<AddressableAssetEntry>();
      settings.GetAllAssets(list, true, entryFilter: x => {
        if (x.IsFolder) {
          return mainKey.StartsWith(x.address, StringComparison.OrdinalIgnoreCase);
        } else {
          return mainKey.Equals(x.address, StringComparison.OrdinalIgnoreCase);
        }
      });

      // given the filtering above, the list will contain more than one if we
      // check for a root asset that has nested assets
      foreach (var entry in list) {
        if (runtimeKey.Equals(entry.address, StringComparison.OrdinalIgnoreCase)) {
          return entry.TargetAsset;
        }
      }

      return default;
    }
  }
}
#endif

#endregion


#region AssetDatabaseUtils.cs

namespace Quantum.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditor.PackageManager;
  using UnityEngine;

  /// <summary>
  /// Utility methods for working with Unity's <see cref="AssetDatabase"/>
  /// </summary>
  public static partial class AssetDatabaseUtils {
    
    /// <summary>
    /// Sets the asset dirty and, if is a sub-asset, also sets the main asset dirty.
    /// </summary>
    /// <param name="obj"></param>
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
    
    /// <summary>
    /// Returns the asset path for the given instance ID or throws an exception if the asset is not found.
    /// </summary>
    public static string GetAssetPathOrThrow(int instanceID) {
      var result = AssetDatabase.GetAssetPath(instanceID);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset with InstanceID {instanceID} not found");
      }
      return result;
    }
    
    /// <summary>
    /// Returns the asset path for the given object or throws an exception if <paramref name="obj"/> is
    /// not an asset.
    /// </summary>
    public static string GetAssetPathOrThrow(UnityEngine.Object obj) {
      var result = AssetDatabase.GetAssetPath(obj);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset {obj} not found");
      }
      return result;
    }
    
    /// <summary>
    /// Returns the asset path for the given asset GUID or throws an exception if the asset is not found.
    /// </summary>
    public static string GetAssetPathOrThrow(string assetGuid) {
      var result = AssetDatabase.GUIDToAssetPath(assetGuid);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset with Guid {assetGuid} not found");
      }

      return result;
    }

    /// <summary>
    /// Returns the asset GUID for the given asset path or throws an exception if the asset is not found.
    /// </summary>
    public static string GetAssetGuidOrThrow(string assetPath) {
      var result = AssetDatabase.AssetPathToGUID(assetPath);
      if (string.IsNullOrEmpty(result)) {
        throw new ArgumentException($"Asset with path {assetPath} not found");
      }

      return result;
    }
    
    /// <summary>
    /// Returns the asset GUID for the given instance ID or throws an exception if the asset is not found.
    /// </summary>
    public static string GetAssetGuidOrThrow(int instanceId) {
      var assetPath = GetAssetPathOrThrow(instanceId);
      return GetAssetGuidOrThrow(assetPath);
    }
    
    /// <summary>
    /// Returns the asset GUID for the given object reference or throws an exception if the asset is not found.
    /// </summary>
    public static string GetAssetGuidOrThrow(UnityEngine.Object obj) {
      var assetPath = GetAssetPathOrThrow(obj);
      return GetAssetGuidOrThrow(assetPath);
    }

    /// <summary>
    /// Gets the GUID and local file identifier for the given object reference or throws an exception if the asset is not found.
    /// </summary>
    public static (string, long) GetGUIDAndLocalFileIdentifierOrThrow<T>(LazyLoadReference<T> reference) where T : UnityEngine.Object {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out var guid, out long localId)) {
        throw new ArgumentException($"Asset with instanceId {reference} not found");
      }

      return (guid, localId);
    }

    /// <summary>
    /// Gets the GUID and local file identifier for the given object reference or throws an exception if the asset is not found.
    /// </summary>
    public static (string, long) GetGUIDAndLocalFileIdentifierOrThrow(UnityEngine.Object obj) {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long localId)) {
        throw new ArgumentException(nameof(obj));
      }

      return (guid, localId);
    }
    
    /// <summary>
    /// Gets the GUID and local file identifier for the instance ID or throws an exception if the asset is not found.
    /// </summary>
    public static (string, long) GetGUIDAndLocalFileIdentifierOrThrow(int instanceId) {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceId, out var guid, out long localId)) {
        throw new ArgumentException($"Asset with instanceId {instanceId} not found");
      }

      return (guid, localId);
    }
    
    /// <summary>
    /// Moves the asset at <paramref name="source"/> to <paramref name="destination"/> or throws an exception if the move fails.
    /// </summary>
    public static void MoveAssetOrThrow(string source, string destination) {
      var error = AssetDatabase.MoveAsset(source, destination);
      if (!string.IsNullOrEmpty(error)) {
        throw new ArgumentException($"Failed to move {source} to {destination}: {error}");
      }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the asset at <paramref name="assetPath"/> has the given <paramref name="label"/>.
    /// </summary>
    public static bool HasLabel(string assetPath, string label) {
      var guidStr = AssetDatabase.AssetPathToGUID(assetPath);
      if (!GUID.TryParse(guidStr, out var guid)) {
        return false;
      }

      var labels = AssetDatabase.GetLabels(guid);
      var index  = Array.IndexOf(labels, label);
      return index >= 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the asset <paramref name="obj"/> has the given <paramref name="label"/>.
    /// </summary>
    public static bool HasLabel(UnityEngine.Object obj, string label) {
      var labels = AssetDatabase.GetLabels(obj);
      var index  = Array.IndexOf(labels, label);
      return index >= 0;
    }
    
    /// <summary>
    /// Returns <see langword="true"/> if the asset <paramref name="guid"/> has the given <paramref name="label"/>.
    /// </summary>
    public static bool HasLabel(GUID guid, string label) {
      var labels = AssetDatabase.GetLabels(guid);
      var index  = Array.IndexOf(labels, label);
      return index >= 0;
    }
    
    /// <summary>
    /// Returns <see langword="true"/> if the asset at <paramref name="assetPath"/> has any of the given <paramref name="labels"/>.
    /// </summary>
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
    
    /// <summary>
    /// Returns <see langword="true"/> if the <paramref name="asset"/> has any of the given <paramref name="labels"/>.
    /// </summary>
    public static bool HasAnyLabel(UnityEngine.Object asset, params string[] labels) {
      var assetLabels = AssetDatabase.GetLabels(asset);
      foreach (var label in labels) {
        if (Array.IndexOf(assetLabels, label) >= 0) {
          return true;
        }
      }

      return false;
    }

    
    /// <summary>
    /// Sets or unsets <paramref name="label"/> label for the asset at <paramref name="assetPath"/>, depending
    /// on the value of <paramref name="present"/>.
    /// </summary>
    /// <returns><see langword="true"/> if there was a change to the labels.</returns>
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

    /// <summary>
    /// Sets or unsets the <paramref name="label"/> label for the asset <paramref name="obj"/>, depending
    /// on the value of <paramref name="present"/>.
    /// </summary>
    /// <returns><see langword="true"/> if there was a change to the labels.</returns>
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
    
    /// <summary>
    /// Sets all the labels for the asset at <paramref name="assetPath"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the asset was found</returns>
    public static bool SetLabels(string assetPath, string[] labels) {
      var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
      if (obj == null) {
        return false;
      }
      
      AssetDatabase.SetLabels(obj, labels);
      return true;
    }
    
    /// <summary>
    /// Checks if a scripting define <paramref name="value"/> is defined for <paramref name="target"/>.
    /// </summary>
    public static bool HasScriptingDefineSymbol(NamedBuildTarget target, string value) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';');
      return System.Array.IndexOf(defines, value) >= 0;
    }
    
    /// <summary>
    /// Checks if a scripting define <paramref name="value"/> is defined for <paramref name="group"/>.
    /// </summary>
    public static bool HasScriptingDefineSymbol(BuildTargetGroup group, string value) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group)).Split(';');
      return System.Array.IndexOf(defines, value) >= 0;
    }
    
    /// <inheritdoc cref="SetScriptableObjectType"/>
    public static T SetScriptableObjectType<T>(ScriptableObject obj) where T : ScriptableObject {
      return (T)SetScriptableObjectType(obj, typeof(T));
    }
    
    /// <summary>
    /// Changes the type of scriptable object.
    /// </summary>
    /// <returns>The new instance with requested type</returns>
    public static ScriptableObject SetScriptableObjectType(ScriptableObject obj, Type type) {
      const string ScriptPropertyName = "m_Script";

      if (!obj) {
        throw new ArgumentNullException(nameof(obj));
      }
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      if (!type.IsSubclassOf(typeof(ScriptableObject))) {
        throw new ArgumentException($"Type {type} is not a subclass of {nameof(ScriptableObject)}");
      }
      
      if (obj.GetType() == type) {
        return obj;
      }

      var tmp = ScriptableObject.CreateInstance(type);
      try {
        using (var dst = new SerializedObject(obj)) {
          using (var src = new SerializedObject(tmp)) {
            var scriptDst = dst.FindPropertyOrThrow(ScriptPropertyName);
            var scriptSrc = src.FindPropertyOrThrow(ScriptPropertyName);
            Debug.Assert(scriptDst.objectReferenceValue != scriptSrc.objectReferenceValue);
            dst.CopyFromSerializedProperty(scriptSrc);
            dst.ApplyModifiedPropertiesWithoutUndo();
            return (ScriptableObject)dst.targetObject;
          }
        }
      } finally {
        UnityEngine.Object.DestroyImmediate(tmp);
      }
    }
    
    private static bool IsEnumValueObsolete<T>(string valueName) where T : System.Enum {
      var fi         = typeof(T).GetField(valueName);
      var attributes = fi.GetCustomAttributes(typeof(System.ObsoleteAttribute), false);
      return attributes?.Length > 0;
    }
    
    internal static IEnumerable<BuildTargetGroup> ValidBuildTargetGroups {
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
    
    /// <summary>
    /// Checks if any and all <see cref="BuildTargetGroup"/> have the given scripting define symbol.
    /// </summary>
    /// <returns><see langword="true"/> if all groups have the symbol, <see langword="false"/> if none have it, <see langword="null"/> if some have it and some don't</returns>
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

    /// <summary>
    /// Adds or removes <paramref name="define"/> scripting define symbol from <paramref name="group"/>, depending
    /// on the value of <paramref name="enable"/>
    /// </summary>
    public static void UpdateScriptingDefineSymbol(BuildTargetGroup group, string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(new[] { group },
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }

    /// <summary>
    /// Adds or removes <paramref name="define"/> from all <see cref="BuildTargetGroup"/>s, depending on the value of <paramref name="enable"/>
    /// </summary>
    public static void UpdateScriptingDefineSymbol(string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }

    internal static void UpdateScriptingDefineSymbol(BuildTargetGroup group, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      UpdateScriptingDefineSymbolInternal(new[] { group },
        definesToAdd,
        definesToRemove);
    }

    internal static void UpdateScriptingDefineSymbol(IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        definesToAdd,
        definesToRemove);
    }

    private static void UpdateScriptingDefineSymbolInternal(IEnumerable<BuildTargetGroup> groups, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      EditorApplication.LockReloadAssemblies();
      try {
        foreach (var group in groups) {
          var originalDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group));
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
          PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), newDefines);
        }
      } finally {
        EditorApplication.UnlockReloadAssemblies();
      }
    }
    
    /// <summary>
    /// Iterates over all assets in the project that match the given search criteria, without
    /// actually loading them.
    /// </summary>
    /// <param name="root">The optional root folder</param>
    /// <param name="label">The optional label</param>
    public static AssetEnumerable IterateAssets<T>(string root = null, string label = null) where T : UnityEngine.Object {
      return IterateAssets(root, label, typeof(T));
    }
    
    /// <summary>
    /// Iterates over all assets in the project that match the given search criteria, without
    /// actually loading them.
    /// </summary>
    /// <param name="root">The optional root folder</param>
    /// <param name="label">The optional label</param>
    /// <param name="type">The optional type</param>
    public static AssetEnumerable IterateAssets(string root = null, string label = null, Type type = null) {
      return new AssetEnumerable(root, label, type);
    }
    
    static Lazy<string[]> s_rootFolders = new Lazy<string[]>(() => new[] { "Assets" }.Concat(UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
      .Where(x => !IsPackageHidden(x))
      .Select(x => x.assetPath))
      .ToArray());
    
    private static bool IsPackageHidden(UnityEditor.PackageManager.PackageInfo info) => info.type == "module" || info.type == "feature" && info.source != PackageSource.Embedded;
    
    /// <summary>
    /// Enumerates assets in the project that match the given search criteria using <see cref="HierarchyProperty"/> API.
    /// Obtained with <see cref="AssetDatabaseUtils.IterateAssets"/>.
    /// </summary>
    public struct AssetEnumerator : IEnumerator<HierarchyProperty> {

      private HierarchyProperty _hierarchyProperty;
      private int               _rootFolderIndex;

      private readonly string[] _rootFolders;

      /// <summary>
      /// Creates a new instance.
      /// </summary>
      public AssetEnumerator(string root, string label, Type type) {
        var searchFilter = MakeSearchFilter(label, type);
        _rootFolderIndex = 0;
        if (string.IsNullOrEmpty(root)) {
          // search everywhere
          _rootFolders = s_rootFolders.Value;
          _hierarchyProperty = new HierarchyProperty(_rootFolders[0]);
        } else {
          _rootFolders       = null;
          _hierarchyProperty = new HierarchyProperty(root);
        }

        _hierarchyProperty.SetSearchFilter(searchFilter, (int)SearchableEditorWindow.SearchMode.All);
      }

      /// <summary>
      /// Updates internal <see cref="HierarchyProperty"/>.
      /// </summary>
      /// <returns></returns>
      public bool MoveNext() {
        if (_hierarchyProperty.Next(null)) {
          return true;
        }

        if (_rootFolders == null || _rootFolderIndex + 1 >= _rootFolders.Length) {
          return false;
        }

        var newHierarchyProperty = new HierarchyProperty(_rootFolders[++_rootFolderIndex]);
        UnityInternal.HierarchyProperty.CopySearchFilterFrom(newHierarchyProperty, _hierarchyProperty);
        _hierarchyProperty = newHierarchyProperty;

        // try again
        return MoveNext();
      }

      /// <summary>
      /// Throws <see cref="System.NotImplementedException"/>.
      /// </summary>
      /// <exception cref="NotImplementedException"></exception>
      public void Reset() {
        throw new System.NotImplementedException();
      }

      /// <summary>
      /// Returns the internernal <see cref="HierarchyProperty"/>. Most of the time
      /// this will be the same instance as returned the last time, so do not cache
      /// the result - check its properties intestead.
      /// </summary>
      public HierarchyProperty Current => _hierarchyProperty;

      object IEnumerator.Current => Current;

      /// <inheritdoc/>
      public void Dispose() {
      }
      
      private static string MakeSearchFilter(string label, Type type) {
        string searchFilter;
        if (type == typeof(GameObject)) {
          searchFilter = "t:prefab";
        } else if (type != null) {
          searchFilter = "t:" + type.FullName;
        } else {
          searchFilter = "";
        }

        if (!string.IsNullOrEmpty(label)) {
          if (searchFilter.Length > 0) {
            searchFilter += " ";
          }

          searchFilter += "l:" + label;
        }

        return searchFilter;
      }
    }

    /// <summary>
    /// Enumerable of assets in the project that match the given search criteria.
    /// </summary>
    /// <seealso cref="AssetEnumerator"/>
    public struct AssetEnumerable : IEnumerable<HierarchyProperty> {

      private readonly string _root;
      private readonly string _label;
      private readonly Type   _type;

      /// <summary>
      /// Not intended to be called directly. Use <see cref="AssetDatabaseUtils.IterateAssets"/> instead.
      /// </summary>
      public AssetEnumerable(string root, string label, Type type) {
        _type  = type;
        _root  = root;
        _label = label;
      }

      /// <summary>
      /// Not intended to be called directly. Use <see cref="AssetDatabaseUtils.IterateAssets"/> instead.
      /// </summary>
      public AssetEnumerator GetEnumerator() => new AssetEnumerator(_root, _label, _type);

      IEnumerator<HierarchyProperty> IEnumerable<HierarchyProperty>.GetEnumerator() => GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Sends out <see cref="QuantumMppmRegisterCustomDependencyCommand"/> command to virtual peers
    /// before calling <see cref="AssetDatabase.RegisterCustomDependency"/>.
    /// </summary>
    public static void RegisterCustomDependencyWithMppmWorkaround(string customDependency, Hash128 hash) {
      QuantumMppm.MainEditor?.Send(new QuantumMppmRegisterCustomDependencyCommand() { 
        DependencyName = customDependency, 
        Hash = hash.ToString(),
      });
      AssetDatabase.RegisterCustomDependency(customDependency, hash);
    }
  }
}

#endregion


#region EditorButtonDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  struct EditorButtonDrawer {

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
            checkResult = DoIfAttributeDrawer.CheckDraw(doIf, editor.serializedObject);
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

        using (warningMessage == null ? null : (IDisposable)new QuantumEditorGUI.WarningScope(warningMessage)) {
          var rect = QuantumEditorGUI.LayoutHelpPrefix(editor, entry.Method);
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

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  struct EnumDrawer {
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

namespace Quantum.Editor {
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

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using Object = UnityEngine.Object;

  internal class LazyAsset<T> {
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

  internal class LazyGUIStyle {
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

    public Font font => Value.font;
    public FontStyle fontStyle => Value.fontStyle;
    public bool richText => Value.richText;
    public RectOffset margin => Value.margin;
    public float fixedWidth => Value.fixedWidth;
    public float fixedHeight => Value.fixedHeight;
    public RectOffset padding => Value.padding;
    public float CalcHeight(GUIContent content, float width) => Value.CalcHeight(content, width);
    public GUIStyleState normal => Value.normal;
    public GUIStyleState onNormal => Value.onNormal;
  }
  
  internal class LazyGUIContent {
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
  
  internal static class LazyAsset {
    public static LazyAsset<T> Create<T>(Func<T> factory) {
      return new LazyAsset<T>(factory);
    }
  }
}

#endregion


#region LogSettingsDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEngine;


  struct LogSettingsDrawer {
    private static readonly Dictionary<string, LogLevel> _logLevels = new Dictionary<string, LogLevel>(StringComparer.Ordinal) {
      { "QUANTUM_LOGLEVEL_DEBUG", LogLevel.Debug },
      { "QUANTUM_LOGLEVEL_INFO", LogLevel.Info },
      { "QUANTUM_LOGLEVEL_WARN", LogLevel.Warn },
      { "QUANTUM_LOGLEVEL_ERROR", LogLevel.Error },
      { "QUANTUM_LOGLEVEL_NONE", LogLevel.None },
    };

    private static readonly Dictionary<string, TraceChannels> _enablingDefines = Enum.GetValues(typeof(TraceChannels))
      .Cast<TraceChannels>()
      .ToDictionary(x => $"QUANTUM_TRACE_{x.ToString().ToUpperInvariant()}", x => x);

    private Dictionary<NamedBuildTarget, string[]> _defines;
    private Lazy<GUIContent> _logLevelHelpContent;
    private Lazy<GUIContent> _traceChannelsHelpContent;

    void EnsureInitialized() {
      if (_defines == null) {
        UpdateDefines();
      }

      if (_logLevelHelpContent == null) {
        _logLevelHelpContent = new Lazy<GUIContent>(() => {
          var result = new GUIContent(QuantumCodeDoc.FindEntry(typeof(LogLevel)) ?? new GUIContent());
          result.text = ("This setting is applied with QUANTUM_LOGLEVEL_* defines.\n" + result.text).Trim();
          return result;
        });
      }

      if (_traceChannelsHelpContent == null) {
        _traceChannelsHelpContent = new Lazy<GUIContent>(() => {
          var result = new GUIContent(QuantumCodeDoc.FindEntry(typeof(TraceChannels)) ?? new GUIContent());
          result.text = ("This setting is applied with QUANTUM_TRACE_* defines.\n" + result.text).Trim();
          return result;
        });
      }
    }

    public void DrawLayoutLevelEnumOnly(ScriptableObject editor) {
      var activeLogLevel = GetActiveBuildTargetDefinedLogLevel();
      var invalidActiveLogLevel = activeLogLevel == null;
      EditorGUI.BeginChangeCheck();

      using (new QuantumEditorGUI.ShowMixedValueScope(invalidActiveLogLevel)) {
        activeLogLevel = (LogLevel)EditorGUILayout.EnumPopup(activeLogLevel ?? LogLevel.Info);
        Debug.Assert(activeLogLevel != null);
      }

      if (EditorGUI.EndChangeCheck()) {
        SetLogLevel(activeLogLevel.Value);
      }
    }
    
    public void DrawLogLevelEnum(Rect rect) {
      EnsureInitialized();
      var activeLogLevel = GetActiveBuildTargetDefinedLogLevel();
      var invalidActiveLogLevel = activeLogLevel == null;
      EditorGUI.BeginChangeCheck();

      using (new QuantumEditorGUI.ShowMixedValueScope(invalidActiveLogLevel)) {
        activeLogLevel = (LogLevel)EditorGUI.EnumPopup(rect, activeLogLevel ?? LogLevel.Info);
        Debug.Assert(activeLogLevel != null);
      }

      if (EditorGUI.EndChangeCheck()) {
        SetLogLevel(activeLogLevel.Value);
      }
    }

    
    public void DrawLayout(ScriptableObject editor, bool inlineHelp = true) {
      EnsureInitialized();
      
      {
        var activeLogLevel = GetActiveBuildTargetDefinedLogLevel();
        var invalidActiveLogLevel = activeLogLevel == null;
        var rect = inlineHelp ? QuantumEditorGUI.LayoutHelpPrefix(editor, "Log Level", _logLevelHelpContent.Value) : EditorGUILayout.GetControlRect();
        EditorGUI.BeginChangeCheck();

        using (new QuantumEditorGUI.ShowMixedValueScope(invalidActiveLogLevel)) {
          activeLogLevel = (LogLevel)EditorGUI.EnumPopup(rect, "Log Level", activeLogLevel ?? LogLevel.Info);
          Debug.Assert(activeLogLevel != null);
        }

        if (invalidActiveLogLevel) {
          using (new QuantumEditorGUI.WarningScope("Either QUANTUM_LOGLEVEL_* define is missing for the current build " +
                                                        "target or there are more than one defined. Changing the value will ensure there is " +
                                                        "exactly one define <b>for each build target</b>.")) {
          }
        } else if (GetAllBuildTargetsDefinedLogLevel() == null) {
          using (new QuantumEditorGUI.WarningScope("Not all build targets have the same log level defined. Changing the value will ensure " +
                                                        "there is exactly one define <b>for each build target</b>.")) {
          }
        }

        if (EditorGUI.EndChangeCheck()) {
          SetLogLevel(activeLogLevel.Value);
        }
      }

      {
        var activeTraceChannels = GetActiveBuildTargetDefinedTraceChannels();
        var rect = inlineHelp ? QuantumEditorGUI.LayoutHelpPrefix(editor, "Trace Channels", _traceChannelsHelpContent.Value) : EditorGUILayout.GetControlRect();
        
        EditorGUI.BeginChangeCheck();
        
        activeTraceChannels = (TraceChannels)EditorGUI.EnumFlagsField(rect, "Trace Channels", activeTraceChannels);

        if (GetAllBuildTargetsDefinedTraceChannels() == null) {
          using (new QuantumEditorGUI.WarningScope("Not all build targets have the same trace channels defined. Changing the value will ensure " +
                                                        "the values are the same <b>for each build target</b>.")) {
          }
        }
        
        if (EditorGUI.EndChangeCheck()) {
          SetTraceChannels(activeTraceChannels);
        }
      }

    }
    
    private void SetLogLevel(LogLevel activeLogLevel) {
      foreach (var kv in _defines) {
        var target = kv.Key;
        var defines = kv.Value;

        string newDefine = null;
        foreach (var (define, level) in _logLevels) {
          if (level == activeLogLevel) {
            newDefine = define;
            continue;
          }
          ArrayUtility.Remove(ref defines, define);
        }
        ArrayUtility.Remove(ref defines, "QUANTUM_LOGLEVEL_TRACE");
        
        Debug.Assert(newDefine != null);
        if (!ArrayUtility.Contains(defines, newDefine)) {
          ArrayUtility.Add(ref defines, newDefine);
        }
        
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
      }

      UpdateDefines();
    }
    
    private void SetTraceChannels(TraceChannels activeTraceChannels) {
      List<string> definesToAdd = new List<string>();
      List<string> definesToRemove = new List<string>();
      
      foreach (var kv in _enablingDefines) {
        var channel = kv.Value;
        if (activeTraceChannels.HasFlag(channel)) {
          definesToAdd.Add(kv.Key);
        } else {
          definesToRemove.Add(kv.Key);
        }
      }
      
      foreach (var kv in _defines) {
        var target = kv.Key;
        var defines = kv.Value;

        foreach (var d in definesToRemove) {
          ArrayUtility.Remove(ref defines, d);
        }

        foreach (var d in definesToAdd) {
          if (!ArrayUtility.Contains(defines, d)) {
            ArrayUtility.Add(ref defines, d);
          }
        }
        
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
      }
      
      
      UpdateDefines();
    }

    public LogLevel? GetActiveBuildTargetDefinedLogLevel() {
      EnsureInitialized();
      var activeBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
      return GetDefinedLogLevel(activeBuildTarget);
    }

    private TraceChannels GetActiveBuildTargetDefinedTraceChannels() {
      var activeBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
      return GetDefinedTraceChannels(activeBuildTarget);
    }
      

    private LogLevel? GetAllBuildTargetsDefinedLogLevel() {
      LogLevel? result = null;

      foreach (var buildTarget in _defines.Keys) {
        var targetLogLevel = GetDefinedLogLevel(buildTarget);

        if (targetLogLevel == null) {
          return null;
        }

        if (result == null) {
          result = targetLogLevel;
        } else if (result != targetLogLevel) {
          return null;
        }
      }

      return result;
    }
    
    private TraceChannels? GetAllBuildTargetsDefinedTraceChannels() {
      TraceChannels? result = null;

      foreach (var buildTarget in _defines.Keys) {
        var targetLogLevel = GetDefinedTraceChannels(buildTarget);
        if (result == null) {
          result = targetLogLevel;
        } else if (result != targetLogLevel) {
          return null;
        }
      }

      return result;
    }
    
    private LogLevel? GetDefinedLogLevel(NamedBuildTarget group) {
      LogLevel? result = null;
      var defines = _defines[group];

      foreach (var define in defines) {
        if (_logLevels.TryGetValue(define, out var logLevel)) {
          if (result != null) {
            if (result != logLevel) {
              return null;
            }
          } else {
            result = logLevel;
          }
        }
      }

      return result;
    }

    private TraceChannels GetDefinedTraceChannels(NamedBuildTarget group) {
      var channels = default(TraceChannels);
      
      var defines = _defines[group];
      foreach (var define in defines) {
        if (_enablingDefines.TryGetValue(define, out var channel)) {
          channels |= channel;
        }
      }

      return channels;
    }

    private void UpdateDefines() {
      _defines = AssetDatabaseUtils.ValidBuildTargetGroups
        .Select(NamedBuildTarget.FromBuildTargetGroup)
        .ToDictionary(x => x, x => PlayerSettings.GetScriptingDefineSymbols(x).Split(';'));
    }
  }
  
  
}

#endregion


#region PathUtils.cs

namespace Quantum.Editor {
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


#region QuantumCodeDoc.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Text.RegularExpressions;
  using System.Xml;
  using UnityEditor;
  using UnityEngine;

  static class QuantumCodeDoc {
    public const string Label            = "QuantumCodeDoc";
    public const string Extension        = "xml";
    public const string ExtensionWithDot = "." + Extension;

    private static readonly Dictionary<string, CodeDoc> s_parsedCodeDocs = new();
    private static readonly Dictionary<(string assemblyName, string memberKey), (GUIContent withoutType, GUIContent withType)> s_guiContentCache = new();
    
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
        QuantumEditorLog.Assert(member.DeclaringType != null);
        assembly = member.DeclaringType.Assembly;
      }

      var assemblyName = assembly.GetName().Name;
      QuantumEditorLog.Assert(assemblyName != null);
      
      if (s_guiContentCache.TryGetValue((assemblyName, key), out var content)) {
        return addTypeInfo ? content.withType : content.withoutType;
      }
      
      if (TryGetEntry(key, out var entry, assemblyName: assemblyName)) {
        // at this point we've got docs or not, need to save it now - in case returnType code doc search tries
        // to load the same member info, which might happen; same for inheritdoc
        content.withoutType = new GUIContent(entry.Summary ?? string.Empty, entry.Tooltip ?? string.Empty);
        content.withType    = content.withoutType; 
      }
      
      s_guiContentCache.Add((assemblyName, key), content);
      
      if (!string.IsNullOrEmpty(entry.InheritDocKey)) {
        // need to resolve the inheritdoc
        QuantumEditorLog.Assert(entry.InheritDocKey != key);
        if (TryResolveInheritDoc(entry.InheritDocKey, out var rootEntry)) {
          content.withoutType = new GUIContent(rootEntry.Summary, rootEntry.Tooltip);
          content.withType    = content.withoutType;
          s_guiContentCache[(assemblyName, key)] = content;
        }
      }
      
      // now add type info
      Type returnType = (member as FieldInfo)?.FieldType ?? (member as PropertyInfo)?.PropertyType;
      if (returnType != null) {
        var    typeEntry   = FindEntry(returnType);
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
          content.withType = AppendContent(content.withType, typeSummary);
          s_guiContentCache[(assemblyName, key)] = content;
        }
      }
            
      return addTypeInfo ? content.withType : content.withoutType;
      
      GUIContent AppendContent(GUIContent existing, string append) {
        return new GUIContent((existing?.text + append).Trim('\n'), existing?.tooltip ?? string.Empty);
      }
    }

    private static bool TryResolveInheritDoc(string key, out MemberInfoEntry entry) {
      // difficult to tell which assembly this comes from; just check in them all
      // also make sure we're not in a loop
      var visited   = new HashSet<string>();
      var currentKey = key;

      for (;;) {
        if (!visited.Add(currentKey)) {
          QuantumEditorLog.Error($"Inheritdoc loop detected for {key}");
          break;
        }
        
        if (!TryGetEntry(currentKey, out var currentEntry)) {
          break;
        }

        if (string.IsNullOrEmpty(currentEntry.InheritDocKey)) {
          entry = currentEntry;
          return true;
        }
        
        currentKey = currentEntry.InheritDocKey;
      }
      
      entry = default;
      return false;
    }

    private static bool TryGetEntry(string key, out MemberInfoEntry entry, string assemblyName = null) {
      foreach (var path in AssetDatabase.FindAssets($"l:{Label} t:TextAsset")
                 .Select(x => AssetDatabase.GUIDToAssetPath(x))) {

        if (assemblyName != null) {
          if (!Path.GetFileNameWithoutExtension(path).Contains(assemblyName, StringComparison.OrdinalIgnoreCase)) {
            continue;
          }
        }

        // has this path been parsed already?
        if (!s_parsedCodeDocs.TryGetValue(path, out var parsedCodeDoc)) {
          s_parsedCodeDocs.Add(path, null);
          
          QuantumEditorLog.Trace($"Trying to parse {path} for {key}");
          if (TryParseCodeDoc(path, out parsedCodeDoc)) {
            s_parsedCodeDocs[path] = parsedCodeDoc;
          } else {
            QuantumEditorLog.Trace($"Failed to parse {path}");
          }
        }

        if (parsedCodeDoc != null) {
          if (assemblyName != null && parsedCodeDoc.AssemblyName != assemblyName) {
            // wrong assembly!
            continue;
          }
          if (parsedCodeDoc.Entries.TryGetValue(key, out entry)) {
            return true;
          }
        }
      }

      entry = default;
      return false;
    }

    private static string SanitizeTypeName(Type type) {
      var t = type;
      if (type.IsGenericType) {
        t = type.GetGenericTypeDefinition();
      }
      QuantumEditorLog.Assert(t != null);
      return t.FullName.Replace('+', '.');
    }
    
    public static void InvalidateCache() {
      s_parsedCodeDocs.Clear();
      s_guiContentCache.Clear();
    }

    private static bool TryParseCodeDoc(string path, out CodeDoc result) {
      var xmlDoc = new XmlDocument();

      try {
        xmlDoc.Load(path);
      } catch (Exception e) {
        QuantumEditorLog.Error($"Failed to load {path}: {e}");
        result = null;
        return false;
      }

      QuantumEditorLog.Assert(xmlDoc.DocumentElement != null);
      var assemblyName = xmlDoc.DocumentElement.SelectSingleNode("assembly")
       ?.SelectSingleNode("name")
       ?.FirstChild
       ?.Value;

      if (assemblyName == null) {
        result = null;
        return false;
      }

      var members = xmlDoc.DocumentElement.SelectSingleNode("members")
        ?.SelectNodes("member");

      if (members == null) {
        result = null;
        return false;
      }
      
      var entries = new Dictionary<string, MemberInfoEntry>();
      
      foreach (XmlNode node in members) {
        QuantumEditorLog.Assert(node.Attributes != null);
        var key     = node.Attributes["name"].Value;
        var inherit = node.SelectSingleNode("inheritdoc");
        if (inherit != null) {
          
          // hold on to the ref, will need to resolve it later
          QuantumEditorLog.Assert(inherit.Attributes != null);
          var cref = inherit.Attributes["cref"]?.Value;
          if (!string.IsNullOrEmpty(cref)) {
            entries.Add(key, new MemberInfoEntry() {
              InheritDocKey = cref
            });
            continue;
          }
        }

        var summary = node.SelectSingleNode("summary")?.InnerXml.Trim();
        if (summary == null) {
          continue;
        }

        // remove generic indicator
        summary = summary.Replace("`1", "");

        // fork tooltip and help summaries
        var help    = Reformat(summary, false);
        var tooltip = Reformat(summary, true);

        entries.Add(key, new MemberInfoEntry() {
          Summary = help,
          Tooltip = tooltip
        });
      }
     
      result = new CodeDoc() {
        AssemblyName = assemblyName,
        Entries      = entries,
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
      
      // handle lists
      for (;;) {
        var listMatch = Regexes.BulletPointList.Match(summary);
        if (!listMatch.Success) {
          break;
        }
        var innerText = listMatch.Groups[1].Value;
        innerText = Regexes.ListItemBracket.Replace(innerText, $"\n\u2022 $1");
        summary = summary.Substring(0, listMatch.Index) + innerText + summary.Substring(listMatch.Index + listMatch.Length);
      }

      
      // unescape <>
      summary = summary.Replace("&lt;", "<");
      summary = summary.Replace("&gt;", ">");
      summary = summary.Replace("&amp;", "&");

      summary = summary.Trim();

      return summary;
    }
    
    private struct MemberInfoEntry {
      public string Summary;
      public string Tooltip;
      public string InheritDocKey;
    }

    private class CodeDoc {
      public string                              AssemblyName;
      public Dictionary<string, MemberInfoEntry> Entries;
    }

    private class Postprocessor : AssetPostprocessor {
      private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
        foreach (var path in importedAssets) {
          if (!path.StartsWith("Assets/") || !path.EndsWith(ExtensionWithDot)) {
            continue;
          } 
          
          if (AssetDatabaseUtils.HasLabel(path, Label)) {
            QuantumEditorLog.Trace($"Code doc {path} was imported, refreshing");
            InvalidateCache();
            continue;
          }

          // is there a dll with the same name?
          if (!File.Exists(path.Substring(0, path.Length - ExtensionWithDot.Length) + ".dll")) {
            QuantumEditorLog.Trace($"No DLL next to {path}, not going to add label {Label}.");
            continue;
          }

          if (!path.StartsWith("Assets/Photon/")) {
            QuantumEditorLog.Trace($"DLL is out of supported folder, not going to add label: {path}");
            continue;
          }

          QuantumEditorLog.Trace($"Detected a dll next to {path}, applying label and refreshing.");
          AssetDatabaseUtils.SetLabel(path, Label, true);
          InvalidateCache();
        }
      }
    }
    
    private static class Regexes {
      public static readonly Regex SeeWithCref          = new(@"<see\w* (?:cref|langword)=""(?:\w: ?)?([\w\.\d]*?)(?:\(.*?\))?"" ?\/>", RegexOptions.None);
      public static readonly Regex See                  = new(@"<see\w* .*>([\w\.\d]*)<\/see\w*>", RegexOptions.None);
      public static readonly Regex WhitespaceString     = new(@"\s+");
      public static readonly Regex XmlCodeBracket       = new(@"<code>([\s\S]*?)</code>");
      public static readonly Regex XmlEmphasizeBrackets = new(@"<\w>([\s\S]*?)</\w>");
      public static readonly Regex BulletPointList      = new(@"<list type=""bullet"">([\s\S]*?)</list>");
      public static readonly Regex ListItemBracket      = new(@"<item>\s*<description>([\s\S]*?)</description>\s*</item>");
    }
  }
}

#endregion


#region QuantumEditor.cs

namespace Quantum.Editor {
  using UnityEditor;

  /// <summary>
  /// Base class for all Photon Common editors. Supports <see cref="EditorButtonAttribute"/> and <see cref="ScriptHelpAttribute"/>.
  /// </summary>
  public abstract class QuantumEditor :
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    Sirenix.OdinInspector.Editor.OdinEditor
#else
    UnityEditor.Editor
#endif
  {
    private EditorButtonDrawer _buttonDrawer;
    
    /// <summary>
    /// Prepares the editor by initializing the script header drawer.
    /// </summary>
    protected void PrepareOnInspectorGUI() {
      QuantumEditorGUI.InjectScriptHeaderDrawer(this);
    }

    /// <summary>
    /// Draws the editor buttons.
    /// </summary>
    protected void DrawEditorButtons() {
      _buttonDrawer.Draw(this);
    }
    
    /// <inheritdoc/>
    public override void OnInspectorGUI() {
      PrepareOnInspectorGUI();
      base.OnInspectorGUI();
      DrawEditorButtons();
    }

    /// <summary>
    /// Draws the script property field.
    /// </summary>
    protected void DrawScriptPropertyField() {
      QuantumEditorGUI.ScriptPropertyField(this);
    }

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    /// <summary>
    /// Draws the default inspector.
    /// </summary>
    public new bool DrawDefaultInspector() {
      EditorGUI.BeginChangeCheck();
      base.DrawDefaultInspector();
      return EditorGUI.EndChangeCheck();
    } 
#else
    /// <summary>
    /// Empty implementations, provided for compatibility with OdinEditor class.
    /// </summary>
    protected virtual void OnEnable() {
    }

    /// <summary>
    /// Empty implementations, provided for compatibility with OdinEditor class.
    /// </summary>
    protected virtual void OnDisable() {
    }
#endif
  }
}


#endregion


#region QuantumEditorGUI.InlineHelp.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  static partial class QuantumEditorGUI {
    private const float SCROLL_WIDTH     = 16f;
    private const float LEFT_HELP_INDENT = 8f;
    
    private static (object, string, int) s_expandedHelp;
    
    internal static Rect GetInlineHelpButtonRect(Rect position, bool expectFoldout = true, bool forScriptHeader = false) {
      var style = QuantumEditorSkin.HelpButtonStyle;

      float width = style.fixedWidth <= 0 ? 16.0f : style.fixedWidth;
      float height = style.fixedHeight <= 0 ? 16.0f : style.fixedHeight;

      // this 2 lower than line height, but makes it look better
      const float FirstLineHeight = 16;
      
      int offsetY    = forScriptHeader ? -1 : 1;
      
      var buttonRect = new Rect(position.x - width, position.y + (FirstLineHeight - height) / 2 + + offsetY, width, height);
      using (new IndentLevelScope(EditorGUI.indentLevel + (expectFoldout ? - 1 : 0))) {
        buttonRect.x = EditorGUI.IndentedRect(buttonRect).x;
        // give indented items a little extra padding - no need for them to be so crammed
        if (buttonRect.x > 8) {
          buttonRect.x -= 2;
        }
      }

      return buttonRect;
    }

    
    internal static bool DrawInlineHelpButton(Rect buttonRect, bool state, bool doButton = true, bool doIcon = true) {

      var style = QuantumEditorSkin.HelpButtonStyle;
      
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
      
      var outerStyle = QuantumEditorSkin.InlineBoxFullWidthStyle;
      var innerStyle = QuantumEditorSkin.RichLabelStyle;
      
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
          var boxMargin = QuantumEditorSkin.InlineBoxFullWidthStyle.margin;
          
          var boxRect = new Rect() {
            x      = boxMargin.left,
            y      = propertyRect.yMax - boxSize.y,
            width  = UnityInternal.EditorGUIUtility.contextWidth - boxMargin.horizontal,
            height = boxSize.y,
          };

          using (new BackgroundColorScope(color)) {
            QuantumEditorSkin.InlineBoxFullWidthStyle.Draw(boxRect, false, false, false, false);

            var labelRect = boxRect;
            labelRect = QuantumEditorSkin.InlineBoxFullWidthStyle.padding.Remove(labelRect);
            QuantumEditorSkin.RichLabelStyle.Draw(labelRect, content, false, false, false, false);
            
            if (drawSelector) {
              var selectorMargin = QuantumEditorSkin.InlineSelectorStyle.margin;

              var selectorRect = new Rect() {
                x      = selectorMargin.left,
                y      = propertyRect.y - selectorMargin.top,
                width  = propertyRect.x - selectorMargin.horizontal,
                height = propertyRect.height - boxSize.y - selectorMargin.bottom,
              };

              if (hasFoldout) {
                selectorRect.width -= 20.0f;
              }

              QuantumEditorSkin.InlineSelectorStyle.Draw(selectorRect, false, false, false, false);
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
      
      var style     = QuantumEditorSkin.ScriptHeaderBackgroundStyle;
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

    internal static void DrawScriptHeaderIcon(Rect position) {
      if (Event.current.type != EventType.Repaint) {
        return;
      }

      var style     = QuantumEditorSkin.ScriptHeaderIconStyle;
      var boxMargin = style.margin;
      var boxRect   = boxMargin.Remove(position);

      style.Draw(boxRect, false, false, false, false);
    }

    internal static bool InjectScriptHeaderDrawer(Editor editor)                               => InjectScriptHeaderDrawer(editor, out _);
    internal static bool InjectScriptHeaderDrawer(Editor editor, out ScriptFieldDrawer drawer) => InjectScriptHeaderDrawer(editor.serializedObject, out drawer);
    internal static bool InjectScriptHeaderDrawer(SerializedObject serializedObject)           => InjectScriptHeaderDrawer(serializedObject, out _);
    
    internal static bool InjectScriptHeaderDrawer(SerializedObject serializedObject, out ScriptFieldDrawer drawer) {
      var sp       = serializedObject.FindPropertyOrThrow(ScriptPropertyName);
      var rootType = serializedObject.targetObject.GetType();
      
      var injected = TryInjectDrawer(sp, null, () => null, () => new ScriptFieldDrawer(), out drawer);
      if (drawer.attribute == null) {
        UnityInternal.PropertyDrawer.SetAttribute(drawer, rootType.GetCustomAttributes<ScriptHelpAttribute>(true).SingleOrDefault() ?? new ScriptHelpAttribute());
      }

      return injected;
    }
    
    internal static void SetScriptFieldHidden(Editor editor, bool hidden) {
      var sp = editor.serializedObject.FindPropertyOrThrow(ScriptPropertyName);
      TryInjectDrawer(sp, null, () => null, () => new ScriptFieldDrawer(), out var drawer);
      drawer.ForceHide = hidden;
    }

    internal static Rect LayoutHelpPrefix(Editor editor, SerializedProperty property) {
      var fieldInfo = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out _);
      if (fieldInfo == null) {
        return EditorGUILayout.GetControlRect(true);
      }
      
      var help = QuantumCodeDoc.FindEntry(fieldInfo);
      return LayoutHelpPrefix(editor, property.propertyPath, help);
    }
    
    internal static Rect LayoutHelpPrefix(ScriptableObject editor, MemberInfo memberInfo) {
      var help = QuantumCodeDoc.FindEntry(memberInfo);
      return LayoutHelpPrefix(editor, memberInfo.Name, help);
    }
    
    internal static Rect LayoutHelpPrefix(ScriptableObject editor, string path, GUIContent help) {
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
        DrawInlineBoxUnderProperty(help, r, QuantumEditorSkin.HelpInlineBoxColor, true);
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
      QuantumEditorLog.Assert(drawer != null, "drawer != null");
      UnityInternal.PropertyDrawer.SetAttribute(drawer, attribute);
      UnityInternal.PropertyDrawer.SetFieldInfo(drawer, field);

      AddDrawer(property, drawer);

      return true;
    }
    
    internal static bool IsHelpExpanded(object id, int pathHash) {
      return s_expandedHelp == (id, default, pathHash);
    }

    internal static bool IsHelpExpanded(object id, string path) {
      return s_expandedHelp == (id, path, default);
    }

    internal static void SetHelpExpanded(object id, string path, bool value) {
      if (value) {
        s_expandedHelp = (id, path, default);
      } else {
        s_expandedHelp = default;
      }
    }
    
    internal static void SetHelpExpanded(object id, int pathHash, bool value) {
      if (value) {
        s_expandedHelp = (id, default, pathHash);
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


#region QuantumEditorGUI.Odin.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
  using Sirenix.Utilities.Editor;
  using Sirenix.OdinInspector.Editor;
  using Sirenix.Utilities;
#endif

  static partial class QuantumEditorGUI {
    internal static T IfOdin<T>(T ifOdin, T ifNotOdin) {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
      return ifOdin;
#else
      return ifNotOdin;
#endif
    }

    internal static UnityEngine.Object ForwardObjectField(Rect position, UnityEngine.Object value, Type objectType, bool allowSceneObjects) {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
      return SirenixEditorFields.UnityObjectField(position, value, objectType, allowSceneObjects);
#else
      return EditorGUI.ObjectField(position, value, objectType, allowSceneObjects);
#endif
    }
    
    internal static UnityEngine.Object ForwardObjectField(Rect position, GUIContent label, UnityEngine.Object value, Type objectType, bool allowSceneObjects) {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
      return SirenixEditorFields.UnityObjectField(position, label, value, objectType, allowSceneObjects);
#else
      return EditorGUI.ObjectField(position, label, value, objectType, allowSceneObjects);
#endif
    }

    
    internal static bool ForwardPropertyField(Rect position, SerializedProperty property, GUIContent label, bool includeChildren, bool lastDrawer = true) {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
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
      if (lastDrawer && !includeChildren) {
        return UnityInternal.EditorGUI.DefaultPropertyField(position, property, label);
      }

      return EditorGUI.PropertyField(position, property, label, includeChildren);
    }
  }
}

#endregion


#region QuantumEditorGUI.Scopes.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  static partial class QuantumEditorGUI {
 
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

    public struct DisabledGroupScope : IDisposable {
      public DisabledGroupScope(bool disabled) {
        EditorGUI.BeginDisabledGroup(disabled);
      }

      public void Dispose() {
        EditorGUI.EndDisabledGroup();
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
        EditorGUILayout.BeginVertical(QuantumEditorSkin.OutlineBoxStyle);

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
        
        GUI.backgroundColor = QuantumEditorSkin.WarningInlineBoxColor;
        EditorGUILayout.BeginVertical(QuantumEditorSkin.InlineBoxFullWidthScopeStyle);
        GUI.backgroundColor = backgroundColor;
        
        EditorGUILayout.LabelField(new GUIContent(message, QuantumEditorSkin.WarningIcon), QuantumEditorSkin.RichLabelStyle);
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
        
        GUI.backgroundColor = QuantumEditorSkin.ErrorInlineBoxColor;
        EditorGUILayout.BeginVertical(QuantumEditorSkin.InlineBoxFullWidthScopeStyle);
        GUI.backgroundColor = backgroundColor;
        
        EditorGUILayout.LabelField(new GUIContent(message, QuantumEditorSkin.ErrorIcon), QuantumEditorSkin.RichLabelStyle);
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


#region QuantumEditorGUI.Utils.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  static partial class QuantumEditorGUI {
    /// <summary>
    /// The name of the script property in Unity objects
    /// </summary>
    public const string ScriptPropertyName = "m_Script";

    private const int IconHeight = 14;

    /// <summary>
    /// GUIContent with a single whitespace
    /// </summary>
    public static readonly GUIContent WhitespaceContent = new(" ");

    internal static Color PrefebOverridenColor => new(1f / 255f, 153f / 255f, 235f / 255f, 0.75f);

    /// <summary>
    /// Width of the foldout arrow
    /// </summary>
    public static float FoldoutWidth => 16.0f;

    internal static Rect Decorate(Rect rect, string tooltip, MessageType messageType, bool hasLabel = false, bool drawBorder = true, bool drawButton = true, bool rightAligned = false) {
      if (hasLabel) {
        rect.xMin += EditorGUIUtility.labelWidth;
      }

      var content  = EditorGUIUtility.TrTextContentWithIcon(string.Empty, tooltip, messageType);
      var iconRect = rect;
      iconRect.width =  Mathf.Min(16, rect.width);

      if (rightAligned) {
        iconRect.x = rect.xMax - iconRect.width;
      } else {
        iconRect.xMin -= iconRect.width;
      }

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

    internal static void AppendTooltip(string tooltip, ref GUIContent label) {
      if (!string.IsNullOrEmpty(tooltip)) {
        label = new GUIContent(label);
        if (string.IsNullOrEmpty(label.tooltip)) {
          label.tooltip = tooltip;
        } else {
          label.tooltip += "\n\n" + tooltip;
        }
      }
    }

    internal static void ScriptPropertyField(Editor editor) {
      ScriptPropertyField(editor.serializedObject);
    }
    
    internal static void ScriptPropertyField(SerializedObject obj) {
      var scriptProperty = obj.FindProperty(ScriptPropertyName);
      if (scriptProperty != null) {
        using (new EditorGUI.DisabledScope(true)) {
          EditorGUILayout.PropertyField(scriptProperty);
        }
      }
    }

    internal static void Overlay(Rect position, string label) {
      GUI.Label(position, label, QuantumEditorSkin.OverlayLabelStyle);
    }
    
    internal static void Overlay(Rect position, GUIContent label) {
      GUI.Label(position, label, QuantumEditorSkin.OverlayLabelStyle);
    }
    
    internal static float GetLinesHeight(int count) {
      return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    internal static float GetLinesHeightWithNarrowModeSupport(int count) {
      if (!EditorGUIUtility.wideMode) {
        count++;
      }
      return count * (EditorGUIUtility.singleLineHeight) + (count - 1) * EditorGUIUtility.standardVerticalSpacing;
    }
    
    internal static System.Type GetDrawerTypeIncludingWorkarounds(System.Attribute attribute) {
      var drawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(attribute.GetType(), false);
      if (drawerType == null) {
        return null;
      }

      if (drawerType == typeof(PropertyDrawerForArrayWorkaround)) {
        drawerType = PropertyDrawerForArrayWorkaround.GetDrawerType(attribute.GetType());
      }
      return drawerType;
    }
    
    internal static void DisplayTypePickerMenu(Rect position, Type[] baseTypes, Action<Type> callback, Func<Type, bool> filter, string noneOptionLabel = "[None]", Type selectedType = null, QuantumEditorGUIDisplayTypePickerMenuFlags flags = QuantumEditorGUIDisplayTypePickerMenuFlags.Default) {

      var types = new List<Type>();

      foreach (var baseType in baseTypes) {
        types.AddRange(TypeCache.GetTypesDerivedFrom(baseType).Where(filter));
        if (filter(baseType)) {
          types.Add(baseType);
        }
      }

      if (baseTypes.Length > 1) {
        types = types.Distinct().ToList();
      }

      types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));


      List<GUIContent> menuOptions = new List<GUIContent>();
      var actualTypes = new Dictionary<string, System.Type>();

      menuOptions.Add(new GUIContent(noneOptionLabel));
      actualTypes.Add(noneOptionLabel, null);

      int selectedIndex = -1;

      foreach (var ns in types.GroupBy(x => string.IsNullOrEmpty(x.Namespace) ? "[Global Namespace]" : x.Namespace)) {
        foreach (var t in ns) {
          var typeName = t.FullName;
          if (string.IsNullOrEmpty(typeName)) {
            continue;
          }

          if (!string.IsNullOrEmpty(t.Namespace)) {
            if ((flags & QuantumEditorGUIDisplayTypePickerMenuFlags.ShowFullName) == 0) {
              typeName = typeName.Substring(t.Namespace.Length + 1);
            }
          }
          
          string path;
          if ((flags & QuantumEditorGUIDisplayTypePickerMenuFlags.GroupByNamespace) != 0) {
            path = ns.Key + "/" + typeName;
          } else {
            path = typeName;
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
    
        
    internal static void DisplayTypePickerMenu(Rect position, Type[] baseTypes, Action<Type> callback, string noneOptionLabel = "[None]", Type selectedType = null, bool enableAbstract = false, bool enableGenericTypeDefinitions = false, QuantumEditorGUIDisplayTypePickerMenuFlags flags = QuantumEditorGUIDisplayTypePickerMenuFlags.Default) {
      DisplayTypePickerMenu(position, baseTypes, callback, 
        x => (enableAbstract || !x.IsAbstract) && (enableGenericTypeDefinitions || !x.IsGenericTypeDefinition),
        noneOptionLabel: noneOptionLabel,
        flags: flags,
        selectedType: selectedType);
    }
    
    internal static void DisplayTypePickerMenu(Rect position, Type baseType, Action<Type> callback, string noneOptionLabel = "[None]", Type selectedType = null, bool enableAbstract = false, bool enableGenericTypeDefinitions = false, QuantumEditorGUIDisplayTypePickerMenuFlags flags = QuantumEditorGUIDisplayTypePickerMenuFlags.Default) {
      DisplayTypePickerMenu(position, new [] { baseType }, callback, 
        x => (enableAbstract || !x.IsAbstract) && (enableGenericTypeDefinitions || !x.IsGenericTypeDefinition),
        noneOptionLabel: noneOptionLabel,
        flags: flags,
        selectedType: selectedType);
    }

    internal static float GetPropertyHeight(SerializedProperty property) {
      return EditorGUI.GetPropertyHeight(property, WhitespaceContent, property.isExpanded || property.IsArrayProperty());
    }
  }
  
  /// <summary>
  /// Flags for the <see cref="QuantumEditorGUI.DisplayTypePickerMenu(UnityEngine.Rect,System.Type[],System.Action{System.Type},System.Func{System.Type,bool},string,System.Type,Quantum.Editor.QuantumEditorGUIDisplayTypePickerMenuFlags)"/> method
  /// and its overloads.
  /// </summary>
  [Flags]
  public enum QuantumEditorGUIDisplayTypePickerMenuFlags {
    /// <summary>
    /// No special flags
    /// </summary>
    None             = 0,
    /// <summary>
    /// Group types by their namespace
    /// </summary>
    GroupByNamespace = 1 << 1,
    /// <summary>
    /// Show the full name of the type including the namespace
    /// </summary>
    ShowFullName     = 1 << 0,
    /// <summary>
    /// The default flags
    /// </summary>
    Default          = GroupByNamespace,
  }
}

#endregion


#region QuantumEditorMenuPriority.cs

namespace Quantum.Editor {
  /// <summary>
  /// An enumeration to globally control the Unity menu item priorities set with the <see cref="UnityEditor.MenuItem"/> attribute.
  /// </summary>
  public enum QuantumEditorMenuPriority {
    /// <summary>
    /// Top priority.
    /// </summary>
    TOP           = 1000,
    /// <summary>
    /// Generic section 1.
    /// </summary>
    SECTION_1     = 2000,
    /// <summary>
    /// Demo and sample entries.
    /// </summary>
    Demo          = SECTION_1 + 0,
    /// <summary>
    /// Export entries.
    /// </summary>
    Export        = SECTION_1 + 9,
    /// <summary>
    /// Configuration entries.
    /// </summary>
    GlobalConfigs = SECTION_1 + 18,
    /// <summary>
    /// Select windows.
    /// </summary>
    Profilers     = SECTION_1 + 27,
    /// <summary>
    /// Setup and create entries.
    /// </summary>
    Setup         = SECTION_1 + 36,
    /// <summary>
    /// Select windows.
    /// </summary>
    Window        = SECTION_1 + 45,
    SECTION_2     = 3000,
    /// <summary>
    /// Map baking menu items.
    /// </summary>
    Bake          = SECTION_2 + 0,
    /// <summary>
    /// Generic section 3
    /// </summary>
    SECTION_3     = 4000,
    /// <summary>
    /// code gen menu items.
    /// </summary>
    CodeGen       = SECTION_3 + 0,
    /// <summary>
    /// Bottom priority.
    /// </summary>
    BOTTOM        = 5000,
  }
}

#endregion


#region QuantumEditorUtility.cs

namespace Quantum.Editor {
  using UnityEditor;

  partial class QuantumEditorUtility {
    public static void DelayCall(EditorApplication.CallbackFunction callback) {
      QuantumEditorLog.Assert(callback.Target == null, "DelayCall callback needs to stateless");
      EditorApplication.delayCall -= callback;
      EditorApplication.delayCall += callback;
    }
  }
}

#endregion


#region QuantumGlobalScriptableObjectEditorAttribute.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;

  class QuantumGlobalScriptableObjectEditorAttribute : QuantumGlobalScriptableObjectSourceAttribute {
    public QuantumGlobalScriptableObjectEditorAttribute(Type objectType) : base(objectType) {
    }

    public override QuantumGlobalScriptableObjectLoadResult Load(Type type) {
      var defaultAssetPath = QuantumGlobalScriptableObjectUtils.FindDefaultAssetPath(type, fallbackToSearchWithoutLabel: true);
      if (string.IsNullOrEmpty(defaultAssetPath)) {
        return default;
      }

      var result = (QuantumGlobalScriptableObject)AssetDatabase.LoadAssetAtPath(defaultAssetPath, type);
      QuantumEditorLog.Assert(result);
      return result;
    }
  }
}

#endregion


#region QuantumGlobalScriptableObjectUtils.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Utility methods for working with <see cref="QuantumGlobalScriptableObject"/>.
  /// </summary>
  public static class QuantumGlobalScriptableObjectUtils {
    /// <summary>
    /// The label that is assigned to global assets.
    /// </summary>
    public const string GlobalAssetLabel = "QuantumDefaultGlobal";

    /// <summary>
    /// Calls <see cref="EditorUtility.SetDirty(UnityEngine.Object)"/> on the object.
    /// </summary>
    /// <param name="obj"></param>
    public static void SetDirty(this QuantumGlobalScriptableObject obj) {
      EditorUtility.SetDirty(obj);
    }
    
    /// <summary>
    /// Locates the asset that is going to be used as a global asset for the given type, that is
    /// an asset marked with the <see cref="GlobalAssetLabel"/> label. If there are multiple such assets,
    /// exception is thrown. If there are no such assets, empty string is returned.
    /// </summary>
    public static string GetGlobalAssetPath<T>() where T : QuantumGlobalScriptableObject<T> {
      return FindDefaultAssetPath(typeof(T), fallbackToSearchWithoutLabel: false);
    }
    
    /// <summary>
    /// A wrapper around <see cref="GetGlobalAssetPath{T}"/> that returns a value indicating if
    /// it was able to find the asset.
    /// </summary>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see langword="true"/> if the asset was found</returns>
    public static bool TryGetGlobalAssetPath<T>(out string path) where T : QuantumGlobalScriptableObject<T> {
      path = FindDefaultAssetPath(typeof(T), fallbackToSearchWithoutLabel: false);
      return !string.IsNullOrEmpty(path);
    }
    
    private static QuantumGlobalScriptableObjectAttribute GetAttributeOrThrow(Type type) {
      var attribute = type.GetCustomAttribute<QuantumGlobalScriptableObjectAttribute>();
      if (attribute == null) {
        throw new InvalidOperationException($"Type {type.FullName} needs to be decorated with {nameof(QuantumGlobalScriptableObjectAttribute)}");
      }

      return attribute;
    }

    /// <summary>
    /// If the global asset does not exist, creates it based on the type's <see cref="QuantumGlobalScriptableObjectAttribute"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see langword="true"/> If the asset already existed.</returns>
    public static bool EnsureAssetExists<T>() where T : QuantumGlobalScriptableObject<T> {
      var defaultAssetPath = FindDefaultAssetPath(typeof(T), fallbackToSearchWithoutLabel: true);
      if (!string.IsNullOrEmpty(defaultAssetPath)) {
        // already exists
        return false;
      }
      
      // need to create a new asset
      CreateDefaultAsset(typeof(T));
      return true;
    }
    
    private static QuantumGlobalScriptableObject CreateDefaultAsset(Type type) {
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
        var instance = (QuantumGlobalScriptableObject)ScriptableObject.CreateInstance(type);

        AssetDatabase.CreateAsset(instance, attribute.DefaultPath);
        AssetDatabase.SaveAssets();

        SetGlobal(instance);

        EditorUtility.SetDirty(instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        QuantumEditorLog.TraceImport($"Created new global {type.Name} instance at {attribute.DefaultPath}");
        
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

        var instance = (QuantumGlobalScriptableObject)AssetDatabase.LoadAssetAtPath(attribute.DefaultPath, type);
        if (!instance) {
          throw new InvalidOperationException($"Failed to load a newly created asset at '{attribute.DefaultPath}'");
        }
        
        SetGlobal(instance);
        QuantumEditorLog.TraceImport($"Created new global {type.Name} instance at {attribute.DefaultPath}");
        return instance;
      }
    }
    
    private static bool IsDefault(this QuantumGlobalScriptableObject obj) {
      return Array.IndexOf(AssetDatabase.GetLabels(obj), GlobalAssetLabel) >= 0;
    }

    private static bool SetGlobal(QuantumGlobalScriptableObject obj) {
      var labels = AssetDatabase.GetLabels(obj);
      if (Array.IndexOf(labels, GlobalAssetLabel) >= 0) {
        return false;
      }

      Array.Resize(ref labels, labels.Length + 1);
      labels[^1] = GlobalAssetLabel;
      AssetDatabase.SetLabels(obj, labels);
      return true;
    }
    
    private static List<(QuantumGlobalScriptableObject, bool)> s_cache;
    
    internal static void CreateFindDefaultAssetPathCache() {
      s_cache = new List<(QuantumGlobalScriptableObject, bool)>();
      foreach (var it in AssetDatabaseUtils.IterateAssets<QuantumGlobalScriptableObject>()) {
        var asset = it.pptrValue as QuantumGlobalScriptableObject;
        if (asset == null) {
          continue;
        }
          
        var hasLabel = AssetDatabaseUtils.HasLabel(asset, GlobalAssetLabel);
        s_cache.Add((asset, hasLabel));
      }
    }

    internal static void ClearFindDefaultAssetPathCache() {
      s_cache = null;
    }
    
    internal static string FindDefaultAssetPath(Type type, bool fallbackToSearchWithoutLabel = false) {
      var list = new List<string>();

      if (s_cache != null) {
        foreach (var (asset, hasLabel) in s_cache) {
          if (!type.IsInstanceOfType(asset)) {
            continue;
          }

          if (!hasLabel && !fallbackToSearchWithoutLabel) {
            continue;
          }
        
          var assetPath = AssetDatabase.GetAssetPath(asset);
          Assert.Check(!string.IsNullOrEmpty(assetPath));
          list.Add(assetPath);
        }
      } else {
        var enumerator = AssetDatabaseUtils.IterateAssets(type: type, label: fallbackToSearchWithoutLabel ? null : GlobalAssetLabel);
        foreach (var asset in enumerator) {
          var path = AssetDatabase.GUIDToAssetPath(asset.guid);
          QuantumEditorLog.Assert(!string.IsNullOrEmpty(path));
          list.Add(path);
        }
      }

      if (list.Count == 0) {
        return string.Empty;
      }

      if (fallbackToSearchWithoutLabel) {
        var found = list.FindIndex(x => AssetDatabaseUtils.HasLabel(x, GlobalAssetLabel));
        if (found >= 0) {
          // carry on as if the search was without fallback in the first place
          list.RemoveAll(x => !AssetDatabaseUtils.HasLabel(x, GlobalAssetLabel));
          fallbackToSearchWithoutLabel = false;
          QuantumEditorLog.Assert(list.Count >= 1);
        }
      }

      if (list.Count == 1) {
        if (fallbackToSearchWithoutLabel) {
          AssetDatabaseUtils.SetLabel(list[0], GlobalAssetLabel, true);
          EditorUtility.SetDirty(AssetDatabase.LoadMainAssetAtPath(list[0]));
          QuantumEditorLog.Log($"Set '{list[0]}' as the default asset for '{type.Name}'");
        }

        return list[0];
      }

      if (fallbackToSearchWithoutLabel) {
        throw new InvalidOperationException($"There are no assets of type '{type.Name}' with {GlobalAssetLabel}, but there are multiple candidates: '{string.Join("', '", list)}'. Assign label manually or remove all but one.");
      } else {
        throw new InvalidOperationException($"There are multiple assets of type '{type.Name}' marked as default: '{string.Join("', '", list)}'. Remove all labels but one.");
      }
    }

    /// <summary>
    /// Attempts to import the global asset for the given type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see langword="true"/> if the asset was found and reimported</returns>
    public static bool TryImportGlobal<T>() where T : QuantumGlobalScriptableObject<T> {
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


#region QuantumGrid.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Linq.Expressions;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using Object = UnityEngine.Object;

  [Serializable]
  class QuantumGridState : TreeViewState {
    public MultiColumnHeaderState HeaderState;
    public bool                   SyncSelection;
  }
  
  class QuantumGridItem : TreeViewItem {
    public virtual Object TargetObject => null;
  }
  
  abstract class QuantumGrid<TItem> : QuantumGrid<TItem, QuantumGridState> 
    where TItem : QuantumGridItem {
  }
  
  [Serializable]
  abstract class QuantumGrid<TItem, TState> 
    where TState : QuantumGridState, new() 
    where TItem : QuantumGridItem
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

    public QuantumGrid() {
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
        
        if (sortingColumn < 0 && column.initiallySorted) {
          sortingColumn = i;
          column.sortedAscending = column.initiallySortedAscending;
        }

        if (!column.initiallyVisible) {
          continue;
        }
        
        visibleColumns.Add(i);
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
      if (GUILayout.Button(new GUIContent(QuantumEditorSkin.RefreshIcon, "Refresh"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) {
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

    protected static Column MakeSimpleColumn<T>(Expression<Func<TItem, T>> propertyExpression, Column column) {

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
      if (string.IsNullOrEmpty(column.headerContent.text) && string.IsNullOrEmpty(column.headerContent.tooltip)) {
        column.headerContent = new GUIContent(propertyName);
      }
        
      return column;
    }
    
    public class Column  : MultiColumnHeaderState.Column {
      public Func<TItem, string>             getSearchText;
      public Func<int, Comparison<TItem>>    getComparer;
      public Action<TItem, Rect, bool, bool> cellGUI;
      public bool                            initiallyVisible = true;
      public bool                            initiallySorted;
      public bool                            initiallySortedAscending = true;

      //
      // [Obsolete("Do not use", true)]
      // public new int userData => throw new NotImplementedException();
    }
    
    class InternalTreeView : TreeView {
      public InternalTreeView(QuantumGrid<TItem, TState> grid, MultiColumnHeader header) : base(grid.State, header) {
        Grid = grid;
        showAlternatingRowBackgrounds = true;
        this.Reload();
      }
      
      public new TState state => (TState)base.state;
      
      public QuantumGrid<TItem, TState> Grid { get; }

      
      protected override void SelectionChanged(IList<int> selectedIds) {
        base.SelectionChanged(selectedIds);
        if (state.SyncSelection) {
          SyncSelection();
        }
      }
      
      protected override void SingleClickedItem(int id) {
        if (state.SyncSelection) {
          var item = (TItem)FindItem(id, rootItem);
          var obj  = item.TargetObject;
          if (obj) {
            EditorGUIUtility.PingObject(obj);
          }
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


#region QuantumMonoBehaviourDefaultEditor.cs

namespace Quantum.Editor {
  using UnityEditor;

  [CustomEditor(typeof(QuantumMonoBehaviour), true)]
  internal class QuantumMonoBehaviourDefaultEditor : QuantumEditor {
  }
}

#endregion


#region QuantumPropertyDrawerMetaAttribute.cs

namespace Quantum.Editor {
  using System;

  [AttributeUsage(AttributeTargets.Class)]
  class QuantumPropertyDrawerMetaAttribute : Attribute {
    public bool HasFoldout   { get; set; }
    public bool HandlesUnits { get; set; }
  }
}

#endregion


#region QuantumScriptableObjectDefaultEditor.cs

namespace Quantum.Editor {
  using UnityEditor;

  [CustomEditor(typeof(QuantumScriptableObject), true)]
  internal class QuantumScriptableObjectDefaultEditor : QuantumEditor {
  }
}

#endregion


#region RawDataDrawer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Text;
  using UnityEditor;
  using UnityEngine;

  struct RawDataDrawer {
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

        _lastHash = arrayHash;
        _lastValue = new GUIContent(_builder.ToString());
      } else {
        Debug.Assert(_lastValue != null);
      }
    }

    public void Refresh(IList<byte> values, int maxLength = 2048) {
      Assert.Check(values != null);
      
      const int charactersPerElement = 2;
      int arraySize = values.Count;
      int arrayHash = 0;
      int effectiveArraySize;
      {
        int length = 0;
        int i;
        for (i = 0; i < arraySize && length < maxLength; ++i) {
          arrayHash = arrayHash * 31 + values[i];
          length += charactersPerElement + 1;
        }

        effectiveArraySize = i;
      }

      if (_builder == null || arrayHash != _lastHash) {
        var format = "{0:x" + charactersPerElement + "} ";

        _builder ??= new StringBuilder();
        _builder.Clear();

        for (int i = 0; i < effectiveArraySize; ++i) {
          _builder.AppendFormat(format, values[i]);
        }

        if (effectiveArraySize < arraySize) {
          _builder.AppendLine("...");
        }

        _lastHash = arrayHash;
        _lastValue = new GUIContent(_builder.ToString());
      } else {
        Debug.Assert(_lastValue != null);
      }
    }
    
    public void Refresh(SerializedProperty property, int maxLength = 2048) {
      Assert.Check(property != null);
      Assert.Check(property.isArray);

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

        _lastHash = arrayHash;
        _lastValue = new GUIContent(_builder.ToString());
      } else {
        Debug.Assert(_lastValue != null);
      }
    }


    public float GetHeight(float width) {
      return QuantumEditorSkin.RawDataStyle.Value.CalcHeight(_lastValue ?? GUIContent.none, width);
    }

    public string Draw(Rect position) => Draw(GUIContent.none, position);
    
    public string Draw(GUIContent label, Rect position) {
      var id = GUIUtility.GetControlID(UnityInternal.EditorGUI.DelayedTextFieldHash, FocusType.Keyboard, position);
      return UnityInternal.EditorGUI.DelayedTextFieldInternal(position, id, label, _lastValue.text ?? string.Empty, "0123456789abcdefABCDEF ", QuantumEditorSkin.RawDataStyle);
    }

    public string DrawLayout() {
      var position = EditorGUILayout.GetControlRect(false, 18f, QuantumEditorSkin.RawDataStyle);
      return Draw(position);
    }
  }
}

#endregion


#region ReflectionUtils.cs

namespace Quantum.Editor {
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
    
    internal static T CreateMethodDelegate<T>(this Type type, string methodName, BindingFlags flags, Type delegateType, params DelegateSwizzle[] fallbackSwizzles) where T : Delegate {
      try {
        delegateType ??= typeof(T);
        
        
        var method = GetMethodOrThrow(type, methodName, flags, delegateType, fallbackSwizzles, out var swizzle);
        if (swizzle == null && typeof(T) == delegateType) {
          return (T)Delegate.CreateDelegate(typeof(T), method);
        }

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
            foreach (var converter in swizzle.Converters) {
              convertedParameters.Add(Expression.Invoke(converter, parameters));
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
    
    public static MethodInfo GetMethodOrThrow(this Type type, string methodName, BindingFlags flags = DefaultBindingFlags) {
      var method = type.GetMethod(methodName, flags);
      if (method == null) {
        throw new ArgumentOutOfRangeException(nameof(methodName), CreateFieldExceptionMessage(type.Assembly, type.FullName, methodName, flags));
      }

      return method;
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
          var swizzled = swizzle.Types;
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
          var swizzled = swizzle.Types;
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

    internal static class DelegateSwizzle<In0, In1> {
      public static DelegateSwizzle Make<Out0>(Expression<Func<In0, In1, Out0>> out0) {
        return new DelegateSwizzle(new Expression[] { out0 }, new [] { typeof(Out0)});
      }
      
      public static DelegateSwizzle Make<Out0, Out1>(Expression<Func<In0, In1, Out0>> out0, Expression<Func<In0, In1, Out1>> out1) {
        return new DelegateSwizzle(new Expression[] { out0, out1 }, new [] { typeof(Out0), typeof(Out1)});
      }
      
      public static DelegateSwizzle Make<Out0, Out1, Out3>(Expression<Func<In0, In1, Out0>> out0, Expression<Func<In0, In1, Out1>> out1, Expression<Func<In0, In1, Out3>> out3) {
        return new DelegateSwizzle(new Expression[] { out0, out1, out3 }, new [] { typeof(Out0), typeof(Out1), typeof(Out3)});
      }
    }

    internal class DelegateSwizzle {
      public DelegateSwizzle(Expression[] converters, Type[] types) {
        Converters = converters;
        Types = types;
      }

      public Expression[] Converters { get; }
      public Type[] Types { get; }
    }

#if UNITY_EDITOR
    
    public static T CreateEditorMethodDelegate<T>(string editorAssemblyTypeName, string methodName, BindingFlags flags) where T : Delegate {
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

namespace Quantum.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using UnityEditor;

  static partial class SerializedPropertyUtilities {
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
        throw new ArgumentOutOfRangeException(nameof(relativePropertyPath), $"Property not found: {relativePropertyPath} (relative to \"{sp.propertyPath}\" of {sp.serializedObject.targetObject}");
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
      
      ReadOnlySpan<char> parentPath = property.propertyPath;
      
      int startIndex = 0;

      do {
        // array element?
        if (parentPath.EndsWith("]", StringComparison.Ordinal)) {
          int arrayDataIndex = parentPath.LastIndexOf(".Array.data[");
          if (arrayDataIndex >= 0) {
            parentPath = parentPath.Slice(0, arrayDataIndex);
          }
        }

        var lastDotIndex = parentPath.LastIndexOf('.');
        if (lastDotIndex < 0) {
          if (parentPath.Length == 0) {
            return null;
          }

          parentPath = string.Empty;
        } else {
          parentPath = parentPath.Slice(0, lastDotIndex);
        }

      } while (relativePath[startIndex++] == '^');

      if (startIndex > 1) {
        relativePath = relativePath.Substring(startIndex - 1);
      }
      
      if (parentPath.Length == 0) {
        return property.serializedObject.FindProperty(relativePath);
      } else {
        return property.serializedObject.FindProperty($"{parentPath.ToString()}.{relativePath}");
      }
    }

    public static bool IsArrayElement(string propertyPath) {
      if (!propertyPath.EndsWith("]", StringComparison.Ordinal)) {
        return false;
      }

      return true;
    }

    public static bool IsArrayElement(this SerializedProperty sp) {
      return sp.depth > 0 && IsArrayElement(sp.propertyPath);
    }

    public static bool IsArrayElement(this SerializedProperty sp, out int index) {
      if (sp.depth == 0) {
        index = -1;
        return false;
      }
      
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
      
      if (path.EndsWith("]", StringComparison.Ordinal)) {
        int arrayDataIndex = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
        if (arrayDataIndex >= 0) {
          var arrayPath = path.Substring(0, arrayDataIndex);
          return sp.serializedObject.FindProperty(arrayPath);
        }
      }
      
      throw new ArgumentException($"Property is not an array element: {path}");
    }

    public static bool IsArrayProperty(this SerializedProperty sp) {
      return sp.isArray && sp.propertyType != SerializedPropertyType.String;
    }

    public static bool ShouldIncludeChildren(this SerializedProperty sp) {
      return sp.isExpanded || sp.propertyType == SerializedPropertyType.Generic || sp.IsArrayProperty();
    }
    
    
    // public static int GetHashCodeForPropertyPath(this SerializedProperty sp) {
    //   return UnityInternal.SerializedProperty.hashCodeForPropertyPath.GetValue(sp);
    // }
    
    public static int GetHashCodeForPropertyPathWithoutArrayIndex(this SerializedProperty sp) {
      return UnityInternal.SerializedProperty.hashCodeForPropertyPathWithoutArrayIndex.GetValue(sp);
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
            QuantumEditorLog.Assert(element.propertyType == SerializedPropertyType.Integer, "Invalid property type, expected integer");
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

// ReSharper disable InconsistentNaming
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Quantum.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;
  using static ReflectionUtils;


  static partial class UnityInternal {
    
    static Assembly FindAssembly(string name) {
      return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
    }
    
    [UnityEditor.InitializeOnLoad]
    public static class Event {
      static readonly StaticAccessor<UnityEngine.Event> s_Current_ = typeof(UnityEngine.Event).CreateStaticFieldAccessor<UnityEngine.Event>(nameof(s_Current));
      public static UnityEngine.Event s_Current => s_Current_.GetValue();
    }
    
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
    public static class GUIClip {
      public static Type InternalType = typeof(UnityEngine.GUIUtility).Assembly.GetType("UnityEngine.GUIClip", true);
      
      private static readonly StaticAccessor<Rect> _visibleRect = InternalType.CreateStaticPropertyAccessor<Rect>(nameof(visibleRect));
      public static Rect visibleRect => _visibleRect.GetValue();
    }
    
    [UnityEditor.InitializeOnLoad]
    public static class HandleUtility {
      public static readonly Action ApplyWireMaterial = typeof(UnityEditor.HandleUtility).CreateMethodDelegate<Action>(nameof(ApplyWireMaterial));
    }


    [UnityEditor.InitializeOnLoad]
    public static class LayerMatrixGUI {
      private const string TypeName =
#if UNITY_2023_1_OR_NEWER
        "UnityEditor.LayerCollisionMatrixGUI2D";
#else
        "UnityEditor.LayerMatrixGUI";
#endif

      private static readonly Type InternalType =
#if UNITY_2023_1_OR_NEWER
        FindAssembly("UnityEditor.Physics2DModule")?.GetType(TypeName, true);
#else 
        typeof(UnityEditor.Editor).Assembly.GetType(TypeName, true);
#endif
      
      private static readonly Type InternalGetValueFuncType = InternalType?.GetNestedTypeOrThrow(nameof(GetValueFunc), BindingFlags.Public);
      private static readonly Type InternalSetValueFuncType = InternalType?.GetNestedTypeOrThrow(nameof(SetValueFunc), BindingFlags.Public);
      
#if UNITY_2023_1_OR_NEWER
      private static readonly Delegate _Draw = InternalType?.CreateMethodDelegate(nameof(Draw), BindingFlags.Public | BindingFlags.Static, 
        typeof(Action<,,>).MakeGenericType(
          typeof(GUIContent), InternalGetValueFuncType, InternalSetValueFuncType)
      );
#else 
      private delegate void Ref2Action<T1, T2, T3, T4>(T1 t1, ref T2 t2, T3 t3, T4 t4);

      private static readonly Delegate _DoGUI = InternalType?.CreateMethodDelegate("DoGUI", BindingFlags.Public | BindingFlags.Static,
        typeof(Ref2Action<,,,>).MakeGenericType(
          typeof(GUIContent), typeof(bool), InternalGetValueFuncType, InternalSetValueFuncType)
      );
#endif
      
      public delegate bool GetValueFunc(int layerA, int layerB);
      public delegate void SetValueFunc(int layerA, int layerB, bool val);

      public static void Draw(GUIContent label, GetValueFunc getValue, SetValueFunc setValue) {
        if (InternalType == null) {
          throw new InvalidOperationException($"{TypeName} not found");
        }
        
        var getter = Delegate.CreateDelegate(InternalGetValueFuncType, getValue.Target, getValue.Method);
        var setter = Delegate.CreateDelegate(InternalSetValueFuncType, setValue.Target, setValue.Method);
        
#if UNITY_2023_1_OR_NEWER
        _Draw.DynamicInvoke(label, getter, setter);
#else
        bool show = true;
        var args = new object[] { label, show, getter, setter };
        _DoGUI.DynamicInvoke(args);
#endif
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
    public static class HierarchyProperty {
      public delegate void CopySearchFilterFromDelegate(UnityEditor.HierarchyProperty to, UnityEditor.HierarchyProperty from);
      public static CopySearchFilterFromDelegate CopySearchFilterFrom = typeof(UnityEditor.HierarchyProperty).CreateMethodDelegate<CopySearchFilterFromDelegate>(nameof(CopySearchFilterFrom), 
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }
    
    [UnityEditor.InitializeOnLoad]
    public static class ScriptAttributeUtility {
      
      public static readonly Type InternalType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ScriptAttributeUtility", true);
      
      public delegate FieldInfo GetFieldInfoFromPropertyDelegate(UnityEditor.SerializedProperty property, out Type type);
      public static readonly GetFieldInfoFromPropertyDelegate GetFieldInfoFromProperty =
        InternalType.CreateMethodDelegate<GetFieldInfoFromPropertyDelegate>(
          "GetFieldInfoFromProperty",
          BindingFlags.Static | BindingFlags.NonPublic);
      
      public delegate Type GetDrawerTypeForTypeDelegate(Type type, bool isManagedReference);
      public static readonly GetDrawerTypeForTypeDelegate GetDrawerTypeForType =
        InternalType.CreateMethodDelegate<GetDrawerTypeForTypeDelegate>(
          "GetDrawerTypeForType",
          BindingFlags.Static | BindingFlags.NonPublic,
          null,
          DelegateSwizzle<Type, bool>.Make((t, b) => t), // post 2023.3
          DelegateSwizzle<Type, bool>.Make((t, b) => t, (t, b) => (Type[])null, (t, b) => b) // pre 2023.3.23
        );
      
      public delegate Type GetDrawerTypeForPropertyAndTypeDelegate(UnityEditor.SerializedProperty property, Type type);
      public static readonly GetDrawerTypeForPropertyAndTypeDelegate GetDrawerTypeForPropertyAndType = 
        InternalType.CreateMethodDelegate<GetDrawerTypeForPropertyAndTypeDelegate>(
          "GetDrawerTypeForPropertyAndType",
          BindingFlags.Static | BindingFlags.NonPublic);

      private static readonly GetHandlerDelegate _GetHandler = InternalType.CreateMethodDelegate<GetHandlerDelegate>("GetHandler", BindingFlags.NonPublic | BindingFlags.Static,
        MakeFuncType(typeof(UnityEditor.SerializedProperty), PropertyHandler.InternalType)
      );

      public delegate List<PropertyAttribute> GetFieldAttributesDelegate(FieldInfo field);
      public static readonly GetFieldAttributesDelegate GetFieldAttributes = InternalType.CreateMethodDelegate<GetFieldAttributesDelegate>(nameof(GetFieldAttributes));

      private static readonly StaticAccessor<object> _propertyHandlerCache = InternalType.CreateStaticPropertyAccessor(nameof(propertyHandlerCache), PropertyHandlerCache.InternalType);

      private static readonly StaticAccessor<object> s_SharedNullHandler = InternalType.CreateStaticFieldAccessor("s_SharedNullHandler", PropertyHandler.InternalType);
      private static readonly StaticAccessor<object> s_NextHandler       = InternalType.CreateStaticFieldAccessor("s_NextHandler", PropertyHandler.InternalType);

      public static PropertyHandlerCache propertyHandlerCache => new() {
        _instance = _propertyHandlerCache.GetValue()
      };

      public static PropertyHandler sharedNullHandler => PropertyHandler.Wrap(s_SharedNullHandler.GetValue());
      public static PropertyHandler nextHandler => PropertyHandler.Wrap(s_NextHandler.GetValue());
      
      public static PropertyHandler GetHandler(UnityEditor.SerializedProperty property) {
        return PropertyHandler.Wrap(_GetHandler(property));
      }

      private delegate object GetHandlerDelegate(UnityEditor.SerializedProperty property);
    }

    public struct PropertyHandlerCache {
      [UnityEditor.InitializeOnLoad]
      private static class Statics {
        public static readonly Type                    InternalType    = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyHandlerCache", true);
        public static readonly GetPropertyHashDelegate GetPropertyHash = InternalType.CreateMethodDelegate<GetPropertyHashDelegate>(nameof(GetPropertyHash));

        public static readonly GetHandlerDelegate GetHandler = InternalType.CreateMethodDelegate<GetHandlerDelegate>(nameof(GetHandler), BindingFlags.NonPublic | BindingFlags.Instance,
          MakeFuncType(InternalType, typeof(UnityEditor.SerializedProperty), PropertyHandler.InternalType));

        public static readonly SetHandlerDelegate SetHandler = InternalType.CreateMethodDelegate<SetHandlerDelegate>(nameof(SetHandler), BindingFlags.NonPublic | BindingFlags.Instance,
          MakeActionType(InternalType, typeof(UnityEditor.SerializedProperty), PropertyHandler.InternalType));
        
        public static readonly FieldInfo m_PropertyHandlers = InternalType.GetFieldOrThrow(nameof(m_PropertyHandlers));
      }

      public static Type InternalType => Statics.InternalType;

      public delegate int GetPropertyHashDelegate(UnityEditor.SerializedProperty property);

      public delegate object GetHandlerDelegate(object instance, UnityEditor.SerializedProperty property);

      public delegate void SetHandlerDelegate(object instance, UnityEditor.SerializedProperty property, object handlerInstance);

      public object _instance;

      public PropertyHandler GetHandler(UnityEditor.SerializedProperty property) {
        return new PropertyHandler {
          _instance = Statics.GetHandler(_instance, property)
        };
      }

      public void SetHandler(UnityEditor.SerializedProperty property, PropertyHandler newHandler) {
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
    public static class SerializedProperty {
      //public static readonly InstanceAccessor<int> hashCodeForPropertyPath                  = typeof(UnityEditor.SerializedProperty).CreatePropertyAccessor<int>(nameof(hashCodeForPropertyPath));
      public static readonly InstanceAccessor<int> hashCodeForPropertyPathWithoutArrayIndex = typeof(UnityEditor.SerializedProperty).CreatePropertyAccessor<int>(nameof(hashCodeForPropertyPathWithoutArrayIndex));
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
      
      internal LazyGUIStyle InspectorTitlebar                => LazyGUIStyle.Create(_ => GetStyle("IN Title"));
      internal LazyGUIStyle FoldoutTitlebar                  => LazyGUIStyle.Create(_ => GetStyle("Titlebar Foldout", "Foldout"));
      internal LazyGUIStyle BoxWithBorders                   => LazyGUIStyle.Create(_ => GetStyle("OL Box"));
      internal LazyGUIStyle HierarchyTreeViewLine            => LazyGUIStyle.Create(_ => GetStyle("TV Line"));
      internal LazyGUIStyle HierarchyTreeViewSceneBackground => LazyGUIStyle.Create(_ => GetStyle("SceneTopBarBg", "ProjectBrowserTopBarBg"));
      internal LazyGUIStyle OptionsButtonStyle               => LazyGUIStyle.Create(_ => GetStyle("PaneOptions"));
      internal LazyGUIStyle AddComponentButton               => LazyGUIStyle.Create(_ => GetStyle("AC Button"));
      internal LazyGUIStyle AnimationEventTooltip            => LazyGUIStyle.Create(_ => GetStyle("AnimationEventTooltip"));
      internal LazyGUIStyle AnimationEventTooltipArrow       => LazyGUIStyle.Create(_ => GetStyle("AnimationEventTooltipArrow"));
      
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
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
// ReSharper enable InconsistentNaming

#endregion


#region ArrayLengthAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using System.Collections;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class ArrayLengthAttributeDrawer {
    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, ArrayLengthAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }

    class OdinAttributeProxy : Attribute {
      public ArrayLengthAttribute SourceAttribute;
    }

    [DrawerPriorityAttribute(DrawerPriorityLevel.WrapperPriority)]
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

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using Sirenix.OdinInspector;

  partial class BinaryDataAttributeDrawer {
    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, BinaryDataAttribute attribute) {
      return new[] { new DrawWithUnityAttribute() };
    }
  }
}
#endif

#endregion


#region DoIfAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
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
            if (Property.GetValueDepth() != 0) {
              if (doIf.ErrorOnConditionMemberNotFound) {
                QuantumEditorLog.ErrorInspector($"Can't check condition for {Property.Path}: non-SerializedProperty checks only work for top-level properties");
              }
            } else {
              try {
                _cachedGetters.Add((objType, doIf.ConditionMember), Property.ParentType.CreateGetter(doIf.ConditionMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy));
              } catch (Exception e) {
                if (doIf.ErrorOnConditionMemberNotFound) {
                  QuantumEditorLog.ErrorInspector($"Can't check condition for {Property.Path}: unable to create getter for {doIf.ConditionMember} with exception {e}");
                }
              }
            }
          }

          if (getter != null) {
            foreach (var obj in Property.GetValueParent().ValueEntry.WeakValues) {
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

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class DrawIfAttributeDrawer {

    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, DrawIfAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : OdinProxyAttributeBase {
    }

    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
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

namespace Quantum.Editor {
  partial class DrawInlineAttributeDrawer {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, DrawInlineAttribute attribute) {
      return new System.Attribute[] { new Sirenix.OdinInspector.InlinePropertyAttribute(), new Sirenix.OdinInspector.HideLabelAttribute() };
    }
#endif
  }
}

#endregion


#region ErrorIfAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using Sirenix.OdinInspector.Editor;
  using UnityEngine;

  partial class ErrorIfAttributeDrawer {

    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, ErrorIfAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : OdinProxyAttributeBase {
    }
    
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    class OdinDrawer : OdinDrawerBase<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label, bool allPassed, bool anyPassed) {
        var attribute = (ErrorIfAttribute)Attribute.SourceAttribute;
        
        base.CallNextDrawer(label);

        if (anyPassed) {
          using (new QuantumEditorGUI.ErrorScope(attribute.Message)) {
          }
        }
      }
    }
  }
}
#endif

#endregion


#region FieldEditorButtonAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using System.Linq;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class FieldEditorButtonAttributeDrawer {

    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, FieldEditorButtonAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public FieldEditorButtonAttribute SourceAttribute;
    }

    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    class OdinDrawer : OdinAttributeDrawer<OdinAttributeProxy> {
      protected override bool CanDrawAttributeProperty(InspectorProperty property) {
        return !property.IsArrayElement(out _);
      }

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

namespace Quantum.Editor {
  partial class HideArrayElementLabelAttributeDrawer {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, HideArrayElementLabelAttribute attribute) {
      // not yet supported
      return System.Array.Empty<System.Attribute>();
    }
#endif
  }
}

#endregion


#region InlineHelpAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class InlineHelpAttributeDrawer {
    
    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, InlineHelpAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public InlineHelpAttribute SourceAttribute;
    }

    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
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

        using (new QuantumEditorGUI.GUIContentScope(label)) {

          (wasExpanded, buttonRect) = InlineHelpAttributeDrawer.DrawInlineHelpBeforeProperty(label, helpContent, _lastRect, Property.Path.GetHashCode(), EditorGUI.indentLevel, hasFoldout, Property.SerializationRoot);

          EditorGUILayout.BeginVertical();
          this.CallNextDrawer(label);
          EditorGUILayout.EndVertical();
        }

        if (Event.current.type == EventType.Repaint) {
          _lastRect = GUILayoutUtility.GetLastRect();
        }

        if (propertyRect.width > 1 && propertyRect.height > 1) {

          if (wasExpanded) {
            var height = QuantumEditorGUI.GetInlineBoxSize(helpContent).y;
            EditorGUILayout.GetControlRect(false, height);
            propertyRect.height += QuantumEditorGUI.GetInlineBoxSize(helpContent).y;
          }

          DrawInlineHelpAfterProperty(buttonRect, wasExpanded, helpContent, propertyRect);
        }
      }

      private GUIContent GetHelpContent(InspectorProperty property, bool includeTypeHelp) {
        var parentType = property.ValueEntry.ParentType;
        var memberInfo = parentType.GetFieldIncludingBaseTypes(property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return QuantumCodeDoc.FindEntry(memberInfo, includeTypeHelp) ?? GUIContent.none;
      }

    }
  }
}
#endif

#endregion


#region LayerMatrixAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class LayerMatrixAttributeDrawer {

    [QuantumOdinAttributeConverter]
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


#region QuantumOdinAttributeConverterAttribute.cs

namespace Quantum.Editor {
  using System;

  [AttributeUsage(AttributeTargets.Method)]
  class QuantumOdinAttributeConverterAttribute : Attribute {
  }
}

#endregion


#region QuantumOdinAttributeProcessor.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEngine;

  internal class QuantumOdinAttributeProcessor : Sirenix.OdinInspector.Editor.OdinAttributeProcessor {
    public override void ProcessChildMemberAttributes(Sirenix.OdinInspector.Editor.InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes) {
      for (int i = 0; i < attributes.Count; ++i) {
        var attribute = attributes[i];
        if (attribute is PropertyAttribute) {
          
          var drawerType = QuantumEditorGUI.GetDrawerTypeIncludingWorkarounds(attribute);
          if (drawerType != null) {

            var method = drawerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
             .FirstOrDefault(x => x.IsDefined(typeof(QuantumOdinAttributeConverterAttribute)));

            if (method != null) {
              var replacementAttributes = (System.Attribute[])method.Invoke(null, new object[] { member, attribute }) ?? Array.Empty<Attribute>();

              attributes.RemoveAt(i);
              QuantumEditorLog.TraceInspector($"Replacing attribute {attribute.GetType().FullName} of {member.ToString()} with {string.Join(", ", replacementAttributes.Select(x => x.GetType().FullName))}");

              if (replacementAttributes.Length > 0) {
                attributes.InsertRange(i, replacementAttributes);
              }

              i += replacementAttributes.Length - 1;
              continue;
            }
          }

          if (attribute is DecoratingPropertyAttribute) {
            QuantumEditorLog.Warn($"Unable to replace {nameof(DecoratingPropertyAttribute)}-derived attribute: {attribute.GetType().FullName}");
            attributes.RemoveAt(i--);
          }
        }
      }
    }
  }
}
#endif

#endregion


#region QuantumOdinExtensions.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  static class QuantumOdinExtensions {
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

    public static int GetValueDepth(this InspectorProperty property) {
      int depth = 0;
      
      var parent = property.GetValueParent();
      while (parent?.IsTreeRoot == false) {
        ++depth;
        parent = parent.GetValueParent();
      }
      
      return depth;
    }

    public static InspectorProperty GetValueParent(this InspectorProperty property) {
      
      var parent = property.Parent;
      while (parent?.Info.PropertyType == PropertyType.Group) {
        parent = parent.Parent;
      }
      return parent;
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
        if (referenceProperty.GetValueParent() == null) {
          return null;
        }
        
        referenceProperty = referenceProperty.GetValueParent();
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

    public static (QuantumPropertyDrawerMetaAttribute, Attribute) GetNextPropertyDrawerMetaAttribute(this InspectorProperty property, Attribute referenceAttribute) {

      var attributeIndex = referenceAttribute == null ? -1 : property.Attributes.IndexOf(referenceAttribute);

      for (int i = attributeIndex + 1; i < property.Attributes.Count; ++i) {
        var otherAttribute = property.Attributes[i];
        if (otherAttribute is DrawerPropertyAttribute == false) {
          continue;
        }

        var attributeDrawerType = QuantumEditorGUI.GetDrawerTypeIncludingWorkarounds(otherAttribute);
        if (attributeDrawerType == null) {
          continue;
        }

        var meta = attributeDrawerType.GetCustomAttribute<QuantumPropertyDrawerMetaAttribute>();
        if (meta != null) {
          return (meta, otherAttribute);
        }
      }

      
      var propertyDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForType(property.ValueEntry.TypeOfValue, false);

      if (propertyDrawerType != null) {
        var meta = propertyDrawerType.GetCustomAttribute<QuantumPropertyDrawerMetaAttribute>();
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

namespace Quantum.Editor {
  partial class ReadOnlyAttributeDrawer {
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
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

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;

  partial class SerializeReferenceTypePickerAttributeDrawer {
    [QuantumOdinAttributeConverter]
      static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, SerializeReferenceTypePickerAttribute attribute) {
        return Array.Empty<System.Attribute>();
      }
  }
}
#endif

#endregion


#region UnitAttributeDrawer.Odin.cs

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using System;
  using Sirenix.OdinInspector.Editor;
  using UnityEditor;
  using UnityEngine;

  partial class UnitAttributeDrawer {

    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, UnitAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : Attribute {
      public UnitAttribute SourceAttribute;
    }
  
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
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

#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
namespace Quantum.Editor {
  using Sirenix.OdinInspector.Editor;
  using UnityEngine;

  partial class WarnIfAttributeDrawer {

    [QuantumOdinAttributeConverter]
    static System.Attribute[] ConvertToOdinAttributes(System.Reflection.MemberInfo memberInfo, WarnIfAttribute attribute) {
      return new[] { new OdinAttributeProxy() { SourceAttribute = attribute } };
    }
    
    class OdinAttributeProxy : OdinProxyAttributeBase {
    }
    
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    class OdinDrawer : OdinDrawerBase<OdinAttributeProxy> {
      protected override void DrawPropertyLayout(GUIContent label, bool allPassed, bool anyPassed) {
        var attribute = (WarnIfAttribute)Attribute.SourceAttribute;
        
        base.CallNextDrawer(label);

        if (anyPassed) {
          using (new QuantumEditorGUI.WarningScope(attribute.Message)) {
          }
        }
      }
    }
  }
}
#endif

#endregion


#region ArrayLengthAttributeDrawer.cs

namespace Quantum.Editor {
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

namespace Quantum.Editor {
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

    Dictionary<string, AssemblyInfo> _allAssemblies;

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var  assemblyName = property.stringValue;
      bool notFound     = false;
      
      if (!string.IsNullOrEmpty(assemblyName)) {
        if (_allAssemblies == null) {
          _allAssemblies = GetAssemblies(AsmDefType.All).ToDictionary(x => x.Name, x => x);
        }

        if (!_allAssemblies.TryGetValue(assemblyName, out var assemblyInfo)) {
          SetInfo($"Assembly not found: {assemblyName}");
          notFound = true;
        } else if (((AssemblyNameAttribute)attribute).RequiresUnsafeCode && !assemblyInfo.AllowUnsafeCode) {
          if (assemblyInfo.IsPredefined) {
            SetError($"Predefined assemblies need 'Allow Unsafe Code' enabled in Player Settings");
          } else {
            SetError($"Assembly does not allow unsafe code");
          }
        }
      }

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
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

            foreach (var asm in GetAssemblies(flag | AsmDefType.InPackages)) {
              menu.AddItem(new GUIContent($"{prefix}Packages/{asm.Name}"), string.Equals(asm.Name, assemblyName, StringComparison.OrdinalIgnoreCase), onClicked, asm.Name);
            }

            menu.AddSeparator(prefix);

            foreach (var asm in GetAssemblies(flag | AsmDefType.InAssets | AsmDefType.Predefined)) {
              menu.AddItem(new GUIContent($"{prefix}{asm.Name}"), string.Equals(asm.Name, assemblyName, StringComparison.OrdinalIgnoreCase), onClicked, asm.Name);
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

    static IEnumerable<AssemblyInfo> GetAssemblies(AsmDefType types) {
      var result = new Dictionary<string, AsmDefData>(StringComparer.OrdinalIgnoreCase);

      if (types.HasFlag(AsmDefType.Predefined)) {
        if (types.HasFlag(AsmDefType.Runtime)) {
          yield return new AssemblyInfo("Assembly-CSharp-firstpass", PlayerSettings.allowUnsafeCode, true);
          yield return new AssemblyInfo("Assembly-CSharp", PlayerSettings.allowUnsafeCode, true);
        }

        if (types.HasFlag(AsmDefType.Editor)) {
          yield return new AssemblyInfo("Assembly-CSharp-Editor-firstpass", PlayerSettings.allowUnsafeCode, true);
          yield return new AssemblyInfo("Assembly-CSharp-Editor", PlayerSettings.allowUnsafeCode, true);
        }
      }

      if (types.HasFlag(AsmDefType.InAssets) || types.HasFlag(AsmDefType.InPackages)) {
        var query = AssetDatabase.FindAssets("t:asmdef")
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
         });
        
        foreach (var asmdef in query) {
          yield return new AssemblyInfo(asmdef.name, asmdef.allowUnsafeCode, false);
        }
      }
    }

    [Serializable]
    private class AsmDefData {
      public string[] includePlatforms = Array.Empty<string>();
      public string   name             = string.Empty;
      public bool     allowUnsafeCode;
    }
    
    private struct AssemblyInfo {
      public string Name;
      public bool   AllowUnsafeCode;
      public bool   IsPredefined;
      
      public AssemblyInfo(string name, bool allowUnsafeCode, bool isPredefined) {
        Name           = name;
        AllowUnsafeCode = allowUnsafeCode;
        IsPredefined   = isPredefined;
      }
    }
  }
}

#endregion


#region BinaryDataAttributeDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class BinaryDataAttributeDrawer : PropertyDrawerWithErrorHandling, INonApplicableOnArrayElements {
    
    private int           MaxLines  = 16;
    private RawDataDrawer _drawer   = new RawDataDrawer();

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        bool wasExpanded = property.isExpanded;
        
        var foldoutPosition = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
        property.isExpanded = EditorGUI.Foldout(foldoutPosition, property.isExpanded, label);

        if (property.hasMultipleDifferentValues) {
          QuantumEditorGUI.Overlay(foldoutPosition, $"---");
        } else {
          QuantumEditorGUI.Overlay(foldoutPosition, $"{property.arraySize}");
        }

        if (!wasExpanded) {
          return;
        }
        
        position.yMin += foldoutPosition.height + EditorGUIUtility.standardVerticalSpacing;
        using (new QuantumEditorGUI.EnabledScope(true)) {
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
        Mathf.Min(QuantumEditorGUI.GetLinesHeight(MaxLines), height);
    }
  }
  
    
  [CustomPropertyDrawer(typeof(BinaryDataAttribute))]
  [RedirectCustomPropertyDrawer(typeof(BinaryDataAttribute), typeof(BinaryDataAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region BitSetAttributeDrawer.cs

// namespace Quantum.Editor {
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
//       using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out var valueRect)) {
//         var pos = valueRect;
//
//         DrawAndMeasureLabel(valueRect, bitStart, QuantumEditorSkin.instance.MiniLabelLowerRight);
//         DrawAndMeasureLabel(valueRect, bitEnd, QuantumEditorSkin.instance.MiniLabelLowerLeft);
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

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Unity.Profiling;
  using UnityEditor;
  using UnityEngine;

  internal abstract class DecoratingPropertyAttributeDrawer : PropertyDrawer {
    bool _isLastDrawer;
    int  _nestingLevel;
    bool _isInitialized;

    public PropertyDrawer NextDrawer { get; private set; }

    public DecoratingPropertyAttributeDrawer() {
      QuantumEditorLog.TraceInspector(GetLogMessage("constructor"));
    }
    
    [Obsolete("Derived classes should override and call OnGUIInternal", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
      QuantumEditorLog.TraceInspector(GetLogMessage($"OnGUI({position}, {property.propertyPath}, {label})"));
      EnsureInitialized(property);
      InvokeOnGUIInternal(position, property, label);
    }

    [Obsolete("Derived classes should override and call GetPropertyHeightInternal", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public sealed override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
      QuantumEditorLog.TraceInspector(GetLogMessage($"GetPropertyHeight({property.propertyPath}, {label})"));
      EnsureInitialized(property);
      return InvokeGetPropertyHeightInternal(property, label);
    }

    protected virtual float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      return InvokeGetPropertyHeightOnNextDrawer(property, label);
    }

    protected virtual void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      QuantumEditorLog.TraceInspector(GetLogMessage($"OnGUIInternal({position}, {property.propertyPath}, {label})"));
      
      if (_nestingLevel != 0) {
        QuantumEditorLog.Assert(false, $"{property.propertyPath} {GetType().FullName}");
      }
      _nestingLevel++;
      try {
        InvokeOnGUIOnNextDrawer(this, position, property, label);
      } finally {
        _nestingLevel--;
      }
    }

    private void InvokeOnGUIOnNextDrawer(DecoratingPropertyAttributeDrawer current, Rect position, SerializedProperty prop, GUIContent label) {
      if (NextDrawer != null) {
        NextDrawer.OnGUI(position, prop, label);
      } else {
        QuantumEditorGUI.ForwardPropertyField(position, prop, label, prop.ShouldIncludeChildren(), _isLastDrawer);
      }
    }
    
    private float InvokeGetPropertyHeightOnNextDrawer(SerializedProperty prop, GUIContent label) {
      if (NextDrawer != null) {
        return NextDrawer.GetPropertyHeight(prop, label);
      }

      var includeChildren = prop.ShouldIncludeChildren();
      if (_isLastDrawer && !includeChildren) {
        return EditorGUI.GetPropertyHeight(prop.propertyType, label);
      }
      return EditorGUI.GetPropertyHeight(prop, label, includeChildren);
    }

    private void InvokeOnGUIInternal(Rect position, SerializedProperty prop, GUIContent label) {
      if (this is INonApplicableOnArrayElements && prop.IsArrayElement()) {
        InvokeOnGUIOnNextDrawer(this, position, prop, label);
      } else {
        OnGUIInternal(position, prop, label);
      }
    }


    private float InvokeGetPropertyHeightInternal(SerializedProperty prop, GUIContent label) {
      if (this is INonApplicableOnArrayElements && prop.IsArrayElement()) {
        return InvokeGetPropertyHeightOnNextDrawer(prop, label);
      } else {
        return GetPropertyHeightInternal(prop, label);
      }
    }
    
    protected virtual bool EnsureInitialized(SerializedProperty property) {
      if (_isInitialized) {
        return false;
      }

      if (fieldInfo == null) {
        // this might happen if this drawer is created dynamically
        var field = UnityInternal.ScriptAttributeUtility.GetFieldInfoFromProperty(property, out _);
        QuantumEditorLog.Assert(field != null, $"Could not find field for property {property.propertyPath} of type {property.serializedObject.targetObject.GetType().FullName} (I'm {GetType().FullName} {GetHashCode()})");
        UnityInternal.PropertyDrawer.SetFieldInfo(this, field);
      }
      
      QuantumEditorLog.Assert(attribute != null);
      QuantumEditorLog.Assert(attribute is DecoratingPropertyAttribute, $"Expected attribute to be of type {nameof(DecoratingPropertyAttribute)} but it's {attribute.GetType().FullName}");

      _isInitialized = true;
      NextDrawer = null;
      
      var isLastDrawer = false;
      var foundSelf    = false;

      var fieldAttributes = fieldInfo != null ? UnityInternal.ScriptAttributeUtility.GetFieldAttributes(fieldInfo) : null;

      if (fieldAttributes != null) {
        QuantumEditorLog.Assert(fieldAttributes.OrderBy(x => x.order).SequenceEqual(fieldAttributes), "Expected field attributes to be sorted");
        QuantumEditorLog.Assert(fieldAttributes.Count > 0);

        for (var i = 0; i < fieldAttributes.Count; ++i) {
          var fieldAttribute = fieldAttributes[i];

          var attributeDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForPropertyAndType(property, fieldAttribute.GetType());
          if (attributeDrawerType == null) {
            QuantumEditorLog.TraceInspector(GetLogMessage($"No drawer for {attributeDrawerType}"));
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

          QuantumEditorLog.Assert(attributeDrawerType.IsSubclassOf(typeof(PropertyDrawer)));

          if (!foundSelf && fieldAttribute.Equals(attribute)) {
            // self
            foundSelf    = true;
            isLastDrawer = true;
            QuantumEditorLog.TraceInspector(GetLogMessage($"Found self at {i} ({this})"));
            continue;
          }

          isLastDrawer = false;
        }
      }

      if (NextDrawer == null && isLastDrawer && fieldInfo != null) {
        // try creating type drawer instead
        var fieldType      = fieldInfo.FieldType;
        if (property.IsArrayElement()) {
          fieldType = fieldType.GetUnityLeafType();
        }
        
        var typeDrawerType = UnityInternal.ScriptAttributeUtility.GetDrawerTypeForPropertyAndType(property, fieldType);
        if (typeDrawerType != null) {
          var drawer = (PropertyDrawer)Activator.CreateInstance(typeDrawerType);
          UnityInternal.PropertyDrawer.SetFieldInfo(drawer, fieldInfo);
          QuantumEditorLog.TraceInspector(GetLogMessage($"Found final drawer is type drawer ({drawer})"));
          NextDrawer = drawer;
        }
      }

      if (isLastDrawer) {
        _isLastDrawer = true;
      }

      return true;
    }

    internal void InitInjected(PropertyDrawer next) {
      _isInitialized = true;
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

    private string GetLogMessage(string message) {
      return $"[{GetType().FullName}] [{GetHashCode():X8}] [{fieldInfo?.DeclaringType?.Name}.{fieldInfo?.Name}] {message}";
    }
  }
}

#endregion


#region DisplayAsEnumAttributeDrawer.cs

namespace Quantum.Editor {
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
            QuantumEditorLog.ErrorInspector($"Can't get enum type for {property.propertyPath}: non-SerializedProperty checks only work for top-level properties");
          } else {
            try {
              getter = objType.CreateGetter<Type>(attr.EnumTypeMemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            } catch (Exception e) {
              QuantumEditorLog.ErrorInspector($"Can't get enum type for {property.propertyPath}: unable to create getter for {attr.EnumTypeMemberName} with exception {e}");
            }
          }
      
          _cachedGetters.Add((objType, attr.EnumTypeMemberName), getter);
        }
      
        enumType = getter(property.serializedObject.targetObject);
      }

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out var valueRect)) {
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

namespace Quantum.Editor {
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
    
#if ODIN_INSPECTOR && !QUANTUM_ODIN_DISABLED
    [QuantumOdinAttributeConverter]
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

namespace Quantum.Editor {
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
          QuantumEditorLog.ErrorInspector($"Can't check condition for {compareProperty.propertyPath}: unsupported property type {compareProperty.propertyType}");
          return true;
      }
    }
    
    private static bool? CheckGetter(DoIfAttributeBase doIf, SerializedObject serializedObject, int depth, string referencePath) {
      var objType = serializedObject.targetObject.GetType();
      if (!_cachedGetters.TryGetValue((objType, doIf.ConditionMember), out var getter)) {
        // maybe this is a top-level property then and we can use reflection?
        if (depth != 0) {
          if (doIf.ErrorOnConditionMemberNotFound) {
            QuantumEditorLog.ErrorInspector($"Can't check condition for {referencePath}: non-SerializedProperty checks only work for top-level properties (depth:{depth}, conditionMember:{doIf.ConditionMember})");
          }
        } else {
          try {
            getter = objType.CreateGetter(doIf.ConditionMember, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
          } catch (Exception e) {
            if (doIf.ErrorOnConditionMemberNotFound) {
              QuantumEditorLog.ErrorInspector($"Can't check condition for {referencePath}: unable to create getter for {doIf.ConditionMember} with exception {e}");
            }
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

namespace Quantum.Editor {
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

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(DrawInlineAttribute))]
  [QuantumPropertyDrawerMeta(HasFoldout = false)]
  internal partial class DrawInlineAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      EditorGUI.BeginProperty(position, label, property);
      
      foreach (var childProperty in property.GetChildren()) {
        position.height = QuantumEditorGUI.GetPropertyHeight(childProperty);
        EditorGUI.PropertyField(position, childProperty, true);
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
      }

      EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      float height = 0f;

      foreach (var childProperty in property.GetChildren()) {
        height += QuantumEditorGUI.GetPropertyHeight(childProperty) + EditorGUIUtility.standardVerticalSpacing;
      }

      height -= EditorGUIUtility.standardVerticalSpacing;
      return height;
    }
  }
}

#endregion


#region ErrorIfAttributeDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class ErrorIfAttributeDrawer : MessageIfDrawerBase {
    private new ErrorIfAttribute Attribute => (ErrorIfAttribute)attribute;

    protected override bool        IsBox          => Attribute.AsBox;
    protected override string      Message        => Attribute.Message;
    protected override MessageType MessageType    => MessageType.Error;
    override protected Color       InlineBoxColor => QuantumEditorSkin.ErrorInlineBoxColor;
    protected override Texture     MessageIcon    => QuantumEditorSkin.ErrorIcon;
  }
  
  [CustomPropertyDrawer(typeof(ErrorIfAttribute))]
  [RedirectCustomPropertyDrawer(typeof(ErrorIfAttribute), typeof(ErrorIfAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}


#endregion


#region ExpandableEnumAttributeDrawer.cs

namespace Quantum.Editor {
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

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
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
        var foldoutRect = new Rect(valueRect) { width = QuantumEditorGUI.FoldoutWidth };
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
          using (new QuantumEditorGUI.EnabledScope(true)) {
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
              buttonRect      =  QuantumEditorGUI.GetInlineHelpButtonRect(rowRect);
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
              var helpContent = QuantumCodeDoc.FindEntry(_enumDrawer.Fields[i], false);
              if (helpContent != null) {
                var helpPath = GetHelpPath(property, _enumDrawer.Fields[i]);
                
                var wasHelpExpanded = QuantumEditorGUI.IsHelpExpanded(this, helpPath);
                if (wasHelpExpanded) {
                  var helpSize = QuantumEditorGUI.GetInlineBoxSize(helpContent);
                  var helpRect = rowRect;
                  helpRect.y      += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                  helpRect.height =  helpSize.y;
                  
                  rowRect.y += helpSize.y;
                  
                  QuantumEditorGUI.DrawInlineBoxUnderProperty(helpContent, helpRect, QuantumEditorSkin.HelpInlineBoxColor, true);
                }
                
                buttonRect.x += buttonRect.width;
                if (QuantumEditorGUI.DrawInlineHelpButton(buttonRect, wasHelpExpanded, doButton: true, doIcon: true)) {
                  QuantumEditorGUI.SetHelpExpanded(this, helpPath, !wasHelpExpanded);
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
            if (QuantumEditorGUI.IsHelpExpanded(this, GetHelpPath(property, field))) {
              var helpContent = QuantumCodeDoc.FindEntry(field, false);
              if (helpContent != null) {
                height += QuantumEditorGUI.GetInlineBoxSize(helpContent).y;
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

namespace Quantum.Editor {
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
            QuantumEditorLog.ErrorInspector($"Unable to find method {attribute.TargetMethod} on type {targetObjectType}");
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

namespace Quantum.Editor {
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

namespace Quantum.Editor {
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  //[CustomPropertyDrawer(typeof(InlineHelpAttribute))]
  internal partial class InlineHelpAttributeDrawer : DecoratingPropertyAttributeDrawer, INonApplicableOnArrayElements {
    bool       _initialized;
    GUIContent _helpContent;
    GUIContent _labelContent;
    
    protected new InlineHelpAttribute attribute => (InlineHelpAttribute)base.attribute; 

    
    protected override float GetPropertyHeightInternal(SerializedProperty property, GUIContent label) {
      
      var height = base.GetPropertyHeightInternal(property, label);
      if (height <= 0) {
        return height;
      }
      
      EnsureContentInitialized(property);
      
      if (QuantumEditorGUI.IsHelpExpanded(this, property.GetHashCodeForPropertyPathWithoutArrayIndex())) {
        if (_helpContent != null) {
          height += QuantumEditorGUI.GetInlineBoxSize(_helpContent).y;
        }
      }

      return height;
    }

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      if (position.height <= 0 || _helpContent == null) {
        // ignore
        base.OnGUIInternal(position, property, label);
        return;
      }
      
      QuantumEditorLog.Assert(_initialized);
      
      var nextDrawer = GetNextDrawer(property);
      var hasFoldout = HasFoldout(nextDrawer, property);

      using (new QuantumEditorGUI.GUIContentScope(label)) {
        var (wasExpanded, buttonRect) = DrawInlineHelpBeforeProperty(label, _helpContent, position, property.GetHashCodeForPropertyPathWithoutArrayIndex(), EditorGUI.indentLevel, hasFoldout, this);

        var propertyRect = position;
        if (wasExpanded) {
          propertyRect.height -= QuantumEditorGUI.GetInlineBoxSize(_helpContent).y;
        }
        base.OnGUIInternal(propertyRect, property, label);
        
        DrawInlineHelpAfterProperty(buttonRect, wasExpanded, _helpContent, position);
      }
    }
    
    private void EnsureContentInitialized(SerializedProperty property) {
      if (_initialized) {
        return;
      }

      _initialized = true;
      if (fieldInfo == null) {
        return;
      }
      
      _helpContent = QuantumCodeDoc.FindEntry(fieldInfo, attribute.ShowTypeHelp);
    }
    
    private bool HasFoldout(PropertyDrawer nextDrawer, SerializedProperty property) {
      var drawerMeta = nextDrawer?.GetType().GetCustomAttribute<QuantumPropertyDrawerMetaAttribute>();
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
    
    public static (bool expanded, Rect buttonRect) DrawInlineHelpBeforeProperty(GUIContent label, GUIContent helpContent, Rect propertyRect, int pathHash, int depth, bool hasFoldout, object context, bool drawHelp = false) {
      
      if (label != null) {
        if (!string.IsNullOrEmpty(label.tooltip)) {
          label.tooltip += "\n\n";
        }
        label.tooltip += helpContent.tooltip;
      }

      if (propertyRect.width > 1 && propertyRect.height > 1) {
        var buttonRect = QuantumEditorGUI.GetInlineHelpButtonRect(propertyRect, hasFoldout);

        if (depth == 0 && hasFoldout) {
          buttonRect.x = 16;
          if (label != null) {
            label.text = "    " + label.text;
          }
        }

        var wasExpanded = QuantumEditorGUI.IsHelpExpanded(context, pathHash);
        
        if (QuantumEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded, doButton: true, doIcon: false)) {
          QuantumEditorGUI.SetHelpExpanded(context, pathHash, !wasExpanded);
        }

        return (wasExpanded, buttonRect);
      }

      return default;
    }
    
    public static void DrawInlineHelpAfterProperty(Rect buttonRect, bool wasExpanded, GUIContent helpContent, Rect propertyRect) {

      if (buttonRect.width <= 0 && buttonRect.height <= 0) {
        return;
      }

      using (new QuantumEditorGUI.EnabledScope(true)) {
        QuantumEditorGUI.DrawInlineHelpButton(buttonRect, wasExpanded, doButton: false, doIcon: true);
      }

      if (!wasExpanded) {
        return;
      }
      
      QuantumEditorGUI.DrawInlineBoxUnderProperty(helpContent, propertyRect, QuantumEditorSkin.HelpInlineBoxColor, true);
    }
  }
  
  
  [CustomPropertyDrawer(typeof(InlineHelpAttribute))]
  [RedirectCustomPropertyDrawer(typeof(InlineHelpAttribute), typeof(InlineHelpAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}

#endregion


#region INonApplicableOnArrayElements.cs

namespace Quantum.Editor {
  interface INonApplicableOnArrayElements {
  }
}

#endregion


#region LayerAttributeDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(LayerAttribute))]
  internal class LayerAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect p, SerializedProperty prop, GUIContent label) {
      EditorGUI.BeginChangeCheck();

      int value;

      using (new QuantumEditorGUI.PropertyScope(p, label, prop))
      using (new QuantumEditorGUI.ShowMixedValueScope(prop.hasMultipleDifferentValues)) {
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

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  internal partial class LayerMatrixAttributeDrawer : PropertyDrawerWithErrorHandling, INonApplicableOnArrayElements {

    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out var valueRect)) {
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

      private readonly GUIContent _label;
      private readonly int _numLayers;
      private readonly float _labelWidth;
      
      private readonly UnityInternal.LayerMatrixGUI.GetValueFunc _getter;
      private readonly UnityInternal.LayerMatrixGUI.SetValueFunc _setter;
      
      public LayerMatrixPopup(string label, UnityInternal.LayerMatrixGUI.GetValueFunc getter, UnityInternal.LayerMatrixGUI.SetValueFunc setter) {
        _label      = new GUIContent(label);
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
        
        UnityInternal.LayerMatrixGUI.Draw(_label, _getter, _setter);

        GUILayout.EndArea();
      }

      public override Vector2 GetWindowSize() {
        int   matrixWidth = checkboxSize * _numLayers;
        float width       = matrixWidth + _labelWidth + margin * 2;
        float height      = matrixWidth + _labelWidth + 15 + QuantumEditorGUI.GetLinesHeight(3);
        return new Vector2(Mathf.Max(width, 350), height);
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

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(MaxStringByteCountAttribute))]
  internal class MaxStringByteCountAttributeDrawer : PropertyDrawerWithErrorHandling {
    
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attribute = (MaxStringByteCountAttribute)this.attribute;
      
      var encoding  = System.Text.Encoding.GetEncoding(attribute.Encoding);
      var byteCount = encoding.GetByteCount(property.stringValue);

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        QuantumEditorGUI.ForwardPropertyField(position, property, label, true);
      }

      QuantumEditorGUI.Overlay(position, $"({byteCount} B)");
      if (byteCount > attribute.ByteCount) {
        QuantumEditorGUI.Decorate(position, $"{attribute.Encoding} string max size ({attribute.ByteCount} B) exceeded: {byteCount} B", MessageType.Error, hasLabel: true);
      }
    }
  }
}

#endregion


#region MessageIfDrawerBase.cs

namespace Quantum.Editor {
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

          QuantumEditorGUI.AppendTooltip(MessageContent.text, ref label);
          
          base.OnGUIInternal(position, property, label);
          
          QuantumEditorGUI.Decorate(decorateRect, MessageContent.text, MessageType);
        } else {

          position = QuantumEditorGUI.DrawInlineBoxUnderProperty(MessageContent, position, InlineBoxColor);
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
      
      return QuantumEditorGUI.GetInlineBoxSize(MessageContent).y;
    }
  }
}

#endregion


#region PropertyDrawerForArrayWorkaround.cs

//#define QUANTUM_EDITOR_TRACE
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

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
      
      // this handler is going to have a drawer eventually,
      // but now we need to make sure it looks like it has drawers before we can actually
      // inject them
      _handler.m_PropertyDrawers ??= new List<PropertyDrawer>() { new DummyPropertyDrawer() };
    }
    
    public override float GetHeight() {
      if (_initialized) {
        return 0;
      }

      _initialized = true;
      if (!_attributeToDrawer.TryGetValue(attribute.GetType(), out var drawerType)) {
        QuantumEditorLog.ErrorInspector($"No drawer for {attribute.GetType()}");
      } else if (_handler.decoratorDrawers?.Contains(this) != true) {
        QuantumEditorLog.Warn($"Unable to forward to {drawerType}.");
      } else {
        var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);
        UnityInternal.PropertyDrawer.SetAttribute(drawer, attribute);

        QuantumEditorLog.Assert(_handler.m_PropertyDrawers != null, "_handler.m_PropertyDrawers != null");

        var propertyDrawers = _handler.m_PropertyDrawers;
        if (propertyDrawers.Count > 0 && propertyDrawers[0] is DummyPropertyDrawer) {
          propertyDrawers.RemoveAt(0);
        }
        int i = 0;
        for (; i < propertyDrawers.Count; ++i) {
          if (propertyDrawers[i].attribute == null) {
            break;
          }
          if (propertyDrawers[i].attribute.order > attribute.order) {
            // perfect spot!
            break;
          }
          if (propertyDrawers[i].attribute.order == attribute.order) {
            // this is tricky; ideally we want to insert exactly in the same order as ScriptAttributeUtility.GetFieldAttributes
            // would return, but the field is not available at the moment; so the next best thing is putting the workaround ahead
            // unless we've found another workaround
            if (!_attributeToDrawer.ContainsKey(propertyDrawers[i].attribute.GetType())) {
              break;
            }
          }
        }
          
        QuantumEditorLog.Trace($"Inserting {drawerType} at {i}");
        _handler.m_PropertyDrawers.Insert(i, drawer);
      }

      return 0;
    }

    public static Type GetDrawerType(Type attributeDrawerType) {
      return _attributeToDrawer[attributeDrawerType];
    }

    class DummyPropertyDrawer : PropertyDrawer {

      static bool _errorReported = false;
      
      public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        if (!_errorReported) {
          _errorReported = true;
          QuantumEditorLog.WarnInspector($"Drawers for property {property.propertyPath} failed to be injected properly. This may happen if property drawers are created in a non-standard way.");
        }
        return EditorGUI.GetPropertyHeight(property, label);
      }
    }
  }
}

#endregion


#region PropertyDrawerWithErrorHandling.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  internal abstract class PropertyDrawerWithErrorHandling : PropertyDrawer {
    private SerializedProperty _currentProperty;

    private readonly Dictionary<string, Entry> _errors = new();
    private          bool                      _hadError;
    private          string                    _info;

    public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      QuantumEditorLog.Assert(_currentProperty == null);

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
      QuantumEditorGUI.Decorate(iconPosition, decoration.Item1, decoration.Item2, hasLabel, drawButton: drawButton, drawBorder: decoration.Item3);
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


#region RangeExAttributeDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(RangeExAttribute))]
  internal partial class RangeExAttributeDrawer : PropertyDrawerWithErrorHandling {

    const float FieldWidth     = 100.0f;
    const float Spacing        = 5.0f;
    const float SliderOffset   = 2.0f;
    const float MinSliderWidth = 40.0f;

    partial void GetFloatValue(SerializedProperty property, ref float? floatValue);
    partial void GetIntValue(SerializedProperty property, ref int? intValue);
    partial void ApplyFloatValue(SerializedProperty property, float floatValue);
    partial void ApplyIntValue(SerializedProperty property, int intValue);
    partial void DrawFloatValue(SerializedProperty property, Rect position, GUIContent label, ref float floatValue);
    partial void DrawIntValue(SerializedProperty property, Rect position, GUIContent label, ref int intValue);


    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attrib = (RangeExAttribute)this.attribute;
      var min    = attrib.Min;
      var max    = attrib.Max;

      int?   intValue   = null;
      float? floatValue = null;

      if (property.propertyType == SerializedPropertyType.Float) {
        floatValue = property.floatValue;
      } else if (property.propertyType == SerializedPropertyType.Integer) {
        intValue = property.intValue;
      } else {
        GetFloatValue(property, ref floatValue);
        if (!floatValue.HasValue) {
          GetIntValue(property, ref intValue);
          if (!intValue.HasValue) {
            EditorGUI.LabelField(position, label.text, "Use RangeEx with float or int.");
            return;
          }
        }
      }
      
      Debug.Assert(floatValue.HasValue || intValue.HasValue);
      
      EditorGUI.BeginChangeCheck();

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        if (attrib.UseSlider) {

          // slider offset is applied to look like the built-in RangeDrawer
          var sliderRect = new Rect(position) {
            xMin = position.xMin + EditorGUIUtility.labelWidth + SliderOffset,
            xMax = position.xMax - FieldWidth - Spacing
          };

          using (new QuantumEditorGUI.LabelWidthScope(position.width - FieldWidth)) {
            if (floatValue.HasValue) {
              if (sliderRect.width > MinSliderWidth) {
                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                  floatValue = GUI.HorizontalSlider(sliderRect, floatValue.Value, (float)min, (float)max);
                }
              }

              floatValue = DrawValue(property, position, label, floatValue.Value);
            } else {
              if (sliderRect.width > MinSliderWidth) {
                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel)) {
                  intValue = Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect, intValue.Value, (float)min, (float)max));
                }
              }

              intValue = DrawValue(property, position, label, intValue.Value);
            }
          }
        } else {
          if (floatValue.HasValue) {
            floatValue = DrawValue(property, position, label, floatValue.Value);
          } else {
            intValue = DrawValue(property, position, label, intValue.Value);
          }
        }
      }

      if (EditorGUI.EndChangeCheck()) {
        if (floatValue.HasValue) {
          floatValue = Clamp(floatValue.Value, attrib);
        } else {
          Debug.Assert(floatValue != null);
          intValue = Clamp(intValue.Value, attrib);
        }

        if (property.propertyType == SerializedPropertyType.Float) {
          Debug.Assert(floatValue != null);
          property.floatValue = floatValue.Value;
        } else if (property.propertyType == SerializedPropertyType.Integer) {
          Debug.Assert(intValue != null);
          property.intValue = intValue.Value;
        } else if (floatValue.HasValue) {
          ApplyFloatValue(property, floatValue.Value);
        } else {
          ApplyIntValue(property, intValue.Value);
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
    
    float DrawValue(SerializedProperty property, Rect position, GUIContent label, float floatValue) {
      if (property.propertyType == SerializedPropertyType.Float) {
        return EditorGUI.FloatField(position, label, floatValue);
      } else {
        DrawFloatValue(property, position, label, ref floatValue);
        return floatValue;
      }
    }
    
    int DrawValue(SerializedProperty property, Rect position, GUIContent label, int intValue) {
      if (property.propertyType == SerializedPropertyType.Integer) {
        return EditorGUI.IntField(position, label, intValue);
      } else {
        DrawIntValue(property, position, label, ref intValue);
        return intValue;
      }
    }
  }
}

#endregion


#region ReadOnlyAttributeDrawer.cs

namespace Quantum.Editor {
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

namespace Quantum.Editor {
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

      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
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

namespace Quantum.Editor {
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

      var  helpButtonRect  = QuantumEditorGUI.GetInlineHelpButtonRect(position, false);
      bool wasHelpExpanded = _helpContent != null && QuantumEditorGUI.IsHelpExpanded(this, property.GetHashCodeForPropertyPathWithoutArrayIndex());
      
      if (wasHelpExpanded) {
        position = QuantumEditorGUI.DrawInlineBoxUnderProperty(_helpContent, position, QuantumEditorSkin.HelpInlineBoxColor);
      }

      if (_helpContent != null) {
        using (new QuantumEditorGUI.EnabledScope(true)) {
          if (QuantumEditorGUI.DrawInlineHelpButton(helpButtonRect, wasHelpExpanded, true, false)) {
            QuantumEditorGUI.SetHelpExpanded(this, property.GetHashCodeForPropertyPathWithoutArrayIndex(), !wasHelpExpanded);
          }
        }
      }
      
      if (attribute.Style == ScriptHeaderStyle.Unity) {
        EditorGUI.PropertyField(position, property, label);
      } else {
        using (new QuantumEditorGUI.EnabledScope(true)) {
          if (attribute.BackColor != ScriptHeaderBackColor.None) {
            QuantumEditorGUI.DrawScriptHeaderBackground(position, QuantumEditorSkin.GetScriptHeaderColor(attribute.BackColor));
          }

          var labelPosition = QuantumEditorSkin.ScriptHeaderLabelStyle.margin.Remove(position);
          EditorGUIUtility.AddCursorRect(labelPosition, MouseCursor.Link);
          EditorGUI.LabelField(labelPosition, _headerContent, QuantumEditorSkin.ScriptHeaderLabelStyle);

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

          QuantumEditorGUI.DrawScriptHeaderIcon(position);
        }
      }
      
      if (_helpContent != null) {
        using (new QuantumEditorGUI.EnabledScope(true)) {
          // paint over what the inspector has drawn
          QuantumEditorGUI.DrawInlineHelpButton(helpButtonRect, wasHelpExpanded, false, true);
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

      if (QuantumEditorGUI.IsHelpExpanded(this, property.GetHashCodeForPropertyPathWithoutArrayIndex()) && _helpContent != null) {
        height += QuantumEditorGUI.GetInlineBoxSize(_helpContent).y;
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
      _helpContent   = QuantumCodeDoc.FindEntry(type);
    }
  }
}

#endregion


#region SerializableTypeDrawer.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Scripting;

  [CustomPropertyDrawer(typeof(SerializableType<>))]
  [CustomPropertyDrawer(typeof(SerializableType))]
  [CustomPropertyDrawer(typeof(SerializableTypeAttribute))]
  internal class SerializableTypeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      
      var attr = (SerializableTypeAttribute)attribute;
      
      var baseType     = typeof(object);
      var leafType     = fieldInfo.FieldType.GetUnityLeafType();
      if (leafType.IsGenericType && leafType.GetGenericTypeDefinition() == typeof(SerializableType<>)) {
        baseType = leafType.GetGenericArguments()[0];
      }
      if (attr?.BaseType != null) {
        baseType = attr.BaseType;
      }

      position = EditorGUI.PrefixLabel(position, label);
      
      var (content, msgType, msg) = GetTypeContent(property, attr?.WarnIfNoPreserveAttribute == true, out var valueProperty);
      if (msgType == MessageType.Warning) {
        SetWarning(msg);
      } else if (msgType == MessageType.Error) {
        SetError(msg);
      }
      
      if (EditorGUI.DropdownButton(position, new GUIContent(content), FocusType.Keyboard)) {
        ClearError();
        QuantumEditorGUI.DisplayTypePickerMenu(position, baseType, t => {
          string typeName = string.Empty;
          if (t != null) {
            typeName = attr?.UseFullAssemblyQualifiedName == false ? SerializableType.GetShortAssemblyQualifiedName(t) : t.AssemblyQualifiedName;
          }
          
          valueProperty.stringValue = typeName;
          valueProperty.serializedObject.ApplyModifiedProperties();
        });
      }
    }
    
        
    public static (string, MessageType, string) GetTypeContent(SerializedProperty property, bool requirePreserveAttribute, out SerializedProperty valueProperty) {
      if (property.propertyType == SerializedPropertyType.String) {
        valueProperty = property;
      } else {
        QuantumEditorLog.Assert(property.propertyType == SerializedPropertyType.Generic);
        valueProperty = property.FindPropertyRelativeOrThrow(nameof(SerializableType.AssemblyQualifiedName));
      }

      var assemblyQualifiedName = valueProperty.stringValue;
      if (string.IsNullOrEmpty(assemblyQualifiedName)) {
        return ("[None]", MessageType.None, string.Empty);
      }

      try {
        var type = Type.GetType(assemblyQualifiedName, true);

        if (requirePreserveAttribute) {
          if (!type.IsDefined(typeof(PreserveAttribute), false)) {
            return (type.FullName, MessageType.Warning, $"Please mark {type.FullName} with [Preserve] attribute to prevent it from being stripped from the build.");
          }
        }

        return (type.FullName, MessageType.None, string.Empty);
      } catch (Exception e) {
        return (assemblyQualifiedName, MessageType.Error, e.ToString());
      }
    }
  }
}

#endregion


#region SerializeReferenceTypePickerAttributeDrawer.cs

namespace Quantum.Editor {
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
        pickerRect.height = EditorGUIUtility.singleLineHeight;
      } else {
        pickerRect = EditorGUI.PrefixLabel(new Rect(position) { height = EditorGUIUtility.singleLineHeight }, QuantumEditorGUI.WhitespaceContent);
      }
      
      object instance = property.managedReferenceValue;
      var instanceType = instance?.GetType();
      
      if (EditorGUI.DropdownButton(pickerRect, new GUIContent(instanceType?.FullName ?? NullContent), FocusType.Keyboard)) {

        var types = attribute.Types;
        if (!types.Any()) {
          types = new[] { fieldInfo.FieldType.GetUnityLeafType() };
        }
        
        QuantumEditorGUI.DisplayTypePickerMenu(pickerRect, types, 
          t => {
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
          }, 
          noneOptionLabel: NullContent, 
          selectedType: instanceType, 
          flags: (attribute.GroupTypesByNamespace ? QuantumEditorGUIDisplayTypePickerMenuFlags.GroupByNamespace : 0) | (attribute.ShowFullName ? QuantumEditorGUIDisplayTypePickerMenuFlags.ShowFullName : 0));
      }
      
      base.OnGUIInternal(position, property, label);
    }
  }
}

#endregion


#region ToggleLeftAttributeDrawer.cs

namespace Quantum.Editor {
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

namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnitAttribute))]
  [QuantumPropertyDrawerMeta(HandlesUnits = true)]
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
        var meta = nextDrawer.GetType().GetCustomAttribute<QuantumPropertyDrawerMetaAttribute>();
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
            QuantumEditorGUI.Overlay(pos, label);
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
          QuantumEditorGUI.Overlay(pos, label);
          pos.x += pos.width;
          QuantumEditorGUI.Overlay(pos, label);
          if (propertyType == SerializedPropertyType.Vector3) {
            pos.x += pos.width;
            QuantumEditorGUI.Overlay(pos, label);
          }

          break;
        }
        case SerializedPropertyType.Vector4:
          if (isExpanded) {
            Rect pos = position;
            pos.yMin   = pos.yMax - 4 * EditorGUIUtility.singleLineHeight - 3 * EditorGUIUtility.standardVerticalSpacing;
            pos.height = EditorGUIUtility.singleLineHeight;
            for (int i = 0; i < 4; ++i) {
              QuantumEditorGUI.Overlay(pos, label);
              pos.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
          }

          break;
        default: {
          var pos = position;
          pos.height = EditorGUIUtility.singleLineHeight;
          QuantumEditorGUI.Overlay(pos, label);
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


#region UnityAddressablesRuntimeKeyAttributeDrawer.cs

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;
  using Object = UnityEngine.Object;

  [CustomPropertyDrawer(typeof(UnityAddressablesRuntimeKeyAttribute))]
  internal class UnityAddressablesRuntimeKeyAttributeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attrib = (UnityAddressablesRuntimeKeyAttribute)attribute;

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
        position.width -= 40;
        EditorGUI.PropertyField(position, property, GUIContent.none, false);
        Object asset = null;

        var runtimeKey = property.stringValue;
        
        if (!string.IsNullOrEmpty(runtimeKey)) {
          if (!QuantumAddressablesUtils.TryParseAddress(runtimeKey, out var _, out var _)) {
            SetError($"Not a valid address: {runtimeKey}");
          } else {
            asset = QuantumAddressablesUtils.LoadEditorInstance(runtimeKey);
            if (asset == null) {
              SetError($"Asset not found for runtime key: {runtimeKey}");
            }
          }
        }
        
        using (new QuantumEditorGUI.EnabledScope(asset)) {
          position.x     += position.width;
          position.width =  40;
          if (GUI.Button(position, "Ping")) {
            EditorGUIUtility.PingObject(asset);
          }
        }
      }
    }
  }
}
#endif

#endregion


#region UnityAssetGuidAttributeDrawer.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnityAssetGuidAttribute))]
  [QuantumPropertyDrawerMeta(HasFoldout = false)]
  internal class UnityAssetGuidAttributeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      string guid;
      position.width -= 40;

      if (property.propertyType == SerializedPropertyType.Generic) {
        guid = DrawMangledRawGuid(position, property, label);
      } else if (property.propertyType == SerializedPropertyType.String) {
        using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
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

      using (new QuantumEditorGUI.EnabledScope(!string.IsNullOrEmpty(assetPath))) {
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

        using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
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

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  [CustomPropertyDrawer(typeof(UnityResourcePathAttribute))]
  internal class UnityResourcePathAttributeDrawer : PropertyDrawerWithErrorHandling {
    protected override void OnGUIInternal(Rect position, SerializedProperty property, GUIContent label) {
      var attrib = (UnityResourcePathAttribute)attribute;

      using (new QuantumEditorGUI.PropertyScopeWithPrefixLabel(position, label, property, out position)) {
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
        
        using (new QuantumEditorGUI.EnabledScope(asset)) {
          position.x     += position.width;
          position.width =  40;
          if (GUI.Button(position, "Ping")) {
            EditorGUIUtility.PingObject(asset);
          }
        }
      }
    }
  }
}

#endregion


#region WarnIfAttributeDrawer.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  partial class WarnIfAttributeDrawer : MessageIfDrawerBase {
    private new WarnIfAttribute Attribute   => (WarnIfAttribute)attribute;

    protected override bool        IsBox          => Attribute.AsBox;
    protected override string      Message        => Attribute.Message;
    protected override MessageType MessageType    => MessageType.Warning;
    protected override Color       InlineBoxColor => QuantumEditorSkin.WarningInlineBoxColor;
    protected override Texture     MessageIcon    => QuantumEditorSkin.WarningIcon;
  }
  
  [CustomPropertyDrawer(typeof(WarnIfAttribute))]
  [RedirectCustomPropertyDrawer(typeof(WarnIfAttribute), typeof(WarnIfAttributeDrawer))]
  partial class PropertyDrawerForArrayWorkaround {
  }
}


#endregion



#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditor.Common.Partial.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;

  partial class ReflectionUtils {
    public static string GetCSharpTypeName(this Type type, string suffix = null, bool includeNamespace = true, bool includeGenerics = true, bool useGenericNames = false) {
      string fullName;

      if (includeNamespace) {
        fullName = type.FullName;

        if (fullName == null) {
          if (type.IsGenericParameter)
            fullName = type.Name;
          else
            fullName = type.Namespace + "." + type.Name;
        }
      } else
        fullName = type.Name;

      if (useGenericNames && type.IsConstructedGenericType) type = type.GetGenericTypeDefinition();

      string result;

      if (type.IsGenericType) {
        var parentType = fullName.Split('`').First();

        if (includeGenerics) {
          var genericArguments = string.Join(", ", type.GetGenericArguments().Select(x => GetCSharpTypeName(x)));
          result = $"{parentType}{suffix ?? ""}<{genericArguments}>";
        } else
          result = $"{parentType}{suffix ?? ""}";
      } else
        result = fullName + (suffix ?? "");

      return result.Replace('+', '.');
    }
  }

  public static partial class QuantumEditorHubSrpTools {
    static partial void BeforeOpenSceneUser() {
      QuantumUnityDBUtilities.RefreshGlobalDB();
    }
  }

}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorAutoBaker.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditor.Build.Reporting;
  using UnityEditor.SceneManagement;
  using UnityEngine.SceneManagement;
  using BuildTrigger = QuantumMapDataBaker.BuildTrigger;

  [InitializeOnLoad]
  public class QuantumEditorAutoBaker : IProcessSceneWithReport {
    const string BakeMapDataAndNavMeshSuffix      = "MapData";
    const string BakeWithNavMeshSuffix            = "MapData with NavMesh";
    const string BakeWithUnityNavMeshImportSuffix = "MapData with Unity NavMesh Import";

    [MenuItem("Tools/Quantum/Bake/" + BakeMapDataAndNavMeshSuffix, false, (int)QuantumEditorMenuPriority.Bake)]
    public static void BakeCurrentScene_MapData() => BakeLoadedScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
    [MenuItem("Tools/Quantum/Bake/" + BakeWithNavMeshSuffix, false, (int)QuantumEditorMenuPriority.Bake)]
    public static void BakeCurrentScene_NavMesh() => BakeLoadedScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh);
    [MenuItem("Tools/Quantum/Bake/" + BakeWithUnityNavMeshImportSuffix, false, (int)QuantumEditorMenuPriority.Bake)]
    public static void BakeCurrentScene_ImportNavMesh() => BakeLoadedScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh | QuantumMapDataBakeFlags.ImportUnityNavMesh | QuantumMapDataBakeFlags.BakeUnityNavMesh);

    [MenuItem("Tools/Quantum/Bake/All Scenes/" + BakeMapDataAndNavMeshSuffix, false, (int)QuantumEditorMenuPriority.Bake + 11)]
    public static void BakeAllScenes_MapData() => BakeAllScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
    [MenuItem("Tools/Quantum/Bake/All Scenes/" + BakeWithNavMeshSuffix, false, (int)QuantumEditorMenuPriority.Bake + 11)]
    public static void BakeAllScenes_NavMesh() => BakeAllScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh);
    [MenuItem("Tools/Quantum/Bake/All Scenes/" + BakeWithUnityNavMeshImportSuffix, false, (int)QuantumEditorMenuPriority.Bake + 11)]
    public static void BakeAllScenes_ImportNavMesh() => BakeAllScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh | QuantumMapDataBakeFlags.ImportUnityNavMesh | QuantumMapDataBakeFlags.BakeUnityNavMesh);

    [MenuItem("Tools/Quantum/Bake/All Enabled Scenes/" + BakeMapDataAndNavMeshSuffix, false, (int)QuantumEditorMenuPriority.Bake + 12)]
    public static void BakeEnabledScenes_MapData() => BakeEnabledScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB);
    [MenuItem("Tools/Quantum/Bake/All Enabled Scenes/" + BakeWithNavMeshSuffix, false, (int)QuantumEditorMenuPriority.Bake + 12)]
    public static void BakeEnabledScenes_NavMesh() => BakeEnabledScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh);
    [MenuItem("Tools/Quantum/Bake/All Enabled Scenes/" + BakeWithUnityNavMeshImportSuffix, false, (int)QuantumEditorMenuPriority.Bake + 12)]
    public static void BakeEnabledScenes_ImportNavMesh() => BakeEnabledScenes(QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB | QuantumMapDataBakeFlags.BakeNavMesh | QuantumMapDataBakeFlags.ImportUnityNavMesh | QuantumMapDataBakeFlags.BakeUnityNavMesh);

    private static void BakeLoadedScenes(QuantumMapDataBakeFlags flags) {
      for (int i = 0; i < EditorSceneManager.sceneCount; ++i) {
        BakeScene(EditorSceneManager.GetSceneAt(i), flags);
      }
    }

    private static void BakeAllScenes(QuantumMapDataBakeFlags flags) {
      var scenes = AssetDatabase.FindAssets("t:scene")
        .Select(x => AssetDatabase.GUIDToAssetPath(x));
      BakeScenes(scenes, flags);
    }

    private static void BakeEnabledScenes(QuantumMapDataBakeFlags flags) {
      var enabledScenes = EditorBuildSettings.scenes
          .Where(x => x.enabled)
          .Select(x => x.path);
      BakeScenes(enabledScenes, flags);
    }


    static QuantumEditorAutoBaker() {
      EditorSceneManager.sceneSaving += OnSceneSaving;
      EditorApplication.playModeStateChanged += OnPlaymodeChange;
    }

    private static void OnPlaymodeChange(PlayModeStateChange change) {
      if (change != PlayModeStateChange.ExitingEditMode) {
        return;
      }
      for (int i = 0; i < EditorSceneManager.sceneCount; ++i) {
        AutoBakeMapData(EditorSceneManager.GetSceneAt(i), BuildTrigger.PlaymodeChange);
      }
    }

    private static void OnSceneSaving(Scene scene, string path) {
      AutoBakeMapData(scene, BuildTrigger.SceneSave);
    }

    private static void AutoBakeMapData(Scene scene, BuildTrigger buildTrigger) {
      if (!QuantumEditorSettings.TryGetGlobal(out var settings)) {
        return;
      }

      switch (buildTrigger) {
        case BuildTrigger.Build:
          BakeScene(scene, settings.AutoBuildOnBuild, buildTrigger);
          break;
        case BuildTrigger.SceneSave:
          BakeScene(scene, settings.AutoBuildOnSceneSave, buildTrigger);
          break;
        case BuildTrigger.PlaymodeChange:
          BakeScene(scene, settings.AutoBuildOnPlaymodeChanged, buildTrigger);
          break;
      }
    }

    private static void BakeScenes(IEnumerable<string> scenes, QuantumMapDataBakeFlags mode) {
      if (mode == QuantumMapDataBakeFlags.None)
        return;

      if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
        return;
      }

      var currentScenes = Enumerable.Range(0, EditorSceneManager.sceneCount)
        .Select(x => EditorSceneManager.GetSceneAt(x))
        .Where(x => x.IsValid())
        .Select(x => new { Path = x.path, IsLoaded = x.isLoaded })
        .ToList();

      // we don't want to generate db for each scene
      bool generateDB = mode.HasFlag(QuantumMapDataBakeFlags.GenerateAssetDB);
      mode &= ~QuantumMapDataBakeFlags.GenerateAssetDB;

      try {
        var mapDataAssets = AssetDatabase.FindAssets($"t:{nameof(Quantum.Map)}")
            .Select(x => AssetDatabase.GUIDToAssetPath(x))
            .Select(x => AssetDatabase.LoadAssetAtPath<Quantum.Map>(x));

        var lookup = scenes
          .ToLookup(x => Path.GetFileNameWithoutExtension(x));

        foreach (var mapData in mapDataAssets) {

          var path = lookup[mapData.Scene].FirstOrDefault();
          if (string.IsNullOrEmpty(path))
            continue;

          var id = mapData?.Identifier;

          try {
            QuantumEditorLog.LogImport($"Baking map {id} (scene: {path})");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid())
              continue;

            BakeScene(scene, mode);
            EditorSceneManager.SaveOpenScenes();
          } catch (System.Exception ex) {
            QuantumEditorLog.ErrorImport($"Error when baking map {id} (scene: {path}): {ex}");
          } 
        }
      } finally {
        var sceneLoadMode = OpenSceneMode.Single;
        foreach (var sceneInfo in currentScenes) {
          try {
            if (string.IsNullOrEmpty(sceneInfo.Path)) {
              continue;
            }
            var scene = EditorSceneManager.OpenScene(sceneInfo.Path, sceneLoadMode);
            if (scene.isLoaded && !sceneInfo.IsLoaded)
              EditorSceneManager.CloseScene(scene, false);
            sceneLoadMode = OpenSceneMode.Additive;
          } catch (System.Exception ex) {
            QuantumEditorLog.WarnImport($"Failed to restore scene: {sceneInfo.Path}: {ex}");
          }
        }
      }

      if (generateDB) {
        QuantumUnityDBUtilities.RefreshGlobalDB();
      }
    }

    private static void BakeScene(Scene scene, QuantumMapDataBakeFlags mode, BuildTrigger buildTrigger = BuildTrigger.Manual) {
      if (mode == QuantumMapDataBakeFlags.None)
        return;

      var mapsData = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<QuantumMapData>()).ToList();

      if (mapsData.Count == 1) {
        QuantumEditorLog.LogImport($"Auto baking {scene.path}");
        BakeMap(mapsData[0], mode, buildTrigger);
      } else if (mapsData.Count > 1) {
        QuantumEditorLog.ErrorImport($"There are multiple {nameof(QuantumMapData)} components on scene {scene.name}. This is not supported.");
      }

      AssetDatabase.Refresh();
      
      if (mode.HasFlag(QuantumMapDataBakeFlags.GenerateAssetDB)) {
        QuantumUnityDBUtilities.RefreshGlobalDB();
      }
    }

    public static void BakeMap(QuantumMapData data, QuantumMapDataBakeFlags buildFlags, BuildTrigger buildTrigger = BuildTrigger.Manual) {
      if (data.AssetRef == default)
        return;

#pragma warning disable CS0618 // Type or member is obsolete
      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.Obsolete_BakeMapData)) {
#pragma warning restore CS0618 // Type or member is obsolete
        QuantumMapDataBaker.BakeMapData(data, true, 
          bakeColliders: true, 
          bakePrototypes: true,
          bakeFlags: buildFlags,
          buildTrigger: buildTrigger);
      } else if (buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapPrototypes) || buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapColliders)) {
        QuantumMapDataBaker.BakeMapData(data, true,
          bakeColliders: buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapColliders),
          bakePrototypes: buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeMapPrototypes),
          bakeFlags: buildFlags,
          buildTrigger: buildTrigger);
      }

#if QUANTUM_ENABLE_AI && !QUANTUM_DISABLE_AI
      
      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeUnityNavMesh)) {
        foreach (var navmesh in data.GetComponentsInChildren<QuantumMapNavMeshUnity>()) {
          if (QuantumMapNavMeshUnityEditor.BakeUnityNavmesh(navmesh.gameObject)) {
            break;
          }
        }
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.ImportUnityNavMesh) || buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeNavMesh)) {
        QuantumMapNavMeshUnityEditor.UpdateDefaultMinAgentRadius();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeNavMesh)) {
          QuantumMapDataBaker.BakeNavMeshes(data, true);
        }

        QuantumEditorLog.LogImport($"Baking Quantum navmeshes took {sw.Elapsed.TotalSeconds:0.00} sec");
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.ClearUnityNavMesh) &&
          buildFlags.HasFlag(QuantumMapDataBakeFlags.BakeUnityNavMesh)) {
        foreach (var navmesh in data.GetComponentsInChildren<QuantumMapNavMeshUnity>()) {
          if (QuantumMapNavMeshUnityEditor.ClearUnityNavmesh(navmesh.gameObject)) {
            break;
          }
        }
      }

#endif

      EditorUtility.SetDirty(data);
      var asset = data.GetAsset(true);
      if (asset) {
        EditorUtility.SetDirty(asset);  
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.SaveUnityAssets)) {
        AssetDatabase.SaveAssets();
      }

      if (buildFlags.HasFlag(QuantumMapDataBakeFlags.GenerateAssetDB)) {
        QuantumUnityDBUtilities.RefreshGlobalDB();
      }
    }

    int IOrderedCallback.callbackOrder => 0;

    void IProcessSceneWithReport.OnProcessScene(Scene scene, BuildReport report) {
      if (report == null)
        return;

      AutoBakeMapData(scene, BuildTrigger.Build);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorDefaultConfigsAssetImporter.cs

namespace Quantum {
  using Quantum.Editor;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A Unity asset post processor that makes use to generate default Quantum configs in the user folder.
  /// </summary>
  public class QuantumDefaultConfigsAssetPostprocessor : AssetPostprocessor {
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
      if (didDomainReload) {
        return;
      }

      for (int i = 0; i < importedAssets.Length; i++) {
        if (AssetDatabase.GetMainAssetTypeAtPath(importedAssets[i]) != typeof(QuantumDefaultConfigs)) {
          continue;
        }

        var defaultConfigsAsset = AssetDatabase.LoadAssetAtPath<QuantumDefaultConfigs>(importedAssets[i]);
        if (defaultConfigsAsset == null) {
          continue;
        }
        
        bool saveAsset = false;
        
        if (defaultConfigsAsset.SimulationConfig == null) {
          defaultConfigsAsset.SimulationConfig = Create<Quantum.SimulationConfig>("DefaultConfigSimulation", defaultConfigsAsset).Item1;
          saveAsset = true;
        }

        if (defaultConfigsAsset.NavMeshAgentConfig == null) {
          (var asset, var assetGuid) = Create<Quantum.NavMeshAgentConfig>("DefaultConfigNavmeshAgent", defaultConfigsAsset);
          defaultConfigsAsset.NavMeshAgentConfig = asset;
          defaultConfigsAsset.SimulationConfig.Navigation.DefaultNavMeshAgent.Id = assetGuid;
          saveAsset = true;
        }

        if (defaultConfigsAsset.CharacterController2DConfig == null) {
          (var asset, var assetGuid) = Create<Quantum.CharacterController2DConfig>("DefaultConfigKCC2D", defaultConfigsAsset);
          defaultConfigsAsset.CharacterController2DConfig = asset;
          defaultConfigsAsset.SimulationConfig.Physics.DefaultCharacterController2D.Id = assetGuid;
          saveAsset = true;
        }

        if (defaultConfigsAsset.CharacterController3DConfig == null) {
          (var asset, var assetGuid) = Create<Quantum.CharacterController3DConfig>("DefaultConfigKCC3D", defaultConfigsAsset);
          defaultConfigsAsset.CharacterController3DConfig = asset;
          defaultConfigsAsset.SimulationConfig.Physics.DefaultCharacterController3D.Id = assetGuid;
          saveAsset = true;
        }

        if (defaultConfigsAsset.PhysicsMaterial == null) {
          (var asset, var assetGuid) = Create<Quantum.PhysicsMaterial>("DefaultConfigPhysicsMaterial", defaultConfigsAsset);
          defaultConfigsAsset.PhysicsMaterial = asset;
          defaultConfigsAsset.SimulationConfig.Physics.DefaultPhysicsMaterial.Id = assetGuid;
          saveAsset = true;
        }

        if (defaultConfigsAsset.SystemsConfig == null) {
          (var asset, var assetGuid) = Create<SystemsConfig>("DefaultConfigSystems", defaultConfigsAsset);
          defaultConfigsAsset.SystemsConfig = asset; 
          saveAsset = true;
        }

        if (saveAsset) {
          EditorUtility.SetDirty(defaultConfigsAsset);
          AssetDatabase.SaveAssets();
        }
      }
    }

    static (T, AssetGuid) Create<T>(string name, QuantumDefaultConfigs mainAsset) where T : AssetObject {
      
      var path = AssetDatabase.GetAssetPath(mainAsset);
      
      var asset = AssetDatabase.LoadAssetAtPath<T>(path);
      if (!asset) {
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        AssetDatabase.AddObjectToAsset(asset, mainAsset);
      }

      var (unityAssetGuid, fileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(asset.GetInstanceID());
      var expectedAssetGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(new GUID(unityAssetGuid), fileId, out _);
      return (asset, expectedAssetGuid);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorGizmoOverlay.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEditor.Overlays;
  using UnityEditor.UIElements;
  using UnityEngine;
  using UnityEngine.UIElements;
  using PopupWindow = UnityEditor.PopupWindow;
  using RangeAttribute = UnityEngine.RangeAttribute;

  [Icon("Assets/Photon/Quantum/Editor/EditorResources/QuantumEditorTextureQtnIcon.png")]
  [Overlay(typeof(SceneView), QuantumGameGizmosSettings.ID, DISPLAY_NAME, true)]
  internal class QuantumEditorGizmoOverlay : Overlay {
    const string DISPLAY_NAME = "Quantum Gizmos";
    SerializedObject _serializedObject;
    SerializedProperty _settingsProperty;

    const string VisibilityOnIcon = "animationvisibilitytoggleon";
    const string VisibilityOffIcon = "animationvisibilitytoggleoff";

    const string SelectedIcon = "Grid.BoxTool";
    const string SettingsIcon = "TerrainInspector.TerrainToolSettings On";

    readonly Texture2D _onSelectedIcon = GetUnityIcon(SelectedIcon);

    readonly Texture2D _settingsIcon = GetUnityIcon(SettingsIcon);

    readonly Dictionary<GizmoHeaderInfo, GizmoHeader> _headers =
      new Dictionary<GizmoHeaderInfo, GizmoHeader>();

    readonly Dictionary<QuantumGizmoEntry, ToolbarToggle> _gizmoEntryToToggle =
      new Dictionary<QuantumGizmoEntry, ToolbarToggle>();
    
    readonly BindingFlags _flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    private VisualElement FindParentWithClass(VisualElement element, string className) {
      var parent = element;

      while (parent != null) {
        if (parent.ClassListContains(className)) {
          return parent;
        }

        parent = parent.parent;
      }

      return null;
    }

    public override VisualElement CreatePanelContent() {
      var root = new VisualElement();

      if (QuantumGameGizmosSettingsScriptableObject.TryGetGlobal(out var settings) == false) {
        // fix error "Overlay "Quantum Gizmos" returned a null VisualElement."
        return root;
      }

      // hot reload re-uses the same class instance, so we need to clear the state
      _gizmoEntryToToggle.Clear();
      _headers.Clear();

      _serializedObject = new SerializedObject(settings);
      _settingsProperty =
        _serializedObject.FindProperty(nameof(QuantumGameGizmosSettingsScriptableObject.Global.Settings));

      root.name = id.ToLower() + "-content-root";

      root.style.maxWidth = Length.Percent(100);
      // leave room for toolbar
      root.style.maxHeight = containerWindow.position.height * .925f;

      SetGlobalSettingsUI(root);
      var sv = CreateScrollView(root);
      var systemEntries = GetEntries(settings.Settings, false);
      var userEntries = GetEntries(settings.Settings, true);

      CreateScrollViewContent(sv, userEntries);
      CreateScrollViewContent(sv, systemEntries);

      BindSearchBar(root);

      ForceBackgroundColor();

      return root;
    }

    private void ForceBackgroundColor() {
      Color unityDark = new Color32(56, 56, 56, 255);

      const string bgClassName = "overlay-box-background";

      // set background color!!
      EditorApplication.delayCall += () => {
        var contentRoot = typeof(Overlay)
          .GetProperty("contentRoot",
            _flags)?
          .GetValue(this) as VisualElement;

        if (contentRoot == null) {
          return;
        }

        // not in toolbar and not floating
        if (isInToolbar == false && floating == false) {
          var bg = FindParentWithClass(contentRoot, bgClassName);
          bg.style.backgroundColor = unityDark;
        }

        // floating and expanded
        if (floating && collapsed == false) {
          var tree = contentRoot.panel.visualTree;
          var overlay = tree.Q<VisualElement>(DISPLAY_NAME);

          var overlayBox = overlay.Q<VisualElement>(className: bgClassName);

          overlayBox.style.backgroundColor = unityDark;
        }

        // access the modal popup and set the background color
        if (collapsed) {
          var modalPopup = typeof(Overlay)
            .GetField("m_ModalPopup",
              _flags)?
            .GetValue(this) as VisualElement;

          var bg = modalPopup.Q(className: bgClassName);
          bg.style.backgroundColor = unityDark;
        }
      };
    }

    private VisualElement CreateScrollView(VisualElement root) {
      var scrollView = QuantumGizmoEditorUtil.CreateScrollView();
      root.Add(scrollView);

      return scrollView;
    }

    private void BindSearchBar(VisualElement container) {
      var scrollView = container.Q<ScrollView>();
      var searchBar = QuantumGizmoEditorUtil.CreateSearchField();

      searchBar.RegisterValueChangedCallback(evt => {
        var text = evt.newValue;

        if (string.IsNullOrEmpty(text)) {
          foreach (var child in scrollView.Children()) {
            ToggleElement(child, true);
          }

          return;
        }

        foreach (var child in scrollView.Children()) {
          if (child.name == "Header") {
            continue;
          }
          
          var nameElement = child.Q<Label>();

          if (nameElement == null)
            continue;

          ToggleElement(child, nameElement.text.Contains(text, StringComparison.OrdinalIgnoreCase));
        }
      });

      // set the search bar to the top of the scroll view
      scrollView.Insert(0, searchBar);
    }

    private void SetGlobalSettingsUI(VisualElement container) {
      var label = QuantumGizmoEditorUtil.CreateLabel("Global Gizmo Settings".ToUpper(), 15);

      label.style.unityFontStyleAndWeight = QuantumEditorSkin.ScriptHeaderLabelStyle.fontStyle;
      label.style.unityFontDefinition = new StyleFontDefinition(QuantumEditorSkin.ScriptHeaderLabelStyle.font);

      container.Add(label);

      var img = new Image();
      img.image = AssetDatabase.LoadAssetAtPath("Assets/Photon/Quantum/Editor/EditorResources/Quantum-logo-2x.png",
        typeof(Texture2D)) as Texture2D;

      img.style.alignSelf = Align.FlexEnd;
      img.style.opacity = 0.25f;
      img.style.width = 80;
      img.style.height = 80;
      img.style.top = -18;
      img.style.position = Position.Absolute;

      container.Add(img);

      var brightnessText = nameof(QuantumGameGizmosSettings.SelectedBrightness);
      var scaleText = nameof(QuantumGameGizmosSettings.IconScale);

      BindSlider(container, brightnessText);
      BindSlider(container, scaleText);
    }

    private void UpdateEnabledIcon(ToolbarToggle toggle) {
      var img = toggle.Q<Image>();

      img.image = GetUnityIcon(toggle.value ? VisibilityOnIcon : VisibilityOffIcon);
    }

    private static Texture2D GetUnityIcon(string name) {
      return (Texture2D)EditorGUIUtility.IconContent(name).image;
    }

    private void SetupToolBarToggles(
      ToolbarToggle selected,
      QuantumGizmoEntry entry) {
      selected.tooltip = "Only Draw On Select";

      SetFlagOnToolbarToggle(
        selected,
        newVal => entry.OnlyDrawSelected = newVal,
        entry.OnlyDrawSelected,
        _onSelectedIcon,
        _onSelectedIcon
      );
    }

    private void SetFlagOnToolbarToggle(
      ToolbarToggle toggle,
      Action<bool> setter,
      OptionalGizmoBool currentVal,
      Texture2D on,
      Texture2D off) {
      var img = new Image();
      toggle.Add(img);

      if (currentVal.HasValue == false) {
        toggle.style.display = DisplayStyle.None;
        return;
      }

      void Evaluate(bool newValue) {
        toggle.value = newValue;
        img.image = newValue ? on : off;

        setter(newValue);

        _serializedObject.Update();
        _serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(QuantumGameGizmosSettingsScriptableObject.Global);
      }

      toggle.RegisterCallback<ChangeEvent<bool>>(evt => Evaluate(evt.newValue));

      Evaluate(currentVal);
    }

    private void CreateScrollViewContent(VisualElement scrollView, GizmoEntryInfo[] entries) {
      var so = QuantumGameGizmosSettingsScriptableObject.Global;

      foreach (var entry in entries) {
        var headerInfo = entry.Header;

        if (headerInfo != null) {
          if (_headers.TryGetValue(headerInfo, out var header) == false) {
            header = InsertHeader(headerInfo);
            scrollView.Add(header.Element);
          }

          header.Entries.Add(entry.Entry);
          header.Update();
        }

        var gizmoName = QuantumGizmoEditorUtil.AddSpacesToString(entry.Name);

        // hack to fix navmesh being separated
        gizmoName = gizmoName.Replace("Nav Mesh", "NavMesh");

        var gizmoEntry = entry.Entry;

        var element = QuantumGizmoEditorUtil.CreateGizmoToolbar(
          gizmoName,
          out var enabledField,
          out var styleButton,
          out var selectedToggle,
          out var togglesParent
        );

        var help = QuantumCodeDoc.FindEntry(entry.Field, false);

        if (help != null) {
          element.tooltip = help.text;
        }

        enabledField.tooltip = "Enable/Disable Gizmo";
        styleButton.tooltip = "Edit Gizmo Style";

        var img = new Image();
        enabledField.Add(img);

        var styleImg = new Image();
        styleButton.Add(styleImg);

        styleImg.image = _settingsIcon;

        SetupToolBarToggles(
          selectedToggle,
          gizmoEntry
        );

        styleButton.clicked += () => {
          var clonedPopup = QuantumGizmoEditorUtil.CreateStylePopup(
            out var colorField,
            out var sColorField,
            out var wColorField,
            out var fillStyle,
            out var scaleStyle,
            out var scaleLabel
          );

          colorField.value = gizmoEntry.Color;
          ToggleElement(fillStyle, false);
          ToggleElement(sColorField.parent, false);
          ToggleElement(wColorField.parent, false);

          if (gizmoEntry is JointGizmoEntry joints) {
            sColorField.value = joints.SecondaryColor;
            wColorField.value = joints.WarningColor;

            ToggleElement(sColorField.parent, true);
            ToggleElement(wColorField.parent, true);

            sColorField.RegisterCallback<ChangeEvent<Color>>(evt => {
              _serializedObject.Update();
              joints.SecondaryColor = evt.newValue;
              _serializedObject.ApplyModifiedProperties();
              EditorUtility.SetDirty(so);
            });

            wColorField.RegisterCallback<ChangeEvent<Color>>(evt => {
              _serializedObject.Update();
              joints.WarningColor = evt.newValue;
              _serializedObject.ApplyModifiedProperties();
              EditorUtility.SetDirty(so);
            });
          }

          if (gizmoEntry is NavMeshGizmoEntry navMesh) {
            sColorField.value = navMesh.RegionColor;
            ToggleElement(sColorField.parent, true);
            sColorField.parent.Q<Label>().text = "Region Color";

            sColorField.RegisterCallback<ChangeEvent<Color>>(evt => {
              _serializedObject.Update();
              navMesh.RegionColor = evt.newValue;
              _serializedObject.ApplyModifiedProperties();
              EditorUtility.SetDirty(so);
            });
          }

          if (gizmoEntry is NavMeshBorderGizmoEntry navMeshBorder) {
            sColorField.value = navMeshBorder.BorderNormalColor;
            ToggleElement(sColorField.parent, true);
            sColorField.parent.Q<Label>().text = "Normal Color";
            ToggleElement(fillStyle, true);
            fillStyle.text = "Draw Normals";
            fillStyle.value = navMeshBorder.DrawNormals;

            fillStyle.RegisterCallback<ChangeEvent<bool>>(evt => {
              _serializedObject.Update();
              navMeshBorder.DrawNormals = evt.newValue;
              _serializedObject.ApplyModifiedProperties();
              EditorUtility.SetDirty(so);
            });

            sColorField.RegisterCallback<ChangeEvent<Color>>(evt => {
              _serializedObject.Update();
              navMeshBorder.BorderNormalColor = evt.newValue;
              _serializedObject.ApplyModifiedProperties();
              EditorUtility.SetDirty(so);
            });
          }

          if (gizmoEntry.Scale > 0) {
            scaleStyle.value = gizmoEntry.Scale;

            // get min and max values from the attribute
            var min = 0f;
            var max = 1f;

            var field = typeof(QuantumGizmoEntry).GetField(nameof(QuantumGizmoEntry.Scale));
            var value = (float)field.GetValue(gizmoEntry);
            
            if (field.GetCustomAttributes(typeof(RangeAttribute), true).FirstOrDefault() is RangeAttribute range) {
              min = range.min;
              max = range.max;
            }

            scaleStyle.lowValue = min;
            scaleStyle.highValue = max;
            scaleStyle.SetValueWithoutNotify(value);
          } else {
            ToggleElement(scaleStyle, false);
            ToggleElement(scaleLabel, false);
          }

          if (gizmoEntry.DisableFill.HasValue) {
            var value = gizmoEntry.DisableFill;
            fillStyle.value = value;
            ToggleElement(fillStyle, true);
          }

          scaleStyle.RegisterCallback<ChangeEvent<float>>(evt => {
            _serializedObject.Update();
            gizmoEntry.Scale = evt.newValue;
            _serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
          });

          fillStyle.RegisterCallback<ChangeEvent<bool>>(evt => {
            _serializedObject.Update();
            gizmoEntry.DisableFill = evt.newValue;
            _serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
          });

          colorField.RegisterCallback<ChangeEvent<Color>>(evt => {
            _serializedObject.Update();
            gizmoEntry.Color = evt.newValue;
            _serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
          });

          var window = new QuantumEditorMapGizmoStylePopup { PopUpContent = clonedPopup };

          PopupWindow.Show(styleButton.worldBound, window);
        };

        enabledField.value = gizmoEntry.Enabled;

        UpdateEnabledIcon(enabledField);

        _gizmoEntryToToggle[gizmoEntry] = enabledField;

        enabledField.RegisterCallback<ChangeEvent<bool>>(evt => {
          gizmoEntry.Enabled = evt.newValue;
          _serializedObject.ApplyModifiedProperties();
          EditorUtility.SetDirty(so);

          UpdateEnabledIcon(enabledField);

          UpdateToggleStates(gizmoEntry);
        });

        if (gizmoEntry is QuantumUserGizmoEntry userEntry) {
          var hasDrawMethod = DoesEntryHaveDrawMethod(userEntry);

          if (hasDrawMethod == false) {
            var bg = new VisualElement();
            bg.style.backgroundColor = Color.black.Alpha(0.5f);
            bg.style.position = Position.Absolute;
            bg.style.width = Length.Percent(100);
            bg.style.height = Length.Percent(100);
            bg.style.flexDirection = FlexDirection.Column;
            bg.style.justifyContent = Justify.Center;

            element.Add(bg);

            var label = new Label("ERROR: No draw method found.");
            label.tooltip = "Please add a method with the QuantumGizmoCallback attribute that points to this entry.";
            label.style.color = Color.red;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            // center the label
            label.style.alignSelf = Align.Center;

            bg.Add(label);
          }

          var hasSelectionMethod = DoesEntryHaveSelectionMethod(userEntry);

          if (hasSelectionMethod == false) {
            selectedToggle.style.display = DisplayStyle.None;
          }
        }

        scrollView.Add(element);
      }
    }

    private bool DoesEntryHaveSelectionMethod(QuantumUserGizmoEntry entry) {
      var settings = QuantumGameGizmosSettingsScriptableObject.Global.Settings;
      var type = settings.GetType();
      var methods = type.GetMethods(_flags);

      foreach (var method in methods) {
        var attr = method.GetCustomAttribute<QuantumGizmoCallbackAttribute>();

        if (attr == null) {
          continue;
        }

        var field = type.GetField(attr.FieldName);

        if (field == null) {
          continue;
        }

        if (field.GetValue(settings) == entry) {
          if (string.IsNullOrEmpty(attr.SelectionValidation) == false) {
            var selectionMethod = type.GetMethod(attr.SelectionValidation, _flags);

            if (selectionMethod?.ReturnType == typeof(bool) == false) {
              Log.Error("Selection validation method must return a boolean: " + attr.SelectionValidation);
            }
            
            return selectionMethod != null;
          }
        }
      }

      return false;
    }

    private bool DoesEntryHaveDrawMethod(QuantumUserGizmoEntry entry) {
      var settings = QuantumGameGizmosSettingsScriptableObject.Global.Settings;
      var type = settings.GetType();
      var methods = type.GetMethods(_flags);

      foreach (var method in methods) {
        var attr = method.GetCustomAttribute<QuantumGizmoCallbackAttribute>();

        if (attr == null) {
          continue;
        }

        var field = type.GetField(attr.FieldName);

        if (field == null) {
          continue;
        }

        if (field.GetValue(settings) == entry) {
          return true;
        }
      }

      return false;
    }

    private void UpdateToggleStates(QuantumGizmoEntry entry) {
      foreach (var kvp in _headers) {
        if (kvp.Value.Entries.Contains(entry)) {
          kvp.Value.Update();
        }
      }
    }

    private GizmoHeader InsertHeader(GizmoHeaderInfo info) {
      var header = new GizmoHeader();

      var element = QuantumGizmoEditorUtil.CreateHeader(info.Name, out var toggle, out var bg);

      bg.style.backgroundColor = QuantumEditorSkin.GetScriptHeaderColor(info.BackColor);
      toggle.style.backgroundColor = QuantumEditorSkin.GetScriptHeaderColor(info.BackColor).Darken();
      header.Element = element;
      header.Entries = new List<QuantumGizmoEntry>();

      _headers.Add(info, header);

      header.Update = () => {
        UpdateHeaderToggleValue(toggle, info);
        UpdateHeaderToggleImage(toggle, info);
      };

      toggle.RegisterValueChangedCallback(evt => {
        foreach (var entry in header.Entries) {
          entry.Enabled = evt.newValue;

          var gToggle = _gizmoEntryToToggle[entry];
          gToggle.SetValueWithoutNotify(evt.newValue);

          UpdateEnabledIcon(gToggle);
        }

        UpdateHeaderToggleImage(toggle, info);

        _serializedObject.Update();
        _serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(QuantumGameGizmosSettingsScriptableObject.Global);
      });

      return header;
    }

    private bool SomeButNotAllGizmosEnabled(IEnumerable<QuantumGizmoEntry> entries) {
      bool foundEnabled = false;
      bool foundDisabled = false;

      foreach (var entry in entries) {
        if (entry.Enabled) {
          foundEnabled = true;
        } else {
          foundDisabled = true;
        }
      }

      return foundEnabled && foundDisabled;
    }

    private bool AllGizmosEnabled(IEnumerable<QuantumGizmoEntry> entries) {
      foreach (var entry in entries) {
        if (!entry.Enabled) {
          return false;
        }
      }

      return true;
    }

    private void UpdateHeaderToggleValue(ToolbarToggle toggle, GizmoHeaderInfo header) {
      var h = _headers[header];
      var someButNotAllEnabled = SomeButNotAllGizmosEnabled(h.Entries);
      var allEnabled = AllGizmosEnabled(h.Entries);

      toggle.SetValueWithoutNotify(allEnabled || someButNotAllEnabled);
    }

    private void UpdateHeaderToggleImage(ToolbarToggle toggle, GizmoHeaderInfo header) {
      var h = _headers[header];
      var image = toggle.Q<Image>();

      var someButNotAllEnabled = SomeButNotAllGizmosEnabled(h.Entries);
      var allEnabled = AllGizmosEnabled(h.Entries);

      if (allEnabled) {
        image.image = GetUnityIcon(VisibilityOnIcon);
      } else {
        image.image = GetUnityIcon(someButNotAllEnabled ? VisibilityOnIcon : VisibilityOffIcon);
      }
    }

    private static void ToggleElement(VisualElement e, bool visible) {
      e.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
      e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool FindNonUserFields(FieldInfo f) {
      return
        (f.FieldType == typeof(QuantumGizmoEntry) || f.FieldType.IsSubclassOf(typeof(QuantumGizmoEntry))) &&
        FindUserFields(f) == false;
    }

    private bool FindUserFields(FieldInfo f) {
      return f.FieldType == typeof(QuantumUserGizmoEntry) || f.FieldType.IsSubclassOf(typeof(QuantumUserGizmoEntry));
    }

    private GizmoEntryInfo[] GetEntries(QuantumGameGizmosSettings settings, bool user) {
      Func<FieldInfo, bool> find = user ? FindUserFields : FindNonUserFields;

      var fields = settings.GetType().GetFields()
        .Where(find)
        .ToArray();

      var entries = new List<GizmoEntryInfo>();

      var currentHeader = user == false
        ? null
        :
        // if someone forgets to add a header, we'll default to this
        new GizmoHeaderInfo { Name = "Quantum User", BackColor = ScriptHeaderBackColor.Green };

      for (var i = 0; i < fields.Length; i++) {
        var field = fields[i];
        var entry = (QuantumGizmoEntry)field.GetValue(settings);

        var attr = field
          .GetCustomAttributes(typeof(HeaderAttribute), false)
          .FirstOrDefault();

        if (attr is HeaderAttribute header) {
          currentHeader = new GizmoHeaderInfo { Name = header.header };

          var colorAttr = field
            .GetCustomAttributes(typeof(GizmoIconColorAttribute), false)
            .FirstOrDefault();

          if (colorAttr is GizmoIconColorAttribute color) {
            currentHeader.BackColor = color.Color;
          }
        }

        entries.Add(new GizmoEntryInfo { Name = field.Name, Field = field, Entry = entry, Header = currentHeader });
      }

      return entries.ToArray();
    }

    private void BindSlider(VisualElement parent, string name) {
      var min = 0f;
      var max = 1f;

      var field = typeof(QuantumGameGizmosSettings).GetField(name);
      var value = (float)field.GetValue(QuantumGameGizmosSettingsScriptableObject.Global.Settings);

      if (field.GetCustomAttributes(typeof(RangeAttribute), true).FirstOrDefault() is RangeAttribute range) {
        min = range.min;
        max = range.max;
      }

      var property = _settingsProperty.FindPropertyRelative(name);

      EventCallback<ChangeEvent<float>> callback = evt => {
        _serializedObject.Update();
        property.floatValue = evt.newValue;
        _serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(QuantumGameGizmosSettingsScriptableObject.Global);
      };

      // add spaces between capital letters
      var spaced = QuantumGizmoEditorUtil.AddSpacesToString(name);
      var newSlider = QuantumGizmoEditorUtil.CreateSliderWithTitle(
        spaced,
        value,
        min,
        max,
        callback
      );

      parent.Add(newSlider);
    }
  }

  class GizmoHeaderInfo {
    public string Name;
    public ScriptHeaderBackColor BackColor;
  }

  class GizmoHeader {
    public VisualElement Element;
    public List<QuantumGizmoEntry> Entries;

    public Action Update;
  }

  class GizmoEntryInfo {
    public string Name;
    public QuantumGizmoEntry Entry;
    public FieldInfo Field;
    public GizmoHeaderInfo Header;
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorHub.Common.cs

// merged EditorHub

#region QuantumEditorHubCondition.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  
  [Serializable]
  public struct QuantumEditorHubCondition {
    public string Value;
  }

  [Serializable]
  public enum QuantumEditorHubConditionEnum {
    None,
    AssetMissing,
    TypeIsValid,
    TypeIsNotValid,
    SceneView3D,
    SceneView2D,
    ButtonClicked,
    SceneExists,
    SceneNotExists,
    DefineEnabled,
    DefineMissing,
    GlobalScriptableObjectExists,
    Custom = 100,
  }

  [CustomPropertyDrawer(typeof(QuantumEditorHubCondition), true)]
  internal partial class QuantumEditorHubConditionDrawer : PropertyDrawer {
    static string[] _typeNames;

    static partial void RegisterTypesUser(List<string> types);

    [InitializeOnLoadMethod]
    static void InitializedPackageImportCallbacks() {
      var types = Enum.GetNames(typeof(QuantumEditorHubConditionEnum)).ToList();
      RegisterTypesUser(types);
      _typeNames = types.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.BeginChangeCheck();

        var p = property.FindPropertyRelativeOrThrow(nameof(QuantumEditorHubCondition.Value));
        var index = Array.IndexOf(_typeNames, p.stringValue);
        position = EditorGUI.PrefixLabel(position, label);
        var newIndex = Math.Max(0, EditorGUI.Popup(position, index, _typeNames));
        if (newIndex != index) {
          p.stringValue = _typeNames[newIndex];
        }

        if (EditorGUI.EndChangeCheck()) {
          property.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion


#region QuantumEditorHubPage.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using Object = UnityEngine.Object;

  [Serializable]
  public class QuantumEditorHubPage {
    string _cachedString;

    internal const string AssetLabel = "QuantumHubContent";
    const string ActionStepTemplate = "<size=20>Step {0}</size>   ";

    internal delegate void CustomDrawWidget(QuantumEditorHubPage page, QuantumEditorHubWidget widget);
    internal delegate bool CustomConditionCheck(QuantumEditorHubCondition condition);

    public string Title;
    public string Description;
    public Texture2D Icon;
    public string OverwritePage;
    public string PopupOncePlayerPrefsKey;
    public List<QuantumEditorHubWidgetBase> Elements;

    public bool IsPopupRequired => string.IsNullOrEmpty(PopupOncePlayerPrefsKey) == false && PlayerPrefs.HasKey(PopupOncePlayerPrefsKey) == false;

    public void DeleteAllPlayerPrefKeys() {
      if (string.IsNullOrEmpty(PopupOncePlayerPrefsKey) == false) {
        PlayerPrefs.DeleteKey(PopupOncePlayerPrefsKey);
      }

      foreach (var widget in Elements) {
        widget.DeleteAllPlayerPrefKeys();
      }
    }

    internal void Draw(QuantumEditorHubWindow window, CustomDrawWidget customDrawWidget, CustomConditionCheck customConditionCheck) {
      if (string.IsNullOrEmpty(PopupOncePlayerPrefsKey) == false) {
        // Mark as read
        PlayerPrefs.SetInt(PopupOncePlayerPrefsKey, 1);
      }

      UpdateStateHierarchy(customConditionCheck);

      foreach (var widget in Elements) {
        Draw(widget, window, customDrawWidget);
      }
    }

    internal void OnImportPackageCompleted(string packageName) {
      foreach (var widget in Elements) {
        OnImportPackageCompleted(packageName, widget);

        foreach (var stepWidget in widget.StepElements) {
          OnImportPackageCompleted(packageName, stepWidget);
        }
      }
    }

    internal void Draw(QuantumEditorHubWidget widget, QuantumEditorHubWindow window, CustomDrawWidget drawCustomWidget) {
      var baseWidget = widget as QuantumEditorHubWidgetBase;

      switch (widget.WidgetModeAsEnum) {
        case QuantumEditorHubWidgetTypeEnum.Custom:
          if (widget.State.IsDrawn == false) { break; }

          drawCustomWidget?.Invoke(this, widget);

          break;

        case QuantumEditorHubWidgetTypeEnum.Step:
          if (widget.State.IsHidden) {
            break;
          }

          using (new EditorGUILayout.HorizontalScope()) {
            GUILayout.Label(window.GetStatusIcon(widget.State.IsComplete),
              GUILayout.Width(QuantumEditorHubWindow.StatusIconWidthLarge.x),
              GUILayout.Height(QuantumEditorHubWindow.StatusIconWidthLarge.y));
            GUILayout.Label(string.Format(ActionStepTemplate, widget.State.StepIndex + 1) + widget.Text);
          }

          if (widget.State.IsDrawn == false) {
            break;
          }

          if (baseWidget == null) {
            break;
          }

          foreach (var stepWidget in baseWidget.StepElements) {
            Draw(stepWidget, window, drawCustomWidget);
          }

          if (GUILayout.Button("<i>Skip this step</i>", window.Styles.TextLabel)) {
            widget.State.TrySetSkippedAndSave();
          }

          break;

        case QuantumEditorHubWidgetTypeEnum.Hierarchy:
          if (widget.State.IsDrawn == false) { break; }

          if (string.IsNullOrEmpty(widget.Text) == false) {
            GUILayout.Label(widget.Text);
          }

          if (baseWidget == null) {
            break;
          }

          foreach (var stepWidget in baseWidget.StepElements) {
            Draw(stepWidget, window, drawCustomWidget);
          }

          break;

        case QuantumEditorHubWidgetTypeEnum.Text:
          if (widget.State.IsDrawn == false) { break; }

          GUILayout.Label(widget.Text);
          //GUILayout.Space(8);
          break;

        case QuantumEditorHubWidgetTypeEnum.Image:

          var tex = widget.Asset.asset as Texture2D;
          if (tex != null) {
            var width = tex.width;
            var height = tex.height;
            if (width > window.ContentSize.x) {
              width = (int)window.ContentSize.x;
              height = (int)(tex.height * (window.ContentSize.x / tex.width));
            }

            GUI.DrawTexture(GUILayoutUtility.GetRect(GUIContent.none, 
              GUIStyle.none, 
              GUILayout.Height(height), 
              GUILayout.Width(width)), tex);
          }

          break;

        case QuantumEditorHubWidgetTypeEnum.SceneButton:
          if (widget.State.IsDrawn == false) { break; }

          window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
            statusIcon: widget.StatusIcon,
            callback: () => {
              if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                AddScenePathToBuildSettings(widget.Scene, addToTop: widget.AddSceneToTop);
                
                EditorSceneManager.OpenScene(widget.Scene);

                widget.OnButtonClicked();

                if (widget.StartPlayMode) {
                  EditorApplication.isPlaying = true;
                }
              }
            });
          break;

        case QuantumEditorHubWidgetTypeEnum.LinkButton:
          if (widget.State.IsDrawn == false) { break; }

          window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
            statusIcon: widget.StatusIcon,
            callback: () => {
              Application.OpenURL(widget.Url);
              widget.OnButtonClicked();
            });
            
          break;

        case QuantumEditorHubWidgetTypeEnum.PingAsset:
          if (widget.State.IsDrawn == false) { break; }

          window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
            statusIcon: widget.StatusIcon,
            callback: () => {
              EditorGUIUtility.PingObject(widget.Asset.asset); Selection.activeObject = widget.Asset.asset;
            });
          break;

        case QuantumEditorHubWidgetTypeEnum.PingGlobalScriptableObject:
          if (widget.State.IsDrawn == false) { break; }

          // As a fallback the SDK App Settings Asset
          Object objToPing = window.SdkAppSettingsAsset;
          
          if (string.IsNullOrEmpty(widget.Type.ScriptableObject) == false) {
            
            Type globalObjectType = QuantumEditorHubWindow.HubUtils.FindType(widget.Type.ScriptableObject);
            if (QuantumEditorHubWindow.HubUtils.TryGetGlobalScriptableObjectRefl(globalObjectType, out var globalScriptableObj)) {
              objToPing = globalScriptableObj;
            }
          }
          
          window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
            statusIcon: widget.StatusIcon,
            callback: () => {
              EditorGUIUtility.PingObject(objToPing); Selection.activeObject = objToPing;
            });
          
          break;

        case QuantumEditorHubWidgetTypeEnum.EnsureGlobalScriptableObjectExists:
          if (widget.State.IsDrawn == false) { break; }

          if (string.IsNullOrEmpty(widget.Type.ScriptableObject) == false) {
            window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
              statusIcon: widget.StatusIcon,
              callback: () => {
                Type globalObjectType = QuantumEditorHubWindow.HubUtils.FindType(widget.Type.ScriptableObject);
                QuantumEditorHubWindow.HubUtils.EnsureGlobalScriptableObjectExistsRefl(globalObjectType);
              });
          }

          break;

        case QuantumEditorHubWidgetTypeEnum.AppIdBox:
          if (widget.State.IsDrawn == false) { break; }

          window.DrawSetupAppId();

          break;

        case QuantumEditorHubWidgetTypeEnum.ClearPlayerPrefs:
          if (widget.State.IsDrawn == false) { break; }

          window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext,
            statusIcon: widget.StatusIcon,
            callback: () => {
              DeleteAllPlayerPrefKeys();
            });
          break;

        case QuantumEditorHubWidgetTypeEnum.InstallPackage:
          if (widget.State.IsDrawn == false) { break; }

          window.DrawButtonAction(widget.Icon, widget.Text, widget.Subtext, statusIcon: widget.StatusIcon, callback: () => {
            AssetDatabase.ImportPackage(AssetDatabase.GetAssetPath(widget.Asset.asset), false);
          });

          break;

        case QuantumEditorHubWidgetTypeEnum.LogLevel:

          if (widget.State.IsDrawn == false) { break; }
          window.DrawLogLevel(widget.Icon, widget.Text);
          break;

        case QuantumEditorHubWidgetTypeEnum.ToggleDefine:
          if (widget.State.IsDrawn == false) { break; }
          if (string.IsNullOrEmpty(widget.Url)) { break; }

          var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
          var hasDefineForCurrentBuildTarget = AssetDatabaseUtils.HasScriptingDefineSymbol(namedBuildTarget, widget.Url);
          var define = AssetDatabaseUtils.HasScriptingDefineSymbol(widget.Url);
          var hasDefineForAllBuildTargets = define.HasValue && define.Value;

          var text = hasDefineForAllBuildTargets ?
            $"{widget.Text} [<color=#7de886>Enabled</color>]" :
              hasDefineForCurrentBuildTarget ?
                $"{widget.Text} [<color=#fade78>Disabled For Some Build Targets</color>]" : 
                $"{widget.Text} [<color=#faa878>Disabled For All Build Targets</color>]";

          window.DrawButtonAction(widget.Icon, text, widget.Subtext,
            statusIcon: widget.StatusIcon,
            callback: () => {
              AssetDatabaseUtils.UpdateScriptingDefineSymbol(widget.Url, !hasDefineForAllBuildTargets);
            });

          break;


        case QuantumEditorHubWidgetTypeEnum.Changelog:
          if (widget.State.IsDrawn == false) { break; }

          if (string.IsNullOrEmpty(widget.State.CachedString)) {
            widget.State.CachedString = ParseReleaseNotes(widget.Asset.asset as TextAsset);
          }

          GUILayout.Label(widget.State.CachedString, window.Styles.ReleaseNotes);
          break;

        case QuantumEditorHubWidgetTypeEnum.Textfile:
          if (widget.State.IsDrawn == false) { break; }

          if (string.IsNullOrEmpty(widget.State.CachedString)) {
            try {
              widget.State.CachedString = (widget.Asset.asset as TextAsset).text;
            }
            catch {
              widget.State.CachedString = "File unreadable";
            }
          }

          GUILayout.Label(widget.State.CachedString);

          break;

        case QuantumEditorHubWidgetTypeEnum.BuildInfoFile:
          if (widget.State.IsDrawn == false) { break; }

          if (string.IsNullOrEmpty(widget.State.CachedString)) {
            widget.State.CachedString = ParseBuildInfo(widget.Asset.asset as TextAsset);
          }

          GUILayout.BeginVertical();
          GUILayout.Space(5);
          GUILayout.Label(widget.State.CachedString, window.Styles.TextLabel);
          GUILayout.EndVertical();

          break;

        case QuantumEditorHubWidgetTypeEnum.AssemblyVersion:
          if (widget.State.IsDrawn == false) { break; }

          if (string.IsNullOrEmpty(widget.State.CachedString)) {
            widget.State.CachedString = ParseAssemblyVersion(widget.Type.Class);
          }

          GUILayout.Label(widget.State.CachedString, window.Styles.TextLabel);

          break;
      }
    }

    string ParseAssemblyVersion(string typeName) { 
      const string ColorTemplate = "<color=#FFDDBB>{0}</color>: {1}";

      var type = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(x => x.GetTypes())
        .Where(t => t != null && t.Name.Equals(typeName)).First();

      try {
        if (type == null) {
          
        }

        var codeBase = System.Reflection.Assembly.GetAssembly(type).CodeBase;
        var path = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
        return string.Format(ColorTemplate, Path.GetFileName(codeBase), fileVersionInfo.ProductVersion);
      } catch {
        return "Type not found";
      }
    }

    string ParseBuildInfo(TextAsset textAssset) {
      const string ClassReformat = "<color=#FFDDBB>{0}</color>";

      try {
        var text = textAssset.text;
        text = Regex.Replace(text, @"(build):", string.Format(ClassReformat, "$1"));
        text = Regex.Replace(text, @"(date):", string.Format(ClassReformat, "$1"));
        text = Regex.Replace(text, @"(git):", string.Format(ClassReformat, "$1"));
        return text;
      } catch {
        return "File unreadable";
      }
    }

    string ParseReleaseNotes(TextAsset textAssset) {
      const string TitleVersionReformat = "<size=22><color=white>{0}</color></size>";
      const string SectionReformat = "<i><color=lightblue>{0}</color></i>";
      const string Header1Reformat = "<size=22><color=white>{0}</color></size>";
      const string Header2Reformat = "<size=18><color=white>{0}</color></size>";
      const string Header3Reformat = "<b><color=#ffffaaff>{0}</color></b>";
      const string ClassReformat = "<color=#FFDDBB>{0}</color>";

      try {
        var text = textAssset.text;
        // #
        text = Regex.Replace(text, @"^# (.*)", string.Format(TitleVersionReformat, "$1"));
        text = Regex.Replace(text, @"(?<=\n)# (.*)", string.Format(Header1Reformat, "$1"));
        // ##
        text = Regex.Replace(text, @"(?<=\n)## (.*)", string.Format(Header2Reformat, "$1"));
        // ###
        text = Regex.Replace(text, @"(?<=\n)### (.*)", string.Format(Header3Reformat, "$1"));
        // **Changes**
        text = Regex.Replace(text, @"(?<=\n)\*\*(.*)\*\*", string.Format(SectionReformat, "$1"));
        // `Class`
        text = Regex.Replace(text, @"\`([^\`]*)\`", string.Format(ClassReformat, "$1"));
        return text;
      } catch {
        return "Failed to parse changelog";
      }
    }

    /// <summary>
    /// Creates mutable state objects and shared it with step elements.
    /// </summary>
    void UpdateStateHierarchy(CustomConditionCheck customConditionCheck) {
      var stepIndex = 0;
      for (int i = 0; i < Elements.Count; i++) {
        var widget = Elements[i];
        widget.State = widget.State ?? new QuantumEditorHubWidget.HubWidgetState($"Quantum.Hub.{Title}.{widget.Id}");
        widget.UpdateState(customConditionCheck);

        switch (widget.WidgetModeAsEnum) {
          case QuantumEditorHubWidgetTypeEnum.Step:

            if (widget.StepElements?.Count > 0) {
              widget.State.StepIndex = stepIndex;

              if (widget.State.IsHidden == false) {
                stepIndex++;
              }

              foreach (var stepWidget in widget.StepElements) {
                stepWidget.State = widget.State;
              }
            }

            break;

          case QuantumEditorHubWidgetTypeEnum.Hierarchy:

            foreach (var stepWidget in widget.StepElements) {
              stepWidget.State = stepWidget.State ?? new QuantumEditorHubWidget.HubWidgetState($"Quantum.Hub.{Title}.{stepWidget.Id}");
              stepWidget.UpdateState(customConditionCheck);
            }

            break;
        }
      }
    }

    void OnImportPackageCompleted(string packageName, QuantumEditorHubWidget widget) {
      if (widget.State != null && widget.State.IsDrawn == false) {
        return;
      }

      switch (widget.WidgetModeAsEnum) {
        case QuantumEditorHubWidgetTypeEnum.InstallPackage:
          var packagePath = AssetDatabase.GetAssetPath(widget.Asset.asset);
          if (string.Equals(packageName, Path.GetFileNameWithoutExtension(packagePath), StringComparison.Ordinal)) {

            if (string.IsNullOrEmpty(widget.Scene) == false) {
              AddScenePathToBuildSettings(widget.Scene, addToTop: widget.AddSceneToTop);
            }

            AssetDatabase.ImportAsset(Path.GetDirectoryName(packagePath), ImportAssetOptions.ImportRecursive);

            if (string.IsNullOrEmpty(widget.Scene) == false) {
              QuantumEditorHubSrpTools.ConvertSampleToSrp(new List<string>(){widget.Scene});
              if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene(widget.Scene);
              }
            }

            return;
          }
          break;
      }
    }
    
    /// <summary>
    /// Add a scene path to the build settings
    /// </summary>
    /// <param name="scenePath">Path to the scene</param>
    /// <param name="addToTop">Add the new scene to the top</param>
    static void AddScenePathToBuildSettings(string scenePath, bool addToTop) {
      var editorBuildSettingsScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
      if (editorBuildSettingsScenes.FindIndex(s => s.path.Equals(scenePath, StringComparison.Ordinal)) < 0) {
        if (addToTop) {
          editorBuildSettingsScenes.Insert(0, new EditorBuildSettingsScene { path = scenePath, enabled = true });
        } else {
          editorBuildSettingsScenes.Add(new EditorBuildSettingsScene { path = scenePath, enabled = true });
        }
        EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
      }
    }
  }
}

#endregion


#region QuantumEditorHubSrpTools.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.Rendering;
  using UnityEngine.SceneManagement;

  /// <summary>
  /// Utilities for converting scene and prefab materials to the used SRP. This allows intro samples to imported into a project with any SRP.
  /// </summary>
  public static partial class QuantumEditorHubSrpTools {
    private static Dictionary<Material, Material> materialsCache = new Dictionary<Material, Material>();

    static partial void BeforeOpenSceneUser();

    private static void ConvertMeshRendererForRenderPipeline(RenderPipelineAsset renderPipeline, MeshRenderer meshRenderer) {
      var suffix = "URP";
# if QUANTUM_ENABLE_HDRP
      suffix = "HDRP";
#endif

      var materials = meshRenderer.sharedMaterials;

      for (int i = 0; i < materials.Length; i++) {
        var oldMaterial = materials[i];
        if (materialsCache.TryGetValue(oldMaterial, out var value)) {
          materials[i] = value;
        } else {
          var oldMaterialPath = AssetDatabase.GetAssetPath(oldMaterial);
          var newMaterial = new Material(renderPipeline.defaultMaterial);
          newMaterial.color = oldMaterial.color;
          newMaterial.mainTexture = oldMaterial.mainTexture;
          newMaterial.mainTextureOffset = oldMaterial.mainTextureOffset;
          newMaterial.mainTextureScale = oldMaterial.mainTextureScale;
          AssetDatabase.CreateAsset(newMaterial, oldMaterialPath.Replace(".mat", $"_{suffix}.mat"));
          materials[i] = newMaterial;
          materialsCache.Add(oldMaterial, newMaterial);
        }
      }

      meshRenderer.sharedMaterials = materials;
    }

    internal static void ConvertSampleToSrp(List<string> scenePaths) {
      var renderPipeline = GraphicsSettings.defaultRenderPipeline;
      if (renderPipeline == null || renderPipeline.defaultMaterial == null)
        return;

      materialsCache.Clear();

      ConvertSamplePrefabsToSrp(scenePaths[0]);

      // E.g. refresh asset db (RefreshGlobalDB)
      BeforeOpenSceneUser();

      EditorSceneManager.SaveOpenScenes();
      foreach (var scenePath in scenePaths) {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        var scene = SceneManager.GetSceneByName(sceneName);
        ConvertScenetoSrp(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // used instead of SceneManager.loadedSceneCount to support Unity 2022 and older. Shouldn't matter because it's unlikely something else is loading / unloading a scene while the hub runs this
        if (SceneManager.sceneCount > 1) { 
          SceneManager.UnloadSceneAsync(scene);
        }
      }
    }

    private static void ConvertSamplePrefabsToSrp(string scenePath) {
      var renderPipeline = GraphicsSettings.defaultRenderPipeline;
      
      var index = scenePath.IndexOf("/Scenes", StringComparison.Ordinal);
      if (index == -1) // no folder structure with scenes in "Scenes" folder for sample
      {
        return;
      }

      List<string> assetPaths = new List<string>();
      
      var folderPath = scenePath.Substring(0, index) + "/Prefabs/";
      if (Directory.Exists(folderPath)) {
        assetPaths.AddRange( AssetDatabase.FindAssets("t:GameObject", new[] { folderPath }));
      }
      
      folderPath =  scenePath.Substring(0, index) + "/Resources/";
      if (Directory.Exists(folderPath)) {
        assetPaths.AddRange( AssetDatabase.FindAssets("t:GameObject", new[] { folderPath }));
      }
    
      foreach (string assetGuid in assetPaths) {
        string path = AssetDatabase.GUIDToAssetPath(assetGuid);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var meshRenderers = asset.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        foreach (var meshRenderer in meshRenderers) {
          ConvertMeshRendererForRenderPipeline(renderPipeline, meshRenderer);
        }

        AssetDatabase.SaveAssetIfDirty(asset);
      }
    }


    private static void ConvertScenetoSrp(Scene scene) {
      var renderPipeline = GraphicsSettings.defaultRenderPipeline;

      var rootGameObjects = scene.GetRootGameObjects();

      foreach (var gameObject in rootGameObjects) {
        var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        foreach (var meshRenderer in meshRenderers) {
          ConvertMeshRendererForRenderPipeline(renderPipeline, meshRenderer);
        }
      }

#if QUANTUM_ENABLE_HDRP
      // HDRP does not render correctly without Fog enabled.
      // Unity enabled it in their default scenes, but not in their default HDRP setup. Here we detect HDRP and enable Fog.
        var go = new GameObject("Global Volume");
        SceneManager.MoveGameObjectToScene(go, scene);
        var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.weight = 1.0f;

        string sceneDirectory = System.IO.Path.GetDirectoryName(scene.path);
        string profilePath = $"{sceneDirectory}/{scene.name}_VolumeProfile.asset";
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
 
        var fog = profile.Add<UnityEngine.Rendering.HighDefinition.Fog>();
        fog.enabled.value = true;

        UnityEditor.AssetDatabase.CreateAsset(profile, profilePath);
        UnityEditor.AssetDatabase.AddObjectToAsset(fog, profile);
        UnityEditor.AssetDatabase.SaveAssets();
        
        volume.sharedProfile = profile;
#endif
    }
  }
}

#endregion


#region QuantumEditorHubWidget.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using System;
  using UnityEditor;
  using UnityEngine;
  using System.IO;

  /// <summary>
  /// Structured as such that a subclass has child elements to prevent Unity inspector recursion problems.
  /// </summary>
  [Serializable]
  public class QuantumEditorHubWidgetBase : QuantumEditorHubWidget {
    public List<QuantumEditorHubWidget> StepElements;

    internal override void DeleteAllPlayerPrefKeys() {
      base.DeleteAllPlayerPrefKeys();

      foreach (var widget in StepElements) {
        widget.DeleteAllPlayerPrefKeys();
      }
    } 
  }

  [Serializable]
  public class QuantumEditorHubWidget {
    QuantumEditorHubWidgetTypeEnum? _widgetModeAsEnum = null;
    QuantumEditorHubConditionEnum? _hideConditionAsEnum = null;
    QuantumEditorHubConditionEnum? _autoCompleteConditionAsEnum = null;

    [Serializable]
    public class TypeInfo {
      public string ScriptableObject;
      public string Class;
    }

    internal class HubWidgetState {
      private SaveData _saveData;
      private string _playerPrefsKey;

      public bool IsHidden { get; set; }
      public bool IsAutoCompleted { get; set; }
      public int StepIndex { get; set; }
      public bool IsComplete => IsAutoCompleted || _saveData.IsMarkedCompleted || _saveData.IsMarkedSkipped;
      public bool IsDrawn => IsComplete == false && IsHidden == false;
      public string CachedString { get; set; }

      public HubWidgetState(string playerPrefsKey = "") {
        _playerPrefsKey = playerPrefsKey;
        _saveData = SaveData.Load(_playerPrefsKey);
      }

      public void ClearSaveData() {
        PlayerPrefs.DeleteKey(_playerPrefsKey);
        _saveData = new SaveData();
      }

      public void TrySetCompleteAndSave(bool isCompleted = true) {
        _saveData.IsMarkedCompleted = isCompleted;
        _saveData.Save(_playerPrefsKey);
      }

      public void TrySetSkippedAndSave(bool isSkipped = true) {
        _saveData.IsMarkedSkipped = isSkipped;
        _saveData.Save(_playerPrefsKey);
      }

      internal struct SaveData {
        public bool IsMarkedCompleted;
        public bool IsMarkedSkipped;

        public static SaveData Load(string key) {
          try {
            return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key, ""));
          } catch {
            return new SaveData();
          }
        }

        public void Save(string key) {
          PlayerPrefs.SetString(key, JsonUtility.ToJson(this));
        }
      }
    }

    [TextArea] public string Text;
    [TextArea] public string Subtext;
    public QuantumEditorHubWidgetType WidgetMode;
    public QuantumEditorHubCondition AutoComplete;
    public QuantumEditorHubCondition Hide;
    [ScenePath] public string Scene;
    public bool AddSceneToTop;
    public Texture2D Icon;
    public Texture2D StatusIcon;
    public bool StartPlayMode;
    public string Url;
    public TypeInfo Type;
    public LazyLoadReference<UnityEngine.Object> Asset;

    /// <summary>
    /// Hidden id is used for player prefs key to save step progress.
    /// </summary>
    [HideInInspector]
    public string Id = Guid.NewGuid().ToString();

    /// <summary>
    /// Internal state saves mutable data for the widget.
    /// </summary>
    internal HubWidgetState State { get; set; }


     internal void OnButtonClicked() {
       if (AutoCompleteAsEnum == QuantumEditorHubConditionEnum.ButtonClicked) {
         State?.TrySetCompleteAndSave();
       }
     }

     internal Texture2D GetStatusIcon(QuantumEditorHubWindow window) {
       return GetStatusIcon(window.CorrectIcon, window.MissingIcon);
     }

     internal Texture2D GetStatusIcon(Texture2D correctIcon, Texture2D missingIcon) {
       if (StatusIcon != null) {
         return StatusIcon;
       }

       if (AutoCompleteAsEnum == QuantumEditorHubConditionEnum.None) {
         return null;
       }

       if (State.IsComplete) {
         return correctIcon;
       }
       else {
         return missingIcon;
       }
     }    
    
    internal virtual void DeleteAllPlayerPrefKeys() {
      State?.ClearSaveData();
    }

    /// <summary>
    /// The mode is saved in string but can be cached into internal enum for convenience.
    /// </summary>
    internal QuantumEditorHubWidgetTypeEnum WidgetModeAsEnum {
      get {
        InitializeParsedEnum(ref _widgetModeAsEnum, WidgetMode.Value, QuantumEditorHubWidgetTypeEnum.Custom);
        return _widgetModeAsEnum.Value;
      }
    }

    internal QuantumEditorHubConditionEnum HideAsEnum {
      get {
        InitializeParsedEnum(ref _hideConditionAsEnum, Hide.Value, QuantumEditorHubConditionEnum.Custom);
        return _hideConditionAsEnum.Value;
      }
    }

    internal QuantumEditorHubConditionEnum AutoCompleteAsEnum {
      get {
        InitializeParsedEnum(ref _autoCompleteConditionAsEnum, AutoComplete.Value, QuantumEditorHubConditionEnum.Custom);
        return _autoCompleteConditionAsEnum.Value;
      }
    }

    static void InitializeParsedEnum<T>(ref T? value, string s, T defaultValue)  where T:struct, Enum {
      if (value.HasValue == false) {
        if (Enum.TryParse(typeof(T), s, out var parseResult)) {
          value = (T)parseResult;
        } else {
          value = defaultValue;
        }
      }
    }

    /// TODO: potentially slow 
    internal bool HasTypeAndTypeIsValid =>
      (string.IsNullOrEmpty(Type.ScriptableObject) == false && QuantumEditorHubWindow.HubUtils.FindType<ScriptableObject>(Type.ScriptableObject) != null) ||
      (string.IsNullOrEmpty(Type.Class) == false && QuantumEditorHubWindow.HubUtils.FindType<object>(Type.Class) != null);


    // TODO: potentially slow
    internal void UpdateState(QuantumEditorHubPage.CustomConditionCheck customConditionCheck) {
      State.IsHidden = IsConditionMatched(HideAsEnum, Hide, customConditionCheck);
      State.IsAutoCompleted = IsConditionMatched(AutoCompleteAsEnum, AutoComplete, customConditionCheck);
    } 

    internal bool IsConditionMatched(QuantumEditorHubConditionEnum conditionAsEnum, QuantumEditorHubCondition condition, QuantumEditorHubPage.CustomConditionCheck customConditionCheck) {
      switch (conditionAsEnum) {
        case QuantumEditorHubConditionEnum.Custom:
          return customConditionCheck(condition);

        case QuantumEditorHubConditionEnum.TypeIsValid:
          return HasTypeAndTypeIsValid;

        case QuantumEditorHubConditionEnum.TypeIsNotValid:
          return HasTypeAndTypeIsValid == false;

        case QuantumEditorHubConditionEnum.AssetMissing:
          return Asset.isSet && Asset.isBroken;

        case QuantumEditorHubConditionEnum.SceneView2D:
          return SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.in2DMode;

        case QuantumEditorHubConditionEnum.SceneView3D:
          return SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.in2DMode == false;

        case QuantumEditorHubConditionEnum.SceneExists:
          return File.Exists(Scene);

        case QuantumEditorHubConditionEnum.SceneNotExists:
          return File.Exists(Scene) == false;

        case QuantumEditorHubConditionEnum.DefineEnabled: {
            var define = AssetDatabaseUtils.HasScriptingDefineSymbol(Url);
            return define.HasValue && define.Value;
          }

        case QuantumEditorHubConditionEnum.DefineMissing: {
            var define = AssetDatabaseUtils.HasScriptingDefineSymbol(Url);
            return !(define.HasValue && define.Value);
          }

        case QuantumEditorHubConditionEnum.GlobalScriptableObjectExists: {
            if (string.IsNullOrEmpty(Type.ScriptableObject) == false) {
              Type globalObjectType = QuantumEditorHubWindow.HubUtils.FindType(Type.ScriptableObject);
              return QuantumEditorHubWindow.HubUtils.HasGlobalScriptableObjectCached(globalObjectType);
            }
            return false;
          }
      }

      return false;
    }
  }
}

#endregion


#region QuantumEditorHubWidgetType.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using System;
  using UnityEditor;
  using UnityEngine;
  using System.Linq;

  [Serializable]
  public struct QuantumEditorHubWidgetType {
    public string Value;
  }

  [Serializable]
  internal enum QuantumEditorHubWidgetTypeEnum {
    Text,
    SceneButton,
    LinkButton,
    PingAsset,
    PingGlobalScriptableObject,
    EnsureGlobalScriptableObjectExists,
    InstallPackage,
    ToggleDefine,
    Step,
    Changelog,
    Textfile,
    BuildInfoFile,
    AssemblyVersion,
    AppIdBox,
    ClearPlayerPrefs,
    LogLevel,
    Hierarchy,
    Image,
    Custom = 100,
  }


  [CustomPropertyDrawer(typeof(QuantumEditorHubWidgetType), true)]
  internal partial class QuantumEditorHubWidgetTypeDrawer : PropertyDrawer {
    static string[] _typeNames;

    static partial void RegisterTypesUser(List<string> types);

    [InitializeOnLoadMethod]
    static void InitializedPackageImportCallbacks() {
      var types = Enum.GetNames(typeof(QuantumEditorHubWidgetTypeEnum)).ToList();
      RegisterTypesUser(types);
      _typeNames = types.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      using (new QuantumEditorGUI.PropertyScope(position, label, property)) {
        EditorGUI.BeginChangeCheck();

        var p = property.FindPropertyRelativeOrThrow(nameof(QuantumEditorHubWidgetType.Value));
        var index = Array.IndexOf(_typeNames, p.stringValue);
        position = EditorGUI.PrefixLabel(position, label);
        var newIndex = Math.Max(0, EditorGUI.Popup(position, index, _typeNames));
        if (newIndex != index) {
          p.stringValue = _typeNames[newIndex];
        }

        if (EditorGUI.EndChangeCheck()) {
          property.serializedObject.ApplyModifiedProperties();
        }
      }
    }
  }
}

#endregion


#region QuantumEditorHubWindow.Draw.cs

namespace Quantum.Editor {
  using System;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  internal partial class QuantumEditorHubWindow {
    Quantum.Editor.LogSettingsDrawer _logSettingsDrawer;

    public void DrawButtonAction(Texture2D icon, string header, string description = null, bool enabled = true, Action callback = null, int? width = null, Texture2D statusIcon = null) {
      var height = IconSize + GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;

      // Draw text separately (not part of button guicontent) to have control over the space between the icon and the text.
      var rect = EditorGUILayout.GetControlRect(false, height, width.HasValue ? GUILayout.Width(width.Value) : GUILayout.ExpandWidth(true));

      var wasEnabled = GUI.enabled;
      GUI.enabled = enabled;
      bool clicked = GUI.Button(rect, icon, GUI.skin.button);
      GUI.enabled = wasEnabled;
      GUI.Label(new Rect(rect) {
        xMin = rect.xMin + IconSize + IconMargin * 2,
        xMax = rect.xMax - (statusIcon != null ? (IconSize + 20) : 0),
      }, description == null ? "<b>" + header + "</b>" : string.Format("<b>{0}</b>\n{1}", header, "<color=#aaaaaa>" + description + "</color>"));
      if (clicked && callback != null) {
        callback.Invoke();
      }

      if (statusIcon) {
        GUI.DrawTexture(new Rect(rect) {
          yMin = rect.yMin + (rect.height - StatusIconWidthDefault.y) / 2,
          xMin = rect.xMax - (StatusIconWidthDefault.x + IconMargin),
          width = StatusIconWidthDefault.y,
          height = StatusIconWidthDefault.x,
        }, statusIcon);
      }
    }

    public void DrawLogLevel(Texture2D icon, string text) {
      {
        var height = IconSize + GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
        var rect = EditorGUILayout.GetControlRect(false, height, GUILayout.ExpandWidth(true));
        GUI.Label(rect, icon, GetButtonPaneStyle);
        rect.xMin += IconSize + IconMargin * 2;

        GUI.Label(rect, string.Format(text, _logSettingsDrawer.GetActiveBuildTargetDefinedLogLevel()));

        rect.xMin += rect.width - 100;
        rect.width -= IconMargin;
        var newHeight = EditorStyles.popup.CalcSize(new GUIContent("T")).y;
        var newY = rect.y + rect.height / 2 - newHeight / 2;
        rect.y = newY;
        rect.height = newHeight;
        _logSettingsDrawer.DrawLogLevelEnum(rect);
      }
    }

    public void DrawSetupAppId() {
      // Getting server settings data
      var photonServerSettings = SdkAppSettingsAsset;
      var isAppIdValid = HubUtils.IsValidGuid(AppId);

      using (new EditorGUILayout.HorizontalScope(GetBoxStyle)) {
        GUILayout.Label("<b>App Id:</b>", GUILayout.Width(80));
        using (new EditorGUI.DisabledScope(photonServerSettings == null)) {
          using (new EditorGUILayout.HorizontalScope()) {
            EditorGUI.BeginChangeCheck();
            var editedAppId = EditorGUILayout.TextField("", AppId, HubSkin.textField, GUILayout.Height(StatusIconWidthDefault.y));
            if (EditorGUI.EndChangeCheck()) {
              AppId = editedAppId;
            }
          }
        }

        GUILayout.Label(GetStatusIcon(isAppIdValid), GUILayout.Width(StatusIconWidthDefault.x), GUILayout.Height(StatusIconWidthDefault.y));
      }
    }

    void DrawLeftNavMenu() {
      for (int i = 0; i < Pages.Count; ++i) {
        if (DrawNavButton(Pages[i], CurrentPage == i)) {
          CurrentPage = i;
          _scrollRect = Vector2.zero;
        }
      }
    }

    void DrawHeader() {
      GUILayout.Label(ProductLogo, Styles.NavbarHeaderGraphic);
    }

    void DrawFooter() {
      GUILayout.BeginHorizontal(HubSkin.window);
      GUILayout.Label("\u00A9 2024, Exit Games GmbH. All rights reserved.");
      GUILayout.EndHorizontal();
    }

    bool DrawNavButton(QuantumEditorHubPage section, bool currentSection) {
      var content = new GUIContent() {
        text = "  " + section.Title,
        image = section.Icon
      };

      var renderStyle = currentSection ? Styles.ButtonActive: GUI.skin.button;
      return GUILayout.Button(content, renderStyle, GUILayout.Height(NavButtonHeight), GUILayout.Width(NavButtonWidth));
    }
  }
}

#endregion


#region QuantumEditorHubWindow.Skin.cs

namespace Quantum.Editor {
  using UnityEngine;

  internal partial class QuantumEditorHubWindow {
    public static Vector2 StatusIconWidthDefault = new Vector2(24, 24);
    public static Vector2 StatusIconWidthLarge = new Vector2(32, 32);

    /// <summary>
    /// The Editor Hub Unity skin.
    /// </summary>
    public GUISkin HubSkin;
    /// <summary>
    /// The product logo.
    /// </summary>
    public Texture2D ProductLogo;
    /// <summary>
    /// The correct icon marking completed installation steps.
    /// </summary>
    public Texture2D CorrectIcon;
    /// <summary>
    /// The icon marking missing installation steps.
    /// </summary>
    public Texture2D MissingIcon;

    public virtual GUIStyle GetBoxStyle => HubSkin.GetStyle("Box");
    public virtual GUIStyle GetButtonPaneStyle => HubSkin.GetStyle("Button");

    public HubStyles Styles;

    public Texture2D GetStatusIcon(bool isValid) {
      return isValid ? CorrectIcon : MissingIcon;
    }

    public class HubStyles {
      public GUIStyle NavbarHeaderGraphic;
      public GUIStyle TextLabel;
      public GUIStyle HeaderLabel;
      public GUIStyle ReleaseNotes;
      public GUIStyle HeaderText;
      public GUIStyle ButtonActive;

      public HubStyles(GUISkin skin, GUIStyle boxStyle) {
        Color commonTextColor = Color.white;

        NavbarHeaderGraphic = new GUIStyle(boxStyle) { alignment = TextAnchor.MiddleCenter };

        HeaderText = new GUIStyle(skin.label) {
          fontSize = 18,
          padding = new RectOffset(12, 8, 8, 8),
          fontStyle = FontStyle.Bold,
          normal = { textColor = commonTextColor }
        };

        ButtonActive = new GUIStyle(skin.button) {
          fontStyle = FontStyle.Bold,
          normal = { background = skin.button.active.background, textColor = Color.white }
        };

        TextLabel = new GUIStyle(skin.label) {
          wordWrap = true,
          normal = { textColor = commonTextColor },
          richText = true,

        };

        HeaderLabel = new GUIStyle(TextLabel) {
          fontSize = 15,
        };

        ReleaseNotes = new GUIStyle(TextLabel) {
          richText = true,
        };
      }
    }
  }
}

#endregion


#region QuantumEditorHubWindow.Utils.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  internal partial class QuantumEditorHubWindow {
    public class HubUtils {
      static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
      internal static HashSet<Type> GlobalInstanceMissing = new();

      public static bool IsValidGuid(string appId) {
        try {
          return new Guid(appId) != null;
        } catch {
          return false;
        }
      }

      public static Type FindType<T>(string name) {
        if (_typeCache.TryGetValue(name, out var result)) {
          return result;
        }

        foreach (var t in TypeCache.GetTypesDerivedFrom<T>()) {
          if (string.Equals(t.Name, name, StringComparison.Ordinal)) {
            _typeCache.Add(name, t);
            return t;
          }
        }

        _typeCache.Add(name, null);
        return null;
      }


      public static Type FindType(string name) {
        if (_typeCache.TryGetValue(name, out var result)) {
          return result;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
          Type type = assembly.GetType(name);
          if (type != null) {
            _typeCache.Add(name, type);
            return type;
          }
        }

        _typeCache.Add(name, null);
        return null;
      }

      public static Action OpenURL(string url, params object[] args) {
        return () => {
          if (args.Length > 0) {
            url = string.Format(url, args);
          }

          Application.OpenURL(url);
        };
      }

      public static string BuildPath(params string[] parts) {
        var basePath = "";

        foreach (var path in parts) {
          basePath = Path.Combine(basePath, path);
        }

        return PathUtils.Normalize(basePath.Replace(Application.dataPath, Path.GetFileName(Application.dataPath)));
      }

      internal static bool TryGetGlobalScriptableObjectRefl(Type type, out QuantumGlobalScriptableObject result) {
        result = null;
        Type globalTypeWrapped = typeof(QuantumGlobalScriptableObject<>).MakeGenericType(type);
        MethodInfo tryGetGlobalMethod = globalTypeWrapped.GetMethod("TryGetGlobalInternal", BindingFlags.NonPublic | BindingFlags.Static);
        object[] parameters = new object[1];
        if ((bool)tryGetGlobalMethod.Invoke(null, parameters)) {
          result = parameters[0] as QuantumGlobalScriptableObject;
          return true;
        }

        return false;
      }

      internal static bool EnsureGlobalScriptableObjectExistsRefl(Type type) {
        MethodInfo tryGetGlobalMethod = typeof(QuantumGlobalScriptableObjectUtils).GetMethod("EnsureAssetExists")
          .MakeGenericMethod(type);
        return (bool)tryGetGlobalMethod.Invoke(null, null);
      }

      internal static bool HasGlobalScriptableObjectCached(Type type) {
        if (GlobalInstanceMissing.Contains(type)) {
          return false;
        } else {
          if (TryGetGlobalScriptableObjectRefl(type, out var globalScriptableObject)) {
            return true;
          } else {
            GlobalInstanceMissing.Add(type);
            return false;
          }
        }
      }

      internal static bool TryGetGlobalScriptableObjectCached<T>(out T result) where T : QuantumGlobalScriptableObject {
        if (TryGetGlobalScriptableObjectCached(typeof(T), out var globalScriptableObject)) {
          result = globalScriptableObject as T;
          return true;
        }
        result = null;
        return false;
      }


      internal static bool TryGetGlobalScriptableObjectCached(Type type, out QuantumGlobalScriptableObject result) {
        result = null;
        if (HasGlobalScriptableObjectCached(type)) {
          return HubUtils.TryGetGlobalScriptableObjectRefl(type, out result);
        }
        return false;
      }
    }
  }
}

#endregion



#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMapGizmoStylePopup.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.UIElements;

  /// <summary>
  /// Popup window for the map gizmo style.
  /// </summary>
  public class QuantumEditorMapGizmoStylePopup : PopupWindowContent {
    /// <summary>
    /// The content of the popup window.
    /// </summary>
    public VisualElement PopUpContent;

    /// <summary>
    /// Gets the size of the window.
    /// </summary>
    /// <returns></returns>
    public override Vector2 GetWindowSize() {
      return new Vector2(110, 120);
    }

    /// <summary>
    /// Called when the window is opened.
    /// </summary>
    public override void OnOpen() {
      editorWindow.rootVisualElement.Add(PopUpContent);
    }

    public override void OnGUI(Rect rect) {
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuCreateAssets.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Create Quantum asset and script files using the context menu.
  /// </summary>
  public static class QuantumEditorMenuCreateAssets {
    const string ScriptTemplateFolder = "Assets/Photon/Quantum/Editor/ScriptTemplates";
    
    [MenuItem("Assets/Create/Quantum/Qtn", false, priority: EditorDefines.AssetMenuPriorityScripts)]
    private static void CreateQtnFile() {
      // TODO: check if the folder belongs to the Quantum.Simulation assembly
      // TODO: find better folder when used from the menu instead of the contect menu
      //string clickedAssetGuid = Selection.assetGUIDs[0];
      //string clickedPath = AssetDatabase.GUIDToAssetPath(clickedAssetGuid)
      ProjectWindowUtil.CreateAssetWithContent("QuantumDefinition.qtn", string.Empty);
    }

    [MenuItem("Assets/Create/Quantum/System", false, priority: EditorDefines.AssetMenuPriorityScripts + 1)]
    private static void CreateQuantumSystemFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateSystem.cs.txt", "NewQuantumSystem.cs");
    }

    [MenuItem("Assets/Create/Quantum/SystemFilter", false, priority: EditorDefines.AssetMenuPriorityScripts + 2)]
    private static void CreateQuantumSystemFilterFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateSystemFilter.cs.txt", "NewQuantumSystem.cs");
    }

    [MenuItem("Assets/Create/Quantum/SystemSignalsOnly", false, priority: EditorDefines.AssetMenuPriorityScripts + 3)]
    private static void CreateQuantumSystemSignalsOnlyFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateSystemSignalsOnly.cs.txt", "NewQuantumSystem.cs");
    }

    [MenuItem("Assets/Create/Quantum/AssetScript", false, priority: EditorDefines.AssetMenuPriorityScripts + 4)]
    private static void CreateQuantumAssetScriptFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateAssetScript.cs.txt", "NewQuantumAsset.cs");
    }

    [MenuItem("Assets/Create/Quantum/InputScript", false, priority: EditorDefines.AssetMenuPriorityScripts + 5)]
    private static void CreateQuantumInputScriptFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateInputScript.cs.txt", "NewQuantumInput.cs");
    }

    [MenuItem("Assets/Create/Quantum/EntityViewContext", false, priority: EditorDefines.AssetMenuPriorityScripts + 6)]
    private static void CreateQuantumEntityViewContextFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateEntityViewContext.cs.txt", "NewQuantumEntityViewContext.cs");
    }

    [MenuItem("Assets/Create/Quantum/EntityViewComponent", false, priority: EditorDefines.AssetMenuPriorityScripts + 7)]
    private static void CreateQuantumEntityViewComponentFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateEntityViewComponent.cs.txt", "NewQuantumEntityViewComponent.cs");
    }

    [MenuItem("Assets/Create/Quantum/EntityViewComponent With Context", false, priority: EditorDefines.AssetMenuPriorityScripts + 8)]
    private static void CreateQuantumEntityViewComponentWithContextFile() {
      ProjectWindowUtil.CreateScriptAssetFromTemplateFile($"{ScriptTemplateFolder}/QuantumScriptTemplateEntityViewComponentWithContext.cs.txt", "NewQuantumEntityViewComponent.cs");
    }


    [MenuItem("Assets/Create/Quantum/Scene", false, priority: EditorDefines.AssetMenuPriorityScripts + 9)]
    private static void CreateQuantumScene() {
      if (Selection.assetGUIDs.Length > 0) {
        string clickedAssetGuid = Selection.assetGUIDs[0];
        string clickedPath = AssetDatabase.GUIDToAssetPath(clickedAssetGuid);
        QuantumEditorMenuCreateScene.CreateNewQuantumScene(clickedPath, null, true, true, false);
      } else {
        QuantumEditorMenuCreateScene.CreateNewQuantumScene(null, QuantumEditorSettings.Global.DefaultNewAssetsLocation, false, false, false);
      }
    }

    [MenuItem("Assets/Create/Quantum/Asset...", false, priority: EditorDefines.AssetMenuPriorityAssets)]
    private static void ShowPopupMenuItem(MenuCommand mc) {
      // need to access the current event directly
      var currentEvent = UnityInternal.Event.s_Current;

      Vector2 activatorPosition = default;

      if (currentEvent == null) {
        QuantumEditorLog.WarnInspector("Can't get mouse position for context menu");
      } else {
        activatorPosition = currentEvent.mousePosition;
      }

      var popupContent = new QuantumTypeSelectorPopupContent(t => {
        ProjectWindowUtil.CreateAsset(ScriptableObject.CreateInstance(t), $"New {ObjectNames.NicifyVariableName(t.Name)}.asset");
      });
      
      // only show types that can be saved as assets
      var assetTypes = TypeCache.GetTypesDerivedFrom<AssetObject>()
        .Where(x => !x.IsDefined(typeof(ObsoleteAttribute)))
        .Where(x => !x.IsAbstract && !x.IsGenericTypeDefinition)
        .Where(x => UnityInternal.EditorGUIUtility.GetScript(x.FullName))
        .ToList();
      
      GroupByBaseTypes(assetTypes, typeof(AssetObject), popupContent);
      
      PopupWindow.Show(new Rect(activatorPosition, new Vector2(100, EditorGUIUtility.singleLineHeight)), popupContent);
    }
    
    private static void GroupByBaseTypes(List<Type> types, Type baseType, QuantumTypeSelectorPopupContent popupContent) {
      // don't get down the hierarchy lower that the base type
      var lookup = types.ToLookup(x => x == baseType ? x : GetRootType(x, baseType))
        .OrderBy(x => x.Key.GetCSharpTypeName(includeNamespace: false));

      foreach (var group in lookup) {
        var label = group.Key.GetCSharpTypeName(includeNamespace: false);

        var subtypes = group.ToList();
        Debug.Assert(subtypes.Count > 0);

        if (subtypes.Count > 1 || subtypes[0] != group.Key) {
          // this item needs subitems
          popupContent.BeginGroup(label);
          GroupByBaseTypes(subtypes, group.Key, popupContent);
          popupContent.EndGroup();
        } else {
          popupContent.AddType(label, subtypes[0]);
        }
      }
      
      Type GetRootType(Type t, Type mostBaseType) {
        while (t.BaseType != mostBaseType) {
          t = t.BaseType;
          Debug.Assert(t != null);
        }
        return t;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuCreateScene.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Quantum.Demo;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using static UnityEngine.Object;
  using static QuantumUnityExtensions;

  /// <summary>
  /// Utility methods to create and set up a Quantum Unity scene.
  /// </summary>
  public static class QuantumEditorMenuCreateScene {
    /// <summary>
    /// Create a new empty Quantum game scene and Quantum map asset.
    /// </summary>
    [MenuItem("Tools/Quantum/Setup/Create New Quantum Scene", false, (int)QuantumEditorMenuPriority.Setup + 0)]
    public static void CreateNewQuantumScene() => CreateNewQuantumScene(null, null, false, true, false);

    /// <summary>
    /// Create and save a new empty Quantum game scene and Quantum map asset inside <see cref="QuantumEditorSettings.DefaultNewAssetsLocation"/>.
    /// </summary>
    [MenuItem("Tools/Quantum/Setup/Create New Quantum Scene (Save Scene)", false, (int)QuantumEditorMenuPriority.Setup + 1)]
    public static void CreateAndSaveNewQuantumScene() => CreateNewQuantumScene(null, null, true, true, false);

    /// <summary>
    /// Create a new empty Quantum game scene and Quantum map asset.
    /// </summary>
    /// <param name="scenePath">Path to the scene asset, can be null to store by default name under QuantumEditorSettings.DefaultNewAssetsLocation</param>
    /// <param name="mapAssetPath">The path to the map asset that is created with the scene</param>
    /// <param name="saveScene">Save the scene</param>
    /// <param name="addToBuildSettings">Add the scene to build settings</param>
    /// <param name="createSceneInfoAsset"></param>
    public static void CreateNewQuantumScene(string scenePath, string mapAssetPath, bool saveScene, bool addToBuildSettings, bool createSceneInfoAsset) {
      if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
        var folderPath = Path.HasExtension(scenePath) ? Path.GetDirectoryName(scenePath) : scenePath;
        var sceneName = (Path.HasExtension(scenePath) ? Path.GetFileNameWithoutExtension(scenePath) : null) ?? "NewQuantumGameScene";

        if (saveScene && string.IsNullOrEmpty(folderPath)) {
          folderPath = EditorUtility.OpenFolderPanel("Quantum Scene Destination", QuantumEditorSettings.Global.DefaultNewAssetsLocation, "");
        }

        if (string.IsNullOrEmpty(folderPath) == false) {
          // Always make folder path relative
          folderPath = Path.GetRelativePath(PathUtils.Normalize(Path.GetFullPath($"{Application.dataPath}/..")), PathUtils.Normalize(Path.GetFullPath(folderPath)));
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var quantumMap = SetupNewQuantumScene(mapAssetPath, null);

        if (saveScene && string.IsNullOrEmpty(folderPath) == false) {
          var newScenePath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{sceneName}.unity");
          if (EditorSceneManager.SaveScene(scene, newScenePath)) {
            if (addToBuildSettings) {
              AddSceneToBuildSettings(scene);
            }
            QuantumEditorLog.Log("Created new Quantum game scene", AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
          }

          QuantumMapDataBaker.BakeMapData(quantumMap, true);
        }

        if (createSceneInfoAsset) {
          var info = ScriptableObject.CreateInstance<QuantumMenuSceneInfo>();
          QuantumMenuConfigEditor.SetToCurrentScene(info, null);
          AssetDatabase.CreateAsset(info, $"{Path.GetDirectoryName(mapAssetPath)}/{Path.GetFileNameWithoutExtension(mapAssetPath)}SceneInfo.asset");
        }
      }
    }

    /// <summary>
    /// Create the simple connection sample scene.
    /// </summary>
    /// <param name="scenePath">Path to scene to be created</param>
    public static void CreateSimpleConnectionScene(string scenePath) {
      QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs);
      Assert.Always(defaultConfigs != null, "No global QuantumDefaultConfigs found.");

      var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

      var go = new GameObject("SimpleConnectGUI");
      var component = go.AddComponent<QuantumSimpleConnectionGUI>();
      component.RuntimeConfig = new RuntimeConfig {
        Map = QuantumUnityDB.FindGlobalAssetGuids(typeof(Map)).FirstOrDefault(),
        SimulationConfig = defaultConfigs.SimulationConfig,
        SystemsConfig = defaultConfigs.SystemsConfig
      };

      var newScenePath = AssetDatabase.GenerateUniqueAssetPath(scenePath);
      if (EditorSceneManager.SaveScene(scene, newScenePath)) {
        AddSceneToBuildSettings(scene);
      }

      QuantumEditorLog.Log("Created new Quantum simple connection sample scene", AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
    }

    /// <summary>
    /// Setup a new Quantum scene.
    /// </summary>
    [MenuItem("GameObject/Quantum/Add Quantum To Current Scene", priority = 100)]
    [MenuItem("Tools/Quantum/Setup/Add Quantum To Current Scene", false, (int)QuantumEditorMenuPriority.Setup + 2)]
    public static void SetupNewQuantumScene() => SetupNewQuantumScene(null, null);

    /// <summary>
    /// Setup a new Quantum scene and save the Map asset at the propsed path.
    /// </summary>
    /// <param name="mapAssetPath">Map asset folder or path, can be null to store under <see cref="QuantumEditorSettings.DefaultNewAssetsLocation"/></param>
    /// <param name="runtimeConfig">Runtime Config to assign to the debug runner, can be null</param>
    public static QuantumMapData SetupNewQuantumScene(string mapAssetPath, RuntimeConfig runtimeConfig) {
      if (FindAnyObjectByType<QuantumEntityViewUpdater>() == null) {
        var entityViewUpdaterGameObject = new GameObject("QuantumEntityViewUpdater");
        entityViewUpdaterGameObject.AddComponent<QuantumEntityViewUpdater>();
      }

      var debugRunner = FindAnyObjectByType<QuantumRunnerLocalDebug>();
      if (debugRunner == null) {
        var debugRunnerGameObject = new GameObject("QuantumDebugRunner");
        debugRunner = debugRunnerGameObject.AddComponent<QuantumRunnerLocalDebug>();
      }

      debugRunner.RuntimeConfig = runtimeConfig ?? new RuntimeConfig();
      if (QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs)) {
        // Make sure to at least set default configs
        if (debugRunner.RuntimeConfig.SimulationConfig.IsValid == false) {
          debugRunner.RuntimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
        }
        if (debugRunner.RuntimeConfig.SystemsConfig.IsValid == false) {
          debugRunner.RuntimeConfig.SystemsConfig = defaultConfigs.SystemsConfig;
        }
      }

      var quantumMap = FindAnyObjectByType<QuantumMapData>();
      if (quantumMap == null) {
        var mapGameObject = new GameObject("QuantumMap");
        quantumMap = mapGameObject.AddComponent<QuantumMapData>();
      }

      if (FindAnyObjectByType<QuantumMemoryStats>() == null) {
        var quantumStatsAsset = Resources.Load<QuantumStats>("QuantumStats");
        var go = PrefabUtility.InstantiatePrefab(quantumStatsAsset);
        go.name = "QuantumStats";
      }

      if (FindAnyObjectByType<QuantumDebugInput>() == null) {
        var debugInputGameobject = new GameObject("QuantumDebugInput");
        debugInputGameobject.AddComponent<QuantumDebugInput>();
      }

      var mapAsset = ScriptableObject.CreateInstance<Quantum.Map>();
      var mapAssetDir = (Path.HasExtension(mapAssetPath) ? Path.GetDirectoryName(mapAssetPath) : mapAssetPath) ?? QuantumEditorSettings.Global.DefaultNewAssetsLocation;
      var mapAssetName = (Path.HasExtension(mapAssetPath) ? Path.GetFileNameWithoutExtension(mapAssetPath) : null) ?? "NewQuantumMap";
      mapAssetPath = PathUtils.Normalize($"{mapAssetDir}/{mapAssetName}.asset");
      AssetDatabase.CreateAsset(mapAsset, AssetDatabase.GenerateUniqueAssetPath(mapAssetPath));
      AssetDatabase.Refresh();
      EditorUtility.SetDirty(mapAsset);

      debugRunner.RuntimeConfig.Map = mapAsset;
      quantumMap.AssetRef = mapAsset;

      QuantumUnityDBUtilities.RefreshGlobalDB();

      QuantumEditorLog.Log("Created new Quantum map asset", AssetDatabase.LoadAssetAtPath<Map>(mapAssetPath));

      return quantumMap;
    }

    /// <summary>
    /// Add the current scene to the build settings.
    /// </summary>
    [MenuItem("Tools/Quantum/Setup/Add Current Scene To Build Settings", false, (int)QuantumEditorMenuPriority.Setup + 3)]
    public static void AddCurrentSceneToSettings() { DirtyAndSaveScene(SceneManager.GetActiveScene()); }

    /// <summary>
    /// Add a scene to the build settings.
    /// </summary>
    /// <param name="scene">Scene handle</param>
    public static void DirtyAndSaveScene(Scene scene) {

      EditorSceneManager.MarkSceneDirty(scene);
      var scenename = scene.path;

      // Give chance to save - required in order to build out. If users cancel will only be able to run in the editor.
      if (scenename == "") {
        EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new Scene[] { scene });
        scenename = scene.path;
      }

      // Add scene to Build and Fusion settings
      if (scenename != "") {
        scene.AddSceneToBuildSettings();
      }
    }

    /// <summary>
    /// Add the scene to the build settings.
    /// </summary>
    /// <param name="scene">Scene handle</param>
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
        QuantumEditorLog.Log($"Added '{scene.path}' as first entry in Build Settings.");
        EditorBuildSettings.scenes = buildList.ToArray();
      }
    }

    /// <summary>
    /// Add a scene path to the build settings
    /// </summary>
    /// <param name="scenePath">Path to the scene</param>
    /// <param name="addToTop">Add the new scene to the top</param>
    public static void AddScenePathToBuildSettings(string scenePath, bool addToTop) {
      var editorBuildSettingsScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
      if (editorBuildSettingsScenes.FindIndex(s => s.path.Equals(scenePath, StringComparison.Ordinal)) < 0) {
        if (addToTop) {
          editorBuildSettingsScenes.Insert(0, new EditorBuildSettingsScene { path = scenePath, enabled = true });
        } else {
          editorBuildSettingsScenes.Add(new EditorBuildSettingsScene { path = scenePath, enabled = true });
        }
        EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
      }
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuCustomPlugin.cs

namespace Quantum.Editor {
  using System;
  using System.IO;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Methods to export Quantum assets and configs.
  /// </summary>
  public class QuantumEditorMenuCustomPlugin {
    [MenuItem("Tools/Quantum/Export/SessionConfig (Selected)", true, (int)QuantumEditorMenuPriority.Export + 11)]
    private static bool ExportSessionConfigCheck() { 
      if (Selection.activeObject == null) {
        return false;
      }

      if (Selection.activeObject.GetType() != typeof(QuantumDeterministicSessionConfigAsset)) {
        return false;
      }

      return true;
    }

    [MenuItem("Tools/Quantum/Export/SessionConfig (Selected)", false, (int)QuantumEditorMenuPriority.Export + 11)]
    private static void ExportSessionConfig() {
      var instance = Selection.activeObject as QuantumDeterministicSessionConfigAsset;
      if (instance?.Config != null) {
        // Make a copy
        var copy = DeterministicSessionConfig.FromByteArray(DeterministicSessionConfig.ToByteArray(instance.Config));

        // Calcuate fixed size
        var stream = new FrameSerializer(DeterministicFrameSerializeMode.Serialize, null, 1024);
        stream.Writing = true;
        stream.InputMode = true;
        Quantum.Input.Write(stream, new Quantum.Input());
        copy.InputFixedSize = stream.ToArray().Length;

        // Guess player count if not set, yet
        if (copy.PlayerCount == 0) {
          copy.PlayerCount = Quantum.Input.MAX_COUNT;
        }

        // Save to disk
        var path = EditorUtility.SaveFilePanel("Save", Application.dataPath, "SessionConfig", "json");
        if (path != null) {
          File.WriteAllText(path, JsonUtility.ToJson(copy, true));
        }
      }
    }

    [MenuItem("Tools/Quantum/Export/RuntimeConfig", true, (int)QuantumEditorMenuPriority.Export + 1)]
    private static bool ExportRuntimeConfigCheck() => Application.isPlaying && QuantumRunner.DefaultGame != null;

    [MenuItem("Tools/Quantum/Export/RuntimeConfig", false, (int)QuantumEditorMenuPriority.Export + 11)]
    private static void ExportRuntimeConfig() {
      var filePath = EditorUtility.SaveFilePanel("Export Runtime Config", Application.dataPath, "RuntimeConfig", "json");
      if (string.IsNullOrEmpty(filePath)) {
        return;
      }

      using (var file = File.Create(filePath)) {
        QuantumRunner.DefaultGame.AssetSerializer.SerializeConfig(file, QuantumRunner.DefaultGame.Configurations.Runtime);
      }
    }

    [Obsolete("Use QuantumDotnetBuildSettings")]
    //[MenuItem("Tools/Quantum/Export/Custom Plugin Assets", false, (int)QuantumEditorMenuPriority.Export + 3)]
    public static void ExportForCustomPlugin() {
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuDllToggle.cs

namespace Quantum.Editor {
  using System;
  using System.Diagnostics;
  using System.IO.Compression;
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;

  /// <summary>
  /// A static utility class that enabled Unity menu methods to toggle Quantum Debug and Release dlls by extracting a zip archive and overwriting the libraries.
  /// In earlier versions this was handed by the QUANTUM_DEBUG define and multiple version of dlls but the Quantum.Engine.dll has internal MonoBehaviours 
  /// that lose the script guids this way.
  /// </summary>
  [InitializeOnLoad]
  public static class QuantumEditorMenuDllToggle {
    /// <summary>
    /// Directory where the Quantum DLLs (Debug or Release) are extracted to.
    /// </summary>
    public const string ExtractToDirectory = "Assets/Photon/Quantum/Assemblies";

    const string DebugPackageTemplate = "Assets/Photon/Quantum/Assemblies/Quantum.{0}.zip";

    static string GetAssemblyFileVersion<T>() {
      try {
        var codeBase = Assembly.GetAssembly(typeof(T)).CodeBase;
        var path = Uri.UnescapeDataString(new UriBuilder(codeBase).Path);
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
        return fileVersionInfo.ProductVersion;
      } catch { }

      return string.Empty;
    }

    static bool? isQuantumDeterministicDllDebug;
    static bool? isQuantumEngineDllDebug;

    /// <summary>
    /// Checks if the Debug version of Quantum.Deterministic.dll is being used.
    /// </summary>
    public static bool IsQuantumDeterministicDllDebug => isQuantumDeterministicDllDebug ??= GetAssemblyFileVersion<FP>().Contains("Debug");

    /// <summary>
    /// Checks if the Debug version of Quantum.Engine.dll is being used.
    /// </summary>
    public static bool IsQuantumEngineDllDebug => isQuantumEngineDllDebug ??= GetAssemblyFileVersion<Transform2D>().Contains("Debug");

    /// <summary>
    /// Checks if any of the Quantum DLLs are NOT Debug.
    /// Use this to check if <see cref="SetToDebug"/> needs to be called in order to switch to Debug.
    /// </summary>
    /// <returns>
    /// <c>true</c> if either Quantum.Deterministic or Quantum.Engine.dll are NOT Debug.
    /// <c>false</c> otherwise.
    /// </returns>
    /// <example>
    /// <code>
    /// if (QuantumEditorMenuDllToggle.SetToDebugCheck()) {
    ///   QuantumEditorMenuDllToggle.SetToDebug();
    /// }
    /// </code>
    /// </example>
    [MenuItem("Tools/Quantum/Toggle Debug Dlls/Debug", priority = (int)QuantumEditorMenuPriority.BOTTOM + 1, validate = true)]
    public static bool SetToDebugCheck() => !IsQuantumDeterministicDllDebug || !IsQuantumEngineDllDebug;

    /// <summary>
    /// Extracts the Debug versions of Quantum DLLs in the <see cref="ExtractToDirectory">specified directory</see>.
    /// </summary>
    [MenuItem("Tools/Quantum/Toggle Debug Dlls/Debug", priority = (int)QuantumEditorMenuPriority.BOTTOM + 1)]
    public static void SetToDebug() {
      ZipFile.ExtractToDirectory(string.Format(DebugPackageTemplate, "Debug"), ExtractToDirectory, overwriteFiles: true);
      isQuantumDeterministicDllDebug = null;
      isQuantumEngineDllDebug = null;
      AssetDatabase.Refresh();
    }

    /// <summary>
    /// Checks if any of the Quantum DLLs are Debug.
    /// Use this to check if <see cref="SetToRelease"/> needs to be called in order to switch to Release.
    /// </summary>
    /// <returns>
    /// <c>true</c> if either Quantum.Deterministic or Quantum.Engine.dll are Debug.
    /// <c>false</c> otherwise.
    /// </returns>
    /// <example>
    /// <code>
    /// if (QuantumEditorMenuDllToggle.SetToReleaseCheck()) {
    ///   QuantumEditorMenuDllToggle.SetToRelease();
    /// }
    /// </code>
    /// </example>
    [MenuItem("Tools/Quantum/Toggle Debug Dlls/Release", priority = (int)QuantumEditorMenuPriority.BOTTOM + 2, validate = true)]
    public static bool SetToReleaseCheck() => IsQuantumDeterministicDllDebug || IsQuantumEngineDllDebug;

    /// <summary>
    /// Extracts the Release versions of Quantum DLLs in the <see cref="ExtractToDirectory">specified directory</see>.
    /// </summary>
    [MenuItem("Tools/Quantum/Toggle Debug Dlls/Release", priority = (int)QuantumEditorMenuPriority.BOTTOM + 2)]
    public static void SetToRelease() {
      ZipFile.ExtractToDirectory(string.Format(DebugPackageTemplate, "Release"), ExtractToDirectory, overwriteFiles: true);
      isQuantumDeterministicDllDebug = null;
      isQuantumEngineDllDebug = null;
      AssetDatabase.Refresh();
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuLookUpTables.cs

namespace Quantum.Editor {
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Quantum Look up table tools.
  /// </summary>
  public class QuantumEditorMenuLookUpTables {
    /// <summary>
    /// Generate the math lookup tables.
    /// </summary>
    [MenuItem("Tools/Quantum/Setup/Generate Math Lookup Tables", priority = (int)QuantumEditorMenuPriority.Setup + 14)]
    public static void Generate() {
      FPLut.GenerateTables(Quantum.PathUtils.Combine(Application.dataPath, "Photon/Quantum/Resources/LUT"));

      // this makes sure the tables are loaded into unity
      AssetDatabase.Refresh();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuPriority.cs

//file removed

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorMenuProfilers.cs

namespace Quantum.Editor {
  using Quantum.Profiling;
  using UnityEditor;
  using UnityEngine;
  using static UnityEngine.Object;
  using static QuantumUnityExtensions;

  /// <summary>
  /// Utility methods to create and set up a Quantum Unity scene.
  /// </summary>
  public static class QuantumEditorMenuProfilers {
    const string ProfilerPrefabGuid = "e7b1355f609cb304da5529115986eb8b";
    const string QuantumStatsPrefabGuid = "9e5addbaa78b7264889bf147e593db91";

    /// <summary>
    /// Find the graph profiler prefab and instantiate it in the current scene.
    /// </summary>
    [MenuItem("Tools/Quantum/Profilers/Add Graph Profilers Prefab", false, (int)QuantumEditorMenuPriority.Profilers + 0)]
    public static void AddGraphProfilersToCurrentScene() {
      var profiler = FindAnyObjectByType<QuantumGraphProfilingTools>();
      if (profiler != null) {
        Debug.LogWarning("QuantumGraphProfilers already exist in the scene.", profiler);
        return;
      }

      var path = AssetDatabase.GUIDToAssetPath(ProfilerPrefabGuid);
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
      var go = GameObject.Instantiate(prefab);
      go.name = "QuantumGraphProfilers";
    }

    /// <summary>
    /// Find the Quantum stats prefab and instantiate it in the current scene.
    /// </summary>
    [MenuItem("Tools/Quantum/Profilers/Add Quantum Stats Prefab", false, (int)QuantumEditorMenuPriority.Profilers + 1)]
    public static void AddQuantumStatsToCurrentScene() {
      var stats = FindAnyObjectByType<QuantumMemoryStats>();
      if (stats != null) {
        Debug.LogWarning("QuantumStats already exist in the scene.", stats);
        return;
      }

      var path = AssetDatabase.GUIDToAssetPath(QuantumStatsPrefabGuid);
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
      var go = GameObject.Instantiate(prefab);
      go.name = "QuantumStats";
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorObjectFactory.cs

namespace Quantum.Editor {
  using System.Runtime.CompilerServices;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  static partial class QuantumEditorObjectFactory {

    private static Mesh _circleMesh;
    private static Mesh CircleMesh => LoadCircleMesh(ref _circleMesh);

    private static Mesh _quadMesh;
    private static Mesh QuadMesh => LoadQuadMesh(ref _quadMesh);

    [MenuItem("GameObject/Quantum/Empty Entity", false, 11)]
    private static void CreateEntity(MenuCommand mc) => new GameObject()
      .ThenAdd<QuantumEntityPrototype>(x => x.TransformMode = QuantumEntityPrototypeTransformMode.None)
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);

    [MenuItem("GameObject/Quantum/2D/Sprite Entity", false, 10)]
#if QUANTUM_XY
    private static GameObject CreateSpriteEntity(MenuCommand mc) => new GameObject()
      .ThenAdd<SpriteRenderer>()
      .ThenAdd<QuantumEntityPrototype>(x => x.TransformMode = QuantumEntityPrototypeTransformMode.Transform2D)
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);
#else
    private static GameObject CreateSpriteEntity(MenuCommand mc) => new GameObject()
      .ThenAlter<Transform>(x => {
        var child = new GameObject("Sprite");
        child.AddComponent<SpriteRenderer>();
        child.transform.rotation = Quaternion.AngleAxis(90.0f, Vector3.right);
        child.transform.SetParent(x, false);
      })
      .ThenAdd<QuantumEntityPrototype>(x => x.TransformMode = QuantumEntityPrototypeTransformMode.Transform2D)
      .ThenAdd<QuantumEntityView>()
      .Finish(mc, select: "Sprite");
#endif

    [MenuItem("GameObject/Quantum/2D/Quad Entity", false, 10)]
    private static GameObject CreateQuadEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = QuadMesh)
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform2D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape2D = new Shape2DConfig() {
          BoxExtents = FP._0_50 * FPVector2.One,
          ShapeType = Shape2DType.Box,
        };
      })
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);


    [MenuItem("GameObject/Quantum/2D/Circle Entity", false, 10)]
    private static GameObject CreateCircleEntity2D(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = CircleMesh)
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform2D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape2D = new Shape2DConfig() {
          CircleRadius = FP._0_50,
          ShapeType = Shape2DType.Circle,
        };
      })
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);

    [MenuItem("GameObject/Quantum/2D/Capsule Entity", false, 10)]
    private static GameObject CreateCapsuleEntity2D(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Capsule)
      .ThenRemove<Collider>()
      .ThenRemove<MeshFilter>()
      .ThenRemove<MeshRenderer>()
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform2D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape2D = new Shape2DConfig() {
          CapsuleSize = new FPVector2(1, 2),
          ShapeType = Shape2DType.Capsule,
        };
      })
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D

    [MenuItem("GameObject/Quantum/2D/Static Quad Collider", false, 10)]
    private static GameObject CreateQuadStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = QuadMesh)
      .ThenAdd<QuantumStaticBoxCollider2D>(x => x.Size = FPVector2.One)
      .Finish(mc);

    [MenuItem("GameObject/Quantum/2D/Static Circle Collider", false, 10)]
    private static GameObject CreateCircleStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAlter<MeshFilter>(x => x.sharedMesh = CircleMesh)
      .ThenAdd<QuantumStaticCircleCollider2D>(x => x.Radius = FP._0_50)
      .Finish(mc);

    [MenuItem("GameObject/Quantum/2D/Static Capsule Collider", false, 10)]
    private static GameObject CreateCapsuleStaticCollider2D(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Capsule)
      .ThenRemove<Collider>()
      .ThenRemove<MeshFilter>()
      .ThenRemove<MeshRenderer>()
      .ThenAdd<QuantumStaticCapsuleCollider2D>(x =>{ x.Size = new FPVector2(1,2);})
      .Finish(mc);

#endif

    [MenuItem("GameObject/Quantum/3D/Box Entity", false, 10)]
    private static GameObject CreateBoxEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape3D = new Shape3DConfig() {
          BoxExtents = FP._0_50 * FPVector3.One,
          ShapeType = Shape3DType.Box,
        };
      })
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);

    [MenuItem("GameObject/Quantum/3D/Sphere Entity", false, 10)]
    private static GameObject CreateSphereEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape3D = new Shape3DConfig() {
          SphereRadius = FP._0_50,
          ShapeType = Shape3DType.Sphere,
        };
      })
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);

    [MenuItem("GameObject/Quantum/3D/Capsule Entity", false, 10)]
    private static GameObject CreateCapsuleEntity(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Capsule)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape3D = new Shape3DConfig() {
          CapsuleRadius = FP._0_50,
          CapsuleHeight = FP._2,
          ShapeType = Shape3DType.Capsule,
        };
      })
      .ThenAdd<QuantumEntityView>()
      .Finish(mc);

#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D

    [MenuItem("GameObject/Quantum/3D/Character Controller Entity", false, 10)]
    private static GameObject CreateCharacterControllerEntity(MenuCommand mc) =>
      ObjectFactory.CreateGameObject("")
      .ThenAdd<QuantumEntityPrototype>(x => {
        x.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
        x.PhysicsCollider.IsEnabled = true;
        x.PhysicsCollider.Shape3D = new Shape3DConfig() {
          SphereRadius = FP._0_50,
#if QUANTUM_XY
          PositionOffset = new FPVector3(0, 0, FP._0_50),
#else
          PositionOffset = new FPVector3(0, FP._0_50, 0),
#endif
          ShapeType = Shape3DType.Sphere,
        };
      })
      .ThenAdd<QPrototypeCharacterController3D>()
      .ThenAlter<QPrototypeCharacterController3D>(x => x.Prototype.Config.Id = AssetGuid.Invalid)
      .ThenAdd<QuantumEntityView>()
      .ThenAdd(ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
        .ThenRemove<Collider>()
#if QUANTUM_XY
          .ThenAlter<Transform>(x => x.position = new Vector3(0, 0, 0.5f))
#else
          .ThenAlter<Transform>(x => x.position = new Vector3(0, 0.5f, 0))
#endif
        )
      .Finish(mc);

    [MenuItem("GameObject/Quantum/3D/Static Box Collider", false, 10)]
    private static GameObject CreateBoxStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticBoxCollider3D>(x => x.Size = FPVector3.One)
      .Finish(mc);


    [MenuItem("GameObject/Quantum/3D/Static Sphere Collider", false, 10)]
    private static GameObject CreateSphereStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Sphere)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticSphereCollider3D>(x => x.Radius = FP._0_50)
      .Finish(mc);

    [MenuItem("GameObject/Quantum/3D/Static Capsule Collider", false, 10)]
    private static GameObject CreateCapsuleStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Capsule)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticCapsuleCollider3D>(x =>{ x.Radius = FP._0_50; x.Height = FP._2;})
      .Finish(mc);

    [MenuItem("GameObject/Quantum/3D/Static Mesh Collider", false, 10)]
    private static GameObject CreateMeshStaticCollider(MenuCommand mc) => ObjectFactory.CreatePrimitive(PrimitiveType.Cube)
      .ThenRemove<Collider>()
      .ThenAdd<QuantumStaticMeshCollider3D>()
      .Finish(mc);

#endif

    private static GameObject ThenRemove<T>(this GameObject go) where T : Component {
      UnityEngine.Object.DestroyImmediate(go.GetComponent<T>());
      return go;
    }

    private static GameObject ThenAdd<T>(this GameObject go, System.Action<T> callback = null) where T : Component {
      var component = go.AddComponent<T>();
      callback?.Invoke(component);
      return go;
    }

    private static GameObject ThenAdd(this GameObject parent, GameObject child) {
      // Finish deals with the creation and reparenting Undo
      child.Finish(parent, name: child.name);
      return parent;
    }

    private static GameObject ThenAlter<T>(this GameObject go, System.Action<T> callback) where T : Component {
      var component = go.GetComponent<T>();
      callback(component);
      return go;
    }

    private static GameObject Finish(this GameObject go, GameObject parent, string select = null, [CallerMemberName] string callerName = null, string name = null) {
      if (name != null) {
        go.name = name;
      }
      else {
        Debug.Assert(callerName.StartsWith("Create"));
        go.name = callerName.Substring("Create".Length);
      }

      // undo updated to match pattern here:
      // https://docs.unity3d.com/ScriptReference/Undo.RegisterCreatedObjectUndo.html
      // to fix crashes with Undo / Redo
      Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

      if (parent != null) {
        Undo.SetTransformParent(go.transform, parent.transform, "Set parent");
      }

      if (!string.IsNullOrEmpty(select)) {
        Selection.activeObject = go.transform.Find(select)?.gameObject;
      } else {
        Selection.activeObject = go;
      }

      return go;
    }

    private static GameObject Finish(this GameObject go, MenuCommand mc, string select = null, [CallerMemberName] string callerName = null) {
      var parent = mc.context as GameObject;
      return Finish(go, parent, select, callerName);
    }

    private static Mesh LoadCircleMesh(ref Mesh field) {
#if QUANTUM_XY
      return QuantumMeshCollection.Global.CircleXY;
#else
      return QuantumMeshCollection.Global.Circle;
#endif
    }

    private static Mesh LoadQuadMesh(ref Mesh field) {
#if QUANTUM_XY
      return QuantumMeshCollection.Global.QuadXY;
#else
      return QuantumMeshCollection.Global.Quad;
#endif
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorSessionShutdown.cs

namespace Quantum.Editor {
  using UnityEditor;

  /// <summary>
  /// An Unity Editor hook to shutdown all Quantum sessions when the editor play mode is stopped.
  /// </summary>
  [InitializeOnLoad]
  public class QuantumEditorSessionShutdown {
    static QuantumEditorSessionShutdown() {
      EditorApplication.update += EditorUpdate;
    }

    static void EditorUpdate() {
      if (EditorApplication.isPlaying == false)
        QuantumRunner.ShutdownAll();
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorSkin.Partial.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine;

  partial class QuantumEditorSkin {
    public static readonly LazyAsset<Texture2D> ScriptableObjectIcon = LazyAsset.Create(() => FindTextureOrThrow("ScriptableObject Icon"));
    public static readonly LazyAsset<Texture2D> _2DIcon              = LazyAsset.Create(() => FindTextureOrThrow("d_PositionAsUV1 Icon"));
    public static readonly LazyAsset<Texture2D> ConsoleIcon          = LazyAsset.Create(() => FindTextureOrThrow("UnityEditor.ConsoleWindow@2x"));
    public static readonly LazyGUIStyle         ScriptTextStyle      = new LazyGUIStyle(_ => new GUIStyle("ScriptText"));
    public static readonly LazyAsset<Texture2D> QuantumIcon          = LazyAsset.Create(() => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Photon/Quantum/Editor/EditorResources/QuantumEditorTextureQtnIcon.png"));
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorToolbarUtilities.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.UIElements;

  /// <summary>
  /// An editor script that places a drop down selection to quickly load Unity scenes, which are listed in the BuildSettings.
  /// To disabled the toolbar toggle <see cref="QuantumEditorSettings.UseQuantumToolbarUtilities"/>.
  /// </summary>
  [InitializeOnLoad]
  public class QuantumEditorToolbarUtilities {
    private static ScriptableObject _toolbar;
    private static List<string>     _scenePaths;
    private static string[]         _sceneNames;
    private static HashSet<string>  _renamedMap;
    private static bool             _hasEditorSettings = true;

    static QuantumEditorToolbarUtilities() {
      EditorApplication.delayCall += () => {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
      };
    }

    private static void Update() {
      if (!_hasEditorSettings) {
        return;
      }
      
      if (QuantumEditorSettings.Get(x => x.UseQuantumToolbarUtilities) != true) {
        _hasEditorSettings = false;
        return;
      }

      if (_toolbar == null) {
        Assembly editorAssembly = typeof(UnityEditor.Editor).Assembly;

        UnityEngine.Object[] toolbars = UnityEngine.Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.Toolbar"));
        _toolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;
        if (_toolbar != null) {
          var root = _toolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
          var rawRoot = root.GetValue(_toolbar);
          var mRoot = rawRoot as VisualElement;
          RegisterCallback(QuantumEditorSettings.Global.QuantumToolbarZone.ToString(), OnGUI);

          void RegisterCallback(string root, Action cb) {
            var toolbarZone = mRoot.Q(root);
            if (toolbarZone != null) {
              var parent = new VisualElement() {
                style = {
                flexGrow = 1,
                flexDirection = FlexDirection.Row,
              }
              };
              var container = new IMGUIContainer();
              container.onGUIHandler += () => {
                cb?.Invoke();
              };
              parent.Add(container);
              toolbarZone.Add(parent);
            }
          }
        }
      }

      // Using renamed map to detect if a scene has been renamed.
      if (_renamedMap != null && _renamedMap.Count > 0) {
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++) {
          if (_renamedMap.Contains(EditorBuildSettings.scenes[i].path) == false) {
            _scenePaths?.Clear();
            _sceneNames = null;
            _renamedMap.Clear();
            break;
          }
        }
      }

      // Cache scene names and paths.
      if (_scenePaths == null || _scenePaths.Count != EditorBuildSettings.scenes.Length) {
        _scenePaths ??= new List<string>();
        _renamedMap ??= new HashSet<string>();
        var sceneNames = new List<string>();

        _scenePaths.Clear();
        _renamedMap.Clear();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) {
          if (scene.path == null || scene.path.StartsWith("Assets") == false)
            continue;

          string scenePath = Application.dataPath + scene.path.Substring(6);

          _scenePaths.Add(scenePath);
          sceneNames.Add(Path.GetFileNameWithoutExtension(scenePath));
          _renamedMap.Add(scene.path);
        }

        _sceneNames = sceneNames.ToArray();
      }

      // If no scenes have been added to the build settings yet, display all of them.
      if (_scenePaths != null && _scenePaths.Count == 0) {
        var sceneGuids = AssetDatabase.FindAssets("t:scene");
        if (_scenePaths.Count != sceneGuids.Length) {
          _scenePaths = sceneGuids.Select(assetGuid => AssetDatabase.GUIDToAssetPath(assetGuid)).ToList();
          _sceneNames = _scenePaths.Select(scenePath => Path.GetFileNameWithoutExtension(scenePath)).ToArray();
          _renamedMap.Clear();
        }
      }
    }

    private static void OnGUI() {
      if (QuantumEditorSettings.Get(x => x.UseQuantumToolbarUtilities) != true) {
        return;
      }

      using (new EditorGUI.DisabledScope(Application.isPlaying)) {
        string sceneName = EditorSceneManager.GetActiveScene().name;
        int sceneIndex = -1;

        for (int i = 0; i < _sceneNames.Length; ++i) {
          if (sceneName == _sceneNames[i]) {
            sceneIndex = i;
            break;
          }
        }

        int newSceneIndex = EditorGUILayout.Popup(sceneIndex, _sceneNames, GUILayout.Width(200.0f));
        if (newSceneIndex != sceneIndex) {
          if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            EditorSceneManager.OpenScene(_scenePaths[newSceneIndex], OpenSceneMode.Single);
          }
        }
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumEditorUserScriptGeneration.cs

namespace Quantum.Editor {
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Utility methods to create initial user files required for the Quantum SDK.
  /// These files are never overwritten by SDK updates.
  /// Files include asmref files, readme files or partial classes.
  /// Files are identified by their Unity script GUIDs.
  /// </summary>
  public static class QuantumEditorUserScriptGeneration {
    static string UnityProjectPath => Directory.GetParent(Application.dataPath).ToString();

    /// <summary>
    /// The default user folder path.
    /// </summary>
    public const string FolderPath = "Assets/QuantumUser";

    /// <summary>
    /// Return true if all important partial user files exist.
    /// </summary>
    public static bool UserFilesExist => FilesExist(UserFiles);
    /// <summary>
    /// Return true if important asmref files exist.
    /// </summary>
    public static bool WorkspaceFilesExist => FilesExist(WorkspaceFiles);
    /// <summary>
    /// Return a user files path.
    /// </summary>
    public static string PingUserFile => AssetDatabase.GUIDToAssetPath(UserFiles[0].Guid);
    /// <summary>
    /// Return a workspace asmref file path.
    /// </summary>
    public static string PingWorkspaceFile => AssetDatabase.GUIDToAssetPath(WorkspaceFiles[0].Guid);

    struct UserFile {
      public string Filename;
      public string Template;
      public string Guid;
    }

    static UserFile[] WorkspaceFiles = new UserFile[] {
      new UserFile { Filename = "readme.txt", Template = TemplateUserReadme, Guid = "d06d5bec0132004488c35943b906d0b0" },
      new UserFile { Filename = "Simulation/Quantum.Simulation.asmref", Template = TemplateUserAssemblyReference("5d82202959c2f144ea95e134645b6833"), Guid = "02b31fc42166b83489f6420d738b63e8" },
      new UserFile { Filename = "View/Generated/Quantum.Unity.asmref", Template = TemplateUserAssemblyReference("f6fa0c2f8b9a9f64897d3351666f3d66"), Guid = "f63dd265c2b202b46a7d5fa69ba403ad" },
      new UserFile { Filename = "Editor/Quantum.Unity.Editor.asmref", Template = TemplateUserAssemblyReference("3dc2666f1394e2c48a30ea492efb1717"), Guid = "a36abd0809aaad94eab47210c35fc77b" },
      new UserFile { Filename = "Editor/CodeGen/Quantum.Unity.Editor.CodeGen.asmref", Template = TemplateUserAssemblyReference("fdae25391b6090942b63551d0cc45292"), Guid = "35245754b8fc0864eb5e25678cabd7b8" },
    };

    static UserFile[] UserFiles = new UserFile[] {
      new UserFile { Filename = "Simulation/SimulationConfig.User.cs", Template = TemplateUserSimulationConfig, Guid = "057b651d1faea304896bb12296057c8f" },
      new UserFile { Filename = "Simulation/RuntimeConfig.User.cs", Template = TemplateUserRuntimeConfig, Guid = "7abf8ca75dd18324185ec5fd0cf594b4" },
      new UserFile { Filename = "Simulation/RuntimePlayer.User.cs", Template = TemplateUserRuntimePlayer, Guid = "6878a44a83ae81040a1d9be831aafdb5" },
      new UserFile { Filename = "Simulation/Frame.User.cs", Template = TemplateUserFrame, Guid = "17c427bb6bd246f409cc104f5e3ef581" },
      new UserFile { Filename = "Simulation/FrameContext.User.cs", Template = TemplateUserFrameContext, Guid = "0dcf5f37941414145806a68798e41dd1" },
      new UserFile { Filename = "Simulation/CommandSetup.User.cs", Template = TemplateUserCommandSetup, Guid = "2722048d0babb254d9f774fa0c8a37af" },
      new UserFile { Filename = "Simulation/SystemSetup.User.cs", Template = TemplateUserSystemSetup, Guid = "a1080442b5d513f4da30c121b4a9e360" },
      new UserFile { Filename = "Editor/CodeGen/QuantumCodeGenSettings.User.cs", Template = TemplateUserCodeGenSettings, Guid = "6114f12024791ff44b0fb4ddc74047e5" },
    };

    #region Templates

    /// <summary>
    /// Template to add user code to <see cref="DeterministicSystemSetup"/>.
    /// </summary>
    public static string TemplateUserSystemSetup => "namespace Quantum" + Environment.NewLine +
        "{" + Environment.NewLine +
        "    using System;" + Environment.NewLine +
        "    using System.Collections.Generic;" + Environment.NewLine + Environment.NewLine +
        "    public static partial class DeterministicSystemSetup" + Environment.NewLine +
        "    {" + Environment.NewLine +
        "        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig)" + Environment.NewLine +
        "        {" + Environment.NewLine +
        "            // The system collection is already filled with systems coming from the SystemsConfig. " + Environment.NewLine +
        "            // Add or remove systems to the collection: systems.Add(new SystemFoo());" + Environment.NewLine +
        "    }" + Environment.NewLine +
        "  }" + Environment.NewLine +
        "}";

    /// <summary>
    /// Template to add user code to <see cref="DeterministicCommandSetup"/>.
    /// </summary>
    public static string TemplateUserCommandSetup => "namespace Quantum" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    using System.Collections.Generic;" + Environment.NewLine +
      "    using Photon.Deterministic;" + Environment.NewLine + Environment.NewLine +
      "    public static partial class DeterministicCommandSetup" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "        static partial void AddCommandFactoriesUser(ICollection<IDeterministicCommandFactory> factories, RuntimeConfig gameConfig, SimulationConfig simulationConfig)" + Environment.NewLine +
      "        {" + Environment.NewLine +
      "            // Add or remove commands to the collection." + Environment.NewLine +
      "            // factories.Add(new NavMeshAgentTestSystem.RunTest());" + Environment.NewLine +
      "        }" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    /// <summary>
    /// Template to add user code to <see cref="Frame"/>.
    /// </summary>
    public static string TemplateUserFrame => "namespace Quantum" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    public unsafe partial class Frame" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "#if UNITY_ENGINE" + Environment.NewLine + Environment.NewLine +
      "#endif" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    /// <summary>
    /// Template to add user code to <see cref="FrameContextUser"/>.
    /// </summary>
    public static string TemplateUserFrameContext => "namespace Quantum" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    public partial class FrameContextUser" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    /// <summary>
    /// Template to add user code to <see cref="RuntimeConfig"/>.
    /// </summary>
    public static string TemplateUserRuntimeConfig => "namespace Quantum" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    public partial class RuntimeConfig" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    /// <summary>
    /// Template to add user code to <see cref="RuntimePlayer"/>.
    /// </summary>
    public static string TemplateUserRuntimePlayer => "namespace Quantum" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    public partial class RuntimePlayer" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    /// <summary>
    /// Template to add user code to <see cref="SimulationConfig"/>.
    /// </summary>
    public static string TemplateUserSimulationConfig => "namespace Quantum" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    public partial class SimulationConfig : AssetObject" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    /// <summary>
    /// Template to add user code to <see cref="QuantumCodeGenSettings"/>.
    /// </summary>
    public static string TemplateUserCodeGenSettings => "namespace Quantum.Editor" + Environment.NewLine +
      "{" + Environment.NewLine +
      "    using Quantum.CodeGen;" + Environment.NewLine + Environment.NewLine +
      "    public static partial class QuantumCodeGenSettings" + Environment.NewLine +
      "    {" + Environment.NewLine +
      "        static partial void GetCodeGenFolderPathUser(ref string path) { }" + Environment.NewLine +
      "        static partial void GetCodeGenUnityRuntimeFolderPathUser(ref string path) { }" + Environment.NewLine +
      "        static partial void GetOptionsUser(ref GeneratorOptions options) { }" + Environment.NewLine +
      "    }" + Environment.NewLine +
      "}";

    public static string TemplateUserAssemblyReference(string guid) => "{" + Environment.NewLine +
      $"    \"reference\": \"GUID:{guid}\"" + Environment.NewLine +
      "}";

    public static string TemplateUserReadme => "This folder is generated by the Quantum Hub and represents the Quantum user workspace." + Environment.NewLine + Environment.NewLine +
      "This folder can be renamed and rearranged if the Assembly References are kept intact and the CodeGen settings have been updated with the new paths (Assets/QuantumUser/Editor/CodeGen/QuantumCodeGenSettings.User.cs)." + Environment.NewLine + Environment.NewLine +
      "The content should be kept in version control." + Environment.NewLine + Environment.NewLine +
      "/Editor                   Quantum assets used in Unity Editor context like editor and gizmo settings, all content is added to the Quantum.Unity.Editor assembly." + Environment.NewLine +
      "/Editor/Generated         Unity editor scripts generated by the Quantum CodeGen." + Environment.NewLine +
      "/Editor/CodeGen           Allows to overwrite the CodeGen settings." + Environment.NewLine +
      "/Resources                Default place for Quantum configuration file like server settings, session config of the QuantumUnityDb" + Environment.NewLine +
      "/Scenes                   The default location for the initial demo scenes." + Environment.NewLine +
      "/Simulation               This is the simulation code and will be added to the Quantum.Simulation.dll. Place game code and Qtn files in here." + Environment.NewLine +
      "/Simulation/Generated     Result of the Qtn file CodeGen." + Environment.NewLine +
      "/View                     An optional place to create Unity view scripts, which can also be anywhere else (except the Generated folder)." + Environment.NewLine +
      "/View/Generated           Quantum asset script generated by the Quantum Unity CodeGen, files inside are added to the Quantum.Unity.dll.";

    #endregion

    static bool FilesExist(UserFile[] files) {
      for (int i = 0; i < files.Length; i++) {
        if (string.IsNullOrEmpty(files[i].Guid) == false) {
          var assetPath = AssetDatabase.GUIDToAssetPath(files[i].Guid);
          if (string.IsNullOrEmpty(assetPath) == false && File.Exists(assetPath)) {
            continue;
          }

          var path = Path.Combine(UnityProjectPath, FolderPath, files[i].Filename);
          if (File.Exists(path) == false) {
            return false;
          }
        }
      }
      return true;
    }

    /// <summary>
    /// Generate user files.
    /// </summary>
    public static void GenerateUserFiles() {
      for (int i = 0; i < UserFiles.Length; i++) {
        WriteFileIfNotExists(Path.Combine(FolderPath, UserFiles[i].Filename), UserFiles[i].Template, UserFiles[i].Guid);
      }
    }

    /// <summary>
    /// Generate workspace asmref files.
    /// </summary>
    public static void GenerateWorkspaceFiles() {
      for (int i = 0; i < WorkspaceFiles.Length; i++) {
        WriteFileIfNotExists(Path.Combine(FolderPath, WorkspaceFiles[i].Filename), WorkspaceFiles[i].Template, WorkspaceFiles[i].Guid);
      }
    }

    #region CreationMethods

    private static void RewriteGuid(string assetFilePath, string guid) {
      if (string.IsNullOrEmpty(guid)) {
        return;
      }

      var metaContents = File.ReadAllText($"{assetFilePath}.meta");
      var guidToReplace = AssetDatabase.AssetPathToGUID(assetFilePath);
      File.WriteAllText($"{assetFilePath}.meta", metaContents.Replace(guidToReplace, guid));
      AssetDatabase.ImportAsset(assetFilePath);
    }

    private static bool Exists(string assetPath, string guid) {
      if (string.IsNullOrEmpty(guid) == false) {
        var assetPath2 = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath2) == false && File.Exists(Path.Combine(UnityProjectPath, assetPath2))) {
          return true;
        }
      }

      var path = Path.Combine(UnityProjectPath, assetPath);
      if (File.Exists(path)) {
        return true;
      }

      return false;
    }

    private static void WriteFileIfNotExists(string assetPath, string content, string guid) {
      if (string.IsNullOrEmpty(guid) == false) {
        var assetPath2 = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath2) == false && File.Exists(Path.Combine(UnityProjectPath, assetPath2))) {
          return;
        }
      }

      var path = Path.Combine(UnityProjectPath, assetPath);
      if (File.Exists(path)) {
        return;
      }

      if (Directory.Exists(Path.GetDirectoryName(path)) == false) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
      }

      File.WriteAllText(path, content, System.Text.Encoding.UTF8);
      QuantumEditorLog.Log($"{Path.GetFileNameWithoutExtension(assetPath)} generated at {path}");

      if (string.IsNullOrEmpty(guid)) {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RewriteGuid(assetPath, guid);
      }
    }

    #endregion
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumGizmoEditorUtil.cs

namespace Quantum {
  using System.Linq;
  using Editor;
  using UnityEditor.UIElements;
  using UnityEngine;
  using UnityEngine.UIElements;

  internal class QuantumGizmoEditorUtil {
    internal static VisualElement CreateScrollView() {
      var scrollView = new ScrollView();

      scrollView.horizontalScroller.SetEnabled(false);
      scrollView.verticalScroller.SetEnabled(true);

      return scrollView;
    }

    internal static VisualElement CreateGizmoToolbar(
      string gizmoName,
      out ToolbarToggle visToggle,
      out ToolbarButton styleButton,
      out ToolbarToggle selectedToggle,
      out VisualElement optionsParent) {
      var toolbar = new VisualElement();

      toolbar.style.paddingLeft = 5;
      toolbar.style.paddingRight = 5;
      toolbar.style.flexDirection = FlexDirection.Row;

      var left = new VisualElement();
      left.style.justifyContent = Justify.FlexStart;
      left.style.flexDirection = FlexDirection.Row;

      var label = new Label(gizmoName);

      label.style.paddingLeft = 5;
      label.style.alignSelf = Align.Center;
      label.style.unityFontStyleAndWeight = FontStyle.Bold;
      label.style.fontSize = 10;

      visToggle = new ToolbarToggle();

      left.Add(visToggle);
      left.Add(label);

      toolbar.Add(left);

      var spacer = new ToolbarSpacer();
      spacer.style.flexGrow = 1;

      toolbar.Add(spacer);

      var right = new VisualElement();

      right.style.justifyContent = Justify.FlexEnd;
      right.style.flexDirection = FlexDirection.Row;
      right.style.paddingLeft = 5;
      right.style.paddingRight = 5;

      selectedToggle = new ToolbarToggle();

      right.Add(selectedToggle);

      toolbar.Add(right);

      styleButton = new ToolbarButton();

      toolbar.Add(styleButton);

      optionsParent = right;

      return toolbar;
    }

    internal static VisualElement CreateStylePopup(
      out ColorField mainColorField,
      out ColorField secondaryColorField,
      out ColorField warningColorField,
      out Toggle disabledFillToggle,
      out Slider scaleSlider,
      out Label scaleLabel) {
      var root = new VisualElement();

      root.style.height = Length.Percent(100);
      root.style.width = Length.Percent(100);
      root.style.paddingLeft = 2;
      root.style.paddingRight = 2;
      root.style.paddingTop = 2;
      root.style.paddingBottom = 2;

      var colorFieldContainer = CreateColorField("Main Color", out mainColorField);

      root.Add(colorFieldContainer);

      var sColorFieldContainer = CreateColorField("Secondary Color", out secondaryColorField);

      root.Add(sColorFieldContainer);

      var warningColorFieldContainer = CreateColorField("Warning Color", out warningColorField);

      root.Add(warningColorFieldContainer);

      disabledFillToggle = new Toggle();
      disabledFillToggle.text = "Disable Fill";

      root.Add(disabledFillToggle);

      scaleLabel = new Label("Scale");

      root.Add(scaleLabel);

      scaleSlider = new Slider(1, 5);

      root.Add(scaleSlider);

      return root;
    }

    private static VisualElement CreateColorField(string label, out ColorField colorField) {
      var container = new VisualElement();
      var colorLabel = new Label(label);
      colorField = new ColorField();

      container.Add(colorLabel);
      container.Add(colorField);

      return container;
    }

    internal static ToolbarSearchField CreateSearchField() {
      var searchField = new ToolbarSearchField();
      searchField.style.flexGrow = 1;
      return searchField;
    }

    internal static VisualElement CreateHeader(string text, out ToolbarToggle toggle, out VisualElement bg) {
      var visualElement = new VisualElement { name = "Header" };

      // darkened unity default color
      Color dark = new Color32(45, 45, 45, 255);

      visualElement.style.flexDirection = FlexDirection.Row;
      visualElement.style.paddingLeft = 5;
      visualElement.style.paddingRight = 5;
      visualElement.style.marginTop = 1;
      visualElement.style.backgroundColor = dark;

      bg = visualElement;

      toggle = new ToolbarToggle();
      var toggleImg = new Image();

      toggle.Add(toggleImg);

      var header = new Label(text.ToUpper());
      header.style.paddingLeft = 5;
      header.style.unityFontStyleAndWeight = QuantumEditorSkin.ScriptHeaderLabelStyle.fontStyle;
      header.style.unityFontDefinition = new StyleFontDefinition(QuantumEditorSkin.ScriptHeaderLabelStyle.font);
      header.style.alignSelf = Align.Center;
      header.style.fontSize = 15;

      visualElement.Add(toggle);
      visualElement.Add(header);

      var spacer = new VisualElement();
      spacer.style.flexGrow = 1;
      visualElement.Add(spacer);

      var img = new Image();
      img.image = QuantumEditorSkin.ScriptHeaderIconStyle.normal.background;
      img.style.justifyContent = Justify.FlexEnd;
      visualElement.Add(img);

      return visualElement;
    }

    internal static VisualElement CreateLabel(string text, int fontSize = 10) {
      var label = new Label(text);
      label.style.paddingLeft = 5;
      label.style.paddingRight = 5;
      label.style.unityFontStyleAndWeight = FontStyle.Bold;
      label.style.fontSize = fontSize;
      return label;
    }

    internal static VisualElement CreateSliderWithTitle(
      string title,
      float value,
      float min,
      float max,
      EventCallback<ChangeEvent<float>> callback) {
      var parent = new VisualElement();
      var label = new Label(title);
      var slider = new Slider(min, max) { value = value };
      var spacer = new VisualElement();

      parent.Add(label);
      parent.Add(spacer);
      parent.Add(slider);

      spacer.style.flexGrow = 1;

      parent.style.flexDirection = FlexDirection.Row;
      parent.style.paddingLeft = 5;
      parent.style.paddingRight = 5;

      label.style.alignSelf = Align.Center;
      label.style.unityFontStyleAndWeight = FontStyle.Bold;
      label.style.fontSize = 10;

      slider.style.width = Length.Percent(50);
      slider.style.height = 20;

      slider.RegisterCallback(callback);

      return parent;
    }

    internal static string AddSpacesToString(string name) {
      var spaced = string.Concat(name.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
      return spaced;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumMenuSceneInfoEditor.cs

namespace Quantum.Editor {
  using UnityEditor;
  using UnityEngine.SceneManagement;
  using UnityEngine;
  using System.Collections.Generic;
  using static QuantumUnityExtensions;

  /// <summary>
  /// Custom inspector for <see cref="QuantumMenuSceneInfo"/>
  /// </summary>
  [CustomEditor(typeof(QuantumMenuSceneInfo))]
  public class QuantumMenuConfigEditor : Editor {
    /// <summary>
    /// Overriding drawing.
    /// </summary>
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      if (GUILayout.Button("Set To Current Scene")) {
        SetToCurrentScene((QuantumMenuSceneInfo)target, null);
      }
    }

    /// <summary>
    /// Add the current open Unity scene to a QuantumMenuConfig.
    /// </summary>
    /// <param name="runtimeConfig">Set an optional <see cref="RuntimeConfig"/></param>
    public static void SetToCurrentScene(QuantumMenuSceneInfo target, RuntimeConfig runtimeConfig) {
      var mapData = FindFirstObjectByType<QuantumMapData>();
      if (mapData == null) {
        QuantumEditorLog.Error($"Map asset not found in current scene");
        return;
      }

      var debugRunner = FindAnyObjectByType<QuantumRunnerLocalDebug>();

      var scene = SceneManager.GetActiveScene();

      var scenePath = PathUtils.Normalize(scene.path);

      target.Name = scene.name;
      target.ScenePath = scenePath;
      target.RuntimeConfig = runtimeConfig ?? debugRunner?.RuntimeConfig ?? new RuntimeConfig();

      if (target.Map.IsValid == false) {
        target.RuntimeConfig.Map = mapData.AssetRef;
      }

      EditorUtility.SetDirty(target);

      AddScenePathToBuildSettings(scenePath);
    }

    private static void AddScenePathToBuildSettings(string scenePath) {
      var editorBuildSettingsScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
      if (editorBuildSettingsScenes.FindIndex(s => s.path.Equals(scenePath, System.StringComparison.Ordinal)) < 0) {
        editorBuildSettingsScenes.Add(new EditorBuildSettingsScene { path = scenePath, enabled = true });
        EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumProfilingServer.cs

namespace Quantum.Editor {
#if QUANTUM_ENABLE_REMOTE_PROFILER
  using System;
  using System.Collections.Generic;
  using System.Net;
  using LiteNetLib;
  using LiteNetLib.Utils;
  using Profiling;
  using UnityEngine;
  using static InternalLogStreams;

  /// <summary>
  /// The Quantum profiling server that listens for profiling data from clients using LiteNetLib.
  /// </summary>
  public class QuantumProfilingServer {
    /// <summary>
    /// The port the server listens on.
    /// </summary>
    public const int PORT = 30000;
    private readonly EventBasedNetListener _listener;
    private readonly NetManager _manager;
    private readonly Dictionary<NetPeer, QuantumProfilingClientInfo> _peers = new();
    private static QuantumProfilingServer _server;

    private QuantumProfilingServer() {
      _listener = new EventBasedNetListener();

      _manager                         = new NetManager(_listener);
      _manager.BroadcastReceiveEnabled = true;
      _manager.Start(PORT);
      _listener.ConnectionRequestEvent         += OnConnectionRequest;
      _listener.PeerConnectedEvent             += OnPeerConnected;
      _listener.PeerDisconnectedEvent          += OnPeerDisconnected;
      _listener.NetworkReceiveEvent            += OnNetworkReceiveEvent;
      _listener.NetworkReceiveUnconnectedEvent += OnNetworkReceiveUnconnectedEvent;

      Debug.Log($"QuantumProfilingServer: Started @ 0.0.0.0:{PORT}");
    }

    /// <summary>
    /// The event that is triggered when a profiling sample is received.
    /// </summary>
    public static event Action<QuantumProfilingClientInfo, ProfilerContextData> SampleReceived;

    /// <summary>
    /// Unity update method will poll events.
    /// </summary>
    public static void Update() {
      if (_server == null) {
        _server = new QuantumProfilingServer();
      }

      _server._manager.PollEvents();
    }

    private void OnConnectionRequest(ConnectionRequest request) {
      request.AcceptIfKey(QuantumProfilingClientConstants.CONNECT_TOKEN);
    }

    private void OnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) {
      try {
        var msgType = reader.GetByte();
        var text    = reader.GetString();

        if (msgType == QuantumProfilingClientConstants.ClientInfoMessage) {
          var data = JsonUtility.FromJson<QuantumProfilingClientInfo>(text);
          _peers[peer] = data;
        } else if (msgType == QuantumProfilingClientConstants.FrameMessage) {
          if (SampleReceived != null) {
            var data = JsonUtility.FromJson<ProfilerContextData>(text);
            try {
              if (_peers.TryGetValue(peer, out var info)) {
                SampleReceived(info, data);
              } else {
                LogError?.Log($"Client Info not found for peer {peer.Address}");
              }
            } catch (Exception ex) {
              LogError?.Log($"QuantumProfilingServer: Sample Handler Error: {ex}");
            }
          }
        } else {
          throw new NotSupportedException($"Unknown message type: {msgType}");
        }
      } catch (Exception ex) {
        LogError?.Log($"QuantumProfilingServer: Receive error: {ex}, disconnecting peer {peer.Address}");
        _manager.DisconnectPeerForce(peer);
      }
    }

    private void OnNetworkReceiveUnconnectedEvent(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype) {
      if (reader.GetString() == QuantumProfilingClientConstants.DISCOVER_TOKEN) {
        LogInfo?.Log($"QuantumProfilingServer: Discovery Request From {remoteendpoint}");
        _manager.SendUnconnectedMessage(NetDataWriter.FromString(QuantumProfilingClientConstants.DISCOVER_RESPONSE_TOKEN), remoteendpoint);
      }
    }

    private void OnPeerConnected(NetPeer peer) {
      LogInfo?.Log($"QuantumProfilingServer: Connection From {peer.Address}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
      _peers.Remove(peer);
    }
  }
#else
  using System;
  using Quantum.Profiling;

  /// <summary>
  /// A dummy implementation of the Quantum profiling server.
  /// </summary>
  public class QuantumProfilingServer {
    private QuantumProfilingServer() {
    }

#pragma warning disable 67
    /// <summary>
    /// The event that is triggered when a profiling sample is received.
    /// </summary>
    public static event Action<QuantumProfilingClientInfo, ProfilerContextData> SampleReceived;
#pragma warning restore 67

    /// <summary>
    /// Empty unity update method.
    /// </summary>
    public static void Update() {
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumTaskProfilerModel.cs

namespace Quantum.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using Photon.Deterministic;
  using Profiling;
  using UnityEngine;

  [Serializable]
  public class QuantumTaskProfilerModel : ISerializationCallbackReceiver {

    public static readonly Color DefaultSampleColor = new Color(0.65f, 0.65f, 0.65f, 1.0f);

    public byte FormatVersion = BinaryFormat.Invalid;
    public List<Frame> Frames = new List<Frame>();
    public List<SampleMeta> SamplesMeta = new List<SampleMeta>();
    public List<QuantumProfilingClientInfo> Clients = new List<QuantumProfilingClientInfo>();

    /// <summary>
    /// 'QPRF'
    /// </summary>
    private const int BinaryHeader = 0x46525051;
    private const int InitialStackCapacity = 10;

    private Dictionary<string, int> _clientIdToIndex = new Dictionary<string, int>();
    private Dictionary<string, int> _samplePathToId = new Dictionary<string, int>();
    private Sample[] _samplesStack = new Sample[InitialStackCapacity];
    private int samplesStackCount = 0;

    public static QuantumTaskProfilerModel LoadFromFile(string path) {
      // first, try to read as a binary
      try {
        using (var serializer = new BinarySerializer(File.OpenRead(path), false)) {
          var result = new QuantumTaskProfilerModel();
          result.Serialize(serializer);
          return result;
        }
      } catch (System.InvalidOperationException) {
        // well, try to load as json now
        var text = File.ReadAllText(path);
        var result = JsonUtility.FromJson<QuantumTaskProfilerModel>(text);
        if (result.FormatVersion == 0) {
          var legacySession = JsonUtility.FromJson<LegacySerializableFrames>(text);
          if (legacySession.Frames != null) {
            result = new QuantumTaskProfilerModel();
            foreach (var frame in legacySession.Frames) {
              result.AddFrame(null, frame);
            }
          }
        }

        return result;
      }
    }

    public void AccumulateDurations(BitArray mask, int startFrame, List<float> target) {
      if (Frames.Count == 0)
        return;

      // we need to keep track of the match depth; if we have a match for a parent, 
      // we want to skip all the descendants
      int matchDepthPlusOne = 0;

      for (int frame = startFrame; frame < Frames.Count; ++frame) {
        long totalTicks = 0;
        var f = Frames[frame];
        foreach (var thread in f.Threads) {
          foreach (var sample in thread.Samples) {
            if (matchDepthPlusOne > 0) {
              if (sample.Depth + 1 > matchDepthPlusOne)
                continue;
              else
                matchDepthPlusOne = 0;
            }

            int mod = mask.Get(sample.Id) ? 1 : 0;

            totalTicks += sample.Duration * mod;
            matchDepthPlusOne = sample.Depth * mod;
          }
        }

        target.Add(Mathf.Min((float)(totalTicks * f.TicksToMS), f.DurationMS));
      }
    }

    public void AddFrame(QuantumProfilingClientInfo clientInfo, ProfilerContextData data) {
      var frame = new Frame();

      GetStartEndRange(data, out frame.Start, out frame.Duration);
      frame.TickFrequency = data.Frequency;
      frame.Number = data.Frame;
      frame.IsVerified = data.IsVerified;
      frame.SimulationId = data.SimulationId;
      if (clientInfo != null) {
        frame.ClientId = GetOrAddClientInfo(clientInfo);
      }

      foreach (var sourceThread in data.Profilers) {
        var thread = new Thread() {
          Name = sourceThread.Name
        };

        foreach (var sourceSample in sourceThread.Samples) {
          switch (sourceSample.Type) {
            case SampleType.Begin: {
                var sample = new Sample() {
                  Id = GetOrAddMetaId(sourceSample.Name),
                  Start = sourceSample.Time,
                };

                PushSample(sample);
              }
              break;

            case SampleType.End: {
                var sample = PopSample();
                var duration = sourceSample.Time - sample.Start;
                sample.Duration = duration;
                sample.Start -= frame.Start;
                sample.Depth = samplesStackCount;
                thread.Samples.Add(sample);
              }
              break;

            case SampleType.Event: {
                // events have duration of 0 and depth is always 0
                var sample = new Sample() {
                  Id = GetOrAddMetaId(sourceSample.Name),
                  Start = sourceSample.Time - frame.Start,
                  Duration = 0,
                  Depth = 0
                };

                thread.Samples.Add(sample);
              }
              break;

            default:
              break;
          }
        }

        frame.Threads.Add(thread);
      }

      Frames.Add(frame);
    }

    public void CreateSearchMask(string pattern, BitArray bitArray) {
      if (bitArray.Length < SamplesMeta.Count) {
        bitArray.Length = SamplesMeta.Count;
      }
      for (int i = 0; i < SamplesMeta.Count; ++i) {
        var name = SamplesMeta[i].Name;
        bitArray.Set(i, name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
      }
    }

    public Frame FindPrevSafe(int index, bool verified = true) {
      if (index > Frames.Count || index <= 0)
        return null;

      for (int i = index - 1; i >= 0; --i) {
        if (Frames[i].IsVerified == verified)
          return Frames[i];
      }

      return null;
    }

    public int FrameIndexToSimulationIndex(int index) {
      if (Frames.Count == 0)
        return 0;

      int currentSimulation = Frames[0].SimulationId;
      int simulationIndex = 0;

      for (int i = 0; i < Frames.Count; ++i) {
        var frame = Frames[i];
        if (frame.SimulationId != currentSimulation) {
          ++simulationIndex;
          currentSimulation = frame.SimulationId;
        }

        if (i == index) {
          return simulationIndex;
        }
      }

      throw new InvalidOperationException();
    }

    public QuantumProfilingClientInfo GetClientInfo(Frame frame) {
      if (frame.ClientId < 0)
        return null;
      return Clients[frame.ClientId];
    }

    public void GetFrameDurations(List<float> values) {
      foreach (var f in Frames) {
        values.Add(f.DurationMS);
      }
    }

    public void GetSampleMeta(Sample s, out Color color, out string text) {
      var meta = SamplesMeta[s.Id];
      color = meta.Color;
      text = meta.Name;
    }

    public void GroupBySimulationId(List<float> values, List<float> grouped, List<float> counts = null) {
      Debug.Assert(values == null || values.Count == Frames.Count);
      if (Frames.Count == 0)
        return;

      int currentSimulation = Frames[0].SimulationId;
      float total = 0.0f;
      int count = 0;

      for (int i = 0; i < Frames.Count; ++i) {
        var frame = Frames[i];
        if (frame.SimulationId != currentSimulation) {
          grouped.Add(total);
          counts?.Add((float)count);
          count = 0;
          total = 0.0f;
          currentSimulation = frame.SimulationId;
        }
        ++count;
        total += values == null ? Frames[i].DurationMS : values[i];
      }

      counts?.Add((float)count);
      grouped.Add(total);
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize() {

      _samplePathToId.Clear();
      for (int i = 0; i < SamplesMeta.Count; ++i) {
        _samplePathToId.Add(SamplesMeta[i].FullName, i);
      }

      _clientIdToIndex.Clear();
      for (int i = 0; i < Clients.Count; ++i) {
        _clientIdToIndex.Add(Clients[i].ProfilerId, i);
      }
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize() {
      FormatVersion = BinaryFormat.Latest;
    }

    public void Serialize(BinarySerializer serializer) {

      if (!serializer.IsReading) {
        ((ISerializationCallbackReceiver)this).OnBeforeSerialize();
      }

      int header = BinaryHeader;
      serializer.Serialize(ref header);
      if (header != BinaryHeader) {
        throw new InvalidOperationException("Invalid header");
      }

      serializer.Serialize(ref FormatVersion);
      if (FormatVersion > BinaryFormat.Latest || FormatVersion == 0) {
        throw new InvalidOperationException($"Version not supported: {FormatVersion}");
      }

      serializer.SerializeList(ref SamplesMeta, Serialize);
      serializer.SerializeList(ref Frames, Serialize);

      if (FormatVersion >= BinaryFormat.WithClientInfo) {
        serializer.SerializeList(ref Clients, Serialize);
      }

      if (serializer.IsReading) {
        ((ISerializationCallbackReceiver)this).OnAfterDeserialize();
      }
    }

    public int SimulationIndexToFrameIndex(int index, out int frameCount) {
      frameCount = 0;
      if (Frames.Count == 0)
        return 0;

      if (index == 0)
        return 0;

      int currentSimulation = Frames[0].SimulationId;
      int simulationIndex = 0;

      int i;
      for (i = 0; i < Frames.Count; ++i) {
        var frame = Frames[i];
        if (frame.SimulationId != currentSimulation) {
          ++simulationIndex;
          currentSimulation = frame.SimulationId;
          if (index == simulationIndex) {
            break;
          }
        }
      }

      var frameIndex = i;

      for (; i < Frames.Count; ++i) {
        var frame = Frames[i];
        if (frame.SimulationId == currentSimulation) {
          ++frameCount;
        } else {
          break;
        }
      }

      return frameIndex;
    }

    private static void GetStartEndRange(ProfilerContextData sourceFrame, out long min, out long range) {
      min = long.MaxValue;
      var max = long.MinValue;

      for (int i = 0; i < sourceFrame.Profilers.Length; ++i) {
        var p = sourceFrame.Profilers[i];
        if (p.Samples.Length > 0) {
          min = Math.Min(min, p.Samples[0].Time);
          max = Math.Max(max, p.Samples[p.Samples.Length - 1].Time);
        }
      }

      range = max - min;
    }

    private static string ProcessName(string name, out Color color) {
      if (name.Length >= 7 && name[name.Length - 7] == '#') {
        // possibly hex encoded color
        var hex = name.Substring(name.Length - 7);
        if (ColorUtility.TryParseHtmlString(hex, out color)) {
          return name.Substring(0, name.Length - 7).Trim();
        }
      }

      color = DefaultSampleColor;
      return name;
    }

    private int GetOrAddClientInfo(QuantumProfilingClientInfo info) {
      if (_clientIdToIndex.TryGetValue(info.ProfilerId, out int id)) {
        return id;
      }

      _clientIdToIndex.Add(info.ProfilerId, Clients.Count);
      Clients.Add(info);

      return Clients.Count - 1;
    }

    private int GetOrAddMetaId(string name) {
      if (_samplePathToId.TryGetValue(name, out int id)) {
        return id;
      }

      var shortName = ProcessName(name, out var color);

      _samplePathToId.Add(name, SamplesMeta.Count);
      SamplesMeta.Add(new SampleMeta() {
        Name = shortName,
        Color = color,
        FullName = name,
      });

      return SamplesMeta.Count - 1;
    }

    private Sample PopSample() {
      Debug.Assert(samplesStackCount > 0);
      return _samplesStack[--samplesStackCount];
    }

    private void PushSample(Sample sample) {
      Debug.Assert(samplesStackCount <= _samplesStack.Length);
      if (samplesStackCount + 1 >= _samplesStack.Length) {
        Array.Resize(ref _samplesStack, samplesStackCount + 10);
      }
      _samplesStack[samplesStackCount++] = sample;
    }

    private void Serialize(BinarySerializer serializer, ref SampleMeta meta) {
      serializer.Serialize(ref meta.FullName);
      serializer.Serialize(ref meta.Name);
      serializer.Serialize(ref meta.Color);
    }

    private void Serialize(BinarySerializer serializer, ref QuantumProfilingClientInfo info) {
      serializer.Serialize(ref info.ProfilerId);
      serializer.Serialize(ref info.Config, DeterministicSessionConfig.ToByteArray, DeterministicSessionConfig.FromByteArray);

      serializer.SerializeList(ref info.Properties, Serialize);
    }

    private void Serialize(BinarySerializer serializer, ref QuantumProfilingClientInfo.CustomProperty info) {
      serializer.Serialize(ref info.Name);
      serializer.Serialize(ref info.Value);
    }

    private void Serialize(BinarySerializer serializer, ref Frame frame) {
      if (FormatVersion < BinaryFormat.WithClientInfo) {
        string oldDeviceId = "";
        serializer.Serialize(ref oldDeviceId);
      } else {
        serializer.Serialize7BitEncoded(ref frame.ClientId);
      }

      serializer.Serialize7BitEncoded(ref frame.Duration);
      serializer.Serialize7BitEncoded(ref frame.TickFrequency);
      serializer.Serialize(ref frame.IsVerified);
      serializer.Serialize(ref frame.Start);
      serializer.Serialize(ref frame.Number);
      serializer.Serialize(ref frame.SimulationId);
      serializer.SerializeList(ref frame.Threads, Serialize);
    }

    private void Serialize(BinarySerializer serializer, ref Thread thread) {
      serializer.Serialize(ref thread.Name);
      serializer.SerializeList(ref thread.Samples, Serialize);
    }

    private void Serialize(BinarySerializer serializer, ref Sample sample) {
      serializer.Serialize7BitEncoded(ref sample.Id);
      serializer.Serialize7BitEncoded(ref sample.Start);
      serializer.Serialize7BitEncoded(ref sample.Duration);
      serializer.Serialize7BitEncoded(ref sample.Depth);
    }

    [Serializable]
    public struct Sample {
      public int Depth;
      public long Duration;
      public int Id;
      public long Start;
    }

    public static class BinaryFormat {
      public const byte Initial = 1;
      public const byte Invalid = 0;
      public const byte Latest = WithClientInfo;
      public const byte WithClientInfo = 2;
    }

    [Serializable]
    public class Frame {
      public int ClientId = -1;
      public long Duration;
      public bool IsVerified;
      public int Number;
      public int SimulationId;
      public long Start;
      public List<Thread> Threads = new List<Thread>();
      public long TickFrequency;
      public float DurationMS => (float)(Duration * TicksToMS);
      public double TicksToMS => 1000.0 / TickFrequency;
    }

    [Serializable]
    public class SampleMeta {
      public Color Color;
      public string FullName;
      public string Name;
    }

    [Serializable]
    public class Thread {
      public string Name;
      public List<Sample> Samples = new List<Sample>();
    }

    [Serializable]
    private class LegacySerializableFrames {
      public ProfilerContextData[] Frames = Array.Empty<ProfilerContextData>();
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/QuantumTaskProfilerWindow.Utils.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumTaskProfilerWindow {

    private static Vector3[] _rectVertices = new Vector3[4];
    private static Vector3[] _graphPoints = new Vector3[1024];

    public static void DrawSolidRectangleWithOutline(Rect rect, Color faceColor, Color outlineColor) {

      _rectVertices[0] = new Vector3(rect.xMin, rect.yMin, 0f);
      _rectVertices[1] = new Vector3(rect.xMax, rect.yMin, 0f);
      _rectVertices[2] = new Vector3(rect.xMax, rect.yMax, 0f);
      _rectVertices[3] = new Vector3(rect.xMin, rect.yMax, 0f);
      Handles.DrawSolidRectangleWithOutline(_rectVertices, faceColor, outlineColor);
    }

    public static void DrawRectFast(Rect r, Color color) {
      GL.Color(color);
      GL.Vertex(new Vector3(r.xMin, r.yMin, 0f));
      GL.Vertex(new Vector3(r.xMax, r.yMin, 0f));
      GL.Vertex(new Vector3(r.xMax, r.yMax, 0f));
      GL.Vertex(new Vector3(r.xMin, r.yMax, 0f));
    }

    public static void DrawVerticalLineFast(float x, float minY, float maxY, Color color) {
      GL.Color(color);
      GL.Vertex(new Vector3(x, minY, 0f));
      GL.Vertex(new Vector3(x, maxY, 0f));
    }
    private static void CalculateMeanStdDev(List<float> values, out double mean, out double stdDev) {
      mean = 0;
      foreach (var v in values)
        mean += v;
      mean /= values.Count;

      stdDev = 0;
      foreach (var v in values) {
        stdDev += (v - mean) * (v - mean);
      }
      stdDev = Math.Sqrt(stdDev / values.Count);
    }

    private static Rect DrawDropShadowLabel(float time, float x, float y, float sizeXMul, float sizeYMul) {
      var content = new GUIContent(FormatTime(time));
      var size = Styles.whiteLabel.CalcSize(content);
      var rect = new Rect(x + size.x * sizeXMul, y + size.y * sizeYMul, size.x, size.y);
      EditorGUI.DropShadowLabel(rect, content, Styles.whiteLabel);
      return rect;
    }

    private static Rect DrawDropShadowLabelWithMargins(Rect r, float time, float maxTime, float x, float sizeXMul = 0.0f, float sizeYMul = -0.5f, Color? color = null) {
      var content = new GUIContent(FormatTime(time));
      var size = Styles.whiteLabel.CalcSize(content);

      var y = (maxTime - time) * r.height / maxTime;
      y += size.y * sizeYMul;
      y = Mathf.Clamp(y, 0.0f, r.height - size.y) + r.y;

      x += size.x * sizeXMul;
      x = Mathf.Clamp(x, 0.0f, r.width - size.x) + r.x;

      var rect = new Rect(x, y, size.x, size.y);

      var oldContentColor = GUI.contentColor;
      try {
        if (color != null) {
          GUI.contentColor = color.Value;
        }
        EditorGUI.DropShadowLabel(rect, content, Styles.whiteLabel);
        return rect;
      } finally {
        GUI.contentColor = oldContentColor;
      }
    }

    private static float LinearRoot(float x, float y, float dx, float dy) {
      return x - y * dx / dy;
    }

    private static void DrawGraph(Rect rect, List<float> durations, ZoomPanel panel, float maxDuration, Color? color = null, float lineWidth = 2) {
      var r = rect.Adjust(0, 3, 0, -4);

      int p = 0;
      var durationToY = r.height / maxDuration;

      float dx = rect.width / panel.range;
      var start = Mathf.FloorToInt(panel.start);
      var end = Mathf.Min(durations.Count-1, Mathf.CeilToInt(panel.start + panel.range));
      var x = panel.TimeToPixel(start, rect);

      for (int i = start; i <= end; ++i, ++p, x += dx) {
        if (_graphPoints.Length - 1 <= p) {
          Array.Resize(ref _graphPoints, p * 2);
        }

        var d = durations[i];
        var y = (maxDuration - d);

        _graphPoints[p].x = x;
        _graphPoints[p].y = (maxDuration - d) * durationToY + r.y;
      }

      using (new Handles.DrawingScope(color ?? Color.white)) {
        Handles.DrawAAPolyLine(lineWidth, p, _graphPoints);
      }
    }

    private static void DrawLegendLabel(Rect rect, string label) {
      GUI.Box(rect, GUIContent.none, Styles.legendBackground);
      rect = rect.Adjust(5, 5, 0, 0);
      EditorGUI.LabelField(rect, label);
    }

    private static float DrawSplitter(Rect rect) {
      float delta = 0.0f;
      var controlId = GUIUtility.GetControlID(Styles.SplitterControlId, FocusType.Passive);
      switch (Event.current.GetTypeForControl(controlId)) {
        case EventType.MouseDown:
          if ((Event.current.button == 0) && (Event.current.clickCount == 1) && rect.Contains(Event.current.mousePosition)) {
            GUIUtility.hotControl = controlId;
          }
          break;

        case EventType.MouseDrag:
          if (GUIUtility.hotControl == controlId) {
            delta = Event.current.delta.y;
            Event.current.Use();
          }
          break;

        case EventType.MouseUp:
          if (GUIUtility.hotControl == controlId) {
            GUIUtility.hotControl = 0;
            Event.current.Use();
          }
          break;

        case EventType.Repaint:
          EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical, controlId);
          break;
      }
      return delta;
    }

    private static string FormatTime(float time) {
      return string.Format("{0:F4}ms", time);
    }
    internal sealed class TickHandler {

      private readonly float[] _tickModulos = new float[] {
        0.00001f,
        0.00005f,
        0.0001f,
        0.0005f,
        0.001f,
        0.005f,
        0.01f,
        0.05f,
        0.1f,
        0.5f,
        1f,
        5f,
        10f,
        50f,
        100f,
        500f,
        1000f,
        5000f,
        10000f,
      };

      private readonly float[] _tickStrengths;

      private int _maxVisibleLevel = -1;
      private int _minVisibleLevel = 0;
      private float _timeMin = 0;
      private float _timeRange = 1;
      private float _timeToPixel = 1;

      private List<float> m_TickList = new List<float>(1000);

      public TickHandler() {
        _tickStrengths = new float[_tickModulos.Length];
      }

      public int VisibleLevelsCount => _maxVisibleLevel - _minVisibleLevel + 1;

      public int GetLevelWithMinSeparation(float pixelSeparation) {
        for (int i = 0; i < _tickModulos.Length; i++) {
          float tickSpacing = _tickModulos[i] * _timeToPixel;
          if (tickSpacing >= pixelSeparation)
            return i - _minVisibleLevel;
        }
        return -1;
      }

      public float GetPeriodOfLevel(int level) {
        return _tickModulos[Mathf.Clamp(_minVisibleLevel + level, 0, _tickModulos.Length - 1)];
      }

      public float GetStrengthOfLevel(int level) {
        return _tickStrengths[_minVisibleLevel + level];
      }

      public List<float> GetTicksAtLevel(int level, bool excludeTicksFromHigherLevels) {
        m_TickList.Clear();

        if (level > 0) {
          GetTicksAtLevel(level, excludeTicksFromHigherLevels, m_TickList);
        }

        return m_TickList;
      }

      public void Refresh(float minTime, float timeRange, float pixelWidth, float minTickSpacing = 3.0f, float maxTickSpacing = 80.0f) {
        _timeMin = minTime;
        _timeRange = timeRange;
        _timeToPixel = pixelWidth / timeRange;

        _minVisibleLevel = 0;
        _maxVisibleLevel = _tickModulos.Length - 1;

        for (int i = _tickModulos.Length - 1; i >= 0; i--) {
          // how far apart (in pixels) these modulo ticks are spaced:
          float tickSpacing = _tickModulos[i] * _timeToPixel;

          // calculate the strength of the tick markers based on the spacing:
          _tickStrengths[i] = (tickSpacing - minTickSpacing) / (maxTickSpacing - minTickSpacing);

          if (_tickStrengths[i] >= 1) {
            _maxVisibleLevel = i;
          }

          if (tickSpacing <= minTickSpacing) {
            _minVisibleLevel = i;
            break;
          }
        }

        for (int i = _minVisibleLevel; i <= _maxVisibleLevel; i++) {
          _tickStrengths[i] = Mathf.Sqrt(Mathf.Clamp01(_tickStrengths[i]));
        }
      }
      private void GetTicksAtLevel(int level, bool excludeTicksFromHigherlevels, List<float> list) {
        if (list == null)
          throw new System.ArgumentNullException("list");

        int l = Mathf.Clamp(_minVisibleLevel + level, 0, _tickModulos.Length - 1);
        int startTick = Mathf.FloorToInt(_timeMin / _tickModulos[l]);
        int endTick = Mathf.FloorToInt((_timeMin + _timeRange) / _tickModulos[l]);
        for (int i = startTick; i <= endTick; i++) {
          // return if tick mark is at same time as larger tick mark
          if (excludeTicksFromHigherlevels
              && l < _maxVisibleLevel
              && (i % Mathf.RoundToInt(_tickModulos[l + 1] / _tickModulos[l]) == 0))
            continue;
          list.Add(i * _tickModulos[l]);
        }
      }
    }

    [Serializable]
    internal class ZoomPanel {
      public bool allowScrollPastLimits;
      public bool enableRangeSelect;

      public int controlId;

      public Rect areaRect;

      public float minRange;
      public float range;
      public float start;
      public float verticalScroll;

      public Vector2? selectionRange;
      private Vector2? _dragStart;

      public float DurationToPixelLength(float duration, Rect rect) {
        return (duration) / range * rect.width;
      }


      public void OnGUI(Rect r, float minValue, float maxValue, out bool unselect, float minY = 0.0f, float maxY = 1.0f, bool verticalSlider = false) {

        unselect = false;

        var areaRect = r.Adjust(0, 0, -Styles.ScrollBarWidth, -Styles.ScrollBarWidth);
        this.areaRect = areaRect;

        var hScrollbarRect = r.SetY(r.yMax - Styles.ScrollBarWidth).SetHeight(Styles.ScrollBarWidth).AddWidth(-Styles.ScrollBarWidth);
        DrawHorizontalScrollbar(hScrollbarRect, maxValue, ref start, ref range);

        var vScrollbarRect = r.SetX(r.xMax - Styles.ScrollBarWidth).SetWidth(Styles.ScrollBarWidth).AddHeight(-Styles.ScrollBarWidth);
        if (verticalSlider) {
          DrawPowerSlider(vScrollbarRect, minY, maxY, 4.0f, ref verticalScroll);
        } else {
          Debug.Assert(minY == 0.0f);
          DrawVerticalScrollbar(vScrollbarRect, maxY < 0 ? areaRect.height : maxY, areaRect.height, ref verticalScroll);
        }
        verticalScroll = Mathf.Clamp(verticalScroll, minY, maxY);

        //GUI.Box(hScrollbarRect.SetX(0).SetWidth(Styles.LeftPaneWidth), GUIContent.none, EditorStyles.toolbar);

        var id = GUIUtility.GetControlID(controlId, FocusType.Passive);



        using (new GUI.GroupScope(areaRect)) {
          if (Event.current.isMouse || Event.current.isScrollWheel) {
            bool doingSelect = Event.current.button == 0 && !Event.current.modifiers.HasFlag(EventModifiers.Alt);
            bool doingDragScroll = Event.current.button == 2 || Event.current.button == 0 && !doingSelect;
            bool doingZoom = Event.current.button == 1 && Event.current.modifiers.HasFlag(EventModifiers.Alt);
            var inRect = r.ZeroXY().Contains(Event.current.mousePosition);

            switch (Event.current.type) {
              case EventType.ScrollWheel:
                if (inRect) {
                  if (Event.current.modifiers.HasFlag(EventModifiers.Shift)) {
                    if (verticalSlider) {
                      var delta = Event.current.delta.x + Event.current.delta.y;
                      var amount = Mathf.Clamp(delta * 0.01f, -0.9f, 0.9f);
                      verticalScroll *= (1 - amount);
                      verticalScroll = Mathf.Clamp(verticalScroll, minY, maxY);
                      Event.current.Use();
                    }
                  } else {
                    PerfomFocusedZoom(Event.current.mousePosition, r.ZeroXY(), -Event.current.delta.x - Event.current.delta.y, minRange,
                      ref start, ref range);
                    Event.current.Use();
                  }
                }
                break;

              case EventType.MouseDown:
                if (inRect && (doingDragScroll || doingSelect || doingZoom)) {
                  _dragStart = Event.current.mousePosition;
                  selectionRange = null;
                  if (doingDragScroll || doingZoom) {
                    GUIUtility.hotControl = id;
                  } else if (!enableRangeSelect) {
                    GUIUtility.hotControl = id;
                    var x = PixelToTime(Event.current.mousePosition.x, areaRect.ZeroXY());
                    selectionRange = new Vector2(x, x);
                  } else {
                    // wait with tracking as this might as well be click-select
                  }
                  Event.current.Use();
                }
                break;

              case EventType.MouseDrag:
                if (_dragStart.HasValue) {
                  if (inRect && GUIUtility.hotControl != id) {
                    var deltaPixels = Event.current.mousePosition - _dragStart.Value;
                    if (Mathf.Abs(deltaPixels.x) > Styles.DragPixelsThreshold) {
                      GUIUtility.hotControl = id;
                      unselect = true;
                    }
                  }

                  if (GUIUtility.hotControl == id) {
                    if (doingSelect) {
                      if (enableRangeSelect) {
                        var minX = Mathf.Min(_dragStart.Value.x, Event.current.mousePosition.x);
                        var maxX = Mathf.Max(_dragStart.Value.x, Event.current.mousePosition.x);
                        selectionRange = new Vector2(minX, maxX) / r.width * range + new Vector2(start, start);
                      } else {
                        var x = PixelToTime(Event.current.mousePosition.x, areaRect.ZeroXY());
                        selectionRange = new Vector2(x, x);
                      }
                    } else if (doingDragScroll) {
                      var deltaTime = (Event.current.delta.x / r.width) * (range);
                      start -= deltaTime;
                    } else if (doingZoom) {
                      PerfomFocusedZoom(_dragStart.Value, r.ZeroXY(), Event.current.delta.x, minRange,
                        ref start, ref range);
                    }

                    Event.current.Use();
                  }
                }
                break;

              case EventType.MouseUp:
                _dragStart = null;
                if (GUIUtility.hotControl == id) {
                  GUIUtility.hotControl = 0;
                  Event.current.Use();
                } else {
                  selectionRange = null;
                  unselect = true;
                }
                break;
            }
          }
        }

        if (!allowScrollPastLimits) {
          range = Mathf.Clamp(range, minRange, maxValue - minValue);
          start = Mathf.Clamp(start, minValue, maxValue - range);
        }
      }

      public float PixelToTime(float pixel, Rect rect) {
        return (pixel - rect.x) * (range / rect.width) + start;
      }

      public float TimeToPixel(float time) => TimeToPixel(time, areaRect);

      public float TimeToPixel(float time, Rect rect) {
        return (time - start) / range * rect.width + rect.x;
      }
      private static void DrawHorizontalScrollbar(Rect rect, float maxValue, ref float start, ref float range) {
        var minScrollbarValue = 0.0f;

        maxValue = Mathf.Max(start + range, maxValue);
        minScrollbarValue = Mathf.Min(start, minScrollbarValue);

        if (Mathf.Abs((maxValue - minScrollbarValue) - range) <= 0.001f) {
          // fill scrollbar
          GUI.HorizontalScrollbar(rect, 0.0f, 1.0f, 0.0f, 1.0f);
        } else {
          // a workaround for
          maxValue += 0.00001f;
          start = GUI.HorizontalScrollbar(rect, start, range, minScrollbarValue, maxValue);
        }
      }

      private static void DrawVerticalScrollbar(Rect rect, float workspaceHeightNeeded, float workspaceHeight, ref float scroll) {
        if (workspaceHeight > workspaceHeightNeeded) {
          scroll = 0.0f;
          GUI.VerticalScrollbar(rect, 0, 1, 0, 1);
        } else {
          scroll = Mathf.Min(scroll, workspaceHeightNeeded - workspaceHeight);
          scroll = GUI.VerticalScrollbar(rect, scroll, workspaceHeight, 0, workspaceHeightNeeded);
        }
      }

      private static void DrawPowerSlider(Rect rect, float min, float max, float power, ref float scroll) {

        var pmin = Mathf.Pow(min, 1f / power);
        var pmax = Mathf.Pow(max, 1f / power);
        var pval = Mathf.Pow(scroll, 1f / power);

        pval = GUI.VerticalSlider(rect, pval, pmax, pmin);

        scroll = Mathf.Pow(pval, power);
      }


      private static void PerfomFocusedZoom(Vector2 zoomAround, Rect rect, float delta, float minRange, ref float start, ref float range) {
        var amount = Mathf.Clamp(delta * 0.01f, -0.9f, 0.9f);

        var oldRange = range;
        range *= (1 - amount);

        if (range < minRange) {
          range = minRange;
          amount = 1.0f - range / oldRange;
        }

        var pivot = zoomAround.x / rect.width;
        start += pivot * oldRange * amount;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/QuantumTypeSelectorPopupContent.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;

  internal class QuantumTypeSelectorPopupContent : PopupWindowContent {
    private readonly SearchField _searchField;

    private readonly TypeTreeView       _treeView;
    private          Type               _requestedType;
    private          Action<Type>       _onTypeSelected;
    private          List<TreeViewItem> _items      = new();
    private          bool               _itemsDirty = false;

    public QuantumTypeSelectorPopupContent(Action<Type> onTypeSelected) {
      _treeView = new TypeTreeView(new TreeViewState(), _items);
      _searchField = new SearchField();
      _searchField.SetFocus();

      _onTypeSelected = onTypeSelected;
      _treeView.OnTypeSelected += type => {
        _requestedType = type;
        _onTypeSelected?.Invoke(type);
      };
    }

    public override Vector2 GetWindowSize() {
      return new Vector2(500, 500);
    }

    public override void OnGUI(Rect rect) {

      if (_itemsDirty) {
        _treeView.Reload();
        _itemsDirty = false;
      }
      
      const int margin = 2;
      rect = new Rect(rect.x + margin, rect.y + margin, rect.width - margin * 2, rect.height - margin * 2);

      // search bar
      var searchRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

      // handle movement with arrow keys, as the tree does not have the focus
      if (Event.current.type == EventType.KeyDown) {
        int offsetSelection = 0;
        const int pageOffset = 10;
        const int homeOffset = 1000000;

        switch (Event.current.keyCode) {
          case KeyCode.Escape:
            Event.current.Use();
            editorWindow.Close();
            return;
          case KeyCode.Return:
            var selectedId = _treeView.GetSelection().FirstOrDefault();

            if (selectedId != 0) {
              var selectedRow = _treeView.GetRows().Where(x => x.id == selectedId).OfType<TypeTreeViewItem>().FirstOrDefault();

              if (selectedRow != null) {
                Event.current.Use();
                _requestedType = selectedRow.Type;
                _onTypeSelected?.Invoke(selectedRow.Type);
                editorWindow.Close();
                return;
              }
            }

            break;
          case KeyCode.DownArrow:
            offsetSelection = 1;
            break;
          case KeyCode.UpArrow:
            offsetSelection = -1;
            break;
          case KeyCode.PageDown:
            offsetSelection = pageOffset;
            break;
          case KeyCode.PageUp:
            offsetSelection = -pageOffset;
            break;
          case KeyCode.Home:
            offsetSelection = -homeOffset;
            break;
          case KeyCode.End:
            offsetSelection = homeOffset;
            break;
        }

        if (offsetSelection != 0) {
          _treeView.OffsetSelection(offsetSelection);
          Event.current.Use();
        }
      }

      var treeRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight, rect.width, rect.height - EditorGUIUtility.singleLineHeight);
      _treeView.OnGUI(treeRect);
      _treeView.searchString = _searchField.OnGUI(searchRect, _treeView.searchString);

      if (_requestedType != null) {
        editorWindow.Close();
      }
    }
    
    private class TypeTreeViewItem : TreeViewItem {
      public Type Type;
    }

    private class TypeTreeView : TreeView {
      
      public TypeTreeView(TreeViewState state, List<TreeViewItem> items) : base(state) {
        _items = items;
        Reload();
        showAlternatingRowBackgrounds = true;
      }

      public event Action<Type> OnTypeSelected;
      private readonly List<TreeViewItem> _items;
      
      protected override void SingleClickedItem(int id) {
        if (FindItem(id, rootItem) is TypeTreeViewItem item) {
          OnTypeSelected?.Invoke(item.Type);
        }
      }

      protected override bool DoesItemMatchSearch(TreeViewItem item, string search) {
        if (item is TypeTreeViewItem) {
          return base.DoesItemMatchSearch(item, search);
        } else {
          return false;
        }
      }

      protected override TreeViewItem BuildRoot() {
        var root = new TreeViewItem {
          id = 0,
          depth = -1,
          displayName = "Root"
        };
        var allItems = new List<TreeViewItem>();
        
        var id = 1;

        foreach (var item in _items) {
          item.id = id++;
          allItems.Add(item);
        }

        SetupParentsAndChildrenFromDepths(root, allItems);
        return root;
      }
      
      public void OffsetSelection(int offset) {
        IList<TreeViewItem> rows = this.GetRows();
        if (rows.Count == 0)
          return;
        int num = Mathf.Clamp(GetIndexOfID(rows, state.lastClickedID) + offset, 0, rows.Count - 1);
        SetSelection(new[] {
          rows[num].id
        });
        FrameItem(rows[num].id);
      }

      static int GetIndexOfID(IList<TreeViewItem> items, int id) {
        for (int index = 0; index < items.Count; ++index) {
          if (items[index].id == id)
            return index;
        }

        return -1;
      }
    }

    private int _currentDepth = 0;

    public void BeginGroup(string label) {
      _items.Add(new TreeViewItem {
        displayName = label,
        depth = _currentDepth++
      });
      _itemsDirty = true;
    }

    public void EndGroup() {
      _currentDepth--;
    }

    public void AddType(string label, Type type) {
      _items.Add(new TypeTreeViewItem {
        displayName = label,
        Type = type,
        depth = _currentDepth,
      });

      _itemsDirty = true;
    }
  }
  
}

#endregion


#region Assets/Photon/Quantum/Editor/UnityDB/QuantumAssetObjectPostprocessor.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;
  using Object = UnityEngine.Object;
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
  using UnityEditor.AddressableAssets.Settings;
#endif

#if !QUANTUM_DISABLE_ASSET_OBJECT_POSTPROCESSOR
  public partial class QuantumAssetObjectPostprocessor : AssetPostprocessor {
    
    [Flags]
    private enum ValidationResult {
      Ok,
      Dirty = 1,
      NeedsRefresh = 4,
    }

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
      
      if (QuantumEditorSettings.Get(x => x.UseQuantumUnityDBAssetPostprocessor) != true) {
        return;
      }
      
      var result = ValidationResult.Ok;
      var refreshHash = false;

      // first, process prototype files
      foreach (var assetPath in movedAssets) {
        if (assetPath.EndsWith(QuantumEntityPrototypeAssetObjectImporter.ExtensionWithDot, StringComparison.Ordinal)) {
          if (EnsurePrototypeAssetMatchesPrefab(assetPath)) {
            refreshHash = true;
          }
        }
      }
      
      foreach (var assetPath in importedAssets) {
        if (assetPath.EndsWith(QuantumEntityPrototypeAssetObjectImporter.ExtensionWithDot, StringComparison.Ordinal)) {
          if (EnsurePrototypeAssetMatchesPrefab(assetPath)) {
            refreshHash = true;
          }
        }
      }
      
      // second, refresh actual assets

      foreach (var assetPath in deletedAssets) {
        if (!CanBeAQuantumAsset(assetPath, checkExists: false)) {
          continue;
        }

        QuantumEditorLog.TraceImport($"Deleted possible Quantum asset: {assetPath}");
        refreshHash = true;
      }
      
      for (var i = 0; i < movedAssets.Length; ++i) {
        var assetPath = movedAssets[i];
        var oldAssetPath = movedFromAssetPaths[i];
        
        if (!CanBeAQuantumAsset(assetPath)) {
          continue;
        }
        
        var partialResult = ValidateQuantumAssetFile(assetPath);
        result |= partialResult;
        refreshHash = true;
        
        QuantumEditorLog.TraceImport($"Moved possible Quantum asset: {oldAssetPath} -> {assetPath} ({partialResult})");
      }
      
      foreach (var assetPath in importedAssets) {
        if (!CanBeAQuantumAsset(assetPath)) {
          continue;
        }
        
        var partialResult = ValidateQuantumAssetFile(assetPath);
        result |= partialResult;
        refreshHash = true;
        
        QuantumEditorLog.TraceImport($"Imported possible Quantum asset: {assetPath} ({partialResult})");
      }

      if ((result & ValidationResult.Dirty) != 0) {
        QuantumEditorLog.TraceImport($"AssetObjects dirty, saving");
        AssetDatabase.SaveAssets();
      }
      
      if ((result & ValidationResult.NeedsRefresh) != 0) {
        QuantumEditorLog.TraceImport($"AssetObjects needs AssetDatabase refresh");
        AssetDatabase.Refresh();
      }

      if (refreshHash) {
        QuantumEditorLog.TraceImport($"AssetObjects needs hash refresh");
        if (RefreshQuantumUnityDBImmediately) {
          QuantumUnityDBImporter.RefreshAssetObjectHash();
          AssetDatabase.Refresh();
        } else {
          QuantumEditorUtility.DelayCall(() => {
            QuantumUnityDBImporter.RefreshAssetObjectHash();
            AssetDatabase.Refresh();
          });
        }
      }
      
      // check if the db is invalidated
      foreach (var assetPath in importedAssets) {
        if (assetPath.EndsWith(QuantumUnityDBImporter.ExtensionWithDot, StringComparison.Ordinal)) {
          QuantumEditorLog.TraceImport($"Imported Quantum DB, make sure global is unloaded");
          QuantumUnityDB.UnloadGlobal();
        }
      }
      
      // check if guids overrides have been invalidated
      foreach (var assetPath in importedAssets) {
        if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(QuantumEditorSettings) && AssetDatabaseUtils.HasLabel(assetPath, QuantumGlobalScriptableObjectUtils.GlobalAssetLabel)) {
          var settings = AssetDatabase.LoadAssetAtPath<QuantumEditorSettings>(assetPath);
          if (settings) {
            settings.RefreshGuidOverridesHash();
            AssetDatabase.Refresh();
          }
        }
      }
    }
    
    private void OnPostprocessPrefab(GameObject prefab) {
      if (!CanBeAQuantumAsset(assetPath)) {
        return;
      }
      
      var legacyObjects = new List<Object>();
      this.context.GetObjects(legacyObjects);
      
      foreach (var handler in QuantumMonoBehaviourAssetSourceHandler.Instances) {
        var sourceComponent = (MonoBehaviour)prefab.GetComponent(handler.MonoBehaviourType);
        if (sourceComponent == null) {
          continue;
        }
        
        handler.OnPostprocessPrefab(context, sourceComponent);
      }
    }

    private static ValidationResult ValidateQuantumAssetFile(string path) {
      
      if (!CanBeAQuantumAsset(path)) {
        if (AssetDatabaseUtils.SetLabel(path, QuantumUnityDBUtilities.AssetLabel, false)) {
          QuantumEditorLog.TraceImport(path, $"Asset label removed from {path}");
        }
        return ValidationResult.Ok;
      }

      var result        = ValidationResult.Ok;
      var anyAssets     = false;

      var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
      var nestedAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
      
      if (mainAsset is Quantum.AssetObject asset) {
        result    |= ValidateQuantumAsset(asset, path, null);
        anyAssets =  true;
      } else if (mainAsset is GameObject prefab) {
        if (prefab.TryGetComponent(out QuantumEntityPrototype _)) {
          anyAssets = true;
          if (!nestedAssets.Any(x => x is EntityPrototype
#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
                  || x is global::EntityPrototype
#pragma warning restore CS0618
#endif
              )) {
            // if there are no nested assets (legacy), create a standalone asset
            if (TryCreateOrUpdatePrototypeAssetForPrefab(path, out _)) {
              QuantumEditorLog.TraceImport(path, $"Created {nameof(Quantum.EntityPrototype)} standalone asset for prefab");
              result |= ValidationResult.NeedsRefresh;
            }
          }
        }
      }

      foreach (var nestedAsset in nestedAssets) {
        if (nestedAsset is Quantum.AssetObject nestedQuantumAsset) {
          result |= ValidateQuantumAsset(nestedQuantumAsset, path, mainAsset);
          anyAssets = true;
        }
      }

      if ((result & ValidationResult.Dirty) != 0) {
        EditorUtility.SetDirty(mainAsset);
        //AssetDatabase.SaveAssets();
      }

      if (AssetDatabaseUtils.SetLabel(path, QuantumUnityDBUtilities.AssetLabel, anyAssets)) {
        QuantumEditorLog.TraceImport(path, $"Asset label added to {path}");
      }
      return result;
    }

    private static ValidationResult ValidateQuantumAsset(Quantum.AssetObject asset, string unityAssetPath, Object parentAsset) {
      Debug.Assert(!string.IsNullOrEmpty(unityAssetPath));

      ValidationResult result = ValidationResult.Ok;
      
      var expectedPath = QuantumUnityDBUtilities.GetExpectedAssetPath(unityAssetPath, asset.name, !parentAsset);
      var expectedGuid = QuantumUnityDBUtilities.GetExpectedAssetGuid(asset, out var isGuidOverriden);

      // handle for legacy nested asset naming
      if (parentAsset is GameObject prefab && QuantumMonoBehaviourAssetSourceHandler.TryGetHandlerFromName(asset.name, out var handler)) {
        var expectedName = handler.GetAssetName(parentAsset.name);
        if (asset.name != expectedName) {
          QuantumEditorLog.TraceImport(unityAssetPath, $"Asset had an incorrect name ({asset.name} vs {expectedName}), going to rename it.");
          asset.name =  expectedName;
          result |= ValidationResult.Dirty;
        }

        if (handler.OnValidateQuantumAsset(prefab, unityAssetPath, asset)) {
          result |= ValidationResult.Dirty;
        }
      }

      var  currentIdentifier = asset.Identifier;
      bool keepOldGuid       = currentIdentifier.Guid.IsValid;
      
      if (currentIdentifier.Path != expectedPath) {
        // has this asset been duplicated?
        if (QuantumUnityDBUtilities.TryGetUnityAssetPath(currentIdentifier, out var originalPath)) {
          QuantumEditorLog.TraceImport(unityAssetPath, $"Asset was seemingly duplicated from {originalPath}, going to assign the new guid {expectedGuid} (overriden: {isGuidOverriden})");
          Debug.Assert(unityAssetPath != originalPath, $"Asset was seemingly duplicated, but the paths are the same ({unityAssetPath} vs {originalPath}))");
          keepOldGuid = false;
        } else {
          QuantumEditorLog.TraceImport(unityAssetPath, $"Despite the path being different ({currentIdentifier.Path} vs {expectedPath}), the asset was not duplicated. Keeping the guid.");
        }
      }
      
      if (keepOldGuid) {
        if (expectedGuid != currentIdentifier.Guid) {
          expectedGuid = currentIdentifier.Guid;
          if (QuantumUnityDBUtilities.SetAssetGuidOverride(asset, expectedGuid)) {
            QuantumEditorLog.TraceImport(unityAssetPath, $"Guid override set to {expectedGuid}");
          } else {
            throw new InvalidOperationException($"Unable to set GUID override for asset {unityAssetPath}");
          }
        }
      }

      if (currentIdentifier.Guid != expectedGuid || currentIdentifier.Path != expectedPath) {
        // not only dirty, but the db needs to be invalidated
        result |= ValidationResult.Dirty;
      }
      
      if ((result & ValidationResult.Dirty) != 0) {
        QuantumEditorLog.TraceImport(unityAssetPath, $"Asset identifier changed from {currentIdentifier} to {expectedGuid} (overriden: {isGuidOverriden}) and {expectedPath}");
        asset.Identifier = new AssetObjectIdentifier() {
          Guid = expectedGuid,
          Path = expectedPath,
        };
      }
      
      return result;
    }

    private static bool IsPrefabPath(string assetPath) {
      return assetPath.EndsWith(".prefab", StringComparison.Ordinal);
    }
    
    private static bool CanBeAQuantumAsset(string assetPath, bool checkExists = true) {
      var extension = Path.GetExtension(assetPath);
      if (extension != ".asset" && extension != ".prefab" && extension != QuantumEntityPrototypeAssetObjectImporter.ExtensionWithDot) {
        return false;
      }

      if (QuantumEditorSettings.IsInAssetSearchPaths(assetPath) == false) {
        return false;
      }

      if (checkExists) {
        if (!File.Exists(assetPath)) {
          return false;
        }
      }

      return true;
    }

    #region Prototype Asset Handling

    public static bool TryCreateOrUpdatePrototypeAssetForPrefab(string prefabPath, out string assetPath) {
      var prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
      if (string.IsNullOrEmpty(prefabGuid)) {
        QuantumEditorLog.ErrorImport(prefabPath, $"Failed to get guid for prefab {prefabPath}");
        assetPath = null;
        return false;
      }
      
      assetPath = QuantumEntityPrototypeAssetObjectImporter.GetPathForPrefab(prefabPath);
      if (File.Exists(assetPath)) {
        // check if the contents match
        var existingGuid = File.ReadAllText(assetPath);
        if (string.Equals(existingGuid, prefabGuid, StringComparison.Ordinal)) {
          return false;
        }
      }

      File.WriteAllText(assetPath, prefabGuid);
      return true;
    }
    
    private static bool EnsurePrototypeAssetMatchesPrefab(string assetPath) {
      Debug.Assert(assetPath.EndsWith(QuantumEntityPrototypeAssetObjectImporter.ExtensionWithDot, StringComparison.Ordinal));
      if (!File.Exists(assetPath)) {
        // likely already moved
        return false;
      }

      // check if the prefab is in the right location
      var prefabGuid = File.ReadAllText(assetPath);
      var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
      if (string.IsNullOrEmpty(prefabPath)) {
        // invalid, destroy
        QuantumEditorLog.TraceImport(assetPath, $"Prefab with guid {prefabGuid} not found, deleting asset {assetPath}");
        AssetDatabase.DeleteAsset(assetPath);
        return true;
      }

      if (AssetDatabase.GetMainAssetTypeAtPath(prefabPath) != typeof(GameObject)) {
        // not a prefab, destroy
        QuantumEditorLog.TraceImport(assetPath, $"Asset at {prefabPath} is not a prefab, deleting asset {assetPath}");
        AssetDatabase.DeleteAsset(assetPath);
        return true;
      }

      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
      if (!prefab || !prefab.TryGetComponent(out QuantumEntityPrototype _)) {
        QuantumEditorLog.TraceImport(assetPath, $"Prefab {prefabPath} does not have {nameof(QuantumEntityPrototype)} component, deleting asset {assetPath}");
        AssetDatabase.DeleteAsset(assetPath);
        return true;
      }

      var desiredPath = QuantumEntityPrototypeAssetObjectImporter.GetPathForPrefab(prefabPath);
      var hasMoved = false;
      
      if (assetPath != desiredPath) {
        if (File.Exists(desiredPath)) {
          // nope can't do, destroy
          QuantumEditorLog.WarnImport(assetPath, $"Asset at {desiredPath} already exists, deleting asset {assetPath}");
          AssetDatabase.DeleteAsset(assetPath);
          return true;
        } else {
          QuantumEditorLog.TraceImport(assetPath, $"Moving to {desiredPath} to match the prefab's location");
          var error = AssetDatabase.MoveAsset(assetPath, desiredPath);
          if (!string.IsNullOrEmpty(error)) {
            QuantumEditorLog.WarnImport(assetPath, $"Failed to move to {desiredPath}: {error}");
            return false;
          } else {
            hasMoved = true;
          }
        }
      }

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
      SyncAddressableSettings(prefabGuid, AssetDatabase.AssetPathToGUID(desiredPath));
#endif
      
      return hasMoved;
    }

#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
    static void SyncAddressableSettings(string prefabGuid, string assetGuid) {
      var prefabAddressableEntry = AssetDatabaseUtils.GetAddressableAssetEntry(prefabGuid);
      if (prefabAddressableEntry != null) {
        AssetDatabaseUtils.CreateOrMoveAddressableAssetEntry(assetGuid, prefabAddressableEntry.parentGroup.name);
      } else {
        AssetDatabaseUtils.RemoveMoveAddressableAssetEntry(assetGuid);
      }
    }
    
    [InitializeOnLoadMethod]
    static void InitializeAddressablePrefabsWatcher() {
      AddressableAssetSettings.OnModificationGlobal += (settings, modificationEvent, data) => {
        if (QuantumEditorSettings.Get(x => x.UseQuantumUnityDBAssetPostprocessor) != true) {
          return;
        }
        
        switch (modificationEvent) {
          case AddressableAssetSettings.ModificationEvent.EntryAdded:
          case AddressableAssetSettings.ModificationEvent.EntryCreated:
          case AddressableAssetSettings.ModificationEvent.EntryModified:
          case AddressableAssetSettings.ModificationEvent.EntryMoved:
          case AddressableAssetSettings.ModificationEvent.EntryRemoved:

            IEnumerable<AddressableAssetEntry> entries;
            if (data is AddressableAssetEntry singleEntry) {
              entries = Enumerable.Repeat(singleEntry, 1);
            } else {
              entries = (IEnumerable<AddressableAssetEntry>)data;
            }
            
            foreach (var entry in entries) {
              if (entry.IsFolder) {
                continue;
              }
              if (AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath) != typeof(GameObject)) {
                continue;
              }
              if (!AssetDatabaseUtils.HasLabel(entry.AssetPath, QuantumUnityDBUtilities.AssetLabel)) {
                continue;
              }
              
              var prototypePath = QuantumEntityPrototypeAssetObjectImporter.GetPathForPrefab(entry.AssetPath);
              if (File.Exists(prototypePath)) {
                SyncAddressableSettings(entry.guid, AssetDatabase.AssetPathToGUID(prototypePath));
              }
            }

            break;
        }
      };
    }
    
    #endif
    
    #endregion
  }
  
#endif

  public partial class QuantumAssetObjectPostprocessor {
    internal static bool RefreshQuantumUnityDBImmediately = false; 
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/UnityDB/QuantumAssetSourceFactories.cs

namespace Quantum.Editor {
  using System;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  partial interface IQuantumAssetSourceFactory {
    IQuantumAssetObjectSource TryCreateAssetObjectSource(in QuantumAssetSourceFactoryContext context);
  }

  public class QuantumAssetSourceFactory {
    private readonly IQuantumAssetSourceFactory[] _factories = TypeCache.GetTypesDerivedFrom<IQuantumAssetSourceFactory>()
     .Select(x => (IQuantumAssetSourceFactory)Activator.CreateInstance(x))
     .OrderBy(x => x.Order)
     .ToArray();

    public IQuantumAssetObjectSource TryCreateAssetObjectSource(in QuantumAssetSourceFactoryContext context) {
      IQuantumAssetObjectSource source = null;
      for (int i = 0; i < _factories.Length && source == null; ++i) {
        source = _factories[i].TryCreateAssetObjectSource(in context);
      }

      return source;   
    }
  }

  partial struct QuantumAssetSourceFactoryContext {
    public Type AssetType {
      get {
        var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(AssetPath);
        if (IsMainAsset) {
          return mainAssetType;
        }
    
        // sadly AssetDatabase.GetTypeFromPathAndFileID() is broken, so there's a bit of guesswork
        if (mainAssetType == typeof(GameObject)) {
          if (QuantumMonoBehaviourAssetSourceHandler.TryGetHandlerFromName(AssetName, out var handler)) {
            return handler.AssetType;
          }
        }

        // last resort
        var instance = EditorUtility.InstanceIDToObject(InstanceID);
        if (instance) {
          return instance.GetType();
        }

        throw new InvalidOperationException();
      }
    }
  }

  partial class QuantumAssetSourceFactoryStatic {
    public IQuantumAssetObjectSource TryCreateAssetObjectSource(in QuantumAssetSourceFactoryContext context) {
      if (TryCreateInternal<QuantumAssetObjectSourceStaticLazy, Quantum.AssetObject>(context, out var result)) {
      };
      return result;
    }
  }
  
  partial class QuantumAssetSourceFactoryResource {
    public IQuantumAssetObjectSource TryCreateAssetObjectSource(in QuantumAssetSourceFactoryContext context) {
      if (TryCreateInternal<QuantumAssetObjectSourceResource, Quantum.AssetObject>(context, out var result)) {
        result.SerializableAssetType = context.AssetType;
      };
      return result;
    }
  }
  
#if (QUANTUM_ADDRESSABLES || QUANTUM_ENABLE_ADDRESSABLES) && !QUANTUM_DISABLE_ADDRESSABLES
  partial class QuantumAssetSourceFactoryAddressable {
    public IQuantumAssetObjectSource TryCreateAssetObjectSource(in QuantumAssetSourceFactoryContext context) {
      if (TryCreateInternal<QuantumAssetObjectSourceAddressable, Quantum.AssetObject>(context, out var result)) {
        result.SerializableAssetType = context.AssetType;
      };
      return result;
    }
  }
#endif
}

#endregion


#region Assets/Photon/Quantum/Editor/UnityDB/QuantumEntityViewAssetSourceHandler.cs

namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  class QuantumEntityViewAssetSourceHandler : QuantumMonoBehaviourAssetSourceHandler {
    
    private const long AssetFileId = 8375094554870764326;
    
    public override Type   MonoBehaviourType => typeof(QuantumEntityView);
    public override Type   AssetType         => typeof(Quantum.EntityView);
    
    public override string NestedNameSuffix  => "EntityView";
    public override int    Order             => 1000;

    public override void OnPostprocessPrefab(AssetImportContext context, MonoBehaviour monoBehaviour) {

      if (GetObject<Quantum.EntityView>(context)) {
        return;
      }

#if QUANTUM_ENABLE_MIGRATION
#pragma warning disable CS0618
      if (GetObject<global::EntityView>(context)) {
        // legacy
        return;
      }
#pragma warning restore CS0618
#endif
      
      var prefabGuid = AssetDatabaseUtils.GetAssetGuidOrThrow(context.assetPath);
      
      var asset = ScriptableObject.CreateInstance<Quantum.EntityView>();
      asset.name     = GetAssetName(monoBehaviour.name);
      asset.Prefab   = monoBehaviour.gameObject;

      asset.Guid = QuantumUnityDBUtilities.GetExpectedAssetGuid(new GUID(prefabGuid), AssetFileId, out var isOverride);
      asset.Path = QuantumUnityDB.CreateAssetPathFromUnityPath(context.assetPath, NestedNameSuffix);
      
      QuantumUnityDBUtilities.AddAssetGuidOverridesDependency(context);
      context.AddObjectToAsset("View", asset);
    }

    public override bool OnValidateQuantumAsset(GameObject prefab, string prefabPath, AssetObject asset) {
      var view = (Quantum.EntityView)asset;
      if (view.Prefab != prefab) {
        view.Prefab = prefab;
        return true;
      } else {
        return false;
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/UnityDB/QuantumMonoBehaviourAssetSourceHandler.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  public abstract class QuantumMonoBehaviourAssetSourceHandler {
    
    public static readonly QuantumMonoBehaviourAssetSourceHandler[] Instances = TypeCache.GetTypesDerivedFrom<QuantumMonoBehaviourAssetSourceHandler>()
     .Select(x => (QuantumMonoBehaviourAssetSourceHandler)Activator.CreateInstance(x))
     .OrderBy(x => x.Order)
     .ToArray();

    public static bool TryGetHandlerFromName(string assetName, out QuantumMonoBehaviourAssetSourceHandler result) {
      foreach (var handler in QuantumMonoBehaviourAssetSourceHandler.Instances) {
        if (assetName.EndsWith(handler.NestedNameSuffix)) {
          result = handler;
          return true;
        }
      }

      result = null;
      return false;
    }
    
    public abstract System.Type MonoBehaviourType { get; }
    public abstract System.Type AssetType         { get; }
    public abstract string      NestedNameSuffix  { get; }
    public abstract int         Order             { get; }

    public abstract void OnPostprocessPrefab(AssetImportContext context, MonoBehaviour monoBehaviour);
    //public abstract void OnImportQuantumPrefabAsset(MonoBehaviour source, QuantumPrefabAsset prefabAsset);

    private List<UnityEngine.Object> _objectBuffer = new();

    protected T GetObject<T>(AssetImportContext context) where T : UnityEngine.Object {
      _objectBuffer.Clear();
      context.GetObjects(_objectBuffer);
      try {
        foreach (var obj in _objectBuffer) {
          if (obj is T t) {
            return t;
          }
        }

        return default;
      } finally{
        _objectBuffer.Clear();
      }
    }
    

    public string GetAssetName(string parentName) {
      return parentName + NestedNameSuffix;
    }

    public abstract bool OnValidateQuantumAsset(GameObject prefab, string prefabPath, AssetObject asset);
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/UnityDB/QuantumUnityDBUtilities.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  /// <summary>
  /// QuantumUnityDB Editor utilities.
  /// </summary>
  public static class QuantumUnityDBUtilities {

    /// <summary>
    /// Label applied to Quantum AssetObject assets.
    /// </summary>
    public const string AssetLabel = "QuantumAsset";
    
    /// <summary>
    /// Returns the default path to the <see cref="QuantumUnityDB"/> asset. 
    /// </summary>
    /// <returns></returns>
    /// <see cref="QuantumGlobalScriptableObject{T}"/>
    public static string GetGlobalDBPath() {
      if (!QuantumUnityDB.TryGetGlobal(out var instance)) {
        return string.Empty;
      }
      
      return AssetDatabase.GetAssetPath(instance);
    }
    
    /// <summary>
    /// Refreshes the global Quantum Unity DB. If <paramref name="force"/> is false, an update will only happen if the hash calculated
    /// with assets under <see cref="AssetLabel"/> label has changed. If <paramref name="force"/> is true, a refresh will be performed
    /// regardless.
    /// </summary>
    /// <param name="force">Force the reimport.</param>
    public static void RefreshGlobalDB(bool force = false) {
      QuantumUnityDBImporter.RefreshAssetObjectHash();
      if (force) {
        var path = GetGlobalDBPath();
        if (!string.IsNullOrEmpty(path)) {
          AssetDatabase.ImportAsset(path);
        }
      } else {
        AssetDatabase.Refresh();
      }
    }

    /// <summary>
    /// Sets the Quantum Unity DB refresh mode. By default, after an asset with <see cref="AssetLabel"/> is imported, a call to
    /// <see cref="QuantumUnityDBImporter.RefreshAssetObjectHash"/> is scheduled. When running in batch mode, it may result
    /// in the hash never being refreshed and the global <see cref="QuantumUnityDB"/> not being updated. To prevent this,
    /// call this method with <paramref name="immediate"/> set to true.
    /// </summary>
    /// <param name="immediate">Should <see cref="QuantumUnityDBImporter.RefreshAssetObjectHash"/> be called immediately
    /// after an asset import.</param>
    public static void SetQuantumUnityDBRefreshMode(bool immediate) {
      QuantumAssetObjectPostprocessor.RefreshQuantumUnityDBImmediately = immediate;
    }

    [MenuItem("Tools/Quantum/Export/Asset Database", false, (int)QuantumEditorMenuPriority.Export + 0)]
    private static void ExportAsJsonMenu() => ExportAsJson();

    /// <summary>
    /// Export the global DB to a JSON file. A dialog is shown to select the file.
    /// </summary>
    /// <returns><see langword="true"/> if a file was selected and saved into.</returns>
    /// <see cref="ExportAsJson(string)"/>
    public static bool ExportAsJson() {
      var lastLocation = LastExportLocation;

      var directory = string.IsNullOrEmpty(lastLocation) ? "." : Path.GetDirectoryName(lastLocation);
      var defaultName = string.IsNullOrEmpty(lastLocation) ? "db" : Path.GetFileName(lastLocation);

      var filePath = EditorUtility.SaveFilePanel("Save Quantum Asset DB to..", directory, defaultName, "json");

      if (string.IsNullOrEmpty(filePath)) {
        return false;
      }

      LastExportLocation = filePath;
      ExportAsJson(filePath);
      return true;
    }

    /// <summary>
    /// Export the global DB to a JSON file with <see cref="QuantumUnityJsonSerializer"/>. None of the asset will have its
    /// <see cref="AssetObject.Loaded"/> called, as they are loaded with <see cref="IQuantumAssetObjectSource.EditorInstance"/>.
    /// </summary>
    /// <param name="path">Output path.</param>
    public static void ExportAsJson(string path) {
      if (string.IsNullOrEmpty(path)) {
        throw new ArgumentException(nameof(path));
      }

      List<AssetObject> assetObjects = new List<AssetObject>();

      foreach (var entry in QuantumUnityDB.Global.Entries) {
        var assetObject = entry.Source.EditorInstance;

        if (assetObject == null) {
          throw new InvalidOperationException($"No editor instance for {entry}");
        }
        
        assetObjects.Add(assetObject);
      }

      var serializer = new QuantumUnityJsonSerializer();
      
      using (var file = File.Create(path)) {
        serializer.SerializeAssets(file, assetObjects.ToArray());
      }
    }

    /// <summary>
    /// Enumerates <see cref="AssetObject"/> assets with <see cref="AssetLabel"/> label.
    /// </summary>
    public static AssetDatabaseUtils.AssetEnumerable IterateAssets() => AssetDatabaseUtils.IterateAssets(type: typeof(AssetObject), label: AssetLabel);
    
    /// <summary>
    /// Create a deterministic <see cref="AssetGuid"/> for the given asset, based on its Unity GUID and FileID.
    /// </summary>
    public static AssetGuid CreateDeterministicAssetGuid(LazyLoadReference<UnityEngine.Object> asset) {
      var (guidStr, fileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(asset);
      return CreateDeterministicAssetGuid(new GUID(guidStr), fileId);
    }
    
    /// <summary> 
    /// Create a deterministic <see cref="AssetGuid"/> for a Unity GUID and a FileID.
    /// </summary>
    public static unsafe AssetGuid CreateDeterministicAssetGuid(GUID unityAssetGuid, long fileID) {
      var pguid = (uint*)&unityAssetGuid;

      var hash = (ulong)fileID;
      hash =  hash * 397 ^ pguid[0];
      hash =  hash * 397 ^ pguid[1];
      hash =  hash * 397 ^ pguid[2];
      hash =  hash * 397 ^ pguid[3];
      hash &= 0x7FFFFFFFFFFFFFFF;

      hash &= ~AssetGuid.ReservedBits;
      return new AssetGuid((long)hash);
    }
    
    /// <summary>
    /// Returns the expected <see cref="AssetGuid"/> for the given Unity asset GUID and FileID. If an override is set, it will be returned instead.
    /// </summary>
    public static AssetGuid GetExpectedAssetGuid(GUID guid, long fileId, out bool isOverride) {
      if (QuantumEditorSettings.Global.TryGetAssetGuidOverride(guid, fileId, out var assetGuid)) {
        isOverride = true;
        return assetGuid;
      } else {
        isOverride = false;
        return CreateDeterministicAssetGuid(guid, fileId);
      }
    }
    
    /// <summary>
    /// Returns the expected <see cref="AssetGuid"/> for the given asset. If an override is set, it will be returned instead.
    /// </summary>
    public static AssetGuid GetExpectedAssetGuid(LazyLoadReference<UnityEngine.Object> asset, out bool isOverride) {
      var (guidStr, fileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(asset);
      return GetExpectedAssetGuid(new GUID(guidStr), fileId, out isOverride);
    }
    
    /// <summary>
    /// Returns true if the given asset has an AssetGuid override set.
    /// </summary>
    public static bool IsAssetGuidOverriden(LazyLoadReference<UnityEngine.Object> asset) {
      GetExpectedAssetGuid(asset, out var isOverride);
      return isOverride;
    }
    
    /// <summary>
    /// Sets the AssetGuid override for the given asset. If the override is already set, it will be replaced. If
    /// <paramref name="assetGuid"/> is <see cref="AssetGuid.Invalid"/>, the override will be removed.
    /// </summary>
    /// <returns><see langword="true"/> if there actually was a change</returns>
    public static bool SetAssetGuidOverride(LazyLoadReference<UnityEngine.Object> asset, AssetGuid assetGuid) {
      return QuantumEditorSettings.Global.SetGuidOverride(asset, assetGuid, out _);
    }

    /// <summary>
    /// Removes the AssetGuid override for the given asset. 
    /// </summary>
    /// <returns>The override value, if present or <see cref="AssetGuid.Invalid"/>.</returns>
    public static AssetGuid RemoveAssetGuidOverride(GUID guid, long fileId) {
      return QuantumEditorSettings.Global.RemoveGuidOverride(guid, fileId);
    }

    internal static string GetExpectedAssetPath(LazyLoadReference<UnityEngine.Object> asset, string objectName, bool isMainAsset) {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var unityAssetGuid, out long _)) {
        throw new InvalidOperationException($"Failed to get Unity GUID and FileID for instanceId '{asset}'");
      }

      var unityAssetPath = AssetDatabaseUtils.GetAssetPathOrThrow(unityAssetGuid);
      return GetExpectedAssetPath(unityAssetPath, objectName, isMainAsset);
    }

    internal static string GetExpectedAssetPath(string unityAssetPath, string objectName, bool isMainAsset) {
      Type parentAssetType = null;
      if (!isMainAsset) {
        parentAssetType = AssetDatabase.GetMainAssetTypeAtPath(unityAssetPath);
        if (parentAssetType == null) {
          throw new InvalidOperationException($"Failed to get main asset type for '{unityAssetPath}'");
        }
      }
      
      return GetExpectedAssetPath(unityAssetPath, objectName, parentAssetType);
    }
    
    internal static string GetExpectedAssetPath(string unityAssetPath, string objectName, Type parentAssetType) {

      // now the tricky part: we need to figure out the path; there are three categories of paths:
      // 1) main asset: relative to /Assets, drop the extension
      // 2) assets nested in prefabs: parent asset path + | + prefab name + quantum asset type name
      // 3) assets nested in non-prefabs: parent asset path + | + instanceName

      if (parentAssetType == null) {
        return QuantumUnityDB.CreateAssetPathFromUnityPath(unityAssetPath);
      } else {
        string nameForNesting = objectName;
        
        if (parentAssetType == typeof(GameObject)) {
          // if only AssetDatabase.GetTypeFromPathAndFileID() worked...
          if (QuantumMonoBehaviourAssetSourceHandler.TryGetHandlerFromName(objectName, out var handler)) {
            nameForNesting = handler.NestedNameSuffix;
          } else {
            throw new NotSupportedException($"Nested asset type not supported: {objectName} ({unityAssetPath})");
          }
        }

        return QuantumUnityDB.CreateAssetPathFromUnityPath(unityAssetPath, nameForNesting);
      }
    }
    
    internal static bool TryGetUnityAssetPath(AssetObjectIdentifier identifier, out string unityAssetPath) {
      if (string.IsNullOrEmpty(identifier.Path)) {
        unityAssetPath = string.Empty;
        return false;
      }

      // possible duplication
      var sourceAssetPath = identifier.Path;

      // ditch everything after the separator 
      var separatorIndex = sourceAssetPath.LastIndexOf(QuantumUnityDB.NestedPathSeparator);
      if (separatorIndex >= 0) {
        sourceAssetPath = sourceAssetPath.Substring(0, separatorIndex);
      }

      foreach (var possiblePath in new[] { $"Assets/{sourceAssetPath}.asset", $"Assets/{sourceAssetPath}.prefab" }) {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(possiblePath).OfType<AssetObject>()) {
          if (asset.Guid == identifier.Guid) {
            unityAssetPath = possiblePath;
            return true;
          }
        }
      }

      unityAssetPath = string.Empty;
      return false;
    }

    internal static bool SetAssetGuidDeterministic(IEnumerable<AssetObject> assets, bool enabled, bool warnIfChange = false) {
      if (enabled) {
        var overridenAssets = assets
         .Where(x => {
            GetExpectedAssetGuid(x, out var isOverride);
            return isOverride;
          })
         .ToList();
        
        if (overridenAssets.Count == 0) {
          return false;
        }
      
        if (warnIfChange) {
          var assetWithNonDeterministicGuids = overridenAssets
           .Where(x => CreateDeterministicAssetGuid(x) != x.Guid)
           .ToList();
      
          if (assetWithNonDeterministicGuids.Count > 0) {
            const int limit = 10;
            string dialogMessage = $"For {assetWithNonDeterministicGuids.Count} out of selected assets, disabling GUID override will alter their Quantum AssetGuid. " +
              $"All AssetRef fields referencing these assets will become invalid.\n\n" +
              $"{string.Join("\n", assetWithNonDeterministicGuids.Take(limit).Select(x => $"{x.name}: {x.Guid} -> {CreateDeterministicAssetGuid(x)}"))}" +
              $"{(assetWithNonDeterministicGuids.Count > limit ? $"\n{assetWithNonDeterministicGuids.Count - limit} more ..." : "")}" +
              $"\n\nDo you want to proceed?";
      
            bool dialogResult = EditorUtility.DisplayDialog("Quantum GUID Override Warning", dialogMessage, "Yes", "No");
            if (!dialogResult) {
              return false;
            }
          }
        }
      
        foreach (var asset in overridenAssets) {
          
          // force deterministic guid
          SetAssetGuidOverride(asset, default);
          asset.Guid = CreateDeterministicAssetGuid(asset);
          
          // asset needs to be marked dirty, won't save otherwise
          AssetDatabaseUtils.SetAssetAndTheMainAssetDirty(asset);
        }
        
        return true;
      } else {
        bool any = false;
        foreach (var asset in assets) {
          var guid = GetExpectedAssetGuid(asset, out var isOverriden);
          if (!isOverriden) {
            any |= SetAssetGuidOverride(asset, guid);
          }
        }
      
        return any;
      }
    }

    internal static void AddAssetGuidOverridesDependency(AssetImportContext ctx) {
      ctx.DependsOnCustomDependency(QuantumEditorSettings.AssetGuidOverrideDependency);
    }

    /// <summary>
    /// Appends the <see cref="AssetGuid"/> to the hash.
    /// </summary>
    public static void Append(this ref Hash128 hash, AssetGuid guid) {
      unchecked {
        var value = (ulong)guid.Value;
        var lw = (int)value;
        var hw = (int)(value >> 32);
        hash.Append(lw);
        hash.Append(hw);
      }
    }
    
    private static string LastExportLocation {
      get => EditorPrefs.GetString("Quantum_Export_LastDBLocation");
      set => EditorPrefs.SetString("Quantum_Export_LastDBLocation", value);
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/Utils/AssetDatabaseExtensions.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.Linq;
  using System.Text.RegularExpressions;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditorInternal;
  using UnityEngine;

  public static class AssetDatabaseExt {
    public static void DeleteNestedAsset(this Object parent, Object child) {
      // destroy child
      Object.DestroyImmediate(child, true);

      // set dirty
      EditorUtility.SetDirty(parent);

      // save
      AssetDatabase.SaveAssets();
    }

    public static void DeleteAllNestedAssets(this Object parent) {
      // get path of parent object
      var path = AssetDatabase.GetAssetPath(parent);

      // LoadAllAssetsAtPath() returns the parent asset AND all of its nested (chidren)
      var assets = AssetDatabase.LoadAllAssetsAtPath(path);
      foreach (var asset in assets) {

        // keep main (parent) asset
        if (AssetDatabase.IsMainAsset(asset))
          continue;

        // delete nested assets
        parent.DeleteNestedAsset(asset);
      }
    }

    public static Object CreateNestedScriptableObjectAsset(this Object parent, System.Type type, System.String name, HideFlags hideFlags = HideFlags.None) {
      // create new asset in memory
      Object asset;

      asset = ScriptableObject.CreateInstance(type);
      asset.name = name;
      asset.hideFlags = hideFlags;

      // add to parent asset
      AssetDatabase.AddObjectToAsset(asset, parent);

      // set dirty
      EditorUtility.SetDirty(parent);

      // save
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();

      return asset;
    }

    public static Object FindNestedObjectParent(this Object asset) {
      var assetPath = AssetDatabase.GetAssetPath(asset);
      if (string.IsNullOrEmpty(assetPath)) {
        return null;
      }

      return AssetDatabase.LoadMainAssetAtPath(assetPath);
    }

    public static int DeleteMissingNestedScriptableObjects(string path) {

      var yamlObjectHeader = new Regex("^--- !u!", RegexOptions.Multiline);
     
      // 114 - class id (see https://docs.unity3d.com/Manual/ClassIDReference.html)
      var monoBehaviourRegex = new Regex(@"^114 &(\d+)");

      // if a script is missing, then it will load as null
      List<long> validFileIds = new List<long>();
      foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
        if (asset == null)
          continue;

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.GetInstanceID(), out var guid, out long fileId)) {
          validFileIds.Add(fileId);
        }
      }

      var yamlObjects = yamlObjectHeader.Split(System.IO.File.ReadAllText(path)).ToList();

      // now remove all that's missing
      int initialCount = yamlObjects.Count;
      for (int i = 0; i < yamlObjects.Count; ++i) {
        var part = yamlObjects[i];
        var m = monoBehaviourRegex.Match(part);
        if (!m.Success)
          continue;

        var assetFileId = long.Parse(m.Groups[1].Value);
        if (!validFileIds.Remove(assetFileId)) {
          yamlObjects.RemoveAt(i--);
        }
      }

      Debug.Assert(initialCount >= yamlObjects.Count);
      if (initialCount != yamlObjects.Count) {
        System.IO.File.WriteAllText(path, string.Join("--- !u!", yamlObjects));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return initialCount - yamlObjects.Count;
      } else {
        return 0;
      }
    }

    [System.Serializable]
    private class AssemblyDefnitionSurrogate {
      public string[] references = new string[0];
    }

    public static string[] GetReferences(this AssemblyDefinitionAsset assemblyDefinition) {
      var json = assemblyDefinition.text;
      return JsonUtility.FromJson<AssemblyDefnitionSurrogate>(json).references;
    }

    public static void SetReferences(this AssemblyDefinitionAsset assemblyDefinition, string[] references) {
      var json = assemblyDefinition.text;

      string newReferencesString;
      if (references.Length > 0) {
        newReferencesString = string.Join(",", references.Select(x => $"\n        \"{x}\""));
      } else {
        newReferencesString = "";
      }

      var regex = new Regex(@"(""references""\s?:\s?\[).*?(\s*\])", RegexOptions.Singleline);

      if (regex.IsMatch(json)) {
        var fixedJson = regex.Replace(json, $"$1{newReferencesString}$2");
        var path = AssetDatabase.GetAssetPath(assemblyDefinition);
        if (string.IsNullOrEmpty(path)) {
          throw new System.ArgumentException("Not an asset", nameof(assemblyDefinition));
        }
        System.IO.File.WriteAllText(path, fixedJson);
        AssetDatabase.ImportAsset(path);
      } else {
        throw new System.NotImplementedException();
      }
    }

    public static void UpdateReferences(this AssemblyDefinitionAsset asmdef, IEnumerable<(string, string)> referencesToAdd, IEnumerable<(string, string)> referencesToRemove) {
      var existingReferences = asmdef.GetReferences().ToList();

      if (referencesToRemove != null) {
        foreach (var r in referencesToRemove) {
          existingReferences.Remove($"GUID:{r.Item1}");
          existingReferences.Remove(r.Item2);
        }
      }

      if (referencesToAdd != null) {
        foreach (var r in referencesToAdd) {
          existingReferences.Remove($"GUID:{r.Item1}");
          existingReferences.Remove(r.Item2);
        }

        // guess whether to use guids or names
        bool useGuids = true;
        if (existingReferences.FirstOrDefault()?.StartsWith("GUID:", System.StringComparison.OrdinalIgnoreCase) == false) {
          useGuids = false;
        }

        foreach (var r in referencesToAdd) {
          if (useGuids) {
            existingReferences.Add($"GUID:{r.Item1}");
          } else {
            existingReferences.Add(r.Item2);
          }
        }
      }

      asmdef.SetReferences(existingReferences.ToArray());
    }

    public static bool HasScriptingDefineSymbol(NamedBuildTarget buildTarget, string value) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget).Split(';');
      return System.Array.IndexOf(defines, value) >= 0;
    }

    public static bool? HasScriptingDefineSymbol(string value) {
      bool anyDefined = false;
      bool anyUndefined = false;
      foreach (var group in ValidBuildTargetGroups) {
        if (HasScriptingDefineSymbol(group, value)) {
          anyDefined = true;
        } else {
          anyUndefined = true;
        }
      }

      return (anyDefined && anyUndefined) ? (bool?)null : anyDefined;
    }

    public static void UpdateScriptingDefineSymbol(NamedBuildTarget group, string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(new[] { group },
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }

    public static void UpdateScriptingDefineSymbol(string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }

    public static void UpdateScriptingDefineSymbol(NamedBuildTarget group, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      UpdateScriptingDefineSymbolInternal(new[] { group },
        definesToAdd,
        definesToRemove);
    }

    public static void UpdateScriptingDefineSymbol(IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        definesToAdd,
        definesToRemove);
    }

    private static void UpdateScriptingDefineSymbolInternal(IEnumerable<NamedBuildTarget> groups, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      EditorApplication.LockReloadAssemblies();
      try {
        foreach (var group in groups) {
          var originalDefines = PlayerSettings.GetScriptingDefineSymbols(group);
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
          PlayerSettings.SetScriptingDefineSymbols(group, newDefines);
        }
      } finally {
        EditorApplication.UnlockReloadAssemblies();
      }
    }

    private static bool IsEnumValueObsolete<T>(string valueName) where T : System.Enum {
      var fi = typeof(T).GetField(valueName);
      var attributes = fi.GetCustomAttributes(typeof(System.ObsoleteAttribute), false);
      return attributes?.Length > 0;
    }

    public static IEnumerable<NamedBuildTarget> ValidBuildTargetGroups {
      get {
        foreach (var name in System.Enum.GetNames(typeof(BuildTargetGroup))) {
          if (IsEnumValueObsolete<BuildTargetGroup>(name))
            continue;
          var group = (BuildTargetGroup)System.Enum.Parse(typeof(BuildTargetGroup), name);
          if (group == BuildTargetGroup.Unknown)
            continue;

          yield return NamedBuildTarget.FromBuildTargetGroup(group);
        }
      }
    }

    public static bool HasLabel(UnityEngine.Object obj, string label) {
      var labels = AssetDatabase.GetLabels(obj);
      var index = System.Array.IndexOf(labels, label);
      return index >= 0;
    }

    public static bool SetLabel(UnityEngine.Object obj, string label, bool present) {
      var labels = AssetDatabase.GetLabels(obj);
      var index = System.Array.IndexOf(labels, label);
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

    public static string GetAssetPathOrThrow(Object obj) {
      var result = AssetDatabase.GetAssetPath(obj);
      if (string.IsNullOrEmpty(result)) {
        throw new System.ArgumentException($"Asset path not found for ({obj?.name})", nameof(obj));
      }
      return result;
    }

    public static string GetAssetPathOrThrow(string guid) {
      var result = AssetDatabase.GUIDToAssetPath(guid);
      if (string.IsNullOrEmpty(result)) {
        throw new System.ArgumentException($"Asset path not found for ({guid})", nameof(guid));
      }
      return result;
    }


    public static string GetAssetGuidOrThrow(Object obj) {
      if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _)) {
        throw new System.ArgumentException($"Guid not found for ({obj?.name})", nameof(obj));
      }
      return guid;
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/Utils/BinarySerializer.cs

namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using UnityEngine;

  public class BinarySerializer : IDisposable {

    private readonly BinaryWriterEx _writer;
    private readonly BinaryReaderEx _reader;

    public delegate void ElementSerializer<T>(BinarySerializer serializer, ref T element);

    private sealed class BinaryWriterEx : BinaryWriter {
      public BinaryWriterEx(Stream stream, System.Text.Encoding encoding, bool leaveOpen) : base(stream, encoding, leaveOpen) { }

      public void Write7BitEncoded(int value) => base.Write7BitEncodedInt(value);
      public void Write7BitEncoded(long value) {
        ulong num;
        for (num = (ulong)value; num >= 128; num >>= 7) {
          Write((byte)(num | 0x80));
        }
        Write((byte)num);
      }
    }

    private sealed class BinaryReaderEx : BinaryReader {
      public BinaryReaderEx(Stream stream, System.Text.Encoding encoding, bool leaveOpen) : base(stream, encoding, leaveOpen) { }

      public int Read7BitEncodedInt32() => base.Read7BitEncodedInt();

      public long Read7BitEncodedInt64() {
        long num = 0;
        int num2 = 0;
        byte b;
        do {
          if (num2 == 70) {
            throw new InvalidOperationException();
          }
          b = ReadByte();
          num |= ((long)(b & 0x7F)) << num2;
          num2 += 7;
        }
        while ((b & 0x80) != 0);
        return num;
      }
    }

    public BinarySerializer(Stream stream, bool writing, Encoding encoding, bool leaveOpen) {
      if (writing) {
        _writer = new BinaryWriterEx(stream, encoding, leaveOpen);
      } else {
        _reader = new BinaryReaderEx(stream, encoding, leaveOpen);
      }
    }

    public BinarySerializer(Stream stream, bool writing) : this(stream, writing, Encoding.Default, false) { }

    public bool IsWriting => _writer != null;
    public bool IsReading => !IsWriting;

    public void Dispose() {
      if (_writer != null) {
        _writer.Dispose();
      } else {
        _reader.Dispose();
      }
    }

    public void Serialize(ref byte value) {
      if (_writer != null) {
        _writer.Write(value);
      } else {
        value = _reader.ReadByte();
      }
    }

    public void Serialize(ref bool value) {
      if (_writer != null) {
        _writer.Write(value);
      } else {
        value = _reader.ReadBoolean();
      }
    }

    public void Serialize(ref float value) {
      if (_writer != null) {
        _writer.Write(value);
      } else {
        value = _reader.ReadSingle();
      }
    }

    public void Serialize(ref Color value) {
      Serialize(ref value.r);
      Serialize(ref value.g);
      Serialize(ref value.b);
      Serialize(ref value.a);
    }



    public void Serialize(ref string value) {
      if (_writer != null) {
        _writer.Write(value);
      } else {
        value = _reader.ReadString();
      }
    }

    public void Serialize(ref int value) {
      if (_writer != null) {
        _writer.Write(value);
      } else {
        value = _reader.ReadInt32();
      }
    }

    public void Serialize(ref long value) {
      if (_writer != null) {
        _writer.Write(value);
      } else {
        value = _reader.ReadInt64();
      }
    }

    public void Serialize(ref byte[] value) {
      if (_writer != null) {
        _writer.Write7BitEncoded(value.Length);
        _writer.Write(value);
      } else {
        int count = _reader.Read7BitEncodedInt32();
        value = _reader.ReadBytes(count);
      }
    }

    public void Serialize<T>(ref T value, Func<T, int> toInt, Func<int, T> fromInt) {
      if (_writer != null) {
        _writer.Write(toInt(value));
      } else {
        value = fromInt(_reader.ReadInt32());
      }
    }

    public void Serialize<T>(ref T value, Func<T, byte[]> toBytes, Func<byte[], T> fromBytes) {
      if (_writer != null) {
        var bytes = toBytes(value);
        _writer.Write7BitEncoded(bytes.Length);
        _writer.Write(bytes);
      } else {
        int count = _reader.Read7BitEncodedInt32();
        var bytes = _reader.ReadBytes(count);
        value = fromBytes(bytes);
      }
    }

    public void Serialize7BitEncoded(ref int value) {
      if (_writer != null) {
        _writer.Write7BitEncoded(value);
      } else {
        value = _reader.Read7BitEncodedInt32();
      }
    }

    public void Serialize7BitEncoded(ref long value) {
      if (_writer != null) {
        _writer.Write7BitEncoded(value);
      } else {
        value = _reader.Read7BitEncodedInt64();
      }
    }

    public void SerializeList<T>(ref List<T> list, ElementSerializer<T> serializer) where T : new() {
      if (_writer != null) {
        _writer.Write(list != null ? list.Count : 0);
        if (list != null) {
          for (int i = 0; i < list.Count; ++i) {
            var element = list[i];
            serializer(this, ref element);
          }
        }
      } else {

        if (list == null) {
          list = new List<T>();
        } else {
          list.Clear();
        }

        var count = _reader.ReadInt32();
        list.Capacity = count;

        for (int i = 0; i < count; ++i) {
          var element = new T();
          serializer(this, ref element);
          list.Add(element);
        }
      }
    }
  }
}




#endregion


#region Assets/Photon/Quantum/Editor/Utils/EnterPlayModeOptionsHandler.cs

namespace Quantum.Editor {
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  internal static partial class EnterPlayModeOptionsHandler {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics() {
      bool enabled = true;
      IsEnabledUser(ref enabled);
      if (enabled) {
        ResetUnityStatics();
        ResetSimulationStatics();
      }
    }

    public static void ResetUnityStatics() {
      QuantumUnityDB.UnloadGlobal();

      QuantumCallback.Clear();
      QuantumEvent.Clear();

      QuantumMapLoader.ResetStatics();
      DebugDraw.Clear();

      QuantumGameGizmos.InvalidateGizmos();

      QuantumUnityNativeUtility.ResetStatics();
    }

    public static void ResetSimulationStatics() {

      // reset core singletons
      MemoryLayoutVerifier.Platform = null;
      Native.Utils = null;

      // invoke core reset methods
      Profiling.HostProfiler.Reset();
      Draw.Reset();

      // reset other
      Navigation.Constants.Reset();

      Quantum.Allocator.Heap.Reset();
    }

    static partial void IsEnabledUser(ref bool enabled);

    [InitializeOnLoadMethod]
    static void InitializeComponentTypeId() {
      Frame.InitStatic();
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/Utils/GUIStyleExtensions.cs

namespace Quantum.Editor {
  using System;
  using UnityEngine;

  public static class GUIStyleExtensions {

    public static IDisposable ContentOffsetScope(this GUIStyle style, Vector2 offset) {
      var result = new DisposableAction<Vector2>(x => style.contentOffset = x, style.contentOffset);
      style.contentOffset = offset;
      return result;
    }

    public static IDisposable ContentOffsetScope(this GUIStyle style, float x) => ContentOffsetScope(style, new Vector2(x, 0));

    public static IDisposable FontStyleScope(this GUIStyle style, FontStyle fontStyle) {
      if (style.fontStyle == fontStyle) {
        return null;
      }

      var result = new DisposableAction<FontStyle>(x => style.fontStyle = x, style.fontStyle);
      style.fontStyle = fontStyle;
      return result;
    }

    public static IDisposable FontStyleScope(this GUIStyle style, bool italic = false, bool bold = false) {
      FontStyle fontStyle;
      if (italic) {
        fontStyle = bold ? FontStyle.BoldAndItalic : FontStyle.Italic;
      } else {
        fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
      }

      return FontStyleScope(style, fontStyle);
    }

    public static IDisposable WordWrapScope(this GUIStyle style, bool wordWrap) {
      var result = new DisposableAction<bool>(x => style.wordWrap = x, style.wordWrap);
      style.wordWrap = wordWrap;
      return result;
    }

    public static IDisposable MarginScope(GUIStyle style, RectOffset margin) {
      var result = new DisposableAction<RectOffset>(x => style.margin = x, style.margin);
      style.margin = margin;
      return result;
    }

    public static IDisposable MarginScope(this GUIStyle style, int margin) => MarginScope(style, new RectOffset(margin, margin, margin, margin));

    private sealed class DisposableAction<T> : IDisposable {
      private T oldValue;
      private Action<T> setter;

      public DisposableAction(Action<T> setter, T oldValue) {
        this.oldValue = oldValue;
        this.setter = setter;
      }

      void IDisposable.Dispose() {
        setter(oldValue);
      }
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/Utils/QuantumEditorColliderConverter.cs

namespace Quantum.Editor {
  using System.Collections.Generic;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;

  /// <summary>
  /// This tool can convert Unity colliders to Quantum colliders.
  /// </summary>
  public static class QuantumEditorColliderConverter {
    private const int MENU_PRIORITY = (int)QuantumEditorMenuPriority.Setup + 25;

    private static List<Collider2D> _colliders2D = new List<Collider2D>();
    private static List<Collider> _colliders3D = new List<Collider>();

    private static List<QuantumStaticEdgeCollider2D> _checkQuantumStaticEdgeCollider2D = new List<QuantumStaticEdgeCollider2D>();
    private static List<QuantumStaticCircleCollider2D> _checkQuantumStaticCircleCollider2D = new List<QuantumStaticCircleCollider2D>();
    private static List<QuantumStaticCapsuleCollider2D> _checkQuantumStaticCapsuleCollider2D = new List<QuantumStaticCapsuleCollider2D>();
    private static List<QuantumStaticBoxCollider2D> _checkQuantumStaticBoxCollider2D = new List<QuantumStaticBoxCollider2D>();
    private static List<QuantumStaticPolygonCollider2D> _checkQuantumStaticPolygonCollider2D = new List<QuantumStaticPolygonCollider2D>();

    private static List<QuantumStaticSphereCollider3D> _checkQuantumStaticSphereCollider3D = new List<QuantumStaticSphereCollider3D>();
    private static List<QuantumStaticCapsuleCollider3D> _checkQuantumStaticCapsuleCollider3D = new List<QuantumStaticCapsuleCollider3D>();
    private static List<QuantumStaticBoxCollider3D> _checkQuantumStaticBoxCollider3D = new List<QuantumStaticBoxCollider3D>();
    private static List<QuantumStaticMeshCollider3D> _checkQuantumStaticMeshCollider3D = new List<QuantumStaticMeshCollider3D>();

    [MenuItem("Tools/Quantum/Setup/Convert Unity Colliders (Selected)", false, MENU_PRIORITY)]
    [MenuItem("GameObject/Quantum/Convert Colliders/Convert Unity Colliders (Selected)", false, 50)]
    private static void ConvertSelectedColliders() {
      ConvertColliders(Selection.gameObjects, false);
    }

    [MenuItem("Tools/Quantum/Setup/Convert Unity Colliders (Recursive)", false, MENU_PRIORITY)]
    [MenuItem("GameObject/Quantum/Convert Colliders/Convert Unity Colliders (Recursive)", false, 51)]
    private static void ConvertChildColliders() {
      ConvertColliders(GetGameObjectsRecursive(), false);
    }

    [MenuItem("Tools/Quantum/Setup/Convert and Remove Unity Colliders (Selected)", false, MENU_PRIORITY)]
    [MenuItem("GameObject/Quantum/Convert Colliders/Convert and Remove Unity Colliders (Selected)", false, 52)]
    private static void ConvertAndRemoveSelectedColliders() {
      ConvertColliders(Selection.gameObjects, true);
    }

    [MenuItem("Tools/Quantum/Setup/Convert and Remove Unity Colliders (Recursive)", false, MENU_PRIORITY)]
    [MenuItem("GameObject/Quantum/Convert Colliders/Convert and Remove Unity Colliders (Recursive)", false, 53)]
    private static void ConvertAndRemoveChildColliders() {
      ConvertColliders(GetGameObjectsRecursive(), true);
    }

    private static void ConvertColliders(IList<GameObject> gameObjects, bool removeSourceCollider) {
      int totalConverted = 0;
      bool markSceneDirty = false;

      foreach (GameObject gameObject in gameObjects) {
        Undo.RegisterCompleteObjectUndo(gameObject, "Convert To Quantum Colliders");

        int converted = ConvertColliders(gameObject, removeSourceCollider);
        if (converted > 0) {
          totalConverted += converted;

          if (gameObject.scene.IsValid() == true) {
            markSceneDirty = true;
          } else {
            EditorUtility.SetDirty(gameObject);
          }
        }
      }

      QuantumEditorLog.Log($"Converted {totalConverted} Unity colliders to Quantum static colliders.");

      if (markSceneDirty == true) {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
      }
    }

    private static int ConvertColliders(GameObject gameObject, bool removeSourceCollider) {
      int convertedColliders = 0;

      _colliders2D.Clear();
      gameObject.GetComponents(_colliders2D);
      foreach (Collider2D collider in _colliders2D) {
        if (ConvertCollider(gameObject, collider) == true) {
          //QuantumEditorLog.Log($"Converted {collider.GetType().Name} on {gameObject.name}.", gameObject);
          ++convertedColliders;

          if (removeSourceCollider == true) {
            Undo.DestroyObjectImmediate(collider);
          }
        }
      }

      _colliders3D.Clear();
      gameObject.GetComponents(_colliders3D);
      foreach (Collider collider in _colliders3D) {
        if (ConvertCollider(gameObject, collider) == true) {
          //QuantumEditorLog.Log($"Converted {collider.GetType().Name} on {gameObject.name}.", gameObject);
          ++convertedColliders;

          if (removeSourceCollider == true) {
            Undo.DestroyObjectImmediate(collider);
          }
        }
      }

      return convertedColliders;
    }

    private static bool ConvertCollider(GameObject gameObject, Collider2D collider) {
      bool isConverted = false;

#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      switch (collider) {
        case EdgeCollider2D edgeCollider: {
            if (HasQuantumStaticEdgeCollider2D(gameObject, edgeCollider) == false) {
              QuantumStaticEdgeCollider2D quantumEdgeCollider = Undo.AddComponent<QuantumStaticEdgeCollider2D>(gameObject);
              quantumEdgeCollider.SourceCollider = edgeCollider;
              quantumEdgeCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case CircleCollider2D circleCollider: {
            if (HasQuantumStaticCircleCollider2D(gameObject, circleCollider) == false) {
              QuantumStaticCircleCollider2D quantumCircleCollider = Undo.AddComponent<QuantumStaticCircleCollider2D>(gameObject);
              quantumCircleCollider.SourceCollider = circleCollider;
              quantumCircleCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case CapsuleCollider2D capsuleCollider: {
            if (HasQuantumStaticCapsuleCollider2D(gameObject, capsuleCollider) == false) {
              QuantumStaticCapsuleCollider2D quantumCapsuleCollider = Undo.AddComponent<QuantumStaticCapsuleCollider2D>(gameObject);
              quantumCapsuleCollider.SourceCollider = capsuleCollider;
              quantumCapsuleCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case BoxCollider2D boxCollider: {
            if (HasQuantumStaticBoxCollider2D(gameObject, boxCollider) == false) {
              QuantumStaticBoxCollider2D quantumBoxCollider = Undo.AddComponent<QuantumStaticBoxCollider2D>(gameObject);
              quantumBoxCollider.SourceCollider = boxCollider;
              quantumBoxCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case PolygonCollider2D polygonCollider: {
            if (HasQuantumStaticPolygonCollider2D(gameObject, polygonCollider) == false) {
              QuantumStaticPolygonCollider2D quantumBoxCollider = Undo.AddComponent<QuantumStaticPolygonCollider2D>(gameObject);
              quantumBoxCollider.SourceCollider = polygonCollider;
              quantumBoxCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        default: {
            QuantumEditorLog.Error($"{collider.GetType().Name} is not supported by {nameof(QuantumEditorColliderConverter)}. A collider of this type was not converted on the game object {gameObject.name}.", gameObject);
            break;
          }
      }
#endif

      return isConverted;
    }

    private static bool ConvertCollider(GameObject gameObject, Collider collider) {
      bool isConverted = false;

#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      switch (collider) {
        case SphereCollider sphereCollider: {
            if (HasQuantumStaticSphereCollider3D(gameObject, sphereCollider) == false) {
              QuantumStaticSphereCollider3D quantumSphereCollider = Undo.AddComponent<QuantumStaticSphereCollider3D>(gameObject);
              quantumSphereCollider.SourceCollider = sphereCollider;
              quantumSphereCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case CapsuleCollider capsuleCollider: {
            if (HasQuantumStaticCapsuleCollider3D(gameObject, capsuleCollider) == false) {
              QuantumStaticCapsuleCollider3D quantumCapsuleCollider = Undo.AddComponent<QuantumStaticCapsuleCollider3D>(gameObject);
              quantumCapsuleCollider.SourceCollider = capsuleCollider;
              quantumCapsuleCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case BoxCollider boxCollider: {
            if (HasQuantumStaticBoxCollider3D(gameObject, boxCollider) == false) {
              QuantumStaticBoxCollider3D quantumBoxCollider = Undo.AddComponent<QuantumStaticBoxCollider3D>(gameObject);
              quantumBoxCollider.SourceCollider = boxCollider;
              quantumBoxCollider.UpdateFromSourceCollider();
              isConverted = true;
            }
            break;
          }
        case MeshCollider meshCollider: {
            if (HasQuantumStaticMeshCollider3D(gameObject, meshCollider) == false) {
              QuantumStaticMeshCollider3D quantumMeshCollider = Undo.AddComponent<QuantumStaticMeshCollider3D>(gameObject);
              quantumMeshCollider.Mesh = meshCollider.sharedMesh;
              isConverted = true;
            }
            break;
          }
        default: {
            QuantumEditorLog.Error($"{collider.GetType().Name} is not supported by {nameof(QuantumEditorColliderConverter)}. A collider of this type was not converted on the game object {gameObject.name}.", gameObject);
            break;
          }
      }
#endif

      return isConverted;
    }

    private static List<GameObject> GetGameObjectsRecursive() {
      List<GameObject> gameObjects = new List<GameObject>();

      foreach (GameObject gameObject in Selection.gameObjects) {
        AddChildObjectsRecursive(gameObjects, gameObject);
      }

      return gameObjects;
    }

    private static void AddChildObjectsRecursive(List<GameObject> gameObjects, GameObject gameObject) {
      if (gameObjects.Contains(gameObject) == true)
        return;

      gameObjects.Add(gameObject);

      foreach (Transform childTransform in gameObject.transform) {
        AddChildObjectsRecursive(gameObjects, childTransform.gameObject);
      }
    }

    private static bool HasQuantumStaticEdgeCollider2D(GameObject gameObject, EdgeCollider2D collider) {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      _checkQuantumStaticEdgeCollider2D.Clear();
      gameObject.GetComponents(_checkQuantumStaticEdgeCollider2D);
      foreach (QuantumStaticEdgeCollider2D quantumCollider in _checkQuantumStaticEdgeCollider2D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif

      return false;
    }

    private static bool HasQuantumStaticCircleCollider2D(GameObject gameObject, CircleCollider2D collider) {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      _checkQuantumStaticCircleCollider2D.Clear();
      gameObject.GetComponents(_checkQuantumStaticCircleCollider2D);
      foreach (QuantumStaticCircleCollider2D quantumCollider in _checkQuantumStaticCircleCollider2D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif

      return false;
    }

    private static bool HasQuantumStaticCapsuleCollider2D(GameObject gameObject, CapsuleCollider2D collider) {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      _checkQuantumStaticCapsuleCollider2D.Clear();
      gameObject.GetComponents(_checkQuantumStaticCapsuleCollider2D);
      foreach (QuantumStaticCapsuleCollider2D quantumCollider in _checkQuantumStaticCapsuleCollider2D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif
      return false;
    }

    private static bool HasQuantumStaticBoxCollider2D(GameObject gameObject, BoxCollider2D collider) {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      _checkQuantumStaticBoxCollider2D.Clear();
      gameObject.GetComponents(_checkQuantumStaticBoxCollider2D);
      foreach (QuantumStaticBoxCollider2D quantumCollider in _checkQuantumStaticBoxCollider2D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif
      return false;
    }

    private static bool HasQuantumStaticPolygonCollider2D(GameObject gameObject, PolygonCollider2D collider) {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
      _checkQuantumStaticPolygonCollider2D.Clear();
      gameObject.GetComponents(_checkQuantumStaticPolygonCollider2D);
      foreach (QuantumStaticPolygonCollider2D quantumCollider in _checkQuantumStaticPolygonCollider2D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif

      return false;
    }

    private static bool HasQuantumStaticSphereCollider3D(GameObject gameObject, SphereCollider collider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      _checkQuantumStaticSphereCollider3D.Clear();
      gameObject.GetComponents(_checkQuantumStaticSphereCollider3D);
      foreach (QuantumStaticSphereCollider3D quantumCollider in _checkQuantumStaticSphereCollider3D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif

      return false;
    }

    private static bool HasQuantumStaticCapsuleCollider3D(GameObject gameObject, CapsuleCollider collider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      _checkQuantumStaticCapsuleCollider3D.Clear();
      gameObject.GetComponents(_checkQuantumStaticCapsuleCollider3D);
      foreach (QuantumStaticCapsuleCollider3D quantumCollider in _checkQuantumStaticCapsuleCollider3D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif
      
      return false;
    }

    private static bool HasQuantumStaticBoxCollider3D(GameObject gameObject, BoxCollider collider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      _checkQuantumStaticBoxCollider3D.Clear();
      gameObject.GetComponents(_checkQuantumStaticBoxCollider3D);
      foreach (QuantumStaticBoxCollider3D quantumCollider in _checkQuantumStaticBoxCollider3D) {
        if (ReferenceEquals(quantumCollider.SourceCollider, collider) == true)
          return true;
      }
#endif
      
      return false;
    }

    private static bool HasQuantumStaticMeshCollider3D(GameObject gameObject, MeshCollider collider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
      _checkQuantumStaticMeshCollider3D.Clear();
      gameObject.GetComponents(_checkQuantumStaticMeshCollider3D);
      foreach (QuantumStaticMeshCollider3D quantumCollider in _checkQuantumStaticMeshCollider3D) {
        if (ReferenceEquals(quantumCollider.Mesh, collider.sharedMesh) == true)
          return true;
      }
#endif

      return false;
    }
  }
}


#endregion


#region Assets/Photon/Quantum/Editor/Utils/ReplayMenu.cs

namespace Quantum.Editor {
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Unity menu items to export replays and save games.
  /// </summary>
  internal class ReplayMenu {
    private static string DefaultLocation => Path.GetFullPath($"{Application.dataPath}/../{QuantumEditorSettings.Global.DefaultNewAssetsLocation}/..");

    private static string ReplayLocation {
      get => EditorPrefs.GetString("Quantum_Export_LastReplayLocation");
      set => EditorPrefs.SetString("Quantum_Export_LastReplayLocation", value);
    }

    private static string SavegameLocation {
      get => EditorPrefs.GetString("Quantum_Export_LastSavegameLocation");
      set => EditorPrefs.SetString("Quantum_Export_LastSavegameLocation", value);
    }

    [MenuItem("Tools/Quantum/Export/Replay (Include Asset DB) %#r", true, (int)QuantumEditorMenuPriority.Export + 0)]
    public static bool ExportReplayAndDbCheck() {
      return Application.isPlaying && QuantumRunner.DefaultGame != null;
    }

    [MenuItem("Tools/Quantum/Export/Replay (Include Asset DB) %#r", false, (int)QuantumEditorMenuPriority.Export + 0)]
    public static void ExportReplayAndDb() {
      ExportDialogReplayAndDB(QuantumRunner.Default, includeDb: true);
    }

    [MenuItem("Tools/Quantum/Export/Replay (Exclude Asset DB)", true, (int)QuantumEditorMenuPriority.Export + 0)]
    public static bool ExportReplayAndExtraDBCheck() {
      return Application.isPlaying && QuantumRunner.DefaultGame != null;
    }

    [MenuItem("Tools/Quantum/Export/Replay (Exclude Asset DB)", false, (int)QuantumEditorMenuPriority.Export + 0)]
    public static void ExportReplayAndExtraDB() {
      ExportDialogReplayAndDB(QuantumRunner.Default, includeDb: false);
    }

    [MenuItem("Tools/Quantum/Export/Savegame (Include Asset DB)", true, (int)QuantumEditorMenuPriority.Export + 0)]
    public static bool SaveGameCheck() {
      return Application.isPlaying && QuantumRunner.DefaultGame != null;
    }

    [MenuItem("Tools/Quantum/Export/Savegame (Include Asset DB)", false, (int)QuantumEditorMenuPriority.Export + 0)]
    public static void SaveGame() {
      ExportDialogSavegame(QuantumRunner.DefaultGame);
    }

    public static void ExportDialogReplayAndDB(QuantumRunner runner, bool includeDb = false) {
      var game = runner.Game;
      var directory = ReplayLocation;
      if (string.IsNullOrEmpty(directory)) {
        directory = $"{DefaultLocation}/Replays";
      }

      Directory.CreateDirectory(directory);

      var filename = game?.Frames?.Verified?.Map?.name ?? "Replay";
      filename = $"{filename}-{DateTime.Now.ToString("yyyy'-'MM'-'dd'-'HH'-'mm'-'ss")}";
      var filePath = EditorUtility.SaveFilePanel("Export Replay File", directory, filename, "json");

      if (string.IsNullOrEmpty(filePath)) {
        return;
      }

      Directory.CreateDirectory(Path.GetDirectoryName(filePath));

      var replay = game.GetRecordedReplay(
        includeChecksums: (runner.RecordingFlags & RecordingFlags.Checksums) == RecordingFlags.Checksums, 
        includeDb: includeDb);
      if (replay == null) {
        Log.Error("No recorded replay found.");
        return;
      }

      File.WriteAllText(filePath, JsonUtility.ToJson(replay));

      if (includeDb == false) {
        // Save db as extra file
        using (var file = File.Create($"{Path.GetDirectoryName(filePath)}/{Path.GetFileNameWithoutExtension(filePath)}-DB{Path.GetExtension(filePath)}")) {
          game.AssetSerializer.SerializeAssets(file, game.ResourceManager.LoadAllAssets().ToArray());
        }
      }

      AssetDatabase.Refresh();

      ReplayLocation = Path.GetDirectoryName(filePath);
    }

    public static void ExportDialogSavegame(QuantumGame game) {
      var directory = SavegameLocation;
      if (string.IsNullOrEmpty(directory)) {
        directory = $"{DefaultLocation}/Savegames";
      }

      Directory.CreateDirectory(directory);

      var filename = game?.Frames?.Verified?.Map?.name ?? "Savegame";
      filename = $"{filename}-{DateTime.Now.ToString("yyyy'-'MM'-'dd'-'HH'-'mm'-'ss")}";
      var filePath = EditorUtility.SaveFilePanel("Export Savegame File", directory, filename, "json");
      if (string.IsNullOrEmpty(filePath)) {
        return; 
      }

      var savegame = game.CreateSavegame(includeDb: true);

      File.WriteAllText(filePath, JsonUtility.ToJson(savegame));

      AssetDatabase.Refresh();

      SavegameLocation = Path.GetDirectoryName(filePath);
    }
  }
}

#endregion


#region Assets/Photon/Quantum/Editor/Utils/SerializableEnterRoomParams.cs

namespace Quantum.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Xml;
  using System.Xml.Serialization;
  using Photon.Client;
  using Photon.Realtime;

  /// <summary>
  /// This class wraps the PhotonRealtime EnterRoomArgs class to make problematic members (Hashtable, TypedLobby restrictions) and its hierarchy XML serializable.  
  /// </summary>
  [Serializable]
  public class SerializableEnterRoomArgs : EnterRoomArgs {
    /// <summary>
    /// Is <see langword="true"/> if <see cref="EnterRoomArgs.Lobby"/> is set.
    /// </summary>
    public bool HasLobby;
    /// <summary>
    /// <see cref="EnterRoomArgs.Lobby"/> name.
    /// </summary>
    public string LobbyName;
    /// <summary>
    /// <see cref="EnterRoomArgs.Lobby"/> lobby type.
    /// </summary>
    public LobbyType LobbyType;
    
    List<DictionaryEntry> _customRoomProperties = new List<DictionaryEntry>();

    /// <summary>
    /// Serialize the class using a xml serializer.
    /// </summary>
    /// <example>
    /// <code>
    /// var enterRoomArgs = new SerializableEnterRoomArgs();
    /// using (var writer = XmlWriter.Create("EnterRoomArgs.xml", new XmlWriterSettings { Indent = true }))
    ///   SerializableEnterRoomArgs.Serialize(writer, enterRoomArgs);
    /// </code></example>
    /// <param name="writer">XmlWriter</param>
    /// <param name="obj">The object to serialize.</param>
    public static void Serialize(XmlWriter writer, SerializableEnterRoomArgs obj) {
      if (obj.RoomOptions != null && obj.RoomOptions.CustomRoomProperties != null) {
        foreach (DictionaryEntry e in obj.RoomOptions.CustomRoomProperties) {
          obj._customRoomProperties.Add(e);
        }
      }

      obj.HasLobby = obj.Lobby != null;
      obj.LobbyName = obj.Lobby?.Name;
      obj.LobbyType = obj.Lobby != null ? obj.Lobby.Type : LobbyType.Default;

      CreateSerializer().Serialize(writer, obj);
    }

    /// <summary>
    /// Read the class using a xml reader.
    /// </summary>
    /// <param name="reader">Xml reader.</param>
    /// <returns>Deserialized class.</returns>
    public static SerializableEnterRoomArgs Deserialize(XmlReader reader) {
      var obj = (SerializableEnterRoomArgs)CreateSerializer().Deserialize(reader);

      if (obj._customRoomProperties != null && obj._customRoomProperties.Count > 0) {
        if (obj.RoomOptions == null) {
          obj.RoomOptions = new RoomOptions();
        }

        if (obj.RoomOptions.CustomRoomProperties == null) {
          obj.RoomOptions.CustomRoomProperties = new PhotonHashtable();
        }

        foreach (DictionaryEntry e in obj._customRoomProperties) {
          obj.RoomOptions.CustomRoomProperties.Add(e.Key, e.Value);
        }
      }

      if (obj.HasLobby) {
        obj.Lobby = new TypedLobby(obj.LobbyName, obj.LobbyType);
      }

      return obj;
    }

    /// <summary>
    /// Create a xml serializer with the necessary overrides.
    /// </summary>
    /// <returns>Xml serializer for SerializableEnterRoomArgs.</returns>
    public static XmlSerializer CreateSerializer() {
      var overrides = new XmlAttributeOverrides();
      var attributes = new XmlAttributes() { XmlIgnore = true };
      overrides.Add(typeof(RoomOptions), "CustomRoomProperties", attributes);
      overrides.Add(typeof(EnterRoomArgs), "Lobby", attributes);
      return new XmlSerializer(typeof(SerializableEnterRoomArgs), overrides);
    }
  }
}

#endregion

#endif
