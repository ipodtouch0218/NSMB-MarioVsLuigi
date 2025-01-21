namespace Quantum {
  using Photon.Deterministic;
  using System;
  using UnityEngine;

  public partial class QPrototypeBreakableObject {
    public void OnValidate() {
      ValidationUtility.SafeOnValidate(() => {
        if (!this) {
          return;
        }
        SpriteRenderer sRenderer = GetComponentInChildren<SpriteRenderer>();
        sRenderer.size = new Vector2(sRenderer.size.x, Prototype.OriginalHeight.AsFloat);
      });

      QuantumEntityPrototype entityPrototype = GetComponent<QuantumEntityPrototype>();
      Shape2DConfig shape = entityPrototype.PhysicsCollider.Shape2D;
      shape.PositionOffset = FPVector2.Up * (Prototype.OriginalHeight / 4);
      shape.BoxExtents.Y = (Prototype.OriginalHeight / 4);
    }
  }

  public static class ValidationUtility {
    public static void SafeOnValidate(Action onValidateAction) {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.delayCall += _OnValidate;


      void _OnValidate() {
        UnityEditor.EditorApplication.delayCall -= _OnValidate;

        onValidateAction();
      }
#endif
    }
    // For more details, see this forum thread: https://discussions.unity.com/t/705805
  }
}