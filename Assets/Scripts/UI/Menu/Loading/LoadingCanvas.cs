using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;

namespace NSMB.Loading {
    public class LoadingCanvas : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private AudioSource audioSource;

        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup loadingGroup, readyGroup;
        [SerializeField] private Image readyBackground;

        //---Private Variables
        private bool loading;

        public void Initialize() {
            if (loading)
                return;

            loading = true;

            readyGroup.gameObject.SetActive(false);
            gameObject.SetActive(true);
            loadingGroup.alpha = 1;
            readyGroup.alpha = 0;
            readyBackground.color = Color.clear;

            audioSource.Play();
            audioListener.enabled = true;
        }

        public void EndLoading() {
            bool spectator = NetworkHandler.Runner.GetLocalPlayerData().IsCurrentlySpectating;
            readyGroup.gameObject.SetActive(true);
            animator.SetTrigger(spectator ? "spectating" : "loaded");

            audioSource.Stop();
            audioListener.enabled = false;
        }

        public void EndAnimation() {
            animator.Play("waiting");
            gameObject.SetActive(false);
            loading = false;
        }
    }
}
