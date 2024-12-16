using NSMB.Entities.Player;
using NSMB.Utils;
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
        [SerializeField] private RectTransform parentTransform;

        [SerializeField] private Vector2 temp = Vector2.one;

        //---Private Variables
        private MarioPlayerAnimator parent;
        private CharacterAsset character;
        private VersusStageData stage;

        private PlayerElements elements;
        private string cachedNickname = "noname";
        private string nicknameColor = "#FFFFFF";
        private bool constantNicknameColor = true;
        private QuantumGame game;

        public unsafe void Initialize(QuantumGame game, Frame f, PlayerElements elements, MarioPlayerAnimator parent) {
            this.game = game;
            this.elements = elements;
            this.parent = parent;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(parent.EntityRef);
            this.character = f.FindAsset(mario->CharacterAsset);
            stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);
            nicknameColor = runtimePlayer?.NicknameColor ?? "#FFFFFF";
            cachedNickname = runtimePlayer?.PlayerNickname.ToValidUsername(f, mario->PlayerRef);

            arrow.color = parent.GlowColor;
            text.color = Utils.Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
            gameObject.SetActive(true);

            UpdateText(f);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            QuantumEvent.Subscribe<EventMarioPlayerDroppedStar>(this, OnMarioPlayerDroppedStar);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            QuantumEvent.Subscribe<EventMarioPlayerPreRespawned>(this, OnMarioPlayerPreRespawned);
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

            nametag.SetActive(elements.Entity != Entity && !(mario->IsDead && mario->IsRespawning) && f.Global->GameState >= GameState.Playing);
            if (!nametag.activeSelf) {
                return;
            }

            var shape = f.Unsafe.GetPointer<PhysicsCollider2D>(Entity)->Shape;
            Vector2 worldPos = parent.models.transform.position;
            worldPos.y += shape.Box.Extents.Y.AsFloat * 2.4f + 0.5f;

            Camera cam = elements.Camera;
            if (stage.IsWrappingLevel) {
                // Wrapping
                if (Mathf.Abs(worldPos.x - cam.transform.position.x) > (stage.TileDimensions.x * 0.25f)) {
                    worldPos.x += (cam.transform.position.x > ((stage.StageWorldMin.X + stage.StageWorldMax.X) / 2).AsFloat ? 1 : -1) * (stage.TileDimensions.x * 0.5f);
                }
            }

            transform.localPosition = cam.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * parentTransform.rect.size;
            transform.localPosition -= (Vector3) (parentTransform.rect.size / 2);

            if (!constantNicknameColor) {
                text.color = Utils.Utils.SampleNicknameColor(nicknameColor, out _);
            }
        }

        private static readonly StringBuilder stringBuilder = new();
        public unsafe void UpdateText(Frame f) {
            if (!f.Unsafe.TryGetPointer(Entity, out MarioPlayer* mario)) {
                return;
            }

            stringBuilder.Clear();

            if (f.Global->Rules.TeamsEnabled && Settings.Instance.GraphicsColorblind) {
                TeamAsset team = f.SimulationConfig.Teams[mario->Team];
                stringBuilder.Append(team.textSpriteColorblindBig);
            }
            stringBuilder.AppendLine(cachedNickname);

            if (f.Global->Rules.IsLivesEnabled) {
                stringBuilder.Append(character.UiString).Append(Utils.Utils.GetSymbolString("x" + mario->Lives)).Append(' ');
            }

            stringBuilder.Append(Utils.Utils.GetSymbolString("Sx" + mario->Stars));

            text.text = stringBuilder.ToString();
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Frame);
        }

        private void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Frame);
        }

        private void OnMarioPlayerDroppedStar(EventMarioPlayerDroppedStar e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Frame);
        }

        private void OnMarioPlayerPreRespawned(EventMarioPlayerPreRespawned e) {
            if (e.Entity != Entity) {
                return;
            }

            UpdateText(e.Frame);
        }
    }
}
