using UnityEngine;

public class TemporarySoundSource : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private AudioSource sfx;

    public void Initialize(SoundEffect sound) {
        Initialize(sound.GetClip());
    }

    public void Initialize(AudioClip clip) {
        sfx.clip = clip;
        sfx.Play();
        Destroy(gameObject, clip.length + 0.5f);
    }
}
