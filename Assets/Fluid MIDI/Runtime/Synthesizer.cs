using FluidSynth;
using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace FluidMidi
{
    public class Synthesizer : MonoBehaviour
    {
        [SerializeField]
        StreamingAsset soundFont = new StreamingAsset();

        struct LoadSoundFontJob : IJob
        {
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            readonly IntPtr synth;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            readonly NativeArray<char> path;

            public LoadSoundFontJob(IntPtr synth, string path)
            {
                this.synth = synth;
                this.path = new NativeArray<char>(path.ToCharArray(), Allocator.Persistent);
            }

            public void Execute()
            {
                string pathString = new string(path.ToArray());
                if (pathString.Length > 0)
                {
                    if (File.Exists(pathString))
                    {
                        Logger.Log("Loading sound font: " + pathString);
                        Api.Synth.LoadSoundFont(synth, pathString, 0);
                    }
                    else
                    {
                        Logger.LogError("Sound font file missing: " + pathString);
                    }
                }
                else
                {
                    Logger.LogError("No sound font specified");
                }
            }
        }

        int count;
        IntPtr synthPtr;
        JobHandle loadSoundFontJob;

        internal IntPtr SoundFontPtr
        {
            get
            {
                return loadSoundFontJob.IsCompleted ? Api.Synth.GetSoundFont(synthPtr, 0) : IntPtr.Zero;
            }
        }

        internal void AddReference()
        {
            if (count == 0)
            {
                Logger.AddReference();
                Settings.AddReference();
                synthPtr = Api.Synth.Create(Settings.Ptr);
                loadSoundFontJob = new LoadSoundFontJob(synthPtr, soundFont.GetFullPath()).Schedule();
            }
            ++count;
        }

        internal void RemoveReference()
        {
            if (--count == 0)
            {
                if (!loadSoundFontJob.IsCompleted)
                {
                    Logger.LogWarning("Destroying Synthesizer before sound font loaded");
                }
                loadSoundFontJob.Complete();
                Api.Synth.Destroy(synthPtr);
                Settings.RemoveReference();
                Logger.RemoveReference();
            }
        }

        void OnEnable()
        {
            AddReference();
        }

        void OnDisable()
        {
            RemoveReference();
        }

        void OnValidate()
        {
            string soundFontPath = soundFont.GetFullPath();
            if (soundFontPath.Length > 0 && Api.Misc.IsSoundFont(soundFontPath) == 0)
            {
                Logger.LogError("Not a sound font: " + soundFontPath);
                soundFont.SetFullPath(string.Empty);
            }
        }

        void Reset()
        {
            if (Directory.Exists(Application.streamingAssetsPath))
            {
                string[] files = Directory.GetFiles(Application.streamingAssetsPath, "*.sf2", SearchOption.AllDirectories);
                if (files.Length == 1)
                {
                    soundFont.SetFullPath(files[0].Replace(Path.DirectorySeparatorChar, '/'));
                }
            }
        }
    }
}