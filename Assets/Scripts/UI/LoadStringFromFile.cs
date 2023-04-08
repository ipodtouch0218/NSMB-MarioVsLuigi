using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LoadStringFromFile : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TextAsset source;
    [SerializeField] private TMP_Text text;

    public void OnValidate() {
        if (!text) text = GetComponentInParent<TMP_Text>();
    }

    public void OnEnable() {
        GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);
    }

    public void OnDisable() {
        GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(NSMB.Translation.TranslationManager tm) {
        Debug.Log("A");
        text.text = tm.GetSubTranslations(source.text);
    }
}
