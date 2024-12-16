namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeTransform2D : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.Transform2DPrototype>, 
    IQuantumUnityPrototypeWrapperForComponent<Quantum.Transform2D> {
    
    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.Transform2DPrototype Prototype = new();
    
    public override System.Type ComponentType => typeof(Quantum.Transform2D);
    
    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}