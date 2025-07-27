using Miniscript;
using Photon.Deterministic;
using Quantum;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

public unsafe class ValUnsafeStruct : ValMap {

    public Type Type;
    public byte* Origin;

    public ValUnsafeStruct(Type type, byte* origin) {
        Type = type;
        Origin = origin;

        evalOverride = (ValMap self, Value key, out Value outValue) => {
            FieldInfo field = Type.GetField(key.ToString(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (field == null) {
                // Ignore.
                outValue = null;
                return true;
            }
            nint offset = Marshal.OffsetOf(Type, field.Name);
            byte* fieldOrigin = origin + offset;

            Type fieldType = field.FieldType;
            switch (fieldType) {
            // Natively support Quantum-compatible primitives
            case var _ when fieldType == typeof(char): outValue = new ValNumber(*(char*) fieldOrigin); break;
            case var _ when fieldType == typeof(byte): outValue = new ValNumber(*fieldOrigin); break;
            case var _ when fieldType == typeof(sbyte): outValue = new ValNumber(*(sbyte*) fieldOrigin); break;
            case var _ when fieldType == typeof(ushort): outValue = new ValNumber(*(ushort*) fieldOrigin); break;
            case var _ when fieldType == typeof(short): outValue = new ValNumber(*(short*) fieldOrigin); break;
            case var _ when fieldType == typeof(uint): outValue = new ValNumber(*(uint*) fieldOrigin); break;
            case var _ when fieldType == typeof(int): outValue = new ValNumber(*(int*) fieldOrigin); break;
            case var _ when fieldType == typeof(ulong): outValue = new ValNumber((uint) (*(ulong*) fieldOrigin)); break;
            case var _ when fieldType == typeof(long): outValue = new ValNumber((int) (*(long*) fieldOrigin)); break;

            // Natively support some Quantum types
            case var _ when fieldType == typeof(QBoolean): outValue = *(QBoolean*) fieldOrigin ? ValNumber.one : ValNumber.zero; break;
            case var _ when fieldType == typeof(FP): outValue = new ValNumber(*(FP*) fieldOrigin); break;
            case var _ when fieldType == typeof(EntityRef): outValue = new ValEntityRef(*(EntityRef*) fieldOrigin); break;
            case var _ when fieldType == typeof(PlayerRef): outValue = new ValNumber((int) *(PlayerRef*) fieldOrigin); break;
            case var _ when fieldType == typeof(AssetRef): outValue = new ValAssetRef(*(AssetRef*) fieldOrigin); break;
            //case var _ when fieldType == typeof(PhysicsQueryRef): outValue = ...; break;
            case var _ when typeof(IQString).IsAssignableFrom(fieldType): outValue = null; break;
            case var _ when typeof(IQStringUtf8).IsAssignableFrom(fieldType): outValue = null; break;
            
            // Fallback to returning the nested struct
            default: outValue = new ValUnsafeStruct(fieldType, fieldOrigin); break;
            };
            return true;
        };
        assignOverride = (ValMap self, Value key, Value value) => {
            FieldInfo field = Type.GetField(key.ToString(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (field == null) {
                // Ignore.
                return true;
            }
            nint offset = Marshal.OffsetOf(Type, field.Name);
            byte* fieldOrigin = origin + offset;

            Type fieldType = field.FieldType;
            switch (fieldType) {
            // Natively support Quantum-compatible primitives
            case var _ when fieldType == typeof(char): *(char*) fieldOrigin = value.ToString()[0]; break;
            case var _ when fieldType == typeof(byte): *fieldOrigin = (byte) value.UIntValue(); break;
            case var _ when fieldType == typeof(sbyte): *(sbyte*) fieldOrigin = (sbyte) value.UIntValue(); break;
            case var _ when fieldType == typeof(ushort): *(ushort*) fieldOrigin = (ushort) value.UIntValue(); break;
            case var _ when fieldType == typeof(short): *(short*) fieldOrigin = (short) value.UIntValue(); break;
            case var _ when fieldType == typeof(uint): *(uint*) fieldOrigin = value.UIntValue(); break;
            case var _ when fieldType == typeof(int): *(int*) fieldOrigin = value.IntValue(); break;
            case var _ when fieldType == typeof(ulong): *(long*) fieldOrigin = (long) value.DoubleValue(); break;
            case var _ when fieldType == typeof(long): *(ulong*) fieldOrigin = (ulong) value.DoubleValue(); break;
            // Natively support some Quantum types
            case var _ when fieldType == typeof(QBoolean): *(QBoolean*) fieldOrigin = value.BoolValue(); break;
            case var _ when fieldType == typeof(FP): *(FP*) fieldOrigin = value.DoubleValue(); break;
            case var _ when fieldType == typeof(EntityRef): *(EntityRef*) fieldOrigin = (value as ValEntityRef)?.EntityRef ?? EntityRef.None; break;
            case var _ when fieldType == typeof(PlayerRef): *(PlayerRef*) fieldOrigin = value.IntValue();  break;
            case var _ when fieldType == typeof(AssetRef): *(AssetRef*) fieldOrigin = (value as ValAssetRef)?.Asset ?? default; break;
            // case var _ when fieldType == typeof(PhysicsQueryRef) => new PhysicsQueryRef(; break;
            case var _ when typeof(IQStringUtf8).IsAssignableFrom(fieldType):
            case var _ when typeof(IQString).IsAssignableFrom(fieldType):
                object qstr = fieldType
                    .GetMethod("op_Implicit", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null)?
                    .Invoke(null, new object[] { value.ToString() });

                int size = Marshal.SizeOf(fieldType);
                IntPtr managedPtr = Marshal.AllocHGlobal(size);
                try {
                    Marshal.StructureToPtr(qstr, managedPtr, false);
                    Buffer.MemoryCopy((void*) managedPtr, fieldOrigin, size, size);
                } finally {
                    Marshal.FreeHGlobal(managedPtr);
                }
                break;
            //default: new ValUnsafeStruct(fieldType, fieldOrigin); break;
            };
            return true;
        };
    }
}