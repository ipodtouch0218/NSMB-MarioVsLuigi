namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsCollider3D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsCollider3DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsCollider3D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.PhysicsCollider3DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsCollider3D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}