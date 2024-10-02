using NSMB.Utils;
using Quantum;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardEntry : MonoBehaviour {

    //---Properties
    public EntityRef Target { get; private set; }

    //---Serialized Variables
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text nicknameText, scoreText;

    //---Private Variables
    private ScoreboardUpdater updater;

    public void Start() {
        QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
        QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
    }

    public void Initialize(Frame f, EntityRef target, ScoreboardUpdater updater) {
        Target = target;
        this.updater = updater;

        UpdateEntry(f);
        gameObject.SetActive(true);
    }

    public unsafe void UpdateEntry(Frame f) {
        if (!f.Exists(Target)) {
            return;
        }

        var mario = f.Unsafe.GetPointer<MarioPlayer>(Target);
        var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);

        if (playerData == null) {
            return;
        }

        RuntimePlayer runtimeData = f.GetPlayerData(mario->PlayerRef);
        nicknameText.text = Utils.GetPingSymbol(playerData->Ping) + runtimeData.PlayerNickname.ToValidUsername();

        StringBuilder scoreBuilder = new();
        if (f.Global->Rules.IsLivesEnabled) {
            var character = f.FindAsset(mario->CharacterAsset);
            scoreBuilder.Append(character.UiString).Append(mario->Lives);
        }
        scoreBuilder.Append(Utils.GetSymbolString("S" + mario->Stars));

        scoreText.text = scoreBuilder.ToString();
        updater.RequestSorting = true;

        Color backgroundColor = Utils.GetPlayerColor(f, mario->PlayerRef);
        backgroundColor.a = 0.2f;
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
}