namespace Quantum.Editor {
  using CodeGen;
  using UnityEditor;
  using UnityEditor.Build;

  public static partial class QuantumCodeGenSettings {

    public const int MenuPriority = 4000;

    public const string DefaultCodeGenQtnFolderPath          = "Assets/QuantumUser/Simulation/Generated";
    public const string DefaultCodeGenUnityRuntimeFolderPath = "Assets/QuantumUser/View/Generated";

    public static bool IsMigrationEnabled => HasDefine("QUANTUM_ENABLE_MIGRATION");
    public static bool IsQuantum3PreviewMigrationEnabled => HasDefine("QUANTUM_ENABLE_MIGRATION_Q3PREVIEW");

    private static bool HasDefine(string define) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).Split(';');
      return System.Array.IndexOf(defines, define) >= 0;
    }
    
    public static GeneratorOptions DefaultOptions => new() {
      LegacyCodeGenOptions = 
        (IsMigrationEnabled ? GeneratorLegacyOptions.AssetRefs | GeneratorLegacyOptions.AssetBaseStubs | GeneratorLegacyOptions.AssetObjectAccessors | GeneratorLegacyOptions.UnderscorePrototypesSuffix | GeneratorLegacyOptions.EntityComponentStubs : default) |
        (IsQuantum3PreviewMigrationEnabled ? GeneratorLegacyOptions.BuiltInComponentPrototypeWrappers : default),
    };

    public static GeneratorOptions Options {
      get {
        var options = DefaultOptions;
        GetOptionsUser(ref options);
        return options;
      }
    }
    
    public static string CodeGenQtnFolderPath {
      get {
        var path = DefaultCodeGenQtnFolderPath;
        GetCodeGenFolderPathUser(ref path);
        return path;
      }
    }

    public static string CodeGenUnityRuntimeFolderPath {
      get {
        var path = DefaultCodeGenUnityRuntimeFolderPath;
        GetCodeGenUnityRuntimeFolderPathUser(ref path);
        return path;
      }
    }
    
    
    static partial void GetOptionsUser(ref GeneratorOptions options);
    static partial void GetCodeGenFolderPathUser(ref string path);
    static partial void GetCodeGenUnityRuntimeFolderPathUser(ref string path);
  }
}