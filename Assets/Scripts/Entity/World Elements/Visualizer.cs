using UnityEngine;
using UnityEngine.SceneManagement;

using NSMB.Extensions;

public class VisualizedBlocks : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject[] blocks;
    [SerializeField] private AudioSource music;

    [Header("Settings")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] [Delayed] private int count = 21, sampleCount = 1024, sampleWidth = 10;
    [SerializeField] private float minHeight = 0.5f, maxHeight = 2f, width = 0.3f, spacing = 0.1f, decayRate = 50f;
    [SerializeField] private FFTWindow window;

    //---Private Variables
    private float[] samples;

    public void Start() {
        OnValidate();
    }

    public void OnValidate() {
        if (gameObject.scene.name != SceneManager.GetActiveScene().name)
            return;

        if ((samples?.Length ?? 0) != sampleCount)
            samples = new float[sampleCount];

        if ((blocks?.Length ?? 0) != count) {
            for (int i = 0; i < transform.childCount; i++) {
                Debug.Log("A");
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            blocks = new GameObject[count];

            for (int i = 0; i < count; i++) {
                blocks[i] = Instantiate(blockPrefab, transform);
                blocks[i].transform.localPosition = new((width + spacing) * (i-(count/2)), 0);
            }
        }
        music = GameManager.Instance.music;
    }

    public void Update() {
        music.GetSpectrumData(samples, 0, window);
        int prev = 0;
        for (int i = 0; i < count; i++) {

            float mel = ((float) (i + 1) / (count + 1)) * 1713;
            float hz = 700 * (Mathf.Pow(10, mel / 2595f) - 1);
            int index = (int) (hz / 20050f * sampleCount);

            float currentHeight = blocks[i].transform.localScale.y;

            float newHeight;
            if (index != prev)
                newHeight = samples[index];
            else
                newHeight = samples[prev] + samples[index] * 0.5f;

            newHeight *= maxHeight;
            newHeight *= Mathf.Log10(index + 1) * 0.75f + 0.15f;
            newHeight += minHeight;

            if (newHeight > currentHeight)
                blocks[i].transform.localScale = new(width, newHeight);
            else
                blocks[i].transform.localScale = new(width, Mathf.Lerp(currentHeight, newHeight, decayRate * Time.deltaTime));

            blocks[i].transform.localPosition = new((width + spacing) * (i-(count/2)), 0);

            prev = index;
        }
    }

#if UNITY_EDITOR
    public void OnDrawGizmos() {
        Vector3 size = new(width, maxHeight, 1);
        for (int i = 0; i < count; i++) {
            Gizmos.DrawCube(transform.position + new Vector3((width + spacing) * (i-(count/2)), 0) + (size.Multiply(Vector3.up) * 0.5f), size);
        }
    }
#endif
}
