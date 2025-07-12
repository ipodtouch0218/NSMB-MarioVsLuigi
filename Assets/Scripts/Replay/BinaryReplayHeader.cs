using Newtonsoft.Json;
using NSMB.UI.Translation;
using Quantum;
using Quantum.Prototypes;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NSMB.Replay {
    public class BinaryReplayHeader {

        //---Helpers
        private static GameVersion CachedCurrentVersion;
        private static int MagicHeaderLength => Encoding.ASCII.GetByteCount(MagicHeader);
        private static readonly byte[] MagicBuffer = new byte[MagicHeaderLength];

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


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ResetCachedVersion() {
            CachedCurrentVersion = GameVersion.Parse(Application.version);
        }

        internal long WriteToStream(Stream output) {
            using BinaryWriter writer = new(output, Encoding.ASCII, true);
            
            writer.Write(Encoding.ASCII.GetBytes(MagicHeader)); // Write the *bytes* to avoid wasteful \0 termination

            Version.Serialize(writer);
            writer.Write(UnixTimestamp);
            writer.Write(InitialFrameNumber);
            writer.Write(ReplayLengthInFrames);
            writer.Write(CustomName);

            // Rules
            writer.Write(JsonConvert.SerializeObject(Rules));

            // Players
            writer.Write((byte) PlayerInformation.Length);
            for (int i = 0; i < PlayerInformation.Length; i++) {
                PlayerInformation[i].Serialize(writer);
            }
            writer.Write(WinningTeam);

            return writer.BaseStream.Length;
        }

        internal static ReplayParseResult TryLoadFromFile(Stream input, out BinaryReplayHeader result) {
            using BinaryReader reader = new(input, Encoding.ASCII, true);
            
            result = new();

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
                result.Rules = JsonConvert.DeserializeObject<GameRulesPrototype>(reader.ReadString());
                if (result.Rules == null) {
                    return ReplayParseResult.ParseFailure;
                }

                // Players
                result.PlayerInformation = new ReplayPlayerInformation[reader.ReadByte()];
                for (int i = 0; i < result.PlayerInformation.Length; i++) {
                    result.PlayerInformation[i] = ReplayPlayerInformation.Deserialize(reader);
                }
                result.WinningTeam = reader.ReadSByte();
            } catch {
                return ReplayParseResult.ParseFailure;
            }
            return ReplayParseResult.Success;
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


        public static GameVersion GetCurrentVersion() {
            return CachedCurrentVersion;
        }
    }
}