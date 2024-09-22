namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsCallbacks2D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsCallbacks2DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsCallbacks2D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.PhysicsCallbacks2DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsCallbacks2D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}