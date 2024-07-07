namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsBody2D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsBody2DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsBody2D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.PhysicsBody2DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsBody2D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}