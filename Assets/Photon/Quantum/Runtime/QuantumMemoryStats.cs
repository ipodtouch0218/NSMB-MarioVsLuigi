namespace Quantum {
  using System;
  using Photon.Analyzer;
  using UnityEngine.UI;

  public unsafe class QuantumMemoryStats : QuantumMonoBehaviour {
    public Text TotalMemory;
    public Text TotalPages;
    public Text TotalUsage;

    public Text PagesFree;
    public Text PagesFull;
    public Text PagesUsed;

    public Text BytesAllocated;
    public Text BytesReserved;
    public Text BytesCommited;

    public Text EntityCount;
    public Text EntityMemory;

    void Update() {
      if (QuantumRunner.Default && TotalMemory.isActiveAndEnabled) {
        var game = QuantumRunner.Default.Game;
        if (game != null && game.Frames.Predicted != null) {
          UpdateStats(game.Frames.Predicted);
        }
      }
    }

    void UpdateStats(Frame f) {
      var stats = f.GetMemoryStats();

      UpdateStatsValue(EntityCount, stats.EntityCount, false);
      UpdateStatsValue(EntityMemory, stats.EntityTotalMemory, true);

      UpdateStatsValue(TotalMemory, stats.HeapStats.TotalMemory);
      UpdateStatsValue(TotalPages, stats.HeapStats.TotalPages, false);

      UpdateStatsValue(PagesFree, stats.HeapStats.PagesFree, false);
      UpdateStatsValue(PagesFull, stats.HeapStats.PagesFull, false);
      UpdateStatsValue(PagesUsed, stats.HeapStats.PagesUsed, false);

      UpdateStatsValue(BytesAllocated, stats.HeapStats.BytesAllocated);
      UpdateStatsValue(BytesReserved, stats.HeapStats.BytesReserved);
      UpdateStatsValue(BytesCommited, stats.HeapStats.BytesCommited);

      TotalUsage.text = Math.Round((stats.HeapStats.BytesAllocated / (double)stats.HeapStats.TotalMemory) * 100, 2) + "%";
    }

    void UpdateStatsValue(Text text, int value, bool isBytes = true) {
      text.text = isBytes ? BytesToString(value) : value.ToString();
    }

    [StaticField(StaticFieldResetMode.None)]
    static string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

    static String BytesToString(long byteCount) {
      if (byteCount == 0) {
        return "0" + suf[0];
      }

      long   bytes = Math.Abs(byteCount);
      int    place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
      double num   = Math.Round(bytes / Math.Pow(1024, place), 1);

      return (Math.Sign(byteCount) * num) + suf[place];
    }
  }
}