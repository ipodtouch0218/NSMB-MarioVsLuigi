using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text;
namespace RTLTMPro {
    public class FastStringBuilder {
        // Using fields to be as efficient as possible
        private int length;
        public int Length {
            get { return length; }
            set {
                if (value <= length) length = value;
            }
        }
        private int[] array;
        private int capacity;

        public FastStringBuilder(int capacity) {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            this.capacity = capacity;
            array = new int[capacity];
        }

        public FastStringBuilder(string text) : this(text, text.Length) {
        }

        public FastStringBuilder(string text, int capacity) : this(capacity) {
            SetValue(text);
        }

        public static implicit operator string(FastStringBuilder x) {
            return x.ToString();
        }

        public static implicit operator FastStringBuilder(string x) {
            return new FastStringBuilder(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index) {
            return array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, int ch) {
            array[index] = ch;
        }

        public void SetValue(string text) {
            int len = 0;
            length = text.Length;
            EnsureCapacity(length, false);

            for (int i = 0; i < text.Length; i++) {
                int unicode32CodePoint = char.ConvertToUtf32(text, i);
                if (unicode32CodePoint > 0xffff) {
                    i++;
                }
                array[len++] = unicode32CodePoint;
            }

            length = len;
        }

        public void SetValue(FastStringBuilder other) {
            EnsureCapacity(other.length, false);
            Copy(other.array, array);
            length = other.length;
        }

        public void Append(int ch) {
            length++;
            if (capacity < length)
                EnsureCapacity(length, true);

            array[length - 1] = ch;
        }

        public void Append(char ch) {
            length++;
            if (capacity < length)
                EnsureCapacity(length, true);

            array[length - 1] = ch;
        }

        public void Insert(int pos, FastStringBuilder str, int offset, int count) {
            if (str == this) throw new InvalidOperationException("You cannot pass the same string builder to insert");
            if (count == 0) return;

            length += count;
            EnsureCapacity(length, true);

            for (int i = length - count - 1; i >= pos; i--) {
                array[i + count] = array[i];
            }

            for (int i = 0; i < count; i++) {
                array[pos + i] = str.array[offset + i];
            }
        }

        public void Insert(int pos, FastStringBuilder str) {
            Insert(pos, str, 0, str.length);
        }

        public void Insert(int pos, int ch) {
            length++;
            EnsureCapacity(length, true);

            for (int i = length - 2; i >= pos; i--)
                array[i + 1] = array[i];

            array[pos] = ch;
        }

        public void RemoveAll(int character) {
            int j = 0; // write index
            int i = 0; // read index
            for (; i < length; i++) {
                if (array[i] == character) continue;

                array[j] = array[i];
                j++;
            }

            length = j;
        }

        public void Remove(int start, int length) {
            for (int i = start; i < this.length - length; i++) {
                array[i] = array[i + length];
            }

            this.length -= length;
        }

        public void Reverse(int startIndex, int length) {
            for (int i = 0; i < length / 2; i++) {
                int firstIndex = startIndex + i;
                int secondIndex = startIndex + length - i - 1;

                int first = array[firstIndex];
                int second = array[secondIndex];

                array[firstIndex] = second;
                array[secondIndex] = first;
            }
        }

        public void Reverse() {
            Reverse(0, length);
        }

        public void Substring(FastStringBuilder output, int start, int length) {
            output.length = 0;
            for (int i = 0; i < length; i++)
                output.Append(array[start + i]);
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < length; i++) {
                sb.Append(char.ConvertFromUtf32(array[i]));
            }
            return sb.ToString();
        }

        public string ToDebugString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                sb.Append("\\");
                sb.Append(array[i].ToString("X"));
            }
            return sb.ToString();
        }

        public void Replace(int oldChar, int newChar) {
            for (int i = 0; i < length; i++) {
                if (array[i] == oldChar)
                    array[i] = newChar;
            }
        }

        public void Replace(FastStringBuilder oldStr, FastStringBuilder newStr)
        {
            for (int i = 0; i < length; i++)
            {
                bool match = true;
                for (int j = 0; j < oldStr.Length; j++)
                {
                    if (array[i + j] != oldStr.Get(j))
                    {
                        match = false;
                        break;
                    }
                }

                if (!match) continue;

                if (oldStr.Length == newStr.Length)
                {
                    for (int k = 0; k < oldStr.Length; k++)
                    {
                        array[i + k] = newStr.Get(k);
                    }
                }
                else if (oldStr.Length < newStr.Length)
                {
                    // We need to expand capacity
                    int diff = newStr.Length - oldStr.Length;
                    length += diff;
                    EnsureCapacity(length, true);

                    // Move everything forward by difference of length
                    for (int k = length - diff - 1; k >= i + oldStr.Length; k--)
                    {
                        array[k + diff] = array[k];
                    }

                    // Start writing new string
                    for (int k = 0; k < newStr.Length; k++)
                    {
                        array[i + k] = newStr.Get(k);
                    }
                }
                else
                {
                    // We need to shrink
                    int diff = oldStr.Length - newStr.Length;

                    // Move everything backwards by diff
                    for (int k = i + diff; k < length - diff; k++)
                    {
                        array[k] = array[k + diff];
                    }

                    for (int k = 0; k < newStr.Length; k++)
                    {
                        array[i + k] = newStr.Get(k);
                    }

                    length -= diff;
                }

                i += newStr.Length;
            }
        }

        public void Clear() {
            length = 0;
        }

        private void EnsureCapacity(int cap, bool keepValues) {
            if (capacity >= cap)
                return;

            if (capacity == 0)
                capacity = 1;

            while (capacity < cap)
                capacity *= 2;

            if (keepValues) {
                int[] newArray = new int[capacity];
                Copy(array, newArray);
                array = newArray;
            } else {
                array = new int[capacity];
            }
        }

        private static void Copy(int[] src, int[] dst) {
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i];
        }
    }
}