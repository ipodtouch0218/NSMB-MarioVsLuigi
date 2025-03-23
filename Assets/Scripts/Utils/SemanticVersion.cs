using System;
using System.IO;

public struct SemanticVersion : IEquatable<SemanticVersion>, IComparable<SemanticVersion> {
    public byte Major, Minor, Patch;

    public bool Equals(SemanticVersion other) {
        return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    }

    public bool Equals(SemanticVersion other, bool ignorePatch) {
        return Major == other.Major && Minor == other.Minor && (ignorePatch || Patch == other.Patch);
    }

    public int CompareTo(SemanticVersion other) {
        if (Major != other.Major) {
            return Major - other.Major;
        }
        if (Minor != other.Minor) {
            return Minor - other.Minor;
        }
        return Patch - other.Patch;
    }

    public static bool operator >(SemanticVersion x, SemanticVersion y) {
        return x.CompareTo(y) > 0;
    }

    public static bool operator <(SemanticVersion x, SemanticVersion y) {
        return x.CompareTo(y) < 0;
    }

    public static bool operator >=(SemanticVersion x, SemanticVersion y) {
        return x.CompareTo(y) >= 0;
    }

    public static bool operator <=(SemanticVersion x, SemanticVersion y) {
        return x.CompareTo(y) <= 0;
    }

    public override string ToString() {
        return $"{Major}.{Minor}.{Patch}";
    }

    public string ToStringNoPatch() {
        return $"{Major}.{Minor}";
    }

    public int GetHashCode(SemanticVersion obj) {
        // https://stackoverflow.com/a/1646913/19635374
        unchecked {
            int hash = 17;
            hash = hash * 31 + obj.Major.GetHashCode();
            hash = hash * 31 + obj.Minor.GetHashCode();
            hash = hash * 31 + obj.Patch.GetHashCode();
            return hash;
        }
    }

    public void Serialize(BinaryWriter writer) {
        writer.Write(Major);
        writer.Write(Minor);
        writer.Write(Patch);
    }

    public static SemanticVersion Deserialize(BinaryReader reader) {
        return new SemanticVersion {
            Major = reader.ReadByte(),
            Minor = reader.ReadByte(),
            Patch = reader.ReadByte()
        };
    }


    public static SemanticVersion Parse(ReadOnlySpan<char> version) {
        Span<byte> parsed = stackalloc byte[3];
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

        return new SemanticVersion {
            Major = parsed[0],
            Minor = parsed[1],
            Patch = parsed[2],
        };
    }
}