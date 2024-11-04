using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Translation;
using NSMB.Utils;
using Quantum;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerElements : MonoBehaviour {

    public static HashSet<PlayerElements> AllPlayerElements = new();

    //---Properties
    public PlayerRef Player => player;
    public EntityRef Entity => spectating ? spectatingEntity : entity;
    public Camera Camera => ourCamera;
    public CameraAnimator CameraAnimator => cameraAnimator;
    public ReplayUI ReplayUi => replayUi;

    //---Serialized Variables
    [SerializeField] private RawImage image;
    [SerializeField] private UIUpdater uiUpdater;
    [SerializeField] private CameraAnimator cameraAnimator;
    [SerializeField] private Camera ourCamera;
    [SerializeField] private InputCollector inputCollector;
    [SerializeField] private ScoreboardUpdater scoreboardUpdater;
    [SerializeField] private ReplayUI replayUi;

    [SerializeField] private GameObject spectationUI;
    [SerializeField] private TMP_Text spectatingText;
    [SerializeField] private PlayerNametag nametagPrefab;
    [SerializeField] public GameObject nametagCanvas;

    //---Private Variables
    private PlayerRef player;
    private EntityRef entity;

    private bool spectating;
    private EntityRef spectatingEntity;

    public void OnValidate() {
        this.SetIfNull(ref image);
        this.SetIfNull(ref uiUpdater);
        this.SetIfNull(ref cameraAnimator);
        this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref inputCollector);
        this.SetIfNull(ref scoreboardUpdater, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref replayUi, UnityExtensions.GetComponentType.Children);
    }

    public void OnEnable() {
        AllPlayerElements.Add(this);
        ControlSystem.controls.UI.SpectatePlayerByIndex.performed += SpectatePlayerIndex;
        ControlSystem.controls.UI.Next.performed += SpectateNextPlayer;
        ControlSystem.controls.UI.Previous.performed += SpectatePreviousPlayer;
        TranslationManager.OnLanguageChanged += OnLanguageChanged;
    }

    public void OnDisable() {
        AllPlayerElements.Remove(this);
        ControlSystem.controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
        ControlSystem.controls.UI.Next.performed -= SpectateNextPlayer;
        ControlSystem.controls.UI.Previous.performed -= SpectatePreviousPlayer;
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Start() {
        nametagCanvas.SetActive(Settings.Instance.GraphicsPlayerNametags);
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void Initialize(QuantumGame game, Frame f, EntityRef entity, PlayerRef player) {
        this.player = player;
        this.entity = entity;

        Camera.transform.SetParent(null);
        Camera.transform.localScale = Vector3.one;
        uiUpdater.Initialize(game, f);
        scoreboardUpdater.Initialize();

        foreach (var mario in MarioAnimator.AllMarioPlayers) {
            MarioPlayerInitialized(game, f, mario);
        }
        MarioAnimator.MarioPlayerInitialized += MarioPlayerInitialized;
    }

    public void OnDestroy() {
        MarioAnimator.MarioPlayerInitialized -= MarioPlayerInitialized;
    }

    private void MarioPlayerInitialized(QuantumGame game, Frame f, MarioAnimator mario) {
        PlayerNametag newNametag = Instantiate(nametagPrefab, nametagPrefab.transform.parent);
        newNametag.Initialize(game, f, this, mario);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        Frame f = e.Game.Frames.Predicted;

        if (!f.Exists(entity)) {
            // Spectating
            StartSpectating();

            if (!f.Exists(spectatingEntity)) {
                // Find a new player to spectate
                SpectateNextPlayer();
            }
        }
    }

    private void CreateNametags() {

    }

    public unsafe void UpdateSpectateUI() {
        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
        var mario = f.Unsafe.GetPointer<MarioPlayer>(spectatingEntity);
        RuntimePlayer runtimePlayer = f.GetPlayerData(mario->PlayerRef);

        string username = runtimePlayer.PlayerNickname.ToValidUsername();

        TranslationManager tm = GlobalController.Instance.translationManager;
        spectatingText.text = tm.GetTranslationWithReplacements("ui.game.spectating", "playername", username);
    }

    public void StartSpectating() {
        spectating = true;

        if (!NetworkHandler.IsReplay) {
            spectationUI.SetActive(true);
        }

        if (GlobalController.Instance.loadingCanvas.isActiveAndEnabled) {
            GlobalController.Instance.loadingCanvas.EndLoading(QuantumRunner.DefaultGame);
        }

        SpectateNextPlayer();
    }

    public void SpectateNextPlayer(InputAction.CallbackContext context) {
        if (!spectating) {
            return;
        }

        SpectateNextPlayer();
    }

    public unsafe void SpectateNextPlayer() {
        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
        List<EntityRef> marios = new();

        var marioFilter = f.Filter<MarioPlayer>();
        while (marioFilter.NextUnsafe(out EntityRef entity, out _)) {
            marios.Add(entity);
        }

        if (marios.Count <= 0) {
            return;
        }

        int currentIndex = -1;
        for (int i = 0; i < marios.Count; i++) {
            if (spectatingEntity == marios[i]
                || marios[i].Index > spectatingEntity.Index) {

                currentIndex = i;
                break;
            }
        }
        spectatingEntity = marios[(currentIndex + 1) % marios.Count];
        UpdateSpectateUI();
    }

    public void SpectatePreviousPlayer(InputAction.CallbackContext context) {
        if (!spectating) {
            return;
        }

        SpectateNextPlayer();
    }

    public unsafe void SpectatePreviousPlayer() {
        Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
        List<EntityRef> marios = new();

        var marioFilter = f.Filter<MarioPlayer>();
        while (marioFilter.NextUnsafe(out EntityRef entity, out _)) {
            marios.Add(entity);
        }

        if (marios.Count <= 0) {
            return;
        }

        int currentIndex = -1;
        for (int i = marios.Count - 1; i >= 0; i--) {
            if (spectatingEntity == marios[i]
                || marios[i].Index < spectatingEntity.Index) {

                currentIndex = i;
                break;
            }
        }
        spectatingEntity = marios[(currentIndex - 1 + marios.Count) % marios.Count];
        UpdateSpectateUI();
    }

    private void SpectatePlayerIndex(InputAction.CallbackContext context) {
        if (!spectating) {
            return;
        }

        if (int.TryParse(context.control.name, out int index)) {
            index += 9;
            index %= 10;

            EntityRef newTarget = scoreboardUpdater.EntityAtPosition(index);
            if (newTarget != EntityRef.None) {
                spectatingEntity = newTarget;
                UpdateSpectateUI();
            }
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateSpectateUI();
    }
}