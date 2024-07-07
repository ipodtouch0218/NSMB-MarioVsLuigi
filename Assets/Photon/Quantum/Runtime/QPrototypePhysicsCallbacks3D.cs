namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsCallbacks3D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsCallbacks3DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsCallbacks3D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.PhysicsCallbacks3DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsCallbacks3D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}