#if !FUSION_DEV

#region Assets/Photon/FusionCodeGen/AssemblyInfo.cs

[assembly: Fusion.NetworkAssemblyIgnore]

#endregion


#region Assets/Photon/FusionCodeGen/ILMacroStruct.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {
  using System;
  using System.Linq;
  using Mono.Cecil.Cil;

  internal struct ILMacroStruct : ILProcessorMacro {
    Action<ILProcessor> generator;
    Instruction[] instructions;
    public static implicit operator ILMacroStruct(Instruction[] instructions) {
      if (instructions == null) {
        throw new ArgumentNullException(nameof(instructions));
      }
      return new ILMacroStruct() {
        instructions = instructions
      };
    }

    public static implicit operator ILMacroStruct(Action<ILProcessor> generator) {
      if (generator == null) {
        throw new ArgumentNullException(nameof(generator));
      }
      return new ILMacroStruct() {
        generator = generator
      };
    }

    public void Emit(ILProcessor il) {
      if (generator != null) {
        generator(il);
      } else {
        foreach (var instruction in instructions) {
          il.Append(instruction);
        }
      }
    }
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaver.Cache.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Runtime.CompilerServices;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;
  using static Fusion.CodeGen.ILWeaverOpCodes;

  partial class ILWeaver {

    private TypeReference MakeFixedBuffer(ILWeaverAssembly asm, int wordCount) {

      FieldDefinition CreateFixedBufferField (ILWeaverAssembly asm, TypeDefinition type, string fieldName, TypeReference elementType, int elementCount) {
        var fixedBufferType = new TypeDefinition("", $"<{fieldName}>e__FixedBuffer", TypeAttributes.SequentialLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.NestedPublic) {
          BaseType = asm.Import(typeof(ValueType)),
          PackingSize = 0,
          ClassSize = elementCount * elementType.GetPrimitiveSize(),
        };
        fixedBufferType.AddAttribute<CompilerGeneratedAttribute>(asm);
        fixedBufferType.AddAttribute<UnsafeValueTypeAttribute>(asm);
        fixedBufferType.AddTo(type);

        var elementField = new FieldDefinition("FixedElementField", FieldAttributes.Private, elementType);
        elementField.AddTo(fixedBufferType);

        var field = new FieldDefinition(fieldName, FieldAttributes.Public, fixedBufferType);
        field.AddAttribute<FixedBufferAttribute, TypeReference, int>(asm, elementType, elementCount);
        field.AddTo(type);

        return field;
      }

      string typeName = $"FixedStorage@{wordCount}";
      var fixedBufferType = asm.CecilAssembly.MainModule.GetType("Fusion.CodeGen", typeName);
      if (fixedBufferType == null) { 
        // fixed buffers could be included directly in structs, but then again it would be impossible to provide a custom drawer;
        // that's why there's this proxy struct
        var storageType = new TypeDefinition("Fusion.CodeGen", typeName,
          TypeAttributes.ExplicitLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.Serializable,
          asm.ValueType);

        storageType.AddTo(asm.CecilAssembly);
        storageType.AddInterface<INetworkStruct>(asm);
        storageType.AddAttribute<NetworkStructWeavedAttribute, int>(asm, wordCount);

        FieldDefinition bufferField;
        if (Allocator.REPLICATE_WORD_SIZE == sizeof(int)) {
          bufferField = CreateFixedBufferField(asm, storageType, $"Data", asm.Import(typeof(int)), wordCount);
          bufferField.Offset = 0;

          // Unity debugger seems to copy only the first element of a buffer,
          // the rest is garbage when inspected; let's add some additional
          // fields to help it
          for (int i = 1; i < wordCount; ++i) {
            var unityDebuggerWorkaroundField = new FieldDefinition($"_{i}", FieldAttributes.Private | FieldAttributes.NotSerialized, asm.Import<int>());
            unityDebuggerWorkaroundField.Offset = Allocator.REPLICATE_WORD_SIZE * i;
            unityDebuggerWorkaroundField.AddTo(storageType);
          }

        }

        fixedBufferType = storageType;
      }
      return fixedBufferType;
    }

    private string TypeNameToIdentifier(TypeReference type, string prefix) {
      string result = type.FullName;
      result = result.Replace("`1", "");
      result = result.Replace("`2", "");
      result = result.Replace("`3", "");
      result = result.Replace(".", "_");
      result = prefix + result;
      return result;
    }

    private TypeDefinition MakeUnitySurrogate(ILWeaverAssembly asm, PropertyDefinition property) {
      var type = property.PropertyType;

      GenericInstanceType baseType;
      string surrogateName;

      TypeReference dataType;
      Instruction initCall = null;

      if (type.IsNetworkDictionary(out var keyType, out var valueType)) {
        keyType = asm.Import(keyType);
        valueType = asm.Import(valueType);
        var keyReaderWriterType = MakeElementReaderWriter(asm, property.DeclaringType, property, keyType);
        var valueReaderWriterType = MakeElementReaderWriter(asm, property.DeclaringType, property, valueType);
        baseType = asm.Import(typeof(Fusion.Internal.UnityDictionarySurrogate<,,,>)).MakeGenericInstanceType(keyType, keyReaderWriterType, valueType, valueReaderWriterType);
        surrogateName = "UnityDictionarySurrogate@" + keyReaderWriterType.Name + "@" + valueReaderWriterType.Name;
        dataType = TypeReferenceRocks.MakeGenericInstanceType(asm.Import(typeof(SerializableDictionary<,>)), keyType, valueType);
        initCall = Call(new GenericInstanceMethod(asm.Import(asm.Import(typeof(SerializableDictionary)).Resolve().GetMethodOrThrow("Create"))) {
          GenericArguments = { keyType, valueType }
        });
      } else if (type.IsNetworkArray(out var elementType)) {
        elementType = asm.Import(elementType);
        var readerWriterType = MakeElementReaderWriter(asm, property.DeclaringType, property, elementType);
        baseType = asm.Import(typeof(Fusion.Internal.UnityArraySurrogate<,>)).MakeGenericInstanceType(elementType, readerWriterType);
        surrogateName = "UnityArraySurrogate@" + readerWriterType.Name;
        dataType = elementType.MakeArrayType();
        initCall = Call(new GenericInstanceMethod(asm.Import(asm.Import(typeof(Array)).Resolve().GetMethodOrThrow("Empty"))) {
          GenericArguments = { elementType }
        });
      } else if (type.IsNetworkList(out elementType)) {
        elementType = asm.Import(elementType);
        var readerWriterType = MakeElementReaderWriter(asm, property.DeclaringType, property, elementType);
        baseType = asm.Import(typeof(Fusion.Internal.UnityLinkedListSurrogate<,>)).MakeGenericInstanceType(elementType, readerWriterType);
        surrogateName = "UnityLinkedListSurrogate@" + readerWriterType.Name;
        dataType = elementType.MakeArrayType();
        initCall = Call(new GenericInstanceMethod(asm.Import(asm.Import(typeof(Array)).Resolve().GetMethodOrThrow("Empty"))) {
          GenericArguments = { elementType }
        });
      } else {
        var readerWriterType = MakeElementReaderWriter(asm, property.DeclaringType, property, property.PropertyType);
        baseType = asm.Import(typeof(Fusion.Internal.UnityValueSurrogate<,>)).MakeGenericInstanceType(property.PropertyType, readerWriterType);
        surrogateName = "UnityValueSurrogate@" + readerWriterType.Name;
        dataType = property.PropertyType;
      }

      int attributesHash = HashCodeUtilities.InitialHash;
      VisitPropertyMovableAttributes(property, (ctor, blob) => {
        attributesHash = HashCodeUtilities.GetHashDeterministic(ctor.FullName, attributesHash);
        attributesHash = HashCodeUtilities.GetHashCodeDeterministic(blob, attributesHash);
      });
      if (attributesHash != HashCodeUtilities.InitialHash) {
        surrogateName += $"@Attributes_0x{attributesHash:X8}";
      }

      var surrogateType = asm.CecilAssembly.MainModule.GetType("Fusion.CodeGen", surrogateName);
      if (surrogateType == null) {
        surrogateType = new TypeDefinition("Fusion.CodeGen", surrogateName,
          TypeAttributes.NotPublic | TypeAttributes.AnsiClass | TypeAttributes.Serializable | TypeAttributes.BeforeFieldInit,
          baseType);

        surrogateType.AddTo(asm.CecilAssembly);
        

        var dataProp = new PropertyDefinition("DataProperty", PropertyAttributes.None, dataType);
        surrogateType.Properties.Add(dataProp);

        var dataField = new FieldDefinition("Data", FieldAttributes.Public, dataType);
        surrogateType.Fields.Add(dataField);

        var getMethod = new MethodDefinition($"get_{dataProp.Name}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, dataType);
        {
          var il = getMethod.Body.GetILProcessor();
          il.Append(Ldarg_0());
          il.Append(Ldfld(dataField));
          il.Append(Ret());
        }
        surrogateType.Methods.Add(getMethod);

        var setMethod = new MethodDefinition($"set_{dataProp.Name}", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual, asm.Void);
        setMethod.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, dataType));
        {
          var il = setMethod.Body.GetILProcessor();
          il.Append(Ldarg_0());
          il.Append(Ldarg_1());
          il.Append(Stfld(dataField));
          il.Append(Ret());
        }
        surrogateType.Methods.Add(setMethod);

        dataProp.GetMethod = getMethod;
        dataProp.SetMethod = setMethod;

        MovePropertyAttributesToBackingField(asm, property, dataField, addSerializeField: false);

        surrogateType.AddDefaultConstructor(il => {
          if (initCall != null) {
            il.Append(Ldarg_0());
            il.Append(initCall);
            il.Append(Stfld(dataField));
          }
        });
      }
      return surrogateType;
    }

    private TypeDefinition MakeElementReaderWriter(ILWeaverAssembly asm, TypeReference declaringType, ICustomAttributeProvider member, TypeReference elementType) {

      elementType = asm.Import(elementType);

      void AddIElementReaderWriterImplementation(ILWeaverAssembly asm, TypeDefinition readerWriterType, ICustomAttributeProvider member, TypeReference elementType, int elementWordCount, bool isExplicit = false) {

        var dataType = asm.Import(typeof(byte*));
        var indexType = asm.Import(typeof(int));
        var interfaceType = asm.Import(typeof(IElementReaderWriter<>)).MakeGenericInstanceType(elementType);

        readerWriterType.Interfaces.Add(new InterfaceImplementation(interfaceType));

        var visibility = isExplicit ? MethodAttributes.Private : MethodAttributes.Public;
        var namePrefix = isExplicit ? $"CodeGen@ElementReaderWriter<{elementType.FullName}>." : "";

        var readMethod = new MethodDefinition($"{namePrefix}Read",
          visibility | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
          elementType);

        readMethod.Parameters.Add(new ParameterDefinition("data", ParameterAttributes.None, dataType));
        readMethod.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, indexType));
        readMethod.AddAttribute<MethodImplAttribute, MethodImplOptions>(asm, MethodImplOptions.AggressiveInlining);
        readMethod.AddTo(readerWriterType);

        var readRefMethod = new MethodDefinition($"{namePrefix}ReadRef",
          visibility | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
          elementType.MakeByReferenceType());

        readRefMethod.Parameters.Add(new ParameterDefinition("data", ParameterAttributes.None, dataType));
        readRefMethod.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, indexType));
        readRefMethod.AddAttribute<MethodImplAttribute, MethodImplOptions>(asm, MethodImplOptions.AggressiveInlining);
        readRefMethod.AddTo(readerWriterType);

        var writeMethod = new MethodDefinition($"{namePrefix}Write",
          visibility | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
          asm.Void);

        writeMethod.Parameters.Add(new ParameterDefinition("data", ParameterAttributes.None, dataType));
        writeMethod.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, indexType));
        writeMethod.Parameters.Add(new ParameterDefinition("val", ParameterAttributes.None, elementType));
        writeMethod.AddAttribute<MethodImplAttribute, MethodImplOptions>(asm, MethodImplOptions.AggressiveInlining);
        writeMethod.AddTo(readerWriterType);

        var getElementWordCountMethod = new MethodDefinition($"{namePrefix}GetElementWordCount",
          visibility | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
          asm.Import(typeof(int)));

        getElementWordCountMethod.AddAttribute<MethodImplAttribute, MethodImplOptions>(asm, MethodImplOptions.AggressiveInlining);

        if (isExplicit) {
          readMethod.Overrides.Add(interfaceType.GetGenericInstanceMethodOrThrow(nameof(IElementReaderWriter<int>.Read)));
          readRefMethod.Overrides.Add(interfaceType.GetGenericInstanceMethodOrThrow(nameof(IElementReaderWriter<int>.ReadRef)));
          writeMethod.Overrides.Add(interfaceType.GetGenericInstanceMethodOrThrow(nameof(IElementReaderWriter<int>.Write)));
          getElementWordCountMethod.Overrides.Add(interfaceType.GetGenericInstanceMethodOrThrow(nameof(IElementReaderWriter<int>.GetElementWordCount)));
        }


        Action<ILProcessor> addressGetter = il => {
          il.Append(Instruction.Create(OpCodes.Ldarg_1));
          il.Append(Instruction.Create(OpCodes.Ldarg_2));
          il.Append(Instruction.Create(OpCodes.Ldc_I4, elementWordCount * Allocator.REPLICATE_WORD_SIZE));
          il.Append(Instruction.Create(OpCodes.Mul));
          il.Append(Instruction.Create(OpCodes.Add));
        };

        EmitRead(asm, readMethod.Body.GetILProcessor(), elementType, readerWriterType, member, addressGetter, emitRet: true);
        EmitRead(asm, readRefMethod.Body.GetILProcessor(), readRefMethod.ReturnType, readerWriterType, member, addressGetter, emitRet: true, throwForNonUnmanagedRefs: true);
        EmitWrite(asm, writeMethod.Body.GetILProcessor(), elementType, readerWriterType, member, addressGetter, OpCodes.Ldarg_3, emitRet: true);

        getElementWordCountMethod.AddTo(readerWriterType);
        {
          var il = getElementWordCountMethod.Body.GetILProcessor();
          il.Append(Ldc_I4(elementWordCount));
          il.Append(Ret());
        }
      }


      var interfaceType = asm.Import(typeof(IElementReaderWriter<>)).MakeGenericInstanceType(elementType);

      var typeInfo = TypeRegistry.GetInfo(elementType);
      
      if (typeInfo.WrapInfo?.NeedsRunner == true) {
        if (!declaringType.Is<NetworkBehaviour>()) {
          throw new ILWeaverException($"{elementType} needs wrapping - such types are only supported as NetworkBehaviour properties.");
        }

        // let's add an interface!
        var behaviour = declaringType.Resolve();
        var wordCount = typeInfo.GetMemberWordCount(member, behaviour);

        if (!behaviour.Interfaces.Any(x => x.InterfaceType.FullName == interfaceType.FullName)) {
          Log.Debug($"Adding interface {behaviour} {interfaceType}");
          AddIElementReaderWriterImplementation(asm, behaviour, (PropertyDefinition)member, elementType, wordCount, isExplicit: true);
        }

        return behaviour;
      } else {
        var readerWriterName = "ReaderWriter@" + elementType.FullName.Replace(".", "_").Replace("/", "__");
        if (typeInfo.IsAccuracySupported && TypeRegistry.TryGetFloatAccuracy(member, out var accuracy)) {
          uint value = BitConverter.ToUInt32(BitConverter.GetBytes(accuracy), 0);
          readerWriterName += $"@Accuracy_0x{value:x}";
        }
        if (typeInfo.IsCapacitySupported && typeInfo.TryGetEffectiveMemberCapacity(member, out int capacity)) {
          readerWriterName += $"@Capacity_{capacity}";
        }

        var readerWriterTypeDef = asm.CecilAssembly.MainModule.GetType("Fusion.CodeGen", readerWriterName);
        
        const string GetInstanceMethodName = "GetInstance";


        if (readerWriterTypeDef == null) { 
          readerWriterTypeDef = new TypeDefinition("Fusion.CodeGen", readerWriterName,
            TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit, asm.ValueType);

          // without this, VS debugger will crash
          readerWriterTypeDef.PackingSize = 0;
          readerWriterTypeDef.ClassSize = 1;

          readerWriterTypeDef.AddTo(asm.CecilAssembly);

          var wordCount = typeInfo.GetMemberWordCount(member, readerWriterTypeDef);
          AddIElementReaderWriterImplementation(asm, readerWriterTypeDef, (PropertyDefinition)member, elementType, wordCount);

          var instanceField = new FieldDefinition("Instance", FieldAttributes.Public | FieldAttributes.Static, interfaceType);
          instanceField.AddTo(readerWriterTypeDef);

          var initializeMethod = new MethodDefinition(GetInstanceMethodName, MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, interfaceType);
          initializeMethod.AddAttribute<MethodImplAttribute, MethodImplOptions>(asm, MethodImplOptions.AggressiveInlining);
          initializeMethod.AddTo(readerWriterTypeDef);

          {
            var il = initializeMethod.Body.GetILProcessor();
            var loadFld = Ldsfld(instanceField);

            var tmpVar = new VariableDefinition(readerWriterTypeDef);
            il.Body.Variables.Add(tmpVar);

            il.Append(Ldsfld(instanceField));
            il.Append(Brtrue_S(loadFld));

            il.Append(Ldloca_S(tmpVar));
            il.Append(Initobj(readerWriterTypeDef));
            il.Append(Ldloc_0());
            il.Append(Box(readerWriterTypeDef));
            il.Append(Stsfld(instanceField));

            il.Append(loadFld);
            il.Append(Ret());
          }
        }

        return readerWriterTypeDef;
      }
    }

    private void EmitElementReaderWriterLoad(ILProcessor il, TypeDefinition readerWriterType) {
      if (readerWriterType.Is<NetworkBehaviour>()) {
        il.Append(Ldarg_0());
      } else {
        var getInstanceMethod = readerWriterType.GetMethodOrThrow("GetInstance");
        il.Append(Call(getInstanceMethod));
      }
    }

    
  }
}
#endif


#endregion


