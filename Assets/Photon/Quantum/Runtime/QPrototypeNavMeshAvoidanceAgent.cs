namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeNavMeshAvoidanceAgent : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.NavMeshAvoidanceAgentPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.NavMeshAvoidanceAgent> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.NavMeshAvoidanceAgentPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.NavMeshAvoidanceAgent);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}