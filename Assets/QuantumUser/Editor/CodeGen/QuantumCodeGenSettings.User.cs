namespace Quantum.Editor {
  using Quantum.CodeGen;

  public static partial class QuantumCodeGenSettings {
    static partial void GetCodeGenFolderPathUser(ref string path) { }
    static partial void GetCodeGenUnityRuntimeFolderPathUser(ref string path) { }
    static partial void GetOptionsUser(ref GeneratorOptions options) { }
  }
}