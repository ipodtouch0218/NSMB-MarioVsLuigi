using Photon.Deterministic;
using Quantum;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.Game {
    public class MasterCanvas : MonoBehaviour {

        //---Serialize Variables
        [SerializeField] private PlayerElements playerElementsPrefab;

        public unsafe void Start() {
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumGame game;
            Frame f;
            if ((game = QuantumRunner.DefaultGame) != null
                && (f = game.Frames.Predicted) != null
                && f.Global->GameState > GameState.WaitingForPlayers) {

                CheckForSpectatorUI(game, f);
            }
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

        public unsafe void CheckForSpectatorUI(QuantumGame game, Frame f) {
            if (PlayerElements.AllPlayerElements.Any(pe => pe)) {
                return;
            }

            // Create a new spectator-only PlayerElement
            PlayerElements newPlayerElements = Instantiate(playerElementsPrefab, transform);
            newPlayerElements.Initialize(game, f, EntityRef.None, PlayerRef.None);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.Starting) {
                CheckForSpectatorUI(e.Game, e.Frame);
            }
        }
    }
}
