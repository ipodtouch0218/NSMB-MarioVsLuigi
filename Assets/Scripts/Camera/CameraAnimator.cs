using NSMB.Background;
using NSMB.UI.Game;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NSMB.Cameras {
    public unsafe class CameraAnimator : ResizingCamera {

        //---Static Variables
        private static event Action<float> OnScreenshake;
        public static void TriggerScreenshake(float duration) => OnScreenshake?.Invoke(duration);

        //---Properties
        public EntityRef Target => playerElements.Entity;
        public CameraMode Mode { get; set; } = CameraMode.FollowPlayer;

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private List<SecondaryCameraPositioner> secondaryPositioners;
        [SerializeField] private float zoomSpeed = 3, moveSpeed = 2;
        [SerializeField] private AudioSource zoomSfx;

        //---Private Variables
        private Vector3 truePosition;
        private VersusStageData stage;
        private float screenshakeTimer;
        private Vector2 previousPointer;
        private bool clickHeld, freecamMouseDragging;

        public override void OnValidate() {
            base.OnValidate();
            if (secondaryPositioners.Count == 0) {
                GetComponentsInChildren(secondaryPositioners);
            }
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public override void Start() {
            base.Start();
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindFirstObjectByType<QuantumMapData>().Asset.UserAsset);

            Settings.Controls.Replay.Reset.performed += OnReset;
            OnScreenshake += OnScreenshakeCallback;
        }

        public void OnDestroy() {
            Settings.Controls.Replay.Reset.performed -= OnReset;
            OnScreenshake -= OnScreenshakeCallback;
        }

        public override void Update() {
            // Do nothing, let OnUpdateView handle it.
        }

        public void OnUpdateView(CallbackUpdateView e) {

            switch (Mode) {
            case CameraMode.FollowPlayer:
                UpdateCameraFollowPlayerMode(e);
                break;
            case CameraMode.Freecam:
                UpdateCameraFreecamMode(e);
                break;
            }

            secondaryPositioners.RemoveAll(scp => !scp);
            secondaryPositioners.ForEach(scp => scp.UpdatePosition());

            if (BackgroundLoop.Instance) {
                BackgroundLoop.Instance.Reposition(ourCamera);
            }

            bool lmb = Settings.Controls.UI.Click.ReadValue<float>() >= 0.5f || Settings.Controls.UI.RightClick.ReadValue<float>() >= 0.5f;
            bool mmb = Settings.Controls.UI.MiddleClick.ReadValue<float>() >= 0.5f;
            if (lmb || !mmb) {
                previousPointer = ourCamera.ScreenToViewportPoint(Settings.Controls.UI.Point.ReadValue<Vector2>());
            }
        }

        private void UpdateCameraFollowPlayerMode(CallbackUpdateView e) {
            QuantumGame game = e.Game;
            Frame f = game.Frames.Predicted;
            Frame fp = game.Frames.PredictedPrevious;

            zoomSfx.enabled = false;

            if (!Target.IsValid || !f.Exists(Target) || !fp.Exists(Target)) {
                return;
            }

            var cameraControllerCurrent = f.Unsafe.GetPointer<CameraController>(Target);
            var cameraControllerPrevious = fp.Unsafe.GetPointer<CameraController>(Target);

            // Resize from game
            float targetOrthoSize = Mathf.Lerp(cameraControllerPrevious->OrthographicSize.AsFloat, cameraControllerCurrent->OrthographicSize.AsFloat, game.InterpolationFactor);
            ClampCameraAspectRatio(targetOrthoSize * 0.5f);

            FPVector2 origin = cameraControllerPrevious->CurrentPosition;
            FPVector2 target = cameraControllerCurrent->CurrentPosition;
            FPVector2 difference = target - origin;
            FP distance = difference.Magnitude;
            if (distance > 10) {
                // Wrapped the level
                //difference.X *= -1;
                FP width = (FP) stage.TileDimensions.X / 2;
                difference.X += (difference.X < 0) ? width : -width;
                target = origin + difference;
            }

            Vector3 newPosition = QuantumUtils.WrapWorld(stage, FPVector2.Lerp(origin, target, game.InterpolationFactor.ToFP()), out _).ToUnityVector3();
            newPosition.z = -10;

            var targetTransformPrevious = fp.Unsafe.GetPointer<Transform2D>(Target);
            var targetTransformCurrent = f.Unsafe.GetPointer<Transform2D>(Target);
            var targetMario = f.Unsafe.GetPointer<MarioPlayer>(Target);

            float playerHeight = targetMario->CurrentPowerupState switch {
                PowerupState.MegaMushroom => 3.5f,
                > PowerupState.Mushroom => 1f,
                _ => 0.5f,
            };

            // Offset to always put the player in the center for extremely long aspect ratios
            float screenAspect = ourCamera.aspect;
            float orthoSize = ourCamera.orthographicSize;
            if (Mathf.Abs((16f / 9f) - screenAspect) < 0.05f) {
                screenAspect = 16f / 9f;
            }

            Vector2 cameraFocus = Vector2.Lerp(targetTransformPrevious->Position.ToUnityVector2(), targetTransformCurrent->Position.ToUnityVector2(), game.InterpolationFactor);
            cameraFocus.y += playerHeight * 0.5f;

            if (!targetMario->IsDead || targetMario->IsRespawning) {
                float cameraHalfHeight = orthoSize - (playerHeight * 0.5f) - 0.25f;
                newPosition.y = Mathf.Clamp(newPosition.y, cameraFocus.y - cameraHalfHeight, cameraFocus.y + cameraHalfHeight);
            }

            // Clamp
            float cameraMinX = stage.CameraMinPosition.X.AsFloat + (orthoSize * screenAspect);
            float cameraMaxX = stage.CameraMaxPosition.X.AsFloat - (orthoSize * screenAspect);
            newPosition.x = Mathf.Clamp(newPosition.x, cameraMinX, cameraMaxX);

            float cameraMinY = stage.CameraMinPosition.Y.AsFloat + orthoSize;
            float cameraMaxY = Mathf.Max(stage.CameraMinPosition.Y.AsFloat + Mathf.Max(7, orthoSize * 2), stage.CameraMaxPosition.Y.AsFloat) - orthoSize;
            newPosition.y = Mathf.Clamp(newPosition.y, cameraMinY, cameraMaxY);

            truePosition = newPosition;

            // Screenshake (ignores clamping)
            if (screenshakeTimer > 0) {
                newPosition += new Vector3((UnityEngine.Random.value - 0.5f) * screenshakeTimer, (UnityEngine.Random.value - 0.5f) * screenshakeTimer);
                screenshakeTimer -= Time.unscaledDeltaTime;
            }

            ourCamera.transform.position = newPosition;
        }

        private void UpdateCameraFreecamMode(CallbackUpdateView e) {
            if (e.Game.Frames.Predicted.Global->GameState >= GameState.Ended
                || playerElements.PauseMenu.IsPaused) {
                zoomSfx.enabled = false;
                return;
            }

            bool ignoreKeyboard = playerElements.ReplayUi.IsOpen;

            // Movement
            Vector2 movement = Vector2.zero;
            if (!ignoreKeyboard) {
                movement += Settings.Controls.Player.Movement.ReadValue<Vector2>() * (Time.unscaledDeltaTime * moveSpeed * ourCamera.orthographicSize);
            }
            Vector2 pointerScreen = Settings.Controls.UI.Point.ReadValue<Vector2>();
            Vector2 pointer = ourCamera.ScreenToViewportPoint(pointerScreen);

            bool lmb = Settings.Controls.UI.Click.ReadValue<float>() >= 0.5f || Settings.Controls.UI.RightClick.ReadValue<float>() >= 0.5f;
            bool mmb = Settings.Controls.UI.MiddleClick.ReadValue<float>() >= 0.5f;
            if (lmb || mmb) {
                if (!clickHeld) {
                    // Make sure we're not over an object.
                    List<RaycastResult> results = new();
                    playerElements.Canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current) {
                        position = pointerScreen
                    }, results);
                    freecamMouseDragging = (results.Count == 0);
                    clickHeld = true;
                }
            } else {
                clickHeld = false;
                freecamMouseDragging = false;
            }

            if (freecamMouseDragging) {
                if (lmb) {
                    if (Vector2.Distance(pointer, previousPointer) < 0.2f) {
                        movement += (previousPointer - pointer) * new Vector2(ourCamera.orthographicSize * ourCamera.aspect * 2, ourCamera.orthographicSize * 2);
                    }
                    freecamMouseDragging = true;
                } else if (mmb) {
                    movement += (pointer - previousPointer) * new Vector2(ourCamera.orthographicSize * ourCamera.aspect * 2, ourCamera.orthographicSize * 2) * (Time.deltaTime * 6);
                    freecamMouseDragging = true;
                }
            }

            Vector3 newPosition = truePosition + (Vector3) movement;
            newPosition = QuantumUtils.WrapWorld(stage, newPosition.ToFPVector2(), out _).ToUnityVector3();
            newPosition.z = -10;

            truePosition = newPosition;

            // Screenshake
            if ((screenshakeTimer -= Time.unscaledDeltaTime) > 0) {
                newPosition += new Vector3((UnityEngine.Random.value - 0.5f) * screenshakeTimer, (UnityEngine.Random.value - 0.5f) * screenshakeTimer);
            }

            ourCamera.transform.position = newPosition;

            // Zoom
            float zoomAmount = Settings.Controls.UI.ScrollWheel.ReadValue<Vector2>().y * -12;
            Vector3? worldPosBefore = (zoomAmount != 0) ? ourCamera.ViewportToWorldPoint(pointer) : null;

            if (!ignoreKeyboard) {
                int zoomIn = Settings.Controls.Replay.ZoomIn.ReadValue<float>() >= 0.5f ? 1 : 0;
                int zoomOut = Settings.Controls.Replay.ZoomOut.ReadValue<float>() >= 0.5f ? 1 : 0;
                zoomAmount += (zoomOut - zoomIn);
            }

            float maxOrthoSize = stage.TileDimensions.X * 0.25f / ourCamera.aspect;
            if (zoomAmount != 0) {
                float newOrthoSize = ourCamera.orthographicSize + (zoomAmount * zoomSpeed * Time.unscaledDeltaTime);
                newOrthoSize = Mathf.Clamp(newOrthoSize, 0.5f, maxOrthoSize);
                ourCamera.orthographicSize = newOrthoSize;

                if (newOrthoSize > 0.5f && newOrthoSize < maxOrthoSize) {
                    if (!zoomSfx.isPlaying) {
                        zoomSfx.Play();
                        zoomSfx.loop = true;
                    } else {
                        zoomSfx.loop = true;
                    }
                } else {
                    zoomSfx.loop = false;
                }

                if (worldPosBefore != null) {
                    Vector3 worldPosAfter = ourCamera.ViewportToWorldPoint(pointer);
                    newPosition += (Vector3) (worldPosBefore - worldPosAfter);
                }
            } else {
                zoomSfx.loop = false;
            }

            // Clamp
            // Ortho size
            ourCamera.orthographicSize = Mathf.Clamp(ourCamera.orthographicSize, 0.5f, maxOrthoSize);

            // Position
            float orthoSize = ourCamera.orthographicSize;
            float screenAspect = ourCamera.aspect;
            float cameraMinX = stage.CameraMinPosition.X.AsFloat - (orthoSize * screenAspect);
            float cameraMaxX = stage.CameraMaxPosition.X.AsFloat + (orthoSize * screenAspect);
            newPosition.x = Mathf.Clamp(newPosition.x, cameraMinX, cameraMaxX);

            float cameraMinY = stage.CameraMinPosition.Y.AsFloat - orthoSize;
            float cameraMaxY = cameraMinY + Mathf.Max(7, stage.CameraMaxPosition.Y.AsFloat - stage.CameraMinPosition.Y.AsFloat) + (orthoSize * 2);
            newPosition.y = Mathf.Clamp(newPosition.y, cameraMinY, cameraMaxY);

            ourCamera.transform.position = newPosition;
        }

        private void OnReset(InputAction.CallbackContext context) {
            if (Mode != CameraMode.Freecam) {
                return;
            }

            ourCamera.orthographicSize = 3.5f;
            Vector3 newPosition = stage.Spawnpoint.ToUnityVector2();
            newPosition.z = -10;

            float screenAspect = ourCamera.aspect;
            float orthoSize = ourCamera.orthographicSize;
            if (Mathf.Abs((16f / 9f) - screenAspect) < 0.05f) {
                screenAspect = 16f / 9f;
            }

            // Clamp
            float cameraMinX = stage.CameraMinPosition.X.AsFloat + (orthoSize * screenAspect);
            float cameraMaxX = stage.CameraMaxPosition.X.AsFloat - (orthoSize * screenAspect);
            newPosition.x = Mathf.Clamp(newPosition.x, cameraMinX, cameraMaxX);

            float cameraMinY = stage.CameraMinPosition.Y.AsFloat + orthoSize;
            float cameraMaxY = Mathf.Max(stage.CameraMinPosition.Y.AsFloat + Mathf.Max(7, orthoSize * 2), stage.CameraMaxPosition.Y.AsFloat) - orthoSize;
            newPosition.y = Mathf.Clamp(newPosition.y, cameraMinY, cameraMaxY);

            ourCamera.transform.position = newPosition;

            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Back);
        }

        private void OnScreenshakeCallback(float screenshake) {
            screenshakeTimer = screenshake;
            
            /*
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;

            if (!f.Exists(Target)
                || !f.Unsafe.TryGetPointer(Target, out PhysicsObject* physicsObject)
                || physicsObject->IsTouchingGround) {
                screenshakeTimer = screenshake;
            }
            */
        }

        public enum CameraMode {
            FollowPlayer, Freecam
        }
    }
}
