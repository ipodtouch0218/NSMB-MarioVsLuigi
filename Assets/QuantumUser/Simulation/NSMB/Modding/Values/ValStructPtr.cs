using Miniscript;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public unsafe class ValStructPtr : ValMap {

    private static readonly Dictionary<Type, CacheTypeInfo> TypeCache = new();

    //---Properties
    public override int Count {
        get {
            if (!TypeCache.TryGetValue(Type, out var cache)) {
                // Miss
                cache = TypeCache[Type] = new();
            }
            if (cache.RealFieldCount < 0) {
                cache.RealFieldCount = Type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
            }
            return cache.RealFieldCount;
        }
    }

    //---Public Variables
    public Type Type;
    public byte* Origin;

    public ValStructPtr(Type type, void* origin) {
        Type = type;
        Origin = (byte*) origin;

        evalOverride = (ValMap self, Value key, out Value outValue) => {
            if (!TryFindMember(Type, key, out var memberCache)) {
                outValue = null;
                return true;
            }

            Type memberType = memberCache.Type;
            if (memberCache is CachePropertyInfo propertyCache) {
                PropertyInfo prop = propertyCache.PropertyInfo;
                object boxed = Marshal.PtrToStructure(new IntPtr(Origin), Type);
                object value = prop.GetValue(boxed);

                switch (memberType) {
                // Natively support Quantum-compatible primitives
                case var _ when memberType == typeof(bool): outValue = (bool) value ? ValNumber.one : ValNumber.zero; break;
                case var _ when memberType == typeof(char): outValue = new ValNumber((char) value); break;
                case var _ when memberType == typeof(byte): outValue = new ValNumber((byte) value); break;
                case var _ when memberType == typeof(sbyte): outValue = new ValNumber((sbyte) value); break;
                case var _ when memberType == typeof(ushort): outValue = new ValNumber((ushort) value); break;
                case var _ when memberType == typeof(short): outValue = new ValNumber((short) value); break;
                case var _ when memberType == typeof(uint): outValue = new ValNumber((uint) value); break;
                case var _ when memberType == typeof(int): outValue = new ValNumber((int) value); break;
                case var _ when memberType == typeof(ulong): outValue = new ValNumber((uint) ((ulong) value)); break;
                case var _ when memberType == typeof(long): outValue = new ValNumber((int) ((long) value)); break;

                // Natively support some Quantum types
                case var _ when memberType == typeof(QBoolean): outValue = (QBoolean) value ? ValNumber.one : ValNumber.zero; break;
                case var _ when memberType == typeof(FP): outValue = new ValNumber((FP) value); break;
                case var _ when memberType == typeof(EntityRef): outValue = new ValEntityRef { EntityRef = (EntityRef) value }; break;
                case var _ when memberType == typeof(PlayerRef): outValue = new ValNumber((int) (PlayerRef) value); break;
                case var _ when memberType == typeof(AssetRef): outValue = new ValAssetRef { Asset = (AssetRef) value }; break;
                case var _ when memberType == typeof(PhysicsQueryRef): outValue = new ValPhysicsQueryRef { PhysicsQuery = (PhysicsQueryRef) value }; break;
                case var _ when memberType == typeof(string):
                case var _ when typeof(IQString).IsAssignableFrom(memberType):
                case var _ when typeof(IQStringUtf8).IsAssignableFrom(memberType): outValue = new ValString((string) value); break;

                // Error for unsupported types
                default: throw new ArgumentException($"Cannot read unsupported primitive type {memberType.Name} in {Type.Name}.{key}");
                }
            } else if (memberCache is CacheFieldInfo fieldCache) {
                byte* memberOrigin = Origin + fieldCache.Offset;

                switch (memberType) {
                // Natively support Quantum-compatible primitives
                case var _ when memberType == typeof(bool): outValue = *(bool*) memberOrigin ? ValNumber.one : ValNumber.zero; break;
                case var _ when memberType == typeof(char): outValue = new ValNumber(*(char*) memberOrigin); break;
                case var _ when memberType == typeof(byte): outValue = new ValNumber(*memberOrigin); break;
                case var _ when memberType == typeof(sbyte): outValue = new ValNumber(*(sbyte*) memberOrigin); break;
                case var _ when memberType == typeof(ushort): outValue = new ValNumber(*(ushort*) memberOrigin); break;
                case var _ when memberType == typeof(short): outValue = new ValNumber(*(short*) memberOrigin); break;
                case var _ when memberType == typeof(uint): outValue = new ValNumber(*(uint*) memberOrigin); break;
                case var _ when memberType == typeof(int): outValue = new ValNumber(*(int*) memberOrigin); break;
                case var _ when memberType == typeof(ulong): outValue = new ValNumber((uint) (*(ulong*) memberOrigin)); break;
                case var _ when memberType == typeof(long): outValue = new ValNumber((int) (*(long*) memberOrigin)); break;

                // Natively support some Quantum types
                case var _ when memberType == typeof(QBoolean): outValue = *(QBoolean*) memberOrigin ? ValNumber.one : ValNumber.zero; break;
                case var _ when memberType == typeof(FP): outValue = new ValNumber(*(FP*) memberOrigin); break;
                case var _ when memberType == typeof(EntityRef): outValue = new ValEntityRef { EntityRef = *(EntityRef*) memberOrigin }; break;
                case var _ when memberType == typeof(PlayerRef): outValue = new ValNumber((int) *(PlayerRef*) memberOrigin); break;
                case var _ when memberType == typeof(AssetRef): outValue = new ValAssetRef { Asset = *(AssetRef*) memberOrigin }; break;
                case var _ when memberType == typeof(PhysicsQueryRef): outValue = new ValPhysicsQueryRef { PhysicsQuery = *(PhysicsQueryRef*) memberOrigin }; break;
                case var _ when typeof(IQString).IsAssignableFrom(memberType): outValue = new ValString(Marshal.PtrToStructure(new IntPtr(memberOrigin), memberType).ToString()); break;
                case var _ when typeof(IQStringUtf8).IsAssignableFrom(memberType): outValue = new ValString(Marshal.PtrToStructure(new IntPtr(memberOrigin), memberType).ToString()); break;

                // Fallback to nested structs
                case var _ when !memberType.IsPrimitive:
                    outValue = new ValStructPtr(memberType, memberOrigin);
                    break;

                // Error for unsupported primitives
                default: throw new ArgumentException($"Cannot read unsupported primitive type {memberType.Name} in {Type.Name}.{key}");
                }
            } else {
                outValue = null;
                return true;
            }
            return true;
        };
        assignOverride = (ValMap self, Value key, Value value) => {
            Assign(Type, Origin, key, value);
            return true;
        };
    }
    
    private void Assign(Type type, byte* origin, Value key, Value value) {
        if (!TryFindMember(type, key, out var memberCache)) {
            return;
        }

        if (memberCache is CacheFieldInfo fieldCache) {
            byte* fieldOrigin = origin + fieldCache.Offset;
            Type fieldType = fieldCache.Type;
            if (fieldType == null) {
                Debug.LogError("fieldtype is null!");
            }

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
            case var _ when fieldType == typeof(PlayerRef): *(PlayerRef*) fieldOrigin = value.IntValue(); break;
            case var _ when fieldType == typeof(AssetRef): *(AssetRef*) fieldOrigin = (value as ValAssetRef)?.Asset ?? default; break;
            case var _ when fieldType == typeof(PhysicsQueryRef): *(PhysicsQueryRef*) fieldOrigin = (value as ValPhysicsQueryRef)?.PhysicsQuery ?? default; break;
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

            // Fall-back to nested structs
            case var _ when !fieldType.IsPrimitive:
                if (value is not ValMap valueMap) {
                    break;
                }

                foreach (var (k, v) in valueMap.map) {
                    Assign(fieldType, fieldOrigin, k, v);
                }
                break;

            // Error for unsupported primitives
            default:
                throw new ArgumentException($"Cannot write to unsupported primitive type {fieldType.Name} in {Type.Name}.{key}");
            }
        } else if (memberCache is CachePropertyInfo prop) {
            throw new ArgumentException($"Writing to properties is not yet implemented! ({Type.Name}.{prop.Type.Name})");
        }

        return;
    }

    private bool TryFindMember(Type type, Value key, out CacheInfo cacheInfo) {
        string keyStr = key.ToString();
        if (string.IsNullOrEmpty(keyStr)) {
            cacheInfo = null;
            return false;
        }

        if (!TypeCache.TryGetValue(type, out var cache)) {
            // Miss
            TypeCache[type] = cache = new();
        } else if (cache.Members.TryGetValue(keyStr, out cacheInfo)) {
            // Hit
            return true;
        }

        // Check for field
        FieldInfo field = type.GetField(keyStr, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (field != null) {
            cache.Members[keyStr] = cacheInfo = new CacheFieldInfo();
            cacheInfo.Type = field.FieldType;
            ((CacheFieldInfo) cacheInfo).Offset = Marshal.OffsetOf(type, field.Name);
            return true;
        } 

        // Check for property
        PropertyInfo property = type.GetProperty(keyStr, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property != null) {
            cache.Members[keyStr] = cacheInfo = new CachePropertyInfo();
            cacheInfo.Type = property.PropertyType;
            ((CachePropertyInfo) cacheInfo).PropertyInfo = property;
            return true;
        }
            
        cache.Members[keyStr] = cacheInfo = null;
        return false;
    }

    private class CacheTypeInfo {
        public Dictionary<string, CacheInfo> Members = new(StringComparer.OrdinalIgnoreCase);
        public int RealFieldCount = -1;
    }

    private class CacheInfo {
        public Type Type;
    }

    private class CacheFieldInfo : CacheInfo {
        public nint Offset;
    }

    private class CachePropertyInfo : CacheInfo {
        public PropertyInfo PropertyInfo;
    }
}