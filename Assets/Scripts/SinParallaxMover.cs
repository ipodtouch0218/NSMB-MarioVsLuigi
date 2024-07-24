using UnityEngine;

public class SinParallaxMover : ParallaxMover {

    //---Serialized Variables
    [SerializeField] private float frequency = 0.2f;

    public override void Update() {
        transform.position += moveBy * (Mathf.Sin(Time.time * frequency * Mathf.PI) * Time.deltaTime);
    }
}
