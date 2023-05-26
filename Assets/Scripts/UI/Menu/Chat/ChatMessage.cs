using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Utils;
using NSMB.Translation;

public class ChatMessage : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private Image image;

    //---Properties
    public bool IsSystemMessage => key != null;

    //---Public Variables
    public PlayerRef player;

    //---Private Variables
    private string key;
    private string[] replacements;

    public void OnValidate() {
        if (!chatText) chatText = GetComponent<TMP_Text>();
        if (!image) image = GetComponent<Image>();
    }

    public void OnDestroy() {
        GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(TranslationManager tm) {
        chatText.text = tm.GetTranslationWithReplacements(key, replacements);
        chatText.isRightToLeftText = tm.RightToLeft;
    }

    public void Initialize(string message, PlayerRef player, Color? color = null) {
        chatText.text = message;
        chatText.color = color ?? Color.black;
        this.player = player;

        UpdatePlayerColor();
    }

    public void InitializeSystem(string key, string[] replacements, Color? color = null) {
        this.key = key;
        this.replacements = replacements;
        chatText.color = color ?? Color.black;

        GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);

        UpdatePlayerColor();
    }

    public void UpdatePlayerColor() {
        if (IsSystemMessage) {
            image.color = Color.white;
        } else {
            image.color = Utils.GetPlayerColor(NetworkHandler.Runner, player, 0.15f);
        }
    }
}
