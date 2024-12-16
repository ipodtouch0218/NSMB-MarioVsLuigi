namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsJoints2D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsJoints2DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsJoints2D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.Unity.PhysicsJoints2DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsJoints2D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}