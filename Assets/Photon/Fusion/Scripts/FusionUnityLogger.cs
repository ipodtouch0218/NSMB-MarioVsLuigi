using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Fusion {
  [Serializable]
  public partial class FusionUnityLogger : Fusion.ILogger {

    /// <summary>
    /// Implement this to modify values of this logger.
    /// </summary>
    /// <param name="logger"></param>
    static partial void InitializePartial(ref FusionUnityLogger logger);

    StringBuilder _builder = new StringBuilder();

    /// <summary>
    /// If true, all messages will be prefixed with [Fusion] tag
    /// </summary>
    public bool UseGlobalPrefix;

    /// <summary>
    /// If true, some parts of messages will be enclosed with &lt;color&gt; tags.
    /// </summary>
    public bool UseColorTags;

    /// <summary>
    /// Color of the global prefix (see <see cref="UseGlobalPrefix"/>).
    /// </summary>
    public string GlobalPrefixColor;

    /// <summary>
    /// 
    /// </summary>
    public Color32 MinRandomColor;
    /// <summary>
    /// 
    /// </summary>
    public Color32 MaxRandomColor;

    public Color ServerColor;

    /// <summary>
    /// Converts object to a color <see cref="UseColorTags"/>. By default works only for <see cref="NetworkRunner"/> and uses <see cref="MinRandomColor"/> and <see cref="MaxRandomColor"/> fields.
    /// </summary>
    public Func<object, int> GetColor { get; set; }

    public FusionUnityLogger() {
      bool isDarkMode = false;
#if UNITY_EDITOR
      isDarkMode = UnityEditor.EditorGUIUtility.isProSkin;
#endif

      MinRandomColor = isDarkMode ? new Color32(158, 158, 158, 255) : new Color32(30, 30, 30, 255);
      MaxRandomColor = isDarkMode ? new Color32(255, 255, 255, 255) : new Color32(90, 90, 90, 255);
      ServerColor    = isDarkMode ? new Color32(255, 255, 158, 255) : new Color32(30, 90, 200, 255);

      UseColorTags = true;
      UseGlobalPrefix = true;
      GlobalPrefixColor = Color32ToRGBString(isDarkMode ? new Color32(115, 172, 229, 255) : new Color32(20, 64, 120, 255));

      GetColor = (obj) => {
        if (obj is NetworkRunner runner) {
          // flag server/host runners as special with seed of -1
          var seed = runner.GetHashCodeForLogger();
          return GetRandomColor(seed);
        }
        return default;
      };
    }

    public void Log<T>(LogType logType, string prefix, ref T context, string message) where T : ILogBuilder {

      Debug.Assert(_builder.Length == 0);
      string fullMessage;

      try {
        if (logType == LogType.Debug) {
          _builder.Append("[DEBUG] ");
        } else if (logType == LogType.Trace) {
          _builder.Append("[TRACE] ");
        }

        if (UseGlobalPrefix) {
          if (UseColorTags) {
            _builder.Append("<color=");
            _builder.Append(GlobalPrefixColor);
            _builder.Append(">");
          }
          _builder.Append("[Fusion");

          if (!string.IsNullOrEmpty(prefix)) {
            _builder.Append("/");
            _builder.Append(prefix);
          }

          _builder.Append("]");

          if (UseColorTags) {
            _builder.Append("</color>");
          }
          _builder.Append(" ");
        } else {
          if (!string.IsNullOrEmpty(prefix)) {
            _builder.Append(prefix);
            _builder.Append(": ");
          }
        }

        var options = new LogOptions(UseColorTags, GetColor);
        context.BuildLogMessage(_builder, message, options);
        fullMessage = _builder.ToString();
      } finally {
        _builder.Clear();
      }

      var obj = context as UnityEngine.Object;

      switch (logType) {
        case LogType.Error:
          Debug.LogError(fullMessage, obj);
          break;
        case LogType.Warn:
          Debug.LogWarning(fullMessage, obj);
          break;
        default:
          Debug.Log(fullMessage, obj);
          break;
      }
    }

    public void LogException<T>(string prefix, ref T context, Exception ex) where T : ILogBuilder {
      Log(LogType.Error, string.Empty, ref context, $"{ex.GetType()}\n<i>See next error log entry for details.</i>");
      if (context is UnityEngine.Object obj) {
        Debug.LogException(ex, obj);
      } else {
        Debug.LogException(ex);
      }
    }

    int GetRandomColor(int seed) => GetRandomColor(seed, MinRandomColor, MaxRandomColor, ServerColor);

    static int GetRandomColor(int seed, Color32 min, Color32 max, Color32 svr) {
      var random = new NetworkRNG(seed);
      int r, g, b;
      // -1 indicates host/client - give it a more pronounced color.
      if (seed == -1) {
        r = svr.r;
        g = svr.g;
        b = svr.b;
      } else {
        r = random.RangeInclusive(min.r, max.r);
        g = random.RangeInclusive(min.g, max.g);
        b = random.RangeInclusive(min.b, max.b);
      }

      r = Mathf.Clamp(r, 0, 255);
      g = Mathf.Clamp(g, 0, 255);
      b = Mathf.Clamp(b, 0, 255);

      int rgb = (r << 16) | (g << 8) | b;
      return rgb;
    }

    static int Color32ToRGB24(Color32 c) {
      return (c.r << 16) | (c.g << 8) | c.b;
    }

    static string Color32ToRGBString(Color32 c) {
      return string.Format("#{0:X6}", Color32ToRGB24(c));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize() {
      if (Fusion.Log.Initialized) {
        return;
      }

      var logger = new FusionUnityLogger();

      // Optional override of default values
      InitializePartial(ref logger);

      if (logger != null) {
        Fusion.Log.Init(logger);
      }
    }
  }
}
