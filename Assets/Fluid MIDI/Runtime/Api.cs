using System;
using System.Runtime.InteropServices;

namespace FluidSynth
{
    static class Api
    {
        private const string LibraryName = "audioplugin-fluidsynth-3";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int MidiEventDelegate(IntPtr data, IntPtr midiEvent);

        public static class Settings
        {
            public const string KEY_PLAYER_RESET_SYNTH = "player.reset-synth";
            public const string KEY_AUDIO_DRIVER = "audio.driver";
            public const string KEY_SYNTH_SAMPLE_RATE = "synth.sample-rate";
            public const string KEY_SYNTH_CPU_CORES = "synth.cpu-cores";

            public const string VALUE_AUDIO_DRIVER_WAVEOUT = "waveout";
            public const string VALUE_AUDIO_DRIVER_UNITY = "unity";

            [DllImport(LibraryName, EntryPoint = "new_fluid_settings")]
            public static extern IntPtr Create();

            [DllImport(LibraryName, EntryPoint = "fluid_settings_setint")]
            public static extern int Set(IntPtr settings, string key, int value);

            [DllImport(LibraryName, EntryPoint = "fluid_settings_setnum")]
            public static extern int Set(IntPtr settings, string key, double value);

            [DllImport(LibraryName, EntryPoint = "fluid_settings_setstr")]
            public static extern int Set(IntPtr settings, string key, string value);

            [DllImport(LibraryName, EntryPoint = "delete_fluid_settings")]
            public static extern void Destroy(IntPtr settings);
        }

        public static class Synth
        {
            public const float GAIN_MIN = 0;
            public const float GAIN_MAX = 100;

            [DllImport(LibraryName, EntryPoint = "new_fluid_synth")]
            public static extern IntPtr Create(IntPtr settings);

            [DllImport(LibraryName, EntryPoint = "delete_fluid_synth")]
            public static extern void Destroy(IntPtr synth);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_sfload")]
            public static extern int LoadSoundFont(IntPtr synth, string filename, int resetPresets);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_set_gain")]
            public static extern void SetGain(IntPtr synth, float gain);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_handle_midi_event")]
            public static extern int HandleMidiEvent(IntPtr synth, IntPtr midiEvent);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_all_sounds_off")]
            public static extern int AllSoundsOff(IntPtr synth, int channel);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_all_notes_off")]
            public static extern int AllNotesOff(IntPtr synth, int channel);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_get_sfont")]
            public static extern IntPtr GetSoundFont(IntPtr synth, int num);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_get_sfont_by_id")]
            public static extern IntPtr GetSoundFontById(IntPtr synth, int id);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_add_sfont")]
            public static extern int AddSoundFont(IntPtr synth, IntPtr soundFont);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_remove_sfont")]
            public static extern int RemoveSoundFont(IntPtr synth, IntPtr soundFont);

            [DllImport(LibraryName, EntryPoint = "fluid_synth_sfcount")]
            public static extern int SoundFontCount(IntPtr synth);
        }

        public static class Player
        {
            public static class Status
            {
                public const int Ready = 0;
                public const int Playing = 1;
                public const int Stopping = 2;
                public const int Done = 3;
            }

            public static class TempoType
            {
                public const int Internal = 0;
                public const int Bpm = 1;
                public const int Midi = 2;
                public const int Nbr = 3;
            }

            public const double TEMPO_MIN = 0.001;
            public const double TEMPO_MAX = 1000;

            [DllImport(LibraryName, EntryPoint = "new_fluid_player")]
            public static extern IntPtr Create(IntPtr synth);

            [DllImport(LibraryName, EntryPoint = "delete_fluid_player")]
            public static extern void Destroy(IntPtr player);

            [DllImport(LibraryName, EntryPoint = "fluid_player_add")]
            public static extern int Add(IntPtr player, string filename);

