namespace Quantum.Editor {
  using System.Collections.Generic;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Build;

  public static class QuantumCodeGenUtils {
    public static bool? HasScriptingDefineSymbol(string value) {
      bool anyDefined   = false;
      bool anyUndefined = false;
      foreach (var group in ValidBuildTargetGroups) {
        if (HasScriptingDefineSymbol(group, value)) {
          anyDefined = true;
        } else {
          anyUndefined = true;
        }
      }

      return (anyDefined && anyUndefined) ? (bool?)null : anyDefined;
    }
    
    public static bool HasScriptingDefineSymbol(NamedBuildTarget buildTarget, string value) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget).Split(';');
      return System.Array.IndexOf(defines, value) >= 0;
    }
    
    public static IEnumerable<NamedBuildTarget> ValidBuildTargetGroups {
      get {
        foreach (var name in System.Enum.GetNames(typeof(BuildTargetGroup))) {
          if (IsEnumValueObsolete<BuildTargetGroup>(name))
            continue;
          var group = (BuildTargetGroup)System.Enum.Parse(typeof(BuildTargetGroup), name);
          if (group == BuildTargetGroup.Unknown)
            continue;

          yield return NamedBuildTarget.FromBuildTargetGroup(group);
        }
      }
    }
    
    public static bool IsEnumValueObsolete<T>(string valueName) where T : System.Enum {
      var fi         = typeof(T).GetField(valueName);
      var attributes = fi.GetCustomAttributes(typeof(System.ObsoleteAttribute), false);
      return attributes?.Length > 0;
    }
    
    public static void UpdateScriptingDefineSymbol(string define, bool enable) {
      UpdateScriptingDefineSymbolInternal(ValidBuildTargetGroups,
        enable ? new[] { define } : null,
        enable ? null : new[] { define });
    }
    
    private static void UpdateScriptingDefineSymbolInternal(IEnumerable<NamedBuildTarget> groups, IEnumerable<string> definesToAdd, IEnumerable<string> definesToRemove) {
      EditorApplication.LockReloadAssemblies();
      try {
        foreach (var group in groups) {
          var originalDefines = PlayerSettings.GetScriptingDefineSymbols(group);
          var defines         = originalDefines.Split(';').ToList();

          if (definesToRemove != null) {
            foreach (var d in definesToRemove) {
              defines.Remove(d);
            }
          }

          if (definesToAdd != null) {
            foreach (var d in definesToAdd) {
              defines.Remove(d);
              defines.Add(d);
            }
          }

          var newDefines = string.Join(";", defines);
          PlayerSettings.SetScriptingDefineSymbols(group, newDefines);
        }
      } finally {
        EditorApplication.UnlockReloadAssemblies();
      }
    }
  }
}