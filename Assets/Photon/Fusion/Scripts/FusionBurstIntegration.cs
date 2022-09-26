#if FUSION_BURST
using Fusion;
using Fusion.Sockets;
using Unity.Burst;
using UnityEngine;

[BurstCompile]
public static unsafe class FusionBurstIntegration {
  public delegate void PackDelegate(int* current, int* shared, int words, NetBitBuffer* buffer);

  public delegate void UnpackDelegate(int* target, int bitCount, NetBitBuffer* buffer);

  class DeltaCompressor : Simulation.IDeltaCompressor {
    public Simulation.IDeltaCompressor Default;

    public PackDelegate   PackDelegate;
    public UnpackDelegate UnpackDelegate;

    void Simulation.IDeltaCompressor.Pack(int* current, int* shared, int words, NetBitBuffer* buffer) {
      PackDelegate(current, shared, words, buffer);
    }

    void Simulation.IDeltaCompressor.Unpack(int* target, int bitCount, NetBitBuffer* buffer) {
      Default.Unpack(target, bitCount, buffer);
    }
  }

  [RuntimeInitializeOnLoadMethod]
  public static void Init() {
    DeltaCompressor compressor;

    compressor                = new DeltaCompressor();
    compressor.Default        = Simulation.GetDefaultDeltaCompressor();
    compressor.PackDelegate   = BurstCompiler.CompileFunctionPointer<PackDelegate>(Pack).Invoke;
    compressor.UnpackDelegate = BurstCompiler.CompileFunctionPointer<UnpackDelegate>(Unpack).Invoke;

    // set on runner
    NetworkRunner.BurstDeltaCompressor = compressor;
    
    // burst aoi resolver
    Simulation.AreaOfInterest.BurstInsertAndResolve = BurstCompiler.CompileFunctionPointer<Simulation.AreaOfInterest.BurstInsertAndResolveDelegate>(BurstAreaOfInterestInsertAndResovleDelegate).Invoke;
  }

  static readonly byte[] _debruijnTable32 = {
    0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
    8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
  };

  static int BitScanReverse(uint v) {
    v |= v >> 1;
    v |= v >> 2;
    v |= v >> 4;
    v |= v >> 8;
    v |= v >> 16;
    return _debruijnTable32[(v * 0x07C4ACDDU) >> 27];
  }

  static readonly int[] _debruijnTable64 = new int[128] {
    0,
    48, -1, -1, 31, -1, 15, 51, -1, 63, 5, -1, -1, -1, 19, -1,
    23, 28, -1, -1, -1, 40, 36, 46, -1, 13, -1, -1, -1, 34, -1, 58,
    -1, 60, 2, 43, 55, -1, -1, -1, 50, 62, 4, -1, 18, 27, -1, 39,
    45, -1, -1, 33, 57, -1, 1, 54, -1, 49, -1, 17, -1, -1, 32, -1,
    53, -1, 16, -1, -1, 52, -1, -1, -1, 64, 6, 7, 8, -1, 9, -1,
    -1, -1, 20, 10, -1, -1, 24, -1, 29, -1, -1, 21, -1, 11, -1, -1,
    41, -1, 25, 37, -1, 47, -1, 30, 14, -1, -1, -1, -1, 22, -1, -1,
    35, 12, -1, -1, -1, 59, 42, -1, -1, 61, 3, 26, 38, 44, -1, 56
  };

  static int BitScanReverse(ulong v) {
    v |= v >> 1;
    v |= v >> 2;
    v |= v >> 4;
    v |= v >> 8;
    v |= v >> 16;
    v |= v >> 32;
    return _debruijnTable64[(v * 0x6c04f118e9966f6bUL) >> 57];
  }

  const int   BITCOUNT   = 64;
  const int   USEDMASK   = BITCOUNT - 1;
  const int   INDEXSHIFT = 6;
  const ulong MAXVALUE   = ulong.MaxValue;


  static void WriteUInt32VarLength(NetBitBuffer* b, uint value, int blockSize) {
    // how many blocks
    var blocks = (BitScanReverse(value) + blockSize) / blockSize;

    // write data
    Write(b, 1U << (blocks - 1), blocks);
    Write(b, value, blocks * blockSize);
  }

  static void WriteUInt64VarLength(NetBitBuffer* b, ulong value, int blockSize) {
    // how many blocks
    var blocks = (BitScanReverse(value) + blockSize) / blockSize;

    // write data
    Write(b, 1U << (blocks - 1), blocks);
    Write(b, value, blocks * blockSize);
  }

  static void Write(NetBitBuffer* buffer, ulong value, int bits) {
    var bitsUsed = buffer->OffsetBitsUnsafe & USEDMASK;
    var bitsFree = BITCOUNT - bitsUsed;

    var b = buffer->Data + (buffer->OffsetBitsUnsafe >> INDEXSHIFT);

    *b = (*b & ((1UL << bitsUsed) - 1UL)) | (value << bitsUsed);

    if (bitsFree < bits) {
      *(b + 1) = value >> bitsFree;
    }

    buffer->OffsetBitsUnsafe += bits;
  }

  [BurstCompile]
  public static void BurstAreaOfInterestInsertAndResovleDelegate(Simulation.AreaOfInterest* aoi, Accuracy* accuracy, Allocator* allocator, NetworkObjectRefMapPtr* map) {
    Simulation.AreaOfInterest.InsertObjects(aoi, *accuracy, allocator, map);
    Simulation.AreaOfInterest.Resolve(aoi, allocator);
  }

  [BurstCompile]
  public static void Pack(int* current, int* shared, int words, NetBitBuffer* buffer) {
    var offset = 0;

    for (int i = 0; i < words; ++i) {
      if (shared[i] != current[i]) {
        var d = current[i] - (long) shared[i];

        WriteUInt32VarLength(buffer, unchecked((uint) (i - offset)), Simulation.DEFAULT_COMPRESSOR_OFFSET_BLOCK_SIZE);
        WriteUInt64VarLength(buffer, unchecked((ulong) ((d >> 63) ^ (d << 1))), Simulation.DEFAULT_COMPRESSOR_VALUE_BLOCK_SIZE);

        offset = i;
      }
    }
  }

  [BurstCompile]
  public static void Unpack(int* target, int bitCount, NetBitBuffer* buffer) {
  }
}
#endif