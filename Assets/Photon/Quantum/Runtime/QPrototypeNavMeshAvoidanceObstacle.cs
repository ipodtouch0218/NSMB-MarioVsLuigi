namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeNavMeshAvoidanceObstacle : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.NavMeshAvoidanceObstaclePrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.NavMeshAvoidanceObstacle> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.NavMeshAvoidanceObstaclePrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.NavMeshAvoidanceObstacle);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}