#region Assets/Photon/FusionCodeGen/ILWeaver.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Compilation;
  using UnityEngine;
  using System.Runtime.CompilerServices;
  using static Fusion.CodeGen.ILWeaverOpCodes;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;
  using Mono.Collections.Generic;
  using CompilerAssembly = UnityEditor.Compilation.Assembly;
  using FieldAttributes = Mono.Cecil.FieldAttributes;
  using MethodAttributes = Mono.Cecil.MethodAttributes;
  using ParameterAttributes = Mono.Cecil.ParameterAttributes;

  public unsafe partial class ILWeaver {

    Dictionary<TypeReference, int> _rpcCount = new Dictionary<TypeReference, int>(new MemberReferenceFullNameComparer());


    internal readonly ILWeaverLog Log;
    internal readonly ILWeaverSettings Settings;

    internal ILWeaver(ILWeaverSettings settings, ILWeaverLog log) {
      if (log == null) {
        throw new ArgumentNullException(nameof(log));
      }
      Log = log;
      Settings = settings;
    }

    public ILWeaver(ILWeaverSettings settings, ILWeaverLogger logger) : this(settings, new ILWeaverLog(logger)) {
    }

    private void EnsureTypeRegistry(ILWeaverAssembly asm) {
      if (TypeRegistry == null) {
        TypeRegistry = new NetworkTypeInfoRegistry(asm.CecilAssembly.MainModule, Settings, Log.Logger, typeRef => CalculateStructWordCount(typeRef));
      }
    }

    void InjectPtrNullCheck(ILWeaverAssembly asm, ILProcessor il, PropertyDefinition property) {
      if (Settings.NullChecksForNetworkedProperties) {
        var nop = Instruction.Create(OpCodes.Nop);

        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Ldfld, asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.Ptr))));
        il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(Instruction.Create(OpCodes.Conv_U));
        il.Append(Instruction.Create(OpCodes.Ceq));
        il.Append(Instruction.Create(OpCodes.Brfalse, nop));

        var ctor = typeof(InvalidOperationException).GetConstructors().First(x => x.GetParameters().Length == 1);
        var exnCtor = asm.Import(ctor);

        il.Append(Instruction.Create(OpCodes.Ldstr, $"Error when accessing {property.DeclaringType.Name}.{property.Name}. Networked properties can only be accessed when Spawned() has been called."));
        il.Append(Instruction.Create(OpCodes.Newobj, exnCtor));
        il.Append(Instruction.Create(OpCodes.Throw));
        il.Append(nop);
      }
    }

    void EmitRead(ILWeaverAssembly asm, ILProcessor il, PropertyDefinition property, Action<ILProcessor> addressGetter) {
      EmitRead(asm, il, property.PropertyType, property.DeclaringType, property, addressGetter, true);
    }

    void EmitRead(ILWeaverAssembly asm, ILProcessor il, TypeReference type, TypeReference declaringType, ICustomAttributeProvider member, Action<ILProcessor> addressGetter, bool emitRet = false, bool throwForNonUnmanagedRefs = false) {
      // for pointer types we can simply just return the address we loaded on the stack
      if (type.IsPointer || type.IsByReference) {
        // load address
        if (throwForNonUnmanagedRefs == false || TypeRegistry.GetInfo(type.GetElementTypeWithGenerics()).IsTriviallyCopyable(member)) {
          addressGetter(il);
        } else {
          il.Append(Ldstr($"Only supported for trivially copyable types. {type.GetElementTypeWithGenerics()} is not trivially copyable."));
          il.Append(Newobj(asm.Import(typeof(NotSupportedException).GetConstructor(new[] { typeof(string) }))));
          il.Append(Throw());
          return;
        }
      } else {
        using (var ctx = new MethodContext(asm, il.Body.Method, addressGetter: addressGetter)) {
          ctx.LoadElementReaderWriterImpl = (il, type, member) => {
            EmitElementReaderWriterLoad(il, MakeElementReaderWriter(asm, declaringType, member, type));
          };

          TypeRegistry.EmitRead(type, il, ctx, member);
        }
      }

      if (emitRet) {
        il.Append(Ret());
      }
    }

    void EmitWrite(ILWeaverAssembly asm, ILProcessor il, PropertyDefinition property, Action<ILProcessor> addressGetter, OpCode valueOpCode) {
      EmitWrite(asm, il, property.PropertyType, property.DeclaringType, property, addressGetter, valueOpCode, true);
    }

    void EmitWrite(ILWeaverAssembly asm, ILProcessor il, TypeReference type, TypeReference declaringType, ICustomAttributeProvider member, Action<ILProcessor> addressGetter, OpCode valueOpCode, bool emitRet = false) {

      if (type.IsPointer || type.IsByReference) {
        throw new ILWeaverException($"Pointer and reference members are read-only");
      }

      using (var ctx = new MethodContext(asm, il.Body.Method, addressGetter: addressGetter, valueGetter: (il) => il.Append(Instruction.Create(valueOpCode)))) {
        ctx.LoadElementReaderWriterImpl = (il, type, member) => {
          EmitElementReaderWriterLoad(il, MakeElementReaderWriter(asm, declaringType, member, type));
        };
        TypeRegistry.EmitWrite(type, il, ctx, member);
        if (emitRet) {
          il.Append(Ret());
        }
      }
    }

    void ThrowIfPropertyNotEmptyOrCompilerGenerated(PropertyDefinition property) {
      Collection<Instruction> instructions;
      int idx;

      var getter = property.GetMethod;
      var setter = property.SetMethod;

      void ExpectNext(params OpCode[] opCodes) {
        foreach (var opCode in opCodes) {
          // skip nops
          for (; idx < instructions.Count && instructions[idx].OpCode.Equals(OpCodes.Nop); ++idx) {
          }

          if (idx >= instructions.Count) {
            throw new InvalidOperationException($"Expected {opCode}, but run out of instructions");
          } else if (!instructions[idx].OpCode.Equals(opCode)) {
            throw new InvalidOperationException($"Expected {opCode}, got {instructions[idx].OpCode} at {idx}. Full IL: {string.Join(", ", instructions)}");
          }
          ++idx;
        }
      }

      if (getter != null && !getter.TryGetAttribute<CompilerGeneratedAttribute>(out _)) {
        instructions = getter.Body.Instructions;
        idx = 0;

        bool expectLocalVariable = false;
        var returnType = getter.ReturnType;

        switch (returnType.MetadataType) {
          case MetadataType.SByte:
          case MetadataType.Byte:
          case MetadataType.Int16:
          case MetadataType.UInt16:
          case MetadataType.Int32:
          case MetadataType.UInt32:
          case MetadataType.Boolean:
          case MetadataType.Char:
            ExpectNext(OpCodes.Ldc_I4_0);
            break;
          case MetadataType.Int64:
          case MetadataType.UInt64:
            ExpectNext(OpCodes.Ldc_I4_0, OpCodes.Conv_I8);
            break;
          case MetadataType.Single:
            ExpectNext(OpCodes.Ldc_R4);
            break;
          case MetadataType.Double:
            ExpectNext(OpCodes.Ldc_R8);
            break;
          case MetadataType.String:
          case MetadataType.Object:
            ExpectNext(OpCodes.Ldnull);
            break;
          default:
            expectLocalVariable = true;
            ExpectNext(OpCodes.Ldloca_S, OpCodes.Initobj, OpCodes.Ldloc_0);
            break;
        }

        if (getter.Body.Variables.Count > (expectLocalVariable ? 1 : 0)) {
          if (expectLocalVariable) {
            ExpectNext(OpCodes.Stloc_1, OpCodes.Br_S, OpCodes.Ldloc_1);
          } else {
            ExpectNext(OpCodes.Stloc_0, OpCodes.Br_S, OpCodes.Ldloc_0);
          }
        }

        ExpectNext(OpCodes.Ret);
      }

      if (setter != null && !setter.TryGetAttribute<CompilerGeneratedAttribute>(out _)) {
        instructions = setter.Body.Instructions;
        idx = 0;
        ExpectNext(OpCodes.Ret);
      }
    }

    (MethodDefinition getter, MethodDefinition setter) PreparePropertyForWeaving(PropertyDefinition property) {
     
      var getter = property.GetMethod;
      var setter = property.SetMethod;

      // clear getter
      getter.CustomAttributes.Clear();
      getter.Body.Instructions.Clear();

      // clear setter if it exists
      setter?.CustomAttributes?.Clear();
      setter?.Body?.Instructions?.Clear();

      return (getter, setter);
    }

    struct WeavablePropertyMeta {
      public string DefaultFieldName;
      public FieldDefinition BackingField;
      public bool ReatainIL;
      public string OnChanged;
    } 

    bool IsWeavableProperty(PropertyDefinition property, out WeavablePropertyMeta meta) {
      if (property.TryGetAttribute<NetworkedAttribute>(out var attr) == false) {
        meta = default;
        return false;
      }

      string onChanged;
      attr.TryGetAttributeProperty(nameof(NetworkedAttribute.OnChanged), out onChanged);

      // check getter ... it has to exist
      var getter = property.GetMethod;
      if (getter == null) {
        meta = default;
        return false;
      }

      // check setter ...
      var setter = property.SetMethod;
      if (setter == null) {
        // if it doesn't exist we allow either array or pointer
        if (property.PropertyType.IsByReference == false && property.PropertyType.IsPointer == false && !property.PropertyType.IsNetworkCollection()) {
          throw new ILWeaverException($"Simple properties need a setter.");
        }
      }

      if (getter.IsStatic) {
        throw new ILWeaverException($"Networked properties can't be static.");
      }

      

      // check for backing field ...
      if (property.TryGetBackingField(out var backing)) {
        var il = attr.Properties.FirstOrDefault(x => x.Name == "RetainIL");

        if (il.Argument.Value is bool retainIL && retainIL) {
          meta = new WeavablePropertyMeta() {
            ReatainIL = true,
            OnChanged = onChanged,
          };
          return true;
        }
      }

      meta = new WeavablePropertyMeta() {
        BackingField = backing,
        ReatainIL = false,
        OnChanged = onChanged,
      };

      attr.TryGetAttributeProperty(nameof(NetworkedAttribute.Default), out meta.DefaultFieldName);
      

      return true;
    }



    void ThrowIfNotRpcCompatible(TypeReference type) {
      if (type.IsPointer || type.IsByReference) {
        throw new ArgumentException($"Pointers and references are not supported");
      }

      if (type.IsNetworkCollection()) {
        throw new ArgumentException($"Network collections are not supported");
      }

      NetworkTypeInfo typeInfo;
     
      typeInfo = TypeRegistry.GetInfo(type);
      if (typeInfo.WrapInfo != null) {
        if (typeInfo.WrapInfo.MaxByteCount > 0) {
          // good
        } else {
          ThrowIfNotRpcCompatible(typeInfo.WrapInfo.WrapperType);
        }
      }
    }

    MethodReference GetBaseMethodReference(ILWeaverAssembly asm, MethodDefinition overridingDefinition, TypeReference baseType) {

      var baseMethod = new MethodReference(overridingDefinition.Name, overridingDefinition.ReturnType, baseType) {
        HasThis = overridingDefinition.HasThis,
        ExplicitThis = overridingDefinition.ExplicitThis,
        CallingConvention = overridingDefinition.CallingConvention,
      };

      foreach (var parameter in overridingDefinition.Parameters) {
        baseMethod.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
      }

      if (baseMethod.DeclaringType is GenericInstanceType genericTypeRef) {
        baseMethod = baseMethod.GetCallable(genericTypeRef);
      }

      return asm.Import(baseMethod);
    }

    string InvokerMethodName(string method, Dictionary<string, int> nameCache) {
      nameCache.TryGetValue(method, out var count);
      nameCache[method] = ++count;
      return $"{method}@Invoker{(count == 1 ? "" : count.ToString())}";
    }

    bool HasRpcPrefixOrSuffix(MethodDefinition def) {
      return def.Name.StartsWith("rpc", StringComparison.OrdinalIgnoreCase) || def.Name.EndsWith("rpc", StringComparison.OrdinalIgnoreCase);
    }

    void WeaveRpcs(ILWeaverAssembly asm, TypeDefinition type, bool allowInstanceRpcs = true) {
      // rpc list
      var rpcs = new List<(MethodDefinition, CustomAttribute)>();


      bool hasStaticRpc = false;

      foreach (var rpc in type.Methods) {
        if (rpc.TryGetAttribute<RpcAttribute>(out var attr)) {

          if (HasRpcPrefixOrSuffix(rpc) == false) {
            throw new ILWeaverException($"{rpc}: name needs to start or end with the \"Rpc\" prefix or suffix");
          }

          if (rpc.IsStatic && rpc.Parameters.FirstOrDefault()?.ParameterType.FullName != asm.NetworkRunner.Reference.FullName) {
            throw new ILWeaverException($"{rpc}: Static RPC needs {nameof(NetworkRunner)} as the first parameter");
          }

          hasStaticRpc |= rpc.IsStatic;

          if (!allowInstanceRpcs && !rpc.IsStatic) {
            throw new ILWeaverException($"{rpc}: Instance RPCs not allowed for this type");
          }

          foreach (var parameter in rpc.Parameters) {
            if (rpc.IsStatic && parameter == rpc.Parameters[0]) {
              continue;
            }

            if (IsInvokeOnlyParameter(parameter)) {
              continue;
            }

            var parameterType = parameter.ParameterType.IsArray ? parameter.ParameterType.GetElementTypeWithGenerics() : parameter.ParameterType;

            try {
              ThrowIfNotRpcCompatible(parameterType);
            } catch (Exception ex) {
              throw new ILWeaverException($"{rpc}: parameter {parameter.Name} is not Rpc-compatible.", ex);
            }
          }

          if (!rpc.ReturnType.Is(asm.RpcInvokeInfo) && !rpc.ReturnType.IsVoid()) {
            throw new ILWeaverException($"{rpc}: RPCs can't return a value.");
          }

          rpcs.Add((rpc, attr));
        }
      }

      if (!rpcs.Any()) {
        return;
      }

      int instanceRpcKeys = GetInstanceRpcCount(type.BaseType);

      Dictionary<string, int> invokerNameCounter = new Dictionary<string, int>();

      foreach (var (rpc, attr) in rpcs) {
        int sources;
        int targets;

        if (attr.ConstructorArguments.Count == 2) {
          sources = attr.GetAttributeArgument<int>(0);
          targets = attr.GetAttributeArgument<int>(1);
        } else {
          sources = AuthorityMasks.ALL;
          targets = AuthorityMasks.ALL;
        }

        ParameterDefinition rpcTargetParameter = rpc.Parameters.SingleOrDefault(x => x.HasAttribute<RpcTargetAttribute>());
        if (rpcTargetParameter != null && !rpcTargetParameter.ParameterType.Is<PlayerRef>()) {
          throw new ILWeaverException($"{rpcTargetParameter}: {nameof(RpcTargetAttribute)} can only be used for {nameof(PlayerRef)} type argument");
        }

        attr.TryGetAttributeProperty<bool>(nameof(RpcAttribute.InvokeLocal), out var invokeLocal, defaultValue: true);
        attr.TryGetAttributeProperty<bool>(nameof(RpcAttribute.InvokeResim), out var invokeResim);
        attr.TryGetAttributeProperty<RpcChannel>(nameof(RpcAttribute.Channel), out var channel);
        attr.TryGetAttributeProperty<bool>(nameof(RpcAttribute.TickAligned), out var tickAligned, defaultValue: true);
        attr.TryGetAttributeProperty<RpcHostMode>(nameof(RpcAttribute.HostMode), out var hostMode);

        // rpc key
        int instanceRpcKey = -1;
        var returnsRpcInvokeInfo = rpc.ReturnType.Is(asm.RpcInvokeInfo);


        using (var ctx = new RpcMethodContext(asm, rpc, rpc.IsStatic)) {

          // local variables
          ctx.DataVariable = new VariableDefinition(asm.Import(typeof(byte)).MakePointerType());
          ctx.OffsetVariable = new VariableDefinition(asm.Import(typeof(int)));
          var message = new VariableDefinition(asm.SimulationMessage.Reference.MakePointerType());
          VariableDefinition localAuthorityMask = null;

          rpc.Body.Variables.Add(ctx.DataVariable);
          rpc.Body.Variables.Add(ctx.OffsetVariable);
          rpc.Body.Variables.Add(message);
          rpc.Body.InitLocals = true;

          // get il processes and our jump instruction
          var il = rpc.Body.GetILProcessor();
          var jmp = Nop();
          var inv = Nop();
          var prepareInv = Nop();

          Instruction targetedInvokeLocal = null;


          // instructions for our branch
          var ins = new List<Instruction>();

          if (returnsRpcInvokeInfo) {
            // find local variable that's used for return(default);
            ctx.RpcInvokeInfoVariable = new VariableDefinition(asm.RpcInvokeInfo);
            rpc.Body.Variables.Add(ctx.RpcInvokeInfoVariable);
            ins.Add(Ldloca(ctx.RpcInvokeInfoVariable));
            ins.Add(Initobj(ctx.RpcInvokeInfoVariable.VariableType));

            // fix each ret
            var returns = il.Body.Instructions.Where(x => x.OpCode == OpCodes.Ret).ToList();
            foreach (var retInstruction in returns) {
              // need to pop the original value and load our new one
              il.InsertBefore(retInstruction, Pop());
              il.InsertBefore(retInstruction, Ldloc(ctx.RpcInvokeInfoVariable));
            }
          }

          if (rpc.IsStatic) {
            ins.Add(Ldsfld(asm.NetworkBehaviourUtils.GetField(nameof(NetworkBehaviourUtils.InvokeRpc))));
            ins.Add(Brfalse(jmp));
            ins.Add(Ldc_I4(0));
            ins.Add(Stsfld(asm.NetworkBehaviourUtils.GetField(nameof(NetworkBehaviourUtils.InvokeRpc))));
          } else {
            ins.Add(Ldarg_0());
            ins.Add(Ldfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.InvokeRpc))));
            ins.Add(Brfalse(jmp));
            ins.Add(Ldarg_0());
            ins.Add(Ldc_I4(0));
            ins.Add(Stfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.InvokeRpc))));
          }
          ins.Add(inv);


          // insert instruction into method body
          var prev = rpc.Body.Instructions[0]; //.OpCode == OpCodes.Nop ? rpc.Body.Instructions[1] :  rpc.Body.Instructions[0];

          for (int i = ins.Count - 1; i >= 0; --i) {
            il.InsertBefore(prev, ins[i]);
            prev = ins[i];
          }

          // jump target
          il.Append(jmp);

          

          var returnInstructions = returnsRpcInvokeInfo
            ? new[] { Ldloc(ctx.RpcInvokeInfoVariable), Ret() }
            : new[] { Ret() };

          var ret = returnInstructions.First();

          // check if runner's ok
          if (rpc.IsStatic) {
            il.AppendMacro(ctx.LoadRunner());
            var checkDone = Nop();
            il.Append(Brtrue_S(checkDone));
            il.Append(Ldstr(rpc.Parameters[0].Name));
            il.Append(Newobj(typeof(ArgumentNullException).GetConstructor(asm, 1)));
            il.Append(Throw());
            il.Append(checkDone);
          } else {
            il.Append(Ldarg_0());
            il.Append(Call(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.ThrowIfBehaviourNotInitialized))));
          }

          il.AppendMacro(ctx.SetRpcInvokeInfoStatus(!invokeLocal, RpcLocalInvokeResult.NotInvokableLocally));

          // if we shouldn't invoke during resim
          if (invokeResim == false) {
            var checkDone = Nop();

            il.AppendMacro(ctx.LoadRunner());

            il.Append(Call(asm.NetworkRunner.GetProperty("Stage")));
            il.Append(Ldc_I4((int)SimulationStages.Resimulate));
            il.Append(Bne_Un_S(checkDone));

            il.AppendMacro(ctx.SetRpcInvokeInfoStatus(invokeLocal, RpcLocalInvokeResult.NotInvokableDuringResim));
            il.AppendMacro(ctx.SetRpcInvokeInfoStatus(RpcSendCullResult.NotInvokableDuringResim));
            il.Append(Br(ret));

            il.Append(checkDone);
          }

          if (!rpc.IsStatic) {
            localAuthorityMask = new VariableDefinition(asm.Import(typeof(int)));
            rpc.Body.Variables.Add(localAuthorityMask);
            il.Append(Ldarg_0());
            il.Append(Ldfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.Object))));
            il.Append(Call(asm.NetworkedObject.GetMethod(nameof(NetworkObject.GetLocalAuthorityMask))));
            il.Append(Stloc(localAuthorityMask));
          }

          // check if target is reachable or not
          if (rpcTargetParameter != null) {
            il.AppendMacro(ctx.LoadRunner());

            il.Append(Ldarg(rpcTargetParameter));
            il.Append(Call(asm.NetworkRunner.GetMethod(nameof(NetworkRunner.GetRpcTargetStatus))));
            il.Append(Dup());

            // check for being unreachable
            {
              var done = Nop();
              il.Append(Ldc_I4((int)RpcTargetStatus.Unreachable));
              il.Append(Bne_Un_S(done));
              il.Append(Ldarg(rpcTargetParameter));
              il.Append(Ldstr(rpc.ToString()));
              il.Append(Call(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.NotifyRpcTargetUnreachable))));
              il.Append(Pop()); // pop the GetRpcTargetStatus

              il.AppendMacro(ctx.SetRpcInvokeInfoStatus(invokeLocal, RpcLocalInvokeResult.TagetPlayerIsNotLocal));
              il.AppendMacro(ctx.SetRpcInvokeInfoStatus(RpcSendCullResult.TargetPlayerUnreachable));
              il.Append(Br(ret));

              il.Append(done);
            }

            // check for self
            {
              il.Append(Ldc_I4((int)RpcTargetStatus.Self));
              if (invokeLocal) {
                // straight to the invoke; this will prohibit any sending
                Log.Assert(targetedInvokeLocal == null);
                targetedInvokeLocal = Nop();
                il.Append(Beq(targetedInvokeLocal));
                il.AppendMacro(ctx.SetRpcInvokeInfoStatus(true, RpcLocalInvokeResult.TagetPlayerIsNotLocal));
              } else {
                // will never get called
                var checkDone = Nop();
                il.Append(Bne_Un_S(checkDone));

                if (NetworkRunner.BuildType == NetworkRunner.BuildTypes.Debug) {
                  il.Append(Ldarg(rpcTargetParameter));
                  il.Append(Ldstr(rpc.ToString()));
                  il.Append(Call(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.NotifyLocalTargetedRpcCulled))));
                }

                il.AppendMacro(ctx.SetRpcInvokeInfoStatus(RpcSendCullResult.TargetPlayerIsLocalButRpcIsNotInvokableLocally));
                il.Append(Br(ret));

                il.Append(checkDone);
              }
            }
          }

          // check if sender flags make sense
          if (!rpc.IsStatic) {
            var checkDone = Nop();

            il.Append(Ldloc(localAuthorityMask));
            il.Append(Ldc_I4(sources));
            il.Append(And());
            il.Append(Brtrue_S(checkDone));

            // source is not valid, notify
            il.Append(Ldstr(rpc.ToString()));
            il.Append(Ldarg_0());
            il.Append(Ldfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.Object))));
            il.Append(Ldc_I4(sources));
            il.Append(Call(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.NotifyLocalSimulationNotAllowedToSendRpc))));

            il.AppendMacro(ctx.SetRpcInvokeInfoStatus(invokeLocal, RpcLocalInvokeResult.InsufficientSourceAuthority));
            il.AppendMacro(ctx.SetRpcInvokeInfoStatus(RpcSendCullResult.InsufficientSourceAuthority));

            il.Append(Br(ret));

            il.Append(checkDone);

            if (invokeLocal) {
              // how about the target? does it match only the local client?
              if (targets != 0 && (targets & AuthorityMasks.PROXY) == 0) {
                il.Append(Ldloc(localAuthorityMask));
                il.Append(Ldc_I4(targets));
                il.Append(And());
                il.Append(Ldc_I4(targets));
                il.Append(Beq(prepareInv));
              }
            }
          }

          // check if sending makes sense at all
          var afterSend = Nop();

          // if not targeted (already handled earlier) check if it can be sent at all
          if (rpcTargetParameter == null) {
            var checkDone = Nop();
            il.AppendMacro(ctx.LoadRunner());
            il.Append(Call(asm.NetworkRunner.GetMethod(nameof(NetworkRunner.HasAnyActiveConnections))));
            il.Append(Brtrue(checkDone));
            il.AppendMacro(ctx.SetRpcInvokeInfoStatus(RpcSendCullResult.NoActiveConnections));
            il.Append(Br(afterSend));
            il.Append(checkDone);
          }

          var messageSizeVar = ctx.CreateVariable(asm.Import<int>());

          // create simulation message
          
          il.Append(Ldc_I4(RpcHeader.SIZE));
          il.Append(Stloc(messageSizeVar));


          for (int i = 0; i < rpc.Parameters.Count; ++i) {
            var para = rpc.Parameters[i];

            if (rpc.IsStatic && i == 0) {
              Log.Assert(para.ParameterType.IsSame<NetworkRunner>());
              continue;
            }

            if (IsInvokeOnlyParameter(para)) {
              continue;
            }
            if (para == rpcTargetParameter) {
              continue;
            }

            il.Append(Ldloc(messageSizeVar));

            using (ctx.ValueGetter(il => il.Append(Ldarg(para)))) {
              if (para.ParameterType.IsArray) {
                // do nothing
                EmitRpcArrayByteSize(il, ctx, para, para.ParameterType.GetElementTypeWithGenerics());
              } else {
                TypeRegistry.EmitMaxByteCountWordAligned(para.ParameterType, il, ctx, para);
              }
            }

            il.Append(Add());
            il.Append(Stloc(messageSizeVar));
          }

          il.AppendMacro(ctx.LoadRunner());
          il.Append(Call(asm.NetworkRunner.GetProperty(nameof(NetworkRunner.Simulation))));
          il.Append(Ldloc(messageSizeVar));


          il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.Allocate), 2)));
          il.Append(Stloc(message));

          // get data for messages
          il.Append(Ldloc(message));
          il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.GetData), 1)));
          il.Append(Stloc(ctx.DataVariable));

          // create RpcHeader
          // il.Append(Ldloc(data));

          if (rpc.IsStatic) {
            il.Append(Ldstr(rpc.ToString()));
            il.Append(Call(asm.Import(typeof(NetworkBehaviourUtils).GetMethod(nameof(NetworkBehaviourUtils.GetRpcStaticIndexOrThrow)))));
            il.Append(Call(asm.RpcHeader.GetMethod(nameof(RpcHeader.Create), 1)));
          } else {
            il.Append(Ldarg_0());
            il.Append(Ldfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.Object))));
            il.Append(Ldfld(asm.NetworkedObject.GetField(nameof(NetworkObject.Id))));

            il.Append(Ldarg_0());
            il.Append(Ldfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.ObjectIndex))));

            instanceRpcKey = ++instanceRpcKeys;
            il.Append(Ldc_I4(instanceRpcKey));
            il.Append(Call(asm.RpcHeader.GetMethod(nameof(RpcHeader.Create), 3)));
          }

          il.Append(Ldloc(ctx.DataVariable));
          il.Append(Call(asm.RpcHeader.GetMethod(nameof(RpcHeader.Write))));
          // no need to align this
          //il.AppendMacro(AlignToWordSize());
          //il.Append(Stobj(asm.RpcHeader.Reference));

          //il.Append(Ldc_I4(RpcHeader.SIZE));
          il.Append(Stloc(ctx.OffsetVariable));

          // write parameters
          for (int i = 0; i < rpc.Parameters.Count; ++i) {
            var para = rpc.Parameters[i];

            if (rpc.IsStatic && i == 0) {
              continue;
            }
            if (IsInvokeOnlyParameter(para)) {
              continue;
            }
            if (para == rpcTargetParameter) {
              continue;
            }

            using (ctx.ValueGetter(il => il.Append(Ldarg(para)))) {
              if (para.ParameterType.IsArray) {
                //WeaveRpcArrayInput(asm, ctx, il, para);
                EmitRpcWriteArray(il, ctx, para, para.ParameterType.GetElementTypeWithGenerics());
              } else {
                TypeRegistry.EmitWrite(para.ParameterType, il, ctx, para);
              }
            }
          }

          // update message offset
          il.Append(Ldloc(message));
          il.Append(Ldflda(asm.SimulationMessage.GetField(nameof(SimulationMessage.Offset))));
          il.Append(Ldloc(ctx.OffsetVariable));
          il.Append(Ldc_I4(8));
          il.Append(Mul());
          il.Append(Stind_I4());

          // send message

          il.AppendMacro(ctx.LoadRunner());

          if (rpcTargetParameter != null) {
            il.Append(Ldloc(message));
            il.Append(Ldarg(rpcTargetParameter));
            il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.SetTarget))));
          }

          if (channel == RpcChannel.Unreliable) {
            il.Append(Ldloc(message));
            il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.SetUnreliable))));
          }

          if (!tickAligned) {
            il.Append(Ldloc(message));
            il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.SetNotTickAligned))));
          }

          if (rpc.IsStatic) {
            il.Append(Ldloc(message));
            il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.SetStatic))));
          }

          // send the rpc
          il.Append(Ldloc(message));
          
          if (ctx.RpcInvokeInfoVariable != null) {
            il.Append(Ldloca(ctx.RpcInvokeInfoVariable));
            il.Append(Ldflda(asm.RpcInvokeInfo.GetField(nameof(RpcInvokeInfo.SendResult))));
            il.Append(Call(asm.NetworkRunner.GetMethod(nameof(NetworkRunner.SendRpc), 2)));
          } else {
            il.Append(Call(asm.NetworkRunner.GetMethod(nameof(NetworkRunner.SendRpc), 1)));
          }

          il.AppendMacro(ctx.SetRpcInvokeInfoStatus(RpcSendCullResult.NotCulled));

          il.Append(afterSend);

          // .. hmm
          if (invokeLocal) {

            if (targetedInvokeLocal != null) {
              il.Append(Br(ret));
              il.Append(targetedInvokeLocal);
            }

            if (!rpc.IsStatic) {
              var checkDone = Nop();
              il.Append(Ldloc(localAuthorityMask));
              il.Append(Ldc_I4(targets));
              il.Append(And());
              il.Append(Brtrue_S(checkDone));

              il.AppendMacro(ctx.SetRpcInvokeInfoStatus(true, RpcLocalInvokeResult.InsufficientTargetAuthority));

              il.Append(Br(ret));

              il.Append(checkDone);
            }

            il.Append(prepareInv);

            foreach (var param in rpc.Parameters) {
              if (param.ParameterType.IsSame<RpcInfo>()) {
                // need to fill it now
                il.AppendMacro(ctx.LoadRunner());
                il.Append(Ldc_I4((int)channel));
                il.Append(Ldc_I4((int)hostMode));
                il.Append(Call(asm.RpcInfo.GetMethod(nameof(RpcInfo.FromLocal))));
                il.Append(Starg_S(param));
              }
            }

            il.AppendMacro(ctx.SetRpcInvokeInfoStatus(true, RpcLocalInvokeResult.Invoked));

            // invoke
            il.Append(Br(inv));
          }

          foreach (var instruction in returnInstructions) {
            il.Append(instruction);
          }
        }

        var invoker = new MethodDefinition(InvokerMethodName(rpc.Name, invokerNameCounter), MethodAttributes.Family | MethodAttributes.Static, asm.Import(typeof(void)));
        using (var ctx = new RpcMethodContext(asm, invoker, rpc.IsStatic)) {

          // create invoker delegate
          if (rpc.IsStatic) {
            var runner = new ParameterDefinition("runner", ParameterAttributes.None, asm.NetworkRunner.Reference);
            invoker.Parameters.Add(runner);
          } else {
            var behaviour = new ParameterDefinition("behaviour", ParameterAttributes.None, asm.NetworkedBehaviour.Reference);
            invoker.Parameters.Add(behaviour);
          }
          var message = new ParameterDefinition("message", ParameterAttributes.None, asm.SimulationMessage.Reference.MakePointerType());
          invoker.Parameters.Add(message);

          // add attribute
          if (rpc.IsStatic) {
            Log.Assert(instanceRpcKey < 0);
            invoker.AddAttribute<NetworkRpcStaticWeavedInvokerAttribute, string>(asm, rpc.ToString());
          } else {
            Log.Assert(instanceRpcKey >= 0);
            invoker.AddAttribute<NetworkRpcWeavedInvokerAttribute, int, int, int>(asm, instanceRpcKey, sources, targets);
          }

          invoker.AddAttribute<UnityEngine.Scripting.PreserveAttribute>(asm);

          // put on type
          type.Methods.Add(invoker);

          // local variables
          ctx.DataVariable = new VariableDefinition(asm.Import(typeof(byte)).MakePointerType());
          ctx.OffsetVariable = new VariableDefinition(asm.Import(typeof(int)));
          var parameters = new VariableDefinition[rpc.Parameters.Count];

          for (int i = 0; i < parameters.Length; ++i) {
            invoker.Body.Variables.Add(parameters[i] = new VariableDefinition(rpc.Parameters[i].ParameterType));
          }

          invoker.Body.Variables.Add(ctx.DataVariable);
          invoker.Body.Variables.Add(ctx.OffsetVariable);
          invoker.Body.InitLocals = true;

          var il = invoker.Body.GetILProcessor();

          // grab data from message and store in local
          il.Append(Ldarg_1());
          il.Append(Call(asm.SimulationMessage.GetMethod(nameof(SimulationMessage.GetData), 1)));
          il.Append(Stloc(ctx.DataVariable));

          il.Append(Ldloc(ctx.DataVariable));
          il.Append(Call(asm.RpcHeader.GetMethod(nameof(RpcHeader.ReadSize))));
          il.AppendMacro(ctx.AlignToWordSize());
          il.Append(Stloc(ctx.OffsetVariable));

          for (int i = 0; i < parameters.Length; ++i) {
            var para = parameters[i];

            if (rpc.IsStatic && i == 0) {
              il.Append(Ldarg_0());
              il.Append(Stloc(para));
              continue;
            }

            if (rpcTargetParameter == rpc.Parameters[i]) {
              il.Append(Ldarg_1());
              il.Append(Ldfld(asm.SimulationMessage.GetField(nameof(SimulationMessage.Target))));
              il.Append(Stloc(para));
            } else if (para.VariableType.IsSame<RpcInfo>()) {
              il.AppendMacro(ctx.LoadRunner());
              il.Append(Ldarg_1());
              il.Append(Ldc_I4((int)hostMode));
              il.Append(Call(asm.RpcInfo.GetMethod(nameof(RpcInfo.FromMessage))));
              il.Append(Stloc(para));
            } else if (para.VariableType.IsArray) {
              EmitRpcReadArray(il, ctx, rpc.Parameters[i], para.VariableType.GetElementTypeWithGenerics(), para);
            } else {
              using (ctx.TargetVariableAddr(para)) {
                TypeRegistry.EmitRead(para.VariableType, il, ctx, rpc.Parameters[i]);
                if (!ctx.TargetAddrUsed) {
                  il.Append(Stloc(para));
                }
              }
            }
          }

          if (rpc.IsStatic) {
            il.Append(Ldc_I4(1));
            il.Append(Stsfld(asm.NetworkBehaviourUtils.GetField(nameof(NetworkBehaviour.InvokeRpc))));
          } else {
            il.Append(Ldarg_0());
            il.Append(Ldc_I4(1));
            il.Append(Stfld(asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.InvokeRpc))));
          }

          var callableRpc = rpc.GetCallable();
          if (!rpc.IsStatic) {
            il.Append(Ldarg_0());
            il.Append(Instruction.Create(OpCodes.Castclass, callableRpc.DeclaringType));
          }

          for (int i = 0; i < parameters.Length; ++i) {
            il.Append(Ldloc(parameters[i]));
          }
          il.Append(Call(callableRpc));
          if (returnsRpcInvokeInfo) {
            il.Append(Pop());
          }
          il.Append(Ret());
        }
      }

      {
        Log.Assert(_rpcCount.TryGetValue(type, out int count) == false || count == instanceRpcKeys);
        _rpcCount[type] = instanceRpcKeys;
      }
    }

    private int GetInstanceRpcCount(TypeReference type) {
      if (_rpcCount.TryGetValue(type, out int result)) {
        return result;
      }

      result = 0;

      var typeDef = type.Resolve();
      
      if (typeDef.BaseType != null) {
        result += GetInstanceRpcCount(typeDef.BaseType);
      }

      result += typeDef.GetMethods()
        .Where(x => !x.IsStatic)
        .Where(x => x.HasAttribute<RpcAttribute>())
        .Count();

      _rpcCount.Add(type, result);
      return result;
    }

    private bool IsInvokeOnlyParameter(ParameterDefinition para) {
      if (para.ParameterType.IsSame<RpcInfo>()) {
        return true;
      }
      return false;
    }

    void EmitRpcWriteArray(ILProcessor il, MethodContext context, ICustomAttributeProvider member, TypeReference elementType) {
      // store array length
      il.AppendMacro(context.LoadAddress());
      il.AppendMacro(context.LoadValue());
      il.Append(Ldlen());
      il.Append(Conv_I4());
      il.Append(Stind_I4());
      il.AppendMacro(context.AddOffset(sizeof(int)));

      if (TypeRegistry.GetInfo(elementType).IsTriviallyCopyable(member)) {
        il.AppendMacro(context.LoadAddress());
        il.AppendMacro(context.LoadValue());
        var memCpy = new GenericInstanceMethod(context.Assembly.Native.GetMethod(nameof(Native.CopyFromArray), 2));
        memCpy.GenericArguments.Add(elementType);
        il.Append(Call(memCpy));
        il.AppendMacro(context.AddOffset());
      } else {
        il.AppendMacro(
          context.For(
            start: il => il.Append(Ldc_I4(0)),
            stop: il => {
              il.AppendMacro(context.LoadValue());
              il.Append(Ldlen());
              il.Append(Conv_I4());
            },
            body: (il, i) => {
              using (context.ValueGetter((il, old) => {
                old(il);
                il.Append(Ldloc(i));
                il.Append(Ldelem(elementType));
              })) {
                TypeRegistry.EmitWrite(elementType, il, context, member);
              }
            }
          )
        );
      }
    }

    void EmitRpcReadArray(ILProcessor il, MethodContext context, ICustomAttributeProvider member, TypeReference elementType, VariableDefinition arrayVar) {
      // alloc array
      il.AppendMacro(context.LoadAddress());
      il.Append(Ldind_I4());
      il.Append(Instruction.Create(OpCodes.Newarr, elementType));
      il.Append(Stloc(arrayVar));
      il.AppendMacro(context.AddOffset(sizeof(int)));

      if (TypeRegistry.GetInfo(elementType).IsTriviallyCopyable(member)) {
        il.Append(Ldloc(arrayVar));
        il.AppendMacro(context.LoadAddress());

        var memCpy = new GenericInstanceMethod(context.Assembly.Native.GetMethod(nameof(Native.CopyToArray), 2));
        memCpy.GenericArguments.Add(elementType);
        il.Append(Call(memCpy));
        il.AppendMacro(context.AddOffset());

      } else {
        il.AppendMacro(
          context.For(
            start: il => il.Append(Ldc_I4(0)),
            stop: il => {
              il.Append(Ldloc(arrayVar));
              il.Append(Ldlen());
              il.Append(Conv_I4());
            },
            body: (il, i) => {
              var placeholder = Nop();
              il.Append(placeholder);

              using (context.TargetVariableAddr(arrayVar, i, elementType)) {
                TypeRegistry.EmitRead(elementType, il, context, member);
                if (!context.TargetAddrUsed) {
                  il.InsertAfter(placeholder, Ldloc(i));
                  il.InsertAfter(placeholder, Ldloc(arrayVar));
                  il.Append(Stelem(elementType));
                }
              }
              il.Remove(placeholder);
              
            }
          )
        );
      }
    }

    void EmitRpcArrayByteSize(ILProcessor il, MethodContext context, ICustomAttributeProvider member, TypeReference elementType) {
      var elementTypeData = TypeRegistry.GetInfo(elementType);

      if (elementTypeData.HasDynamicSize) {
        var totalSize = context.CreateVariable(context.Assembly.Import<int>());

        il.Append(Ldc_I4(sizeof(Int32)));
        il.Append(Stloc(totalSize));

        il.AppendMacro(
          context.For(
            start: il => il.Append(Ldc_I4(0)),
            stop: il =>
            {
              il.AppendMacro(context.LoadValue());
              il.Append(Ldlen());
              il.Append(Conv_I4());
            },
            (il, counter) => {
              il.Append(Ldloc(totalSize));

              using (context.ValueGetter((il, old) => {
                old(il);
                il.Append(Ldloc(counter));
                il.Append(Ldelem(elementType));
              })) {
                TypeRegistry.EmitMaxByteCountWordAligned(elementType, il, context, member);
              }

              il.Append(Add());
              il.Append(Stloc(totalSize));
            }
          )
        );

        il.Append(Ldloc(totalSize));

      } else {
        int size = TypeRegistry.GetInfo(elementType).GetMemberByteCount(member, context.Method.DeclaringType);

        // array length
        il.AppendMacro(context.LoadValue());
        il.Append(Ldlen());
        il.Append(Conv_I4());
        il.Append(Ldc_I4(size));
        il.Append(Mul());

        // store length as well
        il.Append(Ldc_I4(sizeof(Int32)));
        il.Append(Add());

        // align
        il.AppendMacro(context.AlignToWordSize());
      }
    }

    void WeaveSimulation(ILWeaverAssembly asm, TypeDefinition type) {
      EnsureTypeRegistry(asm);
      WeaveRpcs(asm, type, allowInstanceRpcs: false);
    }

    public static bool IsFieldOperand(Instruction instruction, FieldDefinition field, TypeReference declaringType) {
      var storeField = instruction.Operand as FieldReference;
      if (storeField == null) {
        return false;
      }
      if (storeField == field) {
        return true;
      }
      if (storeField.Name == field.Name && storeField.DeclaringType.Is(declaringType)) {
        return true;
      }
      return false;
    }

    private Instruction[] GetInlineFieldInit(MethodDefinition constructor, FieldDefinition field, TypeDefinition declaringType) {
      if (field == null) {
        throw new ArgumentNullException(nameof(field));
      }
      if (constructor == null) {
        throw new ArgumentNullException(nameof(constructor));
      }

      var instructions = constructor.Body.Instructions;

      int ldarg0Index = 0;
      for (int i = 0; i < instructions.Count; ++i) {
        var instruction = instructions[i];
        if (instruction.OpCode == OpCodes.Ldarg_0) {
          ldarg0Index = i;
        } else if (instruction.OpCode == OpCodes.Stfld && IsFieldOperand(instruction, field, declaringType)) {
          // regular init
          return instructions.Skip(ldarg0Index).Take(i - ldarg0Index + 1).ToArray();
        } else if (instruction.OpCode == OpCodes.Initobj && instruction.Previous?.OpCode == OpCodes.Ldflda && IsFieldOperand(instruction.Previous, field, declaringType)) {
          // init with default constructor
          return instructions.Skip(ldarg0Index).Take(i - ldarg0Index + 1).ToArray();
        } else if (instruction.IsBaseConstructorCall(constructor.DeclaringType)) {
          // base constructor init
          break;
        }
      }
      return Array.Empty<Instruction>();
    }

    private Instruction[] RemoveInlineFieldInit(TypeDefinition type, FieldDefinition field) {
      var constructors = type.GetConstructors().Where(x => !x.IsStatic);
      if (!constructors.Any()) {
        return Array.Empty<Instruction>();
      }

      var firstConstructor = constructors.First();
      var firstInlineInit = GetInlineFieldInit(firstConstructor, field, type).ToArray();
      if (firstInlineInit.Length != 0) {
        Log.Debug($"Found {field} inline init: {(string.Join("; ", firstInlineInit.Cast<object>()))}");
      }

      foreach (var constructor in constructors.Skip(1)) {
        var otherInlineInit = GetInlineFieldInit(constructor, field, type);
        if (!firstInlineInit.SequenceEqual(otherInlineInit, new InstructionEqualityComparer())) {
          throw new ILWeaverException($"Expect inline init of {field} to be the same in all constructors," +
            $" but there's a difference between {firstConstructor} and {constructor}");
        }
      }

      foreach (var constructor in constructors) {
        Log.Debug($"Removing inline init of {field} from {constructor}");
        var il = constructor.Body.GetILProcessor();
        var otherInlineInit = GetInlineFieldInit(constructor, field, type);
        foreach (var instruction in otherInlineInit.Reverse()) {
          Log.Debug($"Removing {instruction}");
          il.Remove(instruction);
        }
      }

      return firstInlineInit;
    }

    private static bool IsMakeInitializerCall(Instruction instruction) {
      if (instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference method) {
        if (method.DeclaringType.IsSame<NetworkBehaviour>() && method.Name == nameof(NetworkBehaviour.MakeInitializer)) {
          return true;
        }
      }
      return false;
    }

    private static bool IsMakeRefOrMakePtrCall(Instruction instruction) {
      if (instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference method) {
        if (method.DeclaringType.IsSame<NetworkBehaviour>() && (
          method.Name == nameof(NetworkBehaviour.MakeRef) || method.Name == nameof(NetworkBehaviour.MakePtr)
          )) {
          return true;
        }
      }
      return false;
    }

    private void CheckIfMakeInitializerImplicitCast(Instruction instruction) {
      if (instruction.OpCode == OpCodes.Call && (instruction.Operand as MethodReference)?.Name == "op_Implicit") {
        // all good
      } else {
        throw new ILWeaverException($"Expected an implicit cast, got {instruction}");
      }
    }

    private void ReplaceBackingFieldInInlineInit(ILWeaverAssembly asm, FieldDefinition backingField, FieldReference field, ILProcessor il, Instruction[] instructions) {
      bool nextImplicitCast = false;
      foreach (var instruction in instructions) {
        if (nextImplicitCast) {
          CheckIfMakeInitializerImplicitCast(instruction);
          nextImplicitCast = false;
          il.Remove(instruction);
        } else if (IsFieldOperand(instruction, backingField, field.DeclaringType)) {
          instruction.Operand = field;
        } else if (IsMakeInitializerCall(instruction)) {
          // dictionaries need one extra step, if using SerializableDictionary :(
          if (Settings.UseSerializableDictionary && backingField.FieldType.IsNetworkDictionary(out var keyType, out var valueType)) {
            var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.MakeSerializableDictionary)));
            m.GenericArguments.Add(keyType);
            m.GenericArguments.Add(valueType);
            Log.Debug($"Inline init for {field}, replacing {instruction.Operand} with {m}");
            instruction.Operand = m;
          } else {
            // remove the op, it will be fine
            Log.Debug($"Inline init for {field}, removing {instruction}");
            il.Remove(instruction);
          }
          nextImplicitCast = true;
        }
      }
    }

    static IEnumerable<TypeDefinition> AllTypeDefs(TypeDefinition definitions) {
      yield return definitions;

      if (definitions.HasNestedTypes) {
        foreach (var nested in definitions.NestedTypes.SelectMany(AllTypeDefs)) {
          yield return nested;
        }
      }
    }

    static IEnumerable<TypeDefinition> AllTypeDefs(Collection<TypeDefinition> definitions) {
      return definitions.SelectMany(AllTypeDefs);
    }

    public bool Weave(ILWeaverAssembly asm) {
      // if we don't have the weaved assembly attribute, we need to do weaving and insert the attribute
      if (asm.CecilAssembly.HasAttribute<NetworkAssemblyWeavedAttribute>() != false) {
        return false;
      }

      using (Log.ScopeAssembly(asm.CecilAssembly)) {
        // grab main module .. this contains all the types we need
        var module = asm.CecilAssembly.MainModule;
        var moduleAllTypes = AllTypeDefs(module.Types).ToArray();

        // go through all types and check for network behaviours
        foreach (var t in moduleAllTypes) {
          if (t.IsValueType && t.Is<INetworkStruct>()) {
            try {
              WeaveStruct(asm, t);
            } catch (Exception ex) {
              throw new ILWeaverException($"Failed to weave struct {t}", ex);
            }
          }
        }

        foreach (var t in moduleAllTypes) {
          if (t.IsValueType && t.Is<INetworkInput>()) {
            try {
              WeaveInput(asm, t);
            } catch (Exception ex) {
              throw new ILWeaverException($"Failed to weave input {t}", ex);
            }
          }
        }

        foreach (var t in moduleAllTypes) {
          if (t.IsSubclassOf<NetworkBehaviour>()) {
            try {
              WeaveBehaviour(asm, t);
            } catch (Exception ex) {
              throw new ILWeaverException($"Failed to weave behaviour {t}", ex);
            }
          } else if (t.IsSubclassOf<SimulationBehaviour>()) {
            try {
              WeaveSimulation(asm, t);
            } catch (Exception ex) {
              throw new ILWeaverException($"Failed to weave behaviour {t}", ex);
            }
          }
        }

        if (Settings.CheckRpcAttributeUsage) {
          using (Log.Scope("Checking RpcAttribute usage")) {
            foreach (var t in moduleAllTypes) {
              if (t.IsSubclassOf<SimulationBehaviour>()) {
                continue;
              }
              foreach (var method in t.Methods) {
                if (method.TryGetAttribute<RpcAttribute>(out _)) {
                  Log.Warn($"Incorrect {nameof(RpcAttribute)} usage on {method}: only types derived from {nameof(SimulationBehaviour)} and {nameof(NetworkBehaviour)} are supported");
                }
              }
            }
          }
        }

        // only if it was modified
        if (asm.Modified) {
          // add weaved assembly attribute to this assembly
          asm.CecilAssembly.CustomAttributes.Add(new CustomAttribute(typeof(NetworkAssemblyWeavedAttribute).GetConstructor(asm)));
        }

        return asm.Modified;
      }
    }
  }
}
#endif


