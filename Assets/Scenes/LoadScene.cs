using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour {

    public string levelname;

    public void Scene()
    {
        SceneManager.LoadScene(levelname);
    }

    public void Exit()
    {
        Application.Quit();
    }
}