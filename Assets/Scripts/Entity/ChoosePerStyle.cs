using UnityEngine;
using NSMB.Utilities;
using System.Security.Cryptography;

public class ChoosePerStyle : MonoBehaviour
{
    public GameObject[] ListOfObject;
    public int NoOfFrames = 0;
    private int Incr;
    void Start() {
        DoIt();
    }

    void Update() {
        if (NoOfFrames >= 99999 || Incr < NoOfFrames) {
            DoIt();
        }
        Incr += 1;
    }

    void DoIt()
    {
        foreach (var die in ListOfObject) {
            die.SetActive(false);
        }
        ListOfObject[(int) Utils.GetStageTheme()].SetActive(true);
    }
}
