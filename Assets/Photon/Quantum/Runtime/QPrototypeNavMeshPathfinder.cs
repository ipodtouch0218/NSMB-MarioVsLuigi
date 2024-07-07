namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeNavMeshPathfinder : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.NavMeshPathfinderPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.NavMeshPathfinder> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.NavMeshPathfinderPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.NavMeshPathfinder);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}