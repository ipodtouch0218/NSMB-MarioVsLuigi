using System;

namespace Quantum {
    [System.Serializable]
    public struct Vector2Int : IEquatable<Vector2Int> {
        public int x, y;

        public Vector2Int(int x, int y) {
            this.x = x;
            this.y = y;
        }

        public override bool Equals(object obj) {
            return obj is Vector2Int other && Equals(other);
        }

        public bool Equals(Vector2Int other) {
            return x == other.x && y == other.y;
        }

        public override int GetHashCode() {
            return HashCode.Combine(x, y);
        }

        public static bool operator ==(Vector2Int a, Vector2Int b) {
            return a.Equals(b);
        }

        public static bool operator !=(Vector2Int a, Vector2Int b) {
            return !a.Equals(b);
        }

        public static Vector2Int operator +(Vector2Int a, Vector2Int b) {
            return new Vector2Int(a.x + b.x, a.y + b.y);
        }

        public static Vector2Int operator -(Vector2Int a, Vector2Int b) {
            return new Vector2Int(a.x - b.x, a.y - b.y);
        }

#if QUANTUM_UNITY
        public static explicit operator UnityEngine.Vector2(Vector2Int a) {
            return new UnityEngine.Vector2(a.x, a.y);
        } 
#endif
    }
}