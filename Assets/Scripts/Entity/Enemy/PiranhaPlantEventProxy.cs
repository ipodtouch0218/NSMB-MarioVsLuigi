using NSMB.Extensions;
using UnityEngine;

public class PiranhaPlantEventProxy : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private PiranhaPlantAnimator parent;

    public void OnValidate() {
        this.SetIfNull(ref parent, UnityExtensions.GetComponentType.Parent);
    }

    public void PlayChompSound() {
        parent.PlayChompSound();
    }
}