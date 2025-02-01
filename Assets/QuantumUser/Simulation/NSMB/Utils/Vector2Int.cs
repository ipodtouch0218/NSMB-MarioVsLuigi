using System;

namespace Quantum {
    [System.Serializable]
    public unsafe partial struct Vector2Int : IEquatable<Vector2Int> {

        public Vector2Int(int x, int y) {
            this.x = x;
            this.y = y;
        }

        public bool Equals(Vector2Int other) {
            return this.x == other.x && this.y == other.y;
        }

        public override bool Equals(object obj) {
            return obj is Vector2Int other && this.Equals(other);
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