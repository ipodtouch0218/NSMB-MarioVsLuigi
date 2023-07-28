using Fusion;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;

public class MvLSceneManager : NetworkSceneManagerDefault
{
    public static event Action OnSceneLoadStart;

    protected override YieldInstruction LoadSceneAsync(SceneRef sceneRef, LoadSceneParameters parameters, Action<Scene> loaded) {
        OnSceneLoadStart?.Invoke();
        return base.LoadSceneAsync(sceneRef, parameters, loaded);
    }
}
