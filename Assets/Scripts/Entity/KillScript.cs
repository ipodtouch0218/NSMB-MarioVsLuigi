using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillScript : MonoBehaviour {
    public string spawnAfter;
    public void Kill() {
        Destroy(gameObject);
        if (transform.parent != null)
            Destroy(transform.parent.gameObject);

        if (spawnAfter.Length > 0)
            Instantiate(Resources.Load("Prefabs/" + spawnAfter), transform.position, Quaternion.identity);
    }
}
