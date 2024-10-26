using UnityEngine;

public class TemporarySoundSource : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private AudioSource sfx;

    public void Start() {
        Destroy(gameObject, sfx.clip.length + 0.5f);
    }
}
