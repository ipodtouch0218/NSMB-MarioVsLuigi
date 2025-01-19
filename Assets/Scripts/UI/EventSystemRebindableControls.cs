using NSMB.Extensions;
using UnityEngine;
using UnityEngine.InputSystem.UI;

[RequireComponent(typeof(InputSystemUIInputModule))]
public class EventSystemRebindableControls : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private InputSystemUIInputModule inputSystem;

    public void OnValidate() {
        this.SetIfNull(ref inputSystem);
    }

    public void Start() {
        inputSystem.actionsAsset = Settings.Controls.asset;
    }
}
