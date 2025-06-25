using NSMB.Replay;
using NSMB.UI.Game;
using Photon.Deterministic;
using Quantum;
using System.Linq;
using UnityEngine;

namespace NSMB.Utilities {
    public class QuantumViewUtils : MonoBehaviour {

        public static bool IsReplay => QuantumRunner.Default?.Session.IsReplay ?? false;
        public static bool IsReplayFastForwarding => ActiveReplayManager.Instance.IsReplayFastForwarding;

        public static bool FilterOutReplayFastForward(IDeterministicGame game) {
            return !IsReplayFastForwarding;
        }

        public static bool FilterOutReplay(IDeterministicGame game) {
            return !((QuantumGame) game).Session.IsReplay;
        }

        public static bool IsMarioLocal(EntityRef entity) {
            return PlayerElements.AllPlayerElements.Any(pe => pe.Entity == entity);
        }
    }
}
