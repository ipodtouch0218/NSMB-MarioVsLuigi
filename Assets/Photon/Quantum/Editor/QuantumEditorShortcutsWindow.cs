namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A utility window to quickly access Quantum global configs.
  /// </summary>
  public class QuantumEditorShortcutsWindow : EditorWindow {
    /// <summary>
    /// Configure the button width.
    /// </summary>
    public static float ButtonWidth = 200.0f;

    /// <summary>
    /// Search and select PhotonServerSettings.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Photon Server Settings", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 0)]
    public static void SearchPhotonServerSettings() => Selection.activeObject = PhotonServerSettings.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select QuantumEditorSettings.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Editor Settings", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 11)]
    public static void SearchQuantumEditorSettings() => Selection.activeObject = QuantumEditorSettings.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select game gizmo settings.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Gizmo Settings", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 11)]
    public static void SearchQuantumGizmoSettings() => Selection.activeObject = QuantumGameGizmosSettingsScriptableObject.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select the Quantum default config asset.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Default Configs", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 22)]
    public static void SearchDefaultConfigs() => Selection.activeObject = QuantumDefaultConfigs.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select the session config.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Session Config", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 22)]
    public static void SearchSessionConfig() => Selection.activeObject = QuantumDeterministicSessionConfigAsset.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select simulation config assets.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Simulation Config", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 22)]
    public static void SearchSimulationConfig() => SearchAndSelect<Quantum.SimulationConfig>(selectMode: SelectMode.First);
    /// <summary>
    /// Search and select the Quantum Unity DB file.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Unity DB", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 22)]
    public static void SearchUnityDB() => Selection.activeObject = QuantumUnityDB.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select the Quantum .net build settings.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Dotnet Build Settings", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 33)]
    public static void SearchQuantumDotnetBuildSettings() => Selection.activeObject = QuantumDotnetBuildSettings.TryGetGlobal(out var settings) ? settings : null;
    /// <summary>
    /// Search and select the Quantum .net project settings.
    /// </summary>
    [MenuItem("Tools/Quantum/Find Config/Quantum Dotnet Project Settings", priority = (int)QuantumEditorMenuPriority.GlobalConfigs + 33)]
    public static void SearchQuantumDotnetProjectSettings() => Selection.activeObject = QuantumDotnetProjectSettings.TryGetGlobal(out var settings) ? settings : null; 
    
    /// <summary>
    /// Open the global config shortcut window.
    /// </summary>
    [MenuItem("Window/Quantum/Global Configs")]
    [MenuItem("Tools/Quantum/Window/Global Configs", priority = (int)QuantumEditorMenuPriority.Window + 3)]
    public static void ShowWindow() => GetWindow(typeof(QuantumEditorShortcutsWindow), false, "Quantum Global Configs");

    /// <summary>
    /// A grid scope for the Quantum global config window.
    /// </summary>
    public class GridScope : IDisposable {
      private bool _endHorizontal;

      /// <summary>
      /// Create a new grid scope and begin the horizontal layout.
      /// </summary>
      /// <param name="columnCount">How many columns</param>
      /// <param name="currentColumn">The current column is incremented</param>
      /// <param name="forceClose">Force closing the horizonal layout</param>
      public GridScope(int columnCount, ref int currentColumn, bool forceClose = false) {
        if (currentColumn % columnCount == 0) {
          GUILayout.BeginHorizontal();
        }

        _endHorizontal = (++currentColumn % columnCount == 0) || forceClose;
      }

      /// <summary>
      /// Dispose the grid view and end the horizontal layout if required.
      /// </summary>
      public void Dispose() {
        if (_endHorizontal) { 
          GUILayout.EndHorizontal();
        }
      }
    }

    /// <summary>
    /// OnGUI override to draw the Quantum global config window.
    /// </summary>
    public virtual void OnGUI() {
      var columnCount = (int)Mathf.Max(EditorGUIUtility.currentViewWidth / ButtonWidth, 1);
      var currentColumn = 0;

      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("NetworkView Icon", false), "Photon Server Settings", EditorStyles.miniButton) && PhotonServerSettings.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon(QuantumEditorSkin.QuantumIcon), "Session Configs", EditorStyles.miniButton) && QuantumDeterministicSessionConfigAsset.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon(QuantumEditorSkin.QuantumIcon), "Simulation Configs", EditorStyles.miniButton)) SearchAndSelect<Quantum.SimulationConfig>(selectMode: SelectMode.Steps);
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon(QuantumEditorSkin.QuantumIcon), "Systems Config", EditorStyles.miniButton)) SearchAndSelect<SystemsConfig>(selectMode: SelectMode.Steps);
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("BuildSettings.Editor.Small", false), "Editor Settings", EditorStyles.miniButton) && QuantumEditorSettings.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("BuildSettings.Editor.Small", false), "Gizmo Settings", EditorStyles.miniButton) && QuantumGameGizmosSettingsScriptableObject.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("BuildSettings.Editor.Small", false), "Unity DB", EditorStyles.miniButton)) SearchAndSelect<QuantumUnityDB>(selectMode: SelectMode.First);
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("Settings", true), "Dotnet Build Settings", EditorStyles.miniButton) && QuantumDotnetBuildSettings.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("Settings", true), "Dotnet Project Settings", EditorStyles.miniButton) && QuantumDotnetProjectSettings.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon(QuantumEditorSkin.QuantumIcon), "Default Configs", EditorStyles.miniButton) && QuantumDefaultConfigs.TryGetGlobal(out var settings)) Selection.activeObject = settings;
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("Profiler.Physics", true), "Physics Materials", EditorStyles.miniButton)) SearchAndSelect<Quantum.PhysicsMaterial>(selectMode: SelectMode.Steps);
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("NavMeshData Icon", true), "NavMesh Agent Configs", EditorStyles.miniButton)) SearchAndSelect<Quantum.NavMeshAgentConfig>(selectMode: SelectMode.Steps);
      }
      using (new GridScope(columnCount, ref currentColumn)) {
        if (GUI.Button(DrawIcon("CapsuleCollider2D Icon", true), "Character Controller 2D", EditorStyles.miniButton)) SearchAndSelect<Quantum.CharacterController2DConfig>(selectMode: SelectMode.Steps);
      }
      using (new GridScope(columnCount, ref currentColumn, true)) {
        if (GUI.Button(DrawIcon("CapsuleCollider Icon", true), "Character Controller 3D", EditorStyles.miniButton)) SearchAndSelect<Quantum.CharacterController3DConfig>(selectMode: SelectMode.Steps);
      }
    }

    /// <summary>
    /// Draw an icon.
    /// </summary>
    /// <param name="iconName">Icon name that is found by EditorGUIUtility.IconContent()</param>
    /// <returns>Control rect</returns>
    public static Rect DrawIcon(string iconName, bool hasDarkIcon) {
      if (hasDarkIcon && EditorGUIUtility.isProSkin) {
        iconName = $"d_{iconName}";
      }
      return DrawIcon(EditorGUIUtility.IconContent(iconName));
    }

    private static Rect DrawIcon(Texture2D texture) {
      return DrawIcon(new GUIContent(string.Empty, texture));
    }

    private static Rect DrawIcon(GUIContent content) {
      var rect = EditorGUILayout.GetControlRect();
      var width = rect.width;
      rect.width = 20;
      EditorGUI.LabelField(rect, content);
      rect.xMin += rect.width;
      rect.width = width - rect.width;
      return rect;
    }

    /// <summary>
    /// Different selection modes for SearchAndSelect methods.
    /// </summary>
    public enum SelectMode {
      /// <summary>
      /// Select all
      /// </summary>
      All,
      /// <summary>
      /// Select the first asset found
      /// </summary>
      First,
      /// <summary>
      /// Select the next asset using <see cref="Selection.activeObject"/>
      /// </summary>
      Steps
    }

    /// <summary>
    /// Search and select any type.
    /// </summary>
    /// <typeparam name="T">Type to search</typeparam>
    /// <param name="selectMode">Toggle the selection mode</param>
    /// <returns>The first asset of the type found</returns>
    public static IEnumerable<T> SearchAndSelect<T>(SelectMode selectMode = SelectMode.All) where T : UnityEngine.Object {
      var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, null);
      if (guids.Length == 0) {
        QuantumEditorLog.Log($"No UnityEngine.Objects of type '{typeof(T).Name}' found.");
        return Enumerable.Empty<T>();
      }

      var objects = guids.Select(g => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), typeof(T))).ToArray();

      switch (selectMode) {
        case SelectMode.All: 
          break;
        case SelectMode.First:
          objects = new UnityEngine.Object[] { objects[0] };
          break;
        case SelectMode.Steps:
          var index = (ArrayUtility.FindIndex(objects, o => Selection.activeObject == o) + 1) % objects.Length;
          objects = new UnityEngine.Object[] { objects[index] };
          break;
      }

      Selection.objects = objects.ToArray();
      return objects.Select(t => t as T);

    }

    /// <summary>
    /// Search and select any type by name.
    /// </summary>
    /// <typeparam name="T">Asset type</typeparam>
    /// <param name="name">Asset name</param>
    /// <returns>The asset matching the type and name</returns>
    public static IEnumerable<T> SearchAndSelect<T>(string name) where T : UnityEngine.Object {
      foreach (var asset in SearchAndSelect<T>(selectMode: SelectMode.All)) {
        if (String.Equals(asset.name, name, StringComparison.Ordinal)) {
          return new T[] { asset };
        }
      }
      return Enumerable.Empty<T>();
    }

    [Obsolete("Use QuantumUnityDB.Global.GetAsset() instead")]
    public static IEnumerable<T> SearchAndSelect<T>(AssetGuid assetGuid) where T : UnityEngine.Object => null;
  }
}
