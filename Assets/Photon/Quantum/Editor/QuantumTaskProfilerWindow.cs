namespace Quantum.Editor {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Quantum.Profiling;
  using UnityEditor;
  using UnityEngine;

  public partial class QuantumTaskProfilerWindow : EditorWindow, ISerializationCallbackReceiver {

    [SerializeField]
    private QuantumTaskProfilerModel _session = new QuantumTaskProfilerModel();
    [SerializeField]
    private List<float> _cumulatedMatchingSamples = new List<float>();
    [SerializeField]
    private NavigationState _navigationState = new NavigationState();

    [SerializeField]
    private bool _groupBySimulationId;
    [SerializeField]
    private bool _isPlaying;
    [SerializeField]
    private bool _isRecording;

    [SerializeField]
    private float _lastSamplesHeight = 100.0f;
    [SerializeField]
    private float _navigationHeight = 100.0f;
    [SerializeField]
    private string _searchPhrase = "";
    [SerializeField]
    private List<DeviceEntry> _sources = new List<DeviceEntry>() {
      new DeviceEntry() { id = "Auto", lastAlive = 0, label = new GUIContent("Auto") },
      new DeviceEntry() { id = "Editor", lastAlive = 0, label = new GUIContent("Editor") },
    };
    private int _selectedSourceIndex = 0;


    [SerializeField]
    private ZoomPanel _navigationPanel = new ZoomPanel() {
      controlId = "_navigationPanel".GetHashCode(),
      minRange = 500.0f,
      start = 0,
      range = 1000.0f,
      verticalScroll = 1.0f,
    };

    [SerializeField]
    private ZoomPanel _samplesPanel = new ZoomPanel() {
      controlId = "_samplesPanel".GetHashCode(),
      minRange = Styles.MinVisibleRange,
      start = 0,
      range = 0.2f,
      allowScrollPastLimits = true,
      enableRangeSelect = true,
    };

    [SerializeField]
    private bool _showFullClientInfo;
    [SerializeField]
    private Vector2 _clientInfoScroll;
    
    [NonSerialized]
    private readonly GUIContent _saveButtonContent = new GUIContent("Save");

    [NonSerialized]
    private BitArray _searchMask = new BitArray(0);
    [NonSerialized]
    private float _lastUpdate;
    [NonSerialized]
    private SelectionInfo _selectionInfo;
    [NonSerialized]
    private TickHandler _ticks = new TickHandler();
    [NonSerialized]
    private List<QuantumTaskProfilerModel.Frame> _visibleFrames = new List<QuantumTaskProfilerModel.Frame>();
    [NonSerialized]
    private List<WeakReference<SessionRunner>> _tracedRunners = new List<WeakReference<SessionRunner>>();


    [MenuItem("Window/Quantum/Task Profiler")]
    [MenuItem("Tools/Quantum/Window/Task Profiler", false, (int)QuantumEditorMenuPriority.Window + 15)]
    [MenuItem("Tools/Quantum/Profilers/Open Task Profiler", false, (int)QuantumEditorMenuPriority.Profilers + 15)]
    public static void ShowWindow() {
      GetWindow(typeof(QuantumTaskProfilerWindow));
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize() {
      if (!string.IsNullOrEmpty(_searchPhrase)) {
        _session.CreateSearchMask(_searchPhrase, _searchMask);
      }
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize() {
    }

    public void OnProfilerSample(QuantumProfilingClientInfo clientInfo, ProfilerContextData data) {

      // find the source
      int sourceIndex = 1;
      for (; sourceIndex < _sources.Count; ++sourceIndex) {
        if (_sources[sourceIndex].id == clientInfo.ProfilerId)
          break;
      }

      // add the new one if needed
      if (sourceIndex >= _sources.Count) {
        sourceIndex = _sources.Count;

        _sources.Add(new DeviceEntry() {
          id = clientInfo.ProfilerId,
          label = new GUIContent($"{clientInfo.GetProperty("MachineName")} ({clientInfo.ProfilerId})")
        });
      }

      // touch
      _sources[sourceIndex].lastAlive = DateTime.Now.ToFileTime();
      if (_isRecording == false) {
        return;
      }

      // auto select source
      if (_selectedSourceIndex == 0) {
        _selectedSourceIndex = sourceIndex;
      } else if (_selectedSourceIndex != sourceIndex) {
        return;
      }

      _session.AddFrame(clientInfo, data);

      if (_navigationState.IsCurrent) {
        _navigationPanel.start = _navigationState.durations.Count - _navigationPanel.range;
      }

      RefreshSearch(startFrame: _session.Frames.Count - 1);
      Repaint();
    }

    private void Clear() {
      _selectionInfo = new SelectionInfo();
      _samplesPanel.start = 0;
      _samplesPanel.range = 0.2f;
      _session = new QuantumTaskProfilerModel();
      _cumulatedMatchingSamples.Clear();
      _samplesPanel.verticalScroll = 0.0f;
      _navigationPanel.start = 0;
      _navigationPanel.range = 100.0f;
      _navigationState.selectedIndex = -1;
    }

    private void DoGridGUI(Rect rect, float frameTime) {
      if (Event.current.type != EventType.Repaint)
        return;

      Styles.profilerGraphBackground.Draw(rect, false, false, false, false);

      using (new GUI.ClipScope(rect)) {
        rect.x = rect.y = 0;

        Color tickColor = Styles.timelineTick.normal.textColor;
        tickColor.a = 0.1f;

        GL.Begin(GL.LINES);

        for (int l = 0; l < _ticks.VisibleLevelsCount; l++) {
          var strength = _ticks.GetStrengthOfLevel(l) * .9f;
          if (strength > 0.5f) {
            foreach (var tick in _ticks.GetTicksAtLevel(l, true)) {
              var x = _samplesPanel.TimeToPixel(tick, rect);
              DrawVerticalLineFast(x, 0, rect.height, tickColor);
            }
          }
        }

        // Draw frame start and end delimiters
        DrawVerticalLineFast(_samplesPanel.TimeToPixel(0, rect), 0, rect.height, Styles.frameDelimiterColor);
        DrawVerticalLineFast(_samplesPanel.TimeToPixel(frameTime, rect), 0, rect.height, Styles.frameDelimiterColor);

        GL.End();
      }
    }

    private void DoNavigationGUI(Rect rect, NavigationState info, float maxY) {
      using (new GUI.GroupScope(rect, Styles.profilerGraphBackground)) {
        rect = rect.ZeroXY();
        var r = rect.Adjust(0, 3, 0, -4);

        float timeToY = r.height / maxY;

        if (info.highlight.Count > 0) {
          int highlightStart = 0;

          if (Event.current.type == EventType.Repaint) {
            UnityInternal.HandleUtility.ApplyWireMaterial();
            GL.Begin(GL.QUADS);
            try {
              for (int i = 0; i < info.highlight.Count; ++i) {
                var x = _navigationPanel.TimeToPixel(i, rect);
                if (info.highlight[i]) {
                  if (highlightStart < 0) {
                    highlightStart = i;
                  }
                } else {
                  if (highlightStart >= 0) {

                    var width = x - _navigationPanel.TimeToPixel(highlightStart, rect);
                    var highlightRect = rect;

                    highlightRect.x = x - width;
                    highlightRect.width = width;

                    DrawRectFast(highlightRect, new Color(0, 0, 0, Mathf.Lerp(0.02f, 0.1f, width)));
                    highlightStart = -1;
                  }
                }

                if (x > rect.width) {
                  // we also want the fist point outside of the visible scope, then we're done
                  break;
                }
              }
            } finally {
              GL.End();
            }
          }
        }

        if (info.durations.Count > 0) {
          DrawGraph(rect, info.durations, _navigationPanel, maxY, color: Color.yellow, lineWidth: 2);
        }

        if (info.searchResults.Count > 0) {
          DrawGraph(rect, info.searchResults, _navigationPanel, maxY, color: Styles.SearchHighlightColor, lineWidth: 3);
        }

        using (new Handles.DrawingScope(new Color(1, 1, 1, 0.2f))) {
          foreach (var gridLine in Styles.NavigationGridLines) {
            if (gridLine > maxY)
              continue;
            var labelRect = DrawDropShadowLabelWithMargins(r, gridLine, maxY, 0);
            var y = (maxY - gridLine) * timeToY + r.y;
            Handles.DrawLine(new Vector2(r.xMin + labelRect.xMax, y), new Vector2(r.xMax, y));
          }
        }

        if (_navigationPanel.selectionRange.HasValue) {
          info.selectedIndex = Mathf.Clamp(Mathf.RoundToInt(_navigationPanel.selectionRange.Value.x), 0, info.durations.Count - 1);
          _navigationPanel.selectionRange = null;
        }

        if (info.selectedIndex > 0) {
          using (new Handles.DrawingScope(Styles.selectedFrameColor)) {
            var x = _navigationPanel.TimeToPixel(info.selectedIndex, rect);
            Handles.DrawLine(new Vector2(x, rect.yMin), new Vector2(x, rect.yMax));

            var oldContentColor = GUI.contentColor;
            try {
              DrawDropShadowLabelWithMargins(r, info.durations[info.selectedIndex], maxY, x, -1.0f, color: Color.yellow);

              if (info.searchResults.Count > info.selectedIndex) {
                DrawDropShadowLabelWithMargins(r, Mathf.Min(info.durations[info.selectedIndex], info.searchResults[info.selectedIndex]), maxY, x, color: Styles.SearchHighlightColor);
              }
            } finally {
              GUI.contentColor = oldContentColor;
            }
          }
        }
      }
    }

    private void DoSampleGUI(QuantumTaskProfilerModel.Sample sample, float durationMS, Rect rect, Rect clippedRect, bool selected) {
      GetDrawData(sample, out Color color, out string label);

      if (selected) {
        color = Color.Lerp(color, Color.white, 0.25f);
      }

      if (_searchMask.Count > sample.Id && _searchMask.Get(sample.Id) == false) {
        color = Color.Lerp(color, new Color(0, 0, 0, 0.1f), 0.75f);
      }

      DrawSolidRectangleWithOutline(rect, color, Color.Lerp(color, Color.black, 0.25f));

      if (Event.current.type != EventType.Repaint)
        return;

      if (clippedRect.width > 5.0f) {
        Styles.sampleStyle.Draw(clippedRect, string.Format("{0} ({1:F3}ms)", label, durationMS), false, false, false, false);
      }
    }

    private float DoSamplesGUI(Rect samplesRect, Rect legendRect, IEnumerable<QuantumTaskProfilerModel.Frame> frames) {
      var baseY = Styles.SampleSpacing - _samplesPanel.verticalScroll;
      var startY = baseY;

      var threadsLookup = frames.SelectMany(x => x.Threads).Select(x => x.Name).Distinct().ToDictionary(x => x, x => 0.0f);

      using (new GUI.GroupScope(samplesRect)) {
        samplesRect = samplesRect.ZeroXY();

        Rect tooltipRect = new Rect();
        GUIContent tooltipContent = GUIContent.none;

        foreach (var threadName in threadsLookup.Keys.ToList()) {
          float frameStartTime = 0.0f;
          float initialY = baseY;

          baseY += Styles.EventHeight;
          int maxDepth = 1;

          foreach (var frame in frames) {
            var ticksToMS = frame.TicksToMS;

            for (int i = 0; i < frame.Threads.Count; ++i) {
              var thread = frame.Threads[i];
              if (thread.Name != threadName)
                continue;

              for (int j = 0; j < thread.Samples.Count; ++j) {
                var sample = thread.Samples[j];

                bool isSelected = _selectionInfo.thread == thread && _selectionInfo.sample == j;
                float time = 0;
                Rect sampleRect;

                maxDepth = Mathf.Max(maxDepth, sample.Depth + 1);

                if (sample.Duration == 0) {
                  GetDrawData(sample, out Color color, out string label);
                  time = (float)(sample.Start * ticksToMS) + frameStartTime;
                  sampleRect = new Rect(_samplesPanel.TimeToPixel(time, samplesRect) - Styles.eventMarker.width / 2, baseY - Styles.EventHeight + 1, Styles.eventMarker.width, Styles.eventMarker.height);
                  GUI.DrawTexture(sampleRect, Styles.eventMarker, ScaleMode.ScaleToFit, true, 0, color, 0, 0);
                } else {
                  var x = _samplesPanel.TimeToPixel((float)(sample.Start * ticksToMS) + frameStartTime, samplesRect);
                  var duration = (float)(sample.Duration * ticksToMS);
                  var width = _samplesPanel.DurationToPixelLength(duration, samplesRect);
                  var r = new Rect(x, baseY + sample.Depth * (Styles.SampleHeight + Styles.SampleSpacing), width, Styles.SampleHeight);

                  time = duration;
                  sampleRect = Rect.MinMaxRect(Mathf.Max(r.x, 0.0f), Mathf.Max(r.y, 0.0f), Mathf.Min(r.xMax, samplesRect.width), Mathf.Min(r.yMax, samplesRect.height));
                  DoSampleGUI(sample, duration, r, sampleRect, isSelected);
                }

                if (Event.current.type == EventType.MouseUp && GUIUtility.hotControl == 0 && sampleRect.Contains(Event.current.mousePosition)) {
                  isSelected = true;
                  Event.current.Use();
                }

                if (isSelected) {
                  _selectionInfo = new SelectionInfo() {
                    thread = thread,
                    sample = j,
                  };

                  tooltipRect = sampleRect;
                  GetDrawData(sample, out var dummy, out var name);
                  tooltipContent = new GUIContent(string.Format("{0}\n{1}", name, FormatTime(time)));
                }
              }
            }

            frameStartTime += frame.DurationMS;
          }

          baseY += maxDepth * Styles.SampleHeight + (maxDepth - 1) * Styles.SampleSpacing + Styles.ThreadSpacing;

          using (new Handles.DrawingScope(Color.black)) {
            Handles.DrawLine(new Vector3(0, baseY), new Vector3(samplesRect.width, baseY));
          }

          threadsLookup[threadName] = baseY - initialY;
        }

        if (tooltipRect.width > 0) {
          QuantumEditorGUI.LargeTooltip(samplesRect, tooltipRect, tooltipContent);
        }

        {
          float frameStartTime = 0.0f;

          foreach (var frame in frames) {
            var x = _samplesPanel.TimeToPixel(frameStartTime, samplesRect);
            using (new Handles.DrawingScope(Color.gray)) {
              Handles.DrawLine(new Vector3(x, 0), new Vector3(x, samplesRect.height));
            }
            frameStartTime += frame.DurationMS;
          }
        }
      }

      using (new GUI.GroupScope(legendRect)) {
        float y = Styles.SampleSpacing - _samplesPanel.verticalScroll;

        foreach (var kv in threadsLookup.OrderBy(x => x.Key)) {
          DrawLegendLabel(new Rect(0, y, legendRect.width, kv.Value), kv.Key);
          y += kv.Value;
        }
      }

      return baseY - startY;
    }

    private void DoSelectionGUI(Rect samplesRect, Rect timelineRect) {
      if (_samplesPanel.selectionRange != null) {
        var timeRange = _samplesPanel.selectionRange.Value;
        using (new GUI.ClipScope(samplesRect)) {
          samplesRect = samplesRect.ZeroXY();
          var xMin = _samplesPanel.TimeToPixel(timeRange.x, samplesRect);
          var xMax = _samplesPanel.TimeToPixel(timeRange.y, samplesRect);
          EditorGUI.DrawRect(Rect.MinMaxRect(xMin, samplesRect.yMin, xMax, samplesRect.yMax), Styles.rangeSelectionColor);
        }

        using (new GUI.ClipScope(timelineRect)) {
          timelineRect = timelineRect.ZeroXY();
          var xMin = _samplesPanel.TimeToPixel(timeRange.x, timelineRect);
          var xMax = _samplesPanel.TimeToPixel(timeRange.y, timelineRect);
          var xCentre = (xMax + xMin) / 2.0f;
          DrawDropShadowLabel(timeRange.y - timeRange.x, xCentre, timelineRect.yMax, -0.5f, -1.0f);
        }
      }
    }

    private void DoFitButtonGUI(Rect rect, float visibleRange) {

      try {
        GUI.BeginClip(rect);
        rect = rect.ZeroXY();

        
        if (GUI.Button(rect, GUIContent.none, EditorStyles.toolbarButton)) {
          // add tiny margin
          _samplesPanel.start = -visibleRange * 0.02f;
          _samplesPanel.range = visibleRange * 1.04f;
          GUIUtility.ExitGUI();
        }
        
        // the label is so small it needs to be drawn on top with the "small" style
        var labelSize = EditorStyles.miniLabel.CalcSize(Styles.fitButtonContent);
        var labelOffset = new Vector2(labelSize.x - rect.width, labelSize.y - rect.height);
        var labelRect = new Rect(-labelOffset.x / 2, -labelOffset.y / 2, labelSize.x, labelSize.y);
        GUI.Label(labelRect, Styles.fitButtonContent, EditorStyles.miniLabel);

      } finally {
        GUI.EndClip();
      }
    }

    private void DoTickbarGUI(Rect rect, Color tickColor) {
      if (Event.current.type != EventType.Repaint) {
        return;
      }

      GUI.Box(rect, GUIContent.none, EditorStyles.toolbarButton);
      GUI.BeginClip(rect);
      try {
        var clipRect = rect.ZeroXY();
        UnityInternal.HandleUtility.ApplyWireMaterial();
        GL.Begin(GL.LINES);
        try {
          for (int i = 0; i < _ticks.VisibleLevelsCount; i++) {
            float strength = _ticks.GetStrengthOfLevel(i) * 0.8f;
            if (!(strength < 0.1f)) {
              foreach (float tick in _ticks.GetTicksAtLevel(i, excludeTicksFromHigherLevels: true)) {
                float x = _samplesPanel.TimeToPixel(tick, clipRect);
                var height = clipRect.height * Mathf.Min(1, strength) * Styles.MaxTickHeight;
                DrawVerticalLineFast(x, clipRect.height - height + 0.5f, clipRect.height - 0.5f, tickColor);
              }
            }
          }
        } finally {
          GL.End();
        }

        int labelLevel = _ticks.GetLevelWithMinSeparation(Styles.TickLabelWidth);
        foreach (var tick in _ticks.GetTicksAtLevel(labelLevel, false)) {
          float labelpos = Mathf.Floor(_samplesPanel.TimeToPixel(tick, clipRect));
          string label = FormatTickLabel(tick, labelLevel);
          GUI.Label(new Rect(labelpos + 3, -3, Styles.TickLabelWidth, 20), label, Styles.timelineTick);
        }
      } finally {
        GUI.EndClip();
      }
    }

    private void DoToolbarGUI(Rect toolbarRect, NavigationState state, string selectedLabel) {
      using (new GUI.GroupScope(toolbarRect, EditorStyles.toolbar)) {
        using (new GUILayout.HorizontalScope()) {
          _isRecording = GUILayout.Toggle(_isRecording, "Record", EditorStyles.toolbarButton);

          var selectedSource = _selectedSourceIndex < 0 || _selectedSourceIndex >= _sources.Count ? null : _sources[_selectedSourceIndex];

          var dropdownRect = EditorGUILayout.GetControlRect(false, 16, EditorStyles.toolbarPopup, GUILayout.MaxWidth(60));
          if (EditorGUI.DropdownButton(dropdownRect, selectedSource?.label ?? new GUIContent("<NONE>"), FocusType.Keyboard, EditorStyles.toolbarPopup)) {
            var menu = new GenericMenu();

            for (int i = 0; i < _sources.Count; ++i) {
              if (i == 2) {
                menu.AddSeparator(string.Empty);
              }
              var source = _sources[i];
              menu.AddItem(source.label, i == _selectedSourceIndex, obj => {
                _selectedSourceIndex = (int)obj;
              }, i);
            }
            
#if !QUANTUM_ENABLE_REMOTE_PROFILER
            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Define QUANTUM_ENABLE_REMOTE_PROFILER to enable Player profiling"));
#endif
            menu.DropDown(dropdownRect);
          }

          if (GUILayout.Button("New Window", EditorStyles.toolbarButton)) {
            CreateInstance<QuantumTaskProfilerWindow>().Show();
          }

          GUILayout.FlexibleSpace();

          if (state.EffectiveSelectedIndex > 0) {
            var selectedDuration = state.durations[state.EffectiveSelectedIndex];
            CalculateMeanStdDev(state.durations, out var mean, out var stdDev);

            GUILayout.Label(selectedLabel, Styles.toolbarLabel);
            GUILayout.Label(string.Format("CPU: {0}", FormatTime(state.durations[state.EffectiveSelectedIndex])), Styles.toolbarLabel);
            GUILayout.Label(string.Format("Mean: {0}", FormatTime((float)mean)), Styles.toolbarLabel);
            GUILayout.Label(string.Format("σ: {0}", FormatTime((float)stdDev)), Styles.toolbarLabel);
          }

          GUILayout.FlexibleSpace();

          if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) {
            Clear();
            GUIUtility.ExitGUI();
          }

          if (GUILayout.Button("Load", EditorStyles.toolbarButton)) {
            var path = EditorUtility.OpenFilePanelWithFilters("Open Profiler Report", ".", new[] { "Profiler Report", "dat,json" });
            if (!string.IsNullOrEmpty(path)) {
              LoadFile(path);
              GUIUtility.ExitGUI();
            }
          }

          using (new EditorGUI.DisabledScope(_session.Frames.Count == 0)) {
            var rect    = GUILayoutUtility.GetRect(_saveButtonContent, EditorStyles.toolbarDropDown);
            if (EditorGUI.DropdownButton(rect, _saveButtonContent, FocusType.Keyboard, EditorStyles.toolbarDropDown)) {
              var menu = new GenericMenu();
              menu.AddItem(new GUIContent("JSON"), false, () => SaveFile(true));
              menu.AddItem(new GUIContent("DAT"), false, () => SaveFile(false));
              menu.DropDown(rect);
            }

            void SaveFile(bool asJson) {
              string fileName;
              if (_selectedSourceIndex == 0) {
                fileName = "ProfilerReport";
              } else {
                fileName = $"ProfilerReport_{_sources[_selectedSourceIndex].id}";
              }
              var target = EditorUtility.SaveFilePanel("Save Profiler Report", ".", fileName, asJson ? "json" : "dat");
              if (!string.IsNullOrEmpty(target)) {
                if (asJson) {
                  File.WriteAllText(target, JsonUtility.ToJson(_session));
                } else {
                  using (var serializer = new BinarySerializer(File.Create(target), true)) {
                    _session.Serialize(serializer);
                  }
                }
              }
            }
          }

          {
            GUILayout.Label("Frame:", Styles.toolbarLabel);
            var frameLabel = "Current";
            if (state.selectedIndex >= 0) {
              frameLabel = string.Format("   {0} / {1}", state.selectedIndex + 1, state.durations.Count);
            }
            GUILayout.Label(frameLabel, Styles.toolbarLabel, GUILayout.Width(100));
          }

          // Previous/next/current buttons
          using (new EditorGUI.DisabledScope(!state.CanSelectPreviousFrame)) {
            if (GUILayout.Button(Styles.prevFrame, EditorStyles.toolbarButton))
              state.SelectPrevFrame();
          }

          using (new EditorGUI.DisabledScope(!state.CanSelectNextFrame)) {
            if (GUILayout.Button(Styles.nextFrame, EditorStyles.toolbarButton))
              state.SelectNextFrame();
          }

          GUILayout.Space(10);
          if (GUILayout.Button(Styles.currentFrame, EditorStyles.toolbarButton)) {
            state.SelectCurrentFrame();
          }

          GUILayout.Space(5);
        }
      }
    }

    private string FormatTickLabel(float time, int level) {
      string format = "{0}ms";
      float periodOfLevel = _ticks.GetPeriodOfLevel(level);
      int log10 = Mathf.FloorToInt(Mathf.Log10(periodOfLevel));
      if (log10 >= 3) {
        time /= 1000f;
        format = "{0}s";
      }
      return string.Format(format, time.ToString("N" + Mathf.Max(0, -log10)));
    }

    private void GetDrawData(QuantumTaskProfilerModel.Sample s, out Color color, out string text) {
      _session.GetSampleMeta(s, out color, out text);
    }

    private void LoadFile(string file) {
      if (!string.IsNullOrWhiteSpace(file)) {
        try {
          _session = QuantumTaskProfilerModel.LoadFromFile(file);
          _navigationPanel.start = 0;
          _navigationPanel.range = _session.Frames.Count;
          RefreshSearch();
        } catch (Exception e) {
          QuantumEditorLog.Exception(e);
        }
      }
    }

    private void OnEnable() {
      titleContent.text = "Quantum Task Profiler";
      minSize = new Vector2(200, 200);
    }

    private void OnGUI() {
      string toolbarLabel;

      toolbarLabel = "";

      _visibleFrames.Clear();

      RemoveDeadSources();

      QuantumTaskProfilerModel.Frame currentFrame;

      _navigationState.Refresh(_session, _cumulatedMatchingSamples, _groupBySimulationId);
      if (_navigationState.EffectiveSelectedIndex > 0) {
        if (_groupBySimulationId) {
          var frameIndex = _session.SimulationIndexToFrameIndex(_navigationState.EffectiveSelectedIndex, out var frameCount);
          currentFrame = _session.Frames[frameIndex];
          for (int i = 0; i < frameCount; ++i) {
            _visibleFrames.Add(_session.Frames[frameIndex + i]);
          }
          toolbarLabel = $"Frames#: ({currentFrame.Number}-{currentFrame.Number + frameCount})";
        } else {
          var frameIndex = _navigationState.EffectiveSelectedIndex;
          currentFrame = _session.Frames[frameIndex];
          _visibleFrames.Add(currentFrame);

          if (currentFrame.IsVerified) {
            toolbarLabel = $"Frame#: {currentFrame.Number}";
          } else {
            var prevVerified = _session.FindPrevSafe(frameIndex);
            if (prevVerified != null) {
              toolbarLabel = $"Frame#: {prevVerified.Number} (+{currentFrame.Number - prevVerified.Number})";
            } else {
              toolbarLabel = $"Frame#: ? + {currentFrame.Number}";
            }
          }
        }
      } else {
        currentFrame = null;
      }

      var toolbarRect = new Rect(0, 0, position.width, Styles.ToolbarHeight);
      DoToolbarGUI(toolbarRect, _navigationState, toolbarLabel);

      var clientInfoRect = new Rect(0, toolbarRect.yMax, position.width, _showFullClientInfo ? 200 : Styles.ToolbarHeight);
      if (currentFrame != null) {
        var clientInfo = _session.GetClientInfo(currentFrame);
        if (clientInfo != null) {
          DoClientInfoGUI(clientInfoRect, clientInfo);
        } else {
          clientInfoRect.height = 0;
        }
      } else {
        clientInfoRect.height = 0;
      }

      var navigationLabelRect = new Rect(0, clientInfoRect.yMax, Styles.LeftPaneWidth, _navigationHeight);
      DrawLegendLabel(navigationLabelRect, "CPU Usage");

      EditorGUI.BeginChangeCheck();
      _groupBySimulationId = EditorGUI.ToggleLeft(navigationLabelRect.AddLine().SetLineHeight().Adjust(5, 5, 0, 0), "Group By Simulation", _groupBySimulationId);
      if (EditorGUI.EndChangeCheck() && _navigationState.selectedIndex > 0) {
        // translate index
        if (_groupBySimulationId) {
          _navigationState.selectedIndex = _session.FrameIndexToSimulationIndex(_navigationState.selectedIndex);
        } else {
          _navigationState.selectedIndex = _session.SimulationIndexToFrameIndex(_navigationState.selectedIndex, out var frameCount);
        }

        // make sure the selection is visible
        _navigationPanel.start = _navigationState.selectedIndex - _navigationPanel.range / 2.0f;
        GUIUtility.ExitGUI();
      }

      var navigationBarRect = new Rect(Styles.LeftPaneWidth, clientInfoRect.yMax, position.width - Styles.LeftPaneWidth, _navigationHeight);

      _navigationPanel.minRange = Mathf.Min(_navigationState.durations.Count, (navigationBarRect.width - Styles.ScrollBarWidth) * 0.33f);
      _navigationPanel.OnGUI(navigationBarRect, 0.0f, _navigationState.durations.Count, out bool dummy, verticalSlider: true, minY: Styles.MinYRange, maxY: Styles.MaxYRange);

      DoNavigationGUI(_navigationPanel.areaRect, _navigationState, _navigationPanel.verticalScroll);

      var samplesRect = navigationBarRect.AddY(navigationBarRect.height + Styles.TimelineHeight).SetXMax(position.width).SetYMax(position.height);
      var visibleRange = _visibleFrames.Any() ? _visibleFrames.Sum(x => x.DurationMS) : 1.0f;

      _samplesPanel.OnGUI(samplesRect, 0.0f, visibleRange, out bool unselect, maxY: _lastSamplesHeight);
      if (unselect) {
        _selectionInfo = new SelectionInfo();
      }

      var samplesAreaRect = _samplesPanel.areaRect;
      var timelineRect = navigationBarRect.AddY(navigationBarRect.height).SetHeight(Styles.TimelineHeight).SetWidth(samplesAreaRect.width);

      _ticks.Refresh(_samplesPanel.start, _samplesPanel.range, timelineRect.width);
      DoTickbarGUI(timelineRect, Styles.timelineTick.normal.textColor);
      DoFitButtonGUI(timelineRect.SetXMin(timelineRect.xMax).SetWidth(Styles.ScrollBarWidth), visibleRange);

      _navigationHeight += DrawSplitter(timelineRect.SetHeight(5));
      _navigationHeight = Mathf.Clamp(_navigationHeight, 50.0f, position.height - 100);

      EditorGUI.BeginChangeCheck();
      _searchPhrase = UnityInternal.EditorGUI.ToolbarSearchField(Styles.ToolbarSearchFieldId, timelineRect.SetX(0).SetWidth(Styles.LeftPaneWidth).Adjust(1, 1, -2, -2), _searchPhrase, false);
      if (EditorGUI.EndChangeCheck()) {
        RefreshSearch();
      }

      DoGridGUI(samplesAreaRect, 0);

      if (_visibleFrames.Any()) {
        var minX = _samplesPanel.TimeToPixel(0.0f);
        var maxX = _samplesPanel.TimeToPixel(visibleRange);

        if (minX > samplesAreaRect.xMin) {
          EditorGUI.DrawRect(samplesAreaRect.SetXMax(minX), Styles.outOfRangeColor);
        }

        if (maxX < samplesAreaRect.xMax) {
          EditorGUI.DrawRect(samplesAreaRect.SetX(maxX).SetWidth(samplesAreaRect.xMax - maxX), Styles.outOfRangeColor);
        }
      }

      var legendRect = samplesRect.SetX(0).SetWidth(Styles.LeftPaneWidth);
      _lastSamplesHeight = DoSamplesGUI(_samplesPanel.areaRect, legendRect, _visibleFrames);
      DoSelectionGUI(samplesAreaRect, timelineRect);
    }

    private void DoClientInfoGUI(Rect rect, QuantumProfilingClientInfo clientInfo) {
      using (new GUILayout.AreaScope(rect, GUIContent.none, EditorStyles.toolbar)) {
        using (new GUILayout.HorizontalScope()) {

          GUILayout.FlexibleSpace();

          foreach (var inline in Styles.InlineProperties) {
            GUILayout.Label($"{inline}: {clientInfo.GetProperty(inline)}", Styles.toolbarLabel);
          }
          GUILayout.FlexibleSpace();

          EditorGUI.BeginChangeCheck();
          _showFullClientInfo = GUILayout.Toggle(_showFullClientInfo, "More", EditorStyles.toolbarButton);
          if (EditorGUI.EndChangeCheck()) {
            GUIUtility.ExitGUI();
          }
        }

        if (_showFullClientInfo) {
          using (new QuantumEditorGUI.LabelWidthScope(220)) {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_clientInfoScroll)) {
              _clientInfoScroll = scroll.scrollPosition;
              EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
              foreach (var prop in clientInfo.Properties) {
                using (new EditorGUILayout.HorizontalScope()) {
                  var r = EditorGUILayout.GetControlRect(true);
                  r = EditorGUI.PrefixLabel(r, new GUIContent(prop.Name));
                  EditorGUI.SelectableLabel(r, prop.Value);
                }
              }

              EditorGUILayout.Space();
              EditorGUILayout.LabelField("DeterministicConfig", EditorStyles.boldLabel);
              if (clientInfo.Config != null) {
                foreach (var f in clientInfo.Config.GetType().GetFields()) {
                  using (new EditorGUILayout.HorizontalScope()) {
                    var r = EditorGUILayout.GetControlRect(true);
                    r = EditorGUI.PrefixLabel(r, new GUIContent(f.Name));
                    EditorGUI.SelectableLabel(r, f.GetValue(clientInfo.Config)?.ToString());
                  }
                }

              }
            }
          }
        }
      }
    }

    private void RemoveDeadSources() {
      var sourceTTL = TimeSpan.FromSeconds(120);
      // ignore the first two ones (any and the editor)
      for (int i = 2, originalIndex = i; i < _sources.Count; ++i, ++originalIndex) {
        var source = _sources[i];
        var lastAlive = DateTime.FromFileTime(source.lastAlive);
        if (DateTime.Now - lastAlive > sourceTTL) {
          if (_selectedSourceIndex == originalIndex) {
            // don't remove the selected one
            continue;
          }
          _sources.RemoveAt(i--);
        }
      }
    }

    private void RefreshSearch(int? startFrame = null) {
      if (startFrame == null) {
        _cumulatedMatchingSamples.Clear();
        startFrame = 0;
      }

      if (!string.IsNullOrWhiteSpace(_searchPhrase)) {
        _session.CreateSearchMask(_searchPhrase, _searchMask);
        _session.AccumulateDurations(_searchMask, startFrame.Value, _cumulatedMatchingSamples);
      } else {
        _searchMask.Length = 0;
        _cumulatedMatchingSamples.Clear();
      }
    }

    private void Update() {

      // hook up in editor profiling
      {
        foreach (var runner in QuantumRunnerRegistry.Global.ActiveRunners) {
          if (runner.DeterministicGame == null || !runner.IsRunning)
            continue;

          foreach (var weakRef in _tracedRunners) {
            if (weakRef.TryGetTarget(out var target) && target == runner) {
              goto Next;
            }
          }

          QuantumEditorLog.Log($"Attaching to a local runner {runner}");
          var info = new QuantumProfilingClientInfo(runner.Session.SessionConfig, runner.Session.PlatformInfo);
          info.ProfilerId = "Editor";

          QuantumCallback.Subscribe(this, (CallbackTaskProfilerReportGenerated callback) => OnProfilerSample(info, callback.Report),
            filter: g => _isRecording && g == runner.Session.Game);
          
          _tracedRunners.Add(new WeakReference<SessionRunner>(runner));
        Next:;
        }
        // clean up all dead ones
        _tracedRunners.RemoveAll(x => !x.TryGetTarget(out var dummy));
      }

      QuantumProfilingServer.SampleReceived -= OnProfilerSample;
      QuantumProfilingServer.SampleReceived += OnProfilerSample;
      QuantumProfilingServer.Update();

      if (EditorApplication.isPlaying != _isPlaying) {
        _lastUpdate = 0;
        _isPlaying = EditorApplication.isPlaying;
      }

      var now = Time.realtimeSinceStartup;
      if (now > (_lastUpdate + (1f / 30f))) {
        _lastUpdate = now;
        Repaint();
      }
    }
    private struct SelectionInfo {
      public int sample;
      public QuantumTaskProfilerModel.Thread thread;
    }

    private static class Styles {

      public static GUIStyle toolbarLabel => EditorStyles.label;
      public const float ToolbarHeight = 21.0f;

      public static readonly GUIContent fitButtonContent = new GUIContent("↔");

      public const float MinYRange = 0.2f;
      public const float MaxYRange = 1000.0f;

      public const float DragPixelsThreshold = 5.0f;
      public const float EventHeight = 16.0f;
      public const float LeftPaneWidth = 180.0f;
      public const float MaxTickHeight = 0.7f;
      public const float MinVisibleRange = 0.001f;
      public const float NavigationBarHeight = 90.0f;
      public const float SampleHeight = 16.0f;
      public const float SampleSpacing = 1.0f;
      public const float ScrollBarWidth = 16.0f;
      public const float ThreadSpacing = 10.0f;
      public const float TickLabelWidth = 60.0f;
      public const float TimelineHeight = 16.0f;
      public static readonly GUIContent currentFrame = EditorGUIUtility.TrTextContent("Current", "Go to current frame");
      public static readonly Texture eventMarker = EditorGUIUtility.IconContent("Animation.EventMarker").image;
      public static readonly Color frameDelimiterColor = new Color(0.4f, 0.4f, 0.4f, 1.0f);
      public static readonly GUIStyle leftPane = "ProfilerTimelineLeftPane";
      public static readonly GUIStyle legendBackground = "ProfilerLeftPane";
      public static readonly GUIContent nextFrame = EditorGUIUtility.TrIconContent("Profiler.NextFrame", "Go one frame forwards");
      public static readonly Color outOfRangeColor = new Color(0, 0, 0, 0.1f);
      public static readonly GUIContent prevFrame = EditorGUIUtility.TrIconContent("Profiler.PrevFrame", "Go back one frame");

      public static readonly GUIStyle profilerGraphBackground = new GUIStyle("ProfilerGraphBackground") {
        overflow = new RectOffset()
      };

      public static readonly Color rangeSelectionColor = new Color32(200, 200, 200, 40);
      public static readonly int SamplesControlId = "Samples".GetHashCode();

      public static readonly GUIStyle sampleStyle = new GUIStyle() {
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Clip,
        fontSize = 9,
        normal = new GUIStyleState() {
          textColor = Color.white,
        }
      };

      public static readonly Color SearchHighlightColor = new Color(0.075f, 0.627f, 0.812f);
      public static readonly Color selectedFrameColor = new Color(1, 1, 1, 0.6f);
      public static readonly int SplitterControlId = "Splitter".GetHashCode();
      public static readonly int TimelineControlId = "Timeline".GetHashCode();
      public static readonly GUIStyle timelineTick = "AnimationTimelineTick";
      public static readonly int ToolbarSearchFieldId = "ToolbarSearchField".GetHashCode();
      public static readonly GUIStyle whiteLabel = "ProfilerBadge";
      public static float[] NavigationGridLines = new[] { 1 / 6.0f, 1 / 3.0f, 2 / 3.0f, 2.0f, 5.0f, 10.0f, 20.0f, 50.0f, 100.0f, 200.0f, 500.0f };
      public static Vector2 FramesYRange => new Vector2(0.022f, 1.0f);
      public static Vector2 SimulationsYRange => new Vector2(0.022f, 5.0f);

      public static readonly string[] InlineProperties = new[] {
        "MachineName",
        "Platform",
        "Runtime",
        "RuntimeHost",
        "UnityVersion",
        "ProcessorType",
      };
    }

    [Serializable]
    private class DeviceEntry {
      public string id;
      public GUIContent label;
      public long lastAlive;
    }

    [Serializable]
    private class NavigationState {
      [NonSerialized]
      public List<float> durations = new List<float>();

      [NonSerialized]
      public List<bool> highlight = new List<bool>();

      [NonSerialized]
      public List<float> searchResults = new List<float>();

      public int selectedIndex;

      public bool CanSelectNextFrame => selectedIndex < durations.Count - 1;

      public bool CanSelectPreviousFrame => selectedIndex > 0;

      public int EffectiveSelectedIndex => selectedIndex > 0 ? selectedIndex : (durations.Count - 1);

      public bool IsCurrent => selectedIndex < 0;

      public void ClearBuffers() {
        durations.Clear();
        highlight.Clear();
        searchResults.Clear();
      }
      public void Refresh(QuantumTaskProfilerModel session, List<float> searchResults, bool groupBySimulationId) {
        ClearBuffers();

        if (session.Frames.Count > 0) {
          if (groupBySimulationId) {
            session.GroupBySimulationId(null, durations);
            if (searchResults.Count > 0) {
              session.GroupBySimulationId(searchResults, this.searchResults);
            }
          } else {
            session.GetFrameDurations(durations);
            this.searchResults.AddRange(searchResults);

            var prevSimulationId = session.Frames[0].SimulationId;
            bool highlightState = false;

            foreach (var f in session.Frames) {
              if (f.SimulationId != prevSimulationId) {
                highlightState = !highlightState;
                prevSimulationId = f.SimulationId;
              }
              highlight.Add(highlightState);
            }

            //for (int i = 0; i < session.Frames.Count; ++i) {
            //  highlight.Add(session.Frames[i].isVerified);
            //}
          }
        }

        if (selectedIndex > durations.Count) {
          // it's fine if it becomes -1
          selectedIndex = durations.Count - 1;
        }
      }

      public void SelectCurrentFrame() {
        selectedIndex = -1;
      }

      public void SelectNextFrame() {
        if (CanSelectNextFrame) {
          ++selectedIndex;
        }
      }

      public void SelectPrevFrame() {
        if (CanSelectPreviousFrame) {
          --selectedIndex;
        }
      }
    }
  }
}