using UnityEngine;
using TMPro;

namespace NSMB.UI.MainMenu.Submenus.Main {
    public class VersionNamer : MonoBehaviour {
        public void Start() {
            GetComponent<TMP_Text>().text = "v" + Application.version;
        }
    }
}
