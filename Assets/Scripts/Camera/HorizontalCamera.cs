using UnityEngine;
using UnityEngine.SceneManagement;

public class HorizontalCamera : MonoBehaviour {

    //---Static Variables

    public static float SizeIncreaseTarget = 0f, SizeIncreaseCurrent = 0f;
    private static float SizeIncreaseVelocity;

    //---Serialized Variables
    [SerializeField] private bool renderToTextureIfAvailable = true;
    [SerializeField] private float verticalSizeInTiles = 14f;

    //---Private Variables
    private Camera ourCamera;

    public void OnValidate() {
        if (GetComponentInParent<HorizontalCamera>() is HorizontalCamera parent && parent) {
            verticalSizeInTiles = parent.verticalSizeInTiles;
        }
    }

    public void Start() {
        ourCamera = GetComponent<Camera>();
        AdjustCamera();

        SizeIncreaseTarget = 0;
        SizeIncreaseCurrent = 0;
        SizeIncreaseVelocity = 0;
    }

    public void Update() {
        SizeIncreaseCurrent = Mathf.SmoothDamp(SizeIncreaseCurrent, SizeIncreaseTarget, ref SizeIncreaseVelocity, 1f);
        AdjustCamera();
        ourCamera.targetTexture = renderToTextureIfAvailable && Settings.Instance.graphicsNdsEnabled && SceneManager.GetActiveScene().buildIndex != 0
            ? GlobalController.Instance.ndsTexture
            : null;
    }

    private void AdjustCamera() {

        float aspect = ourCamera.aspect;
        double size = (verticalSizeInTiles / 4f) + SizeIncreaseCurrent;

        // https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double aspectReciprocals = 1d / aspect;
        ourCamera.orthographicSize = Mathf.Min((float) size, (float) (size * (16d/9d) * aspectReciprocals));
    }
}
