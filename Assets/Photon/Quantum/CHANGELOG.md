# 3.1.0

## Preview

Disclaimer: The Quantum SDK 3.1.0 development snapshots are not intended to be used for live games.

**Breaking Changes**

- The library `Quantum.Deterministic.dll` was merged with `Quantun.Engine.dll` and remnants of the old dll have to be deleted from projects that migrate to SDK 3.1
- The API for sending and processing commands has changed so that multiple commands per frame can be send without requiring extra management using a `CompoundCommand`. Sending commands changes to `QuantumGame.AddCommand(DeterministicCommand)` and processing commands uses an iterator `foreach (var command in frame.GetPlayerCommands<MyCommand>(player)) { }`.
- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the simulation rate and input offset ping start, to override this behaviour toggle `Override Hard Tolerance`
- `SessionRunner.Arguments` now requires setting an explicit `TaskRunner` to be set. This can be done by either specifying `TaskRunner = QuantumTaskRunnerJobs.GetInstance()` or by implicitly setting the TaskRunner by using `GameParameters = QuantumRunnerUnityFactory.CreateGameParameters` for Unity. Use `TaskRunner = new DotNetTaskRunner()` outside of Unity
- The demo input `.unitypackage` now requires the installation of the Unity module `com.unity.inputsystem`
- `Quantum.Profiling.HostProfiler` was renamed to `Quantum.HostProfiler`
- The `DeterministicSessionConfig` inspector now computes the `Hard Tolerance` based on the `Simulation Rate`, input `Offset Ping Start` and input `Offset Min`, to override this behaviour toggle `Override Hard Tolerance`

**What's New**

- Added `table` components, which are an alternative to the sparse set ECS components that scale better, see the documentation for more details
- Added a new physics solver `ProjectedGaussSeidel` that greatly improves the stability when stacking rigid bodies, to use the legacy solver select it in the SimulationConfig
- Added `SimulatorContext` to exposes simulation callbacks, which allow for modifications to future simulation and prediction advancements, see the documentation for more details
- Optimized the memory required to store the sparse set ECS components which increases the performance of frame copies
- Upgraded the graph profilers to combine multiple metrics in one graph to better analyse general simulation performance and online play
- Added Unity profiler counters such as `Q Frames Verified`, `Q Frames Predicted`, and `Q Simulation Time`, that can be added to the Unity profiler windows as a custom profiler module
- Added `FPVector2` and `FPVector3.Average` methods for computing the average of a span of vectors
- Added new non-biased random number generation methods to `RNGSession`: `Next(long, long)`, `NextInclusive(long, long)`, `Next(uint)`, `Next(ulong)`, `NextUInt32()`, `Int64()`
- Added `QuantumIgnore` label support - apply to an asset to be ignored by `QuantumUnityDB`, useful on prefabs that are used as map prototypes exclusively for example
- Added `SimulationUpdateTime.EngineUnscaledCappedDeltaTime` which uses a capped version of unscaled delta time, this improves support for breakpoints and Unity Editor pausing in local mode
- Added an `FP` converter utility window that converts `double` to `FP` and raw values
- Added a new configurable value called `Heuristic Weight` to the `NavMeshAgentConfig` which can improve the performance of the A* algorithm algorithm, try setting it to `1.5`
- Added a NavMesh import option to load auto-generated navmesh links directly from the Unity navmesh, only works in Unity Editor
- Added QuantumRunnerExtensions methods to simplify starting Quantum in different modes by providing specific `Init()`-methods to create `SessionRunner.Arguments`, see the usage inside the `QuantumRunnerLocalDebug.cs` script for example
- Added support for Unity's `InputSystem` actions which is now used by the demo Quantum input scripts
- Added a 3D Physics standalone solver API, available through the `Quantum.Physics3D.CollisionSolver` static class
- Added `PhysicsBody3D.ComputeWorldInertiaTensorInverse` to compute the world-space version of a physics body inverse inertia tensor
- Added support for scheduling multiple Physics Updates in the same frame simulation
- Added Asset Bundle support - Quantum Unity DB recognizes assets that can be loaded by Asset Bundles. Use `QuantumAssetSourceAssetBundle` static delegates to change the way bundles themselves are loaded and unloaded
- Added `Quantum.Compression` - an abstract base class for compression algorithms. Comes with two implementations: `CompressionDotNet` (default) and `CompressionSharpLibZib` (enabled when `com.unity.sharp-zip-lib` package is present). Using the latter might help with "Runtime Speed with LTO" Web builds issues. Both implementations produce the same results and rely on `GZip` format

