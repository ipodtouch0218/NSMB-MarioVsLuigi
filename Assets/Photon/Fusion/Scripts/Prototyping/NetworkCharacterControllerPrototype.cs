using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[OrderBefore(typeof(NetworkTransform))]
[DisallowMultipleComponent]
// ReSharper disable once CheckNamespace
public class NetworkCharacterControllerPrototype : NetworkTransform {
  [Header("Character Controller Settings")]
  public float gravity       = -20.0f;
  public float jumpImpulse   = 8.0f;
  public float acceleration  = 10.0f;
  public float braking       = 10.0f;
  public float maxSpeed      = 2.0f;
  public float rotationSpeed = 15.0f;

  [Networked]
  [HideInInspector]
  public bool IsGrounded { get; set; }

  [Networked]
  [HideInInspector]
  public Vector3 Velocity { get; set; }

  /// <summary>
  /// Sets the default teleport interpolation velocity to be the CC's current velocity.
  /// For more details on how this field is used, see <see cref="NetworkTransform.TeleportToPosition"/>.
  /// </summary>
  protected override Vector3 DefaultTeleportInterpolationVelocity => Velocity;

  /// <summary>
  /// Sets the default teleport interpolation angular velocity to be the CC's rotation speed on the Z axis.
  /// For more details on how this field is used, see <see cref="NetworkTransform.TeleportToRotation"/>.
  /// </summary>
  protected override Vector3 DefaultTeleportInterpolationAngularVelocity => new Vector3(0f, 0f, rotationSpeed);

  public CharacterController Controller { get; private set; }

  protected override void Awake() {
    base.Awake();
    CacheController();
  }

  public override void Spawned() {
    base.Spawned();
    CacheController();

    // Caveat: this is needed to initialize the Controller's state and avoid unwanted spikes in its perceived velocity
    Controller.Move(transform.position);
  }

  private void CacheController() {
    if (Controller == null) {
      Controller = GetComponent<CharacterController>();

      Assert.Check(Controller != null, $"An object with {nameof(NetworkCharacterControllerPrototype)} must also have a {nameof(CharacterController)} component.");
    }
  }

  protected override void CopyFromBufferToEngine() {
    // Trick: CC must be disabled before resetting the transform state
    Controller.enabled = false;

    // Pull base (NetworkTransform) state from networked data buffer
    base.CopyFromBufferToEngine();

    // Re-enable CC
    Controller.enabled = true;
  }

  /// <summary>
  /// Basic implementation of a jump impulse (immediately integrates a vertical component to Velocity).
  /// <param name="ignoreGrounded">Jump even if not in a grounded state.</param>
  /// <param name="overrideImpulse">Optional field to override the jump impulse. If null, <see cref="jumpImpulse"/> is used.</param>
  /// </summary>
  public virtual void Jump(bool ignoreGrounded = false, float? overrideImpulse = null) {
    if (IsGrounded || ignoreGrounded) {
      var newVel = Velocity;
      newVel.y += overrideImpulse ?? jumpImpulse;
      Velocity =  newVel;
    }
  }

  /// <summary>
  /// Basic implementation of a character controller's movement function based on an intended direction.
  /// <param name="direction">Intended movement direction, subject to movement query, acceleration and max speed values.</param>
  /// </summary>
  public virtual void Move(Vector3 direction) {
    var deltaTime    = Runner.DeltaTime;
    var previousPos  = transform.position;
    var moveVelocity = Velocity;

    direction = direction.normalized;

    if (IsGrounded && moveVelocity.y < 0) {
      moveVelocity.y = 0f;
    }

    moveVelocity.y += gravity * Runner.DeltaTime;

    var horizontalVel = default(Vector3);
    horizontalVel.x = moveVelocity.x;
    horizontalVel.z = moveVelocity.z;

    if (direction == default) {
      horizontalVel = Vector3.Lerp(horizontalVel, default, braking * deltaTime);
    } else {
      horizontalVel      = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, maxSpeed);
      transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Runner.DeltaTime);
    }

    moveVelocity.x = horizontalVel.x;
    moveVelocity.z = horizontalVel.z;

    Controller.Move(moveVelocity * deltaTime);

    Velocity   = (transform.position - previousPos) * Runner.Simulation.Config.TickRate;
    IsGrounded = Controller.isGrounded;
  }
}