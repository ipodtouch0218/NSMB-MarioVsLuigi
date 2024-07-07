namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeTransform2DVertical : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.Transform2DVerticalPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.Transform2DVertical> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.Transform2DVerticalPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.Transform2DVertical);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}