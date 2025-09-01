namespace Quantum {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEngine;

  /// <summary>
  /// A Quantum configuration asset that will create and start Quantum systems in a data-driven way when starting the simulation.
  /// Can be assigned to <see cref="RuntimeConfig"/>. 
  /// If no config is assigned then a default selection of build-in systems is used (<see cref="DeterministicSystemSetup.CreateSystems(RuntimeConfig, SimulationConfig, SystemsConfig)"/>.
  /// The systems to be used can always be changed by code inside <see cref="DeterministicSystemSetup.AddSystemsUser(ICollection{SystemBase}, RuntimeConfig, SimulationConfig, SystemsConfig)"/>.
  /// </summary>
  [Serializable]
#if QUANTUM_UNITY
  [UnityEngine.CreateAssetMenu(menuName = "Quantum/Configurations/SystemsConfig", order = -897)]
#endif
  public partial class SystemsConfig : AssetObject {

    /// <summary>
    /// System that will be instantiated on simulation start.
    /// </summary>
    [Serializable]
    public abstract class SystemEntryBase {
      /// <summary>
      /// System type name. Use typeof(SystemBase).FullName to get a valid name programmatically. E.g. Quantum.Core.SystemSignalsOnly.
      /// </summary>
      public SerializableType<SystemBase> SystemType;
      /// <summary>
      /// Optional System name. If set, then the SystemType class needs to have a matching constructor.
      /// </summary>
      [HideInInspector]
      [Obsolete("Name is not used anymore")]
      public string SystemName;
      /// <summary>
      /// Start system disabled.
      /// Set <see cref="SystemBase.StartEnabled"/> accordingly. The value is inversed to have a better default value in Unity inspectors.
      /// </summary>
      public bool StartDisabled;
      /// <summary>
      /// Returns child system list if any.
      /// </summary>
      /// <returns>List of child systems</returns>
      public abstract IReadOnlyList<SystemEntryBase> GetChildren();
    }

    /// <summary>
    /// To prevent indefinite recursion in Unity serialization system hierarchies are limited to 3 levels.
    /// </summary>
    /// <typeparam name="T">Type of the children</typeparam>
    public abstract class SystemEntryBase<T> : SystemEntryBase where T : SystemEntryBase, new() {
      /// <summary>
      /// Child systems.
      /// </summary>
      public List<T> Children = new List<T>();
      /// <summary>
      /// Return child systems.
      /// </summary>
      /// <returns>List of child systems</returns>
      public override IReadOnlyList<SystemEntryBase> GetChildren() => Children;
      
      /// <summary>
      /// Add a child system.
      /// </summary>
      /// <typeparam name="TSystem">System type</typeparam>
      /// <param name="enabled">Start enabled</param>
      /// <returns>The created child system entry</returns>
      public T AddSystem<TSystem>(bool enabled = true) where TSystem : SystemBase {
        var entry = new T() {
          SystemType = typeof(TSystem),
          StartDisabled = !enabled,
        };
        Children.Add(entry);
        return entry;
      }
    }

    /// <summary>
    /// Base system type.
    /// </summary>
    [Serializable]
    public class SystemEntry : SystemEntryBase<SubSystemEntry> {}
    
    /// <summary>
    /// 1st sub level system type.
    /// </summary>
    [Serializable]
    public class SubSystemEntry : SystemEntryBase<SubSubSystemEntry> {}
    
    /// <summary>
    /// 2nd sub level system type.
    /// </summary>
    [Serializable]
    public class SubSubSystemEntry : SystemEntryBase {
      /// <summary>
      /// This system cannot have children.
      /// </summary>
      /// <returns>Empty array</returns>
      public override IReadOnlyList<SystemEntryBase> GetChildren() {
        return Array.Empty<SystemEntryBase>();
      }
    }

    /// <summary>
    /// System entries to be instantiated on simulation start.
    /// </summary>
    public List<SystemEntry> Entries = new();

    /// <summary>
    /// Converts the systems configuration into a list of system objects while calling the matching (Name, Children) constructors.
    /// This method throws AssertionExceptions on any invalid system configuration.
    /// 
    ///                                                SystemBase   
    ///                                            Children[] (SystemBase)
    ///             _______________________________________|______________________________________
    ///            |                    |                  |                  |                  |
    ///     SystemMainThread  SystemArrayComponent  SystemArrayFilter  SystemSignalsOnly  SystemThreadedFilter
    ///            |
    ///  SystemMainThreadFilter
    /// </summary>
    public static List<SystemBase> CreateSystems(SystemsConfig config) {
      Assert.Always(config != null, "SystemsConfig is invalid.");

      var result = new List<SystemBase>();

      for (int i = 0; i < config.Entries.Count; i++) {
        try {
          result.Add(CreateSystems<SystemBase>(config.Entries[i]));
        } catch (Exception e) {
          Log.Error($"Creating system failed from asset '{config.Path}' at index {i} with error: {e.Message}");
        }
      }

      return result;
    }

    private static SystemBase CreateSystems<RequiredBaseType>(SystemEntryBase entry) {
      if (entry.SystemType.AssemblyQualifiedName.Contains(", Quantum.Game, Version")) {
        throw new Exception("The assembly 'Quantum.Game' is not supported anymore, edit the SystemsConfig file and replace 'Quantum.Game' with 'Quantum.Simulation'");
      }

      var type = entry.SystemType.Value;

      Assert.Always(type != null, "SystemType not set");
      Assert.Always(type.IsAbstract == false, "Cannot create abstract SystemType {0}", type);
      Assert.Always(typeof(RequiredBaseType).IsAssignableFrom(type), "System type {0} must be derived from {1}", type, typeof(RequiredBaseType).Name);

      var childrenEntries = entry.GetChildren();
      var children = new List<SystemBase>(childrenEntries.Count);
      for (int i = 0; i < childrenEntries.Count; i++) {
        children.Add(CreateSystems<SystemBase>(childrenEntries[i]) as SystemBase);
      }

      var result = Create(type, children.ToArray());
      result.StartEnabled = !entry.StartDisabled;
      return result;
    }

    private static SystemBase Create<ChildrenType>(Type type, ChildrenType[] children) where ChildrenType : SystemBase {
      var result = default(SystemBase);

      // Conventions and priority are: (children), [Obsolete](name, children), [Obsolete](name), ()
      var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ChildrenType[]) }, null);
      if (constructor != null && constructor.GetCustomAttribute<ObsoleteAttribute>() == null) {
        result = Activator.CreateInstance(type, children) as SystemBase;
      } else {
        constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string), typeof(ChildrenType[]) }, null);
        if (constructor != null && constructor.GetCustomAttribute<ObsoleteAttribute>() == null) {
          result = Activator.CreateInstance(type, null, children) as SystemBase;
        } else {
          constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string) }, null);
          if (constructor != null && constructor.GetCustomAttribute<ObsoleteAttribute>() == null) {
            result = Activator.CreateInstance(type, null) as SystemBase;
          } else {
            Assert.Always(type.GetConstructor(Type.EmptyTypes) != null, "SystemType {0} does not have a default constructor", type);
            result = Activator.CreateInstance(type) as SystemBase;
          }
        }
      }

      // If derived systems do not have the constructor convention, still try to set children.
      if (result != null) {
        if (children != null && children.Length > 0 && result.ChildSystems.Count() == 0) {
          result.ChildSystems = children;
        }
      }

      return result;
    }

    /// <summary>
    /// Add a system entry that describes a system to be instantiated on simulation start.
    /// </summary>
    /// <param name="enabled">System starts enabled</param>
    /// <returns>System entry that was added to the config</returns>
    public SystemEntry AddSystem<T>(bool enabled = true) where T : SystemBase {
      return AddSystem(typeof(T), enabled);
    }

    /// <summary>
    /// Add a system entry that describes a system to be instantiated on simulation start.
    /// </summary>
    /// <param name="systemType">System type</param>
    /// <param name="enabled">System starts enabled</param>
    /// <returns>System entry that was added to the config</returns>
    /// <exception cref="ArgumentNullException">Is raised of the systemType is null</exception>
    public SystemEntry AddSystem(Type systemType, bool enabled = true) {
      if (systemType == null) throw new ArgumentNullException(nameof(systemType));

      var entry = new SystemEntry() {
        SystemType = systemType,
        StartDisabled = !enabled,
      };
      Entries.Add(entry);
      return entry;
    }

#if QUANTUM_UNITY
    /// <summary>
    /// Unity Reset() event will add all Quantum core default systems to the asset.
    /// </summary>
    public override void Reset() {
      AddSystem<Core.CullingSystem2D>();
      AddSystem<Core.CullingSystem3D>();
      AddSystem<Core.PhysicsSystem2D>();
      AddSystem<Core.PhysicsSystem3D>();
      AddSystem<Core.NavigationSystem>();
      AddSystem<Core.EntityPrototypeSystem>();
      AddSystem<Core.PlayerConnectedSystem>();
    }
#endif
  }
}