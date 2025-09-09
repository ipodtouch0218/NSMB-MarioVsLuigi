using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/*
[CustomEditor(typeof(StageTile), editorForChildClasses: true)]
public class StageTileCustomEditor : Editor {

    private Editor tileAssetEditor;

    public override void OnInspectorGUI() {
        StageTile stTarget = (StageTile) target;
        if (stTarget) {
            TileBase tile = stTarget.Tile;
            if (tile) {
                if (!tileAssetEditor || tileAssetEditor.target != tile) {
                    tileAssetEditor = CreateEditor(tile);
                }

                int padding = 20;
                int height = 1;

                Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + height));
                r.height = height;
                r.y += padding / 2;
                r.x -= 2;
                r.width += 6;
                EditorGUI.DrawRect(r, Color.gray);

                var sp = tileAssetEditor.serializedObject.GetIterator();
                sp.isExpanded = EditorGUILayout.Foldout(sp.isExpanded, "Tile Visual Properties", true);

                if (sp.isExpanded) {
                    using (new EditorGUI.IndentLevelScope()) {
                        tileAssetEditor.OnInspectorGUI();
                    }
                }
            }
        }

        base.OnInspectorGUI();
    }
}
*/