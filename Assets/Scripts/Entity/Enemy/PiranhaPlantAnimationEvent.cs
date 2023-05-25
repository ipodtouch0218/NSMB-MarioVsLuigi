using UnityEngine;

namespace NSMB.Entities.Enemies {
    public class PiranhaPlantAnimationEvent : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PiranhaPlantController controller;

        public void OnValidate() {
            if (!controller) controller = GetComponentInParent<PiranhaPlantController>();
        }

        public void PlayChompSound() {
            controller.PlaySound(Enums.Sounds.Enemy_PiranhaPlant_Chomp);
        }
    }
}
