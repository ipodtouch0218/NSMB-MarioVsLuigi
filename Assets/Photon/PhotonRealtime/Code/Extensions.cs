// ----------------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Helpful methods and extensions for Hashtables, etc.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using System.Collections;
	using System.Collections.Generic;
    using Photon.Client;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>
    /// This static class defines some useful extension methods for several existing classes (e.g. Vector3, float and others).
    /// </summary>
    public static class Extensions
    {
        /// <summary>Used to get a "stable" hashcode for strings.</summary>
        /// <remarks>Used by RealtimeClient.GetMatchmakingHash for matchmaking debugging.</remarks>
        /// <see href="https://stackoverflow.com/questions/36845430/persistent-hashcode-for-strings"/>
        /// <param name="str">String to create hashcode for.</param>
        /// <returns>Hashcode of string.</returns>
        public static int GetStableHashCode(this string str)
        {
            // https://stackoverflow.com/questions/36845430/persistent-hashcode-for-strings
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }


        /// <summary>Helper method for debugging of IDictionary content, including type-information. Using this is not performant.</summary>
        /// <remarks>Should only be used for debugging as necessary.</remarks>
        /// <param name="origin">A PhotonHashtable.</param>
        /// <returns>String of the content of the IDictionary.</returns>
        public static string ToStringFull(this PhotonHashtable origin)
        {
            return SupportClass.DictionaryToString(origin, false);
        }

        /// <summary>Helper method for debugging of List&lt;T&gt; content. Using this is not performant.</summary>
        /// <remarks>Should only be used for debugging as necessary.</remarks>
        /// <param name="data">Any List&lt;T&gt; where T implements .ToString().</param>
        /// <returns>A comma-separated string containing each value's ToString().</returns>
        public static string ToStringFull<T>(this List<T> data)
		{
			if (data == null) return "null";

			string[] sb = new string[data.Count];
			for (int i = 0; i < data.Count; i++)
			{
				object o = data[i];
				sb[i] = (o != null) ? o.ToString() : "null";
			}

			return string.Join(", ", sb);
		}

        /// <summary>
        /// Converts a byte-array to string (useful as debugging output).
        /// Uses BitConverter.ToString(list) internally after a null-check of list.
        /// </summary>
        /// <param name="list">Byte-array to convert to string.</param>
        /// <param name="count">Count of bytes to convert to string. If negative, list.Length is used. Optional. Default: -1. </param>
        /// <returns>
        /// List of bytes as string.
        /// </returns>
        public static string ToStringFull(this byte[] list, int count = -1)
        {
            return SupportClass.ByteArrayToString(list, count);
        }

        /// <summary>
        /// Converts an ArraySegment&lt;byte&gt; to string (useful as debugging output).
        /// Uses BitConverter.ToString(segment) internally.
        /// </summary>
        /// <param name="segment">ArraySegment&lt;byte&gt; to convert to string.</param>
        /// <param name="count">Count of bytes to convert to string. If negative segment.Count is used. Optional. Default: -1. </param>
        /// <returns>
        /// List of bytes as string.
        /// </returns>
        public static string ToStringFull(this ArraySegment<byte> segment, int count = -1)
        {
            if (count < 0 || count > segment.Count)
            {
                count = segment.Count;
            }

            return BitConverter.ToString(segment.Array, segment.Offset, count);
        }

        /// <summary>Helper method for debugging of object[] content. Using this is not performant.</summary>
        /// <remarks>Should only be used for debugging as necessary.</remarks>
        /// <param name="data">Any object[].</param>
        /// <returns>A comma-separated string containing each value's ToString().</returns>
        public static string ToStringFull(this object[] data)
        {
            if (data == null) return "null";

            string[] sb = new string[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                object o = data[i];
                sb[i] = (o != null) ? o.ToString() : "null";
            }

            return string.Join(", ", sb);
        }


        /// <summary>
        /// Checks if the given IDictionary only contains keys of type string (to be acceptable as Custom Properties).
        /// </summary>
        /// <param name="original">Collection to check.</param>
        /// <param name="NullOrZeroAccepted">If it's ok that the hashtable is null or empty. Defines the return value for those cases.</param>
        /// <returns>NullOrZeroAccepted if the original is null or empty. True if existing keys are of type string or int.</returns>
        public static bool CustomPropKeyTypesValid(this PhotonHashtable original, bool NullOrZeroAccepted = false)
        {
            if (original == null || original.Count == 0)
            {
                return NullOrZeroAccepted;
            }

            foreach (var entry in original)
            {
                if (!(entry.Key is string || entry.Key is int))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the given object[] only contains keys of type string (to be acceptable as Custom Properties in lobby).
        /// </summary>
        public static bool CustomPropKeyTypesValid(this object[] array, bool NullOrZeroAccepted = false)
        {
            if (array == null || array.Length == 0)
            {
                return NullOrZeroAccepted;
            }

            foreach (var entry in array)
            {
                if (!(entry is string || entry is int))
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>Used by StripKeysWithNullValues.</summary>
        /// <remarks>
        /// By making keysWithNullValue a static variable to clear before using, allocations only happen during the warm-up phase
        /// as the list needs to grow. Once it hit the high water mark for keys you need to remove.
        /// </remarks>
        private static readonly List<object> keysWithNullValue = new List<object>();

        /// <summary>Removes all keys with null values.</summary>
        /// <remarks>
        /// Photon properties are removed by setting their value to null. Changes the original IDictionary!
        /// Uses lock(keysWithNullValue), which should be no problem in expected use cases.
        /// </remarks>
        /// <param name="original">The IDictionary to strip of keys with null value.</param>
        public static void StripKeysWithNullValues(this PhotonHashtable original)
        {
            lock (keysWithNullValue)
            {
                keysWithNullValue.Clear();

                foreach (var entry in original)
                {
                    if (entry.Value == null)
                    {
                        keysWithNullValue.Add(entry.Key);
                    }
                }

                for (int i = 0; i < keysWithNullValue.Count; i++)
                {
                    var key = keysWithNullValue[i];
                    original.Remove(key);
                }
            }
        }

        /// <summary>
        /// Merges all keys from addHash into the target. Adds new keys and updates the values of existing keys in target.
        /// </summary>
        /// <param name="target">The IDictionary to update.</param>
        /// <param name="addHash">The IDictionary containing data to merge into target.</param>
        public static void Merge(this PhotonHashtable target, PhotonHashtable addHash)
        {
            if (addHash == null || target.Equals(addHash))
            {
                return;
            }

            foreach (object key in addHash.Keys)
            {
                target[key] = addHash[key];
            }
        }

        /// <summary>
        /// Merges keys of type string to target PhotonHashtable.
        /// </summary>
        /// <remarks>
        /// Does not remove keys from target (so non-string keys CAN be in target if they were before).
        /// </remarks>
        /// <param name="target">The target IDictionary passed in plus all string-typed keys from the addHash.</param>
        /// <param name="addHash">A IDictionary that should be merged partly into target to update it.</param>
        public static void MergeStringKeys(this PhotonHashtable target, PhotonHashtable addHash)
        {
            if (addHash == null || target.Equals(addHash))
            {
                return;
            }

            foreach (var entry in addHash)
            {
                // only merge keys of type string
                if (entry.Key is string)
                {
                    target[entry.Key] = entry.Value;
                }
            }
        }


        /// <summary>
        /// Checks if a particular integer value is in an int-array.
        /// </summary>
        /// <remarks>This might be useful to look up if a particular actorNumber is in the list of players of a room.</remarks>
        /// <param name="target">The array of ints to check.</param>
        /// <param name="nr">The number to lookup in target.</param>
        /// <returns>True if nr was found in target.</returns>
        public static bool Contains(this int[] target, int nr)
        {
            if (target == null)
            {
                return false;
            }

            for (int index = 0; index < target.Length; index++)
            {
                if (target[index] == nr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

