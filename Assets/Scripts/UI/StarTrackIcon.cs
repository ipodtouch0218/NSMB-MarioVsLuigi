using UnityEngine;

public class StarTrackIcon : TrackIcon {

    //---Static Variables
    private static readonly Vector3 ThreeFourths = new(0.75f, 0.75f, 1f);

    //---Serialized Variables
    [SerializeField] private Sprite starSprite;

    //---Private Variables
    private StarBouncer starTarget;

    public void Start() {
        starTarget = target.GetComponent<StarBouncer>();
        if (!starTarget) {
            Destroy(gameObject);
            return;
        }

        if (starTarget.IsStationary) {
            GetComponent<Animator>().enabled = true;
            transform.localScale = Vector3.zero;
        } else {
            transform.localScale = ThreeFourths;
        }

        image.sprite = starSprite;
    }
}
