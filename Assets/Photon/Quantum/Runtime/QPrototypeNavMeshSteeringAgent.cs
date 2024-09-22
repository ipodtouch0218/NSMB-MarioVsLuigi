namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeNavMeshSteeringAgent : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.NavMeshSteeringAgentPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.NavMeshSteeringAgent> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.NavMeshSteeringAgentPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.NavMeshSteeringAgent);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}