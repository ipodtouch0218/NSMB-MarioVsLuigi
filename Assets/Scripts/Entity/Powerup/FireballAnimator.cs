using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class FireballAnimator : MonoBehaviour {

    //--Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private GameObject destroyParticlePrefab;

    public void OnValidate() {
        this.SetIfNull(ref entity);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventProjectileDestroyed>(this, OnProjectileDestroyed);
    }

    private void OnProjectileDestroyed(EventProjectileDestroyed e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        if (e.PlayEffect) {
            Instantiate(destroyParticlePrefab, transform.position, Quaternion.identity);
        }
    }
}