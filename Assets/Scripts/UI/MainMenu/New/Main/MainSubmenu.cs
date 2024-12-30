using System.Collections;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus {
    public class MainSubmenu : MainMenuSubmenu {

        //---Serialized Variables
        [SerializeField] private GameObject exitingBlocker;

        //---Private Variables
        private bool wasOptionsOpen;
        private Coroutine quitCoroutine;

        public void Update() {
            wasOptionsOpen = GlobalController.Instance.optionsManager.IsActive();
        }

        public void OpenOptions() {
            if (!wasOptionsOpen) {
                GlobalController.Instance.optionsManager.OpenMenu();
                Canvas.PlayConfirmSound();
            }
        }

        public void QuitGame() {
            if (quitCoroutine == null) {
                quitCoroutine = Canvas.StartCoroutine(QuitCorotuine());

                // Force focus on a blocker element so the user can't do anything else.
                exitingBlocker.SetActive(true);
                Canvas.EventSystem.SetSelectedGameObject(exitingBlocker);
            }
        }

        private IEnumerator QuitCorotuine() {
            AudioClip clip = SoundEffect.UI_Quit.GetClip();
            Canvas.PlaySound(SoundEffect.UI_Quit);
            yield return new WaitForSeconds(clip.length);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

