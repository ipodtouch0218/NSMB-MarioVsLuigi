namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeTransform3D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.Transform3DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.Transform3D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.Transform3DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.Transform3D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}