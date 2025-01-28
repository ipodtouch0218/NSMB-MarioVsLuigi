using Photon.Deterministic;
using Quantum;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.Game {
    public class MasterCanvas : QuantumSceneViewComponent {

        //---Serialize Variables
        [SerializeField] public PlayerElements playerElementsPrefab;

        public override unsafe void OnActivate(Frame f) {
            if (f.Global->GameState > GameState.WaitingForPlayers) {
                CheckForSpectatorUI(f);
            }
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        }

        public void Update() {
            Frame f;
            if (QuantumRunner.DefaultGame == null
                || (f = QuantumRunner.DefaultGame.Frames.Predicted) == null) {
                return;
            }
            var context = f.Context;
            context.CullingCameraPositions.Clear();
            context.CullingIgnoredEntities.Clear();
            context.MaxCameraOrthoSize = 0;

            foreach (PlayerElements pe in PlayerElements.AllPlayerElements) {
                Camera camera = pe.Camera;
                FPVector2 position = camera.transform.position.ToFPVector2();
                FP size = camera.orthographicSize.ToFP();

                context.CullingIgnoredEntities.Add(pe.Entity);
                context.CullingCameraPositions.Add(position);
                context.MaxCameraOrthoSize = FPMath.Max(context.MaxCameraOrthoSize, size);
            }
        }

        public unsafe void CheckForSpectatorUI(Frame f) {
            if (PlayerElements.AllPlayerElements.Any(pe => pe)) {
                return;
            }

            // Create a new spectator-only PlayerElement
            PlayerElements newPlayerElements = Instantiate(playerElementsPrefab, transform);
            newPlayerElements.Initialize(Game, f, EntityRef.None, PlayerRef.None);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Playing) {
                CheckForSpectatorUI(e.Frame);
            }
        }
    }
}
