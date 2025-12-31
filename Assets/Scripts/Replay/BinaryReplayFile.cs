using Photon.Deterministic;
using Quantum;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

namespace NSMB.Replay {
    public unsafe class BinaryReplayFile {

        // Header
        public BinaryReplayHeader Header { get; private set; }

        // Variable length
        public byte[] CompressedRuntimeConfigData;
        public byte[] CompressedDeterministicConfigData;
        public byte[] CompressedInitialFrameData;
        public byte[] CompressedInputData;

        //---Properties (don't serialize)
        public string FilePath { get; set; }
        public long FileSize { get; private set; }
        public bool FullyLoaded => CompressedRuntimeConfigData != null;
        public byte[] DecompressedRuntimeConfigData => ByteUtils.GZipDecompressBytes(CompressedRuntimeConfigData);
        public byte[] DecompressedDeterministicConfigData => ByteUtils.GZipDecompressBytes(CompressedDeterministicConfigData);
        public byte[] DecompressedInitialFrameData => ByteUtils.GZipDecompressBytes(CompressedInitialFrameData);
        public byte[] DecompressedInputData => ByteUtils.GZipDecompressBytes(CompressedInputData);

        public long WriteToStream(Stream output) {
            if (!FullyLoaded) {
                throw new InvalidOperationException("Cannot write replay that's not fully loaded!");
            }

            using BinaryWriter writer = new(output, Encoding.UTF8, true);
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

            return (FileSize = writer.BaseStream.Length);
        }

        public ReplayParseResult LoadAllIfNeeded() {
            if (FullyLoaded) {
                return ReplayParseResult.Success;
            }
            try {
                using FileStream fs = new(FilePath, FileMode.Open);
                return LoadFromStream(fs, true);

            } catch (Exception e) {
                Debug.LogWarning("Failed to parse replay: " + e);
                return ReplayParseResult.ParseFailure;
            }
        }

        private ReplayParseResult LoadFromStream(Stream input, bool includeReplayData) {
            using BinaryReader reader = new(input, Encoding.UTF8);

            try {
                ReplayParseResult headerParseResult = BinaryReplayHeader.TryLoadFromFile(input, out var tempHeader);
                Header = tempHeader;
                if (headerParseResult != ReplayParseResult.Success) {
                    return headerParseResult;
                }

                if (includeReplayData) {
                    // Read variable-length data.
                    int runtimeConfigSize = reader.ReadInt32();
                    int deterministicConfigSize = reader.ReadInt32();
                    int initialFrameSize = reader.ReadInt32();
                    int inputDataSize = reader.ReadInt32();

                    CompressedRuntimeConfigData = reader.ReadBytes(runtimeConfigSize);
                    CompressedDeterministicConfigData = reader.ReadBytes(deterministicConfigSize);
                    CompressedInitialFrameData = reader.ReadBytes(initialFrameSize);
                    CompressedInputData = reader.ReadBytes(inputDataSize);
                }

                return ReplayParseResult.Success;
            } catch (Exception e) {
                Debug.LogWarning("Failed to parse replay: " + e);
                return ReplayParseResult.ParseFailure;
            }
        }

        public static ReplayParseResult TryLoadNewFromStream(Stream input, bool includeReplayData, out BinaryReplayFile result) {
            result = new();
            return result.LoadFromStream(input, includeReplayData);
        }

        public static ReplayParseResult TryLoadNewFromFile(string filepath, bool includeReplayData, out BinaryReplayFile result) {
            using FileStream fs = new(filepath, FileMode.Open);
            long length = fs.Length;

            ReplayParseResult parseResult = TryLoadNewFromStream(fs, includeReplayData, out result);
            result.FilePath = filepath;
            result.FileSize = length;
            return parseResult;
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
}