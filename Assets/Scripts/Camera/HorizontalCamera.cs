using UnityEngine;
using UnityEngine.SceneManagement;

public class HorizontalCamera : MonoBehaviour {

    //---Static Variables
    private static readonly float BaseSize = 3.5f;

    public static float SizeIncreaseTarget = 0f, SizeIncreaseCurrent = 0f;
    private static float SizeIncreaseVelocity;
    private static double sixteenByNine = 16d / 9d;

    //---Serialized Variables
    [SerializeField] private bool renderToTextureIfAvailable = true;

    //---Private Variables
    private Camera ourCamera;

    public void Start() {
        ourCamera = GetComponent<Camera>();
        AdjustCamera();
    }

    public void Update() {
        SizeIncreaseCurrent = Mathf.SmoothDamp(SizeIncreaseCurrent, SizeIncreaseTarget, ref SizeIncreaseVelocity, 1f);
        AdjustCamera();
        ourCamera.targetTexture = renderToTextureIfAvailable && Settings.Instance.graphicsNdsEnabled && SceneManager.GetActiveScene().buildIndex != 0
            ? GlobalController.Instance.ndsTexture
            : null;
    }

    private void AdjustCamera() {
        double aspectReciprocals = 1d / ourCamera.aspect;
        double size = BaseSize + SizeIncreaseCurrent;

        // https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        ourCamera.orthographicSize = Mathf.Min((float) size, (float) (size * sixteenByNine * aspectReciprocals));
    }
}
