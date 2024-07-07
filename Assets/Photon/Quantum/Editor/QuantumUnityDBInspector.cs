namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using JetBrains.Annotations;
  using Photon.Client.StructWrapping;
  using UnityEditor;
  using UnityEditor.IMGUI.Controls;
  using UnityEngine;
  using Object = UnityEngine.Object;
  using static UnityEngine.Object;
  using static QuantumUnityExtensions;

  /// <summary>
  /// An editor class that renders a windows displaying assets from the Quantum DB.
  /// Open the windows by using Tools > Quantum > Window > Quantum Unity DB
  /// </summary>
  public class QuantumUnityDBInspector : EditorWindow {
    private Grid _grid = new Grid();
    
    private void OnEnable() {
      _grid.OnEnable();
    }

    private void OnInspectorUpdate() {
      _grid.OnInspectorUpdate();
    }
    
    private void OnGUI() {

      using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
        _grid.DrawToolbarReloadButton();
        
        if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) {
          QuantumUnityDBUtilities.ExportAsJson();
        }
      
        _grid.DrawToolbarSyncSelectionButton();
        
        GUILayout.FlexibleSpace();
        
        _grid.DrawToolbarSearchField();
      }
      
      var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
      _grid.OnGUI(rect);
    }

    [MenuItem("Window/Quantum/Quantum Unity DB")]
    [MenuItem("Tools/Quantum/Window/Quantum Unity DB", false, (int)QuantumEditorMenuPriority.Window + 1)]
    public static void ShowWindow() {
      var window = GetWindow<QuantumUnityDBInspector>(false, ObjectNames.NicifyVariableName(nameof(QuantumUnityDB)));
      window.Show();
    }
    
    class GridItem : QuantumGridItem {

      private int _entryIndex;
      
      public GridItem(int entryIndex) {
        _entryIndex = entryIndex;
      }

      [CanBeNull]
      private QuantumUnityDB.Entry Entry {
        get {
          if (_entryIndex < 0) {
            return null;
          }
          
          if (_entryIndex >= QuantumUnityDB.Global.Entries.Count) {
            return null;
          }

          return QuantumUnityDB.Global.Entries[_entryIndex];
        }
      }

      public AssetGuid QuantumGuid => Entry?.Guid ?? default;
      public override Object TargetObject => Entry?.Source.EditorInstance;
      public Type QuantumAssetType => Entry?.Source.AssetType;
      
      public string UnityPath => AssetDatabase.GUIDToAssetPath(UnityGuid);
      
      public string UnityGuid {
        get {
          var instance = TargetObject;
          if (EditorUtility.IsPersistent(instance) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instance, out var guid, out long _)) {
            return guid;
          }
          return string.Empty;
        }
      }

      public string Name => TargetObject?.name ?? "";
      public string QuantumPath => Entry?.Path ?? "";
      public string AssetType => Entry?.Source.AssetType?.Name ?? "";
      public AssetObjectState LoadState => Entry != null ? QuantumUnityDB.GetGlobalAssetState(QuantumGuid) : AssetObjectState.NotFound;
      public bool IsQuantumGuidOverriden => IsPersistent && QuantumUnityDBUtilities.IsAssetGuidOverriden(TargetObject);
      public string SourceDescription => Entry?.Source.Description ?? "";
      public bool IsPersistent => Entry != null && EditorUtility.IsPersistent(TargetObject);
    }

    [Serializable]
    class Grid : QuantumGrid<GridItem> {
      
      public override int GetContentHash() {
        return (int)QuantumUnityDB.Global.Version;
      }

      protected override IEnumerable<Column> CreateColumns() {
        yield return MakeSimpleColumn(x => x.QuantumGuid, new() {
          headerContent = new GUIContent("GUID"),
          width         = 150,
          maxWidth      = 150,
          cellGUI = (item, rect, selected, focused) => {
            TreeView.DefaultGUI.Label(rect, item.QuantumGuid + (item.IsQuantumGuidOverriden ? "*" : ""), selected, focused);
          },
          getComparer = (order) => (a, b) => a.QuantumGuid.CompareTo(b.QuantumGuid) * order,
        });
        yield return MakeSimpleColumn(x => x.LoadState, new() {
          headerContent   = new GUIContent("", "State"),
          maxWidth        = 40,
          width           = 40,
          contextMenuText = "State",
          cellGUI = (item, rect, _, _) => {
            var   icon = QuantumEditorSkin.LoadStateIcon;
            Color color;
            switch (item.LoadState) {
              case AssetObjectState.Loaded:
                color = Color.green;
                break;
              case AssetObjectState.Loading:
                color = Color.yellow;
                break;
              case AssetObjectState.Disposing:
                color = Color.magenta;
                break;
              case AssetObjectState.NotLoaded:
                color = Color.gray;
                break;
              case AssetObjectState.Error:
                color = Color.red;
                break;
              default:
                icon  = QuantumEditorSkin.ErrorIcon;
                color = Color.white;
                break;
            }
        
            using (new QuantumEditorGUI.ContentColorScope(color)) {
              EditorGUI.LabelField(rect, new GUIContent(icon, item.LoadState.ToString()));
            }
          },
        });
        yield return MakeSimpleColumn(x => x.QuantumPath, new() {
          headerContent   = new GUIContent("Path"),
          width           = 600,
          sortedAscending = true
        });
        yield return MakeSimpleColumn(x => x.Name, new() {
          headerContent    = new GUIContent("Name"),
          initiallyVisible = false,
        });
        yield return MakeSimpleColumn(x => x.IsQuantumGuidOverriden, new() {
          headerContent    = new GUIContent("GUID Override"),
          initiallyVisible = false,
          cellGUI = (item, rect, _, _) => EditorGUI.Toggle(rect, item.IsQuantumGuidOverriden),
          getSearchText = item => null,
        });
        yield return MakeSimpleColumn(x => x.AssetType, new() {
          headerContent = new GUIContent("Asset Type"),
          autoResize    = false,
          width         = 200,
          cellGUI = (item, rect, selected, focused) => {
            var assetType = item.QuantumAssetType;
            if (assetType == null) {
              TreeView.DefaultGUI.Label(rect, "<Unknown>", selected, focused);
            } else {
              using (new QuantumEditorGUI.ContentColorScope(QuantumEditorUtility.GetPersistentColor(assetType.FullName, 192))) {
                TreeView.DefaultGUI.Label(rect, assetType.Name, selected, focused);
              }
            }
          }
        });
        yield return MakeSimpleColumn(x => x.UnityPath, new() {
          headerContent    = new GUIContent("Unity Path"),
          initiallyVisible = false,
        });
        yield return MakeSimpleColumn(x => x.UnityGuid, new() {
          headerContent    = new GUIContent("Unity GUID"),
          initiallyVisible = false,
        });
        yield return MakeSimpleColumn(x => x.SourceDescription, new() {
          headerContent    = new GUIContent("Source"),
          initiallyVisible = false,
        });
      }

      protected override IEnumerable<GridItem> CreateRows() {
        for (int i = 0; i < QuantumUnityDB.Global.Entries.Count; i++) {
          if (QuantumUnityDB.Global.Entries[i] == null) {
            continue;
          }
          yield return new GridItem(i) { id = i+1 };
        }
      }

      protected override GenericMenu CreateContextMenu(GridItem item, TreeView treeView) {

        var allRows      = treeView.GetRows().OfType<GridItem>().ToDictionary(x => x.id);
        var selectedRows = treeView.GetSelection().Select(x => allRows[x]).ToList();

        var anyOverriden = selectedRows.Any(x => x.IsQuantumGuidOverriden);

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Copy GUID (as Text)"), false, g => GUIUtility.systemCopyBuffer = (string)g, item.QuantumGuid.ToString());
        menu.AddItem(new GUIContent("Copy GUID (as Long)"), false, g => GUIUtility.systemCopyBuffer = (string)g, item.QuantumGuid.Value.ToString());
        menu.AddItem(new GUIContent("Copy Path"), false, g => GUIUtility.systemCopyBuffer           = (string)g, item.QuantumPath);
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("GUID Override"), anyOverriden, () => {
          var assets = selectedRows.Where(x => x.IsPersistent).Select(x => (AssetObject)x.TargetObject).ToArray();
          QuantumUnityDBUtilities.SetAssetGuidDeterministic(assets, anyOverriden, warnIfChange: true);
          AssetDatabase.SaveAssets();
        });
        menu.AddSeparator("");

        menu.AddDisabledItem(new GUIContent("Default Resource Manager"));

        IResourceManager resourceManager = QuantumUnityDB.Global;

        var loadedGuids = new HashSet<AssetGuid>(selectedRows.Select(x => x.QuantumGuid).Where(x => resourceManager.GetAssetState(x) == AssetObjectState.Loaded).ToArray());

        int loadedAssetCount = selectedRows.Count(x => loadedGuids.Contains(x.QuantumGuid));
        if (selectedRows.Count != loadedAssetCount) {
          menu.AddItem(new GUIContent("Load"), false, () => {
            foreach (var row in selectedRows) {
              try {
                resourceManager.GetAsset(row.QuantumGuid);
              } catch (Exception) { }
            }

            resourceManager.Update();
          });
        } else {
          menu.AddDisabledItem(new GUIContent("Load"));
        }

        if (loadedAssetCount != 0) {
          menu.AddItem(new GUIContent("Unload"), false, () => {
            foreach (var row in selectedRows) {
              try {
                resourceManager.DisposeAsset(row.QuantumGuid);
              } catch (Exception) { }
            }

            resourceManager.Update();
          });
        } else {
          menu.AddDisabledItem(new GUIContent("Unload"));
        }

        return menu;
      }
    }
  }
}
