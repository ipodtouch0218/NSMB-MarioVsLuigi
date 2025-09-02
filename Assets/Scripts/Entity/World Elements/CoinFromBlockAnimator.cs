using UnityEngine;

public class CoinFromBlockAnimator : MonoBehaviour
{
    public GameObject CoinInQuestion;
    public bool Downward;
    private float Incr = 0;
    public Vector2 BeginningPosition;
    // Update is called once per frame
    void Start() {
        BeginningPosition = new Vector2(transform.localPosition.x, transform.localPosition.y);
    }
    void Update()
    {
        CoinInQuestion.transform.localPosition = new Vector3(BeginningPosition.x, (Downward ? -1 : 0) + BeginningPosition.y + (((Mathf.Sin(Mathf.PI * Incr * 1.5f)) / 1f) * (Downward ? -1 : 1)),0);
        Incr += Time.deltaTime;
        if(Incr >= (3f / 5f)) {
            CoinInQuestion.transform.localScale = Vector3.zero;
        }
    }
}
