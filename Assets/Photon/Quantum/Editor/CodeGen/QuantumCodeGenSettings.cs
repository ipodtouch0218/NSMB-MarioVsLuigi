namespace Quantum.Editor {
  using CodeGen;
  using UnityEditor;
  using UnityEditor.Build;

  /// <summary>
  /// Settings for the Quantum code generation. Extend this class with a partial implementation to customize the settings.
  /// </summary>
  public static partial class QuantumCodeGenSettings {
    
    /// <summary>
    /// The default folder path for the Simulation generated code.
    /// </summary>
    public const string DefaultCodeGenQtnFolderPath          = "Assets/QuantumUser/Simulation/Generated";
    /// <summary>
    /// The default folder path for the Unity runtime generated code.
    /// </summary>
    public const string DefaultCodeGenUnityRuntimeFolderPath = "Assets/QuantumUser/View/Generated";
    
    /// <summary>
    /// The default code generation options. If <code>QUANTUM_ENABLE_MIGRATION</code> is defined,
    /// <see cref="GeneratorLegacyOptions.DefaultMigrationFlags"/> are used for <see cref="GeneratorOptions.LegacyCodeGenOptions"/>.
    /// </summary>
    public static GeneratorOptions DefaultOptions => new() {
      LegacyCodeGenOptions = 
        (IsMigrationEnabled ? GeneratorLegacyOptions.DefaultMigrationFlags : default) |
        (IsQuantum3PreviewMigrationEnabled ? GeneratorLegacyOptions.BuiltInComponentPrototypeWrappers : default),
    };

    /// <summary>
    /// Creates a new instance of <see cref="GeneratorOptions"/> with the default options. Uses <see cref="DefaultOptions"/> and
    /// calls <see cref="GetOptionsUser"/> to customize the options.
    /// </summary>
    public static GeneratorOptions Options {
      get {
        var options = DefaultOptions;
        GetOptionsUser(ref options);
        return options;
      }
    }
    
    /// <summary>
    /// Returns the folder path for the Simulation generated code. Uses <see cref="DefaultCodeGenQtnFolderPath"/> and
    /// calls <see cref="GetCodeGenFolderPathUser"/> to customize the path.
    /// </summary>
    public static string CodeGenQtnFolderPath {
      get {
        var path = DefaultCodeGenQtnFolderPath;
        GetCodeGenFolderPathUser(ref path);
        return path;
      }
    }

    /// <summary>
    /// Returns the folder path for the Unity runtime generated code. Uses <see cref="DefaultCodeGenUnityRuntimeFolderPath"/> and
    /// calls <see cref="GetCodeGenUnityRuntimeFolderPathUser"/> to customize the path.
    /// </summary>
    public static string CodeGenUnityRuntimeFolderPath {
      get {
        var path = DefaultCodeGenUnityRuntimeFolderPath;
        GetCodeGenUnityRuntimeFolderPathUser(ref path);
        return path;
      }
    }
    
    /// <summary>
    /// Implement this method to customize the code generation options.
    /// </summary>
    /// <param name="options"></param>
    static partial void GetOptionsUser(ref GeneratorOptions options);
    
    /// <summary>
    /// Implement this method to customize the code generation Simulation folder path.
    /// </summary>
    /// <param name="path"></param>
    static partial void GetCodeGenFolderPathUser(ref string path);
    
    /// <summary>
    /// Implement this method to customize the code generation View folder path.
    /// </summary>
    /// <param name="path"></param>
    static partial void GetCodeGenUnityRuntimeFolderPathUser(ref string path);
    
    
    internal const int MenuPriority = 4000;
    internal static bool IsMigrationEnabled => HasDefine("QUANTUM_ENABLE_MIGRATION");
    internal static bool IsQuantum3PreviewMigrationEnabled => HasDefine("QUANTUM_ENABLE_MIGRATION_Q3PREVIEW");

    private static bool HasDefine(string define) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).Split(';');
      return System.Array.IndexOf(defines, define) >= 0;
    }
  }
}