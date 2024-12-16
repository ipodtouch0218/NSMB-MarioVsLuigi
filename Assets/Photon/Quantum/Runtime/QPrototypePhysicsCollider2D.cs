namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsCollider2D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsCollider2DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsCollider2D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.PhysicsCollider2DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsCollider2D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}