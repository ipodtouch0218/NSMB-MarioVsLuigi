using UnityEngine;

public class PipeBreakParticle : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private SimplePhysicsMover mover;
    [SerializeField] private float mulitplier = 0.5f;

    //---Private Variables
    private Vector3 position;
    private Quaternion rotation;

    public void Start() {
        position = transform.position;
        rotation = transform.rotation;
    }

    public void Update() {
        position.x += mover.velocity.x * Time.deltaTime * mulitplier;
        transform.SetPositionAndRotation(position, rotation);
    }
}
