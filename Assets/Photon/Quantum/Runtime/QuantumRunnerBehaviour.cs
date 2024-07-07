namespace Quantum {
  using UnityEngine;
  using UnityEngine.Rendering;

  /// <summary>
  /// A unity script that updates the Quantum runner.
  /// Also manages calls to Gizmos and DebugDraw required to render Quantum debug gizmos.
  /// If you are writing a custom SRP, you must call RenderPipeline.EndCameraRendering to trigger OnPostRenderInternal().
  /// </summary>
  public class QuantumRunnerBehaviour : QuantumMonoBehaviour {
    /// <summary>
    /// The runner object set during <see cref="QuantumRunner.StartGame(SessionRunner.Arguments)"/>
    /// </summary>
    public QuantumRunner Runner;

    /// <summary>
    /// Unity OnEnable event is required to register to global camera callbacks for gizmos rendering.  
    /// </summary>
    public void OnEnable() {
      Camera.onPostRender += OnPostRenderInternal;
      RenderPipelineManager.endCameraRendering += OnPostRenderInternal;
    }

    /// <summary>
    /// Unity OnDisable event is used to unsubscribe the global camera callbacks.
    /// </summary>
    public void OnDisable() {
      Camera.onPostRender -= OnPostRenderInternal;
      RenderPipelineManager.endCameraRendering -= OnPostRenderInternal;
    }

    /// <summary>
    /// Unity Update event triggers the runner updates and ticks the Quantum simulation.
    /// </summary>
    public void Update() {
      Runner?.Update();
    }

    void OnPostRenderInternal(ScriptableRenderContext context, Camera camera) {
      OnPostRenderInternal(camera);
    }

    void OnPostRenderInternal( Camera camera) {
      if (Runner == null) {
        return;
      }

      if (Runner.Session == null) {
        return;
      }

      if (Runner.HideGizmos) {
        return;
      }

      DebugDraw.OnPostRender(camera);
    }
  }
}