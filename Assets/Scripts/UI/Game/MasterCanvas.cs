using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace NSMB.UI.Game {
    public class MasterCanvas : QuantumSceneViewComponent {

        //---Serialize Variables
        [SerializeField] public PlayerElements playerElementsPrefab;

        //---Private Variables
        private PlayerElements spectatorUI;

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
            if (spectatorUI) {
                return;
            }

            foreach ((_, var playerData) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                if (!playerData->IsSpectator && Game.PlayerIsLocal(playerData->PlayerRef)) {
                    return;
                }
            }

            // Create a new spectator-only PlayerElement
            spectatorUI = Instantiate(playerElementsPrefab, transform);
            spectatorUI.Initialize(Game, f, EntityRef.None, PlayerRef.None);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState is GameState.Playing or GameState.Starting) {
                CheckForSpectatorUI(e.Game.Frames.Predicted);
            }
        }
    }
}
