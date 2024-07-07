namespace Quantum {
  using UnityEngine;


  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeView : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.ViewPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.View> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.ViewPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.View);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}