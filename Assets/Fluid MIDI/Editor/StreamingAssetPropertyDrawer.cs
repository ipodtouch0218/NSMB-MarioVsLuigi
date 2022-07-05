using UnityEditor;
using UnityEngine;

namespace FluidMidi
{
    [CustomPropertyDrawer(typeof(StreamingAsset))]
    public class StreamingAssetPropertyDrawer : PropertyDrawer
    {
        const string FOLDER_STREAMING_ASSETS = "Assets/StreamingAssets/";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty pathProperty = property.FindPropertyRelative("path");
            string path = pathProperty.stringValue;
            Object assetObject = null;
            if (path.Length > 0)
            {
                string combinedPath = FOLDER_STREAMING_ASSETS + path;
                assetObject = AssetDatabase.LoadAssetAtPath(combinedPath, typeof(DefaultAsset));
            }
            EditorGUI.BeginChangeCheck();
            assetObject = EditorGUI.ObjectField(position, label, assetObject, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                path = AssetDatabase.GetAssetPath(assetObject);
                if (path.StartsWith(FOLDER_STREAMING_ASSETS))
                {
                    pathProperty.stringValue = path.Substring(FOLDER_STREAMING_ASSETS.Length);
                }
                else
                {
                    if (path.Length > 0)
                    {
                        Debug.LogError("Not a streaming asset: " + path);
                    }
                    pathProperty.stringValue = string.Empty;
                }
            }
            EditorGUI.EndProperty();
        }
    }
}