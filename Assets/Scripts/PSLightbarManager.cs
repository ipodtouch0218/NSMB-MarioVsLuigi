using NSMB;
using Quantum;
using UnityEngine;
using UnityEngine.InputSystem.DualShock;

public class PSLightbarManager : MonoBehaviour {

    public Color[] colorArray = { Color.red, Color.green };

    public void Start()
    {
        if (Settings.Instance.lightBarColorSettingIndex > 0)
        {
            QuantumUnityDB.TryGetGlobalAsset(NSMB.Settings.Instance.generalCharacter, out var character);

            SetLightbarColor(character.lightBarColors[0]);
        }
    }
    public void SetLightbarColor(Color lightBarColor)
    {
        DualShockGamepad psController = DualShockGamepad.current;
        if (psController != null) {
            psController.SetLightBarColor(lightBarColor);
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

    public enum ColorSettings: int
    {
        Off,
        BrotherColor,
        PowerupColorSimple,
        PowerupColorBlink,
        Slot_TeamColor
    }

}
