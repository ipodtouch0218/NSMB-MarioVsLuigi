using NSMB.Translation;
using Photon.Deterministic;
using Quantum;
using Quantum.Prototypes;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

public class BinaryReplayFile {

    //---Helpers
    public static readonly string[] Versions = { "Invalid", "v1.8.0.0" };
    public const int CurrentVersion = 1;
    private static int MagicHeaderLength => Encoding.ASCII.GetByteCount(MagicHeader);
    private static readonly byte[] HeaderBuffer = new byte[MagicHeaderLength];

    // Header
    private const string MagicHeader = "MvLO-RP";
    public byte Version;
    public long UnixTimestamp;
    public int InitialFrameNumber;
    public int ReplayLengthInFrames;
    public string CustomName = "";
    public bool IsCompatible => Version == CurrentVersion;

    // Rules
    public GameRulesPrototype Rules;

    // Player information
    public byte Players;
    public sbyte WinningTeam = -1;
    public byte[] PlayerStars = new byte[10];
    public byte[] PlayerTeams = new byte[10];
    public string[] PlayerNames = new string[10];

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

        // Rules
        BinaryFormatter formatter = new();
        formatter.Serialize(output, Rules);

        // Players
        writer.Write(Players);
        writer.Write(WinningTeam);
        for (int i = 0; i < Players; i++) {
            writer.Write(PlayerNames[i]);
            writer.Write(PlayerStars[i]);
            writer.Write(PlayerTeams[i]);
        }

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

        if (QuantumUnityDB.TryGetGlobalAsset(Rules.Stage, out Map map)
            && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
            // We can find the map they're talking about
            return tm.GetTranslationWithReplacements("ui.extras.replays.defaultname", "playercount", Players.ToString(), "map", tm.GetTranslation(stage.TranslationKey));
        } else {
            return tm.GetTranslationWithReplacements("ui.extras.replays.defaultname.invalidmap", "playercount", Players.ToString());
        }
    }

    public Sprite GetMapSprite() {
        if (QuantumUnityDB.TryGetGlobalAsset(Rules.Stage, out Map map)
            && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
            return stage.Icon;
        }

        return null;
    }

    public static bool TryLoadFromFile(Stream input, out BinaryReplayFile result) {
        using BinaryReader reader = new(input, Encoding.ASCII);
        result = new();
        try {
            reader.Read(HeaderBuffer);
            string readString = Encoding.ASCII.GetString(HeaderBuffer);
            if (readString != MagicHeader) {
                throw new FormatException($"Error parsing replay file: Incorrect header! Got {readString}, expected {MagicHeader}!");
            }

            // Header is good!
            result.Version = reader.ReadByte();
            result.UnixTimestamp = reader.ReadInt64();
            result.InitialFrameNumber = reader.ReadInt32();
            result.ReplayLengthInFrames = reader.ReadInt32();
            result.CustomName = reader.ReadString();

            // Rules
            BinaryFormatter formatter = new();
            result.Rules = (GameRulesPrototype) formatter.Deserialize(input);

            // Players
            result.Players = reader.ReadByte();
            result.WinningTeam = reader.ReadSByte();
            for (int i = 0; i < result.Players; i++) {
                result.PlayerNames[i] = reader.ReadString();
                result.PlayerStars[i] = reader.ReadByte();
                result.PlayerTeams[i] = reader.ReadByte();
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

            return true;
        } catch (Exception e) {
            Debug.LogWarning(e);
            // result = null;
            return false;
        }
    }

    public static unsafe BinaryReplayFile FromReplayData(QuantumReplayFile replay, GameRules rules, byte players, string[] playernames, byte[] playerteams, byte[] playerstars, sbyte winnerIndex) {
        BinaryReplayFile result = new() {
            Version = CurrentVersion,
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

            Players = players,
            PlayerNames = playernames,
            PlayerTeams = playerteams,
            PlayerStars = playerstars,
            WinningTeam = winnerIndex,

            CompressedDeterministicConfigData = ByteUtils.GZipCompressBytes(DeterministicSessionConfig.ToByteArray(replay.DeterministicConfig)),
            CompressedRuntimeConfigData = ByteUtils.GZipCompressBytes(replay.RuntimeConfigData.Decode()),
            CompressedInitialFrameData = ByteUtils.GZipCompressBytes(replay.InitialFrameData),
            CompressedInputData = ByteUtils.GZipCompressBytes(replay.InputHistoryDeltaCompressed.Decode()),
        };

        return result;
    }
}