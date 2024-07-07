namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeCharacterController2D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.CharacterController2DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.CharacterController2D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.CharacterController2DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.CharacterController2D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}