#endregion


#region Assets/Photon/FusionCodeGen/ILWeaver.INetworkedStruct.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Compilation;
  using UnityEngine;
  using System.Runtime.CompilerServices;
  using static Fusion.CodeGen.ILWeaverOpCodes;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;
  using Mono.Collections.Generic;
  using CompilerAssembly = UnityEditor.Compilation.Assembly;
  using FieldAttributes = Mono.Cecil.FieldAttributes;
  using MethodAttributes = Mono.Cecil.MethodAttributes;
  using ParameterAttributes = Mono.Cecil.ParameterAttributes;

  unsafe partial class ILWeaver {

    const int WordSize = Allocator.REPLICATE_WORD_SIZE;
    NetworkTypeInfoRegistry TypeRegistry;

    public int CalculateStructWordCount(TypeReference typeRef) {
      var type = typeRef.Resolve();
      Log.Assert(type.TryGetAttribute<NetworkStructWeavedAttribute>(out _) == false);

      var wordCount = 0;

      foreach (var property in type.Properties) {
        if (!IsWeavableProperty(property, out var propertyInfo)) {
          continue;
        }

        try {
          wordCount += TypeRegistry.GetPropertyWordCount(property);
        } catch (Exception ex) {
          throw new ILWeaverException($"Failed to get word count of property {property}", ex);
        }
      }

      // figure out word counts for everything
      foreach (var field in type.Fields) {
        // skip statics
        if (field.IsStatic) {
          continue;
        }

        try {
          // increase block count
          wordCount += TypeRegistry.GetTypeWordCount(field.FieldType);
        } catch (Exception ex) {
          throw new ILWeaverException($"Failed to get word count of field {field}", ex);
        }
      }

      return wordCount;
    }

    public bool WeaveInput(ILWeaverAssembly asm, TypeReference typeRef) {
      ILWeaverException.DebugThrowIf(!typeRef.Is<INetworkInput>(), $"Not a {nameof(INetworkInput)}");


      if (typeRef.Module != asm.CecilAssembly.MainModule) {
        throw new ILWeaverException($"Type {typeRef} is not in the main module of assembly {asm.CecilAssembly}");
      }

      var type = typeRef.Resolve();
      if (type.TryGetAttribute<NetworkStructWeavedAttribute>(out _)) {
        return false;
      }

      EnsureTypeRegistry(asm);
      using (Log.ScopeInput(type)) {
        int wordCount = WeaveStructInner(asm, type);
        // add new attribute
        type.AddAttribute<NetworkInputWeavedAttribute, int>(asm, wordCount);
        return true;
      }
    }

    public bool WeaveStruct(ILWeaverAssembly asm, TypeReference typeRef) {
      ILWeaverException.DebugThrowIf(!typeRef.Is<INetworkStruct>(), $"Not a {nameof(INetworkStruct)}");

      if (typeRef.Module != asm.CecilAssembly.MainModule) {
        throw new ILWeaverException($"Type {typeRef} is not in the main module of assembly {asm.CecilAssembly}");
      }

      var type = typeRef.Resolve();
      if (type.TryGetAttribute<NetworkStructWeavedAttribute>(out _)) {
        return false;
      }

      EnsureTypeRegistry(asm);

      using (Log.ScopeStruct(type)) {
        int wordCount = WeaveStructInner(asm, type);
        // add new attribute
        type.AddAttribute<NetworkStructWeavedAttribute, int>(asm, wordCount);
        return true;
      }
    }

    private bool IsTypeAffectedByAccuracy(ILWeaverAssembly asm, TypeReference type) {
      if (type.IsFloat() || type.IsVector2() || type.IsVector3() || type.IsQuaternion()) {
        return true;
      }
      return false;
    }

    int WeaveStructInner(ILWeaverAssembly asm, TypeDefinition type) {

      // flag asm as modified
      asm.Modified = true;

      // set as explicit layout
      type.IsExplicitLayout = true;

      Log.Assert(type.IsValueType);
      Log.Assert(type.Is<INetworkStruct>() || type.Is<INetworkInput>());

      // clear all backing fields
      foreach (var property in type.Properties) {
        if (!IsWeavableProperty(property, out var propertyInfo)) {
          continue;
        }

        try {
          if (TypeRegistry.GetInfo(property.PropertyType).IsTriviallyCopyable(property)) {
            Log.Warn($"Networked property {property} should be replaced with a regular field. For structs, " +
              $"[Networked] attribute should to be applied only on collections, booleans, floats and vectors.");
          }

          int fieldIndex = type.Fields.Count;

          if (propertyInfo.BackingField != null) {
            if (!propertyInfo.BackingField.FieldType.IsValueType) {
              Log.Warn($"Networked property {property} has a backing field that is not a value type. To keep unmanaged status," +
                $" the accessor should follow \"{{ get => default; set {{}} }}\" pattern");
            }

            fieldIndex = type.Fields.IndexOf(propertyInfo.BackingField);
            if (fieldIndex >= 0) {
              type.Fields.RemoveAt(fieldIndex);
            }
          }

          if (Settings.CheckNetworkedPropertiesBeingEmpty) {
            try {
              ThrowIfPropertyNotEmptyOrCompilerGenerated(property);
            } catch (Exception ex) {
              Log.Warn($"{property} is not compiler-generated or empty: {ex.Message}");
            }
          }

          var propertyWordCount = TypeRegistry.GetPropertyWordCount(property);

          property.GetMethod?.RemoveAttribute<CompilerGeneratedAttribute>(asm);
          property.SetMethod?.RemoveAttribute<CompilerGeneratedAttribute>(asm);


          var getIL = property.GetMethod.Body.GetILProcessor();
          getIL.Clear();
          getIL.Body.Variables.Clear();

          var setIL = property.SetMethod?.Body.GetILProcessor();
          if (setIL != null) {
            setIL.Clear();
            setIL.Body.Variables.Clear();
          }

          var backingFieldName = $"_{property.Name}";
          var fixedBufferInfo = MakeFixedBuffer(asm, propertyWordCount);
          var surrogateType = MakeUnitySurrogate(asm, property);
          var storageField = new FieldDefinition($"_{property.Name}", FieldAttributes.Private, fixedBufferInfo);

          TypeRegistry.GetInfo(property.PropertyType).TryGetEffectiveMemberCapacity(property, out var capacity);

          var fixedBufferAttribute = storageField.AddAttribute<FixedBufferPropertyAttribute, TypeReference, TypeReference, int>(asm, property.PropertyType, surrogateType, capacity);

          // this attribute should always be used first; depending on Unity version this means different order
          fixedBufferAttribute.Properties.Add(new CustomAttributeNamedArgument(nameof(PropertyAttribute.order), new CustomAttributeArgument(asm.Import<int>(),
#if UNITY_2021_1_OR_NEWER
            -int.MaxValue
#else
            int.MaxValue
#endif      
            )));


          type.Fields.Insert(fieldIndex, storageField);

          // move field attributes, if any
          if (propertyInfo.BackingField != null) {
            MoveBackingFieldAttributes(asm, propertyInfo.BackingField, storageField);
          }
          MovePropertyAttributesToBackingField(asm, property, storageField);

          Action<ILProcessor> addressGetter = il => {
            var m = new GenericInstanceMethod(asm.Native.GetMethod(nameof(Native.ReferenceToPointer)));
            m.GenericArguments.Add(storageField.FieldType);

            il.Append(Ldarg_0());
            il.Append(Ldflda(storageField));

            il.Append(Call(m));
          };

          EmitRead(asm, getIL, property, addressGetter);
          if (setIL != null) {
            EmitWrite(asm, setIL, property, addressGetter, OpCodes.Ldarg_1);
          }
        } catch (Exception ex) {
          throw new ILWeaverException($"Failed to weave property {property}", ex);
        }
      }

      // figure out word counts for everything
      var wordCount = 0;

      foreach (var field in type.Fields) {

        // skip statics
        if (field.IsStatic) {
          continue;
        }

        // set offset 
        field.Offset = wordCount * Allocator.REPLICATE_WORD_SIZE;

        try {
          // increase block count
          wordCount += TypeRegistry.GetInfo(field.FieldType).WordCount;
        } catch (Exception ex) {
          throw new ILWeaverException($"Failed to get word count of field {field}", ex);
        }
      }

      type.PackingSize = 0;
      type.ClassSize = wordCount * Allocator.REPLICATE_WORD_SIZE;
      return wordCount;

    }
  }
}
#endif


#endregion


