using JimmysUnityUtilities;
using NSMB.Extensions;
using NSMB.Translation;
using TMPro;
using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(TMP_Text))]
public class Underlay : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Vector2 offset;
    [SerializeField, HideInInspector] private TMP_Text text, child;

    //---Private Variables
    private bool initialized;

    public void OnValidate() {
        this.SetIfNull(ref text);
        ValidationUtility.SafeOnValidate(() => {
            if (this) {
                Start();
            }
        });
    }

    public void Start() {
        if (transform.parent.TryGetComponent(out Underlay _)) {
            return;
        }

        if (!child) {
            child = Instantiate(text, text.transform, true);
            Canvas c = child.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = GetComponentInParent<Canvas>().sortingOrder - 1;
            child.RemoveComponentImmediate<Underlay>();
            child.RemoveComponentImmediate<TMP_Translatable>();
            child.RemoveComponentImmediate<TMP_SubTranslatable>();
            child.enableVertexGradient = false;
            child.color = Color.black;
        }
        child.transform.localPosition = offset;
        if (!initialized) {
            text.OnPreRenderText += OnPreRenderParentText;
            initialized = true;
        }
    }

    public void OnDestroy() {
        text.OnPreRenderText -= OnPreRenderParentText;
    }

    private void OnPreRenderParentText(TMP_TextInfo obj) {
        child.text = text.text;
        child.ForceMeshUpdate();
    }
}