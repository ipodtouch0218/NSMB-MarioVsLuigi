namespace Quantum {
  using System;
  using Photon.Analyzer;
  using UnityEngine.UI;

  /// <summary>
  /// A script that displays memory stats of the Quantum heap.
  /// </summary>
  public unsafe class QuantumMemoryStats : QuantumMonoBehaviour {
    /// <summary>
    /// The total memory in MB.
    /// </summary>
    public Text TotalMemory;
    /// <summary>
    /// The number of pages in the heap.
    /// </summary>
    public Text TotalPages;
    /// <summary>
    /// The percentage of memory used.
    /// </summary>
    public Text TotalUsage;
    /// <summary>
    /// The pages that are free.
    /// </summary>
    public Text PagesFree;
    /// <summary>
    /// The pages that are full.
    /// </summary>
    public Text PagesFull;
    /// <summary>
    /// The pages used.
    /// </summary>
    public Text PagesUsed;
    /// <summary>
    /// The total amount of bytes allocated in KB.
    /// </summary>
    public Text BytesAllocated;
    /// <summary>
    /// The total amount of bytes reserved in KB.
    /// </summary>
    public Text BytesReserved;
    /// <summary>
    /// The total amount of bytes commited in KB.
    /// </summary>
    public Text BytesCommited;
    /// <summary>
    /// The entity count.
    /// </summary>
    public Text EntityCount;
    /// <summary>
    /// The memory used by entities in KB.
    /// </summary>
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

      long bytes = Math.Abs(byteCount);
      int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
      double num = Math.Round(bytes / Math.Pow(1024, place), 1);

      return (Math.Sign(byteCount) * num) + suf[place];
    }
  }
}