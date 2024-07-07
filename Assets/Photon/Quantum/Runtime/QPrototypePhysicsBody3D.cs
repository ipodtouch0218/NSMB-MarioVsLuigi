namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsBody3D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsBody3DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsBody3D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.PhysicsBody3DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsBody3D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}