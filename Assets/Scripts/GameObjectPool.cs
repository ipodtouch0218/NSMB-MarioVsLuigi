using UnityEngine;

public class GameObjectPool {

    public GameObject parent;
    private readonly GameObject[] pool;
    private readonly float[] allocations;
    public int size;

    public GameObjectPool(GameObject prefab, int size) {
        this.size = size;
        parent = new(prefab.name + " Pool");
        pool = new GameObject[size];
        allocations = new float[size];
        for (int i = 0; i < size; i++) {
            pool[i] = Object.Instantiate(prefab, parent.transform);
            pool[i].name = prefab.name + " (" + (i + 1) + ")";
        }
    }

    public GameObject Pop() {
        int oldestIndex = 0;
        GameObject oldestObj = null;
        float oldestObjTime = Time.time;
        for (int i = 0; i < size; i++) {
            GameObject obj = pool[i];
            if (!obj.activeInHierarchy) {
                allocations[i] = Time.time;
                return obj;
            }
            if (oldestObj == null || allocations[i] < oldestObjTime) {
                oldestObj = obj;
                oldestObjTime = allocations[i];
                oldestIndex = i;
            }
        }
        allocations[oldestIndex] = Time.time;
        oldestObj.SetActive(false);
        return oldestObj;
    }
}