using Quantum;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Button = UnityEngine.UI.Button;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class RoomBansPromptSubmenu : PromptSubmenu {

        //---Properties
        public override GameObject DefaultSelection => entries.Count > 0 ? entries[0].GameObject : base.DefaultSelection;

        //---Serialized Variables
        [SerializeField] private GameObject template;
        [SerializeField] private TMP_Text noBansText;

        //---Prviate Variables
        private readonly List<BanEntry> entries = new();

        public override void Show(bool first) {
            base.Show(first);

            PopulateBanList();
        }

        public unsafe void PopulateBanList() {
            foreach (var entry in entries) {
                Destroy(entry.GameObject);
            }
            entries.Clear();

            Frame f = NetworkHandler.Game.Frames.Predicted;
            var bans = f.ResolveList(f.Global->BannedPlayerIds);
            for (int i = 0; i < bans.Count; i++) {
                var ban = bans.GetPointer(i);

                GameObject newInstance = Instantiate(template, template.transform.parent);
                TMP_Text label = newInstance.transform.GetChild(0).GetComponent<TMP_Text>();
                label.text = ban->Nickname;
                Button newButton = newInstance.GetComponentInChildren<Button>();

                if (i > 0) {
                    Button previousButton = entries[i - 1].GameObject.GetComponentInChildren<Button>();

                    var newNav = newButton.navigation;
                    newNav.selectOnUp = previousButton;
                    newButton.navigation = newNav;

                    var previousNav = previousButton.navigation;
                    previousNav.selectOnDown = newButton;
                    previousButton.navigation = previousNav;
                }

                newInstance.SetActive(true);
                entries.Add(new BanEntry {
                    GameObject = newInstance,
                    Nickname = ban->Nickname,
                    UserId = ban->UserId,
                });
            }

            if (entries.Count > 0) {
                Button lastButton = entries[^1].GameObject.GetComponentInChildren<Button>();
                var lastNav = lastButton.navigation;
                lastNav.selectOnDown = backButton.GetComponentInChildren<Button>();
                lastButton.navigation = lastNav;
            }

            noBansText.gameObject.SetActive(entries.Count == 0);
        }

        public unsafe void UnbanViaIndex(int index) {
            BanEntry entry = entries[index];

            var game = NetworkHandler.Game;
            PlayerRef host = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);
            if (!game.PlayerIsLocal(host)) {
                return;
            }
            int slot = game.GetLocalPlayerSlots().IndexOf(host);
            game.SendCommand(slot, new CommandUnbanPlayer() {
                TargetUserId = entry.UserId,
            });

            Destroy(entry.GameObject);
            entries.RemoveAt(index);
            noBansText.gameObject.SetActive(entries.Count == 0);
        }


        public class BanEntry {
            public GameObject GameObject { get; set; }
            public string Nickname { get; set; }
            public string UserId { get; set; }
        }
    }
}