using NSMB.Entities.Player;
using NSMB.Utils;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNametag : MonoBehaviour {

    //---Serailzied Variables
    [SerializeField] private GameObject nametag;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Image arrow;
    [SerializeField] private RectTransform parentTransform;

    [SerializeField] private Vector2 temp = Vector2.one;

    //---Private Variables
    private MarioAnimator parent;
    private CharacterAsset character;
    private VersusStageData stage;

    private PlayerElements elements;
    private string cachedNickname = "noname";
    private string nicknameColor = "#FFFFFF";
    private bool constantNicknameColor = false;
    private QuantumGame game;

    public unsafe void Initialize(QuantumGame game, Frame f, PlayerElements elements, MarioAnimator parent) {
        this.game = game;
        this.elements = elements;
        this.parent = parent;

        var mario = f.Unsafe.GetPointer<MarioPlayer>(parent.entity.EntityRef);
        this.character = f.FindAsset(mario->CharacterAsset);
        stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

        RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);
        nicknameColor = runtimePlayer?.NicknameColor ?? "#FFFFFF";

        arrow.color = parent.GlowColor;
        text.color = Utils.SampleNicknameColor(nicknameColor, out constantNicknameColor);
        gameObject.SetActive(true);
    }

    public unsafe void LateUpdate() {
        // If our parent object despawns, we also die.
        if (!parent) {
            Destroy(gameObject);
            return;
        }

        EntityRef entity = parent.entity.EntityRef;
        Frame f = game.Frames.Predicted;
        var mario = f.Unsafe.GetPointer<MarioPlayer>(entity);

        if (!character) {
            character = f.FindAsset(mario->CharacterAsset);
        }

        nametag.SetActive(elements.Entity != entity && !(mario->IsDead && mario->IsRespawning) && f.Global->GameState >= GameState.Playing);

        if (!nametag.activeSelf) {
            return;
        }

        var shape = f.Unsafe.GetPointer<PhysicsCollider2D>(entity)->Shape;

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

        RuntimePlayer player = f.GetPlayerData(mario->PlayerRef);
        if (player != null) {
            cachedNickname = player.PlayerNickname.ToValidUsername();
        }
        // TODO: this allocates every frame.
        string newText = "";

        if (f.Global->Rules.TeamsEnabled && Settings.Instance.GraphicsColorblind) {
            TeamAsset team = f.SimulationConfig.Teams[mario->Team];
            newText += team.textSpriteColorblindBig;
        }
        newText += cachedNickname + "\n";

        if (f.Global->Rules.LivesEnabled) {
            newText += character.UiString + Utils.GetSymbolString("x" + mario->Lives + " ");
        }

        newText += Utils.GetSymbolString("Sx" + mario->Stars);

        text.text = newText;
        if (!constantNicknameColor) {
            text.color = Utils.SampleNicknameColor(nicknameColor, out _);
        }
    }
}