**Changes**

- The default simulation rate was increased from `60 Hz` to `64 Hz` (using powers of two), ensuring that `DeltaTime` has no rounding error and providing greater precision in physics calculations
- The multi client scripts have been moved out of the SDK package into a `.unitypackage` (`Assets/Photon/Quantum/PackageResources/Quantum-MultiClient`)
- Exporting replays and snapshots via the menu now saves the last save location as a relative path
- Changed the NavMesh API by renaming `Map.NavMeshLinks` to `Map.NavMeshAssets` and by removing the property `NavMeshAgentConfig.AutomaticTargetCorrection` instead `AutomaticTargetCorrectionRadius` > 0 is checked to test if target correction is enabled
- Renamed `QuantumGame.CreateSavegame()` to `GetSnapshotFile()`, retired the `QuantumRunnerLocalSavegame.cs` script and merged its functionality with `QuantumRunnerLocalDebug.cs`
- `ByteUtils` compression methods made obsolete, use `Quantum.Compression` instead
- Stats on the QuantumStats window (like bandwidth) are now smoothed and show an average (1 second) to make them more readable
- `QuantumUnityDB` does not throw exceptions in `TryGet*` methods if the DB was failed to be loaded
- `QuantumCallbackHandler_UnityCallbacks.LoadAddressableScenePathsAsync` is now static

### Build 1940 (Dec 16, 2025)

**What's New**

- Bringing back the Quantum open scene toolbar for Unity 6.3

### Build 1939 (Dec 15, 2025)

**What's New**

- `QUANTUM_DISABLE_ASSET_BUNDLE_ASSET_SOURCE` - adding this define will disable `QuantumAssetObjectSourceAssetBundle` 

**Bug Fixes**

- Fixed: `frame.DestroyPending(entity)` giving incorrect results
- Fixed: An issue where the frame (de)serializer skipped the table location info of entities pending destruction.  
This is really a `NullReferenceException` in disguise, but `Ptr.Null` is resolving to an actual pointer (to memory that has been filled with zeros) instead of to `null`. (That will be addressed in a separate fix.)

### Build 1935 (Dec 11, 2025)

**Bug Fixes**

- Fixed: An issue that disabled the Quantum toolbar for non-Unity 6.3 versions in the previous build
- Fixed: An issue that caused to import a Unity navmesh although toggled off in the map build chain

### Build 1929 (Dec 09, 2025)

**What's New**

- Added an `OnResync()` method to all Quantum systems, which is called when the simulation is about to begin after starting from a snapshots
- Asteroid and demo input scripts fully support Unity Input System

**Changes**

- 3D Physics `CollisionSolver.Solve` methods now perform multiple solver iterations by default, unless specified
- The Quantum open scene toolbar is disabled for Unity 6.3

**Bug Fixes**

- Fixed: Obsolete warnings in `QuantumUnityDBScopeImporter.cs` in Unity 6000.3

### Build 1925 (Dec 08, 2025)

**Bug Fixes**

- Fixed: Unity 6000.3 support

### Build 1922 (Dec 06, 2025)

**Bug Fixes**

- Fixed: An issue when using `AllocateOnComponentAdded` on collections inside Globals, which would make the Heap Tracker thrown an exception if the collection was expanded
- Fixed: The collision between polygons and polygons was not being detected when the edges of one of the polygons were perfectly aligned
- Fixed: The collision between circles and polygons was not being detected when the edges of the polygon were perfectly aligned, i.e., when the angle difference between two edges was 180 degrees
- Fixed: Invalid order of guids in `QuantumUnityDB.GetAssetInternal` assertion message
- Fixed: The intersection between 2D boxes was not generating a valid contact point, and the de-penetration direction was inverted

### Build 1919 (Dec 03, 2025)

**Breaking Changes**

