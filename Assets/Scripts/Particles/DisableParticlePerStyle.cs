using NSMB.Utilities;
using UnityEngine;

public class DisableParticlePerStyle : MonoBehaviour {
    public ParticleSystem[] ListOfObject;

    void Start() {
        switch ((int) Utils.GetStageTheme()) {
        case 1:
            foreach (var die in ListOfObject) {
                //die.transform.localPosition = new Vector3(-9999999f, -9999999f, 0);
                die.transform.localScale = Vector3.zero;
            }
            break;
        }
    }
}
