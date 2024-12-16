using NSMB.Extensions;
using UnityEngine;

namespace NSMB.Loading {
    public class LoadingPowerup : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private int marioX = -296, peachX = 296, minX = -410;
        [SerializeField] private Vector3 movementSpeed;
        [SerializeField] private MarioLoader mario;
        [SerializeField] private Animator animator;
        [SerializeField] private RectTransform rect;

        //---Private Variables
        private bool goomba, goombaHit;
        private float goombaTimer;

        public void OnValidate() {
            this.SetIfNull(ref animator);
            this.SetIfNull(ref rect);
        }

        public void OnEnable() {
            TeleportToBeginning();
        }

        public void Update() {
            if (goombaTimer > 0 && (goombaTimer -= Time.deltaTime) > 0) {
                return;
            }

            rect.localPosition += movementSpeed * Time.deltaTime;

            if (rect.localPosition.x <= marioX) {
                if (goomba) {
                    if (!goombaHit) {
                        mario.Scale--;
                        goombaHit = true;
                        goombaTimer = 0.5f;
                    }
                    if (rect.localPosition.x <= minX)
                        TeleportToBeginning();
                } else {
                    mario.Scale++;
                    TeleportToBeginning();
                }
            }
        }

        private void TeleportToBeginning() {
            goombaHit = false;
            goomba = mario.Scale > 0 && (mario.Scale >= 2 || Random.value < 0.5f);
            animator.SetBool("goomba", goomba);
            rect.localPosition = new Vector2(peachX, rect.localPosition.y);
        }
    }
}
