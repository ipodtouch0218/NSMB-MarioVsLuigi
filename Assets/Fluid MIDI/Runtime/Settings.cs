using FluidSynth;
using System;
using UnityEngine;

namespace FluidMidi
{
    static class Settings
    {
        static int count = 0;

        public static IntPtr Ptr { get; private set; }

        public static void AddReference()
        {
            if (count == 0)
            {
                Logger.AddReference();
                Ptr = Api.Settings.Create();
                Api.Settings.Set(Ptr, Api.Settings.KEY_PLAYER_RESET_SYNTH, 0);
                Api.Settings.Set(Ptr, Api.Settings.KEY_SYNTH_SAMPLE_RATE, (double)AudioSettings.outputSampleRate);
                Api.Settings.Set(Ptr, Api.Settings.KEY_AUDIO_DRIVER, Api.Settings.VALUE_AUDIO_DRIVER_UNITY);
            }
            ++count;
        }

        public static void RemoveReference()
        {
            if (--count == 0)
            {
                Api.Settings.Destroy(Ptr);
                Logger.RemoveReference();
            }
        }
    }
}