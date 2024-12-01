using NSMB.Translation;
using Photon.Deterministic;
using Quantum;
using System;
using System.IO;
using System.Text;
using UnityEngine;

public class BinaryReplayFile {

    //---Helpers
    private static readonly string[] Versions = { "Invalid", "v1.8.0.0" };
    public const int CurrentVersion = 1;
    private static int MagicHeaderLength => Encoding.ASCII.GetByteCount(MagicHeader);
    private static readonly byte[] HeaderBuffer = new byte[MagicHeaderLength];

    // Header
    private const string MagicHeader = "MvLO-RP";
    public byte Version;
    public long UnixTimestamp;
    public string CustomName = "";
    public AssetRef<Map> MapAssetRef;
    public byte Players;
    public int InitialFrameNumber;
    public int ReplayLengthInFrames;
    public bool IsCompatible => Version == CurrentVersion;

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
        using BinaryWriter writer = new(output, Encoding.ASCII);

        // Write header stuffs
        writer.Write(Encoding.ASCII.GetBytes(MagicHeader)); // Write the *bytes* to avoid wasteful \0 termination
        writer.Write(Version);
        writer.Write(UnixTimestamp);
        writer.Write(InitialFrameNumber);
        writer.Write(ReplayLengthInFrames);
        writer.Write(CustomName);
        writer.Write(MapAssetRef.Id.Value);
        writer.Write(Players);

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

        return writer.BaseStream.Length;
    }

    public string GetDisplayName(bool replaceNullWithDefault = true) {
        if (!replaceNullWithDefault || !string.IsNullOrWhiteSpace(CustomName)) {
            return CustomName;
        }

        return GetDefaultName();
    }

    public string GetDefaultName() {
        TranslationManager tm = GlobalController.Instance.translationManager;

        if (QuantumUnityDB.TryGetGlobalAsset(MapAssetRef, out Map map)
            && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
            // We can find the map they're talking about
            return tm.GetTranslationWithReplacements("ui.extras.replays.defaultname", "playercount", Players.ToString(), "map", tm.GetTranslation(stage.TranslationKey));
        } else {
            return tm.GetTranslationWithReplacements("ui.extras.replays.defaultname.invalidmap", "playercount", Players.ToString());
        }
    }

    public Sprite GetMapSprite() {
        if (QuantumUnityDB.TryGetGlobalAsset(MapAssetRef, out Map map)
            && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
            return stage.Icon;
        }

        return null;
    }

    public static bool TryLoadFromFile(Stream input, out BinaryReplayFile result) {
        using BinaryReader reader = new(input, Encoding.ASCII);
        try {
            reader.Read(HeaderBuffer);
            string readString = Encoding.ASCII.GetString(HeaderBuffer);
            if (readString != MagicHeader) {
                throw new FormatException($"Error parsing replay file: Incorrect header! Got {readString}, expected {MagicHeader}!");
            }

            // Header is good!
            result = new();
            result.Version = reader.ReadByte();
            result.UnixTimestamp = reader.ReadInt64();
            result.InitialFrameNumber = reader.ReadInt32();
            result.ReplayLengthInFrames = reader.ReadInt32();
            result.CustomName = reader.ReadString();
            result.MapAssetRef = (AssetGuid) reader.ReadInt64();
            result.Players = reader.ReadByte();

            // Read variable-length data.
            int runtimeConfigSize = reader.ReadInt32();
            int deterministicConfigSize = reader.ReadInt32();
            int initialFrameSize = reader.ReadInt32();
            int inputDataSize = reader.ReadInt32();

            result.CompressedRuntimeConfigData = reader.ReadBytes(runtimeConfigSize);
            result.CompressedDeterministicConfigData = reader.ReadBytes(deterministicConfigSize);
            result.CompressedInitialFrameData = reader.ReadBytes(initialFrameSize);
            result.CompressedInputData = reader.ReadBytes(inputDataSize);

            return true;
        } catch (Exception e) {
            Debug.LogWarning(e);
            result = null;
            return false;
        }
    }

    public static unsafe BinaryReplayFile FromReplayData(QuantumReplayFile replay, AssetRef<Map> map, byte players) {
        BinaryReplayFile result = new() {
            Version = CurrentVersion,
            UnixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            InitialFrameNumber = replay.InitialTick,
            ReplayLengthInFrames = replay.LastTick - replay.InitialTick,
            MapAssetRef = map,
            Players = players,
            CompressedDeterministicConfigData = ByteUtils.GZipCompressBytes(DeterministicSessionConfig.ToByteArray(replay.DeterministicConfig)),
            CompressedRuntimeConfigData = ByteUtils.GZipCompressBytes(replay.RuntimeConfigData.Decode()),
            CompressedInitialFrameData = ByteUtils.GZipCompressBytes(replay.InitialFrameData),
            CompressedInputData = ByteUtils.GZipCompressBytes(replay.InputHistoryDeltaCompressed.Decode()),
        };

        return result;
    }
}