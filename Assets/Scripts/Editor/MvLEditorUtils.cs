using Quantum;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MvLEditorUtils : EditorWindow {

    private string mapName;

    [MenuItem("MvLO/Create New Map")]
    public static void OpenCreateMapWindow() {
        GetWindow<MvLEditorUtils>();
    }

    [MenuItem("MvLO/Find VersusStageData Asset")]
    public static void FindStageData() {
        QuantumMapData qmd = GameObject.FindFirstObjectByType<QuantumMapData>();
        if (!qmd) {
            Debug.LogWarning("Not within a stage!");
            return;
        }

        EditorGUIUtility.PingObject(QuantumUnityDB.GetGlobalAsset(qmd.Asset.UserAsset));
    }

    public void OnGUI() {
        minSize = new Vector2(350, 150);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Map Name: ");
        mapName = EditorGUILayout.TextField(mapName);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Create Map")) {
            if (CreateNewMap()) {
                Close();
            }
        }
        //EditorGUILayout.DropdownButton
        EditorGUILayout.EndVertical();
    }

    public bool CreateNewMap() {
        string[] sourceAssets = new string[] {
            "Assets/Scenes/Template/LevelTemplate.unity",
            "Assets/Scenes/Template/TemplateMap.asset",
            "Assets/Scenes/Template/TemplateStageData.asset",
        };
        string[] destinationAssets = new string[] {
            $"Assets/Scenes/Levels/{mapName}.unity",
            $"Assets/QuantumUser/Resources/AssetObjects/Maps/{mapName}/{mapName}Map.asset",
            $"Assets/QuantumUser/Resources/AssetObjects/Maps/{mapName}/{mapName}StageData.asset",
        };

        if (destinationAssets.Any(AssetDatabase.AssetPathExists)) {
            Debug.LogError("A stage called {name} already exists.");
            return false;
        }

        // mkdirs
        for (int i = 0; i < destinationAssets.Length; i++) {
            string directoryPath = Path.GetDirectoryName(destinationAssets[i]);
            if (!Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
                AssetDatabase.Refresh();
            }
        }

        if (!AssetDatabase.CopyAssets(sourceAssets, destinationAssets)) {
            Debug.LogError("Failed to duplicate template assets.");
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(destinationAssets[0]);
        AssetDatabase.ImportAsset(destinationAssets[0], ImportAssetOptions.ForceUpdate);

        VersusStageData stage = AssetDatabase.LoadAssetAtPath<VersusStageData>(destinationAssets[2]);
        stage.TranslationKey = $"levels.custom.{mapName}";
        stage.Guid = default;
        EditorUtility.SetDirty(stage);
        AssetDatabase.ImportAsset(destinationAssets[2], ImportAssetOptions.ForceUpdate);

        Map map = AssetDatabase.LoadAssetAtPath<Map>(destinationAssets[1]);
        map.Scene = mapName;
        map.ScenePath = destinationAssets[0];
        map.SceneGuid = default;
        map.Guid = default;
        map.UserAsset = stage;
        map.StaticColliders3DTrianglesData = default;
        EditorUtility.SetDirty(map);
        AssetDatabase.ImportAsset(destinationAssets[1], ImportAssetOptions.ForceUpdate);

        SimulationConfig simulationConfig = QuantumDefaultConfigs.Global.SimulationConfig;
        Array.Resize(ref simulationConfig.AllStages, simulationConfig.AllStages.Length + 1);
        simulationConfig.AllStages[^1] = map;
        EditorUtility.SetDirty(simulationConfig);

        QuantumMapData mapHolder = FindFirstObjectByType<QuantumMapData>();
        mapHolder.Asset = map;
        EditorUtility.SetDirty(mapHolder);
        EditorUtility.SetDirty(mapHolder.gameObject);

        var buildScenes = EditorBuildSettings.scenes;
        Array.Resize(ref buildScenes, buildScenes.Length + 1);
        buildScenes[^1] = new EditorBuildSettingsScene {
            path = destinationAssets[0],
            guid = AssetDatabase.GUIDFromAssetPath(destinationAssets[0]),
            enabled = true,
        };
        EditorBuildSettings.scenes = buildScenes;

        EditorSceneManager.SaveScene(scene);
        SceneView.lastActiveSceneView.camera.transform.position = new Vector3(0, 0, -10);

        Debug.Log($"Created new map {mapName} from template.");
        return true;
    }
}