using System.Collections.Generic;
using UnityEngine;

namespace FluidMidi
{
    public class Playlist : MonoBehaviour
    {
        [SerializeField]
        List<SongPlayer> songs = new List<SongPlayer>();

        int index = 0;

        public bool IsReady
        {
            get
            {
                return songs.Count == 0 || songs[0].IsReady;
            }
        }

        void Start()
        {
            Play();
        }

        void Update()
        {
            if (songs[index].IsDone)
            {
                ++index;
                Play();
            }
        }

        void Play()
        {
            while (index < songs.Count)
            {
                if (songs[index] != null)
                {
                    songs[index].Play();
                    return;
                }
                ++index;
            }
            enabled = false;
        }
    }
}