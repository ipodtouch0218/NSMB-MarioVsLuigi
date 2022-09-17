using UnityEngine;

using Fusion;

public class BasicEntity : NetworkBehaviour {

    //---Networked Variables
    [Networked(OnChanged = "")] public NetworkBool FacingRight { get; set; } = true;

    //---Components
    public Rigidbody2D body;

    public virtual void Awake() {
        body = GetComponent<Rigidbody2D>();
    }
}