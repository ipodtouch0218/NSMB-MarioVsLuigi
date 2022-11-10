using UnityEngine;
using TMPro;

namespace NSMB.Loading {
    public class LoadingLevelCreator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;
        [SerializeField] private string templateString = "Level designed by: {0}";

        public void OnEnable() {
            if (!string.IsNullOrEmpty(GameManager.Instance.levelDesigner))
                text.text = string.Format(templateString, GameManager.Instance.levelDesigner);
        }
    }
}
