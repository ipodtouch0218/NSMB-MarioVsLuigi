using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject others;
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private AudioSource sfx;

    public void Start() {
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence() {
        yield return new WaitForSeconds(0.75f);
        sfx.Play();
        yield return FadeImageToValue(fullscreenImage, 0, 0.33f);
        yield return new WaitForSeconds(0.5f);

        AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
        sceneLoad.allowSceneActivation = false;

        yield return new WaitForSeconds(0.75f);
        fullscreenImage.color = new Color(0,0,0,0);
        yield return FadeImageToValue(fullscreenImage, 1, 0.33f);
        yield return new WaitForSeconds(0.75f);

        while (sceneLoad.progress < 0.9f) {
            yield return null;
        }
        sceneLoad.allowSceneActivation = true;
        while (!sceneLoad.isDone) {
            yield return null;
        }

        others.SetActive(false);

        // Fuck this lag spike man
        yield return new WaitForSeconds(0.1f);
        do {
            yield return null;
        } while (Time.deltaTime >= Time.maximumDeltaTime);
        
        yield return FadeImageToValue(fullscreenImage, 0, 0.33f);
        yield return new WaitForSeconds(0.5f);
        SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());
        //SceneManager.SetActiveScene(SceneManager.GetSceneByName("MainMenu"));
    }

    private static IEnumerator FadeImageToValue(Image image, float newAlpha, float time) {
        float remainingTime = time;
        float startingAlpha = image.color.a;

        Color newColor = image.color;
        while ((remainingTime -= Time.deltaTime) > 0) {
            newColor.a = Mathf.Lerp(startingAlpha, newAlpha, 1f - (remainingTime / time));
            image.color = newColor;
            yield return null;
        }

        newColor.a = newAlpha;
        image.color = newColor;
    }
}