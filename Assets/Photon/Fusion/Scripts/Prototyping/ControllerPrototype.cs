
using UnityEngine;
using Fusion;

[ScriptHelp(BackColor = EditorHeaderBackColor.Steel)]
public class ControllerPrototype : Fusion.NetworkBehaviour {
  protected NetworkCharacterControllerPrototype _ncc;
  protected NetworkRigidbody _nrb;
  protected NetworkRigidbody2D _nrb2d;
  protected NetworkTransform _nt;

  [Networked]
  public Vector3 MovementDirection { get; set; }

  public bool TransformLocal = false;

  [DrawIf(nameof(ShowSpeed), Hide = true)]
  public float Speed = 6f;

  bool ShowSpeed => this && !TryGetComponent<NetworkCharacterControllerPrototype>(out _);

  public void Awake() {
    CacheComponents();
  }

  public override void Spawned() {
    CacheComponents();
  }

  private void CacheComponents() {
    if (!_ncc) _ncc     = GetComponent<NetworkCharacterControllerPrototype>();
    if (!_nrb) _nrb     = GetComponent<NetworkRigidbody>();
    if (!_nrb2d) _nrb2d = GetComponent<NetworkRigidbody2D>();
    if (!_nt) _nt       = GetComponent<NetworkTransform>();
  }
  
  public override void FixedUpdateNetwork() {
    if (Runner.Config.PhysicsEngine == NetworkProjectConfig.PhysicsEngines.None) {
      return;
    }

    Vector3 direction;
    if (GetInput(out NetworkInputPrototype input)) {
      direction = default;

      if (input.IsDown(NetworkInputPrototype.BUTTON_FORWARD)) {
        direction += TransformLocal ? transform.forward : Vector3.forward;
      }

      if (input.IsDown(NetworkInputPrototype.BUTTON_BACKWARD)) {
        direction -= TransformLocal ? transform.forward : Vector3.forward;
      }

      if (input.IsDown(NetworkInputPrototype.BUTTON_LEFT)) {
        direction -= TransformLocal ? transform.right : Vector3.right;
      }

      if (input.IsDown(NetworkInputPrototype.BUTTON_RIGHT)) {
        direction += TransformLocal ? transform.right : Vector3.right;
      }

      direction = direction.normalized;

      MovementDirection = direction;

      if (input.IsDown(NetworkInputPrototype.BUTTON_JUMP)) {
        if (_ncc) {
          _ncc.Jump();
        } else {
          direction += (TransformLocal ? transform.up : Vector3.up);
        }
      }
    } else {
      direction = MovementDirection;
    }

    if (_ncc) {
      _ncc.Move(direction);
    } else if (_nrb && !_nrb.Rigidbody.isKinematic) {
      _nrb.Rigidbody.AddForce(direction * Speed);
    } else if (_nrb2d && !_nrb2d.Rigidbody.isKinematic) {
      Vector2 direction2d = new Vector2(direction.x, direction.y + direction.z);
      _nrb2d.Rigidbody.AddForce(direction2d * Speed);
    } else {
      transform.position += (direction * Speed * Runner.DeltaTime);
    }
  }
}