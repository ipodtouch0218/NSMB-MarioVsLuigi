using System;
using UnityEngine;

namespace FluidMidi
{
    [Serializable]
    public class StreamingAsset
    {
        [SerializeField]
        string path = string.Empty;

        public void SetFullPath(string fullPath)
        {
            if (fullPath.StartsWith(Application.streamingAssetsPath))
            {
                path = fullPath.Substring(Application.streamingAssetsPath.Length + 1);
            }
            else
            {
                path = string.Empty;
            }
        }

        public string GetFullPath()
        {
            return path.Length > 0 ? Application.streamingAssetsPath + '/' + path : string.Empty;
        }

        public override string ToString()
        {
            return GetFullPath();
        }
    }
}