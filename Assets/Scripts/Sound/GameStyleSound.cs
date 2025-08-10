using UnityEngine;
using NSMB.Utilities;

public class GameStyleSound : MonoBehaviour
{
    public AudioClip[] SoundList;
    public AudioSource AudioOutput;

    void Start()
    {
        AudioOutput.clip = SoundList[(int) Utils.GetStageTheme()];
        AudioOutput.Play();
    }
}
