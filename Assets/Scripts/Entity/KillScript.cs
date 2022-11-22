using UnityEngine;

public class KillScript : MonoBehaviour {

    [SerializeField] private GameObject spawnAfter;

    public void Kill() {
        Destroy(gameObject);
        if (transform.parent != null)
            Destroy(transform.parent.gameObject);

        if (spawnAfter)
            Instantiate(spawnAfter, transform.position, Quaternion.identity);
    }
}
