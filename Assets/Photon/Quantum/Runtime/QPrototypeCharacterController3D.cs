namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeCharacterController3D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.CharacterController3DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.CharacterController3D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.CharacterController3DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.CharacterController3D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}