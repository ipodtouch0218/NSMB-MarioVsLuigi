using UnityEngine;
using UnityEngine.SceneManagement;

using NSMB.Game;
using NSMB.Extensions;

public class VisualizedBlocks : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject[] blocks;
    [SerializeField] private AudioSource music;

    [Header("Settings")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] [Delayed] private int count = 21, sampleCount = 1024;
    [SerializeField] private float minHeight = 0.5f, maxHeight = 2f, width = 0.3f, spacing = 0.1f, decayRate = 50f;
    [SerializeField] private FFTWindow window;
    [SerializeField] private float maxHertz = 20050f;

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
        for (int i = 0; i < count; i++) {

            int index = CalculateIndex(i, count);
            int lowerBound = index + (index - ((i > 0) ? CalculateIndex(i - 1, count) : 0)) / 2;
            int upperBound = index + (((i < count - 1) ? CalculateIndex(i + 1, count) : sampleCount) - index) / 2;

            float currentHeight = blocks[i].transform.localScale.y;
            float newHeight = 0;
            if (upperBound - lowerBound <= 1) {
                newHeight = samples[lowerBound];
            } else {
                for (int j = lowerBound + 1; j < upperBound; j++) {
                    newHeight = Mathf.Max(newHeight, samples[j]);
                }
            }

            //float newHeight;
            //if (index != prev)
            //    newHeight = samples[index];
            //else
            //    newHeight = samples[prev] + samples[index] * 0.5f;

            newHeight *= maxHeight;
            newHeight *= Mathf.Log10(lowerBound + 1) * 0.75f + 0.15f;
            //newHeight += newHeight * (Mathf.Clamp01(Mathf.Log10((float) index / samples.Length) + 1)) * bonusHeight;
            newHeight += minHeight;

            if (newHeight > currentHeight)
                blocks[i].transform.localScale = new(width, newHeight);
            else
                blocks[i].transform.localScale = new(width, Mathf.Lerp(currentHeight, newHeight, decayRate * Time.deltaTime));

            blocks[i].transform.localPosition = new((width + spacing) * (i-(count/2)), 0);

        }
    }

    private int CalculateIndex(int i , int count) {
        float mel = ((float) (i + 1) / (count + 1)) * 1713;
        float hz = 700 * (Mathf.Pow(10, mel / 2595f) - 1);
        return (int) (hz / maxHertz * sampleCount);
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
