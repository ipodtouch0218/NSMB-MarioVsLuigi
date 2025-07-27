using Miniscript;
using Photon.Deterministic;
using Quantum;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public unsafe class ValComponentRef : Value {

    public EntityRef EntityRef;
    public int TypeIndex;
   
    public ValComponentRef(EntityRef entityRef, int typeIndex) {
        EntityRef = entityRef;
        TypeIndex = typeIndex;
    }

    public override bool CanSetElem() {
        return true;
    }

    public override void SetElem(Value index, Value value, TAC.Context context) {
        Frame f = (Frame) ((ValMap) context.vm.globalContext.GetLocal("frame")).userData;
        Type componentType = ComponentTypeId.GetComponentType(TypeIndex);

        if (!f.Unsafe.TryGetPointer(EntityRef, TypeIndex, out void* component)) {
            return;
        }

        TryWrite(componentType, new IntPtr(component), index.ToString(), value);
    }

    private void TryWrite(Type structType, IntPtr destinationStruct, string key, Value value) {
        string capitalizedKey;
        if (key.Length == 1) {
            capitalizedKey = key.ToUpper();
        } else {
            capitalizedKey = char.ToUpper(key[0]) + key[1..];
        }

        FieldInfo targetField = structType.GetField(capitalizedKey);
        if (targetField == null) {
            return;
        }
        nint offset = Marshal.OffsetOf(structType, targetField.Name);
        byte* dest = (byte*) destinationStruct + offset;

        Type destType = targetField.FieldType;
        if (destType.IsPrimitive) {
            // Primitives
            switch (destType) {
            case var _ when destType == typeof(char):
                *((char*) dest) = (char) value.UIntValue();
                break;
            case var _ when destType == typeof(byte):
                *((byte*) dest) = (byte) value.UIntValue();
                break;
            case var _ when destType == typeof(sbyte):
                *((sbyte*) dest) = (sbyte) value.IntValue();
                break;
            case var _ when destType == typeof(ushort):
                *((ushort*) dest) = (ushort) value.UIntValue();
                break;
            case var _ when destType == typeof(short):
                *((short*) dest) = (short) value.IntValue();
                break;
            case var _ when destType == typeof(uint):
                *((uint*) dest) = value.UIntValue();
                break;
            case var _ when destType == typeof(int):
                *((int*) dest) = value.IntValue();
                break;
            case var _ when destType == typeof(ulong):
                *((ulong*) dest) = (ulong) value.DoubleValue();
                break;
            case var _ when destType == typeof(long):
                *((long*) dest) = (long) value.DoubleValue();
                break;
            default:
                Debug.Log("Cannot write to unsupported type in component: " + destType.Name + " (" + structType.Name + "." + key + ")");
                throw new ArgumentException("Cannot write to unsupported type in component: " + destType.Name + " (" + structType.Name + "." + key + ")");
            }
        } else {
            // Structs
            if (value is ValMap map) {
                foreach (var field in destType.GetFields()) {
                    string miniscriptName;
                    if (field.Name.Length == 1) {
                        miniscriptName = field.Name.ToLower();
                    } else {
                        miniscriptName = char.ToLower(field.Name[0]) + field.Name[1..];
                    }

                    if (map.TryGetValue(miniscriptName, out Value nestedValue)) {
                        // Write this field
                        nint nestedOffset = Marshal.OffsetOf(destType, field.Name);
                        byte* nestedPtr = dest + nestedOffset;
                        TryWrite(field.FieldType, new IntPtr(nestedPtr), miniscriptName, nestedValue);
                    }
                }
            } else {
                Debug.Log("Cannot write non-map to struct in component: " + destType.Name + " (" + structType.Name + "." + key + " = " + value + ")");
                throw new ArgumentException("Cannot write non-map to struct in component: " + destType.Name + " (" + structType.Name + "." + key + " = " + value + ")");
            }
        }
    }

    public override FP Equality(Value rhs) {
        if (rhs is not ValComponentRef rhsComponentRef) {
            return 0;
        }

        return (EntityRef == rhsComponentRef.EntityRef
            && TypeIndex == rhsComponentRef.TypeIndex)
            ? 1 : 0;
    }

    public override int Hash() {
        return HashCodeUtils.CombineHashCodes(EntityRef.GetHashCode(), TypeIndex.GetHashCode());
    }

    public override string ToString(TAC.Machine vm) {
        try {
            return EntityRef.ToString() + "->" + ComponentTypeId.Type[TypeIndex].Name;
        } catch {
            return EntityRef.ToString() + "->Unknown(" + TypeIndex.ToString() + ")";
        }
    }
}