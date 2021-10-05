using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillScript : MonoBehaviour
{
    [SerializeField] string playSound = null;
    public void Kill() {
        GameObject.Destroy(gameObject);
        if (transform.parent != null)
            GameObject.Destroy(transform.parent.gameObject);

        if (playSound != null && playSound != "" && GetComponent<AudioSource>() != null) {
            GetComponent<AudioSource>().PlayOneShot((AudioClip) Resources.Load("Sound/" + playSound));
        }
    }
}
