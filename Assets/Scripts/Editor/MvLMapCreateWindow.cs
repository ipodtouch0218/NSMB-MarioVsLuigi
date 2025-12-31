using Quantum;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class MvLMapCreateWindow : EditorWindow {

    private string mapName;

    [MenuItem("Tools/MvLO/Create New Map")]
    public static void OpenCreateMapWindow() {
        GetWindow<MvLMapCreateWindow>();
    }

    [MenuItem("Tools/MvLO/Find VersusStageData Asset")]
    public static void FindStageData() {
        QuantumMapData qmd = GameObject.FindFirstObjectByType<QuantumMapData>();
        if (!qmd) {
            Debug.LogWarning("Not within an MvLO stage scene.");
            return;
        }

        EditorGUIUtility.PingObject(QuantumUnityDB.GetGlobalAsset(qmd.GetAsset(true).UserAsset));
    }

    [MenuItem("Tools/MvLO/Compress Selected Tilemap")]
    public static void CompressTilemap() {
        if (Selection.activeGameObject
            && Selection.activeGameObject.TryGetComponent(out Tilemap tilemap)) {

            tilemap.CompressBounds();
            EditorUtility.SetDirty(tilemap);
        } else {
            Debug.LogWarning("Not selecting a tilemap.");
        }
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
        string newScenePath = $"Assets/Scenes/Levels/{mapName}.unity";
        if (AssetDatabase.AssetPathExists(newScenePath)) {
            Debug.LogError("A stage called {name} already exists.");
            return false;
        }

        Directory.CreateDirectory($"Assets/QuantumUser/Resources/AssetObjects/Maps/{mapName}");

        if (!AssetDatabase.CopyAsset("Assets/Scenes/LevelTemplate.unity", newScenePath)) {
            Debug.LogError("Failed to duplicate template assets.");
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(newScenePath);

        VersusStageData stage = ScriptableObject.CreateInstance<VersusStageData>();
        stage.name = mapName + "Stage";
        stage.TranslationKey = $"levels.custom.{mapName}";
        AssetDatabase.CreateAsset(stage, $"Assets/QuantumUser/Resources/AssetObjects/Maps/{mapName}/{mapName}StageData.asset");

        Map map = ScriptableObject.CreateInstance<Map>();
        map.Scene = mapName;
        map.ScenePath = newScenePath;
        map.SceneGuid = default;
        map.Guid = default;
        map.UserAsset = stage;
        map.StaticColliders3DTrianglesData = default;
        EditorUtility.SetDirty(map);
        AssetDatabase.CreateAsset(map, $"Assets/QuantumUser/Resources/AssetObjects/Maps/{mapName}/{mapName}Map.asset");

        /*
        SimulationConfig simulationConfig = QuantumDefaultConfigs.Global.SimulationConfig;
        Array.Resize(ref simulationConfig.AllStages, simulationConfig.AllStages.Length + 1);
        simulationConfig.AllStages[^1] = map;
        EditorUtility.SetDirty(simulationConfig);
        */

        QuantumMapData mapHolder = FindFirstObjectByType<QuantumMapData>();
        mapHolder.AssetRef = map;
        EditorUtility.SetDirty(mapHolder);
        EditorUtility.SetDirty(mapHolder.gameObject);

        var buildScenes = EditorBuildSettings.scenes;
        Array.Resize(ref buildScenes, buildScenes.Length + 1);
        buildScenes[^1] = new EditorBuildSettingsScene {
            path = newScenePath,
            guid = AssetDatabase.GUIDFromAssetPath(newScenePath),
            enabled = true,
        };
        EditorBuildSettings.scenes = buildScenes;

        EditorSceneManager.SaveScene(scene);
        SceneView.lastActiveSceneView.camera.transform.position = new Vector3(0, 0, -10);

        Debug.Log($"Created new map {mapName} from template.");
        return true;
    }
}