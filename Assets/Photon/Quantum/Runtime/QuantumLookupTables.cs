namespace Quantum {
  using UnityEngine;

  [QuantumGlobalScriptableObject(DefaultPath)]
  [CreateAssetMenu(menuName = "Quantum/Configurations/LookupTables", fileName = "QuantumLookupTables", order = EditorDefines.AssetMenuPriorityConfigurations + 32)]
  public class QuantumLookupTables : QuantumGlobalScriptableObject<QuantumLookupTables> {
    public const string DefaultPath = "Assets/QuantumUser/Resources/QuantumLookupTables.asset";
      
    public TextAsset TableSinCos;
    public TextAsset TableTan;
    public TextAsset TableAsin;
    public TextAsset TableAcos;
    public TextAsset TableAtan;
    public TextAsset TableSqrt;
  }
}