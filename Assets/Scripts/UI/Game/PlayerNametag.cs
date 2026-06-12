using NSMB.Entities.Player;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public class PlayerNametag : MonoBehaviour {

        //---Properties
        public EntityRef Entity => parent.EntityRef;

        //---Serailzied Variables
        [SerializeField] private GameObject nametag;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Image arrow;
        [SerializeField] private Canvas parentCanvas;

        [SerializeField] private Vector2 temp = Vector2.one;

        //---Private Variables
        private MarioPlayerAnimator parent;
        private CharacterAsset character;
        private VersusStageData stage;

        private PlayerElements elements;
        private string cachedNickname = "noname";
        private NicknameColor nicknameColor = NicknameColor.White;
        private QuantumGame game;
        private StringBuilder stringBuilder = new();

        public unsafe void Initialize(QuantumGame game, Frame f, PlayerElements elements, MarioPlayerAnimator parent) {
            this.game = game;
            this.elements = elements;
            this.parent = parent;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(Entity);
            character = f.FindAsset(mario->CharacterAsset);
            stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            UpdateCachedNickname(f, mario);

            arrow.color = parent.GlowColor;
            text.color = nicknameColor.Sample();
            gameObject.SetActive(true);

            UpdateText(f);
            Settings.OnColorblindModeChanged += OnColorblindModeChanged;
        }

        public void Start() {
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
            QuantumEvent.Subscribe<EventMarioPlayerObjectiveCoinsChanged>(this, OnMarioPlayerObjectiveCoinsChanged);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);

            var game = QuantumRunner.DefaultGame;
            if (game != null) {
                UpdateText(game.Frames.Predicted);
            }
        }

        public void OnDestroy() {
            Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
        }

        public unsafe void LateUpdate() {
            // If our parent object despawns, we also die.
            if (!parent) {
                Destroy(gameObject);
                return;
            }

            Frame f = game.Frames.Predicted;
            if (!f.Unsafe.TryGetPointer(Entity, out MarioPlayer* mario)) {
                return;
            }

            bool shouldBeInactive = f.Global->GameState < GameState.Playing
                || elements.Entity == Entity
                || mario->IsRespawning
                || (mario->IsDead && parent.IsBelowDeathplane);

            if (shouldBeInactive && nametag.activeSelf) {
                nametag.SetActive(false);
                return;
            } else if (!shouldBeInactive && !nametag.activeSelf) {
                nametag.SetActive(true);
            }

            var shape = f.Unsafe.GetPointer<PhysicsCollider2D>(Entity)->Shape;
            Vector2 worldPos = parent.ModelRoot.transform.position;
            worldPos.y += shape.Box.Extents.Y.AsFloat * 2.4f + 0.5f;

            Camera cam = elements.Camera;
            if (stage.IsWrappingLevel) {
                // Wrapping
                if (Mathf.Abs(worldPos.x - cam.transform.position.x) > (stage.TileDimensions.X * 0.25f)) {
                    worldPos.x += (cam.transform.position.x > ((stage.StageWorldMin.X + stage.StageWorldMax.X) / 2).AsFloat ? 1 : -1) * (stage.TileDimensions.X * 0.5f);
                }
            }

            RectTransform parentTransform = (RectTransform) transform.parent;
            transform.localPosition = (cam.WorldToViewportPoint(worldPos) * 2) - Vector3.one;
            transform.localPosition = transform.localPosition.Multiply(parentTransform.rect.size / 2);
            transform.localScale = Vector3.one * (3.5f / cam.orthographicSize);

            if (!nicknameColor.Constant) {
                text.color = nicknameColor.Sample();
            }
        }

        public unsafe void UpdateText(Frame f) {
            if (!f.Unsafe.TryGetPointer(Entity, out MarioPlayer* mario)) {
                return;
            }
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

            stringBuilder.Clear();

            if (Settings.Instance.GraphicsColorblind) {
                if (f.Global->Rules.TeamsEnabled && mario->GetTeam(f) is byte teamIndex) {
                    var teams = f.Context.GetAllAssets<TeamAsset>();
                    TeamAsset team = teams[teamIndex % teams.Count];
                    stringBuilder.Append(team.textSpriteColorblindBig);
                } else {
                    stringBuilder.Append(Utils.GetPlayerIcon(f, mario->PlayerRef));
                }
            }
            stringBuilder.AppendLine(cachedNickname);

            if (f.Global->Rules.IsLivesEnabled) {
                stringBuilder.Append(character.UiString).Append(Utils.GetSymbolString("x" + mario->Lives)).Append(' ');
            }

            stringBuilder.Append(Utils.GetSymbolString(gamemode.ObjectiveSymbolPrefix + "x" + Mathf.Max(0, gamemode.GetObjectiveCount(f, mario))));

            text.SetText(stringBuilder);
        }

        public unsafe void UpdateCachedNickname(Frame f, MarioPlayer* mario) {
            RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);
            if (runtimePlayer != null) {
                cachedNickname = runtimePlayer.PlayerNickname.ToValidNickname(f, mario->PlayerRef);
                nicknameColor = NicknameColor.Parse(runtimePlayer.NicknameColor);
            }
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerObjectiveCoinsChanged(EventMarioPlayerObjectiveCoinsChanged e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerDroppedStar(EventMarioPlayerDroppedStar e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Game.Frames.Predicted);
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Game.Frames.Predicted);
        }

        private void OnGameResynced(CallbackGameResynced e) {
            UpdateText(e.Game.Frames.Predicted);
        }

        private unsafe void OnPlayerRemoved(EventPlayerRemoved e) {
            Frame f = e.Game.Frames.Verified;
            if (f.Unsafe.TryGetPointer(Entity, out MarioPlayer* mario)) {
                UpdateCachedNickname(f, mario);
            }
            UpdateText(f);
        }

        private void OnColorblindModeChanged() {
            if (QuantumRunner.DefaultGame != null) {
                UpdateText(QuantumRunner.DefaultGame.Frames.Predicted);
            }
        }
    }
}
