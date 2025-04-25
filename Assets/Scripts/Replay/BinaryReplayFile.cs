using Photon.Deterministic;
using Quantum;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

public unsafe class BinaryReplayFile {

    // Header
    public BinaryReplayHeader Header { get; private set; }

    // Variable length
    public byte[] CompressedRuntimeConfigData;
    public byte[] CompressedDeterministicConfigData;    
    public byte[] CompressedInitialFrameData;
    public byte[] CompressedInputData;

    public byte[] DecompressedRuntimeConfigData => ByteUtils.GZipDecompressBytes(CompressedRuntimeConfigData);
    public byte[] DecompressedDeterministicConfigData => ByteUtils.GZipDecompressBytes(CompressedDeterministicConfigData);
    public byte[] DecompressedInitialFrameData => ByteUtils.GZipDecompressBytes(CompressedInitialFrameData);
    public byte[] DecompressedInputData => ByteUtils.GZipDecompressBytes(CompressedInputData);

    public long WriteToStream(Stream output) {
        using BinaryWriter writer = new(output, Encoding.ASCII, true);
        Header.WriteToStream(output);

        // Write variable-length data lengths
        writer.Write(CompressedRuntimeConfigData.Length);
        writer.Write(CompressedDeterministicConfigData.Length);
        writer.Write(CompressedInitialFrameData.Length);
        writer.Write(CompressedInputData.Length);

        // Write variable-length data
        writer.Write(CompressedRuntimeConfigData);
        writer.Write(CompressedDeterministicConfigData);
        writer.Write(CompressedInitialFrameData);
        writer.Write(CompressedInputData);

        writer.Flush();

        return writer.BaseStream.Length;
    }

    public enum ReplayParseResult {
        NotReplayFile,
        ParseFailure,
        Success
    }

    public static ReplayParseResult TryLoadFromFile(Stream input, out BinaryReplayFile result) {
        using BinaryReader reader = new(input, Encoding.ASCII);
        BinaryFormatter formatter = new();

        result = new();

        try {
            ReplayParseResult headerParseResult = BinaryReplayHeader.TryLoadFromFile(input, out var tempHeader);
            result.Header = tempHeader;
            if (headerParseResult != ReplayParseResult.Success) {
                return headerParseResult;
            }

            // Read variable-length data.
            int runtimeConfigSize = reader.ReadInt32();
            int deterministicConfigSize = reader.ReadInt32();
            int initialFrameSize = reader.ReadInt32();
            int inputDataSize = reader.ReadInt32();

            result.CompressedRuntimeConfigData = reader.ReadBytes(runtimeConfigSize);
            result.CompressedDeterministicConfigData = reader.ReadBytes(deterministicConfigSize);
            result.CompressedInitialFrameData = reader.ReadBytes(initialFrameSize);
            result.CompressedInputData = reader.ReadBytes(inputDataSize);
            
            return ReplayParseResult.Success;
        } catch (Exception e) {
            Debug.LogWarning("Failed to parse replay: " + e);
            // result = null;
            return ReplayParseResult.ParseFailure;
        }
    }

    public static unsafe BinaryReplayFile FromReplayData(QuantumReplayFile replay, BinaryReplayHeader header) {
        BinaryReplayFile result = new() {
            Header = header,
            CompressedDeterministicConfigData = ByteUtils.GZipCompressBytes(DeterministicSessionConfig.ToByteArray(replay.DeterministicConfig)),
            CompressedRuntimeConfigData = replay.RuntimeConfigData.Decode(),
            CompressedInitialFrameData = ByteUtils.GZipCompressBytes(replay.InitialFrameData),
            CompressedInputData = ByteUtils.GZipCompressBytes(replay.InputHistoryDeltaCompressed.Decode()),
        };

        return result;
    }
}