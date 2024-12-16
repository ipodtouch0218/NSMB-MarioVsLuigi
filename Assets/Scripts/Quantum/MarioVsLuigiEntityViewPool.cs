using Quantum;
using System.Collections.Generic;
using UnityEngine;

public class MarioVsLuigiEntityViewPool : QuantumEntityViewPool {

    //---Serialized Variables
    [SerializeField] private List<GameObject> PoolablePrefabs;

    //---Private Variables
    private HashSet<GameObject> PooledObjects = new(64);

    public override T Create<T>(T prefab, bool activate = true, bool createIfEmpty = true) {
        return Create(prefab, null, activate, createIfEmpty);
    }

    public override GameObject Create(GameObject prefab, bool activate = true, bool createIfEmpty = true) {
        return Create(prefab, null, activate, createIfEmpty);
    }

    public override T Create<T>(T prefab, Transform parent, bool activate = true, bool createIfEmpty = true) {
        GameObject newObject = Create(prefab.gameObject, activate, createIfEmpty);
        return newObject ? newObject.GetComponent<T>() : null;
    }

    public override GameObject Create(GameObject prefab, Transform parent, bool activate = true, bool createIfEmpty = true) {
        if (PoolablePrefabs.Contains(prefab)) {
            GameObject newObject = base.Create(prefab, parent, activate, createIfEmpty);
            PooledObjects.Add(newObject);
            return newObject;
        } else {
            GameObject newObject = GameObject.Instantiate(prefab, parent, false);
            newObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            newObject.SetActive(activate);
            return newObject;
        }
    }

    public override void Destroy(Component component, bool deactivate = true) {
        Destroy(component.gameObject, deactivate);
    }

    public override void Destroy(GameObject instance, bool deactivate = true) {
        if (PooledObjects.Remove(instance)) {
            base.Destroy(instance, deactivate);
        } else {
            GameObject.Destroy(instance);
        }
    }

    public override void Destroy(GameObject instance, float delay) {
        if (PooledObjects.Remove(instance)) {
            base.Destroy(instance, delay);
        } else {
            GameObject.Destroy(instance, delay);
        }
    }

    public override void Prepare(GameObject prefab, int desiredCount) {
        if (PoolablePrefabs.Contains(prefab)) {
            base.Prepare(prefab, desiredCount);
        } else {
            Debug.LogWarning($"Tried to prepare {desiredCount} pooled instance(s) of non-poolable object {prefab.name}");
        }
    }
}
