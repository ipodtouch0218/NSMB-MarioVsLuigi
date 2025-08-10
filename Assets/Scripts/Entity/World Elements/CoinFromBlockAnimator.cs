using UnityEngine;

public class CoinFromBlockAnimator : MonoBehaviour
{
    public GameObject CoinInQuestion;
    public bool Downward;
    private float Incr = 0;
    // Update is called once per frame
    void Update()
    {
        CoinInQuestion.transform.localPosition = new Vector3(CoinInQuestion.transform.localPosition.x, CoinInQuestion.transform.localPosition.y + (((Mathf.Cos(Mathf.PI * Incr * 1.5f)) / 40f) * (Downward ? -1 : 1)),0);
        Incr += Time.deltaTime;
        if(Incr >= (4f / 5f)) {
            CoinInQuestion.transform.localScale = Vector3.zero;
        }
    }
}
