using UnityEngine;

namespace NSMB.Entities.Enemies {
    public class PiranhaPlantAnimationEvent : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PiranhaPlant controller;

        public void OnValidate() {
            if (!controller) controller = GetComponentInParent<PiranhaPlant>();
        }

        public void PlayChompSound() {
            controller.PlaySound(Enums.Sounds.Enemy_PiranhaPlant_Chomp);
        }
    }
}