            [DllImport(LibraryName, EntryPoint = "fluid_player_prepare")]
            public static extern void Prepare(IntPtr player);

            [DllImport(LibraryName, EntryPoint = "fluid_player_play")]
            public static extern int Play(IntPtr player);

            [DllImport(LibraryName, EntryPoint = "fluid_player_get_current_tick")]
            public static extern int GetCurrentTick(IntPtr player);

            [DllImport(LibraryName, EntryPoint = "fluid_player_seek")]
            public static extern int Seek(IntPtr player, int ticks);

            [DllImport(LibraryName, EntryPoint = "fluid_player_repeat")]
            public static extern int Repeat(IntPtr player, int begin, int end);

            [DllImport(LibraryName, EntryPoint = "fluid_player_stop")]
            public static extern int Stop(IntPtr player);

            [DllImport(LibraryName, EntryPoint = "fluid_player_set_playback_callback")]
            public static extern int SetPlaybackCallback(IntPtr player, MidiEventDelegate callback, IntPtr data);

            [DllImport(LibraryName, EntryPoint = "fluid_player_set_loop")]
            public static extern int SetLoop(IntPtr player, int loop);

            [DllImport(LibraryName, EntryPoint = "fluid_player_set_loop_begin")]
            public static extern int SetLoopBegin(IntPtr player, int ticks);

            [DllImport(LibraryName, EntryPoint = "fluid_player_set_end")]
            public static extern int SetEnd(IntPtr player, int ticks);

            [DllImport(LibraryName, EntryPoint = "fluid_player_get_status")]
            public static extern int GetStatus(IntPtr player);

            [DllImport(LibraryName, EntryPoint = "fluid_player_set_tempo")]
            public static extern int SetTempo(IntPtr player, int type, double tempo);

            [DllImport(LibraryName, EntryPoint = "fluid_player_set_active_channels")]
            public static extern void SetActiveChannels(IntPtr player, int channels);
        }

        public static class Driver
        {
            [DllImport(LibraryName, EntryPoint = "new_fluid_audio_driver")]
            public static extern IntPtr Create(IntPtr settings, IntPtr synth);

            [DllImport(LibraryName, EntryPoint = "delete_fluid_audio_driver")]
            public static extern void Destroy(IntPtr driver);
        }

        public static class Log
        {
            public static class Level
            {
                public const int Panic = 0;
                public const int Error = 1;
                public const int Warn = 2;
                public const int Info = 3;
                public const int Debug = 4;
            }

            public delegate void FunctionDelegate(int level, string message, IntPtr data);

            [DllImport(LibraryName, EntryPoint = "fluid_set_log_function", CallingConvention = CallingConvention.Cdecl)]
            public static extern FunctionDelegate SetFunction(int level, FunctionDelegate function, IntPtr data);

            public static void SetFunction(FunctionDelegate function, IntPtr data)
            {
                SetFunction(Level.Panic, function, data);
                SetFunction(Level.Error, function, data);
                SetFunction(Level.Warn, function, data);
                SetFunction(Level.Info, function, data);
                SetFunction(Level.Debug, function, data);
            }
        }

        public static class Unity
        {
            [DllImport(LibraryName, EntryPoint = "fluid_unity_set_log_function", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SetLogFunction(Log.FunctionDelegate function, IntPtr data);

            [DllImport(LibraryName, EntryPoint = "fluid_unity_clear_log_function")]
            public static extern void ClearLogFunction(IntPtr handle);
        }

        public static class Misc
        {
            [DllImport(LibraryName, EntryPoint = "fluid_is_soundfont")]
            public static extern int IsSoundFont(string fileName);

            [DllImport(LibraryName, EntryPoint = "fluid_is_midifile")]
            public static extern int IsMidiFile(string fileName);
        }


        public static class Result
        {
            public const int OK = 0;
            public const int Failed = -1;

            public static bool IsError(int code)
            {
                return code < OK;
            }
        }
    }
}
