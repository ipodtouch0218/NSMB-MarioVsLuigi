using UnityEngine;
using UnityEngine.InputSystem.DualShock;

public class PSLightbarManager : MonoBehaviour {

    public Color[] colorArray = { Color.red, Color.green };

    public void Start()
    {
        SetLightbarColor(NSMB.Settings.Instance.lightBarColorIndex);
    }
    public void SetLightbarColor(int lightBarColorIndex)
    {
        DualShockGamepad psController = DualShockGamepad.current;
        if (psController != null) {
            psController.SetLightBarColor(colorArray[lightBarColorIndex]);
        }
    }

    public void ClearLightbarColor()
    {
        DualShockGamepad psController = DualShockGamepad.current;
        if (psController != null)
        {
            psController.SetLightBarColor(Color.clear);
        }
    }

}
