using UnityEngine;

public class SimplePhysicsMover : MonoBehaviour {

    //---Public Variables
    public Vector2 velocity;
    public float angularVelocity;

    //---Serialized Variables
    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private Vector2 minVelocity = new(float.MinValue, float.MinValue), maxVelocity = new(float.MaxValue, float.MaxValue);

    public void Awake() {
        Update();
    }

    public void Update() {
        velocity += gravityScale * Time.deltaTime * Physics2D.gravity;
        velocity.x = Mathf.Clamp(velocity.x, minVelocity.x, maxVelocity.x);
        velocity.y = Mathf.Clamp(velocity.y, minVelocity.y, maxVelocity.y);
        transform.position += (Vector3) velocity * Time.deltaTime;

        transform.eulerAngles += angularVelocity * Time.deltaTime * Vector3.forward;
    }
}
