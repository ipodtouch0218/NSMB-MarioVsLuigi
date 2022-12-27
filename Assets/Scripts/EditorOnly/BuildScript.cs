#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class BuildScript : EditorWindow
{
    private const string path = "Builds";

    // probs could be done better
    bool windows64Build = true;
    bool windows64Debug = false;
    bool windows64IL = false;
    bool windows32Build = true;
    bool windows32Debug = false;
    bool windows32IL = false;
    bool LinuxBuild = true;
    bool LinuxDebug = false;
    bool LinuxIL = false;
    bool macBuild = true;
    bool macDebug = false;
    bool macIL = false;
    bool WebBuild = true;
    bool WebDebug = false;

    public Vector2 mainScroll;

    [MenuItem("Build/Mog Build Menu")]
    public static void ShowWindow()
    {
        GetWindow<BuildScript>();
    }

    /// <summary>
    /// this function is for builds without a graphical unity editor
    /// </summary>
    public static void BuildAll()
    {
        var build = new BuildScript();
        build.BuildWindows64();
        build.BuildWindows32();
        build.BuildLinux();
        build.BuildMac();
        build.BuildWebGL();
    }

    void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Windows 64-bit Build: ");
        windows64Build = EditorGUILayout.Toggle(windows64Build);
        EditorGUILayout.EndHorizontal();
        if (windows64Build) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Windows 64-bit Debug: ");
            windows64Debug = EditorGUILayout.Toggle(windows64Debug);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Windows 64-bit IL2CPP: ");
            windows64IL = EditorGUILayout.Toggle(windows64IL);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Windows 32-bit Build: ");
        windows32Build = EditorGUILayout.Toggle(windows32Build);
        EditorGUILayout.EndHorizontal();
        if (windows32Build)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Windows 32-bit Debug: ");
            windows32Debug = EditorGUILayout.Toggle(windows32Debug);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Windows 32-bit IL2CPP: ");
            windows32IL = EditorGUILayout.Toggle(windows32IL);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Linux Build: ");
        LinuxBuild = EditorGUILayout.Toggle(LinuxBuild);
        EditorGUILayout.EndHorizontal();
        if (LinuxBuild)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Linux Debug: ");
            LinuxDebug = EditorGUILayout.Toggle(LinuxDebug);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Linux IL2CPP: ");
            LinuxIL = EditorGUILayout.Toggle(LinuxIL);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("MacOS Build: ");
        macBuild = EditorGUILayout.Toggle(macBuild);
        EditorGUILayout.EndHorizontal();
        if (macBuild)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use MacOS Debug: ");
            macDebug = EditorGUILayout.Toggle(macDebug);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use MacOS IL2CPP: ");
            macIL = EditorGUILayout.Toggle(macIL);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("WebGL Build: ");
        WebBuild = EditorGUILayout.Toggle(WebBuild);
        EditorGUILayout.EndHorizontal();
        if (WebBuild)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use WebGL Debug: ");
            WebDebug = EditorGUILayout.Toggle(WebDebug);
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Build Game"))
        {
            if (windows64Build)
                BuildWindows64();
            if (windows32Build)
                BuildWindows32();
            if (LinuxBuild)
                BuildLinux();
            if (macBuild)
                BuildMac();
            if (WebBuild)
                BuildWebGL();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    void BuildWindows64()
    {
        BuildOptions options = windows64Debug ? BuildOptions.AllowDebugging : BuildOptions.None;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        if (windows64IL)
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            if (windows64Debug)
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Debug);
            else
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);
        }
        else
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        }

        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).ToArray(), Path.Combine(path, "Windows64", "NSMB-MarioVsLuigi.exe"), BuildTarget.StandaloneWindows64, options);
    }

    void BuildWindows32()
    {
        BuildOptions options = windows32Debug ? BuildOptions.AllowDebugging : BuildOptions.None;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows);
        if (windows32IL)
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            if (windows32Debug)
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Debug);
            else
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);
        }
        else
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        }

        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).ToArray(), Path.Combine(path, "Windows32", "NSMB-MarioVsLuigi.exe"), BuildTarget.StandaloneWindows, options);
    }

    void BuildLinux()
    {
        BuildOptions options = LinuxDebug ? BuildOptions.AllowDebugging : BuildOptions.None;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        if (LinuxIL)
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            if (LinuxDebug)
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Debug);
            else
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);
        }
        else
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        }

        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).ToArray(), Path.Combine(path, "Linux", "NSMB-MarioVsLuigi.x86_64"), BuildTarget.StandaloneLinux64, options);
    }

    void BuildMac()
    {
        BuildOptions options = macDebug ? BuildOptions.AllowDebugging : BuildOptions.None;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
        if (macIL)
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            if (macDebug)
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Debug);
            else
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);
        }
        else
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        }

        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).ToArray(), Path.Combine(path, "MacOS", "NSMB-MarioVsLuigi.app"), BuildTarget.StandaloneOSX, options);
    }

    void BuildWebGL()
    {
        BuildOptions options = WebDebug ? BuildOptions.AllowDebugging : BuildOptions.None;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).ToArray(), Path.Combine(path, "Web"), BuildTarget.WebGL, options);
    }
}
#endif