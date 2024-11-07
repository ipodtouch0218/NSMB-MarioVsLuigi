using NSMB.Utils;
using Quantum;
using System.Drawing.Drawing2D;
using System.Net.NetworkInformation;
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
    private string nickname = "noname";
    private bool isValidPlayer;

    public void Start() {
        QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
        QuantumEvent.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
    }

    public unsafe void Initialize(Frame f, EntityRef target, ScoreboardUpdater updater) {
        Target = target;
        this.updater = updater;

        if (f.Unsafe.TryGetPointer(target, out MarioPlayer* mario)) {
            RuntimePlayer runtimeData = f.GetPlayerData(mario->PlayerRef);
            if (runtimeData != null) {
                nickname = runtimeData.PlayerNickname;
                isValidPlayer = true;
            }
        }
        UpdateEntry(f);
        gameObject.SetActive(true);
    }

    public unsafe void UpdateEntry(Frame f) {
        if (!f.Exists(Target)) {
            return;
        }

        var mario = f.Unsafe.GetPointer<MarioPlayer>(Target);
        var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);

        int ping = playerData != null ? playerData->Ping : (isValidPlayer ? -1 : 0);
        nicknameText.text = Utils.GetPingSymbol(ping) + nickname.ToValidUsername();

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