#region Assets/Photon/FusionCodeGen/ILWeaver.NetworkBehaviour.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using UnityEditor;
  using UnityEditor.Compilation;
  using UnityEngine;
  using System.Runtime.CompilerServices;
  using static Fusion.CodeGen.ILWeaverOpCodes;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;
  using Mono.Collections.Generic;
  using CompilerAssembly = UnityEditor.Compilation.Assembly;
  using FieldAttributes = Mono.Cecil.FieldAttributes;
  using MethodAttributes = Mono.Cecil.MethodAttributes;
  using ParameterAttributes = Mono.Cecil.ParameterAttributes;
  using UnityEngine.Scripting;

  unsafe partial class ILWeaver {

    FieldDefinition AddNetworkBehaviourBackingField(PropertyDefinition property) {

      var fieldType = TypeRegistry.GetInfo(property.PropertyType).GetUnityDefaultFieldType(property);
      var field = new FieldDefinition($"_{property.Name}", FieldAttributes.Private, fieldType);
      return field;
    }

    private void MoveBackingFieldAttributes(ILWeaverAssembly asm, FieldDefinition backingField, FieldDefinition storageField) {
      if (backingField.IsNotSerialized) {
        storageField.IsNotSerialized = true;
      }

      foreach (var attrib in backingField.CustomAttributes) {
        if (attrib.AttributeType.Is<CompilerGeneratedAttribute>() ||
            attrib.AttributeType.Is<System.Diagnostics.DebuggerBrowsableAttribute>()) {
          continue;
        }
        storageField.CustomAttributes.Add(attrib);
      }
    }

    private void VisitPropertyMovableAttributes(PropertyDefinition property, Action<MethodReference, byte[]> onAttribute) {
      foreach (var attribute in property.CustomAttributes) {
        if (attribute.AttributeType.IsSame<NetworkedAttribute>() ||
            attribute.AttributeType.IsSame<NetworkedWeavedAttribute>() ||
            attribute.AttributeType.IsSame<AccuracyAttribute>() ||
            attribute.AttributeType.IsSame<CapacityAttribute>()) {
          continue;
        }

        var attribDef = attribute.AttributeType.Resolve();


        if (attribDef.TryGetAttribute<UnityPropertyAttributeProxyAttribute>(out var proxy)) {
          var attribTypeRef = proxy.GetAttributeArgument<TypeReference>(0);
          var attribTypeDef = attribTypeRef.Resolve();

          if (attribTypeDef.TryGetMatchingConstructor(attribute.Constructor.Resolve(), out var constructor)) {
            onAttribute(constructor, attribute.GetBlob());
          } else {
            Log.Warn($"Failed to find matching constructor of {attribTypeDef} for {attribute.Constructor} (property {property})");
          }
          continue;
        }

        if (attribDef.TryGetAttribute<AttributeUsageAttribute>(out var usage)) {
          var targets = usage.GetAttributeArgument<AttributeTargets>(0);
          if ((targets & AttributeTargets.Field) != AttributeTargets.Field) {
            Log.Debug($"Attribute {attribute.AttributeType} can't be applied on a field, skipping.");
            continue;
          }
        }
        onAttribute(attribute.Constructor, attribute.GetBlob());
      }
    }

    private void MovePropertyAttributesToBackingField(ILWeaverAssembly asm, PropertyDefinition property, FieldDefinition field, bool addSerializeField = true) {
      bool hasNonSerialized = false;

      VisitPropertyMovableAttributes(property, (ctor, blob) => {
        Log.Debug($"Adding {ctor.DeclaringType} to {field}");
        field.CustomAttributes.Add(new CustomAttribute(asm.Import(ctor), blob));
        if (ctor.DeclaringType.IsSame<UnityNonSerializedAttribute>()) {
          Log.Debug($"{field} marked as NonSerialized, SerializeField will not be applied");
          hasNonSerialized = true;
        }
      });

      if (addSerializeField) {
        if (!hasNonSerialized && property.GetMethod.IsPublic) {
          if (field.IsNotSerialized) {
            // prohibited
          } else if (field.HasAttribute<SerializeField>()) {
            // already added
          } else {
            field.AddAttribute<SerializeField>(asm);
          }
        }
      }
    }

    public int GetBehaviourWordCount(ILWeaverAssembly asm, TypeDefinition type) {
      int wordCount = 0;
      while (!type.IsSame<NetworkBehaviour>()) {

        if (type.TryGetAttribute<NetworkBehaviourWeavedAttribute>(out var weavedAttribute)) {
          var result = weavedAttribute.GetAttributeArgument<int>(0);
          if (result > 0) {
            wordCount += result;
          }
          break;
        }

        foreach (var property in type.Properties) {
          if (!IsWeavableProperty(property, out var propertyInfo)) {
            continue;
          }

          wordCount += TypeRegistry.GetPropertyWordCount(property);
        }

        type = type.BaseType.Resolve();
      }

      return wordCount;
    }

    public int WeaveBehaviour(ILWeaverAssembly asm, TypeDefinition type) {
      if (type.IsSame<NetworkBehaviour>()) {
        return 0;
      }

      ILWeaverException.DebugThrowIf(!type.IsSubclassOf<NetworkBehaviour>(), $"Not a {nameof(NetworkBehaviour)}");

      if (type.Module != asm.CecilAssembly.MainModule) {
        throw new ILWeaverException($"Type {type} is not in the main module of assembly {asm.CecilAssembly}");
      }

      if (type.TryGetAttribute<NetworkBehaviourWeavedAttribute>(out var weavedAttribute)) {
        var result = weavedAttribute.GetAttributeArgument<int>(0);
        if (result < 0) {
          return 0;
        }
        return result;
      }

      EnsureTypeRegistry(asm);

      // flag as modified
      asm.Modified = true;

      using (Log.ScopeBehaviour(type)) {

        var changed = asm.Import(typeof(Changed<>)).MakeGenericInstanceType(type);
        var changedDelegate = asm.Import(typeof(ChangedDelegate<>)).MakeGenericInstanceType(type);
        var networkBehaviourCallbacks = asm.Import(typeof(NetworkBehaviourCallbacks<>)).MakeGenericInstanceType(type);
        type.Fields.Add(new FieldDefinition("$IL2CPP_CHANGED", FieldAttributes.Static, changed));
        type.Fields.Add(new FieldDefinition("$IL2CPP_CHANGED_DELEGATE", FieldAttributes.Static, changedDelegate));
        type.Fields.Add(new FieldDefinition("$IL2CPP_NETWORK_BEHAVIOUR_CALLBACKS", FieldAttributes.Static, networkBehaviourCallbacks));

        // get block count of parent as starting point for ourselves
        var wordCount = GetBehaviourWordCount(asm, type.BaseType.Resolve());

        // this is the data field which holds this behaviours root pointer
        var dataField = asm.NetworkedBehaviour.GetField(nameof(NetworkBehaviour.Ptr));

        // find onspawned method
        Func<string, (MethodDefinition, Instruction)> createOverride = (name) => {

          var rootMethod = asm.NetworkedBehaviour.GetMethod(name);

          var result = type.Methods.FirstOrDefault(x => x.Name == name);
          if (result != null) {
            // need to find the placeholder method
            var placeholderMethodName = asm.NetworkedBehaviour.GetMethod(nameof(NetworkBehaviour.InvokeWeavedCode)).FullName;
            var placeholders = result.Body.Instructions
             .Where(x => x.OpCode == OpCodes.Call && (x.Operand as MethodReference)?.FullName == placeholderMethodName)
             .ToList();
            
            if (placeholders.Count != 1) {
              throw new ILWeaverException($"When overriding {name} in a type with [Networked] properties, make sure to call {placeholderMethodName} exactly once somewhere.");
            }
            
            var baseCalls = result.Body.Instructions
             .Where(x => x.OpCode == OpCodes.Call && (x.Operand as MethodReference)?.Name == name)
             .ToList();

            if (baseCalls.Count == 0 && !type.BaseType.IsSame<NetworkBehaviour>()) {
              throw new ILWeaverException($"When overriding {name} in a type derived from a type derived from {nameof(NetworkBehaviour)}, invoke the base method.");
            }
            
            foreach (var baseCall in baseCalls) {
              var baseMethod = (MethodReference)baseCall.Operand;
              if (!baseMethod.DeclaringType.IsSame(type.BaseType)) {
                Log.Debug($"Changing base declaring type from {baseMethod.DeclaringType} to {type.BaseType}");
                baseCall.Operand = GetBaseMethodReference(asm, result, type.BaseType);
              }
            }

            var placeholder = placeholders[0];
            var il = result.Body.GetILProcessor();

            var jumpTarget = Nop();
            var returnTarget = Nop();

            // this is where to jump after weaved code's done
            il.InsertAfter(placeholder, returnTarget);
            il.InsertAfter(placeholder, Br(jumpTarget));

            il.Append(jumpTarget);
            return (result, Br(returnTarget));
          }

          result = new MethodDefinition(name, MethodAttributes.Public, rootMethod.ReturnType) {
            IsVirtual = true,
            IsHideBySig = true,
            IsReuseSlot = true
          };

          foreach (var parameter in rootMethod.Parameters) {
            result.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
          }

          type.Methods.Add(result);

          // call base method if it exists, unless it is a NB
          var baseType = type.BaseType;
          while (!baseType.IsSame<NetworkBehaviour>()) {
            
            var baseMethod = GetBaseMethodReference(asm, result, baseType);
            var bodyIL     = result.Body.GetILProcessor();

            bodyIL.Append(Instruction.Create(OpCodes.Ldarg_0));

            foreach (var parameter in result.Parameters) {
              bodyIL.Append(Ldarg(parameter));
            }

            bodyIL.Append(Call(baseMethod));
            break;
          }
          
          return (result, Ret());
        };

        var setDefaults = createOverride(nameof(NetworkBehaviour.CopyBackingFieldsToState));
        var getDefaults = createOverride(nameof(NetworkBehaviour.CopyStateToBackingFields));

        FieldDefinition lastAddedFieldWithKnownPosition = null;
        List<FieldDefinition> fieldsWithUncertainPosition = new List<FieldDefinition>();

        foreach (var property in type.Properties) {
          if (!IsWeavableProperty(property, out var propertyInfo)) {
            continue;
          }

          if (!string.IsNullOrEmpty(propertyInfo.OnChanged)) {
            WeaveChangedHandler(asm, property, propertyInfo.OnChanged);
          }



          if (property.PropertyType.IsPointer || property.PropertyType.IsByReference) {
            var elementType = property.PropertyType.GetElementTypeWithGenerics();
            if (IsTypeAffectedByAccuracy(asm, elementType)) {
              Log.Warn($"{property}: if {elementType} type is used in a pointer/reference property, it is no longer affected by accuracy. " +
                $"Consider wrapping it with a struct implementing INetworkStruct and a [Networked] property.");
            } else if (!TypeRegistry.GetInfo(elementType).IsTriviallyCopyable(property)) {
              throw new ILWeaverException($"{property}: type {elementType} can't be used in pointer/reference properties.");
            }
          }


          try {
            // try to maintain fields order
            int backingFieldIndex = type.Fields.Count;
            if (propertyInfo.BackingField != null) {
              backingFieldIndex = type.Fields.IndexOf(propertyInfo.BackingField);
              if (backingFieldIndex >= 0) {
                type.Fields.RemoveAt(backingFieldIndex);
              } else {
                Log.Warn($"Unable to find backing field for {property}");
                backingFieldIndex = type.Fields.Count;
              }
            }

            var readOnlyInit = GetReadOnlyPropertyInitializer(property);

            // prepare getter/setter methods
            if (readOnlyInit == null && Settings.CheckNetworkedPropertiesBeingEmpty) {
              try {
                ThrowIfPropertyNotEmptyOrCompilerGenerated(property);
              } catch (Exception ex){
                Log.Warn($"{property} is not compiler-generated or empty: {ex.Message}");
              }
            }

            var (getter, setter) = PreparePropertyForWeaving(property);
            var getterRef = getter.GetCallable();
            var setterRef = setter?.GetCallable();

            // capture word count in case we re-use the lambda that is created later on ...
            var wordOffset = wordCount;

            var getIL = getter.Body.GetILProcessor();
            var setIL = setter?.Body?.GetILProcessor();

            Action<ILProcessor> addressGetter = il => {
              il.Append(Ldarg_0());
              il.Append(Ldfld(dataField));
              il.Append(Ldc_I4(wordOffset * Allocator.REPLICATE_WORD_SIZE));
              il.Append(Add());
            };

            // emit accessors
            InjectPtrNullCheck(asm, getIL, property);
            EmitRead(asm, getIL, property, addressGetter);

            if (setIL != null) {
              InjectPtrNullCheck(asm, setIL, property);
              EmitWrite(asm, setIL, property, addressGetter, OpCodes.Ldarg_1);
            }

            var propertyWordCount = TypeRegistry.GetPropertyWordCount(property);

            // step up wordcount
            wordCount += propertyWordCount;

            // inject attribute to poll weaver data during runtime
            property.AddAttribute<NetworkedWeavedAttribute, int, int>(asm, wordOffset, propertyWordCount);



            if (property.HasAttribute<UnityNonSerializedAttribute>() || propertyInfo.BackingField?.IsNotSerialized == true) {
              // so the property is not serialized, so there will be no backing field.

              IEnumerable<Instruction> fieldInit = null;
              VariableDefinition[] fieldInitLocalVariables = null;

              if (readOnlyInit?.Instructions.Length > 0) {
                fieldInit = readOnlyInit.Value.Instructions;
                fieldInitLocalVariables = readOnlyInit.Value.Variables;
              } else if (propertyInfo.BackingField != null) {
                fieldInit = RemoveInlineFieldInit(type, propertyInfo.BackingField);
                fieldInitLocalVariables = Array.Empty<VariableDefinition>();
              }

              if (fieldInit?.Any() == true) {
                // need to patch defaults with this, but only during the initial set
                var il = setDefaults.Item1.Body.GetILProcessor();
                var postInit = Nop();
                il.Append(Ldarg_1());
                il.Append(Brfalse(postInit));

                foreach (var loc in fieldInitLocalVariables) {
                  il.Body.Variables.Add(loc);
                }

                if (property.PropertyType.IsPointer ||
                    property.PropertyType.IsByReference) {
                  // load up address
                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                } else if (property.PropertyType.IsNetworkCollection()) {

                  var first = fieldInit.First();
                  // needs some special init
                  Log.AssertMessage(first.OpCode == OpCodes.Ldarg_0, $"Expected Ldarg_0, got: {first}");
                  il.Append(first);
                  il.Append(Call(getterRef));
                  fieldInit = fieldInit.Skip(1);
                }

                bool skipImplicitInitializerCast = false;
                foreach (var instruction in fieldInit) {
                  if (instruction.IsLdlocWithIndex(out var index)) {
                    var repl = Ldloc(fieldInitLocalVariables[index], il.Body.Method);
                    il.Append(repl);
                  } else if (skipImplicitInitializerCast) {
                    skipImplicitInitializerCast = false;
                    CheckIfMakeInitializerImplicitCast(instruction);
                  } else if (instruction.OpCode == OpCodes.Stfld && IsFieldOperand(instruction, propertyInfo.BackingField, type)) {
                    // arrays and dictionaries don't have setters
                    if (property.PropertyType.IsPointer || property.PropertyType.IsByReference) {
                      il.Append(Stind_or_Stobj(property.PropertyType.GetElementTypeWithGenerics()));
                    } else if (property.PropertyType.IsNetworkArray(out var elementType)) {
                      var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.InitializeNetworkArray)));
                      m.GenericArguments.Add(elementType);
                      il.Append(Ldstr(property.Name));
                      il.Append(Call(m));
                    } else if (property.PropertyType.IsNetworkList(out elementType)) {
                      var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.InitializeNetworkList)));
                      m.GenericArguments.Add(elementType);
                      il.Append(Ldstr(property.Name));
                      il.Append(Call(m));
                    } else if (property.PropertyType.IsNetworkDictionary(out var keyType, out var valueType)) {
                      var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.InitializeNetworkDictionary)));
                      m.GenericArguments.Add(TypeReferenceRocks.MakeGenericInstanceType(asm.Import(typeof(Dictionary<,>)), keyType, valueType));
                      m.GenericArguments.Add(keyType);
                      m.GenericArguments.Add(valueType);
                      il.Append(Ldstr(property.Name));
                      il.Append(Call(m));
                    } else {
                      il.Append(Call(setterRef));
                    }
                  } else if (IsMakeInitializerCall(instruction)) {
                    skipImplicitInitializerCast = true;
                  } else {
                    il.Append(instruction);
                  }
                }

                if (property.PropertyType.IsPointer || property.PropertyType.IsByReference) {
                  il.Append(Stind_or_Stobj(property.PropertyType.GetElementTypeWithGenerics()));
                }

                il.Append(postInit);
              }

            } else {

              FieldReference defaultField = null;
              {
                FieldDefinition defaultFieldDef;

                if (string.IsNullOrEmpty(propertyInfo.DefaultFieldName)) {

                  defaultFieldDef = AddNetworkBehaviourBackingField(property);
                  if (propertyInfo.BackingField != null) {
                    type.Fields.Insert(backingFieldIndex, defaultFieldDef);
                    MoveBackingFieldAttributes(asm, propertyInfo.BackingField, defaultFieldDef);

                    if (lastAddedFieldWithKnownPosition == null) {
                      // fixup fields that have been added without knowing their index
                      foreach (var f in fieldsWithUncertainPosition) {
                        type.Fields.Remove(f);
                      }

                      var index = type.Fields.IndexOf(defaultFieldDef);
                      fieldsWithUncertainPosition.Reverse();
                      foreach (var f in fieldsWithUncertainPosition) {
                        type.Fields.Insert(index, f);
                      }
                    }

                    lastAddedFieldWithKnownPosition = defaultFieldDef;

                  } else {
                    if (lastAddedFieldWithKnownPosition == null) {
                      // not sure where to put this... append
                      type.Fields.Add(defaultFieldDef);
                      fieldsWithUncertainPosition.Add(defaultFieldDef);
                    } else {
                      // add after the previous field
                      var index = type.Fields.IndexOf(lastAddedFieldWithKnownPosition);
                      Log.Assert(index >= 0);

                      type.Fields.Insert(index + 1, defaultFieldDef);
                      lastAddedFieldWithKnownPosition = defaultFieldDef;
                    }
                  }
                  MovePropertyAttributesToBackingField(asm, property, defaultFieldDef);
                } else {
                  defaultFieldDef = property.DeclaringType.GetFieldOrThrow(propertyInfo.DefaultFieldName);
                }

                defaultFieldDef.AddAttribute<DefaultForPropertyAttribute, string, int, int>(asm, property.Name, wordOffset, propertyWordCount);
                defaultField = defaultFieldDef.GetLoadable();
              }
              

              // in each constructor, replace inline init, if present
              foreach (var constructor in type.GetConstructors()) {

                if (readOnlyInit?.Instructions.Length > 0) {
                  var il = constructor.Body.GetILProcessor();

                  Instruction before = il.Body.Instructions[0];
                  {
                    // find where to plug in; after last stfld, but before base constructor call
                    for (int i = 0; i < il.Body.Instructions.Count; ++i) {
                      var instruction = il.Body.Instructions[i];
                      if (instruction.IsBaseConstructorCall(type)) {
                        break;
                      } else if (instruction.OpCode == OpCodes.Stfld) {
                        before = il.Body.Instructions[i + 1];
                      }
                    }
                  }

                  // clone variables
                  var (instructions, variables) = CloneInstructions(readOnlyInit.Value.Instructions, readOnlyInit.Value.Variables);

                  foreach (var variable in variables) {
                    il.Body.Variables.Add(variable);
                  }

                  il.InsertBefore(before, Ldarg_0());
                  foreach (var instruction in instructions) {
                    il.InsertBefore(before, instruction);
                  }
                  il.InsertBefore(before, Stfld(defaultField));
                } else if (propertyInfo.BackingField != null) {
                  // remove the inline init, if present
                  var init = GetInlineFieldInit(constructor, propertyInfo.BackingField, type);
                  if (init.Length > 0) {
                    ReplaceBackingFieldInInlineInit(asm, propertyInfo.BackingField, defaultField, constructor.Body.GetILProcessor(), init);
                  }
                }
              }

              

              {
                var il = setDefaults.Item1.Body.GetILProcessor();

                if (property.PropertyType.IsByReference || property.PropertyType.IsPointer) {
                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));

                  il.Append(Ldarg_0());
                  il.Append(Ldfld(defaultField));

                  il.Append(Stind_or_Stobj(property.PropertyType.GetElementTypeWithGenerics()));

                } else if (property.PropertyType.IsNetworkDictionary(out var keyType, out var valueType)) {

                  var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.InitializeNetworkDictionary)));
                  m.GenericArguments.Add(defaultField.FieldType);
                  m.GenericArguments.Add(keyType);
                  m.GenericArguments.Add(valueType);

                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldarg_0());
                  il.Append(Ldfld(defaultField));
                  il.Append(Ldstr(property.Name));
                  il.Append(Call(m));

                } else if (property.PropertyType.IsNetworkArray(out var elementType)) {

                  var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.InitializeNetworkArray)));
                  m.GenericArguments.Add(elementType);
                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldarg_0());
                  il.Append(Ldfld(defaultField));
                  il.Append(Ldstr(property.Name));
                  il.Append(Call(m));

                } else if (property.PropertyType.IsNetworkList(out elementType)) {

                  var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.InitializeNetworkList)));
                  m.GenericArguments.Add(elementType);
                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldarg_0());
                  il.Append(Ldfld(defaultField));
                  il.Append(Ldstr(property.Name));
                  il.Append(Call(m));


                } else {
                  Log.AssertMessage(setter != null, $"{property} expected to have a setter");
                  il.Append(Ldarg_0());
                  il.Append(Ldarg_0());
                  il.Append(Ldfld(defaultField));
                  il.Append(Call(setterRef));
                }
              }

              {
                var il = getDefaults.Item1.Body.GetILProcessor();

                if (property.PropertyType.IsByReference || property.PropertyType.IsPointer) {
                  il.Append(Ldarg_0());
                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldind_or_Ldobj(property.PropertyType.GetElementType()));
                  il.Append(Stfld(defaultField));

                } else if (property.PropertyType.IsNetworkDictionary(out var keyType, out var valueType)) {

                  var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.CopyFromNetworkDictionary)));
                  m.GenericArguments.Add(defaultField.FieldType);
                  m.GenericArguments.Add(keyType);
                  m.GenericArguments.Add(valueType);

                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldarg_0());
                  il.Append(Ldflda(defaultField));
                  il.Append(Call(m));

                } else if (property.PropertyType.IsNetworkArray(out var elementType)) {

                  var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.CopyFromNetworkArray)));
                  m.GenericArguments.Add(elementType);

                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldarg_0());
                  il.Append(Ldflda(defaultField));
                  il.Append(Call(m));

                } else if (property.PropertyType.IsNetworkList(out elementType)) {

                  var m = new GenericInstanceMethod(asm.NetworkBehaviourUtils.GetMethod(nameof(NetworkBehaviourUtils.CopyFromNetworkList)));
                  m.GenericArguments.Add(elementType);

                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Ldarg_0());
                  il.Append(Ldflda(defaultField));
                  il.Append(Call(m));
                } else {
                  Log.AssertMessage(getter != null, $"{property} expected to have a getter");
                  il.Append(Ldarg_0());
                  il.Append(Ldarg_0());
                  il.Append(Call(getterRef));
                  il.Append(Stfld(defaultField));
                }
              }
            }
          } catch (Exception ex) {
            throw new ILWeaverException($"Failed to weave property {property}", ex);
          }
        }

        {
          var (method, instruction) = setDefaults;
          method.Body.GetILProcessor().Append(instruction);
        }
        
        {
          var (method, instruction) = getDefaults;
          method.Body.GetILProcessor().Append(instruction);
        }

        if (wordCount != GetBehaviourWordCount(asm, type)) {
          throw new ILWeaverException($"Failed to weave {type} - word count mismatch {wordCount} vs {GetBehaviourWordCount(asm, type)}");
        }

        // add meta attribute
        type.AddAttribute<NetworkBehaviourWeavedAttribute, int>(asm, wordCount);

        WeaveRpcs(asm, type);
        return wordCount;
      }
    }

    private void WeaveChangedHandler(ILWeaverAssembly asm, PropertyDefinition property, string handlerName) {

      // find the handler
      {
        foreach (var declaringType in property.DeclaringType.GetHierarchy()) {
          var candidates = declaringType.GetMethods()
            .Where(x => x.IsStatic)
            .Where(x => x.Name == handlerName)
            .Where(x => x.HasParameters && x.Parameters.Count == 1)
            .Where(x => {
              var parameterType = x.Parameters[0].ParameterType;
              if (!parameterType.IsGenericInstance) {
                return false;
              }
              var openGenericType = parameterType.GetElementTypeWithGenerics();
              if (!openGenericType.IsSame(typeof(Changed<>))) {
                return false;
              }
              var behaviourType = ((GenericInstanceType)parameterType).GenericArguments[0];
              if (!property.DeclaringType.Is(behaviourType)) {
                if (behaviourType.IsGenericInstance) {
                  return property.DeclaringType.Is(behaviourType.Resolve());
                }
                return false;
              }
              return true;
            })
            .ToList();

          if (candidates.Count > 1) {
            throw new ILWeaverException($"Ambiguous match for OnChanged handler for {property}: {string.Join("; ", candidates)}");
          } else if (candidates.Count == 1) {

            var handler = candidates[0];

            Log.Debug($"OnChanged handler for {property}: {handler}");
            // add preserve attribute, if not added already
            if (!handler.TryGetAttribute<PreserveAttribute>(out _)) {
              handler.AddAttribute<PreserveAttribute>(asm);
              Log.Debug($"Added {nameof(PreserveAttribute)} to {handler}");
            }
            return;
          }
        }
      }

      throw new ILWeaverException($"No match found for OnChanged handler for {property}");
    }

    struct ReadOnlyInitializer {
      public Instruction[] Instructions;
      public VariableDefinition[] Variables;
    }

    private ReadOnlyInitializer? GetReadOnlyPropertyInitializer(PropertyDefinition property) {
      if (property.PropertyType.IsPointer || property.PropertyType.IsByReference) {
        // need to check if there's MakeRef/Ptr before getter gets obliterated 
        var instructions = property.GetMethod.Body.Instructions;


        for (int i = 0; i < instructions.Count; ++i) {
          var instr = instructions[i];
          if (IsMakeRefOrMakePtrCall(instr)) {
            // found it!
            return new ReadOnlyInitializer() {
              Instructions = instructions.Take(i).ToArray(),
              Variables = property.GetMethod.Body.Variables.ToArray()
            };
          }
        }
      }
      return null;
    }

    private (Instruction[], VariableDefinition[]) CloneInstructions(Instruction[] source, VariableDefinition[] sourceVariables) {

      var constructor = typeof(Instruction).GetConstructor(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
        new[] { typeof(OpCode), typeof(object) }, null);

      // shallow copy
      var result = source.Select(x => (Instruction)constructor.Invoke(new[] { x.OpCode, x.Operand }))
        .ToArray();

      var variableMapping = new Dictionary<VariableDefinition, VariableDefinition>();

      // now need to resolve local variables and jump targets
      foreach (var instruction in result) {

        if (instruction.IsLdlocWithIndex(out var locIndex) || instruction.IsStlocWithIndex(out locIndex)) {
          var variable = sourceVariables[locIndex];
          if (!variableMapping.TryGetValue(variable, out var replacement)) {
            replacement = new VariableDefinition(variable.VariableType);
            variableMapping.Add(variable, replacement);
          }
          if (instruction.IsLdlocWithIndex(out _)) {
            instruction.OpCode = OpCodes.Ldloc;
          } else {
            instruction.OpCode = OpCodes.Stloc;
          }
          instruction.Operand = replacement;
        } else if (instruction.Operand is VariableDefinition variable) {
          if (!variableMapping.TryGetValue(variable, out var replacement)) {
            replacement = new VariableDefinition(variable.VariableType);
            variableMapping.Add(variable, replacement);
          }
          instruction.Operand = replacement;
        } else if (instruction.Operand is Instruction target) {
          var targetIndex = Array.IndexOf(source, target);
          Log.Assert(targetIndex >= 0);
          instruction.Operand = result[targetIndex];
        } else if (instruction.Operand is Instruction[] targets) {
          instruction.Operand = targets.Select(x => {
            var targetIndex = Array.IndexOf(source, x);
            Log.Assert(targetIndex >= 0);
            return result[targetIndex];
          });
        } else if (instruction.Operand is ParameterDefinition) {
          throw new NotSupportedException();
        }
      }

      return (result, variableMapping.Values.ToArray());
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverAssembly.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using Assembly = UnityEditor.Compilation.Assembly;

  using Mono.Cecil;

  public class ILWeaverImportedType {
    public Type                 ClrType;
    public ILWeaverAssembly     Assembly;
    public List<TypeDefinition> BaseDefinitions;
    public TypeReference        Reference;

    Dictionary<string, FieldReference>  _fields        = new Dictionary<string, FieldReference>();
    Dictionary<(string, int?), MethodReference> _methods = new Dictionary<(string, int?), MethodReference>();
    Dictionary<string, MethodReference> _propertiesGet = new Dictionary<string, MethodReference>();
    Dictionary<string, MethodReference> _propertiesSet = new Dictionary<string, MethodReference>();

    public static implicit operator TypeReference(ILWeaverImportedType type) => type.Reference;

    public ILWeaverImportedType(ILWeaverAssembly asm, Type type) {
      ClrType    = type;
      Assembly   = asm;

      Type baseType = type;
      BaseDefinitions = new List<TypeDefinition>();
      // Store the type, and each of its base types - so we can later find fields/properties/methods in the base class.
      while (baseType != null) {
        BaseDefinitions.Add(asm.CecilAssembly.MainModule.ImportReference(baseType).Resolve());
        baseType = baseType.BaseType;
      }

      Reference = asm.CecilAssembly.MainModule.ImportReference(BaseDefinitions[0]);
    }

    public FieldReference GetField(string name) {
      bool found = _fields.TryGetValue(name, out var fieldRef);
      if (found == false) {
        for (int i = 0; i < BaseDefinitions.Count; ++i) {
          FieldDefinition typeDef = BaseDefinitions[i].Fields.FirstOrDefault(x => x.Name == name);
          if (typeDef != null) {
            fieldRef = Assembly.CecilAssembly.MainModule.ImportReference(typeDef);
            _fields.Add(name, fieldRef);
            return fieldRef;
          }
        }
      }
      return fieldRef;
    }

    public MethodReference GetProperty(string name) {
      bool found = _propertiesGet.TryGetValue(name, out var methRef);
      if (found == false) {
        for (int i = 0; i < BaseDefinitions.Count; ++i) {
          PropertyDefinition typeDef = BaseDefinitions[i].Properties.FirstOrDefault(x => x.Name == name);
          if (typeDef != null) {
            methRef = Assembly.CecilAssembly.MainModule.ImportReference(typeDef.GetMethod);
            _propertiesGet.Add(name, methRef);
            return methRef;
          }
        }
      }
      return methRef;
    }

    public MethodReference SetProperty(string name) {
      bool found = _propertiesSet.TryGetValue(name, out var methRef);
      if (found == false) {
        for (int i = 0; i < BaseDefinitions.Count; ++i) {
          PropertyDefinition def = BaseDefinitions[i].Properties.FirstOrDefault(x => x.Name == name);
          if (def != null) {
            methRef = Assembly.CecilAssembly.MainModule.ImportReference(def.SetMethod);
            _propertiesSet.Add(name, methRef);
            return methRef;
          }
        }
      }
      return methRef;
    }

    public MethodReference GetMethod(string name, int? argsCount = null, int? genericArgsCount = null) {
      bool found = _methods.TryGetValue((name, argsCount), out var methRef);
      if (found == false) {
        for (int i = 0; i < BaseDefinitions.Count; ++i) {
          
          var typeDef = BaseDefinitions[i].Methods.FirstOrDefault(
            x => x.Name == name &&
            (argsCount.HasValue == false || x.Parameters.Count == argsCount.Value) &&
            (genericArgsCount == null || x.GenericParameters.Count == genericArgsCount.Value));

          if (typeDef != null) {
            methRef = Assembly.CecilAssembly.MainModule.ImportReference(typeDef);
            _methods.Add((name, argsCount), methRef);
            return methRef;
          }
        }
      }
      if (methRef == null) {
        throw new InvalidOperationException($"Not found: {name}");
      }
      return methRef;
    }


    public GenericInstanceMethod GetGenericMethod(string name, int? argsCount = null, params TypeReference[] types) {
      var method  = GetMethod(name, argsCount);
      var generic = new GenericInstanceMethod(method);

      foreach (var t in types) {
        generic.GenericArguments.Add(t);
      }

      return generic;
    }
  }

  public class ILWeaverAssembly : IDisposable {
    public bool         Modified;

    public AssemblyDefinition CecilAssembly;
    public IDisposable        AssemblyResolver;

    ILWeaverImportedType _networkRunner;
    ILWeaverImportedType _readWriteUtils;
    ILWeaverImportedType _nativeUtils;
    ILWeaverImportedType _rpcInfo;
    ILWeaverImportedType _rpcInvokeInfo;
    ILWeaverImportedType _rpcHeader;
    ILWeaverImportedType _networkBehaviourUtils;

    ILWeaverImportedType _simulation;
    ILWeaverImportedType _networkedObject;
    ILWeaverImportedType _networkedObjectId;
    ILWeaverImportedType _networkedBehaviour;
    ILWeaverImportedType _networkedBehaviourId;
    ILWeaverImportedType _simulationBehaviour;
    ILWeaverImportedType _simulationMessage;

    ILWeaverImportedType _object;
    ILWeaverImportedType _valueType;
    ILWeaverImportedType _void;
    ILWeaverImportedType _int;
    ILWeaverImportedType _float;

    Dictionary<Type, TypeReference> _types = new Dictionary<Type, TypeReference>();

    private ILWeaverImportedType MakeImportedType<T>(ref ILWeaverImportedType field) {
      return MakeImportedType(ref field, typeof(T));
    }

    private ILWeaverImportedType MakeImportedType(ref ILWeaverImportedType field, Type type) {
      if (field == null) {
        field = new ILWeaverImportedType(this, type);
      }
      return field;
    }

    public ILWeaverImportedType WordSizedPrimitive => MakeImportedType<int>(ref _int);

    public ILWeaverImportedType Void => MakeImportedType(ref _void, typeof(void));

    public ILWeaverImportedType Object => MakeImportedType<object>(ref _object);

    public ILWeaverImportedType ValueType => MakeImportedType<ValueType>(ref _valueType);

    public ILWeaverImportedType Float => MakeImportedType<float>(ref _float);

    public ILWeaverImportedType NetworkedObject => MakeImportedType<NetworkObject>(ref _networkedObject);

    public ILWeaverImportedType Simulation => MakeImportedType<Simulation>(ref _simulation);

    public ILWeaverImportedType SimulationMessage => MakeImportedType<SimulationMessage>(ref _simulationMessage);

    public ILWeaverImportedType NetworkedBehaviour => MakeImportedType<NetworkBehaviour>(ref _networkedBehaviour);

    public ILWeaverImportedType SimulationBehaviour => MakeImportedType<SimulationBehaviour>(ref _simulationBehaviour);

    public ILWeaverImportedType NetworkId => MakeImportedType<NetworkId>(ref _networkedObjectId);

    public ILWeaverImportedType NetworkedBehaviourId => MakeImportedType<NetworkBehaviourId>(ref _networkedBehaviourId);

    public ILWeaverImportedType NetworkRunner => MakeImportedType<NetworkRunner>(ref _networkRunner);

    public ILWeaverImportedType ReadWriteUtils => MakeImportedType(ref _readWriteUtils, typeof(ReadWriteUtilsForWeaver));
    
    public ILWeaverImportedType Native => MakeImportedType(ref _nativeUtils, typeof(Native));

    public ILWeaverImportedType NetworkBehaviourUtils => MakeImportedType(ref _networkBehaviourUtils, typeof(NetworkBehaviourUtils));

    public ILWeaverImportedType RpcHeader => MakeImportedType<RpcHeader>(ref _rpcHeader);

    public ILWeaverImportedType RpcInfo => MakeImportedType<RpcInfo>(ref _rpcInfo);

    public ILWeaverImportedType RpcInvokeInfo => MakeImportedType<RpcInvokeInfo>(ref _rpcInvokeInfo);

    public TypeReference Import(TypeReference type) {
      return CecilAssembly.MainModule.ImportReference(type);
    }

    public MethodReference Import(MethodInfo method) {
      return CecilAssembly.MainModule.ImportReference(method);
    }

    public MethodReference Import(MethodReference method) {
      return CecilAssembly.MainModule.ImportReference(method);
    }

    public MethodReference Import(ConstructorInfo method) {
      return CecilAssembly.MainModule.ImportReference(method);
    }

    public TypeReference Import(Type type) {
      if (_types.TryGetValue(type, out var reference) == false) {
        _types.Add(type, reference = CecilAssembly.MainModule.ImportReference(type));
      }

      return reference;
    }

    public void Dispose() {
      CecilAssembly?.Dispose();
      AssemblyResolver?.Dispose();
      
      Modified      = false;
      CecilAssembly = null;
    }

    public TypeReference Import<T>() {
      return Import(typeof(T));
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverAssemblyResolver.ILPostProcessor.cs

#if FUSION_WEAVER && FUSION_WEAVER_ILPOSTPROCESSOR && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Mono.Cecil;

  internal class ILWeaverAssemblyResolver : IAssemblyResolver {
    private List<string> _lookInDirectories;
    private Dictionary<string, string> _assemblyNameToPath;
    private Dictionary<string, AssemblyDefinition> _resolvedAssemblies = new Dictionary<string, AssemblyDefinition>();
    private string _compiledAssemblyName;
    private ILWeaverLog _log;

    public AssemblyDefinition WeavedAssembly;

    public ILWeaverAssemblyResolver(ILWeaverLog log, string compiledAssemblyName, string[] references, string[] weavedAssemblies) {
      _log                  = log;
      _compiledAssemblyName = compiledAssemblyName;
      _assemblyNameToPath   = new Dictionary<string, string>();

      foreach (var referencePath in references) {
        var assemblyName = Path.GetFileNameWithoutExtension(referencePath);
        if (_assemblyNameToPath.TryGetValue(assemblyName, out var existingPath)) {
          _log.Warn($"Assembly {assemblyName} (full path: {referencePath}) already referenced by {compiledAssemblyName} at {existingPath}");
        } else {
          _log.Debug($"Adding {assemblyName}->{referencePath}");
          _assemblyNameToPath.Add(assemblyName, referencePath);
        }
      }

      _lookInDirectories = references.Select(x => Path.GetDirectoryName(x)).Distinct().ToList();
    }

    public void Dispose() {
      foreach (var asm in _resolvedAssemblies.Values) {
        asm.Dispose();
      }
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name) {
      return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) {
      {
        if (name.Name == _compiledAssemblyName)
          return WeavedAssembly;

        var path = GetAssemblyPath(name);
        if (string.IsNullOrEmpty(path))
          return null;

        if (_resolvedAssemblies.TryGetValue(path, out var result))
          return result;

        parameters.AssemblyResolver = this;

        var pdb = path + ".pdb";
        if (File.Exists(pdb)) {
          parameters.SymbolStream = CreateAssemblyStream(pdb);
        }

        var assemblyDefinition = AssemblyDefinition.ReadAssembly(CreateAssemblyStream(path), parameters);
        _resolvedAssemblies.Add(path, assemblyDefinition);
        return assemblyDefinition;
      }
    }

    private string GetAssemblyPath(AssemblyNameReference name) {
      if (_assemblyNameToPath.TryGetValue(name.Name, out var path)) {
        return path;
      }

      // fallback for second-order references
      foreach (var parentDir in _lookInDirectories) {
        var fullPath = Path.Combine(parentDir, name.Name + ".dll");
        if (File.Exists(fullPath)) {
          _assemblyNameToPath.Add(name.Name, fullPath);
          return fullPath;
        }
      }

      return null;
    }

    private static MemoryStream CreateAssemblyStream(string fileName) {
      var bytes = File.ReadAllBytes(fileName);
      return new MemoryStream(bytes);
    }
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverAssemblyResolver.UnityEditor.cs

#if FUSION_WEAVER && !FUSION_WEAVER_ILPOSTPROCESSOR && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using Mono.Cecil;
  using CompilerAssembly = UnityEditor.Compilation.Assembly;

  class ILWeaverAssemblyResolver : BaseAssemblyResolver {
    Dictionary<string, ILWeaverAssembly> _assemblies;
    Dictionary<string, ILWeaverAssembly> _assembliesByPath;

    public IEnumerable<ILWeaverAssembly> Assemblies => _assemblies.Values;

    public ILWeaverAssemblyResolver() {
      _assemblies = new Dictionary<string, ILWeaverAssembly>(StringComparer.Ordinal);
      _assembliesByPath = new Dictionary<string, ILWeaverAssembly>();
    }

    public sealed override AssemblyDefinition Resolve(AssemblyNameReference name) {
      if (_assemblies.TryGetValue(name.FullName, out var asm) == false) {
        asm = new ILWeaverAssembly();
        asm.CecilAssembly = base.Resolve(name, ReaderParameters(false, false));

        _assemblies.Add(name.FullName, asm);
      }

      return asm.CecilAssembly;
    }

    public void Clear() {
      _assemblies.Clear();
    }

    public bool Contains(CompilerAssembly compilerAssembly) {
      return _assembliesByPath.ContainsKey(compilerAssembly.outputPath);
    }

    public ILWeaverAssembly AddAssembly(string path, bool readWrite = true, bool readSymbols = true) {
      return AddAssembly(AssemblyDefinition.ReadAssembly(path, ReaderParameters(readWrite, readSymbols)), null);
    }

    public ILWeaverAssembly AddAssembly(CompilerAssembly compilerAssembly, bool readWrite = true, bool readSymbols = true) {
      return AddAssembly(AssemblyDefinition.ReadAssembly(compilerAssembly.outputPath, ReaderParameters(readWrite, readSymbols)), compilerAssembly);
    }

    public ILWeaverAssembly AddAssembly(AssemblyDefinition assembly, CompilerAssembly compilerAssembly) {
      if (assembly == null) {
        throw new ArgumentNullException(nameof(assembly));
      }

      if (_assemblies.TryGetValue(assembly.Name.FullName, out var asm) == false) {
        asm = new ILWeaverAssembly();
        asm.CecilAssembly = assembly;

        _assemblies.Add(assembly.Name.FullName, asm);

        if (compilerAssembly != null) {
          Assert.Always(_assembliesByPath.ContainsKey(compilerAssembly.outputPath) == false);
          _assembliesByPath.Add(compilerAssembly.outputPath, asm);
        }
      }

      return asm;
    }

    protected override void Dispose(bool disposing) {
      foreach (var asm in _assemblies.Values) {
        asm.CecilAssembly?.Dispose();
      }

      _assemblies.Clear();

      base.Dispose(disposing);
    }

    ReaderParameters ReaderParameters(bool readWrite, bool readSymbols) {
      ReaderParameters p;
      p = new ReaderParameters(ReadingMode.Immediate);
      p.ReadWrite = readWrite;
      p.ReadSymbols = readSymbols;
      p.AssemblyResolver = this;
      return p;
    }
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverBindings.ILPostProcessor.cs

#if FUSION_WEAVER_ILPOSTPROCESSOR
namespace Fusion.CodeGen {
  using System;
  using Unity.CompilationPipeline.Common.ILPostProcessing;

#if FUSION_WEAVER
  using System.Collections.Generic;
  using Unity.CompilationPipeline.Common.Diagnostics;
#if FUSION_HAS_MONO_CECIL
  using System.IO;
  using System.Linq;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using System.Reflection;
 
  using System.Runtime.Serialization.Json;
  using System.Xml.Linq;

  class ILWeaverBindings : ILPostProcessor {

    const string ConfigPathCachePath = "Temp/FusionILWeaverConfigPath.txt";
    const string MainAssemblyName = "Assembly-CSharp";
    const string OverrideMethodName = nameof(ILWeaverSettings) + ".OverrideNetworkProjectConfigPath";
    const string UserFileName = "Fusion.CodeGen.User.cs";

    enum ConfigPathSource {
      User,
      PathFile,
      Find
    }

    Lazy<(string, ConfigPathSource)> _configPath;
    Lazy<XDocument> _config;

    public ILWeaverBindings() {
      _configPath = new Lazy<(string, ConfigPathSource)>(() => {

        // try the user-provided path
        var defaultPath = ILWeaverSettings.DefaultConfigPath;
        if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath)) {
          return (defaultPath, ConfigPathSource.User);
        }

        // try the editor-provided path
        if (File.Exists(ConfigPathCachePath)) {
          var path = File.ReadAllText(ConfigPathCachePath);
          if (File.Exists(path)) {
            return (path, ConfigPathSource.PathFile);
          }
        }

        // last resort: grep
        string[] paths = Directory.GetFiles("Assets", "*.fusion", SearchOption.AllDirectories);
        if (paths.Length == 0) {
          throw new InvalidOperationException($"No {nameof(NetworkProjectConfig)} file found (.fusion extension) in {Path.GetFullPath("Assets")}");
        }
        if (paths.Length > 1) {
          throw new InvalidOperationException($"Multiple config files found: {string.Join(", ", paths)}");
        }
        return (paths[0], ConfigPathSource.Find);
      });

      _config = new Lazy<XDocument>(() => {
        string configPath = _configPath.Value.Item1;
        using (var stream = File.OpenRead(configPath)) {
          var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas());
          return XDocument.Load(jsonReader);
        }
      });
    }

    public override ILPostProcessor GetInstance() {
      return this;
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly) {
      // try to load the config
      ILWeaverSettings settings;
      try {
        settings = ReadSettings(_config.Value);
      } catch (Exception ex) {

        string message;
        DiagnosticType messageType = DiagnosticType.Error;
        try {
          var (configPath, source) = _configPath.Value;
          message = $"Failed to load config from \"{configPath}\". ";
          if (source == ConfigPathSource.User) {
            message += $"This is path comes from the default location ({ILWeaverSettings.DefaultConfigPath}). " +
                       $"Implement {OverrideMethodName} in {UserFileName} to override. ";
          } else if (source == ConfigPathSource.PathFile) {
            message += $"The path comes from {ConfigPathCachePath} file that is generated by editor scripts each time compilation starts. " +
                       $"This method is used if the default config ({ILWeaverSettings.DefaultConfigPath}) does not exist. " +
                       $"Implement {OverrideMethodName} in {UserFileName} to override. ";
          } else if (source == ConfigPathSource.Find) {
            message += $"The path comes searching Assets directory for *.fusion files. " +
                       $"This method is used if the default config ({ILWeaverSettings.DefaultConfigPath}) does not exist and " +
                       $"{ConfigPathCachePath} was not properly generated by editor scripts. ";
          }
          message += $"Details: {ex}";
        } catch (Exception configPathEx) {
          message = 
            $"Failed to locate a valid config. " +
            $"The weaver first checks the default location - {ILWeaverSettings.DefaultConfigPath} (implement {OverrideMethodName} in {UserFileName} to override). " +
            $"If the file does not exist, editor-generated {ConfigPathCachePath} is checked (there might be a scenario where weaving is triggered without the editor scripts being compiled yet). " +
            $"If that fails, the weaver searches for *.fusion files in Assets directory. ";
          message += $"Details: {configPathEx}";
          messageType = DiagnosticType.Warning;
        }
        return new ILPostProcessResult(null, new List<DiagnosticMessage>() {
          { 
            new DiagnosticMessage() {
              MessageData = message,
              DiagnosticType = messageType,
            } 
          }
        });
      }

      InMemoryAssembly resultAssembly = null;
      var logger = new ILWeaverLoggerDiagnosticMessages();
      var log = new ILWeaverLog(logger);

      using (log.Scope($"Process {compiledAssembly.Name}")) {

        {
          var (configPath, configSource) = _configPath.Value;
          log.Debug($"Using config at {configPath} (from {configSource})");
          if (compiledAssembly.Name == MainAssemblyName && configSource == ConfigPathSource.Find) {
            log.Warn(
              $"The weaver had to use Directory.GetFiles to locate the config {configPath}. " +
              $"This is potentially slow and might happen if you moved config to a non-standard location and editor scripts did not get the chance to run yet. " +
              $"If you see this message while running in a batch mode, implement {OverrideMethodName} in {UserFileName}.");
          }
        }

        try {
          using (var asm = CreateWeaverAssembly(settings, log, compiledAssembly)) {

            ILWeaver weaver;
            using (log.Scope("Init")) {
              weaver = new ILWeaver(settings, log);
            }

            weaver.Weave(asm);

            if (asm.Modified) {
              var pe  = new MemoryStream();
              var pdb = new MemoryStream();
              var writerParameters = new WriterParameters {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream         = pdb,
                WriteSymbols         = true
              };

              using (log.Scope("Writing")) {
                asm.CecilAssembly.Write(pe, writerParameters);
                resultAssembly = new InMemoryAssembly(pe.ToArray(), pdb.ToArray());
              }
            }
          }
        } catch (Exception ex) {
          log.Error($"Exception thrown when weaving {compiledAssembly.Name}");
          log.Exception(ex);
        }
      }

      logger.FixNewLinesInMessages();
      return new ILPostProcessResult(resultAssembly, logger.Messages);
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly) {

      string[] assembliesToWeave;

      try {
        assembliesToWeave = ReadSettings(_config.Value, full: false).AssembliesToWeave;
      } catch {
        // need to go to the next stage for some assembly, main is good enough
        return compiledAssembly.Name == MainAssemblyName;
      }

      if (!ILWeaverSettings.IsAssemblyWeavable(assembliesToWeave, compiledAssembly.Name)) {
        return false;
      }

      if (!ILWeaverSettings.ContainsRequiredReferences(compiledAssembly.References)) {
        return false;
      }

      return true;
    }


    static ILWeaverAssembly CreateWeaverAssembly(ILWeaverSettings settings, ILWeaverLog log, ICompiledAssembly compiledAssembly) {
      var resolver = new ILWeaverAssemblyResolver(log, compiledAssembly.Name, compiledAssembly.References, settings.AssembliesToWeave);

      var readerParameters = new ReaderParameters {
        SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
        SymbolReaderProvider = new PortablePdbReaderProvider(),
        AssemblyResolver = resolver,
        ReadingMode = ReadingMode.Immediate,
        ReadWrite = true,
        ReadSymbols = true,
        ReflectionImporterProvider = new ReflectionImporterProvider(log)
      };

      var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
      var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

      resolver.WeavedAssembly = assemblyDefinition;

      return new ILWeaverAssembly() {
        CecilAssembly = assemblyDefinition,
        AssemblyResolver = resolver
      };
    }

    static ILWeaverSettings ReadSettings(XDocument config, bool full = true) {

      static XElement GetElementOrThrow(XElement element, string name) {
        var child = element.Element(name);
        if (child == null) {
          throw new InvalidOperationException($"Element {element.Name} does not have child element {name}");
        }
        return child;
      }

      static IEnumerable<XElement> GetElementsOrThrow(XElement element, string name) {
        return GetElementOrThrow(element, name).Elements();
      }

      static float GetElementAsFloat(XElement element, string name) {
        var child = GetElementOrThrow(element, name);
        try {
          return (float)child;
        } catch (Exception ex) {
          throw new InvalidOperationException($"Unable to cast {name} (child of {element.Name}) to float", ex);
        }
      }

      (string[], Accuracy[]) ParseAccuracies(XElement element, string keyName, string valueName) {
        var keys = GetElementsOrThrow(element, keyName).Select(x => x.Value).ToArray();
        var values = GetElementsOrThrow(element, valueName).Select(x => GetElementAsFloat(x, nameof(Accuracy._value)))
                        .Zip(keys, (val, key) => new Accuracy(key, val))
                        .ToArray();
        return (keys, values);
      }

      void SetIfExists(ref bool field, string name) {
        var b = (bool?)config.Root.Element(name);
        if (b != null) {
          field = b.Value;
        }
      }

      var result = new ILWeaverSettings();
      if (full) {
        {
          AccuracyDefaults defaults = new AccuracyDefaults();
          var element = config.Root.Element(nameof(NetworkProjectConfig.AccuracyDefaults));
          if (element != null) {
            {
              var t = ParseAccuracies(element, nameof(AccuracyDefaults.coreKeys), nameof(AccuracyDefaults.coreVals));
              defaults.coreKeys = t.Item1;
              defaults.coreVals = t.Item2;
            }

            {
              var t = ParseAccuracies(element, nameof(AccuracyDefaults.tags), nameof(AccuracyDefaults.values));
              defaults.tags = t.Item1.ToList();
              defaults.values = t.Item2.ToList();
            }
            defaults.RebuildLookup();
          }
          result.AccuracyDefaults = defaults;
        }

        SetIfExists(ref result.NullChecksForNetworkedProperties, nameof(NetworkProjectConfig.NullChecksForNetworkedProperties));
        SetIfExists(ref result.UseSerializableDictionary, nameof(NetworkProjectConfig.UseSerializableDictionary));
        SetIfExists(ref result.CheckRpcAttributeUsage, nameof(NetworkProjectConfig.CheckRpcAttributeUsage));
        SetIfExists(ref result.CheckNetworkedPropertiesBeingEmpty, nameof(NetworkProjectConfig.CheckNetworkedPropertiesBeingEmpty));
      }

      result.AssembliesToWeave = config.Root.Element(nameof(NetworkProjectConfig.AssembliesToWeave))?
          .Elements()
          .Select(x => x.Value)
          .ToArray() ?? Array.Empty<string>();

      return result;
    }

    class ReflectionImporterProvider : IReflectionImporterProvider {
      private ILWeaverLog _log;

      public ReflectionImporterProvider(ILWeaverLog log) {
        _log = log;
      }
      
      public IReflectionImporter GetReflectionImporter(ModuleDefinition module) {
        return new ReflectionImporter(_log, module);
      }
    }

    class ReflectionImporter : DefaultReflectionImporter {
      private ILWeaverLog _log;
      
      public ReflectionImporter(ILWeaverLog log, ModuleDefinition module) : base(module) {
        _log = log;
      }

      public override AssemblyNameReference ImportReference(AssemblyName name) {
        if (name.Name == "System.Private.CoreLib") {
          // seems weaver is run with .net core,  but we need to stick to .net framework
          var candidates = module.AssemblyReferences
            .Where(x => x.Name == "mscorlib" || x.Name == "netstandard")
            .OrderBy(x => x.Name)
            .ThenByDescending(x => x.Version)
            .ToList();

          // in Unity 2020.1 and .NET 4.x mode when building with IL2CPP apparently both mscrolib and netstandard can be loaded
          if (candidates.Count > 0) {
            return candidates[0];
          }
          
          throw new ILWeaverException("Could not locate mscrolib or netstandard assemblies");
        }
        
        return base.ImportReference(name);
      }
    }


  }
#else
  class ILWeaverBindings : ILPostProcessor {
    public override ILPostProcessor GetInstance() {
      return this;
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly) {
      return new ILPostProcessResult(null, new List<DiagnosticMessage>() {
        new DiagnosticMessage() {
          DiagnosticType = DiagnosticType.Warning,
          MessageData = "Mono.Cecil not found, Fusion IL weaving is disabled. Make sure package com.unity.nuget.mono-cecil is installed."
        }
      });
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly) {
      return compiledAssembly.Name == "Assembly-CSharp";
    }
  }
#endif
#else
  class ILWeaverBindings : ILPostProcessor {
    public override ILPostProcessor GetInstance() {
      return this;
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly) {
      throw new NotImplementedException();
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly) {
      return false;
    }
  }
#endif
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverBindings.UnityEditor.cs

#if FUSION_WEAVER && !FUSION_WEAVER_ILPOSTPROCESSOR

namespace Fusion.CodeGen {
#if FUSION_HAS_MONO_CECIL
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using Mono.Cecil;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEditor.Build.Reporting;
  using UnityEditor.Compilation;
  using CompilerAssembly = UnityEditor.Compilation.Assembly;

  class ILWeaverBindings {

    public static bool IsEditorAssemblyPath(string path) {
      return path.Contains("-Editor") || path.Contains(".Editor");
    }

    [UnityEditor.InitializeOnLoadMethod]
    public static void InitializeOnLoad() {
      CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state) {
      var projectConfig = NetworkProjectConfig.Global;

      // exit edit mode means play mode is about to start ...
      if (state == PlayModeStateChange.ExitingEditMode) {
        foreach (var assembly in CompilationPipeline.GetAssemblies()) {
          var name = Path.GetFileNameWithoutExtension(assembly.outputPath);
          if (ILWeaverSettings.IsAssemblyWeavable(projectConfig.AssembliesToWeave, name)) {
            OnCompilationFinished(assembly.outputPath, new CompilerMessage[0]);
          }
        }
      }
    }

    static void OnCompilationFinished(string path, CompilerMessage[] messages) {
#if FUSION_DEV
      Stopwatch sw = Stopwatch.StartNew();
      Log.Debug($"OnCompilationFinished({path})");
#endif

      // never modify editor assemblies
      if (IsEditorAssemblyPath(path)) {
        return;
      }

      var projectConfig = NetworkProjectConfig.Global;
      if (projectConfig != null) {
        // name of assembly on disk
        var name = Path.GetFileNameWithoutExtension(path);
        if (!ILWeaverSettings.IsAssemblyWeavable(projectConfig.AssembliesToWeave, name)) {
          return;
        }
      }

      // errors means we should exit
      if (messages.Any(x => x.type == CompilerMessageType.Error)) {
#if FUSION_DEV
        Log.Error($"Can't execute ILWeaver on {path}, compilation errors exist.");
#endif
        return;
      }

      // grab compiler pipe assembly
      var asm = CompilationPipeline.GetAssemblies().First(x => x.outputPath == path);

      // needs to reference phoenix runtime
      if (ILWeaverSettings.ContainsRequiredReferences(asm.allReferences) == false) {
        return;
      }

      // perform weaving
      try {
        var settings = new ILWeaverSettings() {
          AccuracyDefaults                   = projectConfig.AccuracyDefaults,
          CheckNetworkedPropertiesBeingEmpty = projectConfig.CheckNetworkedPropertiesBeingEmpty,
          CheckRpcAttributeUsage             = projectConfig.CheckRpcAttributeUsage,
          NullChecksForNetworkedProperties   = projectConfig.NullChecksForNetworkedProperties,
          UseSerializableDictionary          = projectConfig.UseSerializableDictionary,
          AssembliesToWeave                  = projectConfig.AssembliesToWeave,
        };

        var weaver = new ILWeaver(settings, new ILWeaverLoggerUnityDebug());
        Weave(weaver, asm);
      } catch (Exception ex) {
        UnityEngine.Debug.LogError(ex);
      }

#if FUSION_DEV
      UnityEngine.Debug.Log($"OnCompilationFinished took: {sw.Elapsed}");
#endif
    }


    static void Weave(ILWeaver weaver, Assembly compilerAssembly) {
      using (weaver.Log.Scope("Processing")) {

        using (var resolver = new ILWeaverAssemblyResolver()) {
          // if we're already weaving this don't do anything
          if (resolver.Contains(compilerAssembly)) {
            return;
          }

          // make sure we can load all dlls
          foreach (string path in compilerAssembly.allReferences) {
            resolver.AddSearchDirectory(Path.GetDirectoryName(path));
          }

          // make sure we have the runtime dll loaded
          if (!ILWeaverSettings.ContainsRequiredReferences(compilerAssembly.allReferences)) {
            throw new InvalidOperationException($"Weaving: Could not find required assembly references");
          }

          ILWeaverAssembly asm;

          using (weaver.Log.Scope("Resolving")) {
            asm = resolver.AddAssembly(compilerAssembly);
          }

          if (weaver.Weave(asm)) {

            using (weaver.Log.Scope("Writing")) {
              // write asm to disk
              asm.CecilAssembly.Write(new WriterParameters {
                WriteSymbols = true
              });
            }
          }
        }
      }
    }
  }
#else
  class ILWeaverBindings {
    [UnityEditor.InitializeOnLoadMethod]
    public static void InitializeOnLoad() {
      UnityEngine.Debug.LogError("Mono.Cecil not found, Fusion IL weaving is disabled. Make sure package com.unity.nuget.mono-cecil is installed.");
    }
  }
#endif
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverException.cs

namespace Fusion.CodeGen {
  using System;
  using System.Diagnostics;

  public class ILWeaverException : Exception {
    public ILWeaverException(string error) : base(error) {
    }

    public ILWeaverException(string error, Exception innerException) : base(error, innerException) {
    }


    [Conditional("UNITY_EDITOR")]
    public static void DebugThrowIf(bool condition, string message) {
      if (condition) {
        throw new ILWeaverException(message);
      }
    }
  }
}

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverExtensions.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Runtime.CompilerServices;
  using System.Text;
  using System.Text.RegularExpressions;
  using System.Threading.Tasks;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;

  public static class ILWeaverExtensions {

    public static bool IsIntegral(this TypeReference type) {
      switch (type.MetadataType) {
        case MetadataType.Byte:
        case MetadataType.SByte:
        case MetadataType.UInt16:
        case MetadataType.Int16:
        case MetadataType.UInt32:
        case MetadataType.Int32:
        case MetadataType.UInt64:
        case MetadataType.Int64:
          return true;
        default:
          return false;
      }
    }

    public static int GetPrimitiveSize(this TypeReference type) {
      switch (type.MetadataType) {
        case MetadataType.Byte:
        case MetadataType.SByte:
          return sizeof(byte);
        case MetadataType.UInt16:
        case MetadataType.Int16:
          return sizeof(short);
        case MetadataType.UInt32:
        case MetadataType.Int32:
          return sizeof(int);
        case MetadataType.UInt64:
        case MetadataType.Int64:
          return sizeof(long);
        case MetadataType.Single:
          return sizeof(float);
        case MetadataType.Double:
          return sizeof(double);
        default:
          throw new ArgumentException($"Unknown primitive type: {type}", nameof(type));
      }
    }

    public static bool IsString(this TypeReference type) {
      return type.MetadataType == MetadataType.String;
    }

    public static bool IsFloat(this TypeReference type) {
      return type.MetadataType == MetadataType.Single;
    }

    public static bool IsBool(this TypeReference type) {
      return type.MetadataType == MetadataType.Boolean;
    }

    public static bool IsVector2(this TypeReference type) {
      return type.FullName == typeof(UnityEngine.Vector2).FullName;
    }

    public static bool IsVector3(this TypeReference type) {
      return type.FullName == typeof(UnityEngine.Vector3).FullName;
    }

    public static bool IsQuaternion(this TypeReference type) {
      return type.FullName == typeof(UnityEngine.Quaternion).FullName;
    }

    public static bool IsVoid(this TypeReference type) {
      return type.MetadataType == MetadataType.Void;
    }

    public static TypeReference GetElementTypeWithGenerics(this TypeReference type) {
      if (type.IsPointer) {
        return ((Mono.Cecil.PointerType)type).ElementType;
      } else if (type.IsByReference) {
        return ((Mono.Cecil.ByReferenceType)type).ElementType;
      } else if (type.IsArray) {
        return ((Mono.Cecil.ArrayType)type).ElementType;
      } else {
        return type.GetElementType();
      }
    }

    public static bool IsSubclassOf<T>(this TypeReference type) {
      return !IsSame<T>(type) && Is<T>(type);
    }

    public static bool IsNetworkCollection(this TypeReference type) {
      return type.IsNetworkArray(out _) || type.IsNetworkList(out _) || type.IsNetworkDictionary(out _, out _);
    }

    public static bool IsNetworkList(this TypeReference type, out TypeReference elementType) {
      if (!type.IsGenericInstance || type.GetElementTypeWithGenerics().FullName != typeof(NetworkLinkedList<>).FullName) {
        elementType = default;
        return false;
      }

      var git = (GenericInstanceType)type;
      elementType = git.GenericArguments[0];
      return true;
    }


    public static bool IsNetworkArray(this TypeReference type, out TypeReference elementType) {
      if (!type.IsGenericInstance || type.GetElementTypeWithGenerics().FullName != typeof(NetworkArray<>).FullName) {
        elementType = default;
        return false;
      }

      var git = (GenericInstanceType)type;
      elementType = git.GenericArguments[0];
      return true;
    }

    public static bool IsNetworkDictionary(this TypeReference type, out TypeReference keyType, out TypeReference valueType) {
      if (!type.IsGenericInstance || type.GetElementTypeWithGenerics().FullName != typeof(NetworkDictionary<,>).FullName) {
        keyType = default;
        valueType = default;
        return false;
      }

      var git = (GenericInstanceType)type;
      keyType = git.GenericArguments[0];
      valueType = git.GenericArguments[1];
      return true;
    }

    public static bool Is<T>(this TypeReference type) {
      return Is(type, typeof(T));
    }

    public static bool Is(this TypeReference type, Type t) {
      if (IsSame(type, t)) {
        return true;
      }

      var resolvedType = type.Resolve();

      if (t.IsInterface) {

        foreach (var interf in resolvedType.Interfaces) {
          if (interf.InterfaceType.IsSame(t)) {
            return true;
          }
        }
        return false;
      } else {
        if (resolvedType.BaseType == null) {
          return false;
        }
        return Is(resolvedType.BaseType, t);
      }
    }

    public static bool Is(this TypeReference type, TypeReference t) {

      if (IsSame(type, t)) {
        return true;
      }

      var resolvedType = type.Resolve();
      if (IsSame(resolvedType, t)) {
        return true;
      }

      if (t is GenericParameter genericParameter) {
        foreach (var constraint in genericParameter.Constraints) {
#if FUSION_CECIL_1_11_OR_NEWER
          if (!Is(type, constraint.ConstraintType)) {
#else
          if (!Is(type, constraint)) {
#endif
            return false;
          }
        }
        return true;
      }

      if (t.Resolve().IsInterface == true) {
        if (resolvedType == null) {
          return false;
        }

        foreach (var interf in resolvedType.Interfaces) {
          if (interf.InterfaceType.IsSame(t)) {
            return true;
          }
        }
        return false;
      } else {
        if (resolvedType.BaseType == null) {
          return false;
        }
        return Is(resolvedType.BaseType, t);
      }
    }

    public static bool IsSame<T>(this TypeReference type) {
      return IsSame(type, typeof(T));
    }

    public static bool IsSame(this TypeReference type, Type t) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      if (type.IsByReference) {
        type = type.GetElementTypeWithGenerics();
      }
      if (type.IsVoid() && t == typeof(void)) {
        return true;
      }
      if (type.IsValueType != t.IsValueType) {
        return false;
      }
      if (type.FullName != t.FullName) {
        return false;
      }
      return true;
    }

    public static bool IsSame(this TypeReference type, TypeOrTypeRef t) {
      if (t.Type != null) {
        return IsSame(type, t.Type);
      } else if (t.TypeReference != null) {
        return IsSame(type, t.TypeReference);
      } else {
        throw new InvalidOperationException();
      }
    }

    public static bool IsSame(this TypeReference type, TypeReference t) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      if (type.IsByReference) {
        type = type.GetElementTypeWithGenerics();
      }
      if (type.IsValueType != t.IsValueType) {
        return false;
      }
      if (type.FullName != t.FullName) {
        return false;
      }
      return true;
    }


    public static IEnumerable<TypeDefinition> GetHierarchy(this TypeDefinition type, TypeReference stopAtBaseType = null) {
      if (stopAtBaseType?.IsSame(type) == true) {
        yield break;
      }

      for (; ; ) {
        yield return type;
        if (type.BaseType == null || stopAtBaseType?.IsSame(type.BaseType) == true) {
          break;
        }
        type = type.BaseType.Resolve();
      }
    }
    
    public static bool Remove(this FieldDefinition field) {
      return field.DeclaringType.Fields.Remove(field);
    }

    public static FieldDefinition GetFieldOrThrow(this TypeDefinition type, string fieldName) {
      foreach (var field in type.Fields) {
        if ( field.Name == fieldName ) {
          return field;
        }
      }
      throw new ArgumentOutOfRangeException(nameof(fieldName), $"Field {fieldName} not found in {type}");
    }

    public static MethodReference GetGenericInstanceMethodOrThrow(this GenericInstanceType type, string name) {
      var methodRef = type.Resolve().GetMethodOrThrow(name);

      var newMethodRef = new MethodReference(methodRef.Name, methodRef.ReturnType) {
        HasThis = methodRef.HasThis,
        ExplicitThis = methodRef.ExplicitThis,
        DeclaringType = type,
        CallingConvention = methodRef.CallingConvention,
      };

      foreach (var parameter in methodRef.Parameters) {
        newMethodRef.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType));
      }

      foreach (var genericParameter in methodRef.GenericParameters) {
        newMethodRef.GenericParameters.Add(new GenericParameter(genericParameter.Name, newMethodRef));
      }

      return newMethodRef;
    }

    public static MethodReference GetCallable(this MethodReference methodRef, GenericInstanceType declaringType = null) {

      if (declaringType == null) {
        if (!methodRef.DeclaringType.HasGenericParameters) {
          return methodRef;
        }

        declaringType = new GenericInstanceType(methodRef.DeclaringType);
        foreach (var parameter in methodRef.DeclaringType.GenericParameters) {
          declaringType.GenericArguments.Add(parameter);
        }
      }

      var newMethodRef =  new MethodReference(methodRef.Name, methodRef.ReturnType, declaringType) {
        HasThis = methodRef.HasThis,
        ExplicitThis = methodRef.ExplicitThis,
        CallingConvention = methodRef.CallingConvention
      };

      foreach (var parameter in methodRef.Parameters) {
        newMethodRef.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType));
      }

      foreach (var genericParameter in methodRef.GenericParameters) {
        newMethodRef.GenericParameters.Add(new GenericParameter(genericParameter.Name, newMethodRef));
      }

      return newMethodRef;
    }

    public static FieldReference GetLoadable(this FieldDefinition field) {

      if (!field.DeclaringType.HasGenericParameters) {
        return field;
      }

      var declaringType = new GenericInstanceType(field.DeclaringType);
      foreach (var parameter in field.DeclaringType.GenericParameters) {
        declaringType.GenericArguments.Add(parameter);
      }
      return new FieldReference(field.Name, field.FieldType, declaringType);
    }

    public static void AddInterface<T>(this TypeDefinition type, ILWeaverAssembly asm) {
      type.Interfaces.Add(new InterfaceImplementation(asm.Import(typeof(T))));
    }

    public static bool RemoveAttribute<T>(this IMemberDefinition member, ILWeaverAssembly asm) where T : Attribute {
      for (int i = 0; i < member.CustomAttributes.Count; ++i) {
        var attr = member.CustomAttributes[i];
        if ( attr.AttributeType.Is<T>() ) {
          member.CustomAttributes.RemoveAt(i);
          return true;
        }
      }
      return false;
    }

    public static CustomAttribute AddAttribute<T>(this IMemberDefinition member, ILWeaverAssembly asm) where T : Attribute {
      CustomAttribute attr;
      attr = new CustomAttribute(typeof(T).GetConstructor(asm));
      member.CustomAttributes.Add(attr);
      return attr;
    }

    public static CustomAttribute AddAttribute<T, A0>(this IMemberDefinition member, ILWeaverAssembly asm, A0 arg0) where T : Attribute {
      CustomAttribute attr;
      attr = new CustomAttribute(typeof(T).GetConstructor(asm, 1));
      attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.ImportAttributeType<A0>(), arg0));
      member.CustomAttributes.Add(attr);
      return attr;
    }

    public static CustomAttribute AddAttribute<T, A0, A1>(this IMemberDefinition member, ILWeaverAssembly asm, A0 arg0, A1 arg1) where T : Attribute {
      CustomAttribute attr;
      attr = new CustomAttribute(typeof(T).GetConstructor(asm, 2));
      attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.ImportAttributeType<A0>(), arg0));
      attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.ImportAttributeType<A1>(), arg1));
      member.CustomAttributes.Add(attr);
      return attr;
    }

    public static CustomAttribute AddAttribute<T, A0, A1, A2>(this IMemberDefinition member, ILWeaverAssembly asm, A0 arg0, A1 arg1, A2 arg2) where T : Attribute {
      CustomAttribute attr;
      attr = new CustomAttribute(typeof(T).GetConstructor(asm, 3));
      attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.ImportAttributeType<A0>(), arg0));
      attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.ImportAttributeType<A1>(), arg1));
      attr.ConstructorArguments.Add(new CustomAttributeArgument(asm.ImportAttributeType<A2>(), arg2));
      member.CustomAttributes.Add(attr);
      return attr;
    }

    private static TypeReference ImportAttributeType<T>(this ILWeaverAssembly asm) {
      if (typeof(T) == typeof(TypeReference)) {
        return asm.Import<Type>();
      } else {
        return asm.Import<T>();
      }
    }

    public static MethodReference GetConstructor(this Type type, ILWeaverAssembly asm, int argCount = 0) {
      foreach (var ctor in type.GetConstructors()) {
        if (ctor.GetParameters().Length == argCount) {
          return asm.CecilAssembly.MainModule.ImportReference(ctor);
        }
      }

      throw new ILWeaverException($"Could not find constructor with {argCount} arguments on {type.Name}");
    }

    public static void AddTo(this MethodDefinition method, TypeDefinition type) {
      type.Methods.Add(method);
    }

    public static void AddTo(this PropertyDefinition property, TypeDefinition type) {
      type.Properties.Add(property);
    }


    public static void AddTo(this FieldDefinition field, TypeDefinition type) {
      type.Fields.Add(field);
    }

    public static void AddTo(this TypeDefinition type, AssemblyDefinition assembly) {
      assembly.MainModule.Types.Add(type);
    }

    public static void AddTo(this TypeDefinition type, TypeDefinition parentType) {
      parentType.NestedTypes.Add(type);
    }

    public static Instruction AppendReturn(this ILProcessor il, Instruction instruction) {
      il.Append(instruction);
      return instruction;
    }

    public static void Clear(this ILProcessor il) {
      var instructions = il.Body.Instructions;
      foreach (var instruction in instructions.Reverse()) {
        il.Remove(instruction);
      }
    }

    public static void AppendMacro<T>(this ILProcessor il, in T macro) where T : struct, ILProcessorMacro {
      macro.Emit(il);
    }

    public class TypeOrTypeRef {

      public Type Type { get; }
      public TypeReference TypeReference { get; }


      public TypeOrTypeRef(Type type, bool isOut = false) {
        Type = type;
      }

      public TypeOrTypeRef(TypeReference type, bool isOut = false) {
        TypeReference = type;
      }

      public static implicit operator TypeOrTypeRef(Type type) {
        return new TypeOrTypeRef(type);
      }

      public static implicit operator TypeOrTypeRef(TypeReference type) {
        return new TypeOrTypeRef(type);
      }

      public override string ToString() {
        if (Type != null) {
          return Type.FullName;
        } else if (TypeReference != null) {
          return TypeReference.ToString();
        } else {
          return "AnyType";
        }
      }
    }


    public static bool GetSingleOrDefaultMethodWithAttribute<T>(this TypeDefinition type, out CustomAttribute attribute, out MethodDefinition method) where T : Attribute {

      MethodDefinition resultMethod = null;
      CustomAttribute resultAttribute = null;

      foreach (var m in type.Methods) {
        if (m.TryGetAttribute<T>(out var attr)) {
          if (resultMethod != null) {
            throw new ILWeaverException($"Only one method with attribute {typeof(T)} allowed per class: {type}");
          } else {
            resultMethod = m;
            resultAttribute = attr;
          }
        }
      }

      method = resultMethod;
      attribute = resultAttribute;
      return method != null;
    }


    public static PropertyDefinition ThrowIfStatic(this PropertyDefinition property) {
      if (property.GetMethod?.IsStatic == true ||
          property.SetMethod?.IsStatic == true) {
        throw new ILWeaverException($"Property is static: {property.FullName}");
      }
      return property;
    }

    public static PropertyDefinition ThrowIfNoGetter(this PropertyDefinition property) {
      if (property.GetMethod == null) {
        throw new ILWeaverException($"Property does not have a getter: {property.FullName}");
      }
      return property;
    }

    public static PropertyDefinition ThrowIfNoSetter(this PropertyDefinition property) {
      if (property.GetMethod == null) {
        throw new ILWeaverException($"Property does not have a getter: {property.FullName}");
      }
      return property;
    }


    public static MethodDefinition ThrowIfStatic(this MethodDefinition method) {
      if (method.IsStatic) {
        throw new ILWeaverException($"Method is static: {method.FullName}");
      }
      return method;
    }

    public static MethodDefinition ThrowIfNotStatic(this MethodDefinition method) {
      if (!method.IsStatic) {
        throw new ILWeaverException($"Method is not static: {method}");
      }
      return method;
    }

    public static MethodDefinition ThrowIfNotPublic(this MethodDefinition method) {
      if (!method.IsPublic) {
        throw new ILWeaverException($"Method is not public: {method}");
      }
      return method;
    }

    public static MethodDefinition ThrowIfReturnType(this MethodDefinition method, TypeOrTypeRef type) {
      if (!method.ReturnType.IsSame(type)) {

        throw new ILWeaverException($"Method has an invalid return type (expected {type}): {method}");
      }
      return method;
    }

    public static MethodDefinition ThrowIfParameterCount(this MethodDefinition method, int count) {
      if (method.Parameters.Count != count) {
        throw new ILWeaverException($"Method has invalid parameter count (expected {count}): {method}");
      }
      return method;
    }

    public static MethodDefinition ThrowIfParameterCountLessThan(this MethodDefinition method, int count) {
      if (method.Parameters.Count < count) {
        throw new ILWeaverException($"Method has invalid parameter count (expected at leaset {count}): {method}");
      }
      return method;
    }

    public static MethodDefinition ThrowIfParameter(this MethodDefinition method, int index, TypeOrTypeRef type = null, bool isByReference = false, bool ignore = false) {
      if (ignore) {
        return method;
      }
      var p = method.Parameters[index];
      if (type != null && !p.ParameterType.IsSame(type)) {
        throw new ILWeaverException($"Parameter {p} ({index}) has an invalid type (expected {type}): {method}");
      }
      if (p.ParameterType.IsByReference != isByReference) {
        if (p.IsOut) {
          throw new ILWeaverException($"Parameter {p} ({index}) is a ref parameter: {method}");
        } else {
          throw new ILWeaverException($"Parameter {p} ({index}) is not a ref parameter: {method}");
        }
      }
      return method;
    }

    public static bool IsBaseConstructorCall(this Instruction instruction, TypeDefinition type) {
      if (instruction.OpCode == OpCodes.Call) {
        var m = ((MethodReference)instruction.Operand).Resolve();
        if (m.IsConstructor && m.DeclaringType.IsSame(type.BaseType)) {
          // base constructor init
          return true;
        }
      }
      return false;
    }

    public static bool IsLdloca(this Instruction instruction, out VariableDefinition variable, out bool isShort) {
      if (instruction.OpCode == OpCodes.Ldloca) {
        variable = (VariableDefinition)instruction.Operand;
        isShort = false;
        return true;
      }
      if (instruction.OpCode == OpCodes.Ldloca_S) {
        variable = (VariableDefinition)instruction.Operand;
        isShort = true;
        return true;
      }

      variable = default;
      isShort = default;
      return false;
    }

    public static bool IsLdlocWithIndex(this Instruction instruction, out int index) {
      if (instruction.OpCode == OpCodes.Ldloc_0) {
        index = 0;
        return true;
      }
      if (instruction.OpCode == OpCodes.Ldloc_1) {
        index = 1;
        return true;
      }
      if (instruction.OpCode == OpCodes.Ldloc_2) {
        index = 2;
        return true;
      }
      if (instruction.OpCode == OpCodes.Ldloc_3) {
        index = 3;
        return true;
      }
      index = -1;
      return false;
    }

    public static bool IsStlocWithIndex(this Instruction instruction, out int index) {
      if (instruction.OpCode == OpCodes.Stloc_0) {
        index = 0;
        return true;
      }
      if (instruction.OpCode == OpCodes.Stloc_1) {
        index = 1;
        return true;
      }
      if (instruction.OpCode == OpCodes.Stloc_2) {
        index = 2;
        return true;
      }
      if (instruction.OpCode == OpCodes.Stloc_3) {
        index = 3;
        return true;
      }
      index = -1;
      return false;
    }
  }

  public interface ILProcessorMacro {
    void Emit(ILProcessor il);
  }
}
#endif


