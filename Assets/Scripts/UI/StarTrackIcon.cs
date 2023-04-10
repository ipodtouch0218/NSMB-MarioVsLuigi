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

        //Calculate the Tracking Position on Spawn
        CalculatePosition();
    }

    public override void Update() {
        //If the starTarget is moving, run the position update. Otherwise, don't Calculate the Track Position since the star is not going to move.
        if (!starTarget.IsStationary)
        {
            base.Update();
        }
    }
}
