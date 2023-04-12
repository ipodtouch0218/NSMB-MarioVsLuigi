using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;

public class SplashScreenText : MonoBehaviour
{
    //Public Variables
    public TextMeshProUGUI uiSplashScreen;
    public Gradient rainbowGradient;
    public TextAsset textFile;
    public float maxSize;
    public float minSize;
    public float pulseRate;
    public string filePath;

    //Private Variables
    private bool isGrowing = true;
    private List<string> splashTexts;
    private float rainbowTime;
    private GameObject uiSplashGO;
    void Start()
    {

        //Read in the file line by line - https://stackoverflow.com/questions/1262965/how-do-i-read-a-specified-line-in-a-text-file
        splashTexts = new List<string>();
        using (StreamReader reader = new StreamReader(filePath)) {
            string line;
            while((line = reader.ReadLine()) != null)
            {
                splashTexts.Add(line);
            }
        }
        uiSplashGO = this.gameObject;
        //Select the index of the list & Print (If index is 15, then rainbow it)
        int indexToPrint = Random.Range(0, splashTexts.Count -1);
        uiSplashScreen.text = splashTexts[indexToPrint];
        if(indexToPrint == 15)
        {
            InvokeRepeating("Rainbow", 0, 0.01f);
        }
    }

    private void Update()
    {
        SizeAdjust();
    }

    private void SizeAdjust()
    {
        if (isGrowing) {
            uiSplashGO.transform.localScale = new Vector3(uiSplashGO.transform.localScale.x + Time.deltaTime * pulseRate, uiSplashGO.transform.localScale.y + Time.deltaTime * pulseRate, uiSplashGO.transform.localScale.z);
            if (transform.localScale.x >= maxSize) {
                isGrowing = false;
            }
        }
        else
        {
            uiSplashGO.transform.localScale = new Vector3(uiSplashGO.transform.localScale.x - Time.deltaTime * pulseRate, uiSplashGO.transform.localScale.y - Time.deltaTime * pulseRate, uiSplashGO.transform.localScale.z);
            if (transform.localScale.x <= minSize) {
                isGrowing = true;
            }
        }
    }
    private void Rainbow()
    {
        rainbowTime += Time.deltaTime;
        if(rainbowTime > 1f)
        {
            rainbowTime -= 1f;
        }
        uiSplashScreen.color = rainbowGradient.Evaluate(rainbowTime);
    }

}
