# 3.1.0

## Preview

Disclaimer: The Quantum SDK 3.1.0 development snapshots are not intended to be used for live games.

### Build 1878 (Oct 29, 2025)

**Bug Fixes**

- Fixed: An issue where clients with certain heap configurations would desync upon late-joining

### Build 1873 (Oct 23, 2025)

**What's New**

- Support to scheduling multiple Physics Updates in the same frame simulation

### Build 1872 (Oct 21, 2025)

**Changes**

- The default heap management mode for migrating projects is now the new `PageBased` mode instead of

**Bug Fixes**

- Fixed: An issue where `FrameBase.ComponentCount` throws an `AssertException` for table components that have never been added to an entity

### Build 1871 (Oct 18, 2025)

**Bug Fixes**

- Fixed: A regression where ECS internals would GC allocate when iterating elements of `ComponentSet`
- Fixed: An issue that caused pause mode stepping in Unity Editor to not simulate one tick at a time

### Build 1870 (Oct 17, 2025)

**What's New**

- Added a NavMesh import option to load auto-generated navmesh links directly from the Unity navmesh, only works in Unity Editor
- Added `QuantumRunnerExtensions` methods to simplify starting Quantum in different modes by providing specific Init()-methods to create `SessionRunner.Arguments`, see the usage inside the `QuantumRunnerLocalDebug.cs` script for example
- Quantum instant replays now also work when being activated during a replay

**Changes**

- Renamed `QuantumGame.CreateSavegame()` to `GetSnapshotFile()`, retired the `QuantumRunnerLocalSavegame.cs` script and merged its functionality with `QuantumRunnerLocalDebug.cs`
- Corrected a typo in `QuantumRunnerUnityFactory.CreatePlatformInfo` and changed the static method to a property
- Removed the `StartWithFrame()` method from the `QuantumRunnerLocalDebug` class

**Bug Fixes**

- Fixed: A bug where multiple tasks dispatched from the same `SystemThreadedFilter` can visit the same entities. (The slice length was not being respected.)
- Fixed: An issue that caused the navmesh agent to chose any navmesh link instead of the closest one when having multiple links available that connects two triangles
- Fixed: An issue that caused the `QuantumRunnerLocalDebug` script to not apply the `SimulationSpeedMultiplier` when using `EngineDeltaTime`

### Build 1869 (Oct 16, 2025)

**What's New**

- `Quantum.Compression` - an abstract base class for compression algorithms. Comes with two implementations: `CompressionDotNet` (default) and `CompressionSharpLibZib` (enabled when `com.unity.sharp-zip-lib` package is present). Using the latter might help with "Runtime Speed with LTO" Web builds issues. Both implementations produce the same results and rely on `GZip` format

**Changes**

- `ByteUtils` compression methods made obsolete, use `Quantum.Compression` instead
- `CollisionChecks` in Physics2D and Physics3D namespaces are now static classes

**Bug Fixes**

- Fixed: System task profiler entries not being recorded in non-development builds

### Build 1865 (Oct 15, 2025)

**Bug Fixes**

- Fixed: A regression where filters didn't skip entities pending destruction
- Fixed: An issue that caused the heap settings in SimulationConfig of existing projects to not be migrated correctly

### Build 1863 (Oct 14, 2025)

**Breaking Changes**

- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the `Simulation Rate`, input `Offset Ping Start` and input `Offset Min`, to override this behaviour toggle `Override Hard Tolerance`

**What's New**

- Added `Prediction` statistic to the GraphProfilers and QuantumStats window that shows how many ticks the simulation goes into prediction

**Changes**

- Some stats on the QuantumStats window are now smoothed and show an average (1 second) to make them more readable

### Build 1862 (Oct 11, 2025)

**Bug Fixes**

- Fixed: An issue that could cause an ArgumentException similar to `X cannot be greater than Y` after late-joining

### Build 1861 (Oct 10, 2025)

**Bug Fixes**

- Fixed: An issue in the component block iterator that could cause the exception `_blockCount > 0`

### Build 1859 (Oct 09, 2025)

**Bug Fixes**

- Fixed: An issue in `QuantumStartUI` that caused multiple builds on the same machine that all used the same user name to not join the same room
- Fixed: An issue that caused the `QuantumStartUI` to show the popup window when stopping the Editor during connecting

### Build 1854 (Oct 08, 2025)

**Breaking Changes**

- The library `Quantum.Deterministic.dll` was merged with `Quantun.Engine.dll` and remnants of the old dll have to be deleted from projects that migrate to SDK 3.1
- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the simulation rate and input offset ping start, to override this behaviour toggle `Override Hard Tolerance`
- `SessionRunner.Arguments` now requires setting an explicit `TaskRunner` to be set. This can be done by either specifying `TaskRunner = QuantumTaskRunnerJobs.GetInstance()` or by implicitly setting the TaskRunner by using `GameParameters = QuantumRunnerUnityFactory.CreateGameParameters` for Unity. Use `TaskRunner = new DotNetTaskRunner()` outside of Unity
- The demo input `.unitypackage` now requires the installation of the Unity module `com.unity.inputsystem`
- `Quantum.Profiling.HostProfiler` was renamed to `Quantum.HostProfiler`

**What's New**

- Added `table` components, which are an alternative to the sparse set ECS components that scale better, see the documentation for more details
- Added a new physics solver `ProjectedGaussSeidel` that greatly improves the stability when stacking rigid bodies, to use the legacy solver select it in the SimulationConfig
- Added `SimulatorContext` to exposes simulation callbacks, which allow for modifications to future simulation and prediction advancements, see the documentation for more details
- Optimized the memory required to store the sparse set ECS components which increases the performance of frame copies
- Added new counters to the `GraphProfiler` to track bandwidth for incoming and outgoing traffic
- Added Unity profiler counters such as `Q Frames Verified`, `Q Frames Predicted`, and `Q Simulation Time`, that can be added to the Unity profiler windows as a custom profiler module
- Added new non-biased random number generation methods to `RNGSession`: `Next(long, long)`, `NextInclusive(long, long)`, `Next(uint)`, `Next(ulong)`, `NextUInt32()`, `Int64()`
- Added `QuantumIgnore` label support - apply to an asset to be ignored by `QuantumUnityDB`, useful on prefabs that are used as map prototypes exclusively for example
- Added `SimulationUpdateTime.EngineUnscaledCappedDeltaTime` which uses a capped version of unscaled delta time, this improves support for breakpoints and Unity Editor pausing in local mode
- Added an `FP` converter utility window that converts `double` to `FP` and raw values
- Added a new configurable value called `Heuristic Weight` to the `NavMeshAgentConfig` which can improve the performance of the A* algorithm algorithm, try setting it to `1.5`
- Added support for Unity's `InputSystem` actions which is now used by the demo Quantum input scripts

**Changes**

- The default simulation rate was increased from `60 Hz` to `64 Hz` (using powers of two), ensuring that `DeltaTime` has no rounding error and providing greater precision in physics calculations
- The multi client scripts have been moved out of the SDK package into a `.unitypackage` (`Assets/Photon/Quantum/PackageResources/Quantum-MultiClient`)
- Exporting replays and snapshots via the menu now saves the last save location as a relative path
- Changed the NavMesh API by renaming `Map.NavMeshLinks` to `Map.NavMeshAssets` and by removing the property `NavMeshAgentConfig.AutomaticTargetCorrection` instead `AutomaticTargetCorrectionRadius` > 0 is checked to test if target correction is enabled

