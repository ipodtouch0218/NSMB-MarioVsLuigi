// ----------------------------------------------------------------------------
// <copyright file="SupportLogger.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Logging Helper.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using System.Text;
    using Stopwatch = System.Diagnostics.Stopwatch;
    using Conditional = System.Diagnostics.ConditionalAttribute;
    using Photon.Client;

    #if SUPPORTED_UNITY
    using UnityEngine;
    using SupportClass = Photon.Client.SupportClass;
    #endif


    /// <summary>Static class to provide customizable log functionality.</summary>
    public static class Log
    {
        /// <summary>Enumerates options for log prefixing.</summary>
        public enum PrefixOptions 
        { 
            /// <summary>No prefix.</summary>
            None, 
            /// <summary>Prefix with timestamp.</summary>
            Time, 
            /// <summary>Prefix with message's log level.</summary>
            Level, 
            /// <summary>Prefix with timestamp and log level.</summary>
            TimeAndLevel
        }
        
        /// <summary>Enumerates options for log output stream. Default: Console.</summary>
        public enum LogOutputOption
        {
            /// <summary>Auto becomes UnityDebug if this is a Unity build. Defaults to: Console.</summary>
            Auto, 
            /// <summary>Logs via Console.WriteLine.</summary>
            Console,
            /// <summary>Logs via Debug.WriteLine.</summary>
            Debug,
            /// <summary>Logs via UnityEngine.Debug.Log.</summary>
            UnityDebug
        }

        /// <summary>Prefix option for logging.</summary>
        public static PrefixOptions LogPrefix = PrefixOptions.None;

        private static Action<string> onError;
        private static Action<string> onWarn;
        private static Action<string> onInfo;
        private static Action<string> onDebug;
        private static Action<Exception, string> onException;
        private static Stopwatch sw;


        /// <summary>Static constructor will initialize the logging actions.</summary>
        static Log()
        {
            Init(LogOutputOption.Auto);
        }

        /// <summary>Initializes the logging to selected output stream.</summary>
        /// <remarks>Auto becomes UnityDebug if this is a Unity build. Defaults to: Console.</remarks>
        /// <param name="logOutput"></param>
        public static void Init(LogOutputOption logOutput)
        {
            onError = null;
            onWarn = null;
            onInfo = null;
            onDebug = null;
            
            sw = new Stopwatch();
            sw.Restart();

            #if !SUPPORTED_UNITY
            if (logOutput == LogOutputOption.UnityDebug || logOutput == LogOutputOption.Auto)
            {
                logOutput = LogOutputOption.Console;
            }
            #else
            if (logOutput == LogOutputOption.UnityDebug || logOutput == LogOutputOption.Auto)
            {
                onError = UnityEngine.Debug.LogError;
                onWarn = UnityEngine.Debug.LogWarning;
                onInfo = UnityEngine.Debug.Log;
                onDebug = UnityEngine.Debug.Log;
                return;
            }
            #endif

            if (logOutput == LogOutputOption.Console)
            {
                onError = (msg) => Console.WriteLine(msg);
                onWarn = (msg) => Console.WriteLine(msg);
                onInfo = (msg) => Console.WriteLine(msg);
                onDebug = (msg) => Console.WriteLine(msg);
                return;
            }

            if (logOutput == LogOutputOption.Debug)
            {
                onError = (msg) => System.Diagnostics.Debug.WriteLine(msg);
                onWarn = (msg) => System.Diagnostics.Debug.WriteLine(msg);
                onInfo = (msg) => System.Diagnostics.Debug.WriteLine(msg);
                onDebug = (msg) => System.Diagnostics.Debug.WriteLine(msg);
                return;
            }
        }

        /// <summary>
        /// Initialize the logging actions to custom actions. Note: These are initialized by default.
        /// </summary>
        /// <param name="error">Log errors.</param>
        /// <param name="warn">Log warnings.</param>
        /// <param name="info">Log info.</param>
        /// <param name="debug">Log debugging / tracing.</param>
        /// <param name="exception">Log exceptions.</param>
        public static void Init(Action<string> error, Action<string> warn, Action<string> info, Action<string> debug, Action<Exception, string> exception)
        {
            sw = new Stopwatch();
            sw.Restart();

            onError = error;
            onWarn = warn;
            onInfo = info;
            onDebug = debug;
            onException = exception;
        }


        /// <summary>Prefixes the message with timestamp, log level and prefix.</summary>
        static string ApplyPrefixes(string msg, LogLevel lvl = LogLevel.Error, string prefix = null)
        {
            StringBuilder sb = new StringBuilder();

            if (LogPrefix == PrefixOptions.Time || LogPrefix == PrefixOptions.TimeAndLevel)
            {
                //sb.Append($"[{GetFormattedTimestamp()}]");
                TimeSpan span = sw.Elapsed;
                if (span.Minutes > 0)
                {
                    sb.Append($"[{span.Minutes}:{span.Seconds:D2}.{span.Milliseconds:D3}]");
                }
                else
                    sb.Append($"[{span.Seconds:D2}.{span.Milliseconds:D3}]");

            }
            if (LogPrefix == PrefixOptions.Level || LogPrefix == PrefixOptions.TimeAndLevel)
            {
                sb.Append($"[{lvl}]");
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                sb.Append($"{prefix}: ");
            }
            else if (sb.Length > 0)
            {
                sb.Append(" ");
            }

            sb.Append(msg);
            return sb.ToString();
        }


        /// <summary>Check level, format message and call onException to log.</summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="lvl">The logging level of the instance that is about to log this message (if the level is equal or greater than this message.</param>
        /// <param name="prefix">String to place in front of the actual message.</param>
        public static void Exception(Exception ex, LogLevel lvl = LogLevel.Error, string prefix = null)
        {
            if (lvl < LogLevel.Error || onException == null)
            {
                return;
            }

            string output = ApplyPrefixes(ex.Message, lvl, prefix);
            onException(ex, output);
        }

        /// <summary>Check level, format message and call onError to log.</summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="lvl">The logging level of the instance that is about to log this message (if the level is equal or greater than this message.</param>
        /// <param name="prefix">String to place in front of the actual message.</param>
        public static void Error(string msg, LogLevel lvl = LogLevel.Error, string prefix = null)
        {
            if (lvl < LogLevel.Error || onError == null)
            {
                return;
            }

            string output = ApplyPrefixes(msg, lvl, prefix);
            onError(output);
        }

        /// <summary>Check level, format message and call onWarn to log.</summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="lvl">The logging level of the instance that is about to log this message (if the level is equal or greater than this message.</param>
        /// <param name="prefix">String to place in front of the actual message.</param>
        [Conditional("DEBUG"), Conditional("PHOTON_LOG_WARNING")]
        public static void Warn(string msg, LogLevel lvl = LogLevel.Warning, string prefix = null)
        {
            if (lvl < LogLevel.Warning || onWarn == null)
            {
                return;
            }

            string output = ApplyPrefixes(msg, lvl, prefix);
            onWarn(output);
        }

        /// <summary>Check level, format message and call onInfo to log.</summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="lvl">The logging level of the instance that is about to log this message (if the level is equal or greater than this message.</param>
        /// <param name="prefix">String to place in front of the actual message.</param>
        [Conditional("DEBUG"), Conditional("PHOTON_LOG_INFO")]
        public static void Info(string msg, LogLevel lvl = LogLevel.Info, string prefix = null)
        {
            if (lvl < LogLevel.Info || onInfo == null)
            {
                return;
            }

            string output = ApplyPrefixes(msg, lvl, prefix);
            onInfo(output);
        }

        /// <summary>Check level, format message and call onDebug to log.</summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="lvl">The logging level of the instance that is about to log this message (if the level is equal or greater than this message.</param>
        /// <param name="prefix">String to place in front of the actual message.</param>
        [Conditional("DEBUG"), Conditional("PHOTON_LOG_DEBUG")]
        public static void Debug(string msg, LogLevel lvl = LogLevel.Debug, string prefix = null)
        {
            if (lvl < LogLevel.Debug || onDebug == null)
            {
                return;
            }

            string output = ApplyPrefixes(msg, lvl, prefix);
            onDebug(output);
        }
    }
}