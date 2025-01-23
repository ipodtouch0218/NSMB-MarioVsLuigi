using NSMB.Utils;
using Quantum;
using System.CodeDom;
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
        private int informationIndex;
        private string nickname, nicknameColor;
        private bool constantNicknameColor = true;

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned);
            QuantumEvent.Subscribe<EventMarioPlayerDestroyed>(this, OnMarioPlayerDestroyed);

            if (NetworkHandler.Game != null) {
                UpdateEntry(NetworkHandler.Game.Frames.Predicted);
            }
        }

        public unsafe void Initialize(Frame f, int index, EntityRef target, ScoreboardUpdater updater) {
            Target = target;
            this.updater = updater;

            informationIndex = index;
            ref PlayerInformation info = ref f.Global->PlayerInfo[index];
            nickname = info.Nickname;
            nicknameColor = info.NicknameColor;
            nicknameText.color = Utils.Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);

            UpdateEntry(f);
            gameObject.SetActive(true);
        }

        public void Update() {
            if (!constantNicknameColor) {
                nicknameText.color = Utils.Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
            }
        }

        public unsafe void UpdateEntry(Frame f) {
            ref PlayerInformation info = ref f.Global->PlayerInfo[informationIndex];

            var playerData = QuantumUtils.GetPlayerData(f, info.PlayerRef);
            int ping = (!info.Disconnected && playerData != null) ? playerData->Ping : -1;
            nicknameText.text = Utils.Utils.GetPingSymbol(ping) + nickname.ToValidUsername(f, info.PlayerRef);

            Color backgroundColor = Utils.Utils.GetPlayerColor(f, info.PlayerRef, considerDisqualifications: true);
            backgroundColor.a = 0.5f;
            background.color = backgroundColor;

            var character = f.SimulationConfig.CharacterDatas[info.Character];
            int stars = 0;
            int lives = 0;
            if (f.Unsafe.TryGetPointer(Target, out MarioPlayer* mario)) {
                stars = mario->Stars;
                lives = mario->Lives;
            }

            StringBuilder scoreBuilder = new();
            if (f.Global->Rules.IsLivesEnabled) {
                scoreBuilder.Append(character.UiString).Append(Utils.Utils.GetSymbolString(lives.ToString()));
            }
            scoreBuilder.Append(Utils.Utils.GetSymbolString('S' + stars.ToString()));

            scoreText.text = scoreBuilder.ToString();
            updater.RequestSorting = true;
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

        private void OnMarioPlayerDestroyed(EventMarioPlayerDestroyed e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Frame);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            UpdateEntry(e.Game.Frames.Predicted);
        }
    }
}
