using UnityEngine;

public class MaterialDuplicator : MonoBehaviour {

    public void Awake() {
        Renderer renderer = GetComponent<Renderer>();
        renderer.material = new(renderer.material);
    }
}
