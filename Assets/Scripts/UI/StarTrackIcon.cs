using UnityEngine;

using NSMB.Entities.Collectable;

public class StarTrackIcon : TrackIcon {

    //---Static Variables
    private static readonly Vector3 ThreeFourths = new(0.75f, 0.75f, 1f);

    //---Serialized Variables
    [SerializeField] private Sprite starSprite;

    //---Private Variables
    private BigStar starTarget;

    public void Start() {
        starTarget = target.GetComponent<BigStar>();
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
        target = starTarget.sRenderer.gameObject;
    }
}