- The API for sending and processing commands has changed so that multiple commands per frame can be send without requiring extra management using a `CompoundCommand`. Sending commands changes to `QuantumGame.AddCommand(DeterministicCommand)` and processing commands uses an iterator `foreach (var command in frame.GetPlayerCommands<MyCommand>(player)) { }`

**What's New**

- `QuantumQtnAssetImporter.UseCustomSettings`: enables per-file custom code generation. Can be used to isolate parts of Qtn into libraries. Refer to the class documentation for details and limitations

**Changes**

- [CodeGen] `GeneratorOptions.NewLine` is now an enum (was: string)
- [CodeGen] Imported components do not need their size specified as long as they are not used as fields in other components/structs. In other words, syntax `import component Foo;` is now valid
- [CodeGen] `EnsureNotStripped` generated method renamed to `EnsureNotStrippedGen`. Still not to be called directly
- [FrameContextUser] Update constructor signature to `FrameContextUser(Args args, IRuntimeConfig runtimeConfig)` and pass the runtime config from QuantumGame

**Bug Fixes**

- Fixed: Issues in `CollisionSolver.Solve` overloads that received a `CollisionResultInfo3D` and used an inverted normal, not solving the collision appropriately

### Build 1918 (Dec 02, 2025)

**What's New**

- `PhysicsSceneSettings` now has a `DefaultPhysicsMaterialData` initialized from the Physics Material asset defined in the Simulation Config

**Changes**

- `ISignal` interfaces and Quantum system classes now use the `frame` parameter name instead of abbreviating it with `f`

**Bug Fixes**

- Fixed: Issue in `CollisionSolver.Solve` overloads that did not receive a `Frame` parameter throwing `NullReferenceException` when at least one of the colliders did not have a valid Physics Material

### Build 1914 (Dec 01, 2025)

**What's New**

- Added more pre-defined `ColorRGBA` color variations that can be used in the debug `Draw()` from simulation utility

### Build 1913 (Nov 28, 2025)

**What's New**

- `FPMathUtils.TryLoadLookupTables`
- `QuantumGlobalScriptableObjectUtils.TryImportGlobal`
- Asset Bundle support - Quantum Unity DB recognizes assets that can be loaded by Asset Bundles. Use `QuantumAssetSourceAssetBundle` static delegates to change the way bundles themselves are loaded and unloaded

**Changes**

- `QuantumUnityDB` does not throw exceptions in `TryGet*` methods if the DB was failed to be loaded
- `QuantumUnityDB.OnEnable` no longer loads math lookup tables

**Bug Fixes**

- Fixed: An issue in the navmesh auto baking tools that tried importing a Unity navmesh when `ImportUnityNavMesh` is off and `BakeNavMesh` is on

### Build 1911 (Nov 27, 2025)

**Bug Fixes**

- Fixed: An exception thrown when a component is removed, re-added, and removed again before the first remove commits
- Fixed: An issue in 3.1.0 Preview 1908 that broke filters on sparse components

### Build 1909 (Nov 25, 2025)

**Bug Fixes**

- Fixed: Issues with Dynamic Maps when resetting the physics scene after adding static colliders
- Fixed: DB Scopes not importing the root asset when going through specified `AssetBundles`

### Build 1908 (Nov 24, 2025)

**Changes**

- `BitStreamReplayInputProvider.Stream` and `MaxFrame` are now public to make the class be re-usable to run a replay with chunked input history

**Bug Fixes**

- Fixed: A (3.1) issue where an entity being destroyed when it had pending removes resulted in those removes not being committed
- Fixed: A (3.1) issue where re-adding a sparse component pending removal would add another copy instead of overwriting the original

### Build 1907 (Nov 21, 2025)

**What's New**

- 3D Physics standalone solver API, available through the `Quantum.Physics3D.CollisionSolver` static class
- `FPVector2` and `FPVector3.Average` methods for computing the average of a span of vectors
- `PhysicsBody3D.ComputeWorldInertiaTensorInverse` to compute the world-space version of a physics body inverse inertia tensor

**Bug Fixes**

- Fixed: The intersection between 2D boxes was not generating a valid contact point, and the de-penetration direction was inverted

### Build 1904 (Nov 20, 2025)

**Changes**

- `QuantumCallbackHandler_UnityCallbacks.LoadAddressableScenePathsAsync` is now static

**Bug Fixes**