#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverLog.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {

  using System;
  using System.Diagnostics;
  using System.Runtime.CompilerServices;
  using Mono.Cecil;
  using UnityEngine;

  public interface ILWeaverLogger {
    void Log(LogType logType, string message, string filePath, int lineNumber);
    void Log(Exception ex);
  }


  sealed class ILWeaverLog {

    private ILWeaverLogger _logger;

    public ILWeaverLogger Logger => _logger;

    public ILWeaverLog(ILWeaverLogger logger) {
      if (logger == null) {
        throw new ArgumentNullException(nameof(logger));
      }
      _logger = logger;
    }

    public void AssertMessage(bool condition, string message, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      if (!condition) {
        _logger.Log(LogType.Assert, $"Assert failed: {message}", filePath, lineNumber);
        throw new AssertException($"{message}{(string.IsNullOrEmpty(filePath) ? "" : $" at {filePath}:{lineNumber}")}");
      }
    }

    public void Assert(bool condition, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      if (!condition) {
        _logger.Log(LogType.Assert, $"Assert failed", filePath, lineNumber);
        throw new AssertException($"Assert failed{(string.IsNullOrEmpty(filePath) ? "" : $" at {filePath}:{lineNumber}")}");
      }
    }

    [Conditional("FUSION_WEAVER_DEBUG")]
    public void Debug(string message, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      _logger.Log(LogType.Log, message, filePath, lineNumber);
    }

    public void Warn(string message, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      _logger.Log(LogType.Warning, message, filePath, lineNumber);
    }

    public void Error(string message, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      _logger.Log(LogType.Error, message, filePath, lineNumber);
    }

    public void Exception(Exception ex) {
      _logger.Log(ex);
    }

#if !FUSION_WEAVER_DEBUG
    public struct LogScope : IDisposable {
      public void Dispose() {
      }
    }

    public LogScope Scope(string name) {
      return default;
    }

    public LogScope ScopeAssembly(AssemblyDefinition cecilAssembly) {
      return default;
    }

    public LogScope ScopeBehaviour(TypeDefinition type) {
      return default;
    }

    public LogScope ScopeInput(TypeDefinition type) {
      return default;
    }

    public LogScope ScopeStruct(TypeDefinition type) {
      return default;
    }

#else
    public LogScope Scope(string name, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      return new LogScope(this, name, filePath, lineNumber);
    }

    public LogScope ScopeAssembly(AssemblyDefinition cecilAssembly, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      return new LogScope(this, $"Assembly: {cecilAssembly.FullName}", filePath, lineNumber);
    }

    public LogScope ScopeBehaviour(TypeDefinition type, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      return new LogScope(this, $"Behaviour: {type.FullName}", filePath, lineNumber);
    }

    public LogScope ScopeInput(TypeDefinition type, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      return new LogScope(this, $"Input: {type.FullName}", filePath, lineNumber);
    }

    public LogScope ScopeStruct(TypeDefinition type, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) {
      return new LogScope(this, $"Struct: {type.FullName}", filePath, lineNumber);
    }

#pragma warning disable CS0282 // There is no defined ordering between fields in multiple declarations of partial struct
    public partial struct LogScope : IDisposable {
#pragma warning restore CS0282 // There is no defined ordering between fields in multiple declarations of partial struct
      public string Message;

      public TimeSpan Elapsed => _stopwatch.Elapsed;

      private ILWeaverLog _log;
      private Stopwatch _stopwatch;

      public int LineNumber;
      public string FilePath;

      public LogScope(ILWeaverLog log, string message, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = default) : this() {
        _log = log;
        _stopwatch = Stopwatch.StartNew();
        Message = message;
        LineNumber = lineNumber;
        FilePath = filePath;
        _log.Debug($"{Message} start", FilePath, LineNumber);
      }

      public void Dispose() {
        _stopwatch.Stop();
        _log.Debug($"{Message} end {Elapsed}", FilePath, LineNumber);
      }
    }
#endif
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverLoggerDiagnosticMessages.cs

#if FUSION_WEAVER && FUSION_WEAVER_ILPOSTPROCESSOR && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using Unity.CompilationPipeline.Common.Diagnostics;
  using UnityEngine;

  class ILWeaverLoggerDiagnosticMessages : ILWeaverLogger {

    public List<DiagnosticMessage> Messages { get; } = new List<DiagnosticMessage>();

    public void Log(LogType logType, string message, string filePath, int lineNumber) {

      DiagnosticType diagnosticType;

      if (logType == LogType.Log) {
        // there are no debug diagnostic messages, make pretend warnings
        message = $"DEBUG: {message}";
        diagnosticType = DiagnosticType.Warning;
      } else if (logType == LogType.Warning) {
        diagnosticType = DiagnosticType.Warning;
      } else {
        diagnosticType = DiagnosticType.Error;
      }

      // newlines in messagedata will need to be escaped, but let's not slow things down now
      Messages.Add(new DiagnosticMessage() {
        File = filePath,
        Line = lineNumber,
        DiagnosticType = diagnosticType,
        MessageData = message
      });
    }

    public void Log(Exception ex) {
      var lines = ex.ToString().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines) {
        Log(LogType.Error, line, null, 0);
      }
    }

    public void FixNewLinesInMessages() {
      // fix the messages
      foreach (var msg in Messages) {
        msg.MessageData = msg.MessageData.Replace('\r', ';').Replace('\n', ';');
      }
    }
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverLoggerUnityDebug.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {

  using UnityEngine;
  using System;

  public class ILWeaverLoggerUnityDebug : ILWeaverLogger {

    public void Log(LogType logType, string message, string filePath, int lineNumber) {
      Debug.unityLogger.Log(logType, message);
    }

    public void Log(Exception ex) {
      Debug.unityLogger.LogException(ex);
    }
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverMethodContext.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL

namespace Fusion.CodeGen {

  using System;
  using System.Collections.Generic;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;
  using static Fusion.CodeGen.ILWeaverOpCodes;

  class MethodContext : IDisposable {

    public Action<ILProcessor, TypeReference, ICustomAttributeProvider> LoadElementReaderWriterImpl;
    private Action<ILProcessor> _addressGetter;
    private Dictionary<(string, string), VariableDefinition> _fields = new Dictionary<(string, string), VariableDefinition>();
    private bool _runnerIsLdarg0 = false;
    private Action<ILProcessor> _valueGetter;
    private TargetVariableAddrInfo _targetVariable;

    public MethodContext(ILWeaverAssembly assembly, MethodDefinition method, bool staticRunnerAccessor = false,
      Action<ILProcessor> addressGetter = null,
      Action<ILProcessor> valueGetter = null) {
      if (assembly == null) {
        throw new ArgumentNullException(nameof(assembly));
      }
      if (method == null) {
        throw new ArgumentNullException(nameof(method));
      }

      this.Assembly = assembly;
      this.Method = method;
      this._runnerIsLdarg0 = staticRunnerAccessor;
      this._addressGetter = addressGetter;
      this._valueGetter = valueGetter;
    }

    public ILWeaverAssembly Assembly { get; private set; }
    public virtual bool HasOffset => false;
    public virtual bool IsWriteCompact => false;
    public MethodDefinition Method { get; private set; }

    public void Dispose() {
    }

    public AddOffsetMacro AddOffset() => new AddOffsetMacro(this, null);
    public AddOffsetMacro AddOffset(int value) => new AddOffsetMacro(this, Ldc_I4(value), isAligned: (value % Allocator.REPLICATE_WORD_SIZE) == 0);

    public ILMacroStruct AlignToWordSize() => new[] {
      Ldc_I4(Allocator.REPLICATE_WORD_SIZE - 1),
      Add(),
      Ldc_I4(~(Allocator.REPLICATE_WORD_SIZE - 1)),
      And()
    };

    public ForLoopMacro For(Action<ILProcessor> start, Action<ILProcessor> stop, Action<ILProcessor, VariableDefinition> body) => new ForLoopMacro(this, body, start, stop);

    public VariableDefinition GetOrCreateVariable(string id, TypeReference type, ILProcessor il = null) {
      if (_fields.TryGetValue((id, type.FullName), out var val)) {
        return val;
      }
      var result = CreateVariable(type, il);
      _fields.Add((id, type.FullName), result);
      return result;
    }

    public VariableDefinition CreateVariable(TypeReference type, ILProcessor il = null, Instruction before = null) {
      var result = new VariableDefinition(type);
      Method.Body.Variables.Add(result);
      if (il != null) {
        if (before == null) {
          if (type.IsValueType) {
            il.Append(Ldloca(result));
            il.Append(Initobj(type));
          } else {
            il.Append(Ldnull());
            il.Append(Stloc(result));
          }
        } else {
          if (type.IsValueType) {
            il.InsertBefore(before, Ldloca(result));
            il.InsertBefore(before, Initobj(type));
          } else {
            il.InsertBefore(before, Ldnull());
            il.InsertBefore(before, Stloc(result));
          }
        }         
      }
      return result;
    }

    public virtual ILMacroStruct LoadAddress() => _addressGetter;

    public virtual ILMacroStruct LoadElementReaderWriter(TypeReference type, ICustomAttributeProvider member) => new Action<ILProcessor>(il => LoadElementReaderWriterImpl(il, type, member));

    public ILMacroStruct LoadFixedBufferAddress(FieldDefinition fixedBufferField) => new Action<ILProcessor>(il => {

      var elementField = fixedBufferField.FieldType.Resolve().Fields[0];

      int pointerLoc = il.Body.Variables.Count;
      il.Body.Variables.Add(new VariableDefinition(elementField.FieldType.MakePointerType()));
      int pinnedRefLoc = il.Body.Variables.Count;
      il.Body.Variables.Add(new VariableDefinition(elementField.FieldType.MakeByReferenceType().MakePinnedType()));

      il.Append(Ldflda(fixedBufferField));
      il.Append(Ldflda(elementField));
      il.Append(Stloc(il.Body, pinnedRefLoc));
      il.Append(Ldloc(il.Body, pinnedRefLoc));
      il.Append(Conv_U());
      il.Append(Stloc(il.Body, pointerLoc));
      il.Append(Ldloc(il.Body, pointerLoc));
    });

    public ILMacroStruct LoadRunner() {
      return _runnerIsLdarg0 ?
        new[] { Ldarg_0() } :
        new[] { Ldarg_0(), Ldfld(Assembly.SimulationBehaviour.GetField(nameof(SimulationBehaviour.Runner))) };
    }

    public virtual ILMacroStruct LoadValue() => _valueGetter;

    public ValueGetterScope ValueGetter(Action<ILProcessor> valueGetter) => new ValueGetterScope(this, valueGetter);

    public ValueGetterScope ValueGetter(Action<ILProcessor, Action<ILProcessor>> valueGetter) {
      var current = _valueGetter;
      return new ValueGetterScope(this, il => valueGetter(il, current));
    }

    public LoadVariableAddressMacro GetTargetVariableAddrOrTemp(TypeReference type, ILProcessor il, out VariableDefinition variable, Instruction before = null) {
      if (_targetVariable.Variable == null || !_targetVariable.Type.IsSame(type)) {
        variable = CreateVariable(type, il, before);
        return new LoadVariableAddressMacro(variable, null, null);
      }

      TargetAddrUsed = true;
      variable = null;

      return new LoadVariableAddressMacro(_targetVariable.Variable, _targetVariable.IndexVariable, _targetVariable.Type);
    }


    public TargetVariableScope TargetVariableAddr(VariableDefinition variable) => new TargetVariableScope(this, new TargetVariableAddrInfo(variable));
    public TargetVariableScope TargetVariableAddr(VariableDefinition arrayVariable, VariableDefinition indexVariable, TypeReference type) => new TargetVariableScope(this, new TargetVariableAddrInfo(arrayVariable, indexVariable, type));


    public bool TargetAddrUsed { get; set; }


    public ILMacroStruct VerifyRawNetworkUnwrap(TypeReference type, int maxByteCount) => new[] {
      Ldc_I4(maxByteCount),
      Call(new GenericInstanceMethod(Assembly.ReadWriteUtils.GetMethod(nameof(ReadWriteUtilsForWeaver.VerifyRawNetworkUnwrap), genericArgsCount: 1)) {
        GenericArguments = { type }
      }),
    };

    public ILMacroStruct VerifyRawNetworkWrap(TypeReference type, int maxByteCount) => new[] {
      Ldc_I4(maxByteCount),
      Call(new GenericInstanceMethod(Assembly.ReadWriteUtils.GetMethod(nameof(ReadWriteUtilsForWeaver.VerifyRawNetworkWrap), genericArgsCount: 1)) {
        GenericArguments = { type }
      }),
    };

    protected virtual void EmitAddOffsetAfter(ILProcessor il) {
    }

    protected virtual void EmitAddOffsetBefore(ILProcessor il) {
    }
    public readonly struct AddOffsetMacro : ILProcessorMacro {
      public readonly MethodContext Context;
      public readonly Instruction Instruction;
      public readonly bool IsAligned;


      public AddOffsetMacro(MethodContext context, Instruction instruction = null, bool isAligned = false) {
        Context = context;
        Instruction = instruction;
        IsAligned = isAligned;
      }

      public void Emit(ILProcessor il) {
        if (Context.HasOffset) {
          if (Instruction == null) {
            if (!IsAligned) {
              il.AppendMacro(Context.AlignToWordSize());
            }
          }

          Context.EmitAddOffsetBefore(il);
          if (Instruction != null) {
            il.Append(Instruction);
            if (!IsAligned) {
              il.AppendMacro(Context.AlignToWordSize());
            }
          }

          Context.EmitAddOffsetAfter(il);
        } else {
          if (Instruction == null) {
            // means variant with size already pushed has been used, pop it
            il.Append(Pop());
          }
        }
      }
    }

    public readonly struct TargetVariableAddrInfo {
      public readonly VariableDefinition Variable;
      public readonly VariableDefinition IndexVariable;
      public readonly TypeReference Type;

      public TargetVariableAddrInfo(VariableDefinition variable) {
        Variable = variable;
        IndexVariable = null;
        Type = variable.VariableType;
      }

      public TargetVariableAddrInfo(VariableDefinition variable, VariableDefinition indexVariable, TypeReference elementType) {
        Variable = variable;
        IndexVariable = indexVariable;
        Type = elementType;
      }
    }

    public readonly struct ForLoopMacro : ILProcessorMacro {
      public readonly MethodContext Context;
      public readonly Action<ILProcessor, VariableDefinition> Generator;
      public readonly Action<ILProcessor> Start;
      public readonly Action<ILProcessor> Stop;

      public ForLoopMacro(MethodContext context, Action<ILProcessor, VariableDefinition> generator, Action<ILProcessor> start, Action<ILProcessor> stop) {
        Context = context;
        Generator = generator;
        Start = start;
        Stop = stop;
      }

      public void Emit(ILProcessor il) {
        var body = Context.Method.Body;
        var varId = body.Variables.Count;
        var indexVariable = new VariableDefinition(Context.Assembly.Import(typeof(int)));
        body.Variables.Add(indexVariable);

        Start(il);
        il.Append(Stloc(body, varId));

        var loopConditionStart = Ldloc(body, varId);
        il.Append(Br_S(loopConditionStart));
        {
          var loopBodyBegin = il.AppendReturn(Nop());
          Generator(il, indexVariable);

          il.Append(Ldloc(body, varId));
          il.Append(Ldc_I4(1));
          il.Append(Add());
          il.Append(Stloc(body, varId));

          il.Append(loopConditionStart);
          Stop(il);
          il.Append(Blt_S(loopBodyBegin));
        }
      }
    }

    public struct ValueGetterScope : IDisposable {
      MethodContext _context;
      Action<ILProcessor> _oldValueGetter;

      public ValueGetterScope(MethodContext context, Action<ILProcessor> valueGetter) {
        _context = context;
        _oldValueGetter = context._valueGetter;
        context._valueGetter = valueGetter;
      }

      public void Dispose() {
        _context._valueGetter = _oldValueGetter;
      }
    }

    public struct TargetVariableScope : IDisposable {
      MethodContext _context;
      TargetVariableAddrInfo _oldTargetVariable;
      bool _wasUsed;

      public TargetVariableScope(MethodContext context, TargetVariableAddrInfo variable) {
        _context = context;
        _oldTargetVariable = context._targetVariable;
        _wasUsed = context.TargetAddrUsed;
        context._targetVariable = variable;
        context.TargetAddrUsed = false;
      }

      public void Dispose() {
        _context._targetVariable = _oldTargetVariable;
        _context.TargetAddrUsed = _wasUsed;
      }
    }

    public struct LoadVariableAddressMacro : ILProcessorMacro {
      VariableDefinition _variable;
      VariableDefinition _index;
      TypeReference _elemType;

      public LoadVariableAddressMacro(VariableDefinition variable, VariableDefinition index, TypeReference elemType) {
        _variable = variable;
        _index = index;
        _elemType = elemType;
      }

      void ILProcessorMacro.Emit(ILProcessor il) {
        if (_index == null) {
          il.Append(Ldloca(_variable));
        } else {
          il.Append(Ldloc(_variable));
          il.Append(Ldloc(_index));
          il.Append(Ldelema(_elemType));
        }
      }
    }
  }

  class RpcMethodContext : MethodContext {
    public VariableDefinition DataVariable;
    public VariableDefinition OffsetVariable;
    public VariableDefinition RpcInvokeInfoVariable;

    public RpcMethodContext(ILWeaverAssembly asm, MethodDefinition definition, bool staticRunnerAccessor)
      : base(asm, definition, staticRunnerAccessor) {
    }

    public override bool HasOffset => true;
    public override bool IsWriteCompact => true;

    public override ILMacroStruct LoadAddress() => new[] {
        Ldloc(DataVariable),
        Ldloc(OffsetVariable),
        Add(),
      };

    public ILMacroStruct SetRpcInvokeInfoStatus(bool emitIf, RpcLocalInvokeResult reason) => RpcInvokeInfoVariable == null || !emitIf ? new Instruction[0] :
      new[] {
           Ldloca(RpcInvokeInfoVariable),
           Ldc_I4((int)reason),
           Stfld(Assembly.RpcInvokeInfo.GetField(nameof(RpcInvokeInfo.LocalInvokeResult)))
      };

    public ILMacroStruct SetRpcInvokeInfoStatus(RpcSendCullResult reason) => RpcInvokeInfoVariable == null ? new Instruction[0] :
      new[] {
           Ldloca(RpcInvokeInfoVariable),
           Ldc_I4((int)reason),
           Stfld(Assembly.RpcInvokeInfo.GetField(nameof(RpcInvokeInfo.SendCullResult)))
      };

    protected override void EmitAddOffsetAfter(ILProcessor il) {
      il.Append(Add());
      il.Append(Stloc(OffsetVariable));
    }

    protected override void EmitAddOffsetBefore(ILProcessor il) {
      il.Append(Ldloc(OffsetVariable));
    }
  }
}

#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverOpCodes.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System;
  using System.Diagnostics;

  using Mono.Cecil;
  using Mono.Cecil.Cil;

  static class ILWeaverOpCodes {
    // utils
    public static Instruction Nop()    => Instruction.Create(OpCodes.Nop);
    public static Instruction Ret()    => Instruction.Create(OpCodes.Ret);
    public static Instruction Dup()    => Instruction.Create(OpCodes.Dup);
    public static Instruction Pop()    => Instruction.Create(OpCodes.Pop);
    public static Instruction Ldnull() => Instruction.Create(OpCodes.Ldnull);
    public static Instruction Throw()  => Instruction.Create(OpCodes.Throw);

    public static Instruction Cast(TypeReference type) => Instruction.Create(OpCodes.Castclass, type);

    // breaks
    public static Instruction Brfalse(Instruction target)   => Instruction.Create(OpCodes.Brfalse, target);
    public static Instruction Brtrue(Instruction target)    => Instruction.Create(OpCodes.Brtrue, target);
    public static Instruction Brfalse_S(Instruction target) => Instruction.Create(OpCodes.Brfalse_S, target);
    public static Instruction Brtrue_S(Instruction target)  => Instruction.Create(OpCodes.Brtrue_S, target);
    public static Instruction Br_S(Instruction target)      => Instruction.Create(OpCodes.Br_S, target);
    public static Instruction Br(Instruction target)        => Instruction.Create(OpCodes.Br, target);
    public static Instruction Blt_S(Instruction target)     => Instruction.Create(OpCodes.Blt_S, target);
    public static Instruction Ble_S(Instruction target)     => Instruction.Create(OpCodes.Ble_S, target);
    public static Instruction Beq(Instruction target)       => Instruction.Create(OpCodes.Beq, target);
    public static Instruction Bne_Un_S(Instruction target)  => Instruction.Create(OpCodes.Bne_Un_S, target);
    public static Instruction Beq_S(Instruction target)     => Instruction.Create(OpCodes.Beq_S, target);

    // math
    public static Instruction Add() => Instruction.Create(OpCodes.Add);
    public static Instruction Sub() => Instruction.Create(OpCodes.Sub);
    public static Instruction Mul() => Instruction.Create(OpCodes.Mul);
    public static Instruction Div() => Instruction.Create(OpCodes.Div);
    public static Instruction And() => Instruction.Create(OpCodes.And);

    // obj
    public static Instruction Ldobj(TypeReference type) => Instruction.Create(OpCodes.Ldobj, type);
    public static Instruction Stobj(TypeReference type) => Instruction.Create(OpCodes.Stobj, type);

    public static Instruction Newobj(MethodReference constructor) => Instruction.Create(OpCodes.Newobj, constructor);

    public static Instruction Initobj(TypeReference type) => Instruction.Create(OpCodes.Initobj, type);

    public static Instruction Box(TypeReference type) => Instruction.Create(OpCodes.Box, type);


    // fields
    public static Instruction Ldflda(FieldReference field) => Instruction.Create(OpCodes.Ldflda, field);
    
    public static Instruction Ldfld(FieldReference  field) => Instruction.Create(OpCodes.Ldfld,  field);
    public static Instruction Stfld(FieldReference  field) => Instruction.Create(OpCodes.Stfld,  field);
    
    public static Instruction Ldsfld(FieldReference field) => Instruction.Create(OpCodes.Ldsfld, field);
    public static Instruction Stsfld(FieldReference field) => Instruction.Create(OpCodes.Stsfld, field);

    // locals

    public static Instruction Ldloc_or_const(VariableDefinition var, int val) => var != null ? Ldloc(var) : Ldc_I4(val);

    public static Instruction Ldloc(VariableDefinition var, MethodDefinition method) => Ldloc(method.Body, method.Body.Variables.IndexOf(var));

    public static Instruction Ldloc(VariableDefinition var)    => Instruction.Create(OpCodes.Ldloc, var);
    public static Instruction Ldloca(VariableDefinition var)   => Instruction.Create(OpCodes.Ldloca, var);
    public static Instruction Ldloca_S(VariableDefinition var) => Instruction.Create(OpCodes.Ldloca_S, var);
    public static Instruction Stloc(VariableDefinition var)    => Instruction.Create(OpCodes.Stloc, var);

    public static Instruction Stloc_0() => Instruction.Create(OpCodes.Stloc_0);
    public static Instruction Stloc_1() => Instruction.Create(OpCodes.Stloc_1);
    public static Instruction Stloc_2() => Instruction.Create(OpCodes.Stloc_2);
    public static Instruction Stloc_3() => Instruction.Create(OpCodes.Stloc_3);

    public static Instruction Ldloc_0() => Instruction.Create(OpCodes.Ldloc_0);
    public static Instruction Ldloc_1() => Instruction.Create(OpCodes.Ldloc_1);
    public static Instruction Ldloc_2() => Instruction.Create(OpCodes.Ldloc_2);
    public static Instruction Ldloc_3() => Instruction.Create(OpCodes.Ldloc_3);

    public static Instruction Stloc(MethodBody body, int index) {
      switch (index) {
        case 0:
          return Stloc_0();
        case 1:
          return Stloc_1();
        case 2:
          return Stloc_2();
        case 3:
          return Stloc_3();
        default:
          return Stloc(body.Variables[index]);
      }
    }

    public static Instruction Ldloc(MethodBody body, int index) {
      switch (index) {
        case 0:
          return Ldloc_0();
        case 1:
          return Ldloc_1();
        case 2:
          return Ldloc_2();
        case 3:
          return Ldloc_3();
        default:
          return Ldloc(body.Variables[index]);
      }
    }


    // ldarg
    public static Instruction Ldarg(ParameterDefinition arg) => Instruction.Create(OpCodes.Ldarg, arg);
    public static Instruction Ldarg_0() => Instruction.Create(OpCodes.Ldarg_0);
    public static Instruction Ldarg_1() => Instruction.Create(OpCodes.Ldarg_1);
    public static Instruction Ldarg_2() => Instruction.Create(OpCodes.Ldarg_2);
    public static Instruction Ldarg_3() => Instruction.Create(OpCodes.Ldarg_3);

    // starg

    public static Instruction Starg_S(ParameterDefinition arg) => Instruction.Create(OpCodes.Starg_S, arg);

    // array
    public static Instruction Ldlen()                    => Instruction.Create(OpCodes.Ldlen);

    public static Instruction Ldelem(TypeReference type) {
      switch (type.MetadataType) {
        case MetadataType.Byte:
          return Instruction.Create(OpCodes.Ldelem_U1);
        case MetadataType.SByte:
          return Instruction.Create(OpCodes.Ldelem_I1);
        case MetadataType.UInt16:
          return Instruction.Create(OpCodes.Ldelem_U2);
        case MetadataType.Int16:
          return Instruction.Create(OpCodes.Ldelem_I2);
        case MetadataType.UInt32:
          return Instruction.Create(OpCodes.Ldelem_U4);
        case MetadataType.Int32:
          return Instruction.Create(OpCodes.Ldelem_I4);
        case MetadataType.UInt64:
          return Instruction.Create(OpCodes.Ldelem_I8);
        case MetadataType.Int64:
          return Instruction.Create(OpCodes.Ldelem_I8);
        case MetadataType.Single:
          return Instruction.Create(OpCodes.Ldelem_R4);
        case MetadataType.Double:
          return Instruction.Create(OpCodes.Ldelem_R8);

        default:
          if (type.IsValueType) {
            return Instruction.Create(OpCodes.Ldelem_Any, type);
          } else {
            return Instruction.Create(OpCodes.Ldelem_Ref);
          }
      }
    }

    public static Instruction Stelem(TypeReference type) {
      switch (type.MetadataType) {
        case MetadataType.Byte:
        case MetadataType.SByte:
          return Instruction.Create(OpCodes.Stelem_I1);
        case MetadataType.UInt16:
        case MetadataType.Int16:
          return Instruction.Create(OpCodes.Stelem_I2);
        case MetadataType.UInt32:
        case MetadataType.Int32:
          return Instruction.Create(OpCodes.Stelem_I4);
        case MetadataType.UInt64:
        case MetadataType.Int64:
          return Instruction.Create(OpCodes.Stelem_I8);
        case MetadataType.Single:
          return Instruction.Create(OpCodes.Stelem_R4);
        case MetadataType.Double:
          return Instruction.Create(OpCodes.Stelem_R8);
        default:
          if (type.IsValueType) {
            return Instruction.Create(OpCodes.Stelem_Any, type);
          } else {
            return Instruction.Create(OpCodes.Stelem_Ref);
          }
      }
    }

    public static Instruction Ldelema(TypeReference arg) => Instruction.Create(OpCodes.Ldelema, arg);

    // conversions
    public static Instruction Conv_R4() => Instruction.Create(OpCodes.Conv_R4);
    public static Instruction Conv_I4() => Instruction.Create(OpCodes.Conv_I4);
    public static Instruction Conv_U() => Instruction.Create(OpCodes.Conv_U);

    // functions
    public static Instruction Call(MethodReference  method) => Instruction.Create(OpCodes.Call,  method);
    public static Instruction Ldftn(MethodReference method) => Instruction.Create(OpCodes.Ldftn, method);

    // constants

    public static Instruction Ldstr(string value) => Instruction.Create(OpCodes.Ldstr, value);
    public static Instruction Ldc_R4(float value) => Instruction.Create(OpCodes.Ldc_R4, value);
    public static Instruction Ldc_R8(float value) => Instruction.Create(OpCodes.Ldc_R8, value);

    public static Instruction Ldc_I4(int value) {
      switch (value) {
        case 0: return Instruction.Create(OpCodes.Ldc_I4_0);
        case 1: return Instruction.Create(OpCodes.Ldc_I4_1);
        case 2: return Instruction.Create(OpCodes.Ldc_I4_2);
        case 3: return Instruction.Create(OpCodes.Ldc_I4_3);
        case 4: return Instruction.Create(OpCodes.Ldc_I4_4);
        case 5: return Instruction.Create(OpCodes.Ldc_I4_5);
        case 6: return Instruction.Create(OpCodes.Ldc_I4_6);
        case 7: return Instruction.Create(OpCodes.Ldc_I4_7);
        case 8: return Instruction.Create(OpCodes.Ldc_I4_8);
        default:
          return Instruction.Create(OpCodes.Ldc_I4, value);
      }
    }

    public static Instruction Stind_I4() => Instruction.Create(OpCodes.Stind_I4);
    public static Instruction Ldind_I4() => Instruction.Create(OpCodes.Ldind_I4);

    public static Instruction Stind_R4() => Instruction.Create(OpCodes.Stind_R4);
    public static Instruction Ldind_R4() => Instruction.Create(OpCodes.Ldind_R4);
    


    public static Instruction Stind_or_Stobj(TypeReference type) {
      if (type.IsPrimitive) {
        return Stind(type);
      } else {
        return Stobj(type);
      }
    }

    public static Instruction Ldind_or_Ldobj(TypeReference type) {
      if (type.IsPrimitive) {
        return Ldind(type);
      } else {
        return Ldobj(type);
      }
    }

    public static Instruction Stind(TypeReference type) {
      switch (type.MetadataType) {
        case MetadataType.Byte:
        case MetadataType.SByte:
          return Instruction.Create(OpCodes.Stind_I1);
        case MetadataType.UInt16:
        case MetadataType.Int16:
          return Instruction.Create(OpCodes.Stind_I2);
        case MetadataType.UInt32:
        case MetadataType.Int32:
          return Instruction.Create(OpCodes.Stind_I4);
        case MetadataType.UInt64:
        case MetadataType.Int64:
          return Instruction.Create(OpCodes.Stind_I8);
        case MetadataType.Single:
          return Instruction.Create(OpCodes.Stind_R4);
        case MetadataType.Double:
          return Instruction.Create(OpCodes.Stind_R8);
        default:
          throw new ILWeaverException($"Unknown primitive type {type.FullName}");
      }
    }

    public static Instruction Ldind(TypeReference type) {
      switch (type.MetadataType) {
        case MetadataType.Byte:
          return Instruction.Create(OpCodes.Ldind_U1);
        case MetadataType.SByte:
          return Instruction.Create(OpCodes.Ldind_I1);
        case MetadataType.UInt16:
          return Instruction.Create(OpCodes.Ldind_U2);
        case MetadataType.Int16:
          return Instruction.Create(OpCodes.Ldind_I2);
        case MetadataType.UInt32:
          return Instruction.Create(OpCodes.Ldind_U4);
        case MetadataType.Int32:
          return Instruction.Create(OpCodes.Ldind_I4);
        case MetadataType.UInt64:
          return Instruction.Create(OpCodes.Ldind_I8);
        case MetadataType.Int64:
          return Instruction.Create(OpCodes.Ldind_I8);
        case MetadataType.Single:
          return Instruction.Create(OpCodes.Ldind_R4);
        case MetadataType.Double:
          return Instruction.Create(OpCodes.Ldind_R8);
        default:
          throw new ILWeaverException($"Unknown primitive type {type.FullName}");
      }
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/ILWeaverSettings.cs

#if FUSION_WEAVER
namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;

  public partial class ILWeaverSettings {

    public static string DefaultConfigPath {
      get {
        string result = "Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion";
        OverrideNetworkProjectConfigPath(ref result);
        return result;
      }
    }

    static partial void OverrideNetworkProjectConfigPath(ref string path);

    public static bool IsAssemblyWeavable(string[] assembliesToWeave, string assemblyName) {
      return Array.FindIndex(assembliesToWeave, x => assemblyName.Equals(x, StringComparison.OrdinalIgnoreCase)) >= 0;
    }

    public static bool ContainsRequiredReferences(string[] references) {
      return Array.FindIndex(references, x => x.Contains("Fusion.Runtime")) >= 0;
    }

    public AccuracyDefaults AccuracyDefaults = new AccuracyDefaults();

    internal float GetNamedFloatAccuracy(string tag) {
      try {
        if (AccuracyDefaults.TryGetAccuracy(tag, out var result)) {
          return result._value;
        } else {
          throw new ArgumentOutOfRangeException($"Unknown accuracy tag: {tag}");
        }
      } catch (Exception ex) {
        throw new Exception("Error getting accuracy " + tag, ex);
      }
    }

    public bool NullChecksForNetworkedProperties;
    public bool UseSerializableDictionary;
    public bool CheckRpcAttributeUsage;
    public bool CheckNetworkedPropertiesBeingEmpty;
    public string[] AssembliesToWeave;
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/InstructionEqualityComparer.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System.Collections.Generic;
  using Mono.Cecil.Cil;

  internal class InstructionEqualityComparer : IEqualityComparer<Instruction> {
    public bool Equals(Instruction x, Instruction y) {
      if (x.OpCode != y.OpCode) {
        return false;
      }

      if (x.Operand != y.Operand) {
        if (x.Operand?.GetType() != y?.Operand.GetType()) {
          return false;
        }
        // there needs to be a better way to do this
        if (x.Operand.ToString() != y.Operand.ToString()) {
          return false;
        }

      }

      return true;
    }

    public int GetHashCode(Instruction obj) {
      return obj.GetHashCode();
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/MemberReferenceFullNameComparer.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System.Collections.Generic;
  using Mono.Cecil;

  class MemberReferenceFullNameComparer : IEqualityComparer<MemberReference> {
    bool IEqualityComparer<MemberReference>.Equals(MemberReference x, MemberReference y) {
      if ( x == y ) {
        return true;
      }
      if ( x == null || y == null ) {
        return false;
      }

      return GetFullName(x).Equals(GetFullName(y));
    }

    int IEqualityComparer<MemberReference>.GetHashCode(MemberReference obj) {
      if ( obj == null ) {
        return 0;
      }
      return GetFullName(obj).GetHashCode();
    }

    string GetFullName(MemberReference member) {
      if (member is TypeReference type) {
        if (type.IsGenericParameter) {
          return $"{type.FullName} of {GetFullName(type.DeclaringType)}";
        }
      }
      return member.FullName;
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/MonoCecilExtensions.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {

  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Runtime.CompilerServices;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;

  public static class MonoCecilExtensions {
    public static MethodDefinition AddDefaultConstructor(this TypeDefinition type, Action<ILProcessor> initializers = null) {
      var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
      var method = new MethodDefinition(".ctor", methodAttributes, type.Module.ImportReference(typeof(void)));
      method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));

      MethodReference baseConstructor;

      if (type.BaseType.IsGenericInstance) {
        var gi = (GenericInstanceType)type.BaseType;
        baseConstructor = gi.GetGenericInstanceMethodOrThrow(".ctor");
      } else {
        if (!type.BaseType.Resolve().TryGetMatchingConstructor(method, out var baseConstructorDef)) {
          throw new ILWeaverException("Unable to find matching constructor.");
        }
        baseConstructor = baseConstructorDef;
      }

      type.Methods.Add(method);

      var il = method.Body.GetILProcessor();

      if (initializers != null) {
        initializers(il);
      }

      il.Append(Instruction.Create(OpCodes.Call, type.Module.ImportReference(baseConstructor)));
      il.Append(Instruction.Create(OpCodes.Ret));

      return method;
    }

    public static T GetAttributeArgument<T>(this CustomAttribute attr, int index) {
      if (TryGetAttributeArgument<T>(attr, index, out var result)) {
        return result;
      } else {
        throw new ArgumentOutOfRangeException($"Argument {index} not found in {attr}");
      }
    }

    public static MethodDefinition GetMethodOrThrow(this TypeDefinition type, string methodName, int? argCount = null) {
      var query = type.Methods.Where(x => x.Name == methodName);
      if (argCount != null) {
        query = query.Where(x => x.Parameters.Count == argCount.Value);
      }

      var results = query.ToList();
      if (results.Count == 0) {
        throw new ArgumentOutOfRangeException(nameof(methodName), $"Method {methodName} not found in {type}");
      } else if (results.Count > 1) {
        throw new ArgumentException(nameof(methodName), $"Method {methodName} has multiple matches in {type}");
      } else {
        return results[0];
      }
    }

    public static bool HasAttribute<T>(this ICustomAttributeProvider type) where T : Attribute {
      return TryGetAttribute<T>(type, out _);
    }

    public static bool TryGetBackingField(this PropertyDefinition property, out FieldDefinition field) {
      const string Prefix = "<";
      const string Suffix = ">k__BackingField";

      var fieldName = $"{Prefix}{property.Name}{Suffix}";

      foreach (var f in property.DeclaringType.Fields) {
        if (!f.IsPrivate) {
          continue;
        }
        if (f.Name == fieldName) {
          field = f;
          return true;
        }
      }

      field = null;
      return false;
    }

    public static bool IsBackingField(this FieldDefinition field, out PropertyDefinition property) {
      var fieldName = field.Name;

      const string Prefix = "<";
      const string Suffix = ">k__BackingField";

      if (!fieldName.StartsWith(Prefix) || !fieldName.EndsWith(Suffix)) {
        property = default;
        return false;
      }

      var propertyName = fieldName.Substring(Prefix.Length, fieldName.Length - Prefix.Length - Suffix.Length);
      foreach (var prop in field.DeclaringType.Properties) {
        if (prop.Name == propertyName) {
          property = prop;
          return true;
        }
      }

      throw new InvalidOperationException($"Field {field} matches backing field name, but property {propertyName} is not found");
    }

    public static bool IsEnumType(this TypeReference type, out TypeReference valueType) {
      var typeDef = type.Resolve();

      if (!typeDef.IsEnum) {
        valueType = default;
        return false;
      }

      foreach (var field in typeDef.Fields) {
        if (field.Name == "value__" && field.IsSpecialName && field.IsRuntimeSpecialName && !field.IsStatic) {
          valueType = field.FieldType;
          return true;
        }
      }

      throw new InvalidOperationException($"Matching value__ field not found on {type}");
    }

    public static bool IsFixedBuffer(this TypeReference type, out int size) {
      size = default;
      if (!type.IsValueType) {
        return false;
      }

      if (!type.Name.EndsWith("e__FixedBuffer")) {
        return false;
      }

      var definition = type.Resolve();

      // this is a bit of a guesswork
      if (HasAttribute<CompilerGeneratedAttribute>(definition) &&
          HasAttribute<UnsafeValueTypeAttribute>(definition) &&
          definition.ClassSize > 0) {
        size = definition.ClassSize;
        return true;
      }

      return false;
    }

    public static bool TryGetAttribute<T>(this ICustomAttributeProvider type, out CustomAttribute attribute) where T : Attribute {
      for (int i = 0; i < type.CustomAttributes.Count; ++i) {
        var attr = type.CustomAttributes[i];
        if (attr.AttributeType.Is(typeof(T))) {
          attribute = attr;
          return true;
        }
      }

      attribute = null;
      return false;
    }
    
    public static bool TryGetAttributeArgument<T>(this CustomAttribute attr, int index, out T value, T defaultValue = default) {
      if (index < attr.ConstructorArguments.Count) {
        var val = attr.ConstructorArguments[index].Value;
        if (val is T t) {
          value = t;
          return true;
        } else if ( typeof(T).IsEnum && val.GetType().IsPrimitive ) {
          value = (T)Enum.ToObject(typeof(T), val);
          return true;
        }
      }

      value = defaultValue;
      return false;
    }

    public static bool TryGetAttributeProperty<T>(this CustomAttribute attr, string name, out T value, T defaultValue = default) {
      if (attr.HasProperties) {
        var prop = attr.Properties.FirstOrDefault(x => x.Name == name);

        if (prop.Argument.Value != null) {
          value = (T)prop.Argument.Value;
          return true;
        }
      }

      value = defaultValue;
      return false;
    }

    public static bool TryGetMatchingConstructor(this TypeDefinition type, MethodDefinition constructor, out MethodDefinition matchingConstructor) {
      return TryGetMatchingMethod(type.GetConstructors(), constructor.Parameters, out matchingConstructor);
    }

    public static bool TryGetMethod(this TypeDefinition type, string methodName, IList<ParameterDefinition> parameters, out MethodDefinition method) {
      var methods = type.Methods.Where(x => x.Name == methodName);

      if (TryGetMatchingMethod(methods, parameters, out method)) {
        return true;
      }

      //if (type.BaseType != null) {
      //  if (stopAtBaseType == null || !stopAtBaseType.IsSame(type.BaseType)) {
      //    return TryGetMethod(type.BaseType.Resolve(), methodName, parameters, out method, stopAtBaseType);
      //  }
      //}

      method = null;
      return false;
    }

    private static bool TryGetMatchingMethod(IEnumerable<MethodDefinition> methods, IList<ParameterDefinition> parameters, out MethodDefinition result) {
      foreach (var c in methods) {
        if (c.Parameters.Count != parameters.Count) {
          continue;
        }
        int i;
        for (i = 0; i < c.Parameters.Count; ++i) {
          if (!c.Parameters[i].ParameterType.IsSame(parameters[i].ParameterType)) {
            break;
          }
        }

        if (i == c.Parameters.Count) {
          result = c;
          return true;
        }
      }

      result = null;
      return false;
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/NetworkTypeInfo.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using static ILWeaverOpCodes;

  public class NetworkTypeInfo {

    internal delegate void EmitDelegate(ICustomAttributeProvider member, ILProcessor processor, MethodContext context);
    internal delegate int GetMemberWordCountDelegate(ICustomAttributeProvider member, TypeReference declaringType);
    internal delegate int GetCapacityDelegate(ICustomAttributeProvider member);
    internal delegate bool GetIsTriviallyCopyableDelegate(ICustomAttributeProvider member);

    public TypeDefinition TypeDef { get; private set; }
    public TypeReference TypeRef { get; private set; }
    public NetworkTypeWrapInfo WrapInfo { get; private set; }

    public bool IsAccuracySupported { get; private set; }
    public bool IsCapacitySupported { get; private set; }
    public bool IsWeavable { get; private set; }
    public bool HasDynamicSize => _emitMaxByteCount != null;
    public bool HasStaticSize => _typeByteSize > 0;

    public int ByteCount {
      get {
        if (_typeByteSize <= 0) {
          throw new InvalidOperationException($"{TypeRef} does not have a static type size");
        }
        return _typeByteSize;
      }
    }
    public int ByteCountWordAligned => WordCount * Allocator.REPLICATE_WORD_SIZE;
    public int WordCount => Native.WordCount(ByteCount, Allocator.REPLICATE_WORD_SIZE);

    private int _typeByteSize;
    private EmitDelegate _emitWrite;
    private EmitDelegate _emitRead;
    private EmitDelegate _emitMaxByteCount;
    private GetMemberWordCountDelegate _getMemberWordCount;
    private GetCapacityDelegate _getCapacity;
    private TypeReference _unitySerializableType;
    private GetIsTriviallyCopyableDelegate _isTriviallyCopyable;


    internal static NetworkTypeInfo MakeBlittable(TypeReference type, int typeByteSize, bool isWeavable = false) {
      if (typeByteSize < 0) {
        throw new ArgumentOutOfRangeException(nameof(typeByteSize));
      }

      return new NetworkTypeInfo(type, type, typeByteSize, isWeavable) {
        _isTriviallyCopyable = (m) => true,
      };
    }

    internal static NetworkTypeInfo MakeWrapped(TypeReference type, NetworkTypeWrapInfo wrapInfo, bool isWeavable = false) {
      if (wrapInfo == null) {
        throw new ArgumentNullException(nameof(wrapInfo));
      }

      int wordCount;
      if (wrapInfo.MaxByteCount > 0) {
        // raw
        wordCount = Native.WordCount(wrapInfo.MaxByteCount, Allocator.REPLICATE_WORD_SIZE);
      } else if (wrapInfo.WrapperType != null) {
        // with wrapper
        wordCount = wrapInfo.WrapperTypeInfo.WordCount;
      } else {
        throw new ArgumentException(nameof(wrapInfo));
      }

      return new NetworkTypeInfo(type, type, -1, isWeavable) {
        WrapInfo = wrapInfo,
        _getMemberWordCount = (declaringType, member) => wordCount,
      };
    }

    internal static NetworkTypeInfo MakeCustom(TypeReference type, TypeReference unitySerializableType, int typeByteSize, 
      EmitDelegate read, EmitDelegate write, bool isAccuracySupported = false, bool isWeavable = false, 
      GetIsTriviallyCopyableDelegate isTriviallyCopyable = null) {
      if (read == null) {
        throw new ArgumentNullException(nameof(read));
      }
      if (write == null) {
        throw new ArgumentNullException(nameof(write));
      }
      if (typeByteSize < 0) {
        throw new ArgumentOutOfRangeException(nameof(typeByteSize));
      }

      return new NetworkTypeInfo(type, unitySerializableType, typeByteSize, isWeavable) {
        _emitRead = read,
        _emitWrite = write,
        _isTriviallyCopyable = isTriviallyCopyable,
        IsAccuracySupported = isAccuracySupported,
      };
    }

    internal static NetworkTypeInfo MakeCustom(TypeReference type, TypeReference unitySerializableType, GetMemberWordCountDelegate wordCount, 
      EmitDelegate read, EmitDelegate write, EmitDelegate compactByteCount = null, 
      GetCapacityDelegate capacity = null, bool isWeavable = false, bool isAccuracySupported = false, 
      GetIsTriviallyCopyableDelegate isTriviallyCopyable = null) {
      if (read == null) {
        throw new ArgumentNullException(nameof(read));
      }
      if (write == null) {
        throw new ArgumentNullException(nameof(write));
      }
      if (wordCount == null) {
        throw new ArgumentNullException(nameof(wordCount));
      }

      return new NetworkTypeInfo(type, unitySerializableType, - 1, isWeavable) {
        _getMemberWordCount = wordCount,
        _emitRead = read,
        _emitWrite = write,
        _getCapacity = capacity,
        _emitMaxByteCount = compactByteCount,
        IsAccuracySupported = isAccuracySupported,
        IsCapacitySupported = capacity != null,
        _isTriviallyCopyable = isTriviallyCopyable,
      };
    }

    private NetworkTypeInfo(TypeReference type, TypeReference unitySerializableType, int typeSize, bool isWeavable) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      if (unitySerializableType == null) {
        throw new ArgumentNullException(nameof(unitySerializableType));
      }

      TypeRef = type;
      TypeDef = type.Resolve();
      _unitySerializableType = unitySerializableType;
      _typeByteSize = typeSize;
      IsWeavable = isWeavable;
    }

    public int GetMemberWordCount(ICustomAttributeProvider member, TypeReference declaringType) {
      int result;
      if (_getMemberWordCount != null) {
        if (member == null) {
          throw new InvalidOperationException($"Member is needed to get word count");
        }
        result = _getMemberWordCount(member, declaringType);
      } else {
        result = WordCount;
      }

      if (result <= 0) {
        throw new InvalidOperationException($"Expected word count of {member} to be greater than 0");
      }
      return result;
    }

    public int GetMemberByteCount(ICustomAttributeProvider member, TypeReference declaringType) {
      int result;
      if (_getMemberWordCount != null) {
        if (member == null) {
          throw new InvalidOperationException($"Member is needed to get word count");
        }
        result = _getMemberWordCount(member, declaringType) * Allocator.REPLICATE_WORD_SIZE;
      } else {
        result = ByteCount;
      }

      if (result <= 0) {
        throw new InvalidOperationException($"Expected word count of {member} to be greater than 0");
      }
      return result;
    }

    public bool TryGetEffectiveMemberCapacity(ICustomAttributeProvider member, out int capacity) {
      if (_getCapacity != null) {
        capacity = _getCapacity(member);
        return true;
      } else {
        capacity = 0;
        return false;
      }
    }

    internal void EmitMaxByteCountWordAligned(ILProcessor il, MethodContext context, ICustomAttributeProvider member) {
      if (_emitMaxByteCount != null) {
        _emitMaxByteCount(member, il, context);
        il.AppendMacro(context.AlignToWordSize());

      } else {
        var wordCount = GetMemberWordCount(member, context.Method.DeclaringType);
        il.Append(Ldc_I4(wordCount * Allocator.REPLICATE_WORD_SIZE));
      }
    }

    internal void EmitWrite(ILProcessor il, MethodContext context, ICustomAttributeProvider member) {
      if (_emitWrite != null) {
        _emitWrite(member, il, context);
      } else if (WrapInfo != null) {

        // this is to do the store later on
        if (!WrapInfo.IsRaw) {
          il.AppendMacro(context.LoadAddress());
        }

        // actual args start here

        if (WrapInfo.NeedsRunner) {
          il.AppendMacro(context.LoadRunner());
        }
        il.AppendMacro(context.LoadValue());
        if (WrapInfo.IsRaw) {
          il.AppendMacro(context.LoadAddress());
        }

        il.Append(Call(WrapInfo.WrapMethod.GetCallable()));

        if (WrapInfo.IsRaw) {
          il.AppendMacro(context.VerifyRawNetworkWrap(TypeRef, WrapInfo.MaxByteCount));
          il.AppendMacro(context.AddOffset());

        } else {
          il.Append(Stind_or_Stobj(WrapInfo.WrapperType));
          il.AppendMacro(context.AddOffset(WrapInfo.WrapperTypeInfo.ByteCount));
        }

      } else {
        il.AppendMacro(context.LoadAddress());
        il.AppendMacro(context.LoadValue());
        il.Append(Stind_or_Stobj(TypeRef));
        il.AppendMacro(context.AddOffset(ByteCount));
      }
    }

    internal void EmitRead(ILProcessor il, MethodContext context, ICustomAttributeProvider member) {
      if (_emitRead != null) {
        _emitRead(member, il, context);
      } else if (WrapInfo != null) {

        if (WrapInfo.IsRaw || WrapInfo.UnwrapByRef) {


          var nop = il.AppendReturn(Nop());

          if (WrapInfo.NeedsRunner) {
            il.AppendMacro(context.LoadRunner());
          }

          il.AppendMacro(context.LoadAddress());

          if (!WrapInfo.IsRaw) {
            il.Append(Ldind_or_Ldobj(WrapInfo.WrapperType));
          }

          il.AppendMacro(context.GetTargetVariableAddrOrTemp(WrapInfo.TargetType, il, out var variable, before: nop));
          il.Append(Call(WrapInfo.UnwrapMethod.GetCallable()));

          if (WrapInfo.IsRaw) {
            il.AppendMacro(context.VerifyRawNetworkUnwrap(TypeRef, WrapInfo.MaxByteCount));
            il.AppendMacro(context.AddOffset());
          } else {
            il.AppendMacro(context.AddOffset(WrapInfo.WrapperTypeInfo.ByteCount));
          }

          if (variable != null) {
            il.Append(Ldloc(variable));
            if (!WrapInfo.TargetType.Is(TypeRef)) {
              il.Append(Cast(TypeRef));
            }
          }

          il.Remove(nop);

        } else {
          if (WrapInfo.NeedsRunner) {
            il.AppendMacro(context.LoadRunner());
          }
          il.AppendMacro(context.LoadAddress());
          il.Append(Ldind_or_Ldobj(WrapInfo.WrapperType));
          il.Append(Call(WrapInfo.UnwrapMethod.GetCallable()));

          if (!WrapInfo.TargetType.Is(TypeRef)) {
            il.Append(Cast(TypeRef));
          }

          il.AppendMacro(context.AddOffset(WrapInfo.WrapperTypeInfo.ByteCount));
        }

      } else {
        il.AppendMacro(context.LoadAddress());
        il.Append(Ldind_or_Ldobj(TypeRef));
        il.AppendMacro(context.AddOffset(ByteCount));
      }
    }

    internal TypeReference GetUnityDefaultFieldType(ICustomAttributeProvider member) {
      return _unitySerializableType;
    }

    internal bool IsTriviallyCopyable(ICustomAttributeProvider member) {
      return _isTriviallyCopyable?.Invoke(member) ?? false;
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/NetworkTypeInfoRegistry.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Runtime.InteropServices;
  using Mono.Cecil;
  using Mono.Cecil.Cil;
  using Mono.Cecil.Rocks;
  using UnityEngine;
  using Debug = System.Diagnostics.Debug;
  using static ILWeaverOpCodes;

  public class NetworkTypeInfoRegistry {

    public delegate int CalculateWordCountDelegate(TypeReference type);

    private Dictionary<TypeReference, NetworkTypeInfo> _types = new Dictionary<TypeReference, NetworkTypeInfo>(new MemberReferenceFullNameComparer());
    private ModuleDefinition _module;
    private CalculateWordCountDelegate _calculateValueTypeWordCount;
    private ILWeaverSettings _settings;

    internal ILWeaverLog Log { get; }

    public NetworkTypeInfoRegistry(ModuleDefinition module, ILWeaverSettings settings, ILWeaverLogger log, CalculateWordCountDelegate getWordCount) {
      _module = module;
      _settings = settings;
      _calculateValueTypeWordCount = getWordCount;
      Log = new ILWeaverLog(log);
      AddBuiltInTypes();
    }

    public int GetTypeWordCount(TypeReference type) => GetInfo(type).WordCount;
    public int GetTypeWordCount<T>() => GetInfo<T>().WordCount;
    public int GetTypeWordCount(Type type) => GetInfo(type).WordCount;

    public int GetPropertyWordCount(PropertyDefinition property) => GetMemberWordCount(property.PropertyType, property, property.DeclaringType);
    public int GetMemberWordCount(TypeReference type, ICustomAttributeProvider member, TypeReference declaringType) => GetInfo(type).GetMemberWordCount(member, declaringType);

    internal void EmitRead(TypeReference type, ILProcessor il, MethodContext context, ICustomAttributeProvider member) => GetInfo(type).EmitRead(il, context, member);
    internal void EmitWrite(TypeReference type, ILProcessor il, MethodContext context, ICustomAttributeProvider member) => GetInfo(type).EmitWrite(il, context, member);
    internal void EmitMaxByteCountWordAligned(TypeReference type, ILProcessor il, MethodContext context, ICustomAttributeProvider member) => GetInfo(type).EmitMaxByteCountWordAligned(il, context, member);


    public NetworkTypeInfo GetInfo<T>() => GetInfo(typeof(T));

    public NetworkTypeInfo GetInfo(Type type) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      var t = _module.ImportReference(type);
      if (t == null) {
        throw new InvalidOperationException($"Failed to resolve: {type.FullName}");
      }
      return GetInfo(t);
    }


    public NetworkTypeInfo GetInfo(TypeReference type) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      if (_types.TryGetValue(type, out var result)) {
        return result;
      }

      return AddType(type);
    }

    const int DefaultArrayCapacity = 1;

    public const int DefaultContainerCapacity = 1;
    public const int DefaultStringCapacity = 16;

    private NetworkTypeInfo AddType(TypeReference type) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      
      if (_types.ContainsKey(type)) {
        throw new InvalidOperationException($"Type {type} already added");
      }

      var meta = MakeTypeData(type);

      _types.Add(type, meta);
      return meta;
    }

    private NetworkTypeInfo MakeTypeData(TypeReference type) {

      var resolved = type.Resolve();
      var imported = _module.ImportReference(type);

      if (type is GenericParameter) {
        throw new ArgumentException($"Generic parameters are not supported", nameof(type));
      }

      {
        if (type is Mono.Cecil.PointerType ptrType) {
          return CreatePointerOrRefMeta(imported, ptrType.ElementType);
        } else if (type is ByReferenceType refType) {
          return CreatePointerOrRefMeta(imported, refType.ElementType);
        } else if (type is TypeSpecification && !(type is GenericInstanceType)) {
          throw new ArgumentException($"Invalid TypeReference type: {type.FullName} ({type.GetType()})", nameof(type));
        }
      }

      {
        if (type.IsNetworkArray(out var elementType)) {
          return CreateNetworkArrayMeta(imported, elementType);
        }
        if (type.IsNetworkList(out elementType)) {
          return CreateNetworkLinkedListMeta(imported, elementType);
        }
        if (type.IsNetworkDictionary(out var keyType, out var valueType)) {
          return CreateNetworkDictionaryMeta(imported, keyType, valueType);
        }
      }

      if (TryGetNetworkWrapperType(type, out var wrapInfo)) {
        return NetworkTypeInfo.MakeWrapped(imported, wrapInfo);
      } else if (resolved.IsValueType) {
        if (resolved.IsFixedBuffer(out var byteCount)) {
          return NetworkTypeInfo.MakeBlittable(imported, byteCount);
        } else if (resolved.IsEnumType(out var enumValueType)) {
          return NetworkTypeInfo.MakeBlittable(imported, GetInfo(enumValueType).ByteCount);
        } else if (resolved.Is<INetworkStruct>() || resolved.Is<INetworkInput>()) {
          int wordCount;
          try {
            wordCount = GetUserValueTypeWordCount(resolved, type);
          } catch (Exception ex) {
            throw new ArgumentException($"Failed to get user type word count: {type.FullName}", nameof(type), ex);
          }
          return NetworkTypeInfo.MakeBlittable(imported, Math.Max(1, wordCount) * Allocator.REPLICATE_WORD_SIZE, isWeavable: true);
        } else {
          // TODO: unmanaged portable structs
          throw new ArgumentException($"Value types need to implement either {nameof(INetworkStruct)} or {nameof(INetworkInput)} interface (type: {type.FullName})", nameof(type));
        }
      } else {
        // check for wapper?
        throw new ArgumentException($"Type {type.FullName} is a reference type but does not implement Wrap pattern.", nameof(type));
      }
    }

    private int GetUserValueTypeWordCount(TypeDefinition type, TypeReference typeRef) {
      int wordCount;

      // is the type already weaved?
      if (type.TryGetAttribute<NetworkStructWeavedAttribute>(out var attribute)) {

        wordCount = attribute.GetAttributeArgument<int>(0);

        // is this a generic composite type?
        if (attribute.TryGetAttributeArgument(1, out bool value, false) && value) {
          Log.Assert(typeRef.IsGenericInstance == true);
          foreach (var gen in ((GenericInstanceType)typeRef).GenericArguments) {
            if (gen.IsValueType && gen.Is<INetworkStruct>()) {
              wordCount += GetTypeWordCount(typeRef);
            }
          }
        }
      } else {
        wordCount = _calculateValueTypeWordCount(type);
      }

      return wordCount;
    }

    private NetworkTypeInfo CreatePointerOrRefMeta(TypeReference type, TypeReference elementType) {
      var elementInfo = GetInfo(elementType);
      return NetworkTypeInfo.MakeCustom(type,
        read:       (member, il, context) => il.AppendMacro(context.LoadAddress()),
        write:      (member, il, context) => throw new NotSupportedException($"Pointers and references can't have setters"),
        wordCount:  (member, declaringType) => elementInfo.GetMemberWordCount(member, declaringType),
        unitySerializableType: elementType
      );
    }

    private NetworkTypeInfo CreateNetworkArrayMeta(TypeReference type, TypeReference elementType) {
      var ctor = _module.ImportReference(type.Resolve().GetConstructors().Single(x => x.HasParameters));
      ctor.DeclaringType = type;

      elementType = _module.ImportReference(elementType);

      var elementInfo = GetInfo(elementType);
      return NetworkTypeInfo.MakeCustom(type,
        wordCount: (member, declaringType) => GetCapacity(member, DefaultContainerCapacity) * elementInfo.GetMemberWordCount(member, declaringType),
        read: (member, il, context) => {
          var capacity = GetCapacity(member, DefaultContainerCapacity);
          il.AppendMacro(context.LoadAddress());
          il.Append(Ldc_I4(capacity));
          il.AppendMacro(context.LoadElementReaderWriter(elementType, member));
          il.Append(Newobj(ctor));
        },
        write: (member, il, context) => throw new NotSupportedException($"Collections can't have setters"),
        capacity: (member) => GetCapacity(member, DefaultContainerCapacity),
        unitySerializableType: elementType.MakeArrayType()
      );
    }


    private NetworkTypeInfo CreateNetworkLinkedListMeta(TypeReference type, TypeReference elementType) {
      var ctor = _module.ImportReference(type.Resolve().GetConstructors().Single(x => x.HasParameters));
      ctor.DeclaringType = type;

      elementType = _module.ImportReference(elementType);
      var elementInfo = GetInfo(elementType);

      return NetworkTypeInfo.MakeCustom(type,
        wordCount: (member, declaringType) => {
          var capacity = GetCapacity(member, DefaultCollectionCapacity);
          return NetworkLinkedList<int>.META_WORDS + capacity * (elementInfo.GetMemberWordCount(member, declaringType) + NetworkLinkedList<int>.ELEMENT_WORDS);
        },
        read: (member, il, context) => {
          var capacity = GetCapacity(member, DefaultContainerCapacity);
          il.AppendMacro(context.LoadAddress());
          il.Append(Ldc_I4(capacity));
          il.AppendMacro(context.LoadElementReaderWriter(elementType, member));
          il.Append(Newobj(ctor));
        },
        write: (member, il, context) => throw new NotSupportedException($"Collections can't have setters"),
        capacity: (member) => GetCapacity(member, DefaultContainerCapacity),
        unitySerializableType: elementType.MakeArrayType()
      );
    }

    private NetworkTypeInfo CreateNetworkDictionaryMeta(TypeReference type, TypeReference keyType, TypeReference valueType) {
      var ctor = _module.ImportReference(type.Resolve().GetConstructors().Single(x => x.HasParameters));
      ctor.DeclaringType = type;

      keyType = _module.ImportReference(keyType);
      valueType = _module.ImportReference(valueType);
      
      var keyInfo = GetInfo(keyType);
      var valueInfo = GetInfo(valueType);

      TypeReference unitySerializableType;
      if (_settings.UseSerializableDictionary) {
        unitySerializableType = TypeReferenceRocks.MakeGenericInstanceType(_module.ImportReference(typeof(SerializableDictionary<,>)), keyType, valueType);
      } else {
        unitySerializableType = TypeReferenceRocks.MakeGenericInstanceType(_module.ImportReference(typeof(Dictionary<,>)), keyType, valueType);
      }

      NetworkTypeInfo.GetCapacityDelegate getCapacity = member => Primes.GetNextPrime(Math.Max(1, GetCapacity(member, DefaultCollectionCapacity)));
      return NetworkTypeInfo.MakeCustom(type,
        wordCount: (member, declaringType) => {
          var capacity = getCapacity(member);
          return
            // meta data (counts, etc)
            NetworkDictionary<int, int>.META_WORD_COUNT +
            // buckets
            (capacity) +
              // entry
              // next
              (capacity) +
              // key
              (capacity * keyInfo.GetMemberWordCount(member, declaringType)) +
              // value
              (capacity * valueInfo.GetMemberWordCount(member, declaringType));
        },
        read: (member, il, context) => {
          var capacity = getCapacity(member);
          il.AppendMacro(context.LoadAddress());
          il.Append(Ldc_I4(capacity));
          il.AppendMacro(context.LoadElementReaderWriter(keyType, member));
          il.AppendMacro(context.LoadElementReaderWriter(valueType, member));
          il.Append(Newobj(ctor));
        },
        write: (member, il, context) => throw new NotSupportedException($"Collections can't have setters"),
        capacity: getCapacity,
        unitySerializableType: unitySerializableType
      );
    }

    void AddBuiltInType(NetworkTypeInfo meta) {
      _types.Add(meta.TypeRef, meta);
    }

    unsafe void AddBuiltInType<T>() where T : unmanaged {
      AddBuiltInType(NetworkTypeInfo.MakeBlittable(_module.ImportReference(typeof(T)), sizeof(T)));
    }

    unsafe void AddBuiltInType<T>(bool supportsAccuracy, MethodReference readMethod, MethodReference writeMethod, int? size = null, NetworkTypeInfo.GetIsTriviallyCopyableDelegate isTriviallyCopyable = null) where T : unmanaged {
      readMethod = _module.ImportReference(readMethod);
      writeMethod = _module.ImportReference(writeMethod);

      if (size == null) {
        size = sizeof(T);
      }

      int alignedByteCount = Native.WordCount(size.Value, Allocator.REPLICATE_WORD_SIZE) * Allocator.REPLICATE_WORD_SIZE;

      var imported = _module.ImportReference(typeof(T));
      AddBuiltInType(NetworkTypeInfo.MakeCustom(imported, imported, 
        typeByteSize: size.Value,
        read: (member, il, c) => {
          il.AppendMacro(c.LoadAddress());
          if (supportsAccuracy) {
            il.Append(Ldc_R4(GetFloatAccuracy(member)));
          }
          il.Append(Call(readMethod));
          il.AppendMacro(c.AddOffset(alignedByteCount));
        },
        write: (member, il, c) => {
          il.AppendMacro(c.LoadAddress());
          if (supportsAccuracy) {
            var accuracy = GetFloatAccuracy(member);
            il.Append(Ldc_R4(accuracy == 0 ? 0 : (1f / accuracy)));
          }
          il.AppendMacro(c.LoadValue());
          il.Append(Call(writeMethod));
          il.AppendMacro(c.AddOffset(alignedByteCount));
        },
        isAccuracySupported: supportsAccuracy,
        isTriviallyCopyable: isTriviallyCopyable
      ));
    }
 
    unsafe void AddBuiltInTypes() {

      bool IsFloatTypeTriviallyCopyable(ICustomAttributeProvider member) {
        if (TryGetFloatAccuracy(member, out var value)) {
          if (value == 0) {
            return true;
          }
          return false;
        }

        if (member is ParameterDefinition) {
          // parameters are trivially copyable if there's no accuraccy too
          return true;
        } else {
          // implicitly using default accuraccy
          return false;
        }
      }

      var readWriteUtils = _module.ImportReference(typeof(ReadWriteUtilsForWeaver)).Resolve();
      
      // safe primitive types
      AddBuiltInType<byte>();
      AddBuiltInType<sbyte>();
      AddBuiltInType<Int16>();
      AddBuiltInType<UInt16>();
      AddBuiltInType<Int32>();
      AddBuiltInType<UInt32>();
      AddBuiltInType<Int64>();
      AddBuiltInType<UInt64>();

      {
        var writeFloat = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteFloat), 3));
        var t = _module.ImportReference(typeof(float));
        var typeData = NetworkTypeInfo.MakeCustom(t, t,
          typeByteSize: sizeof(float),
          read: (member, il, c) => {
            il.AppendMacro(c.LoadAddress());
            var accuracy = GetFloatAccuracy(member);
            if (accuracy == 0) {
              il.Append(Ldind_R4());
            } else {
              il.Append(Ldind_I4());
              il.Append(Conv_R4());
              il.Append(Ldc_R4(accuracy));
              il.Append(Mul());
            }
            il.AppendMacro(c.AddOffset(sizeof(float)));
          },
          write: (member, il, c) => {
            var accuracy = GetFloatAccuracy(member);
            il.AppendMacro(c.LoadAddress());
            if (accuracy == 0) {
              il.AppendMacro(c.LoadValue());
              il.Append(Stind_R4());
            } else {
              il.Append(Ldc_R4(1f / accuracy));
              il.AppendMacro(c.LoadValue());
              il.Append(Call(writeFloat));
            }
            il.AppendMacro(c.AddOffset(sizeof(float)));
          },
          isAccuracySupported: true,
          isTriviallyCopyable: IsFloatTypeTriviallyCopyable
        );
        AddBuiltInType(typeData);
      }
      AddBuiltInType<double>();

      AddBuiltInType<char>();


      AddBuiltInType<bool>(
        size: sizeof(int), 
        supportsAccuracy: false, 
        readMethod:  readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadBoolean), 1),
        writeMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteBoolean), 2)
      );

      // Unity types

      AddBuiltInType<Quaternion>(
        supportsAccuracy: true,
        readMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadQuaternion), 2),
        writeMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteQuaternion), 3),
        isTriviallyCopyable: IsFloatTypeTriviallyCopyable
      );

      AddBuiltInType<Vector2>(
        supportsAccuracy: true,
        readMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadVector2), 2),
        writeMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteVector2), 3),
        isTriviallyCopyable: IsFloatTypeTriviallyCopyable
      );

      AddBuiltInType<Vector3>(
        supportsAccuracy: true,
        readMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadVector3), 2),
        writeMethod: readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteVector3), 3),
        isTriviallyCopyable: IsFloatTypeTriviallyCopyable
      );

      AddBuiltInType<Vector4>();
      AddBuiltInType<Matrix4x4>();
      AddBuiltInType<Vector2Int>();
      AddBuiltInType<Vector3Int>();
      AddBuiltInType<BoundingSphere>();
      AddBuiltInType<Bounds>();
      AddBuiltInType<Rect>();
      AddBuiltInType<Color>();
      AddBuiltInType<BoundsInt>();
      AddBuiltInType<RectInt>();
      AddBuiltInType<Color32>();

      AddBuiltInType<NetworkString<_2>>();
      AddBuiltInType<NetworkString<_4>>();
      AddBuiltInType<NetworkString<_8>>();
      AddBuiltInType<NetworkString<_16>>();
      AddBuiltInType<NetworkString<_32>>();
      AddBuiltInType<NetworkString<_64>>();
      AddBuiltInType<NetworkString<_128>>();
      AddBuiltInType<NetworkString<_256>>();
      AddBuiltInType<NetworkString<_512>>();

      {
        var getUtf8ByteCount = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.GetByteCountUtf8NoHash), 1));
        var writeUtf8 = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteStringUtf8NoHash), 2));
        var readUtf8 = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadStringUtf8NoHash), 2));

        var readNoHash = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadStringUtf32NoHash), 3));
        var writeNoHash = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteStringUtf32NoHash), 3));

        var readWithHash = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.ReadStringUtf32WithHash), 3));
        var writeWithHash = _module.ImportReference(readWriteUtils.GetMethodOrThrow(nameof(ReadWriteUtilsForWeaver.WriteStringUtf32WithHash), 4));

        var t = _module.ImportReference(typeof(string));

        FieldDefinition GetCacheField(PropertyDefinition prop) {
          var name = $"cache_{prop.Name}";
          var field = prop.DeclaringType.Fields.SingleOrDefault(x => x.Name == name && x.FieldType.IsSame(t));
          if (field == null) {
            field = new FieldDefinition($"cache_{prop.Name}", FieldAttributes.Private, t);
            prop.DeclaringType.Fields.Add(field);
          }
          return field;
        }

        var typeData = NetworkTypeInfo.MakeCustom(t,
          wordCount: (member, declaringType) => {
            return GetCapacity(member, DefaultStringCapacity) + (declaringType.IsValueType ? 1 : 2);
          },
          read: (member, il, context) => {
            il.AppendMacro(context.LoadAddress());
            if (context.IsWriteCompact) {
              il.AppendMacro(context.GetTargetVariableAddrOrTemp(t, il, out var variable));
              il.Append(Call(readUtf8));
              il.AppendMacro(context.AddOffset());
              if (variable != null) {
                il.Append(Ldloc(variable));
              }
            } else {
              il.Append(Ldc_I4(GetCapacity(member, DefaultStringCapacity)));
              if (context.Method.DeclaringType.IsValueType) {
                il.AppendMacro(context.GetTargetVariableAddrOrTemp(t, il, out var variable));
                il.Append(Call(readNoHash));
                il.AppendMacro(context.AddOffset());
                if (variable != null) {
                  il.Append(Ldloc(variable));
                }
              } else {
                var field = GetCacheField((PropertyDefinition)member);
                il.Append(Ldarg_0());
                il.Append(Ldflda(field));
                il.Append(Call(readWithHash));
                il.AppendMacro(context.AddOffset());
                il.Append(Ldarg_0());
                il.Append(Ldfld(field));
              }
              
            }
          },
          write: (member, il, context) => {

            il.AppendMacro(context.LoadAddress());

            if (context.IsWriteCompact) {
              il.AppendMacro(context.LoadValue());
              il.Append(Call(writeUtf8));
            } else {
              il.Append(Ldc_I4(GetCapacity(member, DefaultStringCapacity)));

              if (context.Method.DeclaringType.IsValueType) {
                il.AppendMacro(context.LoadValue());
                il.Append(Call(writeNoHash));
              } else {
                il.AppendMacro(context.LoadValue());
                il.Append(Ldarg_0());
                il.Append(Ldflda(GetCacheField((PropertyDefinition)member)));
                il.Append(Call(writeWithHash));
              }
            }

            il.AppendMacro(context.AddOffset());
          },
          compactByteCount: (member, il, context) => {
            il.AppendMacro(context.LoadValue());
            il.Append(Call(getUtf8ByteCount));
          },
          capacity: member => GetCapacity(member, DefaultStringCapacity),
          unitySerializableType: t
        );
        AddBuiltInType(typeData);
      }

      // System types
      AddBuiltInType<Guid>();

      // reference types
      foreach (var refType in new [] { typeof(NetworkObject), typeof(NetworkBehaviour) }) {
        var t = _module.ImportReference(refType);
        TryGetNetworkWrapperType(t, out var wrapInfo);
        AddBuiltInType(NetworkTypeInfo.MakeWrapped(t, wrapInfo, isWeavable: false));
      }
    }

    const int DefaultCollectionCapacity = 1;


    internal static int GetCapacity(PropertyDefinition property, int defaultCapacity) {
      if (property.TryGetAttribute<CapacityAttribute>(out var attr)) {
        return attr.GetAttributeArgument<int>(0);
      } else {
        return defaultCapacity;
      }
    }

    bool TryGetNetworkWrapperType(TypeReference type, out NetworkTypeWrapInfo result) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }

      var definition = type.Resolve();

      int rawByteCount = 0;

      bool needsRunner = false;
      int argsStart = 0;

      if (definition.GetSingleOrDefaultMethodWithAttribute<NetworkSerializeMethodAttribute>(out var wrapAttribute, out var wrapMethod)) {
        wrapAttribute.TryGetAttributeProperty<int>(nameof(NetworkSerializeMethodAttribute.MaxSize), out rawByteCount);

        try {
          if (wrapMethod.ThrowIfParameterCountLessThan(1).Parameters[0].ParameterType.Is<NetworkRunner>()) {
            needsRunner = true;
            argsStart = 1;
          }

          if (rawByteCount > 0) {
            wrapMethod.ThrowIfNotStatic()
                      .ThrowIfNotPublic()
                      .ThrowIfParameterCount(argsStart + 2)
                      .ThrowIfParameter(argsStart + 0, type)
                      .ThrowIfParameter(argsStart + 1, typeof(byte*))
                      .ThrowIfReturnType(typeof(int));
          } else {
            wrapMethod.ThrowIfNotStatic()
                      .ThrowIfNotPublic()
                      .ThrowIfParameterCount(argsStart + 1)
                      .ThrowIfParameter(argsStart + 0, type);
          }
        } catch (Exception ex) {
          throw new ILWeaverException($"Method marked with {nameof(NetworkSerializeMethodAttribute)} has an invalid signature", ex);
        }
      }

      bool unwrapByRef = false;

      if (definition.GetSingleOrDefaultMethodWithAttribute<NetworkDeserializeMethodAttribute>(out var unwrapAttribute, out var unwrapMethod)) {
        if (wrapMethod == null) {
          throw new ILWeaverException($"Method marked with {nameof(NetworkDeserializeMethodAttribute)}, but there is no method marked with {nameof(NetworkSerializeMethodAttribute)}: {unwrapMethod}");
        }

        try {
          if (needsRunner) {
            unwrapMethod.ThrowIfParameterCountLessThan(1)
                        .ThrowIfParameter(0, typeof(NetworkRunner));
          }

          if (rawByteCount > 0) {
            unwrapMethod.ThrowIfNotStatic()
                        .ThrowIfNotPublic()
                        .ThrowIfReturnType(typeof(int))
                        .ThrowIfParameterCount(argsStart + 2)
                        .ThrowIfParameter(argsStart + 0, typeof(byte*))
                        .ThrowIfParameter(argsStart + 1, type, isByReference: true);
            unwrapByRef = true;
          } else {
            unwrapMethod.ThrowIfNotStatic()
                        .ThrowIfNotPublic()
                        .ThrowIfParameter(argsStart + 0, wrapMethod.ReturnType);

            if (unwrapMethod.Parameters.Count == 2 + argsStart) {
              unwrapMethod.ThrowIfReturnType(typeof(void))
                          .ThrowIfParameter(argsStart + 1, type, isByReference: true);
              unwrapByRef = true;
            } else {
              unwrapMethod.ThrowIfParameterCount(argsStart + 1);
              unwrapMethod.ThrowIfReturnType(type);
            }
          }
        } catch (Exception ex) {
          throw new ILWeaverException($"Method marked with {nameof(NetworkDeserializeMethodAttribute)} has an invalid signature", ex);
        }
      } else if (wrapMethod != null) {
        throw new ILWeaverException($"Method marked with {nameof(NetworkSerializeMethodAttribute)}, but there is no method marked with {nameof(NetworkDeserializeMethodAttribute)}: {wrapMethod}");
      }


      if (wrapMethod != null && unwrapMethod != null) {
        result = new NetworkTypeWrapInfo() {
          NeedsRunner = needsRunner,
          UnwrapMethod = _module.ImportReference(unwrapMethod),
          WrapMethod = _module.ImportReference(wrapMethod),
          MaxByteCount = rawByteCount,
          UnwrapByRef = unwrapByRef,
          WrapperType = rawByteCount > 0 ? null : _module.ImportReference(wrapMethod.ReturnType),
          WrapperTypeInfo = rawByteCount > 0 ? null : GetInfo(wrapMethod.ReturnType),
          TargetType = _module.ImportReference(definition),
        };
        return true;
      }

      if (definition.BaseType == null) {
        result = default;
        return false;
      } else {
        return TryGetNetworkWrapperType(definition.BaseType, out result);
      }
    }

    public bool TryGetFloatAccuracy(ICustomAttributeProvider member, out float accuracy) {
      if (member.TryGetAttribute<AccuracyAttribute>(out var attr)) {
        var obj = attr.ConstructorArguments[0];

        // If the argument is a string, this is using a global Accuracy. Need to look up the value.
        if (obj.Value is string str) {
          accuracy = _settings.GetNamedFloatAccuracy(str);
        } else {
          var val = attr.ConstructorArguments[0].Value;
          if (val is float fval) {
            accuracy = fval;
          } else if (val is double dval) {
            accuracy = (float)dval;
          } else {
            throw new Exception($"Invalid argument type: {val.GetType()}");
          }
        }
        return true;
      } else {
        accuracy = default;
        return false;
      }
    }

    float GetFloatAccuracy(ICustomAttributeProvider property) {
      if (TryGetFloatAccuracy(property, out var accuracy)) {
        return accuracy;
      }
      return AccuracyDefaults.DEFAULT_ACCURACY;
    }

    public static int GetCapacity(ICustomAttributeProvider member, int defaultCapacity) {
      if (member.TryGetAttribute<CapacityAttribute>(out var attr)) {
        if (attr.TryGetAttributeArgument<int>(0, out var result)) {
          return result;
        }
      }
      return defaultCapacity;
    }

    public static bool TryGetCapacity(ICustomAttributeProvider member, out int capacity) {
      if (member.TryGetAttribute<CapacityAttribute>(out var attr)) {
        if (attr.TryGetAttributeArgument<int>(0, out var result)) {
          capacity = result;
          return true;
        }
      }
      capacity = 0;
      return false;
    }
  }
}
#endif

#endregion


#region Assets/Photon/FusionCodeGen/NetworkTypeWrapInfo.cs

#if FUSION_WEAVER && FUSION_HAS_MONO_CECIL
namespace Fusion.CodeGen {
  using Mono.Cecil;

  public class NetworkTypeWrapInfo {
    public NetworkTypeInfo WrapperTypeInfo;
    public TypeReference WrapperType;
    public TypeReference TargetType;
    public MethodReference WrapMethod;
    public MethodReference UnwrapMethod;
    public bool NeedsRunner;
    public bool UnwrapByRef;
    public int MaxByteCount;
    public bool IsRaw => MaxByteCount > 0; 
  }
}
#endif

#endregion

#endif
