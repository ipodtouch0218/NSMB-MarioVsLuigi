#if !QUANTUM_DEV

#region Assets/Photon/Quantum/Editor/AssemblyAttributes/QuantumEditorAssemblyAttributes.Common.cs

// merged EditorAssemblyAttributes

#region RegisterEditorLoader.cs

// the default edit-mode loader
[assembly:Quantum.Editor.QuantumGlobalScriptableObjectEditorAttribute(typeof(Quantum.QuantumGlobalScriptableObject), AllowEditMode = true, Order = int.MaxValue)]

#endregion



#endregion

#endif
