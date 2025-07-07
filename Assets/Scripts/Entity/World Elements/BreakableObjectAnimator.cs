using NSMB.Particles;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class BreakableObjectAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] protected SpriteRenderer sRenderer;
        [SerializeField] private Sprite unbrokenSprite, brokenSprite;
        [SerializeField] private SimplePhysicsMover breakPrefab;

        //---Private Variables
        private SimplePhysicsMover activeParticle;
        private EventKey currentEventKey;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer);
        }

        public virtual void Start() {
            QuantumCallback.Subscribe<CallbackEventCanceled>(this, OnEventCanceled);
            QuantumEvent.Subscribe<EventBreakableObjectBroken>(this, OnBreakableObjectBroken, FilterOutReplayFastForward);
        }

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)
                || f.Global->GameState < GameState.Playing) {
                return;
            }

            var breakable = f.Unsafe.GetPointer<BreakableObject>(EntityRef);
            sRenderer.size = new Vector2(sRenderer.size.x, breakable->CurrentHeight.AsFloat);
            sRenderer.sprite = breakable->IsBroken ? brokenSprite : unbrokenSprite;
        }

        protected virtual void OnBreakableObjectBroken(EventBreakableObjectBroken e) {
            if (e.Entity != EntityRef) {
                return;
            }

            var pipe = VerifiedFrame.Unsafe.GetPointer<BreakableObject>(e.Entity);

            SimplePhysicsMover particle = Instantiate(breakPrefab, transform.position, transform.rotation);
            //particle.transform.localScale = transform.localScale;
            particle.transform.position += particle.transform.up * ((pipe->MinimumHeight + (e.Height / 2)) / 2).AsFloat;
            SpriteRenderer sRenderer = particle.GetComponentInChildren<SpriteRenderer>();
            sRenderer.size = new Vector2(sRenderer.size.x, e.Height.AsFloat);
            sRenderer.transform.localPosition = new Vector2(0, -(e.Height / 2).AsFloat);

            particle.velocity = (e.LaunchDirection.ToUnityVector2() * 9.5f) + (Vector2.up * 3.5f);

            Vector2 a = particle.velocity;
            Vector2 b = transform.up;
            float angularVelocity = (a.x * b.y) - (b.x * a.y);
            particle.angularVelocity = angularVelocity * -(400f/5);

            activeParticle = particle;
            currentEventKey = e;
            Destroy(particle.gameObject, 3f);
        }

        private void OnEventCanceled(CallbackEventCanceled e) {
            if (!e.EventKey.Equals(currentEventKey)) {
                return;
            }

            if (activeParticle && activeParticle.gameObject) {
                Destroy(activeParticle.gameObject);
            }
        }

#if UNITY_EDITOR
        public void OnDrawGizmos() {
            Gizmos.color = Color.red;
            var breakable = GetComponent<QPrototypeBreakableObject>().Prototype;
            var extents = GetComponent<QuantumEntityPrototype>().PhysicsCollider.Shape2D.BoxExtents;
            Gizmos.DrawLine(
                transform.position + (transform.rotation * new Vector3(-extents.X.AsFloat, breakable.MinimumHeight.AsFloat * 0.5f)),
                transform.position + (transform.rotation * new Vector3(extents.X.AsFloat, breakable.MinimumHeight.AsFloat * 0.5f))
            );
        }
#endif
    }
}
