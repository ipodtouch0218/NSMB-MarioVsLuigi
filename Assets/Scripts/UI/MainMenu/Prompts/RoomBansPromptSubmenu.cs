using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private IDisposable eventSubscription;

        public override void Show(bool first) {
            base.Show(first);

            PopulateBanList();
            eventSubscription = QuantumEvent.SubscribeManual<EventPlayerUnbanned>(this, OnPlayerUnbanned);
        }

        public override void OnDestroy() {
            base.OnDestroy();
            eventSubscription?.Dispose();
            eventSubscription = null;
        }

        public override void Hide(SubmenuHideReason hideReason) {
            if (hideReason == SubmenuHideReason.Closed) {
                eventSubscription?.Dispose();
                eventSubscription = null;
            }
            base.Hide(hideReason);
        }

        public unsafe void PopulateBanList() {
            int selectedIndex = -1;
            for (int i = 0; i < entries.Count; i++) {
                var entry = entries[i];
                if (EventSystem.current.currentSelectedGameObject == entry.GameObject) {
                    selectedIndex = i;
                }
                Destroy(entry.GameObject);
            }
            entries.Clear();

            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            var bans = f.ResolveList(f.Global->BannedPlayerIds);
            for (int i = 0; i < bans.Count; i++) {
                var ban = bans.GetPointer(i);

                GameObject newInstance = Instantiate(template, template.transform.parent);
                SelectablePromptLabel label = newInstance.transform.GetChild(0).GetComponent<SelectablePromptLabel>();
                label.translationKey = ban->Nickname;
                label.UpdateLabel();
                Button newButton = newInstance.GetComponentInChildren<Button>();

                int i2 = i;
                newButton.onClick.AddListener(() => UnbanViaIndex(i2));

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

            Button backButtonBtn = backButton.GetComponent<Button>();
            var backNav = backButtonBtn.navigation;
            if (entries.Count > 0) {
                Button lastButton = entries[^1].GameObject.GetComponentInChildren<Button>();
                var lastNav = lastButton.navigation;
                lastNav.selectOnDown = backButton.GetComponentInChildren<Button>();
                lastButton.navigation = lastNav;

                backNav.selectOnUp = lastButton;
            } else {
                backNav.selectOnUp = null;
            }
            backButtonBtn.navigation = backNav;

            if (entries.Count > 0 && selectedIndex != -1) {
                EventSystem.current.SetSelectedGameObject(entries[Mathf.Clamp(selectedIndex, 0, entries.Count - 1)].GameObject);
            }

            noBansText.gameObject.SetActive(entries.Count == 0);
        }

        public unsafe void UnbanViaIndex(int index) {
            BanEntry entry = entries[index];

            var game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (!game.PlayerIsLocal(host)) {
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Error);
                return;
            }
            int slot = game.GetLocalPlayerSlots().IndexOf(host);
            game.SendCommand(slot, new CommandUnbanPlayer() {
                TargetUserId = entry.UserId,
            });

            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
            PopulateBanList();
        }

        public void OnPlayerUnbanned(EventPlayerUnbanned e) {
            PopulateBanList();
        }

        public class BanEntry {
            public GameObject GameObject { get; set; }
            public string Nickname { get; set; }
            public string UserId { get; set; }
        }
    }
}