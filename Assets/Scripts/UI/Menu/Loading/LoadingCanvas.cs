using UnityEngine;
using UnityEngine.UI;

using NSMB.Extensions;
using System.Collections;

namespace NSMB.Loading {
    public class LoadingCanvas : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private AudioSource audioSource;

        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup loadingGroup, readyGroup;
        [SerializeField] private Image readyBackground;

        //---Private Variables
        private bool initialized;

        public void Initialize() {
            if (initialized)
                return;

            initialized = true;

            readyGroup.gameObject.SetActive(false);
            gameObject.SetActive(true);

            loadingGroup.alpha = 1;
            readyGroup.alpha = 0;
            readyBackground.color = Color.clear;

            animator.Play("waiting");
            audioSource.Play();
            audioListener.enabled = true;
        }

        public void EndLoading() {
            bool spectator = NetworkHandler.Runner.GetLocalPlayerData().IsCurrentlySpectating;
            readyGroup.gameObject.SetActive(true);
            animator.SetTrigger(spectator ? "spectating" : "loaded");

            initialized = false;

            audioSource.Stop();
            audioListener.enabled = false;
        }

        public void EndAnimation() {
            gameObject.SetActive(false);
            initialized = false;
        }
    }
}
