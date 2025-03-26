using NSMB.Translation;
using Photon.Deterministic;
using Quantum;
using Quantum.Prototypes;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

public unsafe class BinaryReplayFile {

    //---Helpers
    private static GameVersion? CachedCurrentVersion;
    private static int MagicHeaderLength => Encoding.ASCII.GetByteCount(MagicHeader);
    private static readonly byte[] MagicBuffer = new byte[MagicHeaderLength];

    // Metadata
    public long FileSize { get; private set; }

    // Header
    private const string MagicHeader = "MvLO-RP";
    public GameVersion Version;
    public long UnixTimestamp;
    public int InitialFrameNumber;
    public int ReplayLengthInFrames;
    public string CustomName = "";
    public bool IsCompatible => Version.EqualsIgnoreHotfix(GetCurrentVersion()); // Major.Minor.Patch.Hotfix -> hotfix is for backwards compatible fixes.

    // Rules
    public GameRulesPrototype Rules;

    // Player information
    public ReplayPlayerInformation[] PlayerInformation = Array.Empty<ReplayPlayerInformation>();
    public sbyte WinningTeam = -1;

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
        BinaryFormatter formatter = new();

        writer.Write(Encoding.ASCII.GetBytes(MagicHeader)); // Write the *bytes* to avoid wasteful \0 termination

        Version.Serialize(writer);
        writer.Write(UnixTimestamp);
        writer.Write(InitialFrameNumber);
        writer.Write(ReplayLengthInFrames);
        writer.Write(CustomName);

        // Rules
        formatter.Serialize(output, Rules);

        // Players
        writer.Write((byte) PlayerInformation.Length);
        for (int i = 0; i < PlayerInformation.Length; i++) {
            PlayerInformation[i].Serialize(writer);
        }
        writer.Write(WinningTeam);

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

    public string GetDisplayName(bool replaceNullWithDefault = true) {
        if (!replaceNullWithDefault || !string.IsNullOrWhiteSpace(CustomName)) {
            return CustomName;
        }

        return GetDefaultName();
    }

    public string GetDefaultName() {
        TranslationManager tm = GlobalController.Instance.translationManager;
        int playerCount = PlayerInformation.Length;

        if (QuantumUnityDB.TryGetGlobalAsset(Rules.Stage, out Map map)
            && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
            // We can find the map they're talking about
            return tm.GetTranslationWithReplacements("ui.extras.replays.defaultname", "playercount", playerCount.ToString(), "map", tm.GetTranslation(stage.TranslationKey));
        } else {
            return tm.GetTranslationWithReplacements("ui.extras.replays.defaultname.invalidmap", "playercount", playerCount.ToString());
        }
    }

    public Sprite GetMapSprite() {
        if (QuantumUnityDB.TryGetGlobalAsset(Rules.Stage, out Map map)
            && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
            return stage.Icon;
        }

        return null;
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
        result.FileSize = reader.BaseStream.Length;

        try {
            reader.Read(MagicBuffer);
            string readString = Encoding.ASCII.GetString(MagicBuffer);
            if (readString != MagicHeader) {
                return ReplayParseResult.NotReplayFile;
            }

            // Header is good!
            result.Version = GameVersion.Deserialize(reader);
            result.UnixTimestamp = reader.ReadInt64();
            result.InitialFrameNumber = reader.ReadInt32();
            result.ReplayLengthInFrames = reader.ReadInt32();
            result.CustomName = reader.ReadString();

            // Rules
            result.Rules = (GameRulesPrototype) formatter.Deserialize(input);

            // Players
            result.PlayerInformation = new ReplayPlayerInformation[reader.ReadByte()];
            for (int i = 0; i < result.PlayerInformation.Length; i++) {
                result.PlayerInformation[i] = ReplayPlayerInformation.Deserialize(reader);
            }
            result.WinningTeam = reader.ReadSByte();

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
        } catch /* (Exception e) */ {
            // Debug.LogWarning("Failed to parse replay: " + e);
            // result = null;
            return ReplayParseResult.ParseFailure;
        }
    }

    public static unsafe BinaryReplayFile FromReplayData(QuantumReplayFile replay, GameRules rules, ReplayPlayerInformation[] playerInformation, sbyte winnerIndex) {
        BinaryReplayFile result = new() {
            Version = GetCurrentVersion(),
            UnixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            InitialFrameNumber = replay.InitialTick,
            ReplayLengthInFrames = replay.LastTick - replay.InitialTick,

            Rules = new GameRulesPrototype {
                Stage = rules.Stage,
                StarsToWin = rules.StarsToWin,
                CoinsForPowerup = rules.CoinsForPowerup,
                Lives = rules.Lives,
                TimerSeconds = rules.TimerSeconds,
                CustomPowerupsEnabled = rules.CustomPowerupsEnabled,
                TeamsEnabled = rules.TeamsEnabled,
            },

            PlayerInformation = playerInformation,
            WinningTeam = winnerIndex,

            CompressedDeterministicConfigData = ByteUtils.GZipCompressBytes(DeterministicSessionConfig.ToByteArray(replay.DeterministicConfig)),
            CompressedRuntimeConfigData = ByteUtils.GZipCompressBytes(replay.RuntimeConfigData.Decode()),
            CompressedInitialFrameData = ByteUtils.GZipCompressBytes(replay.InitialFrameData),
            CompressedInputData = ByteUtils.GZipCompressBytes(replay.InputHistoryDeltaCompressed.Decode()),
        };

        return result;
    }

    private static GameVersion GetCurrentVersion() {
        if (!CachedCurrentVersion.HasValue) {
            CachedCurrentVersion = GameVersion.Parse(Application.version);
        }

        return CachedCurrentVersion.Value;
    }

    public struct ReplayPlayerInformation {
        public string Username;
        public byte FinalStarCount;
        public byte Team;
        public byte Character;
        public PlayerRef PlayerRef;

        public void Serialize(BinaryWriter writer) {
            writer.Write(Username);
            writer.Write(FinalStarCount);
            writer.Write(Team);
            writer.Write(Character);
            writer.Write(PlayerRef);
        }

        public static ReplayPlayerInformation Deserialize(BinaryReader reader) {
            return new ReplayPlayerInformation {
                Username = reader.ReadString(),
                FinalStarCount = reader.ReadByte(),
                Team = reader.ReadByte(),
                Character = reader.ReadByte(),
                PlayerRef = reader.ReadInt32(),
            };
        }
    }
}