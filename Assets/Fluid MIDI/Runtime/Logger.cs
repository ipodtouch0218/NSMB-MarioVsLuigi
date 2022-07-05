using AOT;
using FluidSynth;
using System;
using UnityEngine;

namespace FluidMidi
{
    static class Logger
    {
        static readonly Api.Log.FunctionDelegate logFunction = Log;

        static IntPtr handle;

        static int count = 0;

        public static void AddReference()
        {
            if (count == 0)
            {
                handle = Api.Unity.SetLogFunction(logFunction, IntPtr.Zero);
            }
            ++count;
        }

        public static void RemoveReference()
        {
            if (--count == 0)
            {
                Api.Unity.ClearLogFunction(handle);
            }
        }

        public static void Log(string message)
        {
            Debug.Log(FormatMessage(message));
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning(FormatMessage(message));
        }

        public static void LogError(string message)
        {
            Debug.LogError(FormatMessage(message));
        }

        static string FormatMessage(string message)
        {
            return "Fluid MIDI: " + message;
        }

        [MonoPInvokeCallback(typeof(Api.Log.FunctionDelegate))]
        static void Log(int level, string message, IntPtr data)
        {
            switch (level)
            {
                case Api.Log.Level.Warn:
                    LogWarning(message);
                    break;
                case Api.Log.Level.Error:
                case Api.Log.Level.Panic:
                    LogError(message);
                    break;
                default:
                    Log(message);
                    break;
            }
        }
    }
}