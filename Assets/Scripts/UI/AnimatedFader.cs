using NSMB.Utilities.Extensions;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI {
    public class AnimatedFader : MonoBehaviour {

        //---Static Variables
        private static readonly int ParamDirection = Animator.StringToHash("direction");
        private static readonly int ParamCircle = Animator.StringToHash("circle");
        private static readonly int ParamDissolve = Animator.StringToHash("dissolve");
        private static readonly int ParamRespawn = Animator.StringToHash("respawn");
        private static readonly int ParamLeftSideBumps = Animator.StringToHash("left_side_bumps");
        private static readonly int ParamRightSideBumps = Animator.StringToHash("right_side_bumps");

        //---Serialized Variables
        [SerializeField] private Sprite defaultRespawnStyleSilhouette;
        [SerializeField] private Animator anim;
        [SerializeField] private Image shapeImage;
        [SerializeField] private Canvas canvas;

        public bool FadeBehindUi {
            set => canvas.sortingOrder = value ? 1 : 31000;
        }

        //---Private Variables
        private FadeStyle currentInStyle, currentOutStyle;
        private Coroutine currentCoroutine;

        public void Fade(FadeStyle inStyle, FadeStyle outStyle = FadeStyle.Cut, Action onInComplete = null) {
            this.StopCoroutineNullable(ref currentCoroutine);
            
            currentInStyle = inStyle;
            currentOutStyle = outStyle;

            anim.SetBool(ParamDirection, true);
            // used a switch instead of mapping enum to anim name directly in case there are fade styles that have additional logic
            switch (inStyle) {
            case FadeStyle.Circle:
                anim.SetTrigger(ParamCircle);
                break;
            case FadeStyle.Dissolve:
                anim.SetTrigger(ParamDissolve);
                break;
            case FadeStyle.Respawn:
                anim.SetTrigger(ParamRespawn);
                break;
            case FadeStyle.LeftSideBumps:
                anim.SetTrigger(ParamLeftSideBumps);
                break;
            case FadeStyle.RightSideBumps:
                anim.SetTrigger(ParamRightSideBumps);
                break;
            case FadeStyle.Cut:
                break;
            }
            
            currentCoroutine = StartCoroutine(WaitForAnimation(onInComplete));
        }

        private IEnumerator WaitForAnimation(Action onComplete) {
            yield return null;
            yield return new WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).normalizedTime > 1 && !anim.IsInTransition(0));
            yield return new WaitForSeconds(0.4f);
            try {
                onComplete?.Invoke();
            } catch (Exception ex) {
                Debug.LogError($"Fader onInComplete exception: {ex}");
            }
            yield return new WaitForSeconds(0.1f);
            FadeOut();
        }

        private void FadeOut() {
            anim.SetBool(ParamDirection, false);
            // ditto.
            switch (currentOutStyle) {
            case FadeStyle.Circle:
                anim.SetTrigger(ParamCircle);
                break;
            case FadeStyle.Dissolve:
                anim.SetTrigger(ParamDissolve);
                break;
            case FadeStyle.Respawn:
                anim.SetTrigger(ParamRespawn);
                break;
            case FadeStyle.LeftSideBumps:
                anim.SetTrigger(ParamLeftSideBumps);
                break;
            case FadeStyle.RightSideBumps:
                anim.SetTrigger(ParamRightSideBumps);
                break;
            case FadeStyle.Cut:
                break;
            }
        }

        public void SetRespawnStyleSilhouetteSprite(Sprite silhouette) {
            shapeImage.sprite = silhouette ? silhouette : defaultRespawnStyleSilhouette;
        }

        public enum FadeStyle {
            Cut,
            Dissolve,
            Circle,
            Respawn, // bowser shape on IN, star shape on OUT,
            LeftSideBumps,
            RightSideBumps
        }
    }
}