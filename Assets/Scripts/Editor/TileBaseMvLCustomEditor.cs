using NSMB.Tiles;
using Quantum.Editor;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageTileMenuItems {
    [MenuItem("MvLO/Convert standalone StageTiles to sub-assets")]
    public static void ConvertStageTiles() {
        var allStageTiles = AssetDatabase.FindAssets($"t:{typeof(StageTile).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<StageTile>);

        int converted = 0;
        foreach (var stageTile in allStageTiles) {
            if (!stageTile.Tile
                || (AssetDatabase.IsSubAsset(stageTile) && AssetDatabase.GetAssetPath(stageTile) == AssetDatabase.GetAssetPath(stageTile.Tile))) {
                continue;
            }

            string oldPath = AssetDatabase.GetAssetPath(stageTile);

            AssetDatabase.RemoveObjectFromAsset(stageTile);
            AssetDatabase.AddObjectToAsset(stageTile, stageTile.Tile);

            if (File.Exists(oldPath)) {
                AssetDatabase.DeleteAsset(oldPath);
            }

            converted++;
        }

        Debug.Log($"Converted {converted} standalone StageTile(s) to sub-asset(s).");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

[CustomEditor(typeof(TileBase), editorForChildClasses: true)]
public abstract class TileBaseMvLCustomEditor<TTarget, TInner> : Editor where TTarget : TileBase where TInner : Editor {

    private Editor innerEditor;
    private Editor stageTileEditor;
    private StageTile stageTile;

    private int selectedTypeIndex;
    private string[] stageTileTypeNames;
    private Type[] stageTileTypes;

    public void OnEnable() {
        stageTileTypes = TypeCache.GetTypesDerivedFrom<StageTile>()
            .Where(t => !t.IsAbstract)
            .Prepend(typeof(StageTile))
            .ToArray();

        stageTileTypeNames = stageTileTypes.Select(t => t.Name).ToArray();

        EnsureInnerEditor();
    }

    public override void OnInspectorGUI() {
        TileBase tbTarget = (TileBase) target;

        if (!stageTile || stageTile.Tile != tbTarget) {
            // Find the StageTile for this TileBase
            stageTile = AssetDatabase.FindAssets($"t:{typeof(StageTile).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StageTile>)
                .Where(st => st.Tile == tbTarget)
                .FirstOrDefault();
        }

        if (stageTile) {
            // We have a StageTile - draw the dropdown & nested thingy
            if (!stageTileEditor || stageTileEditor.target != stageTile) {
                stageTileEditor = CreateEditor(stageTile);
            }

            var sp = stageTileEditor.serializedObject.GetIterator();
            sp.isExpanded = EditorGUILayout.Foldout(sp.isExpanded, "Stage Tile Properties", true);

            if (sp.isExpanded) {
                using (new EditorGUI.IndentLevelScope()) {
                    stageTileEditor.OnInspectorGUI();
                    EditorGUILayout.Space();
                    using (new QuantumEditorGUI.BackgroundColorScope(Color.red)) {
                        if (GUILayout.Button("Delete StageTile")) {
                            AssetDatabase.RemoveObjectFromAsset(stageTile);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            stageTile = null;
                        }
                    }
                }
            }
        } else {
            // We don't have a StageTile, draw a button to create one.
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            selectedTypeIndex = EditorGUILayout.Popup("Create StageTile", selectedTypeIndex, stageTileTypeNames);

            using (new QuantumEditorGUI.BackgroundColorScope(Color.green)) {
                if (GUILayout.Button("Create")) {
                    var type = stageTileTypes[selectedTypeIndex];
                    var newStageTile = ScriptableObject.CreateInstance(type);
                    newStageTile.name = tbTarget.name + "StageTile";
                    ((StageTile) newStageTile).Tile = tbTarget;

                    var path = AssetDatabase.GetAssetPath(tbTarget);
                    AssetDatabase.AddObjectToAsset(newStageTile, path);
                    //AssetDatabase.CreateAsset(newStageTile, Path.GetDirectoryName(path) + $"/{newStageTile.name}.asset");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // Draw line
        int padding = 20;
        int height = 1;

        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + height));
        r.height = height;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, Color.gray);

        // Draw normal editor
        EnsureInnerEditor();
        innerEditor.OnInspectorGUI();
    }

    private void EnsureInnerEditor() {
        if (innerEditor == null) {
            innerEditor = CreateEditor(targets, typeof(TInner));
        }
    }

    private void OnDisable() {
        if (innerEditor != null) {
            DestroyImmediate(innerEditor);
            innerEditor = null;
        }
    }

    // --- Forward PREVIEW handling so you keep thumbnails & previews ---
    public override bool HasPreviewGUI() => innerEditor != null && innerEditor.HasPreviewGUI();
    
    public override void OnPreviewGUI(Rect r, GUIStyle background) {
        if (innerEditor != null) innerEditor.OnPreviewGUI(r, background);
    }

    public override void OnPreviewSettings() {
        if (innerEditor != null) innerEditor.OnPreviewSettings();
    }

    public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height) {
        EnsureInnerEditor();
        return innerEditor != null
            ? innerEditor.RenderStaticPreview(assetPath, subAssets, width, height)
            : null;
    }
}

public class TileRenameProcessor : AssetModificationProcessor {

    public static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath) {
        var tile = AssetDatabase.LoadMainAssetAtPath(sourcePath) as TileBase;
        if (tile == null) {
            return AssetMoveResult.DidNotMove;
        }

        var newName = Path.GetFileNameWithoutExtension(destinationPath);

        var subs = AssetDatabase.LoadAllAssetsAtPath(sourcePath);
        foreach (var sub in subs) {
            if (sub is StageTile st && st.Tile == tile) {
                var desired = newName + "StageTile";
                if (st.name != desired) {
                    st.name = desired;
                    EditorUtility.SetDirty(st);
                }
            }
        }

        return AssetMoveResult.DidNotMove;
    }
}

[CustomEditor(typeof(Tile))]
public class TileEditorOverride : TileBaseMvLCustomEditor<Tile, TileEditor> { }
[CustomEditor(typeof(RuleTile))]
public class RuleTileEditorOverride : TileBaseMvLCustomEditor<RuleTile, RuleTileEditor> { }

[CustomEditor(typeof(AnimatedTile))]
public class AnimatedTileEditorOverride : TileBaseMvLCustomEditor<AnimatedTile, AnimatedTileEditor> { }

[CustomEditor(typeof(SiblingRuleTile))]
public class SublingRuleTileEditorOverride : TileBaseMvLCustomEditor<SiblingRuleTile, RuleTileEditor> { }