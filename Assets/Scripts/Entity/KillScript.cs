using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillScript : MonoBehaviour {
    public string spawnAfter;
    public void Kill() {
        GameObject.Destroy(gameObject);
        if (transform.parent != null)
            GameObject.Destroy(transform.parent.gameObject);

        if (spawnAfter.Length > 0) {
            GameObject.Instantiate(Resources.Load("Prefabs/" + spawnAfter), transform.position, Quaternion.identity);
        }
    }
}
