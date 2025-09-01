namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Xml.Linq;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// A configuration asset that describes the search paths to create a non-Unity simulation project.
  /// </summary>
  [Serializable]
  [CreateAssetMenu(menuName = "Quantum/Configurations/Dotnet Project Settings", order = EditorDefines.AssetMenuPriorityConfigurations + 61)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class QuantumDotnetProjectSettings : QuantumGlobalScriptableObject<QuantumDotnetProjectSettings> {
    /// <summary>
    /// The default location of the global instance.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Editor/QuantumDotnetProjectSettings.asset";
    /// <summary>
    /// Use this Unity asset label to mark assets or paths that should be included in the search paths to generate a non-Unity simulation project file.
    /// </summary>
    public const string IncludeLabel = "QuantumDotnetInclude";

    /// <summary>
    /// The destination path when pressing the Export button in the inspector of this asset, absolute or relative to the Unity project.
    /// </summary>
    [InlineHelp]
    public string OutputProjectPath = "../Quantum.Simulation.Gen.csproj";
    
    /// <summary>
    /// Enable to include all qtn assets in the result.
    /// </summary>
    [InlineHelp]
    public bool IncludeAllQtnAssets = true;
    
    /// <summary>
    /// Enabled to include all asset object script in the result.
    /// </summary>
    [InlineHelp]
    public bool IncludeAllAssetObjectScripts = true;

    private static string GetUnityProjectRoot {
      get {
        var currentPath = Application.dataPath;
        Debug.Assert(currentPath.EndsWith("/Assets"));
        return currentPath.Substring(0, currentPath.Length - "Assets".Length);
      }
    }
    
    /// <summary>
    /// The search paths to collect all simulation source files from.E.g. files that are included in the Quantum.Simulation.asmdef.
    /// </summary>
    [Header("Files and folders can either be marked with " + IncludeLabel + " label or included here.")]
    public string[] IncludePaths = new[] { 
      "Assets/QuantumUser/Simulation", 
      QuantumUnityEditorPaths.Root + "/Simulation" };

    /// <summary>
    /// Export the non-Unity simulation project to <see cref="OutputProjectPath"/>.
    /// </summary>
    [EditorButton("Export")]
    void Export() {
      var path = Export(null);

      if (!string.IsNullOrEmpty(path)) {
        QuantumEditorLog.Log($"Exported to {path}");
      }
    }
    
    /// <summary>
    /// Export the non-Unity simulation project to a certain location.
    /// </summary>
    /// <param name="outputPath">Destination path, should end with .csproj</param>
    public string Export(string outputPath) {
      var includes = new List<string>();
      
      if (IncludeAllQtnAssets) {
        var qtnAssets = AssetDatabase.FindAssets($"t:" + nameof(QuantumQtnAsset)).Select(AssetDatabase.GUIDToAssetPath);
        includes.AddRange(qtnAssets);
      }

      if (IncludeAllAssetObjectScripts) {
        var assetScripts = TypeCache.GetTypesDerivedFrom<AssetObject>()
          .Select(type => {
            var script = (MonoScript)UnityInternal.EditorGUIUtility.GetScript(type.FullName);
            if (script == null) {
              return null;
            } else {
              Assert.Check(script.GetClass() == type, "Expected type {0} but got {1}", type, script.GetClass());
              return AssetDatabase.GetAssetPath(script);
            }
          })
          .Where(x => !string.IsNullOrEmpty(x))
          .Where(x => Path.GetExtension(x) == ".cs")
          .Distinct();
        includes.AddRange(assetScripts);
      }

      // ... also, add any file marked with a label
      {
        var labeledAssets = AssetDatabase.FindAssets($"l:{IncludeLabel}")
          .Select(AssetDatabase.GUIDToAssetPath);
        includes.AddRange(labeledAssets);
      }
      
      // now remove duplicates and files otherwise included explicitly
      includes = includes.Distinct().ToList();
      
      // add explicit paths
      foreach (var path in IncludePaths) {
        if (Directory.Exists(path)) {
          // remove any path that starts with this folder
          var dirWithTrailingSlash = path.TrimEnd('/') + "/";
          includes.RemoveAll(x => x.StartsWith(dirWithTrailingSlash));
        }
        
        includes.Add(path);
      }

      var defines = Array.Empty<string>();

      var effectiveOutputPath = outputPath ?? OutputProjectPath;
      Export(effectiveOutputPath, includes.ToArray(), defines);
      return effectiveOutputPath;
    }

    /// <summary>
    /// Export the non-Unity simulation project to a certain location.
    /// </summary>
    /// <param name="outputPath">Destination path, should end with .csproj</param>
    /// <param name="includes">The list of files to add to the Includes list of the project file</param>
    /// <param name="defines">The defines</param>
    public static void Export(string outputPath, string[] includes, string[] defines) {
      // turn current path into path relative to outputPath
      var contents = GeneratePartialQuantumGameCsProj(
        outputPath,
        Path.GetRelativePath(Path.GetDirectoryName(outputPath), QuantumCodeGenSettings.CodeGenQtnFolderPath),
        includes,
        defines
      );
      
      var outputFolder = Path.GetDirectoryName(outputPath);
      Assert.Check(outputFolder != null);
      Directory.CreateDirectory(outputFolder);

      File.WriteAllText(outputPath, contents);
    }

    static string GeneratePartialQuantumGameCsProj(
      string outputPath,
      string codegenOutput,
      string[] includes,
      string[] defines) {

      outputPath = PathUtils.Normalize(outputPath);
      var outputFolder = Path.GetDirectoryName(outputPath);
      
      var projectElement = new XElement("Project");
      
      projectElement.Add(new XComment("Properties"));
      var properties = new XElement("PropertyGroup");
      properties.Add(new XElement("QuantumCodeGenOutput", codegenOutput));
      properties.Add(new XElement("DefineConstants", string.Join(";", defines.Concat(new [] { "$(DefineConstants)" }))));
      projectElement.Add(properties);
      
      projectElement.Add(new XComment("Includes"));
      
      var group = new XElement("ItemGroup");

      foreach (var source in includes) {

        if (Directory.Exists(source)) {
          group.Add(new XElement("Compile",
            new XAttribute("Include", $"{GetPath(outputFolder, source)}/**/*.cs"),
            new XAttribute("LinkBase", GetLink(source))));
          group.Add(new XElement("None",
            new XAttribute("Include", $"{GetPath(outputFolder, source)}/**/*.qtn"),
            new XAttribute("LinkBase", GetLink(source))));
          continue;
        }
        
        var extension = Path.GetExtension(source).ToLower();
        if (extension == ".dll") {
          // add assembly reference
          var assemblyName = Path.GetFileNameWithoutExtension(source);
          group.Add(new XElement("Reference",
            new XAttribute("Include", assemblyName),
            new XElement("HintPath", GetPath(outputFolder, source))
          ));
        } else {
          group.Add(new XElement(extension == ".cs" ? "Compile" : "None",
            new XAttribute("Include", GetPath(outputFolder, source)),
            new XElement("Link", GetLink(source)))
          );
        }
      }
      
      projectElement.Add(group);
      
      var document = new XDocument(projectElement);
      return document.ToString();
    }

    static string GetLink(string path) {
      if (path.StartsWith("Assets/", StringComparison.InvariantCulture)) {
        return path.Substring("Assets/".Length);  
      }

      if (path.StartsWith("Packages/", StringComparison.InvariantCulture)) {
        return path.Substring("Packages/".Length);
      }

      throw new ArgumentException($"Expected path to begin with Assets/ or Packages/.");
    }

    static string GetPath(string pathPrefix, string path) {
      return Path.GetRelativePath(pathPrefix, Path.GetFullPath(path));
    }
  }
}