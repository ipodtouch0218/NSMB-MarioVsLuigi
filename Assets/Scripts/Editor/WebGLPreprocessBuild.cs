using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class WebGLPreprocessBuild : IPreprocessBuildWithReport {
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) {
        PlayerSettings.SetAdditionalIl2CppArgs("--compiler-flags=\"-fbracket-depth=512\"");
    }
}