- Fixed: Addressable scenes not being unloaded correctly after loading same scene multiple times

### Build 1899 (Nov 18, 2025)

**What's New**

- 2D and 3D PhysicsCollider `Create` overload without Frame parameter for shapes other than Compounds
- Static methods `PhysicsMaterialData.GetCombinedRestitution` and `GetCombinedFriction` to retrieved the resultant settings from the interaction of two physics materials
- Static methods to get rows or columns from `FPMatrix2x2` or `FPMatrix3x3`

**Changes**

- Replaced the `ShowAfter` flags on the `QuantumDotnetBuildSettings` inspector with buttons that open the folder and solutions directly

**Bug Fixes**

- Fixed: An issue that caused the dotnet simulation to complain about old Quantum.Log dependencies in debug after migrating and not explicitly exporting the release configuration
- Fixed: A (3.1) issue where filters with a single component did not test an entity's `ComponentSet` correctly

### Build 1897 (Nov 18, 2025)

**Bug Fixes**

- Fixed: An issue in `TaskHandle.AddDependency` that would cause it to not raise an exception in Release and corrupt memory when going beyond the max number of parents or children

### Build 1896 (Nov 14, 2025)

**Improvements**

- Improved performance and stack memory usage when sorting 2D and 3D Hit collections

### Build 1894 (Nov 13, 2025)

**Changes**

- Removing a few internal Linq usages to reduce garbage allocations
- Improved the initialization of signal arrays to remove garbage allocations
- Renamed/shortened Quantum profiler marker names

### Build 1892 (Nov 11, 2025)

**What's New**

- `IntVector2` and `IntVector3` now have an index operator to access xy and z components
- `FPVector2` and `FPVector3` now have an index operator to access xy and z components
- Upgraded the graph profilers to combine multiple metrics in one graph to better analyse general simulation performance and online play
- Added new stats that measure simulation time spend on calculating predicted and verified frames explicitly (`DeterministicStats.PredictionTime` and `VerificationTime`)

**Changes**

- Updated Photon Realtime to a finalized version of `5.1.9 (10. November 2025)`

### Build 1889 (Nov 08, 2025)

**Bug Fixes**

- Fixed: ECS allocating too much memory upfront when creating and expanding tables

### Build 1888 (Nov 07, 2025)

**Bug Fixes**

- Fixed: An issue in `EditMeshScope.ReserveTriangleCapacity` when used in a `DynamicMap` that could cause an internal mismatch in the static collider index

### Build 1885 (Nov 05, 2025)

**What's New**

- `QuantumUnityDBScope`: an edit time collection of `AssetObjects`, fully decoupled from the global `QuantumUnityDB`. Once the scope is loaded at runtime, to apply it use `QuantumUnityDB.AddScope`
- `QuantumUnityDBScopeImporter.IncludeSubfolders` - if enabled, all assets in the current folder and all the subfolders will be included in the scope (true by default)
- `QuantumUnityDBScopeImporter.AssetBundles` - a list of asset bundles to be included in the scope
- `QuantumUnityDBScopeImporter.ExplicitAssets` - a list of assets to be included in the scope
- `QuantumUnityDB.TryAddScope`
- `QuantumUnityDB.IsScopeLoaded`
- `QuantumUnityDB.RemoveAllScopes`
- `QuantumUnityDBScopeImporter.IsUnique` - if an asset is part of multiple scopes and none of them are unique, the importer won't raise a warning (true by default)

**Changes**

- Adding a scope to `QuantumUnityDB` will print a warning if an asset can't be added due to GUID/path conflict. Previously: an exception would be thrown
- `QuantumUnityDB` reimport is speed up for cases where there aren't any package paths in `QuantumEditorSettings.AssetSearchPaths`

**Bug Fixes**

- Fixed: `QuantumUnityDB.RemoveScope` not removing entries correctly
- Fixed: Scoped assets displaying `<no provider>` under `Quantum Unity DB` section when inspected

### Build 1880 (Oct 31, 2025)

**Bug Fixes**

- Fixed: Addressable scenes not being released properly

### Build 1879 (Oct 30, 2025)

**Bug Fixes**

- Fixed: An issue where new `PageBasedHeap` segments would mistakenly think their blocks start from offset 0

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

