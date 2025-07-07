namespace Quantum {
  using Photon.Deterministic;
  using System;
  using System.Collections.Generic;
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

  public partial class QPrototypeLiquid {
    public void OnValidate() {
      ValidationUtility.SafeOnValidate(() => {
        if (!this) {
          return;
        }
        SpriteRenderer sRenderer = GetComponentInChildren<SpriteRenderer>();
        sRenderer.size = new Vector2(Prototype.WidthTiles * 0.5f, (Prototype.HeightTiles.AsFloat + 1) * 0.5f);
      });

      QuantumEntityPrototype entityPrototype = GetComponent<QuantumEntityPrototype>();
      Shape2DConfig shape = entityPrototype.PhysicsCollider.Shape2D;
      shape.ShapeType = Shape2DType.Compound;

      List<Shape2DConfig.CompoundShapeData2D> shapes = new();
      int sections = Mathf.CeilToInt(Prototype.WidthTiles / 8f);
      if (sections > 0) {
        FP sectionWidth = (FP)Prototype.WidthTiles / sections / 2;

        for (int i = 0; i < sections; i++) {
          shapes.Add(new Shape2DConfig.CompoundShapeData2D() {
            ShapeType = Shape2DType.Box,
            BoxExtents = new FPVector2(sectionWidth, Prototype.HeightTiles / 2) / 2,
            PositionOffset = new FPVector2((i - (sections * FP._0_50) + FP._0_50) * sectionWidth * 2, Prototype.HeightTiles / 2) / 2,
            RotationOffset = 0
          });
        }
      }
      shape.CompoundShapes = shapes.ToArray();
    }
  }
}