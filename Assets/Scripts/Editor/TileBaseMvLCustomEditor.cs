using NSMB.Tiles;
using Quantum.Editor;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

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
}

[CustomEditor(typeof(Tile))]
public class TileEditorOverride : TileBaseMvLCustomEditor<Tile, TileEditor> { }
[CustomEditor(typeof(RuleTile))]
public class RuleTileEditorOverride : TileBaseMvLCustomEditor<RuleTile, RuleTileEditor> { }

[CustomEditor(typeof(AnimatedTile))]
public class AnimatedTileEditorOverride : TileBaseMvLCustomEditor<AnimatedTile, AnimatedTileEditor> { }

[CustomEditor(typeof(SiblingRuleTile))]
public class SublingRuleTileEditorOverride : TileBaseMvLCustomEditor<SiblingRuleTile, RuleTileEditor> { }