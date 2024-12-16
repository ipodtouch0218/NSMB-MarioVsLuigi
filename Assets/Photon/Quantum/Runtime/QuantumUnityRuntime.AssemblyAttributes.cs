#if !QUANTUM_DEV

#region Assets/Photon/Quantum/Runtime/AssemblyAttributes/QuantumAssemblyAttributeMapBake.cs

// Tag this assembly to receive map bake callbacks, e.g. used to invalidate gizmo cache
[assembly: Quantum.QuantumMapBakeAssembly]


#endregion


#region Assets/Photon/Quantum/Runtime/AssemblyAttributes/QuantumAssemblyAttributes.Common.cs

// merged AssemblyAttributes

#region RegisterResourcesLoader.cs

// register a default loader; it will attempt to load the asset from their default paths if they happen to be Resources
[assembly: Quantum.QuantumGlobalScriptableObjectResource(typeof(Quantum.QuantumGlobalScriptableObject), Order = 2000, AllowFallback = true)]

#endregion



#endregion

#endif
