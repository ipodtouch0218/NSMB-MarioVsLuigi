using NSMB.Utilities;
using Quantum;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game.Scoreboard {
    public unsafe class ScoreboardEntry : MonoBehaviour {

        //---Properties
        public EntityRef Target { get; private set; }

        //---Serialized Variables
        [SerializeField] private Image background, pingIndicator;
        [SerializeField] private TMP_Text nicknameText, scoreText;

        //---Private Variables
        private ScoreboardUpdater updater;
        private int informationIndex;
        private NicknameColor nicknameColor = NicknameColor.White;
        private string cachedNickname, cachedPingSymbol;
        private bool nicknameMayHaveChanged;

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerObjectiveCoinsChanged>(this, OnMarioPlayerObjectiveCoinsChanged);
            QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned);
            QuantumEvent.Subscribe<EventMarioPlayerDestroyed>(this, OnMarioPlayerDestroyed);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);

            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                UpdateEntry(game.Frames.Predicted);
            }
        }

        public void Initialize(Frame f, int index, EntityRef target, ScoreboardUpdater updater) {
            Target = target;
            this.updater = updater;

            informationIndex = index;
            ref PlayerInformation info = ref f.Global->PlayerInfo[index];
            cachedNickname = info.Nickname.ToString().ToValidNickname(f, info.PlayerRef);
            nicknameColor = NicknameColor.Parse(info.NicknameColor.ToString());
            nicknameText.color = nicknameColor.Sample();
            nicknameMayHaveChanged = true;

            UpdateEntry(f);
            gameObject.SetActive(true);
        }

        public void Update() {
            if (!nicknameColor.Constant) {
                nicknameText.color = nicknameColor.Sample();
            }
        }

        public void UpdatePing(Frame f) {
            ref PlayerInformation info = ref f.Global->PlayerInfo[informationIndex];
            var playerData = QuantumUtils.GetPlayerData(f, info.PlayerRef);
            int ping = (!info.Disconnected && playerData != null) ? playerData->Ping : -1;
            pingIndicator.sprite = Utils.GetPingSprite(ping);
        }

        public void UpdateEntry(Frame f) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            ref PlayerInformation info = ref f.Global->PlayerInfo[informationIndex];
            var playerData = QuantumUtils.GetPlayerData(f, info.PlayerRef);
            
            UpdatePing(f);

            if (nicknameMayHaveChanged) {
                nicknameText.text = cachedNickname;
                nicknameMayHaveChanged = false;
            }

            Color backgroundColor = Utils.GetPlayerColor(f, info.PlayerRef, considerDisqualifications: true);
            backgroundColor.a = 0.5f;
            background.color = backgroundColor;

            CharacterAsset character = f.FindAsset(f.SimulationConfig.CharacterDatas[info.Character]);
            int objective = 0;
            int lives = 0;
            if (f.Unsafe.TryGetPointer(Target, out MarioPlayer* mario)) {
                objective = Mathf.Max(0, gamemode.GetObjectiveCount(f, mario));
                lives = mario->Disconnected ? 0 : mario->Lives;
            }

            StringBuilder scoreBuilder = new();
            if (f.Global->Rules.IsLivesEnabled) {
                scoreBuilder.Append(character.UiString).Append(Utils.GetSymbolString(lives.ToString()));
            }
            scoreBuilder.Append(Utils.GetSymbolString(gamemode.ObjectiveSymbolPrefix + objective.ToString()));

            scoreText.text = scoreBuilder.ToString();
            updater.RequestSorting = true;
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerObjectiveCoinsChanged(EventMarioPlayerObjectiveCoinsChanged e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnPlayerDataChanged(EventPlayerDataChanged e) {
            Frame f = e.Game.Frames.Predicted;
            if (e.Player != f.Global->PlayerInfo[informationIndex].PlayerRef) {
                return;
            }

            UpdateEntry(f);
        }

        private void OnMarioPlayerDroppedStar(EventMarioPlayerDroppedStar e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerDestroyed(EventMarioPlayerDestroyed e) {
            if (e.Entity != Target) {
                return;
            }

            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            UpdateEntry(e.Game.Frames.Predicted);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            Frame f = e.Game.Frames.Verified;
            ref PlayerInformation info = ref f.Global->PlayerInfo[informationIndex];
            cachedNickname = info.Nickname.ToString().ToValidNickname(f, info.PlayerRef);
            nicknameMayHaveChanged = true;

            UpdateEntry(f);
        }
    }
}
