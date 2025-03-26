using System;
using System.IO;

public struct GameVersion : IEquatable<GameVersion>, IComparable<GameVersion> {
    public byte Major, Minor, Patch, Hotfix;

    public bool Equals(GameVersion other) {
        return Major == other.Major && Minor == other.Minor && Patch == other.Patch && Hotfix == other.Hotfix;
    }

    public bool EqualsIgnoreHotfix(GameVersion other) {
        return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    }

    public int CompareTo(GameVersion other) {
        if (Major != other.Major) {
            return Major - other.Major;
        }
        if (Minor != other.Minor) {
            return Minor - other.Minor;
        }
        if (Patch != other.Patch) {
            return Patch - other.Patch;
        }
        return Hotfix - other.Hotfix;
    }

    public static bool operator >(GameVersion x, GameVersion y) {
        return x.CompareTo(y) > 0;
    }

    public static bool operator <(GameVersion x, GameVersion y) {
        return x.CompareTo(y) < 0;
    }

    public static bool operator >=(GameVersion x, GameVersion y) {
        return x.CompareTo(y) >= 0;
    }

    public static bool operator <=(GameVersion x, GameVersion y) {
        return x.CompareTo(y) <= 0;
    }

    public override string ToString() {
        return $"{Major}.{Minor}.{Patch}.{Hotfix}";
    }

    public string ToStringIgnoreHotfix() {
        return $"{Major}.{Minor}.{Patch}";
    }

    public int GetHashCode(GameVersion obj) {
        // https://stackoverflow.com/a/1646913/19635374
        unchecked {
            int hash = 17;
            hash = hash * 31 + obj.Major.GetHashCode();
            hash = hash * 31 + obj.Minor.GetHashCode();
            hash = hash * 31 + obj.Patch.GetHashCode();
            hash = hash * 31 + obj.Hotfix.GetHashCode();
            return hash;
        }
    }

    public void Serialize(BinaryWriter writer) {
        writer.Write(Major);
        writer.Write(Minor);
        writer.Write(Patch);
        writer.Write(Hotfix);
    }

    public static GameVersion Deserialize(BinaryReader reader) {
        return new GameVersion {
            Major = reader.ReadByte(),
            Minor = reader.ReadByte(),
            Patch = reader.ReadByte(),
            Hotfix = reader.ReadByte(),
        };
    }


    public static GameVersion Parse(ReadOnlySpan<char> version) {
        Span<byte> parsed = stackalloc byte[4];
        if (version.StartsWith("v", StringComparison.InvariantCultureIgnoreCase)) {
            version = version[1..];
        }

        for (int i = 0; i < parsed.Length; i++) {
            int separator = version.IndexOf('.');
            if (separator == -1) {
                break;
            }

            byte.TryParse(version[..separator], out parsed[i]);
            version = version[(separator + 1)..];
        }

        return new GameVersion {
            Major = parsed[0],
            Minor = parsed[1],
            Patch = parsed[2],
            Hotfix = parsed[3],
        };
    }
}