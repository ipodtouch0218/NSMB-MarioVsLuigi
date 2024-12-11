using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus {
    public class ProfilePanel : InRoomPanel {

        public void Start() {
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
        }

        public void OnCharacterClicked(int index) {
            Debug.Log("clicked " + index);
        }

        public void OnCharacterToggled() {
            Debug.Log("toggled");
        }

        public void OnChooseColor() {

        }


        //---Callbacks
        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (e.Game.PlayerIsLocal(e.Player)) {
                return;
            }

            // Set character button to the correct state
            PlayerData* data = QuantumUtils.GetPlayerData(e.Frame, e.Player);
            // data->Character
        }
    }
}
