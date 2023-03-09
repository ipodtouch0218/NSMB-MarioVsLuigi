using System.Text;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LoadStringFromFile : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private string filepath;

    //---Private Variables
    private TMP_Text text;

    public void Awake() {
        text = GetComponentInParent<TMP_Text>();
        LoadString();
    }

    public void LoadString() {
        TextAsset credits = (TextAsset) Resources.Load(filepath);
        text.text = Encoding.ASCII.GetString(credits.bytes);
    }
}
