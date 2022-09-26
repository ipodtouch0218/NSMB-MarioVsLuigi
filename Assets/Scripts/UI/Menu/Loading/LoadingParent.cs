using UnityEngine;

using Fusion;

public class LoadingParent : MonoBehaviour {

    [SerializeField] private GameObject canvas;

    public void OnEnable() {
        NetworkHandler.OnSceneLoadStart += OnSceneLoad;
    }

    public void OnDisable() {
        NetworkHandler.OnSceneLoadStart -= OnSceneLoad;
    }

    public void Start() {
        DontDestroyOnLoad(this);
    }

    public void OnSceneLoad(NetworkRunner runner) {
        canvas.SetActive(true);
    }
}