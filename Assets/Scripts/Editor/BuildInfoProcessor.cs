// https://forum.unity.com/threads/build-date-or-version-from-code.59134/

using System;
using System.IO;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class BuildInfoProcessor : IPreprocessBuildWithReport {
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) {
        StringBuilder sb = new();
        sb.Append("public static class BuildInfo");
        sb.Append("{");
        sb.Append("public static string BUILD_TIME = \"");
        sb.Append(DateTime.UtcNow.ToString());
        sb.Append("\";");
        sb.Append("}");

        using StreamWriter file = new(@"Assets/Scripts/BuildInfo.cs");
        file.WriteLine(sb.ToString());
    }
}

