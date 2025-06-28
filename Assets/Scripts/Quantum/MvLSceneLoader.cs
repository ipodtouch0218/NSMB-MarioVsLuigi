using Quantum;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NSMB.Quantum {
    public class MvLSceneLoader : MonoBehaviour {

        //---Static
        public static MvLSceneLoader Instance;

        //---Properties
        public Map CurrentLoadedMap => currentMap;

        //---Private Variables
        private Map currentMap;
        private Coroutine loadingCoroutine;

        public void Start() {
            Instance = this;
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
        }

        public void OnUpdateView(CallbackUpdateView e) {
            if (loadingCoroutine != null) {
                return;
            }

            QuantumGame game = e.Game;
            Frame f = game.Frames.Predicted;

            if (f.Map != currentMap) {
                Map newMap = f.Map;
                Map oldMap = currentMap;

                loadingCoroutine = StartCoroutine(SceneChangeCoroutine(game, oldMap, newMap));
            }
        }

        public void OnGameDestroyed(CallbackGameDestroyed e) {
            if (loadingCoroutine != null) {
                StopCoroutine(loadingCoroutine);
            }
            loadingCoroutine = StartCoroutine(SceneChangeCoroutine(e.Game, currentMap, null));
        }

        private IEnumerator SceneChangeCoroutine(QuantumGame game, Map oldMap, Map newMap) {
            if (oldMap == newMap) {
                loadingCoroutine = null;
                yield break;
            }

            // Load new map
            AsyncOperation loadingOp;
            QuantumCallback.Dispatcher.Publish(new CallbackUnitySceneLoadBegin(game));
            if (newMap == null) {
                if (SceneManager.GetSceneByBuildIndex(1).isLoaded) {
                    goto alreadyLoaded;
                }
                loadingOp = SceneManager.LoadSceneAsync(1, LoadSceneMode.Additive);
            } else {
                if (SceneManager.GetSceneByName(newMap.Scene).isLoaded) {
                    goto alreadyLoaded;
                }
                loadingOp = SceneManager.LoadSceneAsync(newMap.Scene, LoadSceneMode.Additive);
            }

            loadingOp.allowSceneActivation = true;
            while (!loadingOp.isDone) {
                yield return null;
            }

        alreadyLoaded:
            currentMap = newMap;
            if (newMap == null) {
                SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(1));
            } else {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(newMap.Scene));
            }
            QuantumCallback.Dispatcher.Publish(new CallbackUnitySceneLoadDone(game));

            // Unload old map
            if (oldMap != null) {
                QuantumCallback.Dispatcher.Publish(new CallbackUnitySceneUnloadBegin(game));
                loadingOp = SceneManager.UnloadSceneAsync(oldMap.Scene);
#if UNITY_EDITOR
                if (loadingOp == null) {
                    // Prevent a unity-editor error.
                    yield break;
                }
#endif
                while (!loadingOp.isDone) {
                    yield return null;
                }
                QuantumCallback.Dispatcher.Publish(new CallbackUnitySceneUnloadDone(game));
            }

            loadingCoroutine = null;
        }
    }
}
