namespace Quantum.Menu {
#if QUANTUM_ENABLE_TEXTMESHPRO
  using Text = TMPro.TMP_Text;
#else 
  using Text = UnityEngine.UI.Text;
#endif
  using UnityEngine;

  /// <summary>
  /// Displays the average current fps
  /// </summary>
  public class QuantumMenuFpsAvgCounter : QuantumMenuScreenPlugin {
    /// <summary>
    /// The fps text. Can be null.
    /// </summary>
    [InlineHelp, SerializeField] protected Text _fpsText;

    private float[] _duration = new float[60];
    private int _index;
    private int _samples;

    /// <summary>
    /// Unity update method to update text.
    /// </summary>
    public virtual void Update() {
      // Only sample every other update
      _samples = _samples++ % _duration.Length;
      if (_samples % 2 != 0) {
        return;
      }

      _duration[_index++] = Time.unscaledDeltaTime;
      _index = _index % _duration.Length;

      var accum = 0.0f;
      var count = 0;
      for (int i = 0; i < _duration.Length; i++) {
        if (_duration[i] > 0.0f) {
          accum += _duration[i];
          count++;
        }
      }

      var fps = 0;
      if (count > 0) {
        fps = (int)(count / accum);
      }

      _fpsText.text = fps.ToString();
    }
  }
}
