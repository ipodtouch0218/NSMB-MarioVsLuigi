namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypePhysicsJoints3D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.PhysicsJoints3DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.PhysicsJoints3D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.Unity.PhysicsJoints3DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.PhysicsJoints3D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}