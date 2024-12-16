# Online Documentation

https://doc.photonengine.com/quantum/v3

# Overview

Photon Quantum is a high-performance deterministic ECS (Entity Component System) framework for online multiplayer games made with Unity.

It uses a method called predict/rollback, which is ideal for latency-sensitive online games like action RPGs, sports games, fighting games, FPS and more.

Quantum helps developers write clean code. It decouples simulation logic (Quantum ECS) from the view/presentation (Unity), and takes care of the network implementations specifics (internal predict/rollback + transport layer + game agnostic server logic).

Quantum implements a state-of-the-art tech stack composed of the following pieces:

- Server-managed predict/rollback simulation core.
- Sparse-set ECS memory model and API.
- Complete set of stateless deterministic libraries (math, 2D and 3D physics, navigation, etc.).
- Rich Unity editor integration and tooling.

All built on top of mature and industry-proven existing Photon products and infrastructure (Photon Realtime transport layer, Photon Server plugin to host server logic, etc.).

# Content

The Quantum Unity SDK is split into four assemblies:

  - `Quantum.Simulation`: contains simulation code. Any user simulation code should be added to this assembly with `AssemblyDefinitionReferences`. Unity/Odin property attributes can be used at will, but any use of non-deterministic Unity API is heavy discouraged. Code form this assembly can be easily worked on as a standalone `.csproj`, similar to `quantum.code.csproj` in Quantum 2.
  - `Quantum.Unity`: contains code specific to Quantum's integration with Unity. Additionally, CodeGen emits `MonoBehaviours` that wrap component prototypes.
  - `Quantum.Unity.Editor`: contains editor code for `Quantum.Simulation` and `Quantum.Unity`
  - `Quantum.Unity.Editor.CodeGen`: contains CodeGen integration code. It is fully independent of other Quantum assemblies, so can be always run, even if there are compile errors - this may require exiting Safe Mode.

After installing Quantum, the user is presented with following folder structure:

```
Assets
├───Photon
│   ├───PhotonLibs
│   ├───PhotonRealtime
│   ├───Quantum
│   ├───QuantumAsteroids
│   └───QuantumMenu
└───QuantumUser
    ├───Editor
    │   ├───CodeGen
    |   └───Generated
    ├───Resources
    ├───Scenes
    ├───Simulation
    │   └───Generated
    └───View
        └───Generated
```

Upgrading will replace all files in Photon subfolders but not in QuantumUser.

All deterministic simulation code MUST be in `QuantumUser/Simulation` or included in the Quantum.Simulation assembly reference.

Code extending Quantum view scripts CAN be in `Quantum.Unity` or `Quantum.Unity.Editor` (e.g. partial methods) using their respective assembly references.

The Quantum SDK folders are:

* `Assets/Photon/Quantum/Assemblies` - contains `netstandard2.1` Quantum libraries (Quantum.Deterministic, Quantum.Engine, Quantum.Corium, and Quantum.Log) as well release and debug versions in zip archives. Some of the libraries have dependencies to UnityEngine.dll for inspector purposes.
* `Assets/Photon/Quantum/Editor` - contains Quantum editor scripts that are compiled into Quantum.Unity.Editor.dll
* `Assets/Photon/Quantum/Editor/Assemblies` - contains Quantum CodeGen dependencies
* `Assets/Photon/Quantum/Editor/CodeGen` - contains Quantum CodeGen tools that compile into an extra dll Quantum.Unity.Editor.CodeGen.dll
* `Assets/Photon/Quantum/Simulation` - contains Quantum simulation code that is compiled into Quantum.Simulation.dll
* `Assets/Photon/Quantum/Resources` - contains fixed point math lookup tables (LUT), gizmos, a Quantum stats and multi runner prefab
* `Assets/Photon/Quantum/Runtime` - contains Quantum Unity scripts that compile into Quantum.Unity.dll
