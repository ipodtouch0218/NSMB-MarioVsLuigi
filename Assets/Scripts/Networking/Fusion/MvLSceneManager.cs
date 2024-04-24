using System;
using System.Collections;

using Fusion;

public class MvLSceneManager : NetworkSceneManagerDefault {

    public static event Action OnSceneLoadStart;

    protected override IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkLoadSceneParameters sceneParams) {
        OnSceneLoadStart?.Invoke();
        return base.LoadSceneCoroutine(sceneRef, sceneParams);
    }
}
