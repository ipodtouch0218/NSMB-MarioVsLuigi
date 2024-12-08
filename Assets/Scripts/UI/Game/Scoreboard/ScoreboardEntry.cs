using NSMB.Utils;
using Quantum;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Scoreboard {
    public class ScoreboardEntry : MonoBehaviour {

        //---Properties
        public EntityRef Target { get; private set; }

        //---Serialized Variables
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nicknameText, scoreText;

        //---Private Variables
        private ScoreboardUpdater updater;
        private string nickname = "noname";
        private bool isValidPlayer;
        private string nicknameColor = "#FFFFFF";
        private bool constantNicknameColor = true;

        public void Start() {
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned);
        }

        public unsafe void Initialize(Frame f, EntityRef target, ScoreboardUpdater updater) {
            Target = target;
            this.updater = updater;

            if (f.Unsafe.TryGetPointer(target, out MarioPlayer* mario)) {
                RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);
                if (runtimePlayer != null) {
                    nickname = runtimePlayer.PlayerNickname;
                    isValidPlayer = true;
                    nicknameColor = runtimePlayer.NicknameColor;
                    nicknameText.color = Utils.Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
                }
            }
            UpdateEntry(f);
            gameObject.SetActive(true);
        }

        public void Update() {
            if (!constantNicknameColor) {
                nicknameText.color = Utils.Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
            }
        }

        public unsafe void UpdateEntry(Frame f) {
            if (!f.Unsafe.TryGetPointer(Target, out MarioPlayer* mario)) {
                Color dcColor = Utils.Utils.GetPlayerColor(f, PlayerRef.None);
                dcColor.a = 0.5f;
                background.color = dcColor;
                return;
            }

            var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);

            int ping = playerData != null ? playerData->Ping : (isValidPlayer ? -1 : 0);
            nicknameText.text = Utils.Utils.GetPingSymbol(ping) + nickname.ToValidUsername(f, mario->PlayerRef);

            StringBuilder scoreBuilder = new();
            if (f.Global->Rules.IsLivesEnabled) {
                var character = f.FindAsset(mario->CharacterAsset);
                scoreBuilder.Append(character.UiString).Append(Utils.Utils.GetSymbolString(mario->Lives.ToString()));
            }
            scoreBuilder.Append(Utils.Utils.GetSymbolString('S' + mario->Stars.ToString()));

            scoreText.text = scoreBuilder.ToString();
            updater.RequestSorting = true;

            Color backgroundColor = Utils.Utils.GetPlayerColor(f, mario->PlayerRef);
            backgroundColor.a = 0.5f;
            background.color = backgroundColor;
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Frame);
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Frame);
        }

        private void OnMarioPlayerDroppedStar(EventMarioPlayerDroppedStar e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Frame);
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Frame);
        }
    }
}
