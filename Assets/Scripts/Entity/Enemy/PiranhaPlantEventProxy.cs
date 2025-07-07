using NSMB.Utilities.Extensions;
using UnityEngine;

namespace NSMB.Entities.Enemies {
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